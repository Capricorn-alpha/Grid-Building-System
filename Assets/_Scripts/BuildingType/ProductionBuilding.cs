using UnityEngine;
using System.Collections.Generic;

public class ProductionBuilding : Building, ItemSource
{
    [SerializeField] private BeltItem beltItemPrefab;
    [SerializeField] private float productionInterval = 5f;
    [Tooltip("推到输出带上的产物 BeltItem.id")]
    [SerializeField] private string outputItemId = "product";
    [SerializeField] private bool enableDebugLog = true;

    private float productionTimer;
    private int _pendingOutputCount;
    private bool _isPlaced;
    private BuildingGrid _grid;
    private List<Vector2Int> _outputCells;
    private List<Vector2Int> _inputCells;
    private readonly Queue<string> _inputInventory = new();

    private void Update()
    {
        if(!_isPlaced) return;

        TryPullFromInputBelts();

        productionTimer -= Time.deltaTime;
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
        _inputCells = GetInputPortGridCoordinates(_grid);
        if (_inputCells == null) _inputCells = new List<Vector2Int>();
    }

    private List<Vector2Int> GetInputPortGridCoordinates(BuildingGrid grid)
    {
        var list = new List<Vector2Int>();
        if (grid == null)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] GetInputPortGridCoordinates: grid is null");
            return list;
        }
        Building root = GetRootBuilding();
        if (root == null)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] GetInputPortGridCoordinates: Can't find root Building");
            return list;
        }
        if (root.Ports == null || root.Ports.Length == 0)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] GetInputPortGridCoordinates: Ports is empty");
            return list;
        }
        foreach (var port in root.Ports)
        {
            if (port == null || port.PortType != PortType.Input) continue;
            Vector2Int cell = GetGridCellAtPortWorld(grid, port.transform.position);
            list.Add(cell);
            if (enableDebugLog)
                Debug.Log($"[ProductionBuilding] input port={port.name} -> grid ({cell.x}, {cell.y})");
        }
        return list;
    }

    private void TryPullFromInputBelts()
    {
        if (_grid == null || _inputCells == null || _inputCells.Count == 0) return;

        foreach (var cell in _inputCells)
        {
            Building buildingAtCell = _grid.GetBuildingAt(cell);
            if (buildingAtCell == null) continue;

            Belt belt = buildingAtCell.GetComponentInChildren<Belt>(true);
            if (belt == null) continue;
            if (!belt.MatchOutputGrid(_grid, cell)) continue;
            if (belt.beltItem == null) continue;

            if (!belt.TryExtractItem(out BeltItem taken) || taken == null) continue;

            string id = string.IsNullOrEmpty(taken.id) ? "item" : taken.id;
            _inputInventory.Enqueue(id);
            Destroy(taken.gameObject);

            if (enableDebugLog)
                Debug.Log($"[ProductionBuilding] accepted belt item id={id}, inventory count={_inputInventory.Count}");
            return;
        }
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
            if (port == null || port.PortType != PortType.Output) continue;
            Vector2Int cell = GetGridCellAtPortWorld(grid, port.transform.position);
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

    private void Produce()
    {
        if (_inputInventory.Count == 0)
        {
            if (enableDebugLog)
                Debug.Log("[ProductionBuilding] Produce skipped: input inventory empty");
            return;
        }
        if (beltItemPrefab == null)
        {
            if (enableDebugLog) Debug.LogWarning("[ProductionBuilding] Produce: beltItemPrefab is not set");
            return;
        }

        _inputInventory.Dequeue();
        _pendingOutputCount++;
        if (enableDebugLog)
            Debug.Log($"[ProductionBuilding] consumed 1 input, pending output={_pendingOutputCount}, remaining inventory={_inputInventory.Count}");
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

            newItem.id = outputItemId;
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
