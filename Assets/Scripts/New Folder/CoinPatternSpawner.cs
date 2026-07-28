using System.Collections.Generic;
using UnityEngine;

namespace SubwaySurferClone
{
    public enum CoinPattern
    {
        StraightLine,   // a run of coins down one lane
        Arc,            // an upward arc, as if collected mid-jump
        Zigzag,         // weaves left-center-right-center...
        VShape,         // dips down then back up (duck-under coins)
        Row             // a full-width row of coins repeated down the tile
    }

    /// <summary>
    /// Given a TileInfo, decides on a coin pattern and instantiates coins from a pool.
    /// Designed to be called once per tile right after the tile is spawned.
    /// </summary>
    public class CoinPatternSpawner : MonoBehaviour
    {
        [Header("Coin Prefab / Pooling")]
        public GameObject coinPrefab;
        [Tooltip("How many coins to prewarm the pool with at startup.")]
        public int poolPrewarm = 150;

        [Header("Pattern Tuning")]
        public float coinSpacing = 1.5f;
        public float coinHeight = 1.2f;
        [Tooltip("Peak extra height added at the middle of an Arc/VShape pattern.")]
        public float arcHeight = 2.0f;
        [Range(0f, 1f)] public float chanceToSkipTile = 0.15f; // some tiles get no coins at all, for variety

        [Header("Coin Cap")]
        [Tooltip("Hard ceiling on how many coins can exist in the world at once (across all tiles). " +
                 "Once reached, further placement attempts are skipped until some are collected/recycled.")]
        public int maxActiveCoins = 60;

        [Header("Row Pattern")]
        [Tooltip("How many coins to place per row (across lanes) when the Row pattern is chosen. " +
                 "1 = single lane (centered), 2 = two lanes, 3 = full-width row across all lanes.")]
        [Range(1, 3)] public int coinsPerRow = 3;

        [Tooltip("Distance along Z between one row and the next.")]
        public float rowSpacing = 2.5f;

        private ObjectPool _coinPool;
        private readonly System.Random _rng = new System.Random();

        private void Awake()
        {
            if (coinPrefab != null)
                _coinPool = new ObjectPool(coinPrefab, transform, poolPrewarm);
        }

        /// <summary>
        /// Call this right after a tile is placed in the world.
        /// </summary>
        public void SpawnCoinsForTile(TileInfo tile)
        {
            if (_coinPool == null || tile == null || !tile.allowCoins) return;
            if (!HasCoinBudget()) return; // world is already at the coin cap - nothing to do
            if (_rng.NextDouble() < chanceToSkipTile) return;

            int lane = RandomLane();
            CoinPattern pattern = (CoinPattern)_rng.Next(0, 5);

            switch (pattern)
            {
                case CoinPattern.StraightLine:
                    SpawnStraightLine(tile, lane);
                    break;
                case CoinPattern.Arc:
                    SpawnArc(tile, lane);
                    break;
                case CoinPattern.Zigzag:
                    SpawnZigzag(tile);
                    break;
                case CoinPattern.VShape:
                    SpawnVShape(tile, lane);
                    break;
                case CoinPattern.Row:
                    SpawnRows(tile);
                    break;
            }
        }

        private int RandomLane() => _rng.Next(-1, 2); // -1, 0, 1

        /// <summary>
        /// Attempts to place a single coin. Returns false (and places nothing) if either
        /// the active-coin cap has been reached, or an obstacle overlaps the coin's spot.
        /// The obstacle check pads the sample point by half the coin spacing on either
        /// side so a wall can't slip between two coin positions undetected.
        /// </summary>
        private bool PlaceIfClear(TileInfo tile, int lane, float localZ, float yOffset = 0f)
        {
            if (_coinPool.ActiveCount >= maxActiveCoins) return false;

            float padding = coinSpacing * 0.5f;
            if (tile.IsLaneBlockedInRange(lane, localZ - padding, localZ + padding)) return false;

            float worldZ = tile.StartZ + localZ;
            float worldX = tile.GetLaneX(lane);
            Vector3 pos = new Vector3(worldX, coinHeight + yOffset, worldZ);
            _coinPool.Get(pos, Quaternion.identity);
            return true;
        }

        /// <summary>Quick check used by pattern loops to bail out early once the cap is hit.</summary>
        private bool HasCoinBudget() => _coinPool.ActiveCount < maxActiveCoins;

        private void SpawnStraightLine(TileInfo tile, int lane)
        {
            float margin = coinSpacing;
            for (float z = margin; z < tile.length - margin; z += coinSpacing)
            {
                if (!HasCoinBudget()) return;
                PlaceIfClear(tile, lane, z);
            }
        }

        private void SpawnArc(TileInfo tile, int lane)
        {
            float margin = coinSpacing;
            float usableLength = tile.length - 2 * margin;
            int steps = Mathf.Max(1, Mathf.FloorToInt(usableLength / coinSpacing));

            for (int i = 0; i <= steps; i++)
            {
                if (!HasCoinBudget()) return;
                float t = steps == 0 ? 0f : (float)i / steps; // 0..1 across the arc
                float z = margin + t * usableLength;
                float heightOffset = Mathf.Sin(t * Mathf.PI) * arcHeight; // 0 at ends, peak in middle
                PlaceIfClear(tile, lane, z, heightOffset);
            }
        }

        private void SpawnZigzag(TileInfo tile)
        {
            float margin = coinSpacing;
            int[] laneSequence = { -1, 0, 1, 0 };
            int idx = 0;

            for (float z = margin; z < tile.length - margin; z += coinSpacing)
            {
                if (!HasCoinBudget()) return;
                int lane = laneSequence[idx % laneSequence.Length];
                PlaceIfClear(tile, lane, z);
                idx++;
            }
        }

        private void SpawnVShape(TileInfo tile, int lane)
        {
            // Dips toward ground (negative offset) then rises again - reads as "duck here" coins.
            float margin = coinSpacing;
            float usableLength = tile.length - 2 * margin;
            int steps = Mathf.Max(1, Mathf.FloorToInt(usableLength / coinSpacing));

            for (int i = 0; i <= steps; i++)
            {
                if (!HasCoinBudget()) return;
                float t = steps == 0 ? 0f : (float)i / steps;
                float z = margin + t * usableLength;
                float heightOffset = -Mathf.Sin(t * Mathf.PI) * (arcHeight * 0.5f);
                PlaceIfClear(tile, lane, z, heightOffset);
            }
        }

        /// <summary>
        /// Lanes to fill for a row, ordered so a partial row (coinsPerRow &lt; 3) stays centered
        /// rather than favoring one side: center first, then left, then right.
        /// </summary>
        private static readonly int[] CenteredLaneOrder = { 0, -1, 1 };

        /// <summary>
        /// Places repeated rows of coins spanning multiple lanes down the length of the tile.
        /// Each row contains up to `coinsPerRow` coins (one per lane), skipping any lane that's
        /// blocked by an obstacle or over the active-coin cap, so a partially-blocked row still
        /// places whatever coins are safe rather than dropping the whole row.
        /// </summary>
        private void SpawnRows(TileInfo tile)
        {
            float margin = rowSpacing;
            for (float z = margin; z < tile.length - margin; z += rowSpacing)
            {
                if (!HasCoinBudget()) return;
                SpawnSingleRow(tile, z);
            }
        }

        private void SpawnSingleRow(TileInfo tile, float localZ)
        {
            int placed = 0;
            foreach (int lane in CenteredLaneOrder)
            {
                if (placed >= coinsPerRow) break;
                if (!HasCoinBudget()) return;
                if (PlaceIfClear(tile, lane, localZ)) placed++;
            }
        }
    }
}