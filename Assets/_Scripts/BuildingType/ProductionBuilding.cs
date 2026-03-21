using UnityEngine;
using System.Collections.Generic;

public class ProductionBuilding : Building, ItemSource
{
    [SerializeField] private BeltItem beltItemPrefab;
    [SerializeField] private float productionInterval = 5f;
    [SerializeField] private bool enableDebugLog = true;

    private float productionTimer;
    private int _pendingOutputCount;
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
            Debug.Log($"[ProductionBuilding] OnPlaced is called, name={name}");
        
        _isPlaced = true;
        productionTimer = productionInterval;
        _grid = FindFirstObjectByType<BuildingGrid>();
        if (_grid == null)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] Can't find BuildingGrid");
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
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] GetOutputPortGridCoordinates: grid is null");
            return list;
        }
        Building root = GetRootBuilding();
        if (root == null)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] GetOutputPortGridCoordinates: Can't find root Building");
            return list;
        }
        if (root.Ports == null || root.Ports.Length == 0)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] GetOutputPortGridCoordinates: Ports is empty");
            return list;
        }
        foreach (var port in root.Ports)
        {   
            //Debug.Log(port.transform.forward * BuildingSystem.CellSize);
            if (port == null || port.PortType != PortType.Output) continue;
            Vector3 worldInFront = port.transform.position + 0.5f * BuildingSystem.CellSize * port.transform.forward;
            Vector2Int cell = grid.WorldToGridPosition(worldInFront);
            list.Add(cell);
            if (enableDebugLog)
                Debug.Log($"[ProductionBuilding] outpu port={port.name} -> grid ({cell.x}, {cell.y})");
        }
        if (enableDebugLog && list.Count > 0)
        {
            var coords = string.Join(", ", System.Array.ConvertAll(list.ToArray(), c => $"({c.x}, {c.y})"));
            Debug.Log($"[ProductionBuilding] output ports grid coordinates: {coords}");
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
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] Produce: beltItemPrefab is not set");
            return;
        }
        _pendingOutputCount++;
        if (enableDebugLog) Debug.Log($"[ProductionBuilding] Produce: pending output +1, total={_pendingOutputCount}");
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
        if (_grid == null || _pendingOutputCount <= 0 || beltItemPrefab == null) return;

        foreach (var cell in _outputCells)
        {
            if (enableDebugLog)
                Debug.Log($"[ProductionBuilding] try to push to output port grid ({cell.x}, {cell.y})");
            Building buildingAtCell = _grid.GetBuildingAt(cell);
            if (buildingAtCell == null)
            {
                if (enableDebugLog)
                    Debug.Log($"[ProductionBuilding] output port grid ({cell.x}, {cell.y}) is empty");
                continue;
            }

            Belt belt = buildingAtCell.GetComponentInChildren<Belt>(true);
            if (belt == null) continue;

            if (!belt.MatchOutputGrid(_grid, cell)) continue;
            if (belt.beltItem != null || belt.isSpaceTaken) continue;

            BeltItem newItem = CreateOutputBeltItem();
            if (newItem == null) return;

            if (belt.TryInputItem(newItem))
            {
                _pendingOutputCount--;
                if (enableDebugLog)
                    Debug.Log($"[ProductionBuilding] pushed to belt, remaining pending output={_pendingOutputCount}");
                return;
            }

            Destroy(newItem.gameObject);
        }
    }

    public bool TryOutputItem(out BeltItem item)
    {
        item = null;
        return false;
    }
}
