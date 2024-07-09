using UnityEngine;

public class RaytracedSphere : MonoBehaviour
{
    public RaytracingMaterial material;

    [SerializeField, HideInInspector] int materialObjectID;
    [SerializeField, HideInInspector] bool materialInitFlag;

    void OnValidate()
    {
        if (!materialInitFlag)
        {
            materialInitFlag = true;
            material.SetDefaultValues();
        }

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            if (materialObjectID != gameObject.GetInstanceID())
            {
                renderer.sharedMaterial = new Material(renderer.sharedMaterial);
                materialObjectID = gameObject.GetInstanceID();
            }
            renderer.sharedMaterial.color = new Color(material.color.x, material.color.y, material.color.z);
        }
    }
}
