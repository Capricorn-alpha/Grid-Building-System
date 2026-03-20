using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BuildingDatabase : MonoBehaviour
{
    public static readonly List<BuildingData> all = new();
    public static IReadOnlyList<BuildingData> All => all;
    public void Awake()
    {
        all.Clear();
        BuildingData[] loaded = Resources.LoadAll<BuildingData>("Data");
        all.AddRange(loaded);
    }
}
