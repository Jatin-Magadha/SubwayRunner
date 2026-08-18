using System.Collections.Generic;
using UnityEngine;

namespace SubwaySurferClone
{
    /// <summary>
    /// The core of the endless runner: keeps a rolling window of tile prefabs
    /// ahead of the player, recycles ones that fall behind, and hands each new
    /// tile off (e.g. to a TileCoinSpawner) to be filled with coins.
    ///
    /// Setup:
    /// 1. Drag your tile prefabs (each with a TileInfo component) into "tilePrefabs".
    /// 2. Assign "player" to the runner's transform.
    /// 3. Set how many tiles should exist ahead of the player at once ("tilesAhead").
    ///
    /// Restart flow:
    /// - GameManager.onMenuClicked  -> tiles are recycled back to their pools, nothing spawns.
    /// - GameManager.onGameStarted  -> any leftover tiles are recycled first (so this is safe
    ///   to call even if onMenuClicked never fired first - e.g. a "restart" button that jumps
    ///   straight back into a run), THEN a fresh set is spawned from z = 0.
    ///
    /// This is the ONLY place tiles get spawned - there is no separate spawn call in Start().
    /// Previously, Start() spawned an initial set AND GameManager_onGameStarted spawned
    /// another full set on top of it (both starting at z = 0), which is what caused tiles
    /// to visibly overlap at the starting position after a restart.
    /// </summary>
    public class EndlessTileGenerator : MonoBehaviour
    {
        [Header("Tile Prefabs")]
        [Tooltip("All possible tile prefabs. Each needs a TileInfo component.")]
        public GameObject[] tilePrefabs;

        [Tooltip("Guaranteed-safe starting tiles used for the first few slots (e.g. a straight tile with no obstacles).")]
        public GameObject[] startingTilePrefabs;
        public int startingTileCount = 3;

        [Header("References")]
        public Transform player;

        [Header("Streaming Settings")]
        [Tooltip("How many tiles ahead of the player's current tile should exist at once.")]
        public int tilesAhead = 5;
        [Tooltip("How many tiles behind the player to keep before recycling (small buffer avoids pop-out).")]
        public int tilesBehindBuffer = 1;

        [Header("Repeat Avoidance")]
        [Tooltip("Don't spawn the exact same tile prefab this many times in a row.")]
        public int noImmediateRepeatWindow = 2;

        private readonly Dictionary<GameObject, ObjectPool> _tilePools = new Dictionary<GameObject, ObjectPool>();
        private readonly LinkedList<ActiveTile> _activeTiles = new LinkedList<ActiveTile>();
        private readonly List<GameObject> _recentPrefabHistory = new List<GameObject>();

        private float _nextSpawnZ = 0f;
        private readonly System.Random _rng = new System.Random();

        /// <summary>
        /// True only while a run is actually in progress. Update() no-ops while this is
        /// false, so nothing spawns or recycles while sitting at the menu.
        /// </summary>
        private bool _isRunning = false;

        private struct ActiveTile
        {
            public GameObject instance;
            public GameObject sourcePrefab;
            public TileInfo info;
        }

        private void Awake()
        {
            BuildPoolsIfNeeded();
        }

        /// <summary>
        /// Creates a pool per unique prefab, once. Called only from Awake - restarts reuse
        /// these same pools (via ClearActiveTiles/SpawnTile) instead of rebuilding them,
        /// so no GameObjects get orphaned across repeated menu <-> game transitions.
        /// </summary>
        private void BuildPoolsIfNeeded()
        {
            foreach (var prefab in tilePrefabs)
                if (!_tilePools.ContainsKey(prefab))
                    _tilePools[prefab] = new ObjectPool(prefab, transform, prewarmCount: 2);

            foreach (var prefab in startingTilePrefabs)
                if (!_tilePools.ContainsKey(prefab))
                    _tilePools[prefab] = new ObjectPool(prefab, transform, prewarmCount: 2);
        }

        private void OnEnable()
        {
            GameManager.Instance.onGameStarted += GameManager_onGameStarted;
            GameManager.Instance.onMenuClicked += GameManager_onMenuClicked;
        }

        private void OnDisable()
        {
            GameManager.Instance.onGameStarted -= GameManager_onGameStarted;
            GameManager.Instance.onMenuClicked -= GameManager_onMenuClicked;
        }

        private void GameManager_onGameStarted(object sender, System.EventArgs e)
        {
            // Recycle anything still active first. This guards against double-spawning
            // even if this event fires while tiles from a previous run are still around.
            ClearActiveTiles();

            _nextSpawnZ = 0f;
            _recentPrefabHistory.Clear();
            _isRunning = true;

            SpawnInitialSet();
        }

        private void GameManager_onMenuClicked(object sender, System.EventArgs e)
        {
            _isRunning = false;
            ClearActiveTiles();
        }

        private void Update()
        {
            if (!_isRunning || player == null) return;

            // Spawn ahead: if the player has advanced enough that fewer than
            // `tilesAhead` tiles remain in front of them, add another.
            while (CountTilesAheadOfPlayer() < tilesAhead)
            {
                SpawnTile(PickNextPrefab());
            }

            RecycleTilesBehindPlayer();
        }

        /// <summary>Spawns the fixed starting tiles, then fills up to tilesAhead with random ones.</summary>
        private void SpawnInitialSet()
        {
            for (int i = 0; i < startingTileCount && startingTilePrefabs.Length > 0; i++)
            {
                SpawnTile(startingTilePrefabs[i % startingTilePrefabs.Length]);
            }
            while (_activeTiles.Count < tilesAhead)
            {
                SpawnTile(PickNextPrefab());
            }
        }

        /// <summary>
        /// Recycles every currently active tile back into its own pool and empties the
        /// active list. Pools themselves are kept alive (not rebuilt) so their existing
        /// inactive instances get reused on the next spawn instead of leaking orphaned
        /// GameObjects and instantiating brand-new ones on every restart.
        /// </summary>
        private void ClearActiveTiles()
        {
            while (_activeTiles.Count > 0)
            {
                ActiveTile oldest = _activeTiles.First.Value;
                if (oldest.sourcePrefab != null && _tilePools.TryGetValue(oldest.sourcePrefab, out ObjectPool pool))
                {
                    pool.Release(oldest.instance);
                }
                _activeTiles.RemoveFirst();
            }
        }

        private int CountTilesAheadOfPlayer()
        {
            int count = 0;
            foreach (var t in _activeTiles)
            {
                if (t.info.EndZ > player.position.z) count++;
            }
            return count;
        }

        private void RecycleTilesBehindPlayer()
        {
            while (_activeTiles.Count > 0)
            {
                ActiveTile oldest = _activeTiles.First.Value;
                float bufferDistance = tilesBehindBuffer * (oldest.info != null ? oldest.info.length : 30f);

                if (oldest.info.EndZ < player.position.z - bufferDistance)
                {
                    _tilePools[oldest.sourcePrefab].Release(oldest.instance);
                    _activeTiles.RemoveFirst();
                }
                else
                {
                    break; // list is ordered oldest->newest by Z, so we can stop here
                }
            }
        }

        private GameObject PickNextPrefab()
        {
            if (tilePrefabs.Length == 0) return null;

            GameObject chosen;
            int guard = 0;
            do
            {
                chosen = tilePrefabs[_rng.Next(tilePrefabs.Length)];
                guard++;
            }
            while (_recentPrefabHistory.Contains(chosen) && guard < 20 && tilePrefabs.Length > noImmediateRepeatWindow);

            _recentPrefabHistory.Add(chosen);
            if (_recentPrefabHistory.Count > noImmediateRepeatWindow)
                _recentPrefabHistory.RemoveAt(0);

            return chosen;
        }

        private void SpawnTile(GameObject prefab)
        {
            if (prefab == null) return;

            Vector3 spawnPos = new Vector3(0f, 0f, _nextSpawnZ);
            GameObject instance = _tilePools[prefab].Get(spawnPos, Quaternion.identity);
            TileInfo info = instance.GetComponent<TileInfo>();

            if (info == null)
            {
                Debug.LogWarning($"Tile prefab '{prefab.name}' is missing a TileInfo component.");
                _nextSpawnZ += 30f; // fallback length
            }
            else
            {
                _nextSpawnZ = info.EndZ;
            }

            _activeTiles.AddLast(new ActiveTile
            {
                instance = instance,
                sourcePrefab = prefab,
                info = info
            });
        }

        /// <summary>Total distance generated so far — handy for a "distance" score readout.</summary>
        public float DistanceGenerated => _nextSpawnZ;
    }
}