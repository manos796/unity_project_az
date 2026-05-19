using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class SimpleTrueDepthPrototypeExporter : MonoBehaviour
{
    private const string DefaultOutputRoot = "GeneratedSamples/DepthCapturesPrototype";

    [Serializable]
    private struct RendererMaterialState
    {
        public Renderer renderer;
        public Material[] sharedMaterials;
    }

    [Header("Dependencies")]
    public FixedSensorRig sensorRig;
    public SimpleSceneCycleController sceneController;

    [Header("Depth Prototype")]
    public Shader prototypeDepthShader;
    public string relativeOutputRoot = DefaultOutputRoot;
    public bool logExports = true;

    private Material prototypeDepthMaterial;

    private void Awake()
    {
        AutoResolveDependencies();
    }

    private void OnValidate()
    {
        AutoResolveDependencies();
    }

    [ContextMenu("Export Prototype True Depth For Latest Accepted Sample")]
    public void ExportLatestAcceptedPrototypeDepth()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("SimpleTrueDepthPrototypeExporter: Enter Play Mode before exporting.");
            return;
        }

        if (!TryExportLatestAcceptedPrototypeDepth(out string summary))
        {
            Debug.LogWarning("SimpleTrueDepthPrototypeExporter: " + summary);
        }
    }

    public bool TryExportLatestAcceptedPrototypeDepth(out string summary)
    {
        AutoResolveDependencies();

        if (sceneController == null || !sceneController.LastGenerationAccepted)
        {
            summary = "no_accepted_sample_ready";
            return false;
        }

        if (sensorRig == null)
        {
            summary = "sensor_rig_missing";
            return false;
        }

        if (prototypeDepthShader == null)
        {
            summary = "prototype_depth_shader_missing";
            return false;
        }

        string sampleName = "sample_" + sceneController.LastAcceptedSampleIndex.ToString("D4");
        string timestampUtc = DateTime.UtcNow.ToString("O");
        string sampleDirectory = PrepareSampleDirectory(sampleName);

        sensorRig.ApplyConfigurationToChildren();
        Physics.SyncTransforms();

        Camera[] cameras = sensorRig.GetOrderedCameras();
        if (cameras.Length == 0)
        {
            summary = "fixed_rig_cameras_missing";
            return false;
        }

        DepthMetadataExport depthMetadata = sensorRig.BuildDepthMetadata(sceneController.LastAcceptedSampleIndex, sampleName, timestampUtc);

        List<RendererMaterialState> rendererStates = OverrideSceneRenderersForDepth();
        try
        {
            for (int i = 0; i < cameras.Length; i++)
            {
                float[] depthValues = CaptureDepthMeters(cameras[i], sensorRig.width, sensorRig.height);
                WriteFloatRaw(Path.Combine(sampleDirectory, depthMetadata.fullDepthRawFiles[i]), depthValues);
                WriteBytes(
                    Path.Combine(sampleDirectory, depthMetadata.fullDepthPreviewFiles[i]),
                    BuildDepthPreviewPng(depthValues, sensorRig.width, sensorRig.height, sensorRig.farClip));
            }
        }
        finally
        {
            RestoreSceneRenderers(rendererStates);
        }

        WriteJson(Path.Combine(sampleDirectory, "depth_metadata.json"), depthMetadata);
        summary = "prototype_depth_exported path=" + sampleDirectory;
        if (logExports)
        {
            Debug.Log("SimpleTrueDepthPrototypeExporter: " + summary);
        }

        return true;
    }

    private float[] CaptureDepthMeters(Camera cameraComponent, int width, int height)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = cameraComponent.targetTexture;
        bool previousEnabled = cameraComponent.enabled;
        CameraClearFlags previousClearFlags = cameraComponent.clearFlags;
        Color previousBackground = cameraComponent.backgroundColor;

        cameraComponent.targetTexture = renderTexture;
        cameraComponent.enabled = false;
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = Color.black;
        cameraComponent.Render();

        RenderTexture.active = renderTexture;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RFloat, false);
        texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        texture.Apply();

        Color[] pixels = texture.GetPixels();
        float[] depthValues = new float[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            float value = pixels[i].r;
            if (value < cameraComponent.nearClipPlane)
            {
                value = cameraComponent.farClipPlane;
            }

            depthValues[i] = Mathf.Clamp(value, cameraComponent.nearClipPlane, cameraComponent.farClipPlane);
        }

        cameraComponent.targetTexture = previousTarget;
        cameraComponent.enabled = previousEnabled;
        cameraComponent.clearFlags = previousClearFlags;
        cameraComponent.backgroundColor = previousBackground;
        RenderTexture.active = previousActive;

        RenderTexture.ReleaseTemporary(renderTexture);
        Destroy(texture);
        return depthValues;
    }

    private List<RendererMaterialState> OverrideSceneRenderersForDepth()
    {
        Material depthMaterial = GetPrototypeDepthMaterial();
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        List<RendererMaterialState> states = new List<RendererMaterialState>(renderers.Length);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                continue;
            }

            RendererMaterialState state = new RendererMaterialState
            {
                renderer = renderer,
                sharedMaterials = sharedMaterials
            };
            states.Add(state);

            Material[] replacement = new Material[sharedMaterials.Length];
            for (int m = 0; m < replacement.Length; m++)
            {
                replacement[m] = depthMaterial;
            }

            renderer.sharedMaterials = replacement;
        }

        return states;
    }

    private void RestoreSceneRenderers(List<RendererMaterialState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].renderer != null)
            {
                states[i].renderer.sharedMaterials = states[i].sharedMaterials;
            }
        }
    }

    private Material GetPrototypeDepthMaterial()
    {
        if (prototypeDepthMaterial != null && prototypeDepthMaterial.shader == prototypeDepthShader)
        {
            return prototypeDepthMaterial;
        }

        if (prototypeDepthMaterial != null)
        {
            Destroy(prototypeDepthMaterial);
        }

        prototypeDepthMaterial = new Material(prototypeDepthShader);
        prototypeDepthMaterial.hideFlags = HideFlags.HideAndDontSave;
        return prototypeDepthMaterial;
    }

    private static byte[] BuildDepthPreviewPng(float[] depthValues, int width, int height, float farClip)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false, false);
        Color32[] colors = new Color32[depthValues.Length];
        float maxDepth = 0f;

        for (int i = 0; i < depthValues.Length; i++)
        {
            float value = depthValues[i];
            if (value < farClip && value > maxDepth)
            {
                maxDepth = value;
            }
        }

        if (maxDepth <= 0f)
        {
            maxDepth = farClip;
        }

        for (int i = 0; i < depthValues.Length; i++)
        {
            float normalized = Mathf.Clamp01(depthValues[i] / maxDepth);
            byte value = (byte)Mathf.Clamp(Mathf.RoundToInt((1f - normalized) * 255f), 0, 255);
            colors[i] = new Color32(value, value, value, 255);
        }

        texture.SetPixels32(colors);
        texture.Apply(false, false);
        byte[] pngBytes = texture.EncodeToPNG();
        Destroy(texture);
        return pngBytes;
    }

    private string PrepareSampleDirectory(string sampleName)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string baseDirectory = Path.Combine(projectRoot, relativeOutputRoot);
        string sampleDirectory = Path.Combine(baseDirectory, sampleName);

        Directory.CreateDirectory(baseDirectory);
        if (Directory.Exists(sampleDirectory))
        {
            Directory.Delete(sampleDirectory, true);
        }

        Directory.CreateDirectory(sampleDirectory);
        return sampleDirectory;
    }

    private static void WriteFloatRaw(string path, float[] values)
    {
        byte[] bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        File.WriteAllBytes(path, bytes);
    }

    private static void WriteBytes(string path, byte[] bytes)
    {
        File.WriteAllBytes(path, bytes);
    }

    private static void WriteJson<T>(string path, T value)
    {
        File.WriteAllText(path, JsonUtility.ToJson(value, true));
    }

    private void AutoResolveDependencies()
    {
        if (sensorRig == null)
        {
            sensorRig = GetComponent<FixedSensorRig>();
            if (sensorRig == null)
            {
                sensorRig = FindAnyObjectByType<FixedSensorRig>();
            }
        }

        if (sceneController == null)
        {
            sceneController = FindAnyObjectByType<SimpleSceneCycleController>();
        }

        if (prototypeDepthShader == null)
        {
            prototypeDepthShader = Shader.Find("Hidden/SimpleTrueDepthPrototype");
        }
    }

    private void OnDestroy()
    {
        if (prototypeDepthMaterial != null)
        {
            Destroy(prototypeDepthMaterial);
            prototypeDepthMaterial = null;
        }
    }
}
