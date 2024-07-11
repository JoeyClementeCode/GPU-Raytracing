using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
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
    private RenderTexture compositeBufferTexture;
    private Camera cam;

    private uint currentSample = 0;
    public int seed;
    private Material AA;
    
    [SerializeField] private int maxReflections = 8;

    [Header("Sphere Parameters")]
    [SerializeField] private Vector2 sphereRadius = new Vector2(3.0f, 8.0f);
    [SerializeField] private int sphereCount = 100;
    [SerializeField] private float spherePlacementRadius = 100.0f;
    private ComputeBuffer sphereBuffer;

    [Header("Info")]
    [SerializeField] int numRenderedFrames;


    private static bool meshObjectsNeedRebuilding = false;
    private static List<RaytracingObject> raytracingObjects = new List<RaytracingObject>();

    public static void RegisterObject(RaytracingObject obj)
    {
        raytracingObjects.Add(obj);
        meshObjectsNeedRebuilding = true;
    }
    public static void UnregisterObject(RaytracingObject obj)
    {
        raytracingObjects.Remove(obj);
        meshObjectsNeedRebuilding = true;
    }

    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;
        public Vector3 albedo;
        public Vector3 specular;
        public float smoothness;
        public Vector3 emission;
        public float emissionStrength;
    }

    private static List<MeshObject> _meshObjects = new List<MeshObject>();
    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<int> _indices = new List<int>();
    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;

    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
        public float smoothness;
        public Vector3 emission;
        public float emissionStrength;
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
        RebuildMeshObjectBuffers();
        SceneSetupActual();
    }

    private void OnDisable()
    {
        if (sphereBuffer != null)
            sphereBuffer.Release();

        if (_meshObjectBuffer != null)
            _meshObjectBuffer.Release();

        if (_indexBuffer != null)
            _indexBuffer.Release();

        if (_vertexBuffer != null)
            _vertexBuffer.Release();
    }

    private void SceneSetup()
    {
        //Random.InitState(seed);

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
            float chance = Random.value;

            if (chance < 0.8f)
            {
                bool metal = chance < 0.4f;
                sphere.albedo = metal ? Vector4.zero : new Vector4(color.r, color.g, color.b);
                sphere.specular = metal ? new Vector4(color.r, color.g, color.b) : new Vector4(0.04f, 0.04f, 0.04f);
                sphere.smoothness = Random.value;
            }
            else
            {
                Color emission = Random.ColorHSV(0, 1, 0, 1, 3.0f, 8.0f);
                sphere.emission = new Vector3(emission.r, emission.g, emission.b);
            }

            spheres.Add(sphere);

        SkipSphere:
            continue;
        }

        sphereBuffer = new ComputeBuffer(spheres.Count, 60);
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

            sphere.smoothness = sphereObjects[i].material.smoothness;
            sphere.emission = sphereObjects[i].material.emission;
            sphere.emissionStrength = sphereObjects[i].material.emissionStrength;

            spheres.Add(sphere);
        }

        sphereBuffer = new ComputeBuffer(spheres.Count, 60);
        sphereBuffer.SetData(spheres);
    }

    private void RebuildMeshObjectBuffers()
    {
        if (!meshObjectsNeedRebuilding)
        {
            return;
        }
        meshObjectsNeedRebuilding = false;
        currentSample = 0;
        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();
        // Loop over all objects and gather their data
        foreach (RaytracingObject obj in raytracingObjects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            // Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);
            // Add index data - if the vertex buffer wasn't empty before, the
            // indices need to be offset
            int firstIndex = _indices.Count;
            var indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(index => index + firstVertex));
            // Add the object itself
            _meshObjects.Add(new MeshObject()
            {
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length,
                albedo = obj.material.color,
                specular = obj.material.specularColor,
                emission = obj.material.emission,
                emissionStrength = obj.material.emissionStrength,
                smoothness = obj.material.smoothness
            });

            
        }
        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 116);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
        where T : struct
    {
        // Do we already have a compute buffer?
        if (buffer != null)
        {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }
        if (data.Count != 0)
        {
            // If the buffer has been released or wasn't there to
            // begin with, create it
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }
            // Set data on the buffer
            buffer.SetData(data);
        }
    }
    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            computeShader.SetBuffer(0, name, buffer);
        }
    }

    RenderTexture resultTexture;

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        RebuildMeshObjectBuffers();
        SetParams();

        // Create copy of prev frame
        RenderTexture prevFrameCopy = RenderTexture.GetTemporary(src.width, src.height, 0, GraphicsFormat.R32G32B32A32_SFloat);
        Graphics.Blit(resultTexture, prevFrameCopy);

        // Run the ray tracing shader and draw the result to a temp texture
        computeShader.SetInt("Frame", numRenderedFrames);
        RenderTexture currentFrame = RenderTexture.GetTemporary(src.width, src.height, 0, GraphicsFormat.R32G32B32A32_SFloat);
        Graphics.Blit(null, currentFrame);

        // Accumulate
        if (AA == null)
        {
            AA = new Material(Shader.Find("Hidden/AA"));
        }

        AA.SetInt("_Frame", numRenderedFrames);
        AA.SetTexture("_PrevFrame", prevFrameCopy);
        Graphics.Blit(currentFrame, resultTexture, AA);

        // Draw result to screen
        Graphics.Blit(resultTexture, dest);

        // Release temps
        RenderTexture.ReleaseTemporary(currentFrame);
        RenderTexture.ReleaseTemporary(prevFrameCopy);
        RenderTexture.ReleaseTemporary(currentFrame);

        numRenderedFrames += Application.isPlaying ? 1 : 0;
    }
    
    private void Render(RenderTexture destination)
    {


        /*// Make sure we have a current render target
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        computeShader.SetTexture(0, "Result", renderTexture);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 16.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 16.0f);
        computeShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        // Anti-Aliasing
        if (AA == null)
        {
            AA = new Material(Shader.Find("Hidden/AA"));
        }
        AA.SetFloat("_Sample", currentSample);
        Graphics.Blit(renderTexture, compositeBufferTexture, AA);
        Graphics.Blit(compositeBufferTexture, destination);
        currentSample++;*/
    }

    private void InitRenderTexture()
    {
        /*if (renderTexture == null)
        {
            // Release render texture if we already have one
            if (renderTexture != null)
            {
                renderTexture.Release();
                compositeBufferTexture.Release();
            }

            // Gets render targets for Ray Tracing
            renderTexture = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
            compositeBufferTexture = new RenderTexture(Screen.width, Screen.height, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            compositeBufferTexture.enableRandomWrite = true;
            compositeBufferTexture.Create();

            currentSample = 0;
        }*/
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

        SetComputeBuffer("_Spheres", sphereBuffer);
        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);

        computeShader.SetFloat("_Seed", Random.value);
    }
}
