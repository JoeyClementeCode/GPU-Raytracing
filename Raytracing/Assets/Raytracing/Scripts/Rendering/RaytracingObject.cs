using UnityEngine;


[RequireComponent (typeof(MeshRenderer))]
[RequireComponent (typeof(MeshFilter))]
public class RaytracingObject : MonoBehaviour
{
    public RaytracingMaterial material;

    private void OnEnable()
    {
        ComputeTest.RegisterObject(this);
    }
    private void OnDisable()
    {
        ComputeTest.UnregisterObject(this);
    }
}
