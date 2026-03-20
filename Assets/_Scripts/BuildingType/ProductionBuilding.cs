using UnityEngine;
using System.Collections.Generic;

public class ProductionBuilding : Building, ItemSource
{
    [SerializeField] private BeltItem beltItemPrefab;
    [SerializeField] private float productionInterval = 5f;
    [SerializeField] private bool enableDebugLog = true;

    private float productionTimer;
    private Queue<BeltItem> buffer = new Queue<BeltItem>();
    private bool _isPlaced;
    private BuildingGrid _grid;
    private List<Vector2Int> _outputCells;

    private void Update()
    {
        if(!_isPlaced) return;

        productionTimer -= productionInterval * Time.deltaTime;
        if (productionTimer <= 0f)
        {
            productionTimer += productionInterval;
            Produce();
        }

        TryPushToOutputBelt();
    }

    public override void OnPlaced()
    {
        base.OnPlaced();
        if (enableDebugLog)
            Debug.Log($"[ProductionBuilding] OnPlaced 被调用, name={name}");
        
        _isPlaced = true;
        _grid = FindFirstObjectByType<BuildingGrid>();
        if (_grid == null)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] 未找到 BuildingGrid");
            return;
        }
        _outputCells = GetOutputPortGridCoordinates(_grid);
        if (_outputCells == null) _outputCells = new List<Vector2Int>();
    }

    public List<Vector2Int> GetOutputPortGridCoordinates(BuildingGrid grid)
    {
        var list = new List<Vector2Int>();
        if (grid == null)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] GetOutputPortGridCoordinates: grid 为 null");
            return list;
        }
        Building root = GetRootBuilding();
        if (root == null)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] GetOutputPortGridCoordinates: 未找到根 Building");
            return list;
        }
        if (root.Ports == null || root.Ports.Length == 0)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] GetOutputPortGridCoordinates: Ports 为空");
            return list;
        }
        foreach (var port in root.Ports)
        {
            if (port == null || port.PortType != PortType.Output) continue;
            Vector3 worldInFront = port.transform.position + (port.transform.forward * -0.5f) * BuildingSystem.CellSize;
            Vector2Int cell = grid.WorldToGridPosition(worldInFront);
            list.Add(cell);
            if (enableDebugLog)
                Debug.Log($"[ProductionBuilding] 输出口 port={port.name} -> 格子 ({cell.x}, {cell.y})");
        }
        if (enableDebugLog && list.Count > 0)
        {
            var coords = string.Join(", ", System.Array.ConvertAll(list.ToArray(), c => $"({c.x}, {c.y})"));
            Debug.Log($"[ProductionBuilding] 输出口所在格子: {coords}");
        }
        return list;
    }

    private Building GetRootBuilding()
    {
        Transform current = transform.parent;
        while (current != null)
        {
            var b = current.GetComponent<Building>();
            if (b != null && b != this) return b;
            current = current.parent;
        }
        return null;
    }

    private void Produce()
    {
        if (beltItemPrefab == null)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] Produce: beltItemPrefab 未设置");
            return;
        }
        if (buffer == null) buffer = new Queue<BeltItem>();
        Building root = GetRootBuilding();
        BuildingPort outPort = GetFirstOutputPort(root);
        Vector3 spawnPos = outPort != null
            ? outPort.transform.position + Vector3.up * 0.3f
            : transform.position + Vector3.up * 0.3f;
        BeltItem newItem = Instantiate(beltItemPrefab, spawnPos, Quaternion.identity);
        buffer.Enqueue(newItem);
        if (enableDebugLog) Debug.Log($"[ProductionBuilding] Produce: 生成物品, buffer={buffer.Count}");
    }

    private static BuildingPort GetFirstOutputPort(Building root)
    {
        if (root?.Ports == null) return null;
        foreach (var port in root.Ports)
        {
            if (port != null && port.PortType == PortType.Output) return port;
        }
        return null;
    }

    private void TryPushToOutputBelt()
    {   
        if(_grid == null || buffer.Count == 0) return;

        foreach(var cell in _outputCells)
        {
            Debug.Log($"[ProductionBuilding] 尝试推送到输出口格子 ({cell.x}, {cell.y})");
            Building buildingAtCell = _grid.GetBuildingAt(cell);
            if(buildingAtCell == null) continue;
            Debug.Log($"[ProductionBuilding] 未检测到建筑, cell: ({cell.x}, {cell.y})");

            Belt belt = buildingAtCell.GetComponentInChildren<Belt>(true);
            Debug.Log($"[ProductionBuilding] 检测到传送带, belt: {belt.name}");
            if(belt == null) continue;

            Debug.Log($"[ProductionBuilding] 二次检测传送带, belt: {belt.name}");
            if(!belt.MatchOutputGrid(_grid, cell)) continue;

            BeltItem frontItem = buffer.Peek();
            if(belt.TryInputItem(frontItem))
            {   
                Debug.Log($"[ProductionBuilding] 输出口格子 ({cell.x}, {cell.y}) 检测到传送带，已推送物品");
                buffer.Dequeue();
                return;
            }
        }
    }

    public bool TryOutputItem(out BeltItem item)
    {
        if (buffer.Count > 0)
        {
            item = buffer.Dequeue();
            return true;
        }
        item = null;
        return false;
    }
}
