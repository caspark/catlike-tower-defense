using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class GameBoard : MonoBehaviour {
    public enum PlaceResult {
        // Content was successfully put on tile
        Success,
        // Content was already on tile so it was cleared
        Cleared,
        // Content cannot be placed because it would make paths unroutable
        PathingFailure,
        // Content cannot be placed because existing content cannot be replaced with this content
        ConflictFailure
    }

    [SerializeField] private Transform ground = default;

    [SerializeField] private GameTile tilePrefab = default;

    [SerializeField] Texture2D gridTexture = default;

    bool showPaths = false;

    public bool ShowPaths {
        get => showPaths;
        set {
            showPaths = value;
            if (showPaths) {
                foreach (GameTile tile in tiles) {
                    tile.ShowPath();
                }
            }
            else {
                foreach (GameTile tile in tiles) {
                    tile.HidePath();
                }
            }
        }
    }

    bool showGrid = false;

    public bool ShowGrid {
        get => showGrid;
        set {
            showGrid = value;
            Material m = ground.GetComponent<MeshRenderer>().material;
            if (showGrid) {
                m.mainTexture = gridTexture;
                m.SetTextureScale("_BaseMap", size);
            }
            else {
                m.mainTexture = null;
            }
        }
    }

    public int SpawnPointCount => spawnPoints.Count;

    GameTileContentFactory contentFactory;

    [ShowInInspector]
    Vector2Int size;

    [ShowInInspector]
    GameTile[] tiles = new GameTile[0];

    Queue<GameTile> searchFrontier = new Queue<GameTile>();
    private List<GameTile> spawnPoints = new List<GameTile>();

    private List<GameTileContent> updatingContent = new List<GameTileContent>();

    public void Initialize(Vector2Int size, GameTileContentFactory contentFactory) {
        this.size = size;
        this.contentFactory = contentFactory;
        ground.localScale = new Vector3(size.x, size.y, 1);

        Vector2 offset = new Vector2((size.x - 1) * .5f, (size.y - 1) * .5f);
        tiles = new GameTile[size.x * size.y];
        int i = 0;
        for (int y = 0; y < size.y; y++) {
            for (int x = 0; x < size.x; x++) {
                GameTile tile = Instantiate(tilePrefab, ground);
                tile.transform.SetParent(transform, false);
                tile.transform.localPosition = new Vector3(x - offset.x, 0, y - offset.y);
                tile.IsAlternative = (i & 1) == 0;

                if (x > 0) {
                    GameTile.MakeEastWestNeighbors(tile, tiles[i - 1]);
                }
                if (y > 0) {
                    GameTile.MakeNorthSouthNeighbors(tile, tiles[i - size.x]);
                }

                tiles[i] = tile;
                i++;
            }
        }

        Clear();
    }

    public void GameUpdate() {
        for (int i = 0; i < updatingContent.Count; i++) {
            updatingContent[i].GameUpdate();
        }
    }

    public void Clear() {
        foreach (GameTile tile in tiles) {
            tile.Content = contentFactory.Get(GameTileContentType.Empty);
        }
        spawnPoints.Clear();
        updatingContent.Clear();
    }

    private bool FindPaths() {
        foreach (GameTile tile in tiles) {
            if (tile.Content.Type == GameTileContentType.Destination) {
                tile.BecomeDestination();
                searchFrontier.Enqueue(tile);
            }
            else {
                tile.ClearPath();
            }
        }
        if (searchFrontier.Count == 0) {
            return false;
        }

        while (searchFrontier.Count > 0) {
            GameTile tile = searchFrontier.Dequeue();
            if (tile != null) {
                if (tile.IsAlternative) {
                    searchFrontier.Enqueue(tile.GrowPathNorth());
                    searchFrontier.Enqueue(tile.GrowPathSouth());
                    searchFrontier.Enqueue(tile.GrowPathEast());
                    searchFrontier.Enqueue(tile.GrowPathWest());
                }
                else {
                    searchFrontier.Enqueue(tile.GrowPathWest());
                    searchFrontier.Enqueue(tile.GrowPathEast());
                    searchFrontier.Enqueue(tile.GrowPathSouth());
                    searchFrontier.Enqueue(tile.GrowPathNorth());
                }
            }
        }

        foreach (GameTile tile in tiles) {
            if (!tile.HasPath) {
                return false;
            }
        }

        if (showPaths) {
            foreach (GameTile tile in tiles) {
                tile.ShowPath();
            }
        }

        return true;
    }

    public void ToggleSpawnPoint(GameTile tile) {
        if (tile.Content.Type == GameTileContentType.SpawnPoint) {
            if (spawnPoints.Count > 1) {
                spawnPoints.Remove(tile);
                tile.Content = contentFactory.Get(GameTileContentType.Empty);
            }
        }
        else if (tile.Content.Type == GameTileContentType.Empty) {
            tile.Content = contentFactory.Get(GameTileContentType.SpawnPoint);
            spawnPoints.Add(tile);
        }
    }

    public void ToggleDestination(GameTile tile) {
        if (tile.Content.Type == GameTileContentType.Destination) {
            tile.Content = contentFactory.Get(GameTileContentType.Empty);
            if (!FindPaths()) {
                tile.Content = contentFactory.Get(GameTileContentType.Destination);
                FindPaths();
            }
        }
        else if (tile.Content.Type == GameTileContentType.Empty) {
            tile.Content = contentFactory.Get(GameTileContentType.Destination);
            FindPaths();
        }
    }

    public PlaceResult PlaceWall(GameTile tile) {
        if (tile.Content.Type == GameTileContentType.Wall) {
            tile.Content = contentFactory.Get(GameTileContentType.Empty);
            FindPaths();
            return PlaceResult.Cleared;
        }
        else if (tile.Content.Type == GameTileContentType.Empty) {
            tile.Content = contentFactory.Get(GameTileContentType.Wall);
            if (FindPaths()) {
                return PlaceResult.Success;
            }
            else {
                tile.Content = contentFactory.Get(GameTileContentType.Empty);
                FindPaths();
                return PlaceResult.PathingFailure;
            }
        }
        else {
            return PlaceResult.ConflictFailure;
        }
    }

    public PlaceResult PlaceTower(GameTile tile, TowerType towerType) {
        if (tile.Content.Type == GameTileContentType.Tower) {
            updatingContent.Remove(tile.Content);
            if (((Tower)tile.Content).TowerType == towerType) {
                tile.Content = contentFactory.Get(GameTileContentType.Empty);
                FindPaths();
                return PlaceResult.Cleared;
            }
            else {
                tile.Content = contentFactory.Get(towerType);
                updatingContent.Add(tile.Content);
                return PlaceResult.Success;
            }
        }
        else if (tile.Content.Type == GameTileContentType.Empty) {
            tile.Content = contentFactory.Get(towerType);
            if (FindPaths()) {
                updatingContent.Add(tile.Content);
                return PlaceResult.Success;
            }
            else {
                tile.Content = contentFactory.Get(GameTileContentType.Empty);
                FindPaths();
                return PlaceResult.Cleared;
            }
        }
        else if (tile.Content.Type == GameTileContentType.Wall) {
            tile.Content = contentFactory.Get(towerType);
            updatingContent.Add(tile.Content);
            return PlaceResult.Success;
        }
        else {
            return PlaceResult.ConflictFailure;
        }
    }

    public GameTile GetTile(int index) {
        return tiles[index];
    }

    public GameTile GetTile(int x, int y) {
        return tiles[y * size.x + x];
    }

    public GameTile GetTile(Ray ray) {
        if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, 1)) {
            int x = (int)(hit.point.x + size.x * 0.5f);
            int y = (int)(hit.point.z + size.y * 0.5f);
            if (x >= 0 && x < size.x && y >= 0 && y < size.y) {
                return tiles[x + y * size.x];
            }
        }
        return null;
    }

    public GameTile GetSpawnPoint(int index) {
        return spawnPoints[index];
    }

}
