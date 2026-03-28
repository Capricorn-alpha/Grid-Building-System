using UnityEngine;

public class Building : MonoBehaviour
{
    private const string LogTag = "[Building.Select]";

    public string Description => data.Description;
    public int Cost => data.Cost;
    public BuildingData Data => data;
    protected BuildingModel model { get; private set; }
    protected BuildingData data { get; private set; }
    public BuildingPort[] Ports { get; private set; }

    [Tooltip("勾选后，Setup 时会在 Console 输出碰撞体/包围盒计算结果。")]
    [SerializeField] private bool debugSelectionCollider;

    public void Setup(BuildingData data, float rotation)
    {
        this.data = data;
        model = Instantiate(data.Model, transform.position, Quaternion.identity, transform);
        model.Rotate(rotation);

        Ports = GetComponentsInChildren<BuildingPort>(true);
        EnsureRaycastColliderForSelection();
    }

    /// <summary>
    /// 在根节点添加非 Trigger 的 BoxCollider 供射线选中。
    /// 仅当<b>根节点</b>已有非 Trigger 碰撞体时跳过；子物体上的碰撞体不再阻止添加（避免仅 Trigger/过小导致打不中）。
    /// </summary>
    private void EnsureRaycastColliderForSelection()
    {
        var existingOnRoot = GetComponent<Collider>();
        if (existingOnRoot != null && existingOnRoot.enabled && !existingOnRoot.isTrigger)
        {
            if (debugSelectionCollider)
                Debug.Log($"{LogTag} {name}: 根节点已有非 Trigger 碰撞体，跳过。", this);
            return;
        }

        if (!TryComputeWorldBoundsEncapsulatingVisuals(out Bounds worldBounds))
        {
            worldBounds = new Bounds(transform.position, new Vector3(1f, 1f, 1f));
            if (debugSelectionCollider)
                Debug.LogWarning($"{LogTag} {name}: 未找到 Renderer/MeshFilter，使用默认 1m 包围盒。", this);
        }

        var box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.center = transform.InverseTransformPoint(worldBounds.center);
        Vector3 ls = transform.lossyScale;
        Vector3 size = worldBounds.size;
        size.x = Mathf.Max(0.15f, size.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)));
        size.y = Mathf.Max(0.15f, size.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)));
        size.z = Mathf.Max(0.15f, size.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
        box.size = size;

        if (debugSelectionCollider)
            Debug.Log($"{LogTag} {name}: 已在根节点添加 BoxCollider center={box.center} size={box.size}", this);
    }

    private bool TryComputeWorldBoundsEncapsulatingVisuals(out Bounds worldBounds)
    {
        bool has = false;
        worldBounds = default;

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!has)
            {
                worldBounds = r.bounds;
                has = true;
            }
            else
                worldBounds.Encapsulate(r.bounds);
        }

        if (has)
            return true;

        var meshFilters = GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            EncapsulateMeshWorldAabb(mf.sharedMesh.bounds, mf.transform, ref worldBounds, ref has);
        }

        return has;
    }

    private static void EncapsulateMeshWorldAabb(Bounds localMeshBounds, Transform t, ref Bounds acc, ref bool has)
    {
        Vector3 c = localMeshBounds.center;
        Vector3 e = localMeshBounds.extents;
        for (int ix = -1; ix <= 1; ix += 2)
        {
            for (int iy = -1; iy <= 1; iy += 2)
            {
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Vector3 cornerLocal = c + new Vector3(ix * e.x, iy * e.y, iz * e.z);
                    Vector3 w = t.TransformPoint(cornerLocal);
                    if (!has)
                    {
                        acc = new Bounds(w, Vector3.zero);
                        has = true;
                    }
                    else
                        acc.Encapsulate(w);
                }
            }
        }
    }

    public virtual void OnPlaced()
    {
        SetPortsVisualsActive(false);
    }

    public void SetPortsVisualsActive(bool active)
    {
        if (Ports == null) return;
        foreach (var port in Ports)
        {
            if (port == null) continue;
            foreach (var r in port.GetComponentsInChildren<Renderer>(true))
                r.enabled = active;
        }
    }

    protected Building GetRootBuilding()
    {
        Building root = null;
        for (Transform tr = transform; tr != null; tr = tr.parent)
        {
            var b = tr.GetComponent<Building>();
            if (b != null) root = b;
        }
        return root;
    }

    protected static Vector2Int GetGridCellAtPortWorld(BuildingGrid grid, Vector3 portWorldPosition)
    {
        return grid.WorldToGridPosition(portWorldPosition);
    }
}
