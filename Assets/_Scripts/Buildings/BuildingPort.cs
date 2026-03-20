using UnityEngine;

public class BuildingPort : MonoBehaviour
{
    public PortType PortType;

    public string portId = "Deafault";
}

public enum PortType
{
    Input,
    Output
}
