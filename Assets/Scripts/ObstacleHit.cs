using UnityEngine;

/// <summary>
/// Attach to any obstacle prefab. Evaluates collision severity based on
/// HOW the player hit the obstacle, not just what the obstacle type is.
///
/// RESULT LOGIC:
///
///   Barrier (jump to clear):
///     • Not jumping at all              → Fatal   (ran straight into it)
///     • Jumping, velocity above threshold → Avoided (clean jump)
///     • Jumping, velocity below threshold → Stumble (late/barely jumped)
///
///   LowBar (slide to clear):
///     • Not sliding                     → Fatal   (ran straight under it)
///     • Sliding, well into slide        → Avoided (properly ducking)
///     • Sliding, just started           → Stumble (late slide, barely ducked)
///
///   FullBlock / MovingTrain:
///     • Player is mid-lane-switch       → Stumble (side clip / glancing blow)
///     • Player running straight         → Fatal   (head-on impact)
/// </summary>
public class ObstacleHit : MonoBehaviour
{
    public enum ObstacleType
    {
        Barrier,      // waist-high block, cleared by jumping
        LowBar,       // overhead bar, cleared by sliding
        JumpOrSlideBarrier,
        FullBlock,    // wall filling lane, must change lanes — side clip = stumble
        MovingTrain   // same rules as FullBlock (side clip = stumble, head-on = fatal)
    }

    public enum CollisionResult { Avoided, Stumble, Fatal }

    [Header("Obstacle Type")]
    public ObstacleType type = ObstacleType.Barrier;

    [Header("Barrier / Jump Thresholds")]
    [Tooltip("Minimum upward velocity to count as a clean jump clear. " +
             "Below this = 'barely jumped' = Stumble. Tune alongside jumpForce.")]
    public float jumpClearVelocityThreshold = 4f;

    [Header("LowBar / Slide Thresholds")]
    [Tooltip("Fraction of slide duration that must have elapsed to count as 'properly sliding'. " +
             "E.g. 0.25 means the player must have been sliding for at least 25% of slide duration. " +
             "Above this = Avoided. Below = barely started = Stumble.")]
    [Range(0f, 1f)]
    public float slideProperlyElapsedRatio = 0.25f;

    [Header("Side-Clip Detection (FullBlock / Train)")]
    [Tooltip("If the player's X position is further than this from their target lane centre " +
             "when collision occurs, it counts as a side clip → Stumble instead of Fatal.")]
    public float sideClipLaneOffsetThreshold = 0.6f;

    [Header("Override")]
    [Tooltip("Force a specific result regardless of context. Use sparingly.")]
    public CollisionSeverityOverride severityOverride = CollisionSeverityOverride.Default;
    public enum CollisionSeverityOverride { Default, AlwaysStumble, AlwaysFatal, AlwaysAvoided }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Full context-aware evaluation. PlayerController builds a CollisionContext
    /// and passes it here so all logic lives in one place.
    /// </summary>
    public CollisionResult GetCollisionResult(CollisionContext ctx)
    {
        // Hard overrides first
        switch (severityOverride)
        {
            case CollisionSeverityOverride.AlwaysAvoided: return CollisionResult.Avoided;
            case CollisionSeverityOverride.AlwaysStumble: return CollisionResult.Stumble;
            case CollisionSeverityOverride.AlwaysFatal:   return CollisionResult.Fatal;
        }

        switch (type)
        {
            case ObstacleType.Barrier:
                return EvaluateBarrier(ctx);

            case ObstacleType.LowBar:
                return EvaluateLowBar(ctx);

            case ObstacleType.JumpOrSlideBarrier:
                return EvaluateJumpOrSlideBarrier(ctx);

            case ObstacleType.FullBlock:
            case ObstacleType.MovingTrain:
                return EvaluateSolidObstacle(ctx);

            default:
                return CollisionResult.Fatal;
        }
    }

    // ── Evaluation per type ───────────────────────────────────────────────────

    private CollisionResult EvaluateBarrier(CollisionContext ctx)
    {
        if (!ctx.isJumping)
            // No jump input at all — ran directly into the barrier
            return CollisionResult.Fatal;

        if (ctx.verticalVelocity >= jumpClearVelocityThreshold)
            // Well into the jump, cleared it cleanly
            return CollisionResult.Avoided;

        // Jumped but velocity is low — late input or barely off the ground
        return CollisionResult.Stumble;
    }

    private CollisionResult EvaluateLowBar(CollisionContext ctx)
    {
        if (!ctx.isSliding)
            // No slide input — ran straight into the bar
            return CollisionResult.Fatal;

        // Check how far into the slide we are
        // slideElapsedRatio: 0 = just pressed slide, 1 = slide almost over
        float elapsedRatio = ctx.slideDuration > 0f
            ? 1f - (ctx.slideTimeRemaining / ctx.slideDuration)
            : 1f;

        if (elapsedRatio >= slideProperlyElapsedRatio)
            // Properly into the slide, cleared the bar
            return CollisionResult.Avoided;

        // Slide input was given but too late — just barely starting to duck
        return CollisionResult.Stumble;
    }

    private CollisionResult EvaluateJumpOrSlideBarrier(CollisionContext ctx)
    {
        if (!ctx.isJumping && !ctx.isSliding)
            // No jump input at all — ran directly into the barrier
            return CollisionResult.Fatal;

        // Check how far into the slide we are
        // slideElapsedRatio: 0 = just pressed slide, 1 = slide almost over
        float elapsedRatio = ctx.slideDuration > 0f
            ? 1f - (ctx.slideTimeRemaining / ctx.slideDuration)
            : 1f;

        if (elapsedRatio >= slideProperlyElapsedRatio)
            // Properly into the slide, cleared the bar
            return CollisionResult.Avoided;

        if (ctx.verticalVelocity >= jumpClearVelocityThreshold)
            // Well into the jump, cleared it cleanly
            return CollisionResult.Avoided;

        // Jumped but velocity is low — late input or barely off the ground
        return CollisionResult.Stumble;
    }

    private CollisionResult EvaluateSolidObstacle(CollisionContext ctx)
    {
        // Side clip: player is mid-lane-switch when they make contact
        if (ctx.isChangingLane && ctx.laneOffsetMagnitude >= sideClipLaneOffsetThreshold)
            return CollisionResult.Stumble;

        // Head-on: player is running straight or fully settled in a lane
        return CollisionResult.Fatal;
    }

    // ── Legacy shim ───────────────────────────────────────────────────────────
    public bool CanBeAvoided(bool isSliding, bool isJumping) =>
        GetCollisionResult(new CollisionContext
        {
            isJumping          = isJumping,
            isSliding          = isSliding,
            verticalVelocity   = isJumping ? 999f : 0f,   // assume clean if legacy call
            slideTimeRemaining = 0f,
            slideDuration      = 1f,
            isChangingLane     = false,
            laneOffsetMagnitude = 0f
        }) == CollisionResult.Avoided;
}

/// <summary>
/// Snapshot of player state at the moment of collision, passed to
/// ObstacleHit.GetCollisionResult so it can make context-aware decisions.
/// </summary>
public struct CollisionContext
{
    /// True if the player is airborne (not grounded).
    public bool  isJumping;

    /// True if the player is in an active slide.
    public bool  isSliding;

    /// Vertical velocity at moment of collision. Positive = rising, negative = falling.
    public float verticalVelocity;

    /// Seconds of slide duration still remaining (0 = slide about to end).
    public float slideTimeRemaining;

    /// Total slide duration, used to compute elapsed ratio.
    public float slideDuration;

    /// True if the player's X is still moving toward the target lane.
    public bool  isChangingLane;

    /// How far (world units) the player is from the centre of their target lane.
    /// High value = deep mid-switch = more likely a side clip.
    public float laneOffsetMagnitude;
}