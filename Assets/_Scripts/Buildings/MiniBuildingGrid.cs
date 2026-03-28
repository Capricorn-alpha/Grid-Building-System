using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 微观建造用网格：逻辑与 <see cref="BuildingGrid"/> 相同，但使用更小的 <see cref="cellSize"/>。
/// 原点为物体 Transform 在 XZ 平面上的位置；格子索引 (0,0) 为角落，向 +X、+Z 延伸。
/// </summary>
public class MiniBuildingGrid : MonoBehaviour
{
    [SerializeField] private int width = 20;
    [SerializeField] private int height = 20;
    [SerializeField] private float cellSize = 0.2f;

    [Header("Gizmo（与建造模式联动）")]
    [Tooltip("指定场景中的 BuildingController；留空则自动查找第一个。仅当当前为微观模式时绘制细格。")]
    [SerializeField] private BuildingController gizmoModeController;
    [SerializeField] private Color microGizmoColor = new(0.35f, 0.82f, 1f, 0.88f);

    private BuildingGridCell[,] grid;
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

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private void Awake()
    {
        if (cellSize <= 0f)
            Debug.LogWarning($"{name}: MiniBuildingGrid cellSize 应大于 0。", this);

        grid = new BuildingGridCell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                grid[x, y] = new BuildingGridCell();
        }
    }

    public void SetBuilding(Building building, List<Vector3> allBuildingPositions)
    {
        foreach (var p in allBuildingPositions)
        {
            Vector2Int gridPos = WorldToGridPosition(p);
            if (gridPos.x < 0 || gridPos.x >= width || gridPos.y < 0 || gridPos.y >= height)
                continue;
            grid[gridPos.x, gridPos.y].SetBuilding(building);
        }
    }

    /// <param name="plannedBuilding">与宏观网格 API 一致；微观环境/矿点规则接入前不参与判定。</param>
    public bool CanBuild(List<Vector3> allBuildingPositions, BuildingData plannedBuilding = null)
    {
        _ = plannedBuilding;

        foreach (var p in allBuildingPositions)
        {
            Vector2Int gridPos = WorldToGridPosition(p);
            if (gridPos.x < 0 || gridPos.x >= width || gridPos.y < 0 || gridPos.y >= height)
                return false;
            if (!grid[gridPos.x, gridPos.y].IsEmpty())
                return false;
        }

        return true;
    }

    public Vector2Int WorldToGridPosition(Vector3 worldPosition)
    {
        if (cellSize <= 0f)
            return Vector2Int.zero;

        Vector3 local = worldPosition - transform.position;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.z / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorldCenter(Vector2Int gridPosition)
    {
        Vector3 o = transform.position;
        return new Vector3(
            o.x + (gridPosition.x + 0.5f) * cellSize,
            o.y,
            o.z + (gridPosition.y + 0.5f) * cellSize
        );
    }

    public Building GetBuildingAt(Vector2Int gridPosition)
    {
        if (grid == null)
            return null;
        if (gridPosition.x < 0 || gridPosition.x >= width || gridPosition.y < 0 || gridPosition.y >= height)
            return null;
        return grid[gridPosition.x, gridPosition.y].GetBuilding();
    }

    private void OnDrawGizmos()
    {
        BuildingController modeSource = ResolvedModeController;
        if (modeSource != null && !modeSource.IsGizmoActiveFor(BuildingController.BuildMode.Micro))
            return;

        if (cellSize <= 0f || width <= 0 || height <= 0)
            return;

        Gizmos.color = microGizmoColor;
        // 略高于宏观格，减少与地板/宏观线 Z-fight（两物体原点接近时仍可能重叠，可微调此值）
        float yLift = 0.018f;
        Vector3 origin = transform.position;

        for (int gy = 0; gy <= height; gy++)
        {
            Vector3 start = origin + new Vector3(0f, yLift, gy * cellSize);
            Vector3 end = origin + new Vector3(width * cellSize, yLift, gy * cellSize);
            Gizmos.DrawLine(start, end);
        }

        for (int gx = 0; gx <= width; gx++)
        {
            Vector3 start = origin + new Vector3(gx * cellSize, yLift, 0f);
            Vector3 end = origin + new Vector3(gx * cellSize, yLift, height * cellSize);
            Gizmos.DrawLine(start, end);
        }
    }
}
