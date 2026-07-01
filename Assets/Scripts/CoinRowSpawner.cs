using UnityEngine;

/// <summary>
/// Attach to a Tile prefab root. Divides the tile into zones along Z and
/// spawns a row of coins in each zone — so long tiles get multiple rows
/// instead of a single cluster in the middle.
///
/// SETUP:
///   1. Add this component to your Tile prefab root.
///   2. Assign the Coin prefab (CoinCollector + Trigger collider).
///   3. Set tileLength to match TileSpawner.tileLength (default 30).
///   4. Tune zonesPerTile to control how many coin rows appear per tile.
/// </summary>
public class CoinRowSpawner : MonoBehaviour
{
    [Header("Coin Prefab")]
    public GameObject coinPrefab;

    [Header("Tile Layout")]
    [Tooltip("Must match TileSpawner.tileLength exactly.")]
    public float tileLength = 30f;

    [Tooltip("How many independent coin zones to split the tile into. " +
             "Each zone can spawn its own row in a random lane.")]
    public int zonesPerTile = 3;

    [Tooltip("Padding at the start and end of the tile so coins never " +
             "spawn right at the seam between tiles.")]
    public float tilePadding = 2f;

    [Header("Row Settings")]
    public int   coinsPerRow = 8;
    public float spacing     = 1.5f;    // gap between individual coins along Z
    public float coinHeight  = 0.5f;    // Y offset above tile surface

    [Header("Lane Settings")]
    public float laneDistance = 2.5f;

    [Header("Spawn Chances")]
    [Range(0f, 1f)]
    [Tooltip("Chance that any given zone actually spawns a coin row. " +
             "Keeps runs from feeling totally predictable.")]
    public float zoneSpawnChance = 0.75f;

    [Tooltip("Allow zigzag rows (alternating between two lanes) instead of a straight row.")]
    public bool allowZigzag = true;
    [Range(0f, 1f)]
    public float zigzagChance = 0.25f;

    private void Start()
    {
        if (coinPrefab == null)
        {
            Debug.LogWarning("CoinRowSpawner: coinPrefab not assigned!", this);
            return;
        }
        SpawnAllZones();
    }

    private void SpawnAllZones()
    {
        float usableLength = tileLength - (tilePadding * 2f);
        float zoneLength   = usableLength / zonesPerTile;

        for (int z = 0; z < zonesPerTile; z++)
        {
            if (Random.value > zoneSpawnChance) continue;  // skip zone randomly

            // Pick a random Z start position within this zone
            float zoneStart = tilePadding + z * zoneLength;
            float rowWidth  = coinsPerRow * spacing;
            float maxStart  = zoneLength - rowWidth;
            float localZ    = zoneStart + (maxStart > 0 ? Random.Range(0f, maxStart) : 0f);

            int lane            = Random.Range(-1, 2);   // -1, 0, or 1
            Vector3 localOffset = new Vector3(0f, coinHeight, localZ);

            bool doZigzag = allowZigzag && Random.value < zigzagChance;
            if (doZigzag)
            {
                int otherLane = (lane == 0)
                    ? (Random.value < 0.5f ? -1 : 1)
                    : 0;
                SpawnZigzag(lane, otherLane, localOffset);
            }
            else
            {
                SpawnRow(lane, localOffset);
            }
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Straight row of coins in one lane.</summary>
    public void SpawnRow(int lane, Vector3 localOffset)
    {
        float xPos = lane * laneDistance;
        for (int i = 0; i < coinsPerRow; i++)
        {
            Vector3 pos    = localOffset + new Vector3(xPos, 0f, i * spacing);
            GameObject coin = Instantiate(coinPrefab, transform);
            coin.transform.localPosition = pos;
        }
    }

    /// <summary>Zigzag row alternating between laneA and laneB.</summary>
    public void SpawnZigzag(int laneA, int laneB, Vector3 localOffset)
    {
        for (int i = 0; i < coinsPerRow; i++)
        {
            int lane   = (i % 2 == 0) ? laneA : laneB;
            float xPos = lane * laneDistance;
            Vector3 pos = localOffset + new Vector3(xPos, 0f, i * spacing);
            GameObject c = Instantiate(coinPrefab, transform);
            c.transform.localPosition = pos;
        }
    }

    /// <summary>Fill all 3 lanes with coins at the given local offset (bonus burst).</summary>
    public void SpawnRowAllLanes(Vector3 localOffset)
    {
        SpawnRow(-1, localOffset);
        SpawnRow( 0, localOffset);
        SpawnRow( 1, localOffset);
    }
}