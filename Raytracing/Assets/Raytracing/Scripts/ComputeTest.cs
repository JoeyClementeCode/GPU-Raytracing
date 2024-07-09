using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class ComputeTest : MonoBehaviour
{
    public ComputeShader computeShader;
    public Texture skyboxTexture;
    private RenderTexture renderTexture;
    private Camera cam;

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
    }

    private void InitRenderTexture()
    {
        if (renderTexture == null || renderTexture.width != Screen.width || renderTexture.height != Screen.height)
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
    }
}
