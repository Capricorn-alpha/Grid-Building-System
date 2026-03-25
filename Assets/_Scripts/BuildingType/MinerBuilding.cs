using UnityEngine;
using System.Collections.Generic;

public class MinerBuilding : Building, ItemSource
{
    [SerializeField] private BeltItem beltItemPrefab;
    [SerializeField] private float productionInterval = 5f;
    [SerializeField] private bool enableDebugLog = true;

    private float productionTimer;
    private int _pendingOutputCount;
    private bool _isPlaced;
    private bool _canMine;
    private string _resourceId;
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
        _isPlaced = true;
        productionTimer = productionInterval;
        _grid = FindFirstObjectByType<BuildingGrid>();
        if (_grid == null)
        {
            if (enableDebugLog) Debug.LogWarning("[MinerBuilding] Can't find BuildingGrid");
            return;
        }

        _outputCells = GetOutputPortGridCoordinates(_grid);
        if (_outputCells == null) _outputCells = new List<Vector2Int>();

        if (enableDebugLog)
            LogOutputPortsOnPlaced();

        Vector2Int minerCell = _grid.WorldToGridPosition(transform.position);
        _canMine = _grid.TryGetOreResourceIdAt(minerCell, out _resourceId);
        if (enableDebugLog)
        {
            if (_canMine) Debug.Log($"[MinerBuilding] on ore node. resourceId={_resourceId}");
            else Debug.Log("[MinerBuilding] not on ore node, miner will stay idle.");
        }
    }

    private void LogOutputPortsOnPlaced()
    {
        Building root = GetRootBuilding();
        if (root == null)
        {
            Debug.LogWarning("[MinerBuilding] OnPlaced: 未找到根 Building（请检查 Miner 组件层级是否在 Building 子物体上）。");
            return;
        }

        if (root.Ports == null || root.Ports.Length == 0)
        {
            Debug.LogWarning($"[MinerBuilding] OnPlaced: 根物体 '{root.name}' 上没有 BuildingPort。");
            return;
        }

        int outputCount = 0;
        foreach (var p in root.Ports)
        {
            if (p != null && p.PortType == PortType.Output) outputCount++;
        }

        if (outputCount == 0)
            Debug.LogWarning($"[MinerBuilding] OnPlaced: '{root.name}' 共有 {root.Ports.Length} 个端口，但没有 PortType.Output 的输出口。");
        else
            Debug.Log($"[MinerBuilding] OnPlaced: '{root.name}' 检测到 {outputCount} 个输出口。");

        foreach (var port in root.Ports)
        {
            if (port == null || port.PortType != PortType.Output) continue;
            Vector3 portWorld = port.transform.position;
            Vector2Int targetCell = GetGridCellAtPortWorld(_grid, portWorld);
            Debug.Log($"[MinerBuilding] 输出口 '{port.name}': 端口世界坐标={portWorld}, 目标网格=({targetCell.x},{targetCell.y})");
        }
    }

    private void Produce()
    {
        if (!_canMine) return;
        if (beltItemPrefab == null)
        {
            if (enableDebugLog) Debug.LogWarning("[MinerBuilding] beltItemPrefab is not set");
            return;
        }
        _pendingOutputCount++;
    }

    private void TryPushToOutputBelt()
    {
        if (_grid == null || _pendingOutputCount <= 0 || beltItemPrefab == null) return;

        foreach (var cell in _outputCells)
        {
            Building buildingAtCell = _grid.GetBuildingAt(cell);
            if (buildingAtCell == null) continue;

            Belt belt = buildingAtCell.GetComponentInChildren<Belt>(true);
            if (belt == null) continue;
            if (!belt.MatchOutputGrid(_grid, cell)) continue;
            if (belt.beltItem != null || belt.isSpaceTaken) continue;

            BeltItem newItem = CreateOutputBeltItem();
            if (newItem == null) return;

            newItem.id = _resourceId;
            if (belt.TryInputItem(newItem))
            {
                _pendingOutputCount--;
                return;
            }
            Destroy(newItem.gameObject);
        }
    }

    private BeltItem CreateOutputBeltItem()
    {
        if (beltItemPrefab == null) return null;

        Building root = GetRootBuilding();
        BuildingPort outPort = GetFirstOutputPort(root);
        Vector3 spawnPos = outPort != null
            ? outPort.transform.position + Vector3.up * 0.3f
            : transform.position + Vector3.up * 0.3f;
        return Instantiate(beltItemPrefab, spawnPos, Quaternion.identity);
    }

    public List<Vector2Int> GetOutputPortGridCoordinates(BuildingGrid grid)
    {
        var list = new List<Vector2Int>();
        if (grid == null)
        {
            if (enableDebugLog) Debug.LogWarning("[MinerBuilding] GetOutputPortGridCoordinates: grid is null");
            return list;
        }
        Building root = GetRootBuilding();
        if (root == null)
        {
            if (enableDebugLog) Debug.LogWarning("[MinerBuilding] GetOutputPortGridCoordinates: Can't find root Building");
            return list;
        }
        if (root.Ports == null || root.Ports.Length == 0)
        {
            if (enableDebugLog) Debug.LogWarning("[MinerBuilding] GetOutputPortGridCoordinates: Ports is empty");
            return list;
        }
        foreach (var port in root.Ports)
        {
            if (port == null || port.PortType != PortType.Output) continue;
            Vector2Int cell = GetGridCellAtPortWorld(grid, port.transform.position);
            list.Add(cell);
            if (enableDebugLog)
                Debug.Log($"[MinerBuilding] output port={port.name} -> grid ({cell.x}, {cell.y})");
        }
        if (enableDebugLog && list.Count > 0)
        {
            var coords = string.Join(", ", System.Array.ConvertAll(list.ToArray(), c => $"({c.x}, {c.y})"));
            Debug.Log($"[MinerBuilding] output ports grid coordinates: {coords}");
        }
        return list;
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

    public bool TryOutputItem(out BeltItem item)
    {
        item = null;
        return false;
    }
}