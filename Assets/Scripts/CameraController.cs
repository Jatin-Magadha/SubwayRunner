using UnityEngine;

/// <summary>
/// Smooth follow camera for an endless runner.
///
/// Features:
///   - Smooth position follow with SmoothDamp (no jitter)
///   - Slight Z lean forward as speed increases (sense of acceleration)
///   - Roll tilt when the player changes lanes (dynamic feel)
///   - FOV increase at high speed (tunnel vision effect)
///   - Death zoom-out when game ends
///
/// SETUP: Attach to Main Camera. Assign the player Transform.
/// The camera does NOT need to be a child of the player — it follows via script.
/// </summary>
public class CameraController : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    //  INSPECTOR
    // ════════════════════════════════════════════════════════════════════════

    [Header("Target")]
    [Tooltip("Assign the Player root Transform.")]
    public Transform player;

    [Header("Follow Offset")]
    [Tooltip("Camera position relative to the player (local space). " +
             "Typical: X=0, Y=3, Z=-6 for a behind-and-above view.")]
    public Vector3 positionOffset = new Vector3(0f, 3f, -6f);

    [Header("Follow Smoothing")]
    [Tooltip("Position smooth time — lower = tighter follow, higher = floaty.")]
    public float positionSmoothTime = 0.12f;

    [Tooltip("Rotation smooth speed (degrees per second lerp).")]
    public float rotationSmoothSpeed = 8f;

    [Header("Look At")]
    [Tooltip("World-space offset from player the camera looks toward. " +
             "Raise Y to look slightly above the player's feet.")]
    public Vector3 lookAtOffset = new Vector3(0f, 1f, 4f);

    [Header("Speed Lean")]
    [Tooltip("Maximum forward pitch (X rotation) added at max game speed. " +
             "Gives a sense of leaning into acceleration.")]
    public float maxSpeedLeanAngle = 4f;

    [Header("Lane Change Tilt")]
    [Tooltip("Maximum camera roll (Z rotation) when the player changes lanes.")]
    public float maxLaneTiltAngle = 3.5f;

    [Tooltip("How quickly the tilt builds up when a lane change starts.")]
    public float tiltBuildSpeed = 10f;

    [Tooltip("How quickly the tilt returns to zero after the lane settles.")]
    public float tiltReturnSpeed = 6f;

    [Header("FOV")]
    public float baseFOV  = 65f;

    [Tooltip("Extra FOV added at maximum game speed.")]
    public float maxExtraFOV = 10f;

    [Tooltip("FOV change smooth time.")]
    public float fovSmoothTime = 0.3f;

    [Header("Death Camera")]
    [Tooltip("How far the camera pulls back (Z) when the player dies.")]
    public float deathPullbackZ = -3f;

    [Tooltip("How fast the death pull-back moves.")]
    public float deathPullbackSpeed = 2f;

    // ════════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ════════════════════════════════════════════════════════════════════════

    private Camera cam;
    private Vector3 posVelocity;     // SmoothDamp velocity for position
    private float   fovVelocity;     // SmoothDamp velocity for FOV

    private float currentTilt;       // current Z roll from lane change
    private float targetTilt;        // tilt we're moving toward
    private float lastPlayerX;       // detect lateral movement direction

    private bool  isDead;
    private Vector3 deathOffset;

    // ════════════════════════════════════════════════════════════════════════
    //  INIT
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        cam.fieldOfView = baseFOV;
    }

    private void Start()
    {
        // Auto-find player if not assigned
        if (player == null && GameManager.Instance?.player != null)
            player = GameManager.Instance.player.transform;

        if (player != null)
        {
            lastPlayerX = player.position.x;
            // Snap to correct start position immediately (no smooth from world origin)
            transform.position = player.position + positionOffset;
        }

        deathOffset = Vector3.zero;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  LATE UPDATE  (runs after player moves, so no one-frame lag)
    // ════════════════════════════════════════════════════════════════════════

    private void LateUpdate()
    {
        if (player == null) return;

        bool playing = GameManager.Instance != null &&
                       GameManager.Instance.CurrentState == GameManager.GameState.Playing;

        bool gameOver = GameManager.Instance != null &&
                        GameManager.Instance.CurrentState == GameManager.GameState.GameOver;

        if (gameOver && !isDead)
        {
            isDead = true;
        }

        if (isDead)
        {
            HandleDeathCamera();
            return;
        }

        UpdatePosition(playing);
        UpdateRotation(playing);
        UpdateFOV(playing);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  POSITION
    // ════════════════════════════════════════════════════════════════════════

    private void UpdatePosition(bool playing)
    {
        // Target position = player world pos + fixed offset
        Vector3 targetPos = player.position + positionOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position, targetPos, ref posVelocity, positionSmoothTime);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ROTATION
    // ════════════════════════════════════════════════════════════════════════

    private void UpdateRotation(bool playing)
    {
        // ── Base look-at ────────────────────────────────────────────────────
        Vector3 lookTarget = player.position + lookAtOffset;
        Quaternion lookRot  = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);

        // ── Speed lean (pitch forward) ───────────────────────────────────────
        float speedT        = GameManager.Instance != null
            ? Mathf.InverseLerp(0f, GameManager.Instance.maxSpeed, GameManager.Instance.CurrentSpeed)
            : 0f;
        float leanAngle     = playing ? maxSpeedLeanAngle * speedT : 0f;
        Quaternion leanRot  = Quaternion.Euler(leanAngle, 0f, 0f);

        // ── Lane change tilt (roll) ──────────────────────────────────────────
        float playerXDelta  = player.position.x - lastPlayerX;
        lastPlayerX         = player.position.x;

        // If the player is moving laterally, build tilt in that direction
        if (Mathf.Abs(playerXDelta) > 0.01f)
        {
            // Negative tilt on right move (camera rolls right), positive on left
            targetTilt = -Mathf.Sign(playerXDelta) * maxLaneTiltAngle;
        }
        else
        {
            targetTilt = 0f;
        }

        currentTilt = Mathf.Lerp(currentTilt, targetTilt,
            Time.deltaTime * (Mathf.Abs(targetTilt) > 0.01f ? tiltBuildSpeed : tiltReturnSpeed));

        Quaternion tiltRot = Quaternion.Euler(0f, 0f, currentTilt);

        // ── Combine and smooth ───────────────────────────────────────────────
        Quaternion finalRot = lookRot * leanRot * tiltRot;
        transform.rotation  = Quaternion.Slerp(transform.rotation, finalRot,
                                  Time.deltaTime * rotationSmoothSpeed);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  FOV
    // ════════════════════════════════════════════════════════════════════════

    private void UpdateFOV(bool playing)
    {
        float speedT   = GameManager.Instance != null
            ? Mathf.InverseLerp(0f, GameManager.Instance.maxSpeed, GameManager.Instance.CurrentSpeed)
            : 0f;
        float targetFOV = playing ? baseFOV + maxExtraFOV * speedT : baseFOV;
        cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFOV, ref fovVelocity, fovSmoothTime);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DEATH CAMERA
    // ════════════════════════════════════════════════════════════════════════

    private void HandleDeathCamera()
    {
        // Smoothly pull the camera back and stop following Z
        deathOffset = Vector3.MoveTowards(deathOffset,
            new Vector3(0f, 1f, deathPullbackZ),
            Time.unscaledDeltaTime * deathPullbackSpeed);

        // Keep following player X/Y but freeze on Z (player stopped)
        Vector3 frozenTarget = player.position + positionOffset + deathOffset;
        transform.position   = Vector3.SmoothDamp(transform.position, frozenTarget,
                                   ref posVelocity, positionSmoothTime, Mathf.Infinity,
                                   Time.unscaledDeltaTime);

        // Look at where the player is
        Vector3 lookTarget  = player.position + Vector3.up;
        Quaternion targetRot = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        transform.rotation  = Quaternion.Slerp(transform.rotation, targetRot,
                                  Time.unscaledDeltaTime * rotationSmoothSpeed);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC HELPERS
    // ════════════════════════════════════════════════════════════════════════

    /// Call this to hard-snap the camera to the player with no smooth transition
    /// (e.g. after scene load or player respawn).
    public void SnapToPlayer()
    {
        if (player == null) return;
        transform.position = player.position + positionOffset;
        posVelocity        = Vector3.zero;
        isDead             = false;
        deathOffset        = Vector3.zero;
    }
}