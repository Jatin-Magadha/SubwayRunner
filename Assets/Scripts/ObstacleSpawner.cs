using UnityEngine;

/// <summary>
/// Divides the tile into zones and spawns obstacle patterns per zone.
///
/// TRAIN CLEARANCE FIX:
///   Each ObstacleOption now has a trainApproachClearance field.
///   When a moving train is selected, spawnZ is pushed forward by that amount
///   past nextAvailableZ — guaranteeing a visible gap BEFORE the train, not
///   just after it. Previously only post-obstacle clearance was tracked, so
///   trains could spawn immediately after another obstacle with no reaction time.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    //  OBSTACLE DESCRIPTOR
    // ═══════════════════════════════════════════════════════════════════════

    [System.Serializable]
    public class ObstacleOption
    {
        public GameObject prefab;

        [Tooltip("1 = single lane blocker, 2 = tunnel (blocks 2 lanes).")]
        public int lanesBlocked = 1;

        [Tooltip("Length of this obstacle along Z in world units. " +
                 "Advances nextAvailableZ after spawning so the next obstacle " +
                 "can't overlap this one.")]
        public float obstacleLength = 2f;

        [Tooltip("Is this a moving train?")]
        public bool isMovingTrain = false;

        [Tooltip("Minimum clear space required BEFORE this train spawns " +
                 "(world units after the previous obstacle ends). " +
                 "This is the approach window — the gap the player sees " +
                 "and has to react to. Recommended: 8–14.")]
        public float trainApproachClearance = 10f;

        [Tooltip("Clear space enforced AFTER this obstacle ends before the " +
                 "next one can spawn. For trains this should be large enough " +
                 "for the player to change lanes comfortably once the train passes.")]
        public float postObstacleClearance = 4f;

        [Range(0f, 1f)]
        public float weight = 1f;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  INSPECTOR
    // ═══════════════════════════════════════════════════════════════════════

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
    [Range(0f, 1f)] public float zoneSpawnChance  = 0.8f;
    [Range(0f, 1f)] public float laneSpawnChance  = 0.45f;
    [Range(0f, 1f)] public float tunnelSpawnChance = 0.25f;
    public bool alwaysLeaveOneLaneFree         = true;
    public bool preventSameLaneConsecutively   = true;

    // ═══════════════════════════════════════════════════════════════════════
    //  SPAWN LOOP
    // ═══════════════════════════════════════════════════════════════════════

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

        // nextAvailableZ = earliest Z at which the NEXT obstacle may start.
        // It advances by obstacleLength + postObstacleClearance after every spawn.
        float nextAvailableZ = tilePadding;

        for (int z = 0; z < zonesPerTile; z++)
        {
            float zoneStart = tilePadding + z * zoneLength;
            float zoneEnd   = zoneStart + zoneLength;

            // Previous obstacle's footprint still covers this whole zone — skip
            if (nextAvailableZ > zoneEnd) continue;

            if (Random.value > zoneSpawnChance) continue;

            bool spawnTunnel = Random.value < tunnelSpawnChance && HasTunnelOption();

            if (spawnTunnel)
            {
                ObstacleOption tunnel = PickTunnelOption();
                if (tunnel == null) continue;

                float spawnZ = ComputeSpawnZ(tunnel, zoneStart, zoneLength, nextAvailableZ);
                if (spawnZ + tunnel.obstacleLength > tileLength - tilePadding) continue; // won't fit

                SpawnTunnel(tunnel, spawnZ);
                lastBlockedLane = -1;
                nextAvailableZ  = spawnZ + tunnel.obstacleLength + tunnel.postObstacleClearance;
            }
            else
            {
                ObstacleOption chosen = PickWeightedRandom(singleLaneOnly: true);
                if (chosen == null) continue;

                float spawnZ = ComputeSpawnZ(chosen, zoneStart, zoneLength, nextAvailableZ);
                if (spawnZ + chosen.obstacleLength > tileLength - tilePadding) continue;

                int[] flags = DecideLaneFlags(lastBlockedLane);
                lastBlockedLane = GetPrimaryBlockedLane(flags);

                for (int lane = -1; lane <= 1; lane++)
                {
                    if (flags[lane + 1] != 1) continue;

                    Vector3 pos = transform.position +
                                  new Vector3(lane * laneDistance, obstacleY, spawnZ);

                    GameObject go = Instantiate(chosen.prefab, pos, Quaternion.identity, transform);

                    if (chosen.isMovingTrain)
                        go.GetComponent<MovingTrain>()?.Initialise(lane);
                }

                nextAvailableZ = spawnZ + chosen.obstacleLength + chosen.postObstacleClearance;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SPAWN Z CALCULATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the Z position where this obstacle should spawn.
    ///
    /// For regular obstacles: centre of the zone with a small random offset,
    /// but never earlier than nextAvailableZ.
    ///
    /// For moving trains: also adds trainApproachClearance on top of
    /// nextAvailableZ — this is the key fix. The train can't start until
    /// nextAvailableZ + trainApproachClearance, giving the player a visible
    /// gap to react before the train appears.
    /// </summary>
    private float ComputeSpawnZ(ObstacleOption option, float zoneStart,
                                float zoneLength, float nextAvailableZ)
    {
        // Earliest allowed spawn point for this obstacle type
        float earliestAllowed = option.isMovingTrain
            ? nextAvailableZ + option.trainApproachClearance   // ← THE FIX
            : nextAvailableZ;

        // Preferred position: centre of zone + small jitter for variety
        float preferred = zoneStart + zoneLength * 0.5f
                          + Random.Range(-zoneLength * 0.2f, zoneLength * 0.2f);

        return Mathf.Max(earliestAllowed, preferred);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TUNNEL
    // ═══════════════════════════════════════════════════════════════════════

    private int SpawnTunnel(ObstacleOption option, float spawnZ)
    {
        int openLane = Random.Range(-1, 2);

        for (int lane = -1; lane <= 1; lane++)
        {
            if (lane == openLane) continue;

            Vector3 pos = transform.position +
                          new Vector3(lane * laneDistance, obstacleY, spawnZ);

            GameObject go = Instantiate(option.prefab, pos, Quaternion.identity, transform);

            if (option.isMovingTrain)
                go.GetComponent<MovingTrain>()?.Initialise(lane);
        }
        return openLane;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  LANE FLAGS
    // ═══════════════════════════════════════════════════════════════════════

    private int[] DecideLaneFlags(int lastBlockedLane)
    {
        int[] flags = new int[3];
        for (int i = 0; i < 3; i++)
            flags[i] = Random.value <= laneSpawnChance ? 1 : 0;

        if (alwaysLeaveOneLaneFree && AllBlocked(flags))
            flags[Random.Range(0, 3)] = 0;

        if (preventSameLaneConsecutively && lastBlockedLane >= 0 && lastBlockedLane <= 2)
        {
            int count = flags[0] + flags[1] + flags[2];
            if (count == 1 && flags[lastBlockedLane] == 1)
            {
                flags[lastBlockedLane]           = 0;
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

    // ═══════════════════════════════════════════════════════════════════════
    //  PICKERS
    // ═══════════════════════════════════════════════════════════════════════

    private bool HasTunnelOption() =>
        System.Array.Exists(obstacleOptions, o => o.lanesBlocked == 2);

    private ObstacleOption PickTunnelOption() =>
        PickFromList(System.Array.FindAll(obstacleOptions, o => o.lanesBlocked == 2));

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
        float roll = Random.Range(0f, total), cum = 0f;
        foreach (var o in pool) { cum += o.weight; if (roll <= cum) return o; }
        return pool[pool.Length - 1];
    }
}