using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using UnityEngine.InputSystem.Controls;
using UnityEditor.TerrainTools;

public class BuildingSystem : MonoBehaviour
{
    public const float CellSize = 1f;

    [SerializeField] private BuildingPreview previewPrefab;
    [SerializeField] private Building buildingPrefab;
    [SerializeField] private BuildingGrid grid;

    private Dictionary<KeyCode, BuildingData> keyToBuilding = new();
    private Dictionary<KeyCode, BuildingData> keyToLogistics = new();
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

    private void Start()
    {
        int logisticsIndex = 0;
        for(int i = 0; i < BuildingDatabase.All.Count && i < 9; i++)
        {
            var data = BuildingDatabase.All[i];
            if(data.Category == BuildingCategory.Logistics){
                if (logisticsIndex < 9)
                {
                    keyToLogistics[KeyCode.Alpha1 + logisticsIndex] = data;
                    logisticsIndex++;
                }
            }
            else
            {
                keyToBuilding[KeyCode.Alpha1 + i] = BuildingDatabase.All[i];
            }
        }
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

        if(Input.GetKeyDown(KeyCode.E))
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
            if(!Input.anyKeyDown)
            {
                return;
            }

            if(!isLogisticsMode)
            {
                
                foreach(var kvp in keyToBuilding)
                {
                    if(Input.GetKeyDown(kvp.Key))
                    {
                        preview = CreatePreview(kvp.Value, mousePos);
                        break;
                    }
                }
            }
            else
            {
                foreach(var kvp in keyToLogistics)
                {
                    if(Input.GetKeyDown(kvp.Key))
                    {   
                        preview = CreatePreview(kvp.Value, mousePos);
                        break;
                    }
                }
            }
            

            // if(Input.GetKeyDown(KeyCode.Alpha1))
            // {
            //     preview = CreatePreview(buildingData1, mousePos);
            // }
            // else if(Input.GetKeyDown(KeyCode.Alpha2))
            // {
            //     preview = CreatePreview(buildingData2, mousePos);
            // }
            // else if(Input.GetKeyDown(KeyCode.Alpha3))
            // {
            //     preview = CreatePreview(buildingData3, mousePos);
            // }
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
            bool CanBuild = grid.CanBuild(buildPosition);
            if(CanBuild)
            {   
                Vector3 unitWorld = buildPosition[0];
                Vector3 snapped = SnapWorldToCellCenter(unitWorld);
                Vector3 offset = unitWorld - preview.transform.position;
                preview.transform.position = snapped - offset;

                preview.ChangeState(BuildingPreview.BuildingPreviewState.Positive);
                if(Input.GetMouseButtonDown(0))
                {
                    PlaceBuilding(buildPosition);
                }
            }   
            else
            {
                preview.ChangeState(BuildingPreview.BuildingPreviewState.Negative);
            }
            if(Input.GetKeyDown(KeyCode.R))
            {
                preview.Rotate(90);
            }
            if(Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Escape)){
                Destroy(preview.gameObject);
            }
        }
    }

    private void HandleLogisticsPreview(Vector3 mouseWorldPosition)
    {
        Vector2Int currentGrid = grid.WorldToGridPosition(mouseWorldPosition);

        if (!hasLogisticsStart)
        {
            Vector3 pos = mouseWorldPosition;
            Vector3 snapped = SnapWorldToCellCenter(new Vector3(pos.x, grid.transform.position.y, pos.z));
            preview.transform.position = snapped;
            preview.BuildingModel.transform.rotation = GetRotationFromDir(beltDir);

            bool canBuild = grid.CanBuild(new List<Vector3> { snapped });
            preview.ChangeState(canBuild
                ? BuildingPreview.BuildingPreviewState.Positive
                : BuildingPreview.BuildingPreviewState.Negative);

            if (Input.GetMouseButtonDown(0))
            {
                logisticsStartGrid = currentGrid;
                hasLogisticsStart = true;
            }

            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Escape))
            {
                Destroy(preview.gameObject);
                preview = null;
                hasLogisticsStart = false;
            }
            return;
        }

        logisticsPreviewPath = FindPath(currentGrid);
        UpdateLogisticsPathPreview(logisticsPreviewPath);

        if(Input.GetMouseButtonDown(0) && logisticsPreviewPath.Count > 0){
            bool canBuildPath = logisticsPreviewPath.TrueForAll(cell =>
                grid.CanBuild(new List<Vector3> { GridToWorldCenter(cell) }));

            if(canBuildPath)
            {
                PlaceLogisticsPath(logisticsPreviewPath);
            }
        }
        
        if(Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Escape))
        {
            foreach(var p in logisticsPathPreviews) Destroy(p.gameObject);
            logisticsPathPreviews.Clear();
            Destroy(preview.gameObject);
            preview = null;
            hasLogisticsStart = false;
            logisticsPreviewPath.Clear();
        }

    }

    private List<Vector2Int> FindPath(Vector2Int endGrid)
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

                if(!grid.CanBuild(new List<Vector3> {GridToWorldCenter(next)})) continue;

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

        Vector3 startPos = GridToWorldCenter(path[0]);
        Vector3 snappedStartPos = SnapWorldToCellCenter(new Vector3(startPos.x, grid.transform.position.y, startPos.z));
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
            Vector3 pos = GridToWorldCenter(path[i]);
            Vector3 snapped = SnapWorldToCellCenter(new Vector3(pos.x, grid.transform.position.y, pos.z));

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
            
            Vector3 snapped = SnapWorldToCellCenter(new Vector3(pos.x, grid.transform.position.y, pos.z));

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
            grid.SetBuilding(building, new List<Vector3> {snapped});

            if (i == 0)
            {   
                Vector2Int startCell = grid.WorldToGridPosition(snapped);
                Debug.Log($"[BuildingSystem] 放置传送带起点(格子): ({startCell.x}, {startCell.y})");
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
        grid.SetBuilding(building, buildingPosition);
        Destroy(preview.gameObject);
        preview = null;
        building.OnPlaced();

        var productionBuilding = building.GetComponentInChildren<ProductionBuilding>(true);
        if (productionBuilding != null)
        {
            productionBuilding.OnPlaced();
        }
    }

    private void UpdateBuildingPortsHints(Vector3 mousePos)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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

    private Vector3 SnapWorldToCellCenter(Vector3 worldPos)
    {
        Vector3 o = grid.transform.position;
        float gx = Mathf.Floor((worldPos.x - o.x) / CellSize);
        float gz = Mathf.Floor((worldPos.z - o.z) / CellSize);
        return new Vector3(
            o.x + (gx + 0.5f) * CellSize,
            o.y,
            o.z + (gz + 0.5f) * CellSize
        );
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
        float centerX = (xs.Min() + xs.Max()) / 2f + CellSize / 2f;
        float centerZ = (zs.Min() + zs.Max()) / 2f + CellSize / 2f;

        return new(centerX, 0, centerZ);
    }

    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new(Vector3.up, Vector3.zero);
        if(groundPlane.Raycast(ray, out float distance))
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

    private Vector3 GridToWorldCenter(Vector2Int cell){
        Vector3 o = grid.transform.position;
        return new Vector3(
            o.x + (cell.x + 0.5f) * CellSize,
            o.y,
            o.z + (cell.y + 0.5f) * CellSize
        );
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
