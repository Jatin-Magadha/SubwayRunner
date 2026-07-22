using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Hoverboard system — a consumable shield that absorbs ONE fatal hit.
///
/// HOW IT WORKS:
///   - Player collects Hoverboard pickups in the world (tagged "Hoverboard")
///   - Press the board button (or double-tap) to equip/activate
///   - While active: one fatal obstacle hit is absorbed → board breaks, player survives
///   - Board has a time limit; auto-deactivates when timer runs out
///   - Max board count is capped (boardStockMax)
///
/// SETUP:
///   1. Attach this to the same GameObject as PlayerController
///   2. Assign boardVisual (a child GameObject with your board mesh)
///   3. Assign UI elements for stock count and active indicator
///   4. Call TryActivateBoard() from a UI button or input handler
/// </summary>
public class HoverboardController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR
    // ═══════════════════════════════════════════════════════════════════════

    [Header("Board Settings")]
    [Tooltip("Maximum boards the player can carry.")]
    public int boardStockMax = 3;

    [Tooltip("How long (seconds) an active board lasts before auto-breaking.")]
    public float boardDuration = 30f;

    [Header("Visual")]
    [Tooltip("Child GameObject with the board mesh. Enabled while board is active.")]
    public GameObject boardVisual;

    [Tooltip("Particle effect played when board breaks (assign a prefab).")]
    public GameObject breakVFX;

    [Tooltip("Y offset the player hovers above ground while on the board.")]
    public float hoverHeight = 0.35f;

    [Header("Audio")]
    public AudioClip activateClip;
    public AudioClip breakClip;
    public AudioClip collectClip;

    [Header("UI")]
    [Tooltip("Text showing how many boards are in stock.")]
    public Text boardCountText;

    [Tooltip("GameObject shown while the board is active (e.g. glowing icon).")]
    public GameObject boardActiveIndicator;

    [Tooltip("Fill image for the board timer (fill amount 0–1).")]
    public Image boardTimerFill;

    // ═══════════════════════════════════════════════════════════════════════
    //  PUBLIC STATE
    // ═══════════════════════════════════════════════════════════════════════

    public bool IsActive        { get; private set; }
    public int  BoardStock      { get; private set; }
    public float BoardTimeLeft  { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════
    //  PRIVATE
    // ═══════════════════════════════════════════════════════════════════════

    private AudioSource audioSource;
    private PlayerController player;
    private CharacterController characterController;
    private float originalGroundY;
    private bool  hoverApplied;

    // ═══════════════════════════════════════════════════════════════════════
    //  INIT
    // ═══════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        player               = GetComponent<PlayerController>();
        characterController  = GetComponent<CharacterController>();
        audioSource          = gameObject.AddComponent<AudioSource>();

        if (boardVisual)          boardVisual.SetActive(false);
        if (boardActiveIndicator) boardActiveIndicator.SetActive(false);
    }

    public void ResetBoard()
    {
        if (IsActive) DeactivateBoardSilent();
        BoardStock    = 0;
        BoardTimeLeft = 0f;
        RefreshUI();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  UPDATE
    // ═══════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (!IsActive) return;
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing) return;

        BoardTimeLeft -= Time.deltaTime;
        RefreshTimerFill();

        if (BoardTimeLeft <= 0f)
            BreakBoard(destroyed: false);   // timer ran out, clean deactivation
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ACTIVATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call this from a UI button or PlayerController input handler.
    /// Equips the board if stock > 0 and not already active.
    /// </summary>
    public bool TryActivateBoard()
    {
        if (IsActive)    return false;   // already riding one
        if (BoardStock <= 0) return false;

        BoardStock--;
        ActivateBoard();
        return true;
    }

    private void ActivateBoard()
    {
        IsActive      = true;
        BoardTimeLeft = boardDuration;

        if (boardVisual)          boardVisual.SetActive(true);
        if (boardActiveIndicator) boardActiveIndicator.SetActive(true);

        PlaySFX(activateClip);
        RefreshUI();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HIT ABSORPTION  (called by PlayerController instead of TriggerDeath)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called by PlayerController when a Fatal collision occurs while the
    /// board is active. Absorbs the hit, breaks the board, player survives.
    /// Returns true if the hit was absorbed.
    /// </summary>
    public bool TryAbsorbFatalHit()
    {
        if (!IsActive) return false;

        BreakBoard(destroyed: true);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DEACTIVATION / BREAKING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Board breaks from impact — play destruction VFX/SFX.
    /// Board expires from timer — clean deactivation, no destruction effects.
    /// </summary>
    private void BreakBoard(bool destroyed)
    {
        IsActive      = false;
        BoardTimeLeft = 0f;

        if (boardVisual)          boardVisual.SetActive(false);
        if (boardActiveIndicator) boardActiveIndicator.SetActive(false);

        if (destroyed)
        {
            PlaySFX(breakClip);
            if (breakVFX)
                Instantiate(breakVFX, transform.position, Quaternion.identity);

            // Brief stumble so the player knows the shield broke
            // (non-fatal — board absorbed the hit)
            StartCoroutine(PostBreakStumble());
        }

        RefreshUI();
    }

    private void DeactivateBoardSilent()
    {
        IsActive      = false;
        BoardTimeLeft = 0f;
        if (boardVisual)          boardVisual.SetActive(false);
        if (boardActiveIndicator) boardActiveIndicator.SetActive(false);
    }

    private IEnumerator PostBreakStumble()
    {
        // Give visual feedback that the hit happened even though the board saved it
        // by briefly flashing the player — requires a Renderer child named "Body"
        Renderer body = GetComponentInChildren<Renderer>();
        if (body != null)
        {
            for (int i = 0; i < 4; i++)
            {
                body.enabled = false;
                yield return new WaitForSeconds(0.08f);
                body.enabled = true;
                yield return new WaitForSeconds(0.08f);
            }
        }
        else yield break;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  COLLECTION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Call this when the player runs over a Hoverboard pickup collider.
    /// </summary>
    public void CollectBoard(int amount = 1)
    {
        BoardStock = Mathf.Min(BoardStock + amount, boardStockMax);
        PlaySFX(collectClip);
        RefreshUI();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  UI
    // ═══════════════════════════════════════════════════════════════════════

    private void RefreshUI()
    {
        if (boardCountText)
            boardCountText.text = BoardStock.ToString();

        RefreshTimerFill();
    }

    private void RefreshTimerFill()
    {
        if (!boardTimerFill) return;
        boardTimerFill.fillAmount = IsActive
            ? Mathf.Clamp01(BoardTimeLeft / boardDuration)
            : 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  AUDIO
    // ═══════════════════════════════════════════════════════════════════════

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}