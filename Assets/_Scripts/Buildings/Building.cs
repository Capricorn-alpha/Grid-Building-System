using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

public class Building : MonoBehaviour
{
    public string Description => data.Description;
    public int Cost => data.Cost;
    protected BuildingModel model {get; private set;}
    protected BuildingData data {get; private set;}
    public BuildingPort[] Ports{get; private set;}

    public void Setup(BuildingData data, float rotation)
    {
        this.data = data;
        model = Instantiate(data.Model, transform.position, Quaternion.identity, transform);
        model.Rotate(rotation);

        Ports = GetComponentsInChildren<BuildingPort>(true);
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

    /// <summary>
    /// 从当前物体沿父链（含自身）向上，取最顶层的 <see cref="Building"/>。
    /// 与执行 <see cref="Setup"/> 时缓存 <see cref="Ports"/> 的根一致，避免子物体上的逻辑脚本用错端口变换。
    /// </summary>
    protected Building GetRootBuilding()
    {
        Building root = null;
        for (Transform t = transform; t != null; t = t.parent)
        {
            var b = t.GetComponent<Building>();
            if (b != null) root = b;
        }
        return root;
    }

    /// <summary>
    /// 输出口 Transform 已摆在目标格（与传送带同格）时使用：用端口世界坐标直接映射网格，不再叠加 forward 半格偏移。
    /// </summary>
    protected static Vector2Int GetGridCellAtPortWorld(BuildingGrid grid, Vector3 portWorldPosition)
    {
        return grid.WorldToGridPosition(portWorldPosition);
    }
}
