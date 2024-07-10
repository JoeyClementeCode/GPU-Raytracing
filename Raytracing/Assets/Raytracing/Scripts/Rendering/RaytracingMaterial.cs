using UnityEngine;

[System.Serializable]
public struct RaytracingMaterial
{
    public Vector3 color;
    public Vector3 specularColor;

    public float smoothness;

    public Vector3 emission;
    public float emissionStrength;

    public void SetDefaultValues()
    {
        color = new Vector3(0.8f, 0.8f, 0.8f);
        specularColor = new Vector3(0.08f, 0.08f, 0.08f);
        smoothness = 0.0f;
        emission = new Vector3(0.0f, 0.0f, 0.0f);
        emissionStrength = 1.0f;
    }
}
