using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BuildingDatabase : MonoBehaviour
{
    public static readonly List<BuildingData> all = new();
    public static IReadOnlyList<BuildingData> All => all;

    // 预计算后的分组索引：按宏/微观与是否物流分组，供 BuildingSystem 快速取用。
    private static readonly Dictionary<BuildScope, List<BuildingData>> allByScope = new();
    private static readonly Dictionary<BuildScope, List<BuildingData>> logisticsByScope = new();
    private static readonly Dictionary<BuildScope, List<BuildingData>> normalByScope = new();

    public void Awake()
    {
        all.Clear();
        BuildingData[] loaded = Resources.LoadAll<BuildingData>("Data");
        all.AddRange(loaded);
        RebuildIndexes();
    }

    private static void RebuildIndexes()
    {
        allByScope.Clear();
        logisticsByScope.Clear();
        normalByScope.Clear();

        foreach (BuildScope scope in System.Enum.GetValues(typeof(BuildScope)))
        {
            List<BuildingData> scoped = all.Where(d => d != null && d.Scope == scope).ToList();
            allByScope[scope] = scoped;
            logisticsByScope[scope] = scoped.Where(d => d.Category == BuildingCategory.Logistics).ToList();
            normalByScope[scope] = scoped.Where(d => d.Category != BuildingCategory.Logistics).ToList();
        }
    }

    public static IReadOnlyList<BuildingData> GetByScope(BuildScope scope)
    {
        return allByScope.TryGetValue(scope, out List<BuildingData> list) ? list : System.Array.Empty<BuildingData>();
    }

    public static IReadOnlyList<BuildingData> GetLogisticsByScope(BuildScope scope)
    {
        return logisticsByScope.TryGetValue(scope, out List<BuildingData> list) ? list : System.Array.Empty<BuildingData>();
    }

    public static IReadOnlyList<BuildingData> GetNormalByScope(BuildScope scope)
    {
        return normalByScope.TryGetValue(scope, out List<BuildingData> list) ? list : System.Array.Empty<BuildingData>();
    }
}
