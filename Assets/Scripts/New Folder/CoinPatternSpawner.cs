using System.Collections.Generic;
using UnityEngine;

namespace SubwaySurferClone
{
    public enum CoinPattern
    {
        StraightLine,   // a run of coins down one lane
        Arc,            // an upward arc, as if collected mid-jump
        Zigzag,         // weaves left-center-right-center...
        VShape          // dips down then back up (duck-under coins)
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
            if (_rng.NextDouble() < chanceToSkipTile) return;

            int lane = RandomLane();
            CoinPattern pattern = (CoinPattern)_rng.Next(0, 4);

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
            }
        }

        private int RandomLane() => _rng.Next(-1, 2); // -1, 0, 1

        private bool PlaceIfClear(TileInfo tile, int lane, float localZ, float yOffset = 0f)
        {
            if (tile.IsLaneBlockedAt(lane, localZ)) return false;

            float worldZ = tile.StartZ + localZ;
            float worldX = tile.GetLaneX(lane);
            Vector3 pos = new Vector3(worldX, coinHeight + yOffset, worldZ);
            _coinPool.Get(pos, Quaternion.identity);
            return true;
        }

        private void SpawnStraightLine(TileInfo tile, int lane)
        {
            float margin = coinSpacing;
            for (float z = margin; z < tile.length - margin; z += coinSpacing)
            {
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
                float t = steps == 0 ? 0f : (float)i / steps;
                float z = margin + t * usableLength;
                float heightOffset = -Mathf.Sin(t * Mathf.PI) * (arcHeight * 0.5f);
                PlaceIfClear(tile, lane, z, heightOffset);
            }
        }
    }
}
