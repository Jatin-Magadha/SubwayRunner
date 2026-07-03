using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// A pursuer character that chases the player from behind.
/// Stays at a configurable Z distance, slowly closes the gap over time,
/// and lunges forward when the player stumbles.
///
/// SETUP:
///   1. Place your chaser character prefab in the scene (behind the player).
///   2. Assign player transform and this script's inspector fields.
///   3. Add a ChaserWarningUI child panel with an Image for the danger tint.
///   4. (Optional) Add an AudioSource to this GameObject for warning sounds.
///
/// GAME OVER: if gap drops to or below catchDistance, TriggerGameOver() fires.
/// </summary>
public class ChaserController : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    //  INSPECTOR
    // ════════════════════════════════════════════════════════════════════════

    [Header("References")]
    public Transform player;

    [Header("Starting Position")]
    [Tooltip("How far behind the player the chaser starts (world units).")]
    public float startDistance = 22f;

    [Tooltip("Chaser's Y position (keep it on the ground like the player).")]
    public float chaserY = 0f;

    [Header("Chase Speed")]
    [Tooltip("How many units per second the chaser GAINS on the player. " +
             "At 0 the gap holds steady; positive values close it over time.")]
    public float gapCloseRatePerSecond = 0.8f;

    [Tooltip("Extra units per second gained per minute of play — escalates pressure.")]
    public float gapCloseRateEscalation = 0.15f;

    [Tooltip("Maximum gap-close rate regardless of play time.")]
    public float maxGapCloseRate = 5f;

    [Header("Stumble Reaction")]
    [Tooltip("Distance gained instantly when the player stumbles.")]
    public float stumbleLunge = 6f;

    [Tooltip("Smooth time for the lunge movement (lower = snappier).")]
    public float lungeSmoothTime = 0.15f;

    [Header("Catch Distance")]
    [Tooltip("Gap at which the chaser is considered to have caught the player → Game Over.")]
    public float catchDistance = 1.5f;

    [Header("Warning Thresholds")]
    [Tooltip("Gap at which the 'danger close' warning starts showing.")]
    public float warningDistance = 10f;

    [Tooltip("Gap at which the warning reaches full intensity.")]
    public float dangerDistance = 4f;

    [Header("Warning UI")]
    [Tooltip("A full-screen Image (transparent → red) for the danger vignette. " +
             "Assign a UI Image with a red tint and alpha 0 at rest.")]
    public Image dangerVignetteImage;

    [Tooltip("Maximum alpha of the danger vignette (0–1).")]
    [Range(0f, 1f)]
    public float maxVignetteAlpha = 0.45f;

    [Header("Warning Audio")]
    public AudioClip warningLoopClip;   // heartbeat / alarm loop
    [Range(0f, 1f)]
    public float maxWarningVolume = 0.7f;

    [Header("Chaser Animation")]
    public Animator chaserAnimator;

    [Tooltip("Speed parameter name in the chaser's Animator (float, 0–1).")]
    public string animSpeedParam = "ChaseSpeed";

    [Tooltip("Lunge trigger name in the chaser's Animator.")]
    public string animLungeTrigger = "Lunge";

    // ════════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ════════════════════════════════════════════════════════════════════════

    private float currentGap;           // distance behind player (positive = behind)
    private float targetGap;            // gap we're smoothing toward
    private float gapVelocity;          // used by SmoothDamp for lunge
    private float playTime;
    private bool  isActive;

    private AudioSource audioSource;

    // ════════════════════════════════════════════════════════════════════════
    //  INIT
    // ════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop        = true;
        audioSource.playOnAwake = false;
        audioSource.volume      = 0f;
        if (warningLoopClip) audioSource.clip = warningLoopClip;
    }

    private void Start()
    {
        if (player == null && GameManager.Instance?.player != null)
            player = GameManager.Instance.player.transform;

            DeactivateChaser();
    }

    // Called by GameManager when the game starts
    public void ResetChaser()
    {
        currentGap = startDistance;
        targetGap  = startDistance;
        gapVelocity = 0f;
        playTime   = 0f;
        isActive   = false;

        PositionBehindPlayer(currentGap);

        SetVignetteAlpha(0f);
        if (audioSource)
            audioSource.volume = 0f;
        if (warningLoopClip) audioSource.Play();
    }

    public void ActivateChaser() => isActive = true;
    public void DeactivateChaser()
    {
        isActive = false;
        if (audioSource)
            audioSource.volume = 0f;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  UPDATE
    // ════════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (!isActive) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;
        if (player == null) return;

        playTime += Time.deltaTime;

        // -- Close the gap progressively over time ---------------------------
        float currentCloseRate = Mathf.Min(
            gapCloseRatePerSecond + gapCloseRateEscalation * (playTime / 60f),
            maxGapCloseRate);

        targetGap -= currentCloseRate * Time.deltaTime;
        targetGap  = Mathf.Max(targetGap, catchDistance - 0.1f);

        // Smooth movement (lunge also feeds into targetGap)
        currentGap = Mathf.SmoothDamp(currentGap, targetGap, ref gapVelocity, lungeSmoothTime);

        // -- Position chaser behind player -----------------------------------
        PositionBehindPlayer(currentGap);

        // -- Animator --------------------------------------------------------
        if (chaserAnimator)
        {
            float normalizedSpeed = Mathf.InverseLerp(startDistance, catchDistance, currentGap);
            chaserAnimator.SetFloat(animSpeedParam, normalizedSpeed);
        }

        // -- Warning UI + Audio ----------------------------------------------
        UpdateWarnings();

        // -- Catch check -----------------------------------------------------
        if (currentGap <= catchDistance)
            OnChaserCaughtPlayer();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  STUMBLE REACTION (called by PlayerController.BeginStumble)
    // ════════════════════════════════════════════════════════════════════════

    public void OnPlayerStumbled()
    {
        // Instantly reduce the target gap — creates a lunge effect via SmoothDamp
        targetGap -= stumbleLunge;
        targetGap  = Mathf.Max(targetGap, catchDistance);

        if (chaserAnimator) chaserAnimator.SetTrigger(animLungeTrigger);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  POSITIONING
    // ════════════════════════════════════════════════════════════════════════

    private void PositionBehindPlayer(float gap)
    {
        if (player == null) return;
        Vector3 pos = new Vector3(
            0f,                          // chaser stays on centre X (not lane-specific)
            chaserY,
            player.position.z - gap);    // gap units behind player's Z
        transform.position = pos;
        transform.forward  = Vector3.forward;   // always faces the same direction as player
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WARNING FEEDBACK
    // ════════════════════════════════════════════════════════════════════════

    private void UpdateWarnings()
    {
        if (currentGap >= warningDistance)
        {
            SetVignetteAlpha(0f);
            if (audioSource)
                audioSource.volume = 0f;
            return;
        }

        // t = 0 at warningDistance, 1 at dangerDistance
        float t = 1f - Mathf.InverseLerp(dangerDistance, warningDistance, currentGap);
        t = Mathf.Clamp01(t);

        SetVignetteAlpha(t * maxVignetteAlpha);
        if (audioSource)
            audioSource.volume = t * maxWarningVolume;

        // Optional: pulse the vignette using a sine wave for heartbeat feel
        if (dangerVignetteImage != null)
        {
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * Mathf.PI * (2f + t * 4f));
            Color c = dangerVignetteImage.color;
            c.a = t * maxVignetteAlpha * pulse;
            dangerVignetteImage.color = c;
        }
    }

    private void SetVignetteAlpha(float alpha)
    {
        if (dangerVignetteImage == null) return;
        Color c = dangerVignetteImage.color;
        c.a = alpha;
        dangerVignetteImage.color = c;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  CAUGHT
    // ════════════════════════════════════════════════════════════════════════

    private void OnChaserCaughtPlayer()
    {
        DeactivateChaser();
        SetVignetteAlpha(0f);
        GameManager.Instance.TriggerGameOver();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC HELPERS
    // ════════════════════════════════════════════════════════════════════════

    /// Returns 0 (safe) to 1 (caught) — useful for external UI bars.
    public float GetDangerLevel() =>
        1f - Mathf.Clamp01((currentGap - catchDistance) / (startDistance - catchDistance));

    /// How many world units behind the player the chaser currently is.
    public float CurrentGap => currentGap;
}