using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Experimental.Rendering;

using Random = UnityEngine.Random;

[ImageEffectAllowedInSceneView]
public class ComputeTest : MonoBehaviour
{
    public ComputeShader computeShader;
    public Texture skyboxTexture;
    public Light sun;
    private RenderTexture renderTexture;
    private Camera cam;

    private uint currentSample = 0;
    private Material AA;

    [Range(1, 8)]
    [SerializeField] private int maxReflections = 8;

    [Header("Sphere Parameters")]
    [SerializeField] private Vector2 sphereRadius = new Vector2(3.0f, 8.0f);
    [SerializeField] private int sphereCount = 100;
    [SerializeField] private float spherePlacementRadius = 100.0f;
    private ComputeBuffer sphereBuffer;

    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
    };

    private void Update()
    {
        if (transform.hasChanged)
        {
            currentSample = 0;
            transform.hasChanged = false;
        }
    }

    private void OnEnable()
    {
        currentSample = 0;
        SceneSetupActual();
    }

    private void OnDisable()
    {
        if (sphereBuffer != null)
            sphereBuffer.Release();
    }

    private void SceneSetup()
    {
        List<Sphere> spheres = new List<Sphere>();

        for (int i = 0; i < sphereCount; i++)
        {
            Sphere sphere = new Sphere();

            sphere.radius = sphereRadius.x + Random.value * (sphereRadius.y - sphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * spherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);

            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist)
                {
                    goto SkipSphere;
                }
            }

            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            spheres.Add(sphere);

        SkipSphere:
            continue;
        }

        sphereBuffer = new ComputeBuffer(spheres.Count, 40);
        sphereBuffer.SetData(spheres);
    }

    private void SceneSetupActual()
    {
        RaytracedSphere[] sphereObjects = FindObjectsOfType<RaytracedSphere>();
        List<Sphere> spheres = new List<Sphere>();

        for (int i = 0; i < sphereObjects.Length; i++)
        {
            Sphere sphere = new Sphere();

            sphere.radius = sphereObjects[i].transform.localScale.x * 0.5f;
            sphere.position = sphereObjects[i].transform.position;

            sphere.albedo = sphereObjects[i].material.color;
            sphere.specular = sphereObjects[i].material.specularColor;

            spheres.Add(sphere);
        }

        sphereBuffer = new ComputeBuffer(spheres.Count, 40);
        sphereBuffer.SetData(spheres);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        SetParams();
        Render(dest);
    }
    
    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        computeShader.SetTexture(0, "Result", renderTexture);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 16.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 16.0f);
        computeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        // Blit the result texture to the screen
        Graphics.Blit(renderTexture, destination);

        // Anti-Aliasing
        if (AA == null)
        {
            AA = new Material(Shader.Find("Hidden/AA"));
        }
        AA.SetFloat("_Sample", currentSample);
        Graphics.Blit(renderTexture, destination, AA);
        currentSample++;
    }

    private void InitRenderTexture()
    {
        if (renderTexture == null)
        {
            // Release render texture if we already have one
            if (renderTexture != null)
                renderTexture.Release();

            // Get a render target for Ray Tracing
            renderTexture = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
        }
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void SetParams()
    {
        computeShader.SetMatrix("_CameraToWorld", cam.cameraToWorldMatrix);
        computeShader.SetMatrix("_CameraInverseProjection", cam.projectionMatrix.inverse);
        computeShader.SetTexture(0, "_SkyboxTexture", skyboxTexture);
        computeShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        computeShader.SetInt("_MaxReflections", maxReflections);

        Vector3 light = sun.transform.forward;
        computeShader.SetVector("_DirectionalLight", new Vector4(light.x, light.y, light.z, sun.intensity));

        computeShader.SetBuffer(0, "_Spheres", sphereBuffer);
    }
}
