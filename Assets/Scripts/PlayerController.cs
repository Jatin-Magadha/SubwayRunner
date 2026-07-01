using UnityEngine;

/// <summary>
/// Handles 3-lane runner movement: left/right lane switching, jump, slide,
/// gravity, and collision detection with obstacles/coins/trains.
///
/// Cancel moves (matching the original game feel):
///   Jump → Slide: swipe/press down while airborne → slams player to ground fast,
///                 then auto-enters slide the moment they land.
///   Slide → Jump: swipe/press up while sliding → instantly cancels slide,
///                 restores collider, and fires a full jump.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Lane Settings")]
    public float laneDistance    = 2.5f;
    public float laneChangeSpeed = 12f;
    private int currentLane = 0;   // -1 left | 0 center | 1 right

    [Header("Jump / Slide")]
    public float jumpForce           = 9f;
    public float gravity             = -25f;
    public float slideDuration       = 0.6f;

    [Tooltip("Extra downward force added when the player cancels a jump into a slide mid-air." +
             " Higher = hits the ground faster.")]
    public float jumpCancelSlamForce = 22f;

    [Header("Animation (optional)")]
    public Animator animator;

    // ── Components ──────────────────────────────────────────────────────────
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private bool wasGrounded;          // track landing frame

    // ── Slide state ─────────────────────────────────────────────────────────
    private bool  isSliding;
    private float slideTimer;

    // ── Jump-cancel-into-slide ───────────────────────────────────────────────
    // Player pressed Down while airborne: slam them to ground, then auto-slide.
    private bool isSlamming;
    private bool pendingSlideOnLand;

    // ── Collider cache ───────────────────────────────────────────────────────
    private Vector3 originalCenter;
    private float   originalHeight;

    // ── Swipe input ─────────────────────────────────────────────────────────
    [Header("Swipe Input")]
    public float minSwipeDistance = 50f;
    private Vector2 touchStartPos;
    private bool    trackingTouch;

    // ────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        controller     = GetComponent<CharacterController>();
        originalCenter = controller.center;
        originalHeight = controller.height;
    }

    public void ResetPlayer()
    {
        currentLane        = 0;
        velocity           = Vector3.zero;
        isSliding          = false;
        isSlamming         = false;
        pendingSlideOnLand = false;
        slideTimer         = 0f;
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

        DetectLanding();
        HandleInput();
        HandleSlideTimer();
        ApplyMovement();
    }

    // ────────────────────────────────────────────────────────────────────────
    //  LANDING DETECTION
    //  Runs before input so pendingSlideOnLand is consumed this same frame.
    // ────────────────────────────────────────────────────────────────────────

    private void DetectLanding()
    {
        bool justLanded = isGrounded && !wasGrounded;

        if (justLanded)
        {
            isSlamming = false;

            if (pendingSlideOnLand)
            {
                pendingSlideOnLand = false;
                BeginSlide();          // auto-slide on landing after slam
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  INPUT
    // ════════════════════════════════════════════════════════════════════════

    private void HandleInput()
    {
        // ── Keyboard (editor / desktop) ──────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.LeftArrow))  ChangeLane(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) ChangeLane(1);
        if (Input.GetKeyDown(KeyCode.UpArrow))    OnUpInput();
        if (Input.GetKeyDown(KeyCode.DownArrow))  OnDownInput();

        // ── Touch / swipe ────────────────────────────────────────────────────
        if (Input.touchCount > 0)
        {
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
                    ChangeLane(delta.x > 0 ? 1 : -1);
                else if (delta.y > 0)
                    OnUpInput();
                else
                    OnDownInput();
            }
        }
    }

    // ── Unified directional handlers ─────────────────────────────────────────

    /// Up input:
    ///   • Grounded + not sliding → normal jump
    ///   • Sliding → CANCEL slide immediately, then jump  (Slide → Jump cancel)
    ///   • Airborne (and not already slamming) → ignored (no double-jump)
    private void OnUpInput()
    {
        if (isSliding)
        {
            // ── Slide → Jump cancel ───────────────────────────────────────────
            CancelSlide();
            ForceJump();
        }
        else if (isGrounded)
        {
            ForceJump();
        }
        // airborne + not sliding: ignore (extend to double-jump here if desired)
    }

    /// Down input:
    ///   • Grounded + not sliding → normal slide
    ///   • Airborne (not slamming) → CANCEL jump, slam down  (Jump → Slide cancel)
    ///   • Already slamming → ignore (already committed)
    ///   • Already sliding  → ignore
    private void OnDownInput()
    {
        if (!isGrounded && !isSlamming)
        {
            // ── Jump → Slide cancel ───────────────────────────────────────────
            // Spike velocity downward and queue a slide for when we land.
            velocity.y        = -jumpCancelSlamForce;
            isSlamming        = true;
            pendingSlideOnLand = true;

            if (animator) animator.SetTrigger("Slam"); // optional "tuck" animation
        }
        else if (isGrounded && !isSliding)
        {
            BeginSlide();
        }
    }

    private void ChangeLane(int direction)
    {
        currentLane = Mathf.Clamp(currentLane + direction, -1, 1);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  JUMP & SLIDE INTERNALS
    // ════════════════════════════════════════════════════════════════════════

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

        // Shrink collider so the player can pass under low obstacles
        float slideHeight    = originalHeight * 0.5f;

        // Keep the BOTTOM of the collider pinned at its original floor position.
        // CharacterController bottom = center.y - height/2
        // So: newCenter.y = originalBottom + newHeight/2
        float originalBottom = originalCenter.y - originalHeight * 0.5f;
        float slideCenterY   = originalBottom + slideHeight * 0.5f;

        controller.height = slideHeight;
        controller.center = new Vector3(originalCenter.x, slideCenterY, originalCenter.z);

        if (animator) animator.SetTrigger("Slide");
    }

    /// Instantly end an active slide — used by the Slide → Jump cancel.
    private void CancelSlide()
    {
        isSliding  = false;
        slideTimer = 0f;
        RestoreCollider();
        // No "EndSlide" trigger needed; Jump trigger will override the anim.
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
    //  MOVEMENT (all axes in one Move call)
    // ════════════════════════════════════════════════════════════════════════

    private void ApplyMovement()
    {
        // 1. Lane (X) ─ lerp toward target lane
        float targetX = currentLane * laneDistance;
        float deltaX  = Mathf.MoveTowards(transform.position.x, targetX,
                            Time.deltaTime * laneChangeSpeed * laneDistance)
                        - transform.position.x;

        // 2. Vertical (Y) ─ gravity always applies; slam amplifies it
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;                         // keep grounded flag stable

        velocity.y += gravity * Time.deltaTime;

        // 3. Forward (Z)
        float forwardSpeed = GameManager.Instance.CurrentSpeed;

        controller.Move(new Vector3(deltaX,
                                    velocity.y * Time.deltaTime,
                                    forwardSpeed * Time.deltaTime));

        if (animator)
        {
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetBool("IsSliding",  isSliding);
            animator.SetBool("IsSlamming", isSlamming);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  COLLISIONS
    // ════════════════════════════════════════════════════════════════════════

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Coin"))
        {
            CoinCollector.HandleCoinCollected(other.gameObject);
        }
        else if (other.CompareTag("Obstacle"))
        {
            ObstacleHit obstacle = other.GetComponent<ObstacleHit>();
            if (obstacle != null && obstacle.CanBeAvoided(isSliding, !isGrounded))
                return; // successfully avoided

            HandleDeath();
        }
        else if (other.CompareTag("PowerUp"))
        {
            PowerUp powerUp = other.GetComponent<PowerUp>();
            powerUp?.Activate(this);
            other.gameObject.SetActive(false);
        }
    }

    private void HandleDeath()
    {
        if (animator) animator.SetTrigger("Death");
        GameManager.Instance.TriggerGameOver();
    }
}