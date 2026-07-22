using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chaser that follows the player's lane with a lag, maintains a comfortable
/// resting distance during clean runs, and only surges forward on stumbles.
///
/// GAP MODEL:
///   currentGap starts at startDistance and slowly drifts toward restingGap.
///   Stumbles fire a lunge (targetGap -= stumbleLunge).
///   Gap can never go below catchDistance without a stumble.
///   At catchDistance → Game Over.
/// </summary>
public class ChaserController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR
    // ═══════════════════════════════════════════════════════════════════════

    [Header("References")]
    public Transform player;

    [Header("Gap Settings")]
    [Tooltip("Distance behind player when the run starts.")]
    public float startDistance  = 26f;

    [Tooltip("Gap the chaser settles into during clean running. " +
             "It drifts toward this — NOT toward catchDistance — so the " +
             "player is only caught by accumulated stumbles.")]
    public float restingGap     = 18f;

    [Tooltip("Gap at which the chaser catches the player → Game Over.")]
    public float catchDistance  = 1.5f;

    [Header("Gap Close Rates")]
    [Tooltip("Units/sec the gap closes while drifting toward restingGap. " +
             "Very low — this is just cosmetic 'closing in' during clean play.")]
    public float normalCloseRate      = 0.4f;

    [Tooltip("Units/sec the gap opens when player is ahead of restingGap. " +
             "Prevents the chaser sitting unreachably far away early in the run.")]
    public float openRate             = 1.5f;

    [Tooltip("SmoothDamp time for the lunge on stumble. Low = snappy.")]
    public float lungeSmoothTime      = 0.25f;

    [Tooltip("Units instantly added to targetGap when player stumbles " +
             "(chaser lurches forward).")]
    public float stumbleLunge         = 7f;

    [Header("Lane Following")]
    [Tooltip("How fast the chaser slides to match the player's X lane. " +
             "Lower = more lag behind lane changes (looks more natural).")]
    public float laneFollowSpeed      = 3.5f;

    [Tooltip("Y position of the chaser on the ground (match player ground Y).")]
    public float chaserY              = 0f;

    [Header("Warning Thresholds")]
    public float warningDistance      = 12f;
    public float dangerDistance       = 5f;

    [Header("Warning UI")]
    [Tooltip("Full-screen Image, red colour, alpha=0 at rest.")]
    public Image dangerVignetteImage;
    [Range(0f, 1f)] public float maxVignetteAlpha = 0.45f;

    [Header("Warning Audio")]
    public AudioClip warningLoopClip;
    [Range(0f, 1f)] public float maxWarningVolume = 0.7f;

    [Header("Animator")]
    public Animator chaserAnimator;
    public string animSpeedParam   = "ChaseSpeed";
    public string animLungeTrigger = "Lunge";

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC STATE
    // ═══════════════════════════════════════════════════════════════════════

    public bool  IsActive   { get; private set; }
    public float CurrentGap => currentGap;

    public float GetDangerLevel() =>
        1f - Mathf.Clamp01((currentGap - catchDistance) /
                            (startDistance - catchDistance));

    // ═══════════════════════════════════════════════════════════════════════
    //  PRIVATE STATE
    // ═══════════════════════════════════════════════════════════════════════

    private float currentGap;
    private float targetGap;
    private float gapVelocity;

    private float currentChaserX;   // smoothly tracks player lane X

    private AudioSource audioSource;

    // ═══════════════════════════════════════════════════════════════════════
    //  INIT
    // ═══════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        audioSource             = gameObject.AddComponent<AudioSource>();
        audioSource.loop        = true;
        audioSource.playOnAwake = false;
        audioSource.volume      = 0f;
        if (warningLoopClip) audioSource.clip = warningLoopClip;
    }

    public void ResetChaser()
    {
        if (player == null && GameManager.Instance?.player != null)
            player = GameManager.Instance.player.transform;

        IsActive        = false;
        currentGap      = startDistance;
        targetGap       = startDistance;
        gapVelocity     = 0f;
        currentChaserX  = player != null ? player.position.x : 0f;

        if (player != null) PlaceChaser();

        SetVignetteAlpha(0f);
        audioSource.volume = 0f;
    }

    public void ActivateChaser()
    {
        if (player == null) return;
        IsActive = true;
        if (warningLoopClip) audioSource.Play();
    }

    public void DeactivateChaser()
    {
        IsActive = false;
        audioSource.Stop();
        SetVignetteAlpha(0f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  UPDATE
    // ═══════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (!IsActive) return;
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;
        if (player == null) return;

        UpdateGap();
        UpdateLaneX();
        PlaceChaser();
        UpdateAnimator();
        UpdateWarnings();

        if (currentGap <= catchDistance)
            CaughtPlayer();
    }

    // ─── Gap logic ──────────────────────────────────────────────────────────

    private void UpdateGap()
    {
        // Drift targetGap toward restingGap — NOT toward catchDistance.
        // This keeps clean runs feeling tense but survivable.
        if (currentGap > restingGap)
            // Gap is bigger than resting → close it (player started ahead)
            targetGap = Mathf.MoveTowards(targetGap, restingGap,
                            openRate * Time.deltaTime);
        else
            // Gap is at or below resting → only close very slowly during clean play
            targetGap = Mathf.MoveTowards(targetGap, restingGap - 1f,
                            normalCloseRate * Time.deltaTime);

        // Hard floor — chaser can't clip through the player
        targetGap = Mathf.Max(targetGap, catchDistance);

        // Smooth current toward target (lunge feeds into targetGap instantly,
        // but currentGap catches up over lungeSmoothTime seconds)
        currentGap = Mathf.SmoothDamp(currentGap, targetGap,
                         ref gapVelocity, lungeSmoothTime);
    }

    // ─── Lane following ─────────────────────────────────────────────────────

    private void UpdateLaneX()
    {
        // Lerp chaser X toward player X with a lag so lane switches feel
        // like the chaser is reacting and catching up, not teleporting
        currentChaserX = Mathf.Lerp(currentChaserX, player.position.x,
                             Time.deltaTime * laneFollowSpeed);
    }

    // ─── Placement ──────────────────────────────────────────────────────────

    private void PlaceChaser()
    {
        transform.position = new Vector3(
            currentChaserX,
            chaserY,
            player.position.z - currentGap);
        transform.forward = Vector3.forward;
    }

    // ─── Animator ───────────────────────────────────────────────────────────

    private void UpdateAnimator()
    {
        if (!chaserAnimator) return;
        // 0 = at restingGap, 1 = caught player
        float t = 1f - Mathf.InverseLerp(catchDistance, restingGap, currentGap);
        chaserAnimator.SetFloat(animSpeedParam, Mathf.Clamp01(t));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  STUMBLE REACTION  (called from PlayerController.BeginStumble)
    // ═══════════════════════════════════════════════════════════════════════

    public void OnPlayerStumbled()
    {
        targetGap -= stumbleLunge;
        targetGap  = Mathf.Max(targetGap, catchDistance);
        if (chaserAnimator) chaserAnimator.SetTrigger(animLungeTrigger);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  WARNING UI + AUDIO
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateWarnings()
    {
        if (currentGap >= warningDistance)
        {
            SetVignetteAlpha(0f);
            audioSource.volume = 0f;
            return;
        }

        float t     = 1f - Mathf.InverseLerp(dangerDistance, warningDistance, currentGap);
        t           = Mathf.Clamp01(t);
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * Mathf.PI * (2f + t * 4f));

        SetVignetteAlpha(t * maxVignetteAlpha * pulse);
        audioSource.volume = t * maxWarningVolume;
    }

    private void SetVignetteAlpha(float alpha)
    {
        if (dangerVignetteImage == null) return;
        Color c = dangerVignetteImage.color;
        c.a = alpha;
        dangerVignetteImage.color = c;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CAUGHT
    // ═══════════════════════════════════════════════════════════════════════

    private void CaughtPlayer()
    {
        DeactivateChaser();
        GameManager.Instance.TriggerGameOver();
    }
}