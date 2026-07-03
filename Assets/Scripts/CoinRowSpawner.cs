using UnityEngine;

/// <summary>
/// Divides the tile into zones and spawns a coin pattern per zone.
/// Patterns available per zone:
///   Straight  — coins in a single lane, flat
///   Zigzag    — coins alternating between two lanes
///   Arc       — coins in a vertical arc (rise then fall), single lane
///   Ramp      — coins rising progressively (jump lead-up)
///   Spread    — one coin per lane side by side (wide burst)
/// </summary>
public class CoinRowSpawner : MonoBehaviour
{
    public enum CoinPattern { Straight, Zigzag, Arc, Ramp, Spread }

    [System.Serializable]
    public class PatternWeight
    {
        public CoinPattern pattern = CoinPattern.Straight;
        [Range(0f, 1f)] public float weight = 1f;
    }

    [Header("Coin Prefab")]
    public GameObject coinPrefab;

    [Header("Tile Layout")]
    [Tooltip("Must match TileSpawner.tileLength.")]
    public float tileLength  = 30f;
    public int   zonesPerTile = 3;
    public float tilePadding  = 2f;

    [Header("Row Settings")]
    public int   coinsPerRow  = 8;
    public float spacing      = 1.5f;     // Z gap between coins
    public float coinHeight   = 0.5f;     // baseline Y above tile surface

    [Header("Arc / Ramp Settings")]
    [Tooltip("Peak height of the arc above coinHeight.")]
    public float arcPeakHeight = 2.5f;

    [Tooltip("Max height coins reach at the top of a ramp.")]
    public float rampMaxHeight = 2f;

    [Header("Lane Settings")]
    public float laneDistance = 2.5f;

    [Header("Spawn Chances")]
    [Range(0f, 1f)]
    public float zoneSpawnChance = 0.75f;

    [Header("Pattern Weights")]
    [Tooltip("Controls how often each pattern is chosen. " +
             "Leave empty to pick uniformly from all patterns.")]
    public PatternWeight[] patternWeights;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (coinPrefab == null)
        {
            Debug.LogWarning("CoinRowSpawner: coinPrefab not assigned!", this);
            return;
        }
        SpawnAllZones();
    }

    // ── Zone loop ─────────────────────────────────────────────────────────────

    private void SpawnAllZones()
    {
        float usableLength = tileLength - tilePadding * 2f;
        float zoneLength   = usableLength / zonesPerTile;

        for (int z = 0; z < zonesPerTile; z++)
        {
            if (Random.value > zoneSpawnChance) continue;

            float zoneStart = tilePadding + z * zoneLength;

            // Row width so we can keep it inside the zone
            float rowLength = coinsPerRow * spacing;
            float maxStart  = zoneLength - rowLength;
            float localZ    = zoneStart + (maxStart > 0 ? Random.Range(0f, maxStart) : 0f);

            CoinPattern pattern = PickPattern();
            int lane = Random.Range(-1, 2);   // -1, 0, 1

            SpawnPattern(pattern, lane, localZ);
        }
    }

    // ── Pattern dispatcher ────────────────────────────────────────────────────

    private void SpawnPattern(CoinPattern pattern, int lane, float localZStart)
    {
        switch (pattern)
        {
            case CoinPattern.Straight: SpawnStraight(lane, localZStart);      break;
            case CoinPattern.Zigzag:   SpawnZigzag(lane, localZStart);         break;
            case CoinPattern.Arc:      SpawnArc(lane, localZStart);             break;
            case CoinPattern.Ramp:     SpawnRamp(lane, localZStart);            break;
            case CoinPattern.Spread:   SpawnSpread(localZStart);                break;
        }
    }

    // ── Pattern implementations ───────────────────────────────────────────────

    /// Straight row — all coins flat in one lane.
    private void SpawnStraight(int lane, float zStart)
    {
        float x = lane * laneDistance;
        for (int i = 0; i < coinsPerRow; i++)
            PlaceCoin(x, coinHeight, zStart + i * spacing);
    }

    /// Zigzag — coins alternate between the chosen lane and an adjacent lane,
    /// giving the player a weaving line to follow.
    private void SpawnZigzag(int laneA, float zStart)
    {
        // Pick a second lane that's different from laneA
        int laneB = (laneA == 0)
            ? (Random.value < 0.5f ? -1 : 1)
            : 0;

        for (int i = 0; i < coinsPerRow; i++)
        {
            int  activeLane = (i % 2 == 0) ? laneA : laneB;
            float x         = activeLane * laneDistance;
            PlaceCoin(x, coinHeight, zStart + i * spacing);
        }
    }

    /// Arc — coins rise to a peak in the middle then fall back down,
    /// forming a smooth arch the player jumps through.
    ///    height = coinHeight + arcPeakHeight * sin( i/count * PI )
    private void SpawnArc(int lane, float zStart)
    {
        float x = lane * laneDistance;
        for (int i = 0; i < coinsPerRow; i++)
        {
            float t      = (float)i / (coinsPerRow - 1);           // 0 → 1
            float yArc   = coinHeight + arcPeakHeight * Mathf.Sin(t * Mathf.PI);
            PlaceCoin(x, yArc, zStart + i * spacing);
        }
    }

    /// Ramp — coins rise progressively from ground to rampMaxHeight,
    /// hinting the player should jump to follow them.
    private void SpawnRamp(int lane, float zStart)
    {
        float x = lane * laneDistance;
        for (int i = 0; i < coinsPerRow; i++)
        {
            float t    = (float)i / (coinsPerRow - 1);
            float yRamp = coinHeight + rampMaxHeight * t;
            PlaceCoin(x, yRamp, zStart + i * spacing);
        }
    }

    /// Spread — one coin per lane placed at the same Z position,
    /// rewards the player for being in any lane (short wide burst).
    private void SpawnSpread(float zStart)
    {
        // Use a subset of coinsPerRow, spread across fewer Z positions
        int rows = Mathf.Max(1, coinsPerRow / 3);
        for (int r = 0; r < rows; r++)
        {
            float z = zStart + r * spacing * 3f;   // wider gap between spread rows
            for (int lane = -1; lane <= 1; lane++)
                PlaceCoin(lane * laneDistance, coinHeight, z);
        }
    }

    // ── Coin placement ────────────────────────────────────────────────────────

    private void PlaceCoin(float localX, float localY, float localZ)
    {
        GameObject coin = Instantiate(coinPrefab, transform);
        coin.transform.localPosition = new Vector3(localX, localY, localZ);
    }

    // ── Public API (call from other scripts if needed) ────────────────────────

    public void SpawnRow(int lane, Vector3 localOffset) =>
        SpawnStraight(lane, localOffset.z);

    public void SpawnZigzag(int laneA, int laneB, Vector3 localOffset) =>
        SpawnZigzag(laneA, localOffset.z);   // laneB auto-chosen inside

    public void SpawnRowAllLanes(Vector3 localOffset) =>
        SpawnSpread(localOffset.z);

    // ── Pattern picker ────────────────────────────────────────────────────────

    private CoinPattern PickPattern()
    {
        if (patternWeights == null || patternWeights.Length == 0)
        {
            // Uniform random across all patterns
            return (CoinPattern)Random.Range(0, System.Enum.GetValues(typeof(CoinPattern)).Length);
        }

        float total = 0f;
        foreach (var pw in patternWeights) total += pw.weight;

        float roll = Random.Range(0f, total), cumulative = 0f;
        foreach (var pw in patternWeights)
        {
            cumulative += pw.weight;
            if (roll <= cumulative) return pw.pattern;
        }
        return CoinPattern.Straight;
    }
}