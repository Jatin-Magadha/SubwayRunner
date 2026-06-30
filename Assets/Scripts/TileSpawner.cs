using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns track "tile" prefabs (each containing a section of track, plus its own
/// obstacles/coins already placed as children) ahead of the player, and
/// despawns/recycles tiles that fall behind the player.
///
/// Design: each Tile prefab is a fixed length (tileLength) along Z, and may
/// itself contain ObstacleHit and Coin objects as children, pre-arranged by lane.
/// </summary>
public class TileSpawner : MonoBehaviour
{
    [Header("Tile Prefabs")]
    public GameObject[] tilePrefabs;     // variety of track segments
    public GameObject startTilePrefab;   // a safe, obstacle-free starting tile

    [Header("Spawn Settings")]
    public float tileLength = 30f;
    public int tilesAheadCount = 6;      // how many tiles stay active ahead of player
    public Transform player;

    private readonly List<GameObject> activeTiles = new List<GameObject>();
    private float nextSpawnZ;

    public void ResetSpawner()
    {
        foreach (var tile in activeTiles)
            Destroy(tile);
        activeTiles.Clear();

        nextSpawnZ = 0f;

        // place a few safe tiles first
        SpawnTile(startTilePrefab);
        for (int i = 0; i < tilesAheadCount; i++)
        {
            SpawnRandomTile();
        }
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

        // Spawn new tiles if player is getting close to the end of spawned track
        if (player.position.z > nextSpawnZ - tilesAheadCount * tileLength)
        {
            SpawnRandomTile();
        }

        RecycleOldTiles();
    }

    private void SpawnRandomTile()
    {
        GameObject prefab = tilePrefabs[Random.Range(0, tilePrefabs.Length)];
        SpawnTile(prefab);
    }

    private void SpawnTile(GameObject prefab)
    {
        GameObject tile = Instantiate(prefab, new Vector3(0, 0, nextSpawnZ), Quaternion.identity, transform);
        activeTiles.Add(tile);
        nextSpawnZ += tileLength;
    }

    private void RecycleOldTiles()
    {
        // Remove tiles that are far enough behind the player to be invisible
        float despawnThreshold = player.position.z - tileLength * 2f;

        for (int i = activeTiles.Count - 1; i >= 0; i--)
        {
            GameObject tile = activeTiles[i];
            if (tile.transform.position.z < despawnThreshold)
            {
                activeTiles.RemoveAt(i);
                Destroy(tile);
            }
        }
    }
}