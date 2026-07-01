using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Attach to a Tile prefab root. Divides the tile into zones along Z and
/// spawns an obstacle pattern per zone — so a 30-unit tile with 3 zones
/// gets 3 independent obstacle sets instead of one cluster in the middle.
///
/// SETUP:
///   1. Add this to your Tile prefab root (same object as CoinRowSpawner).
///   2. Set tileLength to match TileSpawner.tileLength (default 30).
///   3. Add obstacle prefabs (each with ObstacleHit) into Obstacle Options.
///   4. No manual spawn point GameObjects needed — positions are computed
///      from tileLength, zonesPerTile, and laneDistance automatically.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    [System.Serializable]
    public class ObstacleOption
    {
        public GameObject prefab;
        [Range(0f, 1f)] public float weight = 1f;
    }

    [Header("Obstacle Prefabs")]
    public ObstacleOption[] obstacleOptions;

    [Header("Tile Layout")]
    [Tooltip("Must match TileSpawner.tileLength exactly.")]
    public float tileLength  = 30f;

    [Tooltip("How many obstacle zones the tile is divided into along Z.")]
    public int zonesPerTile  = 3;

    [Tooltip("Padding at the start and end of the tile so obstacles never " +
             "spawn right at the seam between tiles.")]
    public float tilePadding = 3f;

    [Tooltip("Height above the tile surface at which obstacles are placed.")]
    public float obstacleY   = 0f;

    [Header("Lane Settings")]
    public float laneDistance = 2.5f;

    [Header("Spawn Rules")]
    [Range(0f, 1f)]
    [Tooltip("Chance that any given zone spawns obstacles at all.")]
    public float zoneSpawnChance = 0.8f;

    [Range(0f, 1f)]
    [Tooltip("Per-lane chance of spawning within an active zone.")]
    public float laneSpawnChance = 0.45f;

    [Tooltip("Always leave at least one lane clear per zone so the run is never unwinnable.")]
    public bool alwaysLeaveOneLaneFree = true;

    [Tooltip("Prevent two zones that are adjacent from blocking the same lane, " +
             "forcing the player to switch lanes between zones.")]
    public bool preventSameLaneConsecutively = true;

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
        float usableLength = tileLength - (tilePadding * 2f);
        float zoneLength   = usableLength / zonesPerTile;

        int lastBlockedLane = -99;  // track previous zone's blocked lane for variety

        for (int z = 0; z < zonesPerTile; z++)
        {
            if (Random.value > zoneSpawnChance) continue;

            // Place each zone's obstacles at the centre of its zone section,
            // with a small random offset so tiles don't look mechanical.
            float zoneCentre  = tilePadding + (z * zoneLength) + (zoneLength * 0.5f);
            float randomOffset = Random.Range(-zoneLength * 0.2f, zoneLength * 0.2f);
            float spawnZ       = zoneCentre + randomOffset;

            int[] flags = DecideLaneFlags(lastBlockedLane);

            // Record which lane(s) were blocked for next zone's check
            lastBlockedLane = GetPrimaryBlockedLane(flags);

            for (int lane = -1; lane <= 1; lane++)
            {
                int laneIndex = lane + 1;   // -1→0, 0→1, 1→2
                if (flags[laneIndex] != 1) continue;

                float   xPos    = lane * laneDistance;
                Vector3 spawnPos = transform.position + new Vector3(xPos, obstacleY, spawnZ);

                GameObject prefab = PickWeightedRandom();
                if (prefab != null)
                    Instantiate(prefab, spawnPos, Quaternion.identity, transform);
            }
        }
    }

    // ── Lane decision logic ────────────────────────────────────────────────

    private int[] DecideLaneFlags(int lastBlockedLane)
    {
        int[] flags = new int[3];

        for (int i = 0; i < 3; i++)
            flags[i] = Random.value <= laneSpawnChance ? 1 : 0;

        // Enforce: always one free lane
        if (alwaysLeaveOneLaneFree && AllBlocked(flags))
        {
            int freeLane = Random.Range(0, 3);
            flags[freeLane] = 0;
        }

        // Enforce: don't block the exact same single lane as last zone
        if (preventSameLaneConsecutively && lastBlockedLane >= 0 && lastBlockedLane <= 2)
        {
            int blockedCount = flags[0] + flags[1] + flags[2];
            if (blockedCount == 1 && flags[lastBlockedLane] == 1)
            {
                // Shift the block to an adjacent lane
                flags[lastBlockedLane] = 0;
                int next = (lastBlockedLane + 1) % 3;
                flags[next] = 1;
            }
        }

        return flags;
    }

    private bool AllBlocked(int[] flags) =>
        flags[0] + flags[1] + flags[2] == 3;

    /// Returns the index (0-2) of the first blocked lane, or -1 if none / multiple.
    private int GetPrimaryBlockedLane(int[] flags)
    {
        int count = flags[0] + flags[1] + flags[2];
        if (count != 1) return -1;
        for (int i = 0; i < 3; i++)
            if (flags[i] == 1) return i;
        return -1;
    }

    private GameObject PickWeightedRandom()
    {
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