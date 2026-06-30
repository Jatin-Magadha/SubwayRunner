using UnityEngine;

/// <summary>
/// Handles 3-lane runner movement: left/right lane switching, jump, slide,
/// gravity, and collision detection with obstacles/coins/trains.
/// Works with swipe input (mobile) or arrow keys (testing in editor).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Lane Settings")]
    public float laneDistance = 2.5f;   // distance between lanes
    public float laneChangeSpeed = 12f; // how fast player slides between lanes
    private int currentLane = 0;        // -1 = left, 0 = center, 1 = right

    [Header("Jump / Slide")]
    public float jumpForce = 9f;
    public float gravity = -25f;
    public float slideDuration = 0.6f;

    [Header("Animation (optional)")]
    public Animator animator;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isSliding;
    private float slideTimer;

    // Original collider size, used to shrink collider while sliding
    private Vector3 originalCenter;
    private float originalHeight;

    [Header("Swipe Input")]
    public float minSwipeDistance = 50f; // pixels
    private Vector2 touchStartPos;
    private bool trackingTouch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        originalCenter = controller.center;
        originalHeight = controller.height;
    }

    public void ResetPlayer()
    {
        currentLane = 0;
        velocity = Vector3.zero;
        isSliding = false;
        slideTimer = 0f;
        controller.center = originalCenter;
        controller.height = originalHeight;
        transform.position = new Vector3(0f, transform.position.y, transform.position.z);
        if (animator) animator.Rebind();
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        HandleInput();
        HandleSlideTimer();

        // --- Combine all movement into ONE controller.Move() call per frame ---
        // Mixing direct transform.position writes with controller.Move() causes
        // the CharacterController to fight/override lane changes. Build a single
        // displacement vector instead.

        // 1. Lane (x) - move toward target lane position smoothly
        float targetX = currentLane * laneDistance;
        float deltaX = Mathf.MoveTowards(transform.position.x, targetX, Time.deltaTime * laneChangeSpeed * laneDistance) - transform.position.x;

        // 2. Vertical (y) - gravity/jump
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        // 3. Forward (z)
        float forwardSpeed = GameManager.Instance.CurrentSpeed;

        Vector3 frameMovement = new Vector3(deltaX, velocity.y * Time.deltaTime, forwardSpeed * Time.deltaTime);
        controller.Move(frameMovement);

        if (animator) animator.SetBool("IsGrounded", isGrounded);
    }

    // ---------------- INPUT ----------------

    private void HandleInput()
    {
        // Keyboard (editor testing)
        if (Input.GetKeyDown(KeyCode.LeftArrow)) ChangeLane(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) ChangeLane(1);
        if (Input.GetKeyDown(KeyCode.UpArrow)) Jump();
        if (Input.GetKeyDown(KeyCode.DownArrow)) Slide();

        // Touch / swipe
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
                {
                    ChangeLane(delta.x > 0 ? 1 : -1);
                }
                else
                {
                    if (delta.y > 0) Jump();
                    else Slide();
                }
            }
        }
    }

    private void ChangeLane(int direction)
    {
        int targetLane = Mathf.Clamp(currentLane + direction, -1, 1);
        currentLane = targetLane;
    }

    private void Jump()
    {
        if (!isGrounded || isSliding) return;
        velocity.y = jumpForce;
        isGrounded = false;
        if (animator) animator.SetTrigger("Jump");
    }

    private void Slide()
    {
        if (isSliding || !isGrounded) return;
        isSliding = true;
        slideTimer = slideDuration;

        // Shrink collider so player can pass under low obstacles
        controller.height = originalHeight * 0.5f;
        controller.center = new Vector3(originalCenter.x, originalCenter.y * 0.5f, originalCenter.z);

        if (animator) animator.SetTrigger("Slide");
    }

    private void HandleSlideTimer()
    {
        if (!isSliding) return;
        slideTimer -= Time.deltaTime;
        if (slideTimer <= 0f)
        {
            isSliding = false;
            controller.height = originalHeight;
            controller.center = originalCenter;
        }
    }

    // ---------------- COLLISIONS ----------------

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Coin"))
        {
            CoinCollector.HandleCoinCollected(other.gameObject);
        }
        else if (other.CompareTag("Obstacle"))
        {
            // Allow slide-under / jump-over obstacles tagged specifically
            ObstacleHit obstacle = other.GetComponent<ObstacleHit>();
            if (obstacle != null && obstacle.CanBeAvoided(isSliding, !isGrounded))
            {
                return; // successfully avoided
            }
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