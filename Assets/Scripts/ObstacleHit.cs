using UnityEngine;

/// <summary>
/// Attach to any obstacle prefab (barrier, low bar, train, etc).
/// Defines whether the obstacle can be dodged by jumping or sliding,
/// and is queried by PlayerController on collision.
/// </summary>
public class ObstacleHit : MonoBehaviour
{
    public enum ObstacleType
    {
        FullBlock,    // must change lanes — cannot jump or slide past
        JumpOver,     // low barrier — jumping clears it, sliding does not
        SlideUnder,   // overhead bar — sliding clears it, jumping does not
        JumpOrSlide,  // low barrier — jumping and sliding both clears it
        MovingTrain   // always lethal, no avoidance
    }

    public ObstacleType type = ObstacleType.FullBlock;

    /// <summary>
    /// Called by PlayerController on trigger enter. Returns true if the
    /// player's current action (jump or slide) successfully avoids this obstacle.
    /// </summary>
    public bool CanBeAvoided(bool isSliding, bool isJumping)
    {
        return type switch
        {
            ObstacleType.JumpOver   => isJumping,
            ObstacleType.SlideUnder => isSliding,
            ObstacleType.JumpOrSlide => isJumping||isSliding,
            _                       => false
        };
    }
}