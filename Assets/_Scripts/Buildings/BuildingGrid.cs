using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BuildingGrid : MonoBehaviour
{
    [SerializeField] private int width;

    [SerializeField] private int height;
    private BuildingGridCell[,] grid;
    [SerializeField] private EnvironmentMapRuntime environmentMap;

    private void Awake()
    {
        grid = new BuildingGridCell[width, height];
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                grid[x, y] = new();
            }
        }
        if (environmentMap == null) environmentMap = FindFirstObjectByType<EnvironmentMapRuntime>();
    }

    public void SetBuilding(Building building, List<Vector3> allBuildingPositions)
    {
        foreach (var p in allBuildingPositions)
        {
            Vector2Int gridPos = WorldToGridPosition(p);
            grid[gridPos.x, gridPos.y].SetBuilding(building);
        }
    }

    /// <param name="plannedBuilding">当前预览/即将放置的建筑数据；为 null 时矿点上禁止任何建造（保守策略）。</param>
    public bool CanBuild(List<Vector3> allBuildingPositions, BuildingData plannedBuilding = null)
    {
        foreach (var p in allBuildingPositions)
        {
            Vector2Int gridPos = WorldToGridPosition(p);
            if(gridPos.x < 0 || gridPos.x >= width || gridPos.y < 0 || gridPos.y >= height) return false;
            if(!grid[gridPos.x, gridPos.y].IsEmpty()) return false;
            if (environmentMap != null && !environmentMap.CanPlaceBuilding(gridPos, plannedBuilding)) return false;
        }
        return true;
    }

    public bool TryGetOreResourceIdAt(Vector2Int gridPosition, out string resourceId)
    {
        resourceId = null;
        return environmentMap != null && environmentMap.TryGetOreResourceId(gridPosition, out resourceId);
    }

    public Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition - transform.position).x / BuildingSystem.CellSize);
        int y = Mathf.FloorToInt((worldPosition - transform.position).z / BuildingSystem.CellSize);
        return new Vector2Int(x, y);   
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        if(BuildingSystem.CellSize <= 0 || width <= 0 || height <= 0) return;
        Vector3 origin = transform.position;
        for(int y = 0; y <= height; y++)
        {
            Vector3 start = origin + new Vector3(0, 0.01f, y * BuildingSystem.CellSize);
            Vector3 end = origin + new Vector3(width * BuildingSystem.CellSize, 0.01f, y * BuildingSystem.CellSize);
            Gizmos.DrawLine(start, end);
        }

        for(int x = 0; x <= width; x++)
        {
            Vector3 start = origin + new Vector3(x * BuildingSystem.CellSize, 0.01f, 0);
            Vector3 end = origin + new Vector3(x * BuildingSystem.CellSize, 0.01f, height * BuildingSystem.CellSize);
            Gizmos.DrawLine(start, end);
        }
    }

    public Building GetBuildingAt(Vector2Int gridPosition)
    {
        if(grid == null) return null;
        if(gridPosition.x < 0 || gridPosition.x >= width || gridPosition.y < 0 || gridPosition.y >= height) 
        {
            return null;
        }
        return grid[gridPosition.x, gridPosition.y].GetBuilding();
    }
}

public class BuildingGridCell
{
    private Building building;

    public void SetBuilding(Building building)
    {
        this.building = building;
    }

    public bool IsEmpty()
    {
        return building == null;
    }

    public Building GetBuilding()
    {
        return building;
    }
}
