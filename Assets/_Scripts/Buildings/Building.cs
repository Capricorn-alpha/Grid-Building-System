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
}
