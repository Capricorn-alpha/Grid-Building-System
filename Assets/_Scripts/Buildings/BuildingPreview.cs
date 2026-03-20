using System.Collections.Generic;
using UnityEngine;

public class BuildingPreview : MonoBehaviour
{
    public enum BuildingPreviewState
    {
        Positive,
        Negative
    }

    [SerializeField] private Material positiveMaterial;
    [SerializeField] private Material negativeMaterial;

    public BuildingPreviewState State {get; private set;} = BuildingPreviewState.Negative;
    public BuildingData Data {get; private set;}
    public BuildingModel BuildingModel{get; private set;}
    private List<Renderer> renderers = new();
    private List<Collider> colliders = new();
    public void Setup(BuildingData data)
    {
        Data = data;
        BuildingModel = Instantiate(data.Model, transform.position, Quaternion.identity, transform);
        renderers.AddRange(BuildingModel.GetComponentsInChildren<Renderer>());
        colliders.AddRange(BuildingModel.GetComponentsInChildren<Collider>());
        foreach(var col in colliders)
        {
            col.enabled = false;
        }

        var ports = BuildingModel.GetComponentsInChildren<BuildingPort>(true);
        foreach (var port in ports)
        {
            port.gameObject.SetActive(true);
        }

        SetPreviewMaterial(State);
    }

    public void ChangeState(BuildingPreviewState newState)
    {
        if(newState == State) return;
        State = newState;
        SetPreviewMaterial(State);
    }

    public void Rotate(int rotationStep)
    {
        BuildingModel.Rotate(rotationStep);
    }

    private void SetPreviewMaterial(BuildingPreviewState newState)
    {
        Material previewMat = newState == BuildingPreviewState.Positive ? positiveMaterial : negativeMaterial;
        foreach(var rend in renderers)
        {
            Material[] mats = new Material[rend.sharedMaterials.Length];
            for(int i = 0; i < mats.Length; i++)
            {
                mats[i] = previewMat;
            }
            rend.materials = mats;
        }
    }
}
