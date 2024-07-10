using UnityEngine;


[RequireComponent (typeof(MeshRenderer))]
[RequireComponent (typeof(MeshFilter))]
public class RaytracingObject : MonoBehaviour
{ 
    private void OnEnable()
    {
        ComputeTest.RegisterObject(this);
    }
    private void OnDisable()
    {
        ComputeTest.UnregisterObject(this);
    }
}
