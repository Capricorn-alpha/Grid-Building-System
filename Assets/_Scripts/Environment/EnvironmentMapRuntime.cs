using System.Collections.Generic;
using UnityEngine;

public class EnvironmentMapRuntime : MonoBehaviour
{
    [SerializeField] private EnvironmentMapData mapData;
    [SerializeField] private BuildingGrid grid;
    [SerializeField] private Transform environmentRoot;
    [SerializeField] private bool spawnPrefabsOnStart = true;

    private Dictionary<Vector2Int, EnvironmentCellEntry> _lookup = new();

    private void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<BuildingGrid>();
        RebuildLookup();
    }

    private void Start()
    {
        if (!spawnPrefabsOnStart || grid == null) return;
        SpawnAllPrefabs();
    }

    public void RebuildLookup()
    {
        _lookup = mapData != null ? mapData.BuildLookup() : new Dictionary<Vector2Int, EnvironmentCellEntry>();
    }

    public bool TryGetEntry(Vector2Int cell, out EnvironmentCellEntry entry)
    {
        entry = null;
        return _lookup != null && _lookup.TryGetValue(cell, out entry) && entry != null;
    }

    public bool TryGetOreResourceId(Vector2Int cell, out string resourceId)
    {
        resourceId = null;
        if (!TryGetEntry(cell, out var entry)) return false;
        if (entry.tileType != EnvironmentTileType.Ore) return false;
        resourceId = entry.resourceId;
        return !string.IsNullOrWhiteSpace(resourceId);
    }

    public bool CanPlaceBuilding(Vector2Int cell, BuildingData buildingData)
    {
        if (!TryGetEntry(cell, out var entry)) return true;

        if (entry.tileType == EnvironmentTileType.Ore)
            return buildingData != null && buildingData.Category == BuildingCategory.Miner;

        if (entry.tileType == EnvironmentTileType.Blocked)
            return false;

        return true;
    }

    private void SpawnAllPrefabs()
    {
        foreach (var kvp in _lookup)
        {
            EnvironmentCellEntry entry = kvp.Value;
            if (entry == null || entry.prefab == null) continue;

            Vector3 worldPos = GridToWorldCenter(kvp.Key);
            Transform parent = environmentRoot != null ? environmentRoot : transform;
            Instantiate(entry.prefab, worldPos, Quaternion.identity, parent);
        }
    }

    private Vector3 GridToWorldCenter(Vector2Int cell)
    {
        Vector3 o = grid.transform.position;
        return new Vector3(
            o.x + (cell.x + 0.5f) * BuildingSystem.CellSize,
            o.y,
            o.z + (cell.y + 0.5f) * BuildingSystem.CellSize
        );
    }
}
