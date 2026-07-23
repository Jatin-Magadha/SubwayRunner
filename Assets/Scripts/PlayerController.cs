using UnityEngine;

/// <summary>
/// 3-lane endless runner controller.
///
/// COLLISION SETUP — HYBRID (important, read carefully):
///
///   BARRIER / LOWBAR  → Is Trigger: ON,  tag "Obstacle", layer "Obstacle"
///     Player physically passes through them. OnTriggerEnter evaluates
///     whether the dodge (jump / slide) was correct → Avoided / Stumble / Fatal.
///
///   FULLBLOCK / TRAIN → Is Trigger: OFF, tag "Obstacle", layer "Obstacle"
///     Solid — physically stops the player. OnControllerColliderHit handles
///     the hit. IsLaneClear (QueryTriggerInteraction.Ignore) only detects
///     these solid obstacles, so Barrier/LowBar never block lane changes.
///
///   COINS      → Is Trigger: ON, tag "Coin"
///   POWER-UPS  → Is Trigger: ON, tag "PowerUp"
///   HOVERBOARD → Is Trigger: ON, tag "Hoverboard"
///
/// LANE CHANGE + COLLISION:
///   Three-layer defence against clipping through obstacles:
///     1. IsLaneClear() — box-cast before the switch is committed
///     2. Continuous Update check — re-validates every frame mid-transition
///     3. OnControllerColliderHit — last resort physical contact
///
///   RevertLaneChange() is the ONLY place that reverts lane state.
///   BeginStumble() does NOT touch lane state — this avoids the
///   double-revert that was snapping the player to wrong lanes.
///
///   After a revert, collisionCooldown is set immediately so the
///   follow-up OnControllerColliderHit from the same obstacle is ignored.
///
/// HOP:
///   Lane-change hop uses a separate isLaneHopping flag so it does NOT
///   set isJumping = true in BuildCollisionContext. Without this,
///   Barrier obstacles returned Avoided while the player hopped through them.
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
    [Tooltip("Small hop applied when changing lanes. Uses a separate flag so " +
             "it does not affect obstacle collision detection.")]
    public float laneChangeHopForce = 3f;

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
    public float laneBouncebackSpeed    = 20f;

    [Header("Lane Check (pre-move validation)")]
    [Tooltip("Layer mask containing all obstacle colliders (layer 'Obstacle').")]
    public LayerMask obstacleLayerMask;
    [Tooltip("How far ahead (Z) to scan before allowing a lane change.")]
    public float laneCheckDepth = 3f;
    [Tooltip("Half-width of the lane check box.")]
    public float laneCheckWidth = 0.8f;

    [Header("Collision Debounce")]
    public float collisionCooldownTime = 0.3f;

    [Header("Side-Clip Detection")]
    public float laneChangeDetectThreshold = 0.3f;

    [Header("Animation")]
    public Animator animator;

    [Header("Swipe Input")]
    public float minSwipeDistance = 50f;

    [Header("Hoverboard")]
    public HoverboardController hoverboard;

    // ════════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ════════════════════════════════════════════════════════════════════════

    private CharacterController controller;
    private Vector3 velocity;

    // Grounded
    private bool isGrounded;
    private bool wasGrounded;

    // Lane
    private int   currentLane   = 0;    // -1 | 0 | 1
    private int   previousLane  = 0;
    private float targetLaneX   = 0f;
    private int   blockedLane   = -99;
    private bool  isChangingLane;

    // Slide
    private bool  isSliding;
    private float slideTimer;

    // Jump / slam
    private bool  isSlamming;
    private bool  pendingSlideOnLand;

    // Lane-change hop — separate from the main jump so collision context
    // does not think the player is "jumping" during a lane change
    private bool  isLaneHopping;

    // Stumble
    private bool  isStumbling;
    private float stumbleTimer;
    private float stumbleGraceTimer;
    private int   stumbleCount;
    private float stumbleWindowTimer;

    // Collision debounce
    private float collisionCooldown;

    // Invincibility
    public  bool  IsInvincible { get; private set; }
    private float invincibleTimer;

    // Collider cache
    private Vector3 originalCenter;
    private float   originalHeight;

    // Touch
    private Vector2 touchStartPos;
    private bool    trackingTouch;

    // Cached references
    private ChaserController chaser;

    // ════════════════════════════════════════════════════════════════════════
    //  INIT / RESET
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        controller     = GetComponent<CharacterController>();
        originalCenter = controller.center;
        originalHeight = controller.height;

        if (hoverboard == null)
            hoverboard = GetComponent<HoverboardController>();

        chaser = FindObjectOfType<ChaserController>();
    }

    public void ResetPlayer()
    {
        currentLane        = 0;
        previousLane       = 0;
        targetLaneX        = 0f;
        blockedLane        = -99;
        isChangingLane     = false;
        velocity           = Vector3.zero;
        isSliding          = false;
        isSlamming         = false;
        isLaneHopping      = false;
        pendingSlideOnLand = false;
        slideTimer         = 0f;
        isStumbling        = false;
        stumbleTimer       = 0f;
        stumbleGraceTimer  = 0f;
        stumbleCount       = 0;
        stumbleWindowTimer = 0f;
        collisionCooldown  = 0f;
        IsInvincible       = false;
        invincibleTimer    = 0f;
        RestoreCollider();

        // Hard-snap position to lane centre
        controller.enabled = false;
        transform.position = new Vector3(0f, transform.position.y, transform.position.z);
        controller.enabled = true;

        if (animator) animator.Rebind();
        hoverboard?.ResetBoard();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  UPDATE
    // ════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        wasGrounded = isGrounded;
        isGrounded  = controller.isGrounded;

        // Clear lane-hop once back on ground
        if (isGrounded && isLaneHopping) isLaneHopping = false;

        TickTimers();
        DetectLanding();

        // ── Continuous lane validity check ────────────────────────────────
        // Every frame during a transition, re-check whether the destination
        // lane is still clear. Moving obstacles can enter after the initial
        // IsLaneClear passed. If blocked → snap back immediately.
        if (isChangingLane && !IsLaneClear(currentLane))
        {
            RevertLaneChange();
        }

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
        if (collisionCooldown > 0f) collisionCooldown -= Time.deltaTime;

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
        if (Input.GetKeyDown(KeyCode.Space))      hoverboard?.TryActivateBoard();
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

    // ════════════════════════════════════════════════════════════════════════
    //  LANE CHANGE
    // ════════════════════════════════════════════════════════════════════════

    private void TryChangeLane(int dir)
    {
        int destination = Mathf.Clamp(currentLane + dir, -1, 1);
        if (destination == currentLane) return;
        if (destination == blockedLane) return;
        if (isChangingLane) return;   // already mid-transition, ignore new input

        if (!IsLaneClear(destination))
        {
            // Obstacle ahead — give stumble feedback but stay in current lane
            if (!isStumbling && stumbleGraceTimer <= 0f && !IsInvincible)
                TryStumble();
            return;
        }

        previousLane   = currentLane;
        currentLane    = destination;
        targetLaneX    = currentLane * laneDistance;
        isChangingLane = true;

        // Small hop — uses isLaneHopping NOT the jump system,
        // so BuildCollisionContext.isJumping stays false
        if (isGrounded && !isSliding && !isSlamming)
        {
            velocity.y    = laneChangeHopForce;
            isLaneHopping = true;
            isGrounded    = false;
        }

        if (animator) animator.SetTrigger("LaneChange");
    }

    /// <summary>
    /// Checks whether the target lane is clear of obstacles ahead of the player.
    /// QueryTriggerInteraction.Ignore means coins/pickups never block lane switches.
    /// </summary>
    private bool IsLaneClear(int targetLane)
    {
        float   targetX     = targetLane * laneDistance;
        float   centerY     = transform.position.y + controller.height * 0.5f;
        float   centerZ     = transform.position.z + laneCheckDepth * 0.5f;
        Vector3 center      = new Vector3(targetX, centerY, centerZ);
        Vector3 halfExtents = new Vector3(laneCheckWidth * 0.5f,
                                          controller.height * 0.5f,
                                          laneCheckDepth  * 0.5f);

        return !Physics.CheckBox(center, halfExtents, Quaternion.identity,
                                 obstacleLayerMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Immediately cancels a lane change and hard-snaps the player back to
    /// their previous lane. This is the ONLY place that reverts lane state.
    /// collisionCooldown is set here so the follow-up OnControllerColliderHit
    /// from the same obstacle contact is ignored automatically.
    /// </summary>
    private void RevertLaneChange()
    {
        int   safeLane = previousLane;
        float safeX    = safeLane * laneDistance;

        // Update logical state BEFORE snap so any callbacks this frame
        // read consistent values
        blockedLane    = currentLane;   // prevent immediately re-entering this lane
        currentLane    = safeLane;
        previousLane   = safeLane;      // both point to safe lane — no stale state
        targetLaneX    = safeX;
        isChangingLane = false;
        isLaneHopping  = false;

        // Hard position snap — disable CC, move transform, re-enable
        controller.enabled = false;
        transform.position = new Vector3(safeX,
                                         transform.position.y,
                                         transform.position.z);
        controller.enabled = true;

        // Suppress the follow-up collision callback that fires this same frame
        collisionCooldown = collisionCooldownTime;

        // Stumble feedback
        if (!isStumbling && stumbleGraceTimer <= 0f && !IsInvincible)
            TryStumble();
    }

    private void OnUpInput()
    {
        if (isSliding) { CancelSlide(); ForceJump(); }
        else if (isGrounded) ForceJump();
    }

    private void OnDownInput()
    {
        if (!isGrounded && !isSlamming && !isLaneHopping)
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
        isSlamming    = false;
        isLaneHopping = false;
        if (pendingSlideOnLand) { pendingSlideOnLand = false; BeginSlide(); }
    }

    private void ForceJump()
    {
        velocity.y    = jumpForce;
        isGrounded    = false;
        isLaneHopping = false;   // real jump overrides any hop
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
    //  PHYSICAL COLLISION  (obstacles, Is Trigger: OFF)
    // ════════════════════════════════════════════════════════════════════════

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.moveDirection.y < -0.5f) return;              // ground/ceiling
        if (!hit.gameObject.CompareTag("Obstacle")) return;
        if (collisionCooldown > 0f) return;                   // debounce / post-revert

        // Mid lane-change hit that the continuous Update check missed
        // (e.g. obstacle entered the lane between Update ticks)
        if (isChangingLane)
        {
            RevertLaneChange();   // snaps + sets cooldown + triggers stumble
            return;
        }

        // Settled-lane collision — normal evaluation
        collisionCooldown = collisionCooldownTime;
        ObstacleHit obs = hit.gameObject.GetComponent<ObstacleHit>();
        if (obs != null) HandleObstacleCollision(obs);
        else             TriggerDeath();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  TRIGGER ENTRY  (Barrier, LowBar, coins, pickups — Is Trigger: ON)
    // ════════════════════════════════════════════════════════════════════════

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Coin"))
            CoinCollector.HandleCoinCollected(other.gameObject);

        else if (other.CompareTag("Obstacle"))
        {
            // Only Barrier and LowBar reach here (Is Trigger: ON).
            // FullBlock and Train have solid colliders and are handled
            // by OnControllerColliderHit instead.
            if (collisionCooldown > 0f) return;
            collisionCooldown = collisionCooldownTime;

            ObstacleHit obs = other.GetComponent<ObstacleHit>();
            if (obs != null) HandleObstacleCollision(obs);
            else             TriggerDeath();
        }
        else if (other.CompareTag("PowerUp"))
        {
            other.GetComponent<PowerUp>()?.Activate(this);
            other.gameObject.SetActive(false);
        }
        else if (other.CompareTag("Hoverboard"))
        {
            hoverboard?.CollectBoard(1);
            other.gameObject.SetActive(false);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  COLLISION CONTEXT
    // ════════════════════════════════════════════════════════════════════════

    private CollisionContext BuildCollisionContext()
    {
        float offsetFromTarget = Mathf.Abs(transform.position.x - targetLaneX);
        return new CollisionContext
        {
            // isJumping is TRUE only for real jump input, NOT lane-change hop
            isJumping           = !isGrounded && !isLaneHopping,
            isSliding           = isSliding,
            verticalVelocity    = velocity.y,
            slideTimeRemaining  = slideTimer,
            slideDuration       = slideDuration,
            isChangingLane      = isChangingLane,
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

        var result = obstacle.GetCollisionResult(BuildCollisionContext());

        switch (result)
        {
            case ObstacleHit.CollisionResult.Avoided:  break;
            case ObstacleHit.CollisionResult.Stumble:  TryStumble(); break;
            case ObstacleHit.CollisionResult.Fatal:    TriggerDeath(); break;
        }
    }

    private void TryStumble()
    {
        stumbleCount++;

        if (stumbleCount >= 2 && stumbleWindowTimer > 0f)
        {
            TriggerDeath();
            return;
        }

        stumbleWindowTimer = stumbleWindowDuration;
        BeginStumble();
    }

    /// <summary>
    /// Handles stumble animation, timer, and chaser notification.
    /// Does NOT touch lane state — RevertLaneChange() is solely responsible
    /// for reverting lanes. Separating these prevents double-revert bugs.
    /// </summary>
    private void BeginStumble()
    {
        isStumbling  = true;
        stumbleTimer = stumbleDuration;

        if (isSliding) CancelSlide();

        if (animator) animator.SetTrigger("Stumble");

        Camera.main?.GetComponent<CameraShake>()
              ?.Shake(stumbleShakeMagnitude, stumbleDuration * 0.5f);

        chaser?.OnPlayerStumbled();
    }

    private void EndStumble()
    {
        isStumbling       = false;
        stumbleTimer      = 0f;
        stumbleGraceTimer = stumbleGracePeriod;
        blockedLane       = -99;
        isChangingLane    = false;
        if (animator) animator.SetTrigger("RecoverFromStumble");
    }

    private void TriggerDeath()
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;
        if (hoverboard != null && hoverboard.TryAbsorbFatalHit()) return;
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
        float xSpeed = isStumbling ? laneBouncebackSpeed : laneChangeSpeed * laneDistance;
        float newX   = Mathf.MoveTowards(transform.position.x, targetLaneX,
                           Time.deltaTime * xSpeed);

        // Settle isChangingLane when X reaches target
        if (isChangingLane && Mathf.Abs(newX - targetLaneX) < 0.01f)
        {
            newX           = targetLaneX;
            isChangingLane = false;
            blockedLane    = -99;   // open all lanes once settled
        }

        float deltaX = newX - transform.position.x;

        if (isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        float speed = GameManager.Instance.CurrentSpeed;
        if (isStumbling) speed *= stumbleSpeedMultiplier;

        controller.Move(new Vector3(deltaX, velocity.y * Time.deltaTime, speed * Time.deltaTime));
    }

    private void UpdateAnimator()
    {
        if (!animator) return;
        animator.SetBool("IsGrounded",      isGrounded);
        animator.SetBool("IsSliding",       isSliding);
        animator.SetBool("IsSlamming",      isSlamming);
        animator.SetBool("IsStumbling",     isStumbling);
        animator.SetBool("IsInvincible",    IsInvincible);
        animator.SetBool("IsLaneHopping",   isLaneHopping);
        animator.SetInteger("StumbleCount", stumbleCount);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  GIZMOS
    // ════════════════════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        for (int lane = -1; lane <= 1; lane++)
        {
            float   x    = lane * laneDistance;
            float   y    = transform.position.y + controller.height * 0.5f;
            float   z    = transform.position.z + laneCheckDepth * 0.5f;
            Vector3 size = new Vector3(laneCheckWidth, controller.height, laneCheckDepth);
            Gizmos.color = (lane == currentLane) ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(new Vector3(x, y, z), size);
        }
    }
}