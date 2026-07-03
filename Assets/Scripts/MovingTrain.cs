using UnityEngine;

/// <summary>
/// Attach to a train obstacle prefab. The train moves toward the player
/// along -Z (oncoming) or alongside the player along +Z (same direction,
/// slower than player so it falls behind).
///
/// ObstacleSpawner calls Initialise(lane) after instantiation.
/// </summary>
public class MovingTrain : MonoBehaviour
{
    public enum TrainDirection { Oncoming, SameDirection }

    [Header("Movement")]
    public TrainDirection direction       = TrainDirection.Oncoming;
    public float oncomingSpeed            = 12f;   // speed toward player (world Z -)
    public float sameDirectionSpeedOffset = -4f;   // offset from game speed (negative = slower than player)

    [Header("Lifetime")]
    [Tooltip("Destroy the train after it travels this distance past its spawn point.")]
    public float despawnDistance = 80f;

    private int    lane;
    private Vector3 spawnPosition;
    private bool   initialised;

    /// <summary>Called by ObstacleSpawner immediately after Instantiate.</summary>
    public void Initialise(int laneIndex)
    {
        lane          = laneIndex;
        spawnPosition = transform.position;
        initialised   = true;
    }

    private void Update()
    {
        if (!initialised) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        float speed;
        if (direction == TrainDirection.Oncoming)
        {
            // Moves toward the player (negative Z in world space)
            speed = -oncomingSpeed;
        }
        else
        {
            // Moves in same direction as player but slower, so it drifts behind
            speed = GameManager.Instance.CurrentSpeed + sameDirectionSpeedOffset;
        }

        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.World);

        // Despawn when far enough from spawn point
        if (Vector3.Distance(transform.position, spawnPosition) > despawnDistance)
            Destroy(gameObject);
    }
}