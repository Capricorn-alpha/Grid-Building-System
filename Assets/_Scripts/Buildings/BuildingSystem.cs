using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BuildingSystem : MonoBehaviour
{
    public const float CellSize = 1f;

    [SerializeField] private BuildingPreview previewPrefab;
    [SerializeField] private Building buildingPrefab;
    [SerializeField] private BuildingGrid grid;
    [Tooltip("微观建造模式使用的网格；留空则微观模式下无法放置（仍会按 Scope 过滤按键表）。")]
    [SerializeField] private MiniBuildingGrid miniGrid;
    [Tooltip("留空则自动查找场景中的 BuildingController。")]
    [SerializeField] private BuildingController buildingController;

    private readonly Dictionary<KeyCode, BuildingData> keyToBuildingMacro = new();
    private readonly Dictionary<KeyCode, BuildingData> keyToBuildingMicro = new();
    private readonly Dictionary<KeyCode, BuildingData> keyToLogisticsMacro = new();
    private readonly Dictionary<KeyCode, BuildingData> keyToLogisticsMicro = new();

    private Dictionary<KeyCode, BuildingData> ActiveKeyToBuilding =>
        CurrentScope == BuildScope.Macro ? keyToBuildingMacro : keyToBuildingMicro;

    private Dictionary<KeyCode, BuildingData> ActiveKeyToLogistics =>
        CurrentScope == BuildScope.Macro ? keyToLogisticsMacro : keyToLogisticsMicro;

    private BuildScope CurrentScope =>
        buildingController != null && buildingController.CurrentMode == BuildingController.BuildMode.Micro
            ? BuildScope.Micro
            : BuildScope.Macro;

    /// <summary>正在放置预览（含物流路径）时，其它系统（如建筑信息面板）应忽略左键选中逻辑。</summary>
    public bool HasActivePlacementPreview => preview != null;

    private BuildingPreview preview;

    private bool isLogisticsMode = false;
    private bool hasLogisticsStart;
    private Vector2Int logisticsStartGrid;
    private List<Vector2Int> logisticsPreviewPath = new();
    private List<BuildingPreview> logisticsPathPreviews = new();
    private Building currentHoveredBuilding;

    private Vector3 lastMouseWorldPos;
    private bool hasLastMousePos;
    private Vector3 beltDir = Vector3.forward;

    private void Awake() => ResolveBuildingController();

    private void OnEnable()
    {
        ResolveBuildingController();
        if (buildingController != null)
            buildingController.BuildModeChanged += OnBuildModeChanged;
    }

    private void ResolveBuildingController()
    {
        if (buildingController == null)
            buildingController = FindFirstObjectByType<BuildingController>();
    }

    private void OnDisable()
    {
        if (buildingController != null)
            buildingController.BuildModeChanged -= OnBuildModeChanged;
    }

    private void OnBuildModeChanged(BuildingController.BuildMode mode)
    {
        if (preview != null)
        {
            Destroy(preview.gameObject);
            preview = null;
        }

        foreach (var p in logisticsPathPreviews)
            Destroy(p.gameObject);
        logisticsPathPreviews.Clear();

        hasLogisticsStart = false;
        logisticsPreviewPath.Clear();
        isLogisticsMode = false;
        currentHoveredBuilding = null;
    }

    private void Start()
    {
        FillKeyMap(keyToBuildingMacro, BuildingDatabase.GetNormalByScope(BuildScope.Macro));
        FillKeyMap(keyToBuildingMicro, BuildingDatabase.GetNormalByScope(BuildScope.Micro));
        FillKeyMap(keyToLogisticsMacro, BuildingDatabase.GetLogisticsByScope(BuildScope.Macro));
        FillKeyMap(keyToLogisticsMicro, BuildingDatabase.GetLogisticsByScope(BuildScope.Micro));
    }

    private static void FillKeyMap(Dictionary<KeyCode, BuildingData> dict, IReadOnlyList<BuildingData> list)
    {
        dict.Clear();
        for (int i = 0; i < list.Count && i < 9; i++)
            dict[KeyCode.Alpha1 + i] = list[i];
    }

    private bool HasActiveGridForCurrentScope()
    {
        if (CurrentScope == BuildScope.Macro)
            return grid != null;
        return miniGrid != null;
    }

    private Vector3 ActiveGridOrigin()
    {
        if (CurrentScope == BuildScope.Macro)
            return grid != null ? grid.transform.position : Vector3.zero;
        if (miniGrid != null)
            return miniGrid.transform.position;
        return grid != null ? grid.transform.position : Vector3.zero;
    }

    private float ActiveCellSize()
    {
        if (CurrentScope == BuildScope.Macro)
            return CellSize;
        return miniGrid != null ? miniGrid.CellSize : CellSize;
    }

    private bool ActiveCanBuild(List<Vector3> positions, BuildingData data)
    {
        if (CurrentScope == BuildScope.Macro)
            return grid != null && grid.CanBuild(positions, data);
        return miniGrid != null && miniGrid.CanBuild(positions, data);
    }

    private void ActiveSetBuilding(Building building, List<Vector3> positions)
    {
        if (CurrentScope == BuildScope.Macro)
        {
            if (grid != null)
                grid.SetBuilding(building, positions);
        }
        else if (miniGrid != null)
        {
            miniGrid.SetBuilding(building, positions);
        }
    }

    private Vector2Int ActiveWorldToGrid(Vector3 worldPosition)
    {
        if (CurrentScope == BuildScope.Macro)
            return grid != null ? grid.WorldToGridPosition(worldPosition) : default;
        if (miniGrid != null)
            return miniGrid.WorldToGridPosition(worldPosition);
        return grid != null ? grid.WorldToGridPosition(worldPosition) : default;
    }

    private Vector3 ActiveGridToWorldCenter(Vector2Int cell)
    {
        if (CurrentScope == BuildScope.Macro)
        {
            if (grid == null) return Vector3.zero;
            Vector3 o = grid.transform.position;
            return new Vector3(
                o.x + (cell.x + 0.5f) * CellSize,
                o.y,
                o.z + (cell.y + 0.5f) * CellSize
            );
        }

        if (miniGrid != null)
            return miniGrid.GridToWorldCenter(cell);
        if (grid == null) return Vector3.zero;
        Vector3 og = grid.transform.position;
        return new Vector3(
            og.x + (cell.x + 0.5f) * CellSize,
            og.y,
            og.z + (cell.y + 0.5f) * CellSize
        );
    }

    private Vector3 ActiveSnapWorldToCellCenter(Vector3 worldPos)
    {
        Vector3 o = ActiveGridOrigin();
        float cs = ActiveCellSize();
        float gx = Mathf.Floor((worldPos.x - o.x) / cs);
        float gz = Mathf.Floor((worldPos.z - o.z) / cs);
        return new Vector3(
            o.x + (gx + 0.5f) * cs,
            o.y,
            o.z + (gz + 0.5f) * cs
        );
    }

    private void Update()
    {   
        Vector3 mousePos = GetMouseWorldPosition();

        // 计算鼠标移动方向
        Vector3 moveDir = Vector3.zero;
        if (hasLastMousePos)
        {
            moveDir = mousePos - lastMouseWorldPos;
        }
        lastMouseWorldPos = mousePos;
        hasLastMousePos = true;

        // 离散成上下左右方向
        beltDir = Vector3.forward;
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.z))
            {
                beltDir = moveDir.x > 0 ? Vector3.right : Vector3.left;
            }
            else
            {
                beltDir = moveDir.z > 0 ? Vector3.forward : Vector3.back;
            }
        }

        if (GameInput.KeyDown(KeyCode.E))
        {
            isLogisticsMode = !isLogisticsMode;
            Debug.Log("Logistics mode: " + isLogisticsMode);
        }

        if (isLogisticsMode && preview == null)
        {
            UpdateBuildingPortsHints(mousePos);
        }

        if(preview != null)
        {
            HandlePreview(mousePos);
        }
        else
        {
            if (!HasActiveGridForCurrentScope())
                return;

            if (!isLogisticsMode)
            {
                foreach (var kvp in ActiveKeyToBuilding)
                {
                    if (GameInput.KeyDown(kvp.Key))
                    {
                        preview = CreatePreview(kvp.Value, mousePos);
                        break;
                    }
                }
            }
            else
            {
                foreach (var kvp in ActiveKeyToLogistics)
                {
                    if (GameInput.KeyDown(kvp.Key))
                    {
                        preview = CreatePreview(kvp.Value, mousePos);
                        break;
                    }
                }
            }
        }
    }

    private void HandlePreview(Vector3 mouseWorldPosition)
    {   
        if(isLogisticsMode){
            HandleLogisticsPreview(mouseWorldPosition);
        }
        else
        {
            preview.transform.position = mouseWorldPosition;
            List<Vector3> buildPosition = preview.BuildingModel.GetAllBuildingPositions();
            bool CanBuild = ActiveCanBuild(buildPosition, preview.Data);
            if(CanBuild)
            {   
                Vector3 unitWorld = buildPosition[0];
                Vector3 snapped = ActiveSnapWorldToCellCenter(unitWorld);
                Vector3 offset = unitWorld - preview.transform.position;
                preview.transform.position = snapped - offset;

                preview.ChangeState(BuildingPreview.BuildingPreviewState.Positive);
                if (GameInput.LeftButtonDownThisFrame())
                {
                    PlaceBuilding(buildPosition);
                }
            }   
            else
            {
                preview.ChangeState(BuildingPreview.BuildingPreviewState.Negative);
            }
            if (GameInput.KeyDown(KeyCode.R))
            {
                preview.Rotate(90);
            }
            if (GameInput.KeyDown(KeyCode.Q) || GameInput.KeyDown(KeyCode.Escape))
            {
                Destroy(preview.gameObject);
            }
        }
    }

    private void HandleLogisticsPreview(Vector3 mouseWorldPosition)
    {
        Vector2Int currentGrid = ActiveWorldToGrid(mouseWorldPosition);

        if (!hasLogisticsStart)
        {
            Vector3 pos = mouseWorldPosition;
            Vector3 snapped = ActiveSnapWorldToCellCenter(new Vector3(pos.x, ActiveGridOrigin().y, pos.z));
            preview.transform.position = snapped;
            preview.BuildingModel.transform.rotation = GetRotationFromDir(beltDir);

            bool canBuild = ActiveCanBuild(new List<Vector3> { snapped }, preview.Data);
            preview.ChangeState(canBuild
                ? BuildingPreview.BuildingPreviewState.Positive
                : BuildingPreview.BuildingPreviewState.Negative);

            if (GameInput.LeftButtonDownThisFrame())
            {
                logisticsStartGrid = currentGrid;
                hasLogisticsStart = true;
            }

            if (GameInput.KeyDown(KeyCode.Q) || GameInput.KeyDown(KeyCode.Escape))
            {
                Destroy(preview.gameObject);
                preview = null;
                hasLogisticsStart = false;
            }
            return;
        }

        logisticsPreviewPath = FindPath(currentGrid, preview.Data);
        UpdateLogisticsPathPreview(logisticsPreviewPath);

        if (GameInput.LeftButtonDownThisFrame() && logisticsPreviewPath.Count > 0)
        {
            bool canBuildPath = logisticsPreviewPath.TrueForAll(cell =>
                ActiveCanBuild(new List<Vector3> { ActiveGridToWorldCenter(cell) }, preview.Data));

            if (canBuildPath)
            {
                PlaceLogisticsPath(logisticsPreviewPath);
            }
        }
        
        if (GameInput.KeyDown(KeyCode.Q) || GameInput.KeyDown(KeyCode.Escape))
        {
            foreach(var p in logisticsPathPreviews) Destroy(p.gameObject);
            logisticsPathPreviews.Clear();
            Destroy(preview.gameObject);
            preview = null;
            hasLogisticsStart = false;
            logisticsPreviewPath.Clear();
        }

    }

    private List<Vector2Int> FindPath(Vector2Int endGrid, BuildingData logisticsBuilding)
    {
        Vector2Int startGrid = logisticsStartGrid;
        if(startGrid == endGrid) return new List<Vector2Int> {startGrid};

        var queue = new Queue<Vector2Int>();
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        queue.Enqueue(startGrid);
        parent[startGrid] = startGrid;

        Vector2Int[] neightbors = {new(1,0), new(-1, 0), new(0,1), new(0, -1)};

        while(queue.Count > 0)
        {   
            Vector2Int cur = queue.Dequeue();
            foreach(var delta in neightbors)
            {
                Vector2Int next = cur + delta;
                if(parent.ContainsKey(next)) continue;

                if(!ActiveCanBuild(new List<Vector3> {ActiveGridToWorldCenter(next)}, logisticsBuilding)) continue;

                parent[next] = cur;
                if(next == endGrid)
                {
                    var path = new List<Vector2Int>();
                    for (Vector2Int p = endGrid; p != parent[p]; p = parent[p])
                    {
                        path.Add(p);
                    }
                    path.Add(startGrid);
                    path.Reverse();
                    return path;
                }
                queue.Enqueue(next);
            }
        }
        return new List<Vector2Int> {startGrid};
    }

    private void UpdateLogisticsPathPreview(List<Vector2Int> path)
    {
        foreach (var p in logisticsPathPreviews) Destroy(p.gameObject);
        logisticsPathPreviews.Clear();

        Vector3 startPos = ActiveGridToWorldCenter(path[0]);
        Vector3 snappedStartPos = ActiveSnapWorldToCellCenter(new Vector3(startPos.x, ActiveGridOrigin().y, startPos.z));
        preview.transform.position = snappedStartPos;

        preview.ChangeState(BuildingPreview.BuildingPreviewState.Positive);

        if (path.Count > 1)
        {
            Vector2Int delta0 = path[1] - path[0];
            Vector3 dir0 = Vector3.forward;
            if (delta0.x > 0) dir0 = Vector3.right;
            else if (delta0.x < 0) dir0 = Vector3.left;
            else if (delta0.y > 0) dir0 = Vector3.forward;
            else if (delta0.y < 0) dir0 = Vector3.back;
            preview.BuildingModel.transform.rotation = GetRotationFromDir(dir0);
        }

        for(int i = 1; i < path.Count; i++)
        {
            Vector3 pos = ActiveGridToWorldCenter(path[i]);
            Vector3 snapped = ActiveSnapWorldToCellCenter(new Vector3(pos.x, ActiveGridOrigin().y, pos.z));

            var segmentPreview = CreatePreview(preview.Data, snapped);

            Vector3 dir = Vector3.forward;
            if (i < path.Count - 1)
            {
                Vector2Int delta = path[i + 1] - path[i];
                if (delta.x > 0) dir = Vector3.right;
                else if (delta.x < 0) dir = Vector3.left;
                else if (delta.y > 0) dir = Vector3.forward;
                else if (delta.y < 0) dir = Vector3.back;
            }
            else
            {
                Vector2Int delta = path[i] - path[i - 1];
                if (delta.x > 0) dir = Vector3.right;
                else if (delta.x < 0) dir = Vector3.left;
                else if (delta.y > 0) dir = Vector3.forward;
                else if (delta.y < 0) dir = Vector3.back;
            }
            segmentPreview.BuildingModel.transform.rotation = GetRotationFromDir(dir);

            segmentPreview.ChangeState(BuildingPreview.BuildingPreviewState.Positive);
            logisticsPathPreviews.Add(segmentPreview);
        }
    }

    private void PlaceLogisticsPath(List<Vector2Int> path)
    {
        for(int i = 0; i < path.Count; i++)
        {
            Vector3 pos = (i == 0)
                ? preview.transform.position
                : logisticsPathPreviews[i - 1].transform.position;
            
            Vector3 snapped = ActiveSnapWorldToCellCenter(new Vector3(pos.x, ActiveGridOrigin().y, pos.z));

            beltDir = Vector3.forward;
            if (i < path.Count - 1)
            {
                Vector2Int cur  = path[i];
                Vector2Int next = path[i + 1];
                Vector2Int delta = next - cur;
                if (delta.x > 0) beltDir = Vector3.right;
                else if (delta.x < 0) beltDir = Vector3.left;
                else if (delta.y > 0) beltDir = Vector3.forward;
                else if (delta.y < 0) beltDir = Vector3.back;
            }
            else if (i > 0)
            {
                Vector2Int delta = path[i] - path[i - 1];
                if (delta.x > 0) beltDir = Vector3.right;
                else if (delta.x < 0) beltDir = Vector3.left;
                else if (delta.y > 0) beltDir = Vector3.forward;
                else if (delta.y < 0) beltDir = Vector3.back;
            }
            Quaternion rot = GetRotationFromDir(beltDir);
            Building building = Instantiate(buildingPrefab, snapped, rot);
            
            // pos = new Vector3(pos.x, grid.transform.position.y, pos.z);
            float yAngle = rot.eulerAngles.y;
            building.Setup(preview.Data, yAngle);
            ActiveSetBuilding(building, new List<Vector3> {snapped});

            if (i == 0)
            {   
                Vector2Int startCell = ActiveWorldToGrid(snapped);
                Debug.Log($"[BuildingSystem] place belt start grid: ({startCell.x}, {startCell.y})");
                var startBelt = building.GetComponentInChildren<Belt>(true);
                if (startBelt != null)
                    startBelt.LogBeltStartGrid();
            }
        }


        foreach(var p in logisticsPathPreviews) Destroy(p.gameObject);
        logisticsPathPreviews.Clear();
        Destroy(preview.gameObject);
        preview = null;
        hasLogisticsStart = false;
        logisticsPreviewPath.Clear();
    }

    private void PlaceBuilding(List<Vector3> buildingPosition)
    {
        Building building = Instantiate(buildingPrefab, preview.transform.position, Quaternion.identity);
        building.Setup(preview.Data, preview.BuildingModel.Rotation);
        ActiveSetBuilding(building, buildingPosition);
        Destroy(preview.gameObject);
        preview = null;
        building.OnPlaced();

        var productionBuilding = building.GetComponentInChildren<ProductionBuilding>(true);
        if (productionBuilding != null)
        {
            productionBuilding.OnPlaced();
        }

        var minerBuilding = building.GetComponentInChildren<MinerBuilding>(true);
        if (minerBuilding != null)
        {
            minerBuilding.OnPlaced();
        }
    }

    private void UpdateBuildingPortsHints(Vector3 mousePos)
    {
        if (!GameInput.TryGetPointerScreen(out Vector2 screen) || Camera.main == null)
            return;
        Ray ray = Camera.main.ScreenPointToRay(screen);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            Building building = hit.collider.GetComponentInParent<Building>();
            if (building == null) continue;
            if (currentHoveredBuilding != building)
            {
                HideBuildingPorts(currentHoveredBuilding);
                currentHoveredBuilding = building;
                ShowBuildingPorts(building);
            }
            return;
        }
        HideBuildingPorts(currentHoveredBuilding);
        currentHoveredBuilding = null;
    }

    private void HideBuildingPorts(Building building)
    {
        if (building == null || building.Ports == null) return;
        building.SetPortsVisualsActive(false);
    }

    private void ShowBuildingPorts(Building building)
    {
        if (building == null || building.Ports == null) return;
        building.SetPortsVisualsActive(true);
    }

    private Vector3 GetSnappedCenterPosition(List<Vector3> allBuildingPositions)
    {
        // Vector3 gridOrigin = grid.transform.position;

        // List<int> gxs = new();
        // List<int> gzs = new();
        // foreach (var p in allBuildingPositions)
        // {
        //     int gx = Mathf.FloorToInt((p.x - gridOrigin.x) / CellSize);
        //     int gz = Mathf.FloorToInt((p.z - gridOrigin.z) / CellSize);
        //     gxs.Add(gx);
        //     gzs.Add(gz);
        // }
        
        // int minGx = gxs.Min();
        // int maxGx = gxs.Max();
        // int minGz = gzs.Min();
        // int maxGz = gzs.Max();

        // float centerX = gridOrigin.x + (minGx + maxGx) / 2f * CellSize + CellSize / 2f;
        // float centerZ = gridOrigin.z + (minGz + maxGz) / 2f * CellSize + CellSize / 2f;

        List<int> xs = allBuildingPositions.Select(p => Mathf.FloorToInt(p.x)).ToList();
        List<int> zs = allBuildingPositions.Select(p => Mathf.FloorToInt(p.z)).ToList();
        float cs = ActiveCellSize();
        float centerX = (xs.Min() + xs.Max()) / 2f + cs / 2f;
        float centerZ = (zs.Min() + zs.Max()) / 2f + cs / 2f;

        return new(centerX, 0, centerZ);
    }

    private Vector3 GetMouseWorldPosition()
    {
        if (!GameInput.TryGetPointerScreen(out Vector2 screen) || Camera.main == null)
            return Vector3.zero;
        Ray ray = Camera.main.ScreenPointToRay(screen);
        Plane groundPlane = new(Vector3.up, Vector3.zero);
        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector3.zero;
    }

    private BuildingPreview CreatePreview(BuildingData data, Vector3 position)
    {
        BuildingPreview buildingPreview = Instantiate(previewPrefab, position, Quaternion.identity);
        buildingPreview.Setup(data);
        return buildingPreview;
    }

    Quaternion GetRotationFromDir(Vector3 dir)
    {
        if (dir == Vector3.right)   return Quaternion.Euler(0, 90, 0);
        if (dir == Vector3.left)    return Quaternion.Euler(0, 270, 0);
        if (dir == Vector3.forward) return Quaternion.Euler(0, 0, 0);
        if (dir == Vector3.back)    return Quaternion.Euler(0, 180, 0);
        return Quaternion.identity;
    }
}
