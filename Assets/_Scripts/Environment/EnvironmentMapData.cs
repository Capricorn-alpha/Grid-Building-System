using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnvironmentTileType
{
    None = 0,
    Ore = 1,
    Blocked = 2
}

[Serializable]
public class EnvironmentCellEntry
{
    public Vector2Int cell;
    public EnvironmentTileType tileType = EnvironmentTileType.None;
    public string resourceId = "ore";
    public GameObject prefab;
}

[CreateAssetMenu(menuName = "Data/Environment Map")]
public class EnvironmentMapData : ScriptableObject
{
    [SerializeField] private List<EnvironmentCellEntry> entries = new();

    public IReadOnlyList<EnvironmentCellEntry> Entries => entries;

    public Dictionary<Vector2Int, EnvironmentCellEntry> BuildLookup(bool logConflict = true)
    {
        var lookup = new Dictionary<Vector2Int, EnvironmentCellEntry>();
        foreach (var entry in entries)
        {
            if (lookup.TryGetValue(entry.cell, out var existing))
            {
                if (logConflict)
                    Debug.LogWarning($"[EnvironmentMapData] Duplicate cell {entry.cell}. Keep first={existing.tileType}, skip={entry.tileType}.");
                continue;
            }
            lookup[entry.cell] = entry;
        }
        return lookup;
    }
}
