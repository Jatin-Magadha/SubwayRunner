using UnityEngine;

namespace SubwaySurferClone
{
    /// <summary>
    /// Attach this to the root of every tile prefab (straight, curve, ramp, obstacle-tile, etc).
    /// It tells the TileGenerator how long the tile is and where things may be placed on it,
    /// and tells the CoinPatternSpawner where it's safe to drop coins (avoiding obstacles).
    /// </summary>
    public class TileInfo : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("Length of this tile along the Z axis. Next tile is placed exit-to-entry.")]
        public float length = 30f;

        [Tooltip("Distance between the 3 lanes (left/center/right).")]
        public float laneWidth = 3f;

        [Header("Obstacles on this tile")]
        [Tooltip("Z-position ranges (local, 0 = tile start) that already contain an obstacle. " +
                 "Coin patterns will skip lanes/ranges that overlap these so coins never spawn inside a wall/train.")]
        public ObstacleZone[] obstacleZones;

        [Header("Coin Spawning")]
        [Tooltip("If false, this tile type never gets coins (e.g. a tutorial or boss tile).")]
        public bool allowCoins = true;

        [Tooltip("Optional explicit points where a jump/duck coin arc should be centered (e.g. above a low barrier).")]
        public Transform[] specialArcAnchors;

        public float StartZ => transform.position.z;
        public float EndZ => transform.position.z + length;

        public float GetLaneX(int lane)
        {
            // lane: -1 = left, 0 = center, 1 = right
            return transform.position.x + lane * laneWidth;
        }

        public bool IsLaneBlockedAt(int lane, float localZ)
        {
            if (obstacleZones == null) return false;
            foreach (var zone in obstacleZones)
            {
                if (zone.lane == lane && localZ >= zone.zStart && localZ <= zone.zEnd)
                    return true;
                if (zone.lane == TileInfo.AllLanes && localZ >= zone.zStart && localZ <= zone.zEnd)
                    return true;
            }
            return false;
        }

        public const int AllLanes = 99;
    }

    [System.Serializable]
    public struct ObstacleZone
    {
        [Tooltip("-1 left, 0 center, 1 right, or TileInfo.AllLanes for a full-width obstacle")]
        public int lane;
        public float zStart;
        public float zEnd;
    }
}
