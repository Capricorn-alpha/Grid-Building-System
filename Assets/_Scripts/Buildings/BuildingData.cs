using UnityEngine;

[CreateAssetMenu(menuName = "Data/Building")]
public class BuildingData : ScriptableObject
{
    [field: SerializeField] public string Description {get; private set;}

    [field: SerializeField] public int Cost {get; private set;}
    
    [field: SerializeField] public BuildingModel Model {get; private set;}

    [field: SerializeField] public BuildingCategory Category {get; private set;}
}