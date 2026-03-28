using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BuildingGrid : MonoBehaviour
{
    [SerializeField] private int width;

    [SerializeField] private int height;
    private BuildingGridCell[,] grid;
    [SerializeField] private EnvironmentMapRuntime environmentMap;

    [Header("Gizmo（与建造模式联动）")]
    [Tooltip("指定场景中的 BuildingController；留空则自动查找第一个。仅当当前为宏观模式时绘制黄格。")]
    [SerializeField] private BuildingController gizmoModeController;
    [SerializeField] private Color macroGizmoColor = new(1f, 0.85f, 0.2f, 0.9f);

    private BuildingController _resolvedModeController;

    private BuildingController ResolvedModeController
    {
        get
        {
            if (gizmoModeController != null)
                return gizmoModeController;
            if (_resolvedModeController == null)
                _resolvedModeController = FindFirstObjectByType<BuildingController>();
            return _resolvedModeController;
        }
    }

    private void OnEnable()
    {
        _resolvedModeController = null;
    }

    private void Reset()
    {
        gizmoModeController = FindFirstObjectByType<BuildingController>();
    }

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

    private void OnDrawGizmos()
    {
        BuildingController modeSource = ResolvedModeController;
        if (modeSource != null && !modeSource.IsGizmoActiveFor(BuildingController.BuildMode.Macro))
            return;

        if (BuildingSystem.CellSize <= 0f || width <= 0 || height <= 0)
            return;

        Gizmos.color = macroGizmoColor;
        float yLift = 0.01f;
        Vector3 origin = transform.position;
        float cs = BuildingSystem.CellSize;

        for (int gy = 0; gy <= height; gy++)
        {
            Vector3 start = origin + new Vector3(0f, yLift, gy * cs);
            Vector3 end = origin + new Vector3(width * cs, yLift, gy * cs);
            Gizmos.DrawLine(start, end);
        }

        for (int gx = 0; gx <= width; gx++)
        {
            Vector3 start = origin + new Vector3(gx * cs, yLift, 0f);
            Vector3 end = origin + new Vector3(gx * cs, yLift, height * cs);
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
