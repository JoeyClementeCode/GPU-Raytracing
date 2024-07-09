using UnityEngine;

[System.Serializable]
public struct RaytracingMaterial
{
    public Vector3 color;
    public Vector3 specularColor;

    public void SetDefaultValues()
    {
        color = new Vector3(0.8f, 0.8f, 0.8f);
        specularColor = new Vector3(0.08f, 0.08f, 0.08f);
    }
}
