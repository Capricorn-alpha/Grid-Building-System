using Unity.XR.Oculus.Input;
using UnityEngine;

public class BeltItem : MonoBehaviour
{
    public GameObject item;
    public string id;
    public float Size = 1f;
    
    private void Awake()
    {
        item = gameObject;
    }
}
