using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Divides the tile into zones and spawns obstacle patterns per zone.
/// Supports:
///   - Single-lane blockers (jump over / slide under / full block)
///   - Tunnel prefabs that block 2 lanes, leaving exactly 1 lane open
///   - Moving trains with a configurable length footprint
///   - Footprint-aware zone budgeting so long obstacles never overlap
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    // ── Obstacle descriptor ──────────────────────────────────────────────────
    [System.Serializable]
    public class ObstacleOption
    {
        public GameObject prefab;

        [Tooltip("1 = single lane blocker, 2 = tunnel (blocks 2 lanes).")]
        public int lanesBlocked = 1;

        [Tooltip("Length of this obstacle along Z in world units. " +
                 "Used to budget spacing so obstacles don't overlap.")]
        public float obstacleLength = 2f;

        [Tooltip("Is this a moving train? Moving trains get an extra Z clearance buffer " +
                 "before the next obstacle so the player has time to react.")]
        public bool isMovingTrain = false;

        [Tooltip("Extra clearance added after a moving train (world units).")]
        public float trainClearanceBuffer = 6f;

        [Range(0f, 1f)]
        public float weight = 1f;
    }

    [Header("Obstacle Prefabs")]
    public ObstacleOption[] obstacleOptions;

    [Header("Tile Layout")]
    [Tooltip("Must match TileSpawner.tileLength.")]
    public float tileLength   = 30f;
    public int   zonesPerTile = 3;
    public float tilePadding  = 3f;
    public float obstacleY    = 0f;

    [Header("Lane Settings")]
    public float laneDistance = 2.5f;

    [Header("Spawn Rules")]
    [Range(0f, 1f)] public float zoneSpawnChance    = 0.8f;
    [Range(0f, 1f)] public float laneSpawnChance     = 0.45f;
    [Range(0f, 1f)] public float tunnelSpawnChance   = 0.25f;

    [Tooltip("Always leave at least one lane clear per zone.")]
    public bool alwaysLeaveOneLaneFree = true;

    [Tooltip("Prevent the same single blocked lane repeating in consecutive zones.")]
    public bool preventSameLaneConsecutively = true;

    // ── Runtime ──────────────────────────────────────────────────────────────
    private void Start()
    {
        if (obstacleOptions == null || obstacleOptions.Length == 0)
        {
            Debug.LogWarning("ObstacleSpawner: no obstacle prefabs assigned!", this);
            return;
        }
        SpawnAllZones();
    }

    private void SpawnAllZones()
    {
        float usableLength = tileLength - tilePadding * 2f;
        float zoneLength   = usableLength / zonesPerTile;

        int   lastBlockedLane = -99;
        float nextAvailableZ  = tilePadding;   // tracks clearance after long obstacles

        for (int z = 0; z < zonesPerTile; z++)
        {
            float zoneStart = tilePadding + z * zoneLength;
            float zoneEnd   = zoneStart + zoneLength;

            // Skip zone if a previous long obstacle is still occupying this space
            if (nextAvailableZ > zoneEnd) continue;

            if (Random.value > zoneSpawnChance) continue;

            // Decide: tunnel (2-lane) or regular pattern
            bool spawnTunnel = Random.value < tunnelSpawnChance && HasTunnelOption();

            float spawnZ = Mathf.Max(nextAvailableZ,
                               zoneStart + zoneLength * 0.5f
                               + Random.Range(-zoneLength * 0.2f, zoneLength * 0.2f));

            if (spawnTunnel)
            {
                ObstacleOption tunnel = PickTunnelOption();
                int openLane          = SpawnTunnel(tunnel, spawnZ);
                lastBlockedLane       = -1;  // tunnel blocks 2 lanes, reset tracker
                nextAvailableZ        = spawnZ + tunnel.obstacleLength
                                        + (tunnel.isMovingTrain ? tunnel.trainClearanceBuffer : 0f);
            }
            else
            {
                ObstacleOption chosen = PickWeightedRandom(singleLaneOnly: true);
                if (chosen == null) continue;

                int[] flags = DecideLaneFlags(lastBlockedLane);
                lastBlockedLane = GetPrimaryBlockedLane(flags);

                for (int lane = -1; lane <= 1; lane++)
                {
                    int laneIdx = lane + 1;
                    if (flags[laneIdx] != 1) continue;

                    Vector3 pos = transform.position
                                  + new Vector3(lane * laneDistance, obstacleY, spawnZ);

                    GameObject go = Instantiate(chosen.prefab, pos, Quaternion.identity, transform);

                    // Kick off movement if it's a train
                    if (chosen.isMovingTrain)
                    {
                        MovingTrain mt = go.GetComponent<MovingTrain>();
                        mt?.Initialise(lane);
                    }
                }

                nextAvailableZ = spawnZ + chosen.obstacleLength
                                 + (chosen.isMovingTrain ? chosen.trainClearanceBuffer : 0f);
            }
        }
    }

    // ── Tunnel spawning ──────────────────────────────────────────────────────

    /// <summary>
    /// A tunnel blocks exactly 2 lanes, leaving 1 open.
    /// We spawn 2 blocker prefabs (left+center, left+right, or center+right)
    /// and return the index of the open lane so the next zone can vary it.
    /// </summary>
    private int SpawnTunnel(ObstacleOption option, float spawnZ)
    {
        // Pick which lane stays open (0=left, 1=center, 2=right → lane -1, 0, 1)
        int openLaneIndex  = Random.Range(0, 3);
        int openLane       = openLaneIndex - 1;   // convert to -1 / 0 / 1

        for (int lane = -1; lane <= 1; lane++)
        {
            if (lane == openLane) continue;   // leave this lane free

            Vector3 pos = transform.position
                          + new Vector3(lane * laneDistance, obstacleY, spawnZ);

            GameObject go = Instantiate(option.prefab, pos, Quaternion.identity, transform);

            if (option.isMovingTrain)
            {
                MovingTrain mt = go.GetComponent<MovingTrain>();
                mt?.Initialise(lane);
            }
        }

        return openLane;
    }

    // ── Lane flag helpers ────────────────────────────────────────────────────

    private int[] DecideLaneFlags(int lastBlockedLane)
    {
        int[] flags = new int[3];
        for (int i = 0; i < 3; i++)
            flags[i] = Random.value <= laneSpawnChance ? 1 : 0;

        if (alwaysLeaveOneLaneFree && AllBlocked(flags))
        {
            flags[Random.Range(0, 3)] = 0;
        }

        // Don't repeat the same single-lane block consecutively
        if (preventSameLaneConsecutively && lastBlockedLane >= 0 && lastBlockedLane <= 2)
        {
            int count = flags[0] + flags[1] + flags[2];
            if (count == 1 && flags[lastBlockedLane] == 1)
            {
                flags[lastBlockedLane]       = 0;
                flags[(lastBlockedLane + 1) % 3] = 1;
            }
        }

        return flags;
    }

    private bool AllBlocked(int[] f) => f[0] + f[1] + f[2] == 3;

    private int GetPrimaryBlockedLane(int[] flags)
    {
        if (flags[0] + flags[1] + flags[2] != 1) return -1;
        for (int i = 0; i < 3; i++) if (flags[i] == 1) return i;
        return -1;
    }

    // ── Prefab pickers ───────────────────────────────────────────────────────

    private bool HasTunnelOption() =>
        System.Array.Exists(obstacleOptions, o => o.lanesBlocked == 2);

    private ObstacleOption PickTunnelOption()
    {
        var tunnels = System.Array.FindAll(obstacleOptions, o => o.lanesBlocked == 2);
        return PickFromList(tunnels);
    }

    private ObstacleOption PickWeightedRandom(bool singleLaneOnly = false)
    {
        var pool = singleLaneOnly
            ? System.Array.FindAll(obstacleOptions, o => o.lanesBlocked == 1)
            : obstacleOptions;
        return PickFromList(pool);
    }

    private ObstacleOption PickFromList(ObstacleOption[] pool)
    {
        if (pool == null || pool.Length == 0) return null;

        float total = 0f;
        foreach (var o in pool) total += o.weight;

        float roll = Random.Range(0f, total), cumulative = 0f;
        foreach (var o in pool)
        {
            cumulative += o.weight;
            if (roll <= cumulative) return o;
        }
        return pool[pool.Length - 1];
    }
}