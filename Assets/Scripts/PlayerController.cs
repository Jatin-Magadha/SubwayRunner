using UnityEngine;

/// <summary>
/// 3-lane endless runner controller.
///
/// SIDE-CLIP LANE BOUNCE:
///   When a FullBlock/Train side-clip results in a stumble, the player is
///   immediately bounced back to their previous lane and that destination lane
///   is locked for the duration of the stumble — preventing clipping through.
///
/// STUMBLE RULES:
///   • Stumble 1: survive, bounce back to safe lane, lock destination lane
///   • Stumble 2 within stumbleWindowDuration: Fatal
///   • isStumbling / grace period: collisions ignored
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    //  INSPECTOR
    // ════════════════════════════════════════════════════════════════════════

    [Header("Lane Settings")]
    public float laneDistance    = 2.5f;
    public float laneChangeSpeed = 12f;

    [Header("Jump / Slide")]
    public float jumpForce           = 9f;
    public float gravity             = -25f;
    public float slideDuration       = 0.6f;
    public float jumpCancelSlamForce = 22f;

    [Header("Stumble Settings")]
    public float stumbleDuration        = 0.9f;
    public float stumbleWindowDuration  = 3f;
    public float stumbleGracePeriod     = 0.4f;
    [Range(0.1f, 1f)]
    public float stumbleSpeedMultiplier = 0.6f;
    public float stumbleShakeMagnitude  = 0.15f;

    [Tooltip("How quickly the player snaps back to the safe lane after a side-clip stumble.")]
    public float laneBouncebackSpeed = 20f;

    [Header("Side-Clip Detection")]
    public float laneChangeDetectThreshold = 0.3f;

    [Header("Animation")]
    public Animator animator;

    [Header("Swipe Input")]
    public float minSwipeDistance = 50f;

    // ════════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ════════════════════════════════════════════════════════════════════════

    private CharacterController controller;
    private Vector3 velocity;

    // Grounded
    private bool isGrounded;
    private bool wasGrounded;

    // Lane
    private int   currentLane  = 0;    // destination lane (-1 | 0 | 1)
    private int   previousLane = 0;    // lane before the most recent switch
    private float targetLaneX;         // world X we're moving toward
    private int   blockedLane  = -99;  // lane locked after a side-clip stumble

    // Slide
    private bool  isSliding;
    private float slideTimer;

    // Jump-cancel slam
    private bool isSlamming;
    private bool pendingSlideOnLand;

    // Stumble
    private bool  isStumbling;
    private float stumbleTimer;
    private float stumbleGraceTimer;
    private int   stumbleCount;
    private float stumbleWindowTimer;

    // Invincibility (power-up)
    public  bool  IsInvincible  { get; private set; }
    private float invincibleTimer;

    // Collider cache
    private Vector3 originalCenter;
    private float   originalHeight;

    // Touch
    private Vector2 touchStartPos;
    private bool    trackingTouch;

    // ════════════════════════════════════════════════════════════════════════
    //  INIT / RESET
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        controller     = GetComponent<CharacterController>();
        originalCenter = controller.center;
        originalHeight = controller.height;
        targetLaneX    = 0f;
    }

    public void ResetPlayer()
    {
        currentLane        = 0;
        previousLane       = 0;
        targetLaneX        = 0f;
        blockedLane        = -99;
        velocity           = Vector3.zero;
        isSliding          = false;
        isSlamming         = false;
        pendingSlideOnLand = false;
        slideTimer         = 0f;
        isStumbling        = false;
        stumbleTimer       = 0f;
        stumbleGraceTimer  = 0f;
        stumbleCount       = 0;
        stumbleWindowTimer = 0f;
        IsInvincible       = false;
        invincibleTimer    = 0f;
        RestoreCollider();
        transform.position = new Vector3(0f, transform.position.y, transform.position.z);
        if (animator) animator.Rebind();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  UPDATE
    // ════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        wasGrounded = isGrounded;
        isGrounded  = controller.isGrounded;

        TickTimers();
        DetectLanding();

        if (!isStumbling) HandleFullInput();
        else              HandleLaneInputOnly();

        HandleSlideTimer();
        ApplyMovement();
        UpdateAnimator();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  TIMERS
    // ════════════════════════════════════════════════════════════════════════

    private void TickTimers()
    {
        if (isStumbling)
        {
            stumbleTimer -= Time.deltaTime;
            if (stumbleTimer <= 0f) EndStumble();
        }

        if (stumbleGraceTimer > 0f) stumbleGraceTimer -= Time.deltaTime;

        if (stumbleWindowTimer > 0f)
        {
            stumbleWindowTimer -= Time.deltaTime;
            if (stumbleWindowTimer <= 0f) stumbleCount = 0;
        }

        if (IsInvincible)
        {
            invincibleTimer -= Time.deltaTime;
            if (invincibleTimer <= 0f) IsInvincible = false;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  INPUT
    // ════════════════════════════════════════════════════════════════════════

    private void HandleFullInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))  TryChangeLane(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) TryChangeLane(1);
        if (Input.GetKeyDown(KeyCode.UpArrow))    OnUpInput();
        if (Input.GetKeyDown(KeyCode.DownArrow))  OnDownInput();
        ReadSwipe(fullInput: true);
    }

    private void HandleLaneInputOnly()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))  TryChangeLane(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) TryChangeLane(1);
        ReadSwipe(fullInput: false);
    }

    private void ReadSwipe(bool fullInput)
    {
        if (Input.touchCount == 0) return;
        Touch touch = Input.GetTouch(0);
        if (touch.phase == TouchPhase.Began)
        {
            touchStartPos = touch.position;
            trackingTouch = true;
        }
        else if (touch.phase == TouchPhase.Ended && trackingTouch)
        {
            trackingTouch = false;
            Vector2 delta = touch.position - touchStartPos;
            if (delta.magnitude < minSwipeDistance) return;

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                TryChangeLane(delta.x > 0 ? 1 : -1);
            else if (fullInput)
            {
                if (delta.y > 0) OnUpInput();
                else             OnDownInput();
            }
        }
    }

    /// <summary>
    /// Attempts a lane change. Rejects if the destination lane is currently
    /// blocked due to a recent side-clip stumble.
    /// </summary>
    private void TryChangeLane(int dir)
    {
        int destination = Mathf.Clamp(currentLane + dir, -1, 1);
        if (destination == currentLane) return;       // already at edge
        if (destination == blockedLane) return;       // lane is locked after side-clip

        previousLane = currentLane;
        currentLane  = destination;
        targetLaneX  = currentLane * laneDistance;
    }

    private void OnUpInput()
    {
        if (isSliding) { CancelSlide(); ForceJump(); }
        else if (isGrounded) ForceJump();
    }

    private void OnDownInput()
    {
        if (!isGrounded && !isSlamming)
        {
            velocity.y         = -jumpCancelSlamForce;
            isSlamming         = true;
            pendingSlideOnLand = true;
            if (animator) animator.SetTrigger("Slam");
        }
        else if (isGrounded && !isSliding)
        {
            BeginSlide();
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  JUMP & SLIDE
    // ════════════════════════════════════════════════════════════════════════

    private void DetectLanding()
    {
        if (!isGrounded || wasGrounded) return;
        isSlamming = false;
        if (pendingSlideOnLand) { pendingSlideOnLand = false; BeginSlide(); }
    }

    private void ForceJump()
    {
        velocity.y = jumpForce;
        isGrounded = false;
        if (animator) animator.SetTrigger("Jump");
    }

    private void BeginSlide()
    {
        isSliding  = true;
        slideTimer = slideDuration;
        float newH   = originalHeight * 0.5f;
        float bottom = originalCenter.y - originalHeight * 0.5f;
        controller.height = newH;
        controller.center = new Vector3(originalCenter.x, bottom + newH * 0.5f, originalCenter.z);
        if (animator) animator.SetTrigger("Slide");
    }

    private void CancelSlide()
    {
        isSliding  = false;
        slideTimer = 0f;
        RestoreCollider();
    }

    private void HandleSlideTimer()
    {
        if (!isSliding) return;
        slideTimer -= Time.deltaTime;
        if (slideTimer <= 0f)
        {
            isSliding = false;
            RestoreCollider();
            if (animator) animator.SetTrigger("EndSlide");
        }
    }

    private void RestoreCollider()
    {
        controller.height = originalHeight;
        controller.center = originalCenter;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  COLLISION CONTEXT
    // ════════════════════════════════════════════════════════════════════════

    private CollisionContext BuildCollisionContext()
    {
        float offsetFromTarget = Mathf.Abs(transform.position.x - targetLaneX);
        return new CollisionContext
        {
            isJumping           = !isGrounded,
            isSliding           = isSliding,
            verticalVelocity    = velocity.y,
            slideTimeRemaining  = slideTimer,
            slideDuration       = slideDuration,
            isChangingLane      = offsetFromTarget > laneChangeDetectThreshold,
            laneOffsetMagnitude = offsetFromTarget
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    //  STUMBLE SYSTEM
    // ════════════════════════════════════════════════════════════════════════

    public void HandleObstacleCollision(ObstacleHit obstacle)
    {
        if (IsInvincible) return;
        if (isStumbling || stumbleGraceTimer > 0f) return;

        CollisionContext ctx = BuildCollisionContext();
        var result = obstacle.GetCollisionResult(ctx);

        switch (result)
        {
            case ObstacleHit.CollisionResult.Avoided: return;
            case ObstacleHit.CollisionResult.Stumble: TryStumble(isSideClip: ctx.isChangingLane); break;
            case ObstacleHit.CollisionResult.Fatal:   TriggerDeath(); break;
        }
    }

    private void TryStumble(bool isSideClip)
    {
        stumbleCount++;

        if (stumbleCount >= 2 && stumbleWindowTimer > 0f)
        {
            TriggerDeath();
            return;
        }

        stumbleWindowTimer = stumbleWindowDuration;
        BeginStumble(isSideClip);
    }

    private void BeginStumble(bool isSideClip)
    {
        isStumbling  = true;
        stumbleTimer = stumbleDuration;

        if (isSliding) CancelSlide();

        // ── Side-clip: bounce back and lock the blocked lane ─────────────────
        if (isSideClip)
        {
            blockedLane  = currentLane;       // lock the lane they were entering
            currentLane  = previousLane;      // snap back to where they came from
            targetLaneX  = currentLane * laneDistance;
        }

        if (animator) animator.SetTrigger("Stumble");

        CameraShake shake = Camera.main?.GetComponent<CameraShake>();
        shake?.Shake(stumbleShakeMagnitude, stumbleDuration * 0.5f);

        // Notify the chaser that player stumbled (chaser gains ground)
        ChaserController chaser = FindObjectOfType<ChaserController>();
        chaser?.OnPlayerStumbled();
    }

    private void EndStumble()
    {
        isStumbling       = false;
        stumbleTimer      = 0f;
        stumbleGraceTimer = stumbleGracePeriod;
        blockedLane       = -99;   // unlock the lane once stumble animation finishes
        if (animator) animator.SetTrigger("RecoverFromStumble");
    }

    private void TriggerDeath()
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;
        if (animator) animator.SetTrigger("Death");
        GameManager.Instance.TriggerGameOver();
    }

    public void SetInvincible(float duration)
    {
        IsInvincible    = true;
        invincibleTimer = duration;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MOVEMENT
    // ════════════════════════════════════════════════════════════════════════

    private void ApplyMovement()
    {
        // X — move toward target lane; use faster bounceback speed while stumbling
        float xSpeed = isStumbling ? laneBouncebackSpeed : laneChangeSpeed * laneDistance;
        float newX   = Mathf.MoveTowards(transform.position.x, targetLaneX,
                            Time.deltaTime * xSpeed);
        float deltaX = newX - transform.position.x;

        // Y — gravity
        if (isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        // Z — forward, slowed during stumble
        float speed = GameManager.Instance.CurrentSpeed;
        if (isStumbling) speed *= stumbleSpeedMultiplier;

        controller.Move(new Vector3(deltaX, velocity.y * Time.deltaTime, speed * Time.deltaTime));
    }

    private void UpdateAnimator()
    {
        if (!animator) return;
        animator.SetBool("IsGrounded",   isGrounded);
        animator.SetBool("IsSliding",    isSliding);
        animator.SetBool("IsSlamming",   isSlamming);
        animator.SetBool("IsStumbling",  isStumbling);
        animator.SetBool("IsInvincible", IsInvincible);
        animator.SetInteger("StumbleCount", stumbleCount);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  TRIGGERS
    // ════════════════════════════════════════════════════════════════════════

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Coin"))
            CoinCollector.HandleCoinCollected(other.gameObject);
        else if (other.CompareTag("Obstacle"))
        {
            ObstacleHit obs = other.GetComponent<ObstacleHit>();
            if (obs != null) HandleObstacleCollision(obs);
            else             TriggerDeath();
        }
        else if (other.CompareTag("PowerUp"))
        {
            other.GetComponent<PowerUp>()?.Activate(this);
            other.gameObject.SetActive(false);
        }
    }
}