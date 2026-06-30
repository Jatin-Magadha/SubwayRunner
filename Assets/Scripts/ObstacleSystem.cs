using UnityEngine;

/// <summary>
/// Attach to any obstacle prefab (barrier, low bar, oncoming train, etc).
/// Defines how the obstacle can — or can't — be avoided, and lets you
/// scatter obstacle variants procedurally within a tile.
/// </summary>
public class ObstacleHit : MonoBehaviour
{
    public enum ObstacleType
    {
        FullBlock,     // must change lanes - cannot jump or slide past
        JumpOver,      // low barrier - jumping avoids it, sliding does not
        SlideUnder,    // overhead bar - sliding avoids it, jumping does not
        MovingTrain    // always lethal, no avoidance (the "death" obstacle)
    }

    public ObstacleType type = ObstacleType.FullBlock;

    /// <summary>
    /// Called by PlayerController on trigger enter to decide if this hit
    /// should count as a successful dodge.
    /// </summary>
    public bool CanBeAvoided(bool isSliding, bool isJumping)
    {
        switch (type)
        {
            case ObstacleType.JumpOver:
                return isJumping;
            case ObstacleType.SlideUnder:
                return isSliding;
            case ObstacleType.FullBlock:
            case ObstacleType.MovingTrain:
            default:
                return false;
        }
    }
}

/// <summary>
/// Optional helper for procedurally placing obstacles inside a tile prefab
/// at runtime, instead of (or in addition to) hand-placed ones.
/// Attach to the Tile prefab root.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    [System.Serializable]
    public class ObstacleOption
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float weight = 1f;
    }

    public ObstacleOption[] obstacleOptions;
    public Transform[] laneSpawnPoints; // 3 points: left, center, right
    [Range(0f, 1f)] public float spawnChancePerLane = 0.4f;

    private void Start()
    {
        foreach (var point in laneSpawnPoints)
        {
            if (Random.value <= spawnChancePerLane)
            {
                GameObject prefab = PickWeightedRandom();
                if (prefab != null)
                    Instantiate(prefab, point.position, point.rotation, transform);
            }
        }
    }

    private GameObject PickWeightedRandom()
    {
        if (obstacleOptions.Length == 0) return null;

        float total = 0f;
        foreach (var opt in obstacleOptions) total += opt.weight;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        foreach (var opt in obstacleOptions)
        {
            cumulative += opt.weight;
            if (roll <= cumulative) return opt.prefab;
        }
        return obstacleOptions[obstacleOptions.Length - 1].prefab;
    }
}