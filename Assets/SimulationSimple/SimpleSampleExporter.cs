using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class SimpleSampleExporter : MonoBehaviour
{
    public enum DepthCaptureBackend
    {
        MaterialOverridePrototype = 0,
        UrpDepthTexture = 1,
    }

    [Serializable]
    private struct RendererMaterialState
    {
        public Renderer renderer;
        public Material[] sharedMaterials;
    }

    private const string DefaultOutputRoot = "GeneratedSamples/DepthCaptures";
    private const string DefaultSideCameraName = "SideRgbCam";
    private const string SideRgbFileName = "cam_side_rgb.png";

    private readonly List<Bounds> voxelBounds = new List<Bounds>();
    private Material linearDepthMaterial;
    private Material prototypeDepthMaterial;

    [Header("Dependencies")]
    public FixedSensorRig sensorRig;
    public Camera sideRgbCamera;
    public SimpleSceneCycleController sceneController;
    public RobotPoseController poseController;
    public SimplePropSpawner propSpawner;
    public SimpleForbiddenZones forbiddenZones;

    [Header("Output")]
    public string relativeOutputRoot = DefaultOutputRoot;
    public bool logExports = true;

    [Header("Depth Capture")]
    public DepthCaptureBackend depthBackend = DepthCaptureBackend.MaterialOverridePrototype;
    public Shader prototypeDepthShader;
    public Shader linearDepthShader;

    [Header("Voxel Grid")]
    public float voxelSizeMeters = 0.05f;

    [Header("Runtime Debug")]
    [SerializeField] private string lastExportDirectory = "";
    [SerializeField] [TextArea(4, 10)] private string lastExportSummary = "";

    public string LastExportDirectory
    {
        get { return lastExportDirectory; }
    }

    private void Awake()
    {
        AutoResolveDependencies();
    }

    private void OnValidate()
    {
        AutoResolveDependencies();
    }

    [ContextMenu("Export Latest Accepted Simple Sample")]
    public void ExportLatestAcceptedSample()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("SimpleSampleExporter: Enter Play Mode before exporting.");
            return;
        }

        if (!TryExportLatestAcceptedSample(out string summary))
        {
            Debug.LogWarning("SimpleSampleExporter: " + summary);
        }
    }

    public bool TryExportLatestAcceptedSample(out string summary)
    {
        AutoResolveDependencies();

        if (sceneController == null || !sceneController.LastGenerationAccepted)
        {
            summary = "no_accepted_sample_ready";
            return false;
        }

        return TryExportAcceptedSample(sceneController.LastAcceptedSampleIndex, out summary);
    }

    public bool TryExportAcceptedSample(int sampleIndex, out string summary)
    {
        AutoResolveDependencies();

        if (!ValidateDependencies(out summary))
        {
            return false;
        }

        if (sampleIndex < 0)
        {
            summary = "invalid_sample_index";
            return false;
        }

        string sampleName = "sample_" + sampleIndex.ToString("D4");
        string timestampUtc = DateTime.UtcNow.ToString("O");
        string sampleDirectory = PrepareSampleDirectory(sampleName);

        Camera[] rigCameras = sensorRig.GetOrderedCameras();
        if (rigCameras.Length == 0)
        {
            summary = "fixed_rig_cameras_missing";
            return false;
        }

        sensorRig.ApplyConfigurationToChildren();
        Physics.SyncTransforms();

        DepthMetadataExport depthMetadata = sensorRig.BuildDepthMetadata(sampleIndex, sampleName, timestampUtc);
        RgbMetadataExport rgbMetadata = sensorRig.BuildRgbMetadata(sampleIndex, sampleName, timestampUtc);
        RgbMetadataExport sideRgbMetadata = sensorRig.BuildSingleRgbMetadata(sampleIndex, sampleName, timestampUtc, sideRgbCamera, SideRgbFileName);

        if (!TryCaptureRigDepthExports(rigCameras, depthMetadata, sampleDirectory, out summary))
        {
            return false;
        }

        for (int i = 0; i < rigCameras.Length; i++)
        {
            WriteBytes(Path.Combine(sampleDirectory, rgbMetadata.rgbFiles[i]), CaptureRgbPng(rigCameras[i], sensorRig.width, sensorRig.height));
        }

        WriteBytes(Path.Combine(sampleDirectory, SideRgbFileName), CaptureRgbPng(sideRgbCamera, sensorRig.width, sensorRig.height));

        LegacyRobotPoseExport robotExport = poseController.CaptureLegacyPoseExport();
        CanonicalRobotStateExport robotStateExport = poseController.CaptureCanonicalStateExport();
        SimpleRandomizationExport randomizationExport = BuildRandomizationExport();
        SceneObjectsExport sceneObjectsExport = BuildSceneObjectsExport();

        byte[] propVoxelBytes;
        SimpleVoxelMetadataExport propVoxelMetadata = BuildVoxelMetadata(true, "voxel_props_occupancy.raw", out propVoxelBytes);
        byte[] sceneVoxelBytes;
        SimpleVoxelMetadataExport sceneVoxelMetadata = BuildVoxelMetadata(false, "voxel_scene_occupancy.raw", out sceneVoxelBytes);

        SimpleSampleMetadataExport sampleExport = new SimpleSampleMetadataExport
        {
            sampleIndex = sampleIndex,
            sampleName = sampleName,
            timestampUtc = timestampUtc,
            poseProfile = sceneController != null ? sceneController.playModeProfile.ToString() : RobotPoseProfile.FreeRoomSnapshot.ToString(),
            robotSeed = sceneController != null ? sceneController.LastAcceptedRobotSeed : -1,
            propSeed = sceneController != null && sceneController.LastAcceptedScene != null ? sceneController.LastAcceptedScene.propSeed : -1,
            depth = depthMetadata,
            rgb = rgbMetadata,
            sideRgb = sideRgbMetadata,
            robot = robotExport,
            robotState = robotStateExport,
            randomization = randomizationExport,
            sceneObjects = sceneObjectsExport,
            voxel = propVoxelMetadata,
            voxelScene = sceneVoxelMetadata,
        };

        WriteJson(Path.Combine(sampleDirectory, "depth_metadata.json"), depthMetadata);
        WriteJson(Path.Combine(sampleDirectory, "rgb_metadata.json"), rgbMetadata);
        WriteJson(Path.Combine(sampleDirectory, "side_rgb_metadata.json"), sideRgbMetadata);
        WriteJson(Path.Combine(sampleDirectory, "robot_pose.json"), robotExport);
        WriteJson(Path.Combine(sampleDirectory, "robot_state.json"), robotStateExport);
        WriteJson(Path.Combine(sampleDirectory, "randomization.json"), randomizationExport);
        WriteJson(Path.Combine(sampleDirectory, "scene_objects.json"), sceneObjectsExport);
        WriteJson(Path.Combine(sampleDirectory, "voxel_metadata.json"), propVoxelMetadata);
        WriteJson(Path.Combine(sampleDirectory, "voxel_scene_metadata.json"), sceneVoxelMetadata);
        WriteJson(Path.Combine(sampleDirectory, "sample_metadata.json"), sampleExport);

        WriteBytes(Path.Combine(sampleDirectory, propVoxelMetadata.fileName), propVoxelBytes);
        WriteBytes(Path.Combine(sampleDirectory, sceneVoxelMetadata.fileName), sceneVoxelBytes);

        lastExportDirectory = sampleDirectory;
        lastExportSummary =
            "sample=" + sampleName +
            " robotSeed=" + sampleExport.robotSeed +
            " propSeed=" + sampleExport.propSeed +
            " propsPlaced=" + randomizationExport.placedProps +
            " voxelProps=" + propVoxelMetadata.occupiedVoxels +
            " voxelScene=" + sceneVoxelMetadata.occupiedVoxels;

        if (logExports)
        {
            Debug.Log("SimpleSampleExporter: " + lastExportSummary + " path=" + sampleDirectory);
        }

        summary = lastExportSummary;
        return true;
    }

    private bool ValidateDependencies(out string summary)
    {
        if (sensorRig == null)
        {
            summary = "sensor_rig_missing";
            return false;
        }

        if (sideRgbCamera == null)
        {
            summary = "side_rgb_camera_missing";
            return false;
        }

        if (poseController == null)
        {
            summary = "pose_controller_missing";
            return false;
        }

        if (propSpawner == null)
        {
            summary = "simple_prop_spawner_missing";
            return false;
        }

        if (forbiddenZones == null)
        {
            summary = "simple_forbidden_zones_missing";
            return false;
        }

        if (depthBackend == DepthCaptureBackend.MaterialOverridePrototype && prototypeDepthShader == null)
        {
            summary = "prototype_depth_shader_missing";
            return false;
        }

        if (depthBackend == DepthCaptureBackend.UrpDepthTexture && linearDepthShader == null)
        {
            summary = "linear_depth_shader_missing";
            return false;
        }

        summary = "ok";
        return true;
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

    private bool TryCaptureRigDepthExports(
        Camera[] rigCameras,
        DepthMetadataExport depthMetadata,
        string sampleDirectory,
        out string summary)
    {
        if (depthBackend == DepthCaptureBackend.MaterialOverridePrototype)
        {
            List<RendererMaterialState> rendererStates = OverrideSceneRenderersForDepth();
            try
            {
                return TryWriteDepthExports(rigCameras, depthMetadata, sampleDirectory, CaptureDepthMetersMaterialOverride, out summary);
            }
            finally
            {
                RestoreSceneRenderers(rendererStates);
            }
        }

        return TryWriteDepthExports(rigCameras, depthMetadata, sampleDirectory, CaptureDepthMetersUrpDepthTexture, out summary);
    }

    private bool TryWriteDepthExports(
        Camera[] rigCameras,
        DepthMetadataExport depthMetadata,
        string sampleDirectory,
        Func<Camera, int, int, float[]> captureDepth,
        out string summary)
    {
        for (int i = 0; i < rigCameras.Length; i++)
        {
            float[] depthValues = captureDepth(rigCameras[i], sensorRig.width, sensorRig.height);
            if (!ValidateDepthRange(depthValues, rigCameras[i], out string depthError))
            {
                summary = depthError;
                return false;
            }

            WriteFloatRaw(Path.Combine(sampleDirectory, depthMetadata.fullDepthRawFiles[i]), depthValues);
            WriteBytes(
                Path.Combine(sampleDirectory, depthMetadata.fullDepthPreviewFiles[i]),
                BuildDepthPreviewPng(depthValues, sensorRig.width, sensorRig.height, sensorRig.nearClip, sensorRig.farClip));
        }

        summary = "ok";
        return true;
    }

    private float[] CaptureDepthMetersMaterialOverride(Camera cameraComponent, int width, int height)
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

    private float[] CaptureDepthMetersUrpDepthTexture(Camera cameraComponent, int width, int height)
    {
        RenderTexture sceneRenderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture depthRenderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = cameraComponent.targetTexture;
        bool previousEnabled = cameraComponent.enabled;
        CameraClearFlags previousClearFlags = cameraComponent.clearFlags;
        Color previousBackground = cameraComponent.backgroundColor;
        DepthTextureMode previousDepthTextureMode = cameraComponent.depthTextureMode;

        cameraComponent.targetTexture = sceneRenderTexture;
        cameraComponent.enabled = false;
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = Color.black;
        cameraComponent.depthTextureMode |= DepthTextureMode.Depth;
        cameraComponent.Render();

        Material depthMaterial = GetLinearDepthMaterial();
        Graphics.Blit(null, depthRenderTexture, depthMaterial);

        RenderTexture.active = depthRenderTexture;
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
        cameraComponent.depthTextureMode = previousDepthTextureMode;
        RenderTexture.active = previousActive;

        RenderTexture.ReleaseTemporary(sceneRenderTexture);
        RenderTexture.ReleaseTemporary(depthRenderTexture);
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

            states.Add(new RendererMaterialState
            {
                renderer = renderer,
                sharedMaterials = sharedMaterials,
            });

            Material[] replacement = new Material[sharedMaterials.Length];
            for (int materialIndex = 0; materialIndex < replacement.Length; materialIndex++)
            {
                replacement[materialIndex] = depthMaterial;
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

    private Material GetLinearDepthMaterial()
    {
        if (linearDepthMaterial != null && linearDepthMaterial.shader == linearDepthShader)
        {
            return linearDepthMaterial;
        }

        if (linearDepthMaterial != null)
        {
            Destroy(linearDepthMaterial);
        }

        linearDepthMaterial = new Material(linearDepthShader);
        linearDepthMaterial.hideFlags = HideFlags.HideAndDontSave;
        return linearDepthMaterial;
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

    private bool ValidateDepthRange(float[] depthValues, Camera cameraComponent, out string summary)
    {
        float minDepth = float.PositiveInfinity;
        float maxDepth = float.NegativeInfinity;

        for (int i = 0; i < depthValues.Length; i++)
        {
            float value = depthValues[i];
            if (!float.IsFinite(value))
            {
                summary = "depth_capture_invalid camera=" + cameraComponent.name + " reason=non_finite";
                return false;
            }

            if (value < minDepth)
            {
                minDepth = value;
            }

            if (value > maxDepth)
            {
                maxDepth = value;
            }
        }

        if (minDepth < cameraComponent.nearClipPlane || maxDepth > cameraComponent.farClipPlane)
        {
            summary =
                "depth_capture_invalid camera=" + cameraComponent.name +
                " min=" + minDepth.ToString("F4") +
                " max=" + maxDepth.ToString("F4") +
                " near=" + cameraComponent.nearClipPlane.ToString("F4") +
                " far=" + cameraComponent.farClipPlane.ToString("F4");
            return false;
        }

        if (minDepth >= cameraComponent.farClipPlane)
        {
            summary = "depth_capture_invalid camera=" + cameraComponent.name + " reason=no_geometry";
            return false;
        }

        summary = "ok";
        return true;
    }

    private byte[] CaptureRgbPng(Camera cameraComponent, int width, int height)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false, false);

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = cameraComponent.targetTexture;

        cameraComponent.targetTexture = renderTexture;
        cameraComponent.Render();

        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
        texture.Apply(false, false);
        byte[] pngBytes = texture.EncodeToPNG();

        cameraComponent.targetTexture = previousTarget;
        RenderTexture.active = previousActive;

        RenderTexture.ReleaseTemporary(renderTexture);
        Destroy(texture);
        return pngBytes;
    }

    private byte[] BuildDepthPreviewPng(float[] depthValues, int width, int height, float nearClip, float farClip)
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

    private SimpleRandomizationExport BuildRandomizationExport()
    {
        SimpleSceneSummary acceptedScene = sceneController != null ? sceneController.LastAcceptedScene : null;

        SimpleRandomizationExport export = new SimpleRandomizationExport();
        export.profile = sceneController != null ? sceneController.playModeProfile.ToString() : RobotPoseProfile.FreeRoomSnapshot.ToString();
        export.poseValid = sceneController != null && sceneController.LastGenerationAccepted;
        export.robotAttempts = sceneController != null ? sceneController.LastGenerationRobotAttempts : 0;
        export.robotAttemptLimit = sceneController != null ? sceneController.maxRobotPoseAttempts : 0;
        export.requestedProps = acceptedScene != null ? acceptedScene.requestedProps : 0;
        export.placedProps = acceptedScene != null ? acceptedScene.placedProps : 0;
        export.rejectedProps = acceptedScene != null ? acceptedScene.rejectedProps : 0;
        export.robotSeed = sceneController != null ? sceneController.LastAcceptedRobotSeed : -1;
        export.propSeed = acceptedScene != null ? acceptedScene.propSeed : -1;

        if (acceptedScene != null && acceptedScene.placedPropIds != null)
        {
            export.placedPropIds.AddRange(acceptedScene.placedPropIds);
        }

        return export;
    }

    private SceneObjectsExport BuildSceneObjectsExport()
    {
        SceneObjectsExport export = new SceneObjectsExport();
        AppendRoomEntry(export);
        AppendTableEntries(export);
        AppendPropEntries(export);
        return export;
    }

    private void AppendRoomEntry(SceneObjectsExport export)
    {
        Bounds roomBounds;
        if (!forbiddenZones.TryGetRoomInteriorBounds(out roomBounds))
        {
            return;
        }

        export.objects.Add(new SceneObjectExport
        {
            objectId = "room_interior_bounds",
            category = "room",
            surface = "Room",
            position = roomBounds.center,
            eulerRotation = Vector3.zero,
            scale = roomBounds.size,
            boundsSize = roomBounds.size,
        });
    }

    private void AppendTableEntries(SceneObjectsExport export)
    {
        if (forbiddenZones == null || forbiddenZones.tableRoot == null)
        {
            return;
        }

        SurgeryTableBuilder table = forbiddenZones.tableRoot.GetComponent<SurgeryTableBuilder>();
        if (table == null)
        {
            return;
        }

        Transform root = forbiddenZones.tableRoot.transform;
        AppendFixtureEntry(
            export,
            "table_top",
            "table",
            "Table",
            root.TransformPoint(new Vector3(table.longitudinalOffset, table.topSurfaceHeight - (table.topThickness * 0.5f), table.lateralOffset)),
            root.eulerAngles,
            new Vector3(table.topLength, table.topThickness, table.topWidth));

        if (table.includeSideRails)
        {
            float railY = table.topSurfaceHeight - (table.railHeight * 0.5f);
            float railZ = (table.topWidth * 0.5f) - table.railInsetFromEdge - (table.railWidth * 0.5f);
            float railLength = Mathf.Max(0.1f, table.topLength - 0.20f);

            AppendFixtureEntry(
                export,
                "rail_left",
                "table",
                "Table",
                root.TransformPoint(new Vector3(table.longitudinalOffset, railY, table.lateralOffset - railZ)),
                root.eulerAngles,
                new Vector3(railLength, table.railHeight, table.railWidth));

            AppendFixtureEntry(
                export,
                "rail_right",
                "table",
                "Table",
                root.TransformPoint(new Vector3(table.longitudinalOffset, railY, table.lateralOffset + railZ)),
                root.eulerAngles,
                new Vector3(railLength, table.railHeight, table.railWidth));
        }

        float pedestalHeight = Mathf.Max(0.25f, (table.topSurfaceHeight - table.topThickness) - table.baseHeight);
        float builtBaseLength = table.baseMatchesPedestalFootprint ? table.pedestalWidth : table.baseLength;
        float builtBaseWidth = table.baseMatchesPedestalFootprint ? table.pedestalDepth : table.baseWidth;

        AppendFixtureEntry(
            export,
            "pedestal",
            "table",
            "Table",
            root.TransformPoint(new Vector3(table.supportLongitudinalOffset, table.baseHeight + (pedestalHeight * 0.5f), table.supportLateralOffset)),
            root.eulerAngles,
            new Vector3(table.pedestalWidth, pedestalHeight, table.pedestalDepth));

        AppendFixtureEntry(
            export,
            "table_base",
            "table",
            "Table",
            root.TransformPoint(new Vector3(table.supportLongitudinalOffset, table.baseHeight * 0.5f, table.supportLateralOffset)),
            root.eulerAngles,
            new Vector3(builtBaseLength, table.baseHeight, builtBaseWidth));
    }

    private void AppendFixtureEntry(
        SceneObjectsExport export,
        string objectId,
        string category,
        string surface,
        Vector3 position,
        Vector3 eulerRotation,
        Vector3 size)
    {
        export.objects.Add(new SceneObjectExport
        {
            objectId = objectId,
            category = category,
            surface = surface,
            position = position,
            eulerRotation = eulerRotation,
            scale = size,
            boundsSize = size,
        });
    }

    private void AppendPropEntries(SceneObjectsExport export)
    {
        if (propSpawner == null || propSpawner.activePropsRoot == null)
        {
            return;
        }

        for (int i = 0; i < propSpawner.activePropsRoot.childCount; i++)
        {
            Transform child = propSpawner.activePropsRoot.GetChild(i);
            Bounds bounds;
            if (!TryGetCombinedBounds(child, out bounds))
            {
                continue;
            }

            string propId = ExtractPropId(child.name);
            export.objects.Add(new SceneObjectExport
            {
                objectId = child.name,
                category = propId,
                surface = InferSurfaceLabel(propId),
                position = child.position,
                eulerRotation = child.eulerAngles,
                scale = child.lossyScale,
                boundsSize = bounds.size,
            });
        }
    }

    private SimpleVoxelMetadataExport BuildVoxelMetadata(bool propsOnly, string fileName, out byte[] occupancy)
    {
        Bounds roomBounds;
        if (!forbiddenZones.TryGetRoomInteriorBounds(out roomBounds))
        {
            occupancy = new byte[0];
            return new SimpleVoxelMetadataExport
            {
                fileName = fileName,
                sizeX = 0,
                sizeY = 0,
                sizeZ = 0,
                origin = Vector3.zero,
                voxelSize = Vector3.one * Mathf.Max(0.001f, voxelSizeMeters),
                occupiedVoxels = 0,
                propsOnly = propsOnly,
            };
        }

        voxelBounds.Clear();
        if (propsOnly)
        {
            CollectBounds(propSpawner != null ? propSpawner.activePropsRoot : null, voxelBounds);
        }
        else
        {
            CollectBounds(propSpawner != null ? propSpawner.activePropsRoot : null, voxelBounds);
            CollectBounds(poseController != null && poseController.robotRoot != null ? poseController.robotRoot.transform : null, voxelBounds);
            CollectBounds(forbiddenZones != null && forbiddenZones.tableRoot != null ? forbiddenZones.tableRoot.transform : null, voxelBounds);
        }

        float voxelSize = Mathf.Max(0.001f, voxelSizeMeters);
        int sizeX = Mathf.Max(1, Mathf.CeilToInt(roomBounds.size.x / voxelSize));
        int sizeY = Mathf.Max(1, Mathf.CeilToInt(roomBounds.size.y / voxelSize));
        int sizeZ = Mathf.Max(1, Mathf.CeilToInt(roomBounds.size.z / voxelSize));

        occupancy = new byte[sizeX * sizeY * sizeZ];

        for (int i = 0; i < voxelBounds.Count; i++)
        {
            RasterizeBounds(voxelBounds[i], roomBounds, voxelSize, sizeX, sizeY, sizeZ, occupancy);
        }

        int occupied = 0;
        for (int i = 0; i < occupancy.Length; i++)
        {
            if (occupancy[i] != 0)
            {
                occupied++;
            }
        }

        return new SimpleVoxelMetadataExport
        {
            fileName = fileName,
            sizeX = sizeX,
            sizeY = sizeY,
            sizeZ = sizeZ,
            origin = roomBounds.min,
            voxelSize = Vector3.one * voxelSize,
            occupiedVoxels = occupied,
            propsOnly = propsOnly,
        };
    }

    private void CollectBounds(Transform root, List<Bounds> results)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            results.Add(renderer.bounds);
        }

        if (renderers.Length > 0)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            results.Add(collider.bounds);
        }
    }

    private void RasterizeBounds(Bounds bounds, Bounds roomBounds, float voxelSize, int sizeX, int sizeY, int sizeZ, byte[] occupancy)
    {
        if (bounds.size == Vector3.zero)
        {
            return;
        }

        int minX = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x - roomBounds.min.x) / voxelSize), 0, sizeX - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt((bounds.min.y - roomBounds.min.y) / voxelSize), 0, sizeY - 1);
        int minZ = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z - roomBounds.min.z) / voxelSize), 0, sizeZ - 1);

        int maxX = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x - roomBounds.min.x) / voxelSize) - 1, 0, sizeX - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt((bounds.max.y - roomBounds.min.y) / voxelSize) - 1, 0, sizeY - 1);
        int maxZ = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z - roomBounds.min.z) / voxelSize) - 1, 0, sizeZ - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    occupancy[x + (sizeX * (z + (sizeZ * y)))] = 1;
                }
            }
        }
    }

    private static string ExtractPropId(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return "prop";
        }

        int secondUnderscore = objectName.IndexOf('_');
        if (secondUnderscore >= 0)
        {
            secondUnderscore = objectName.IndexOf('_', secondUnderscore + 1);
        }

        return secondUnderscore >= 0 && secondUnderscore + 1 < objectName.Length
            ? objectName.Substring(secondUnderscore + 1)
            : objectName;
    }

    private static string InferSurfaceLabel(string propId)
    {
        switch (propId)
        {
            case "ceiling_short":
            case "ceiling_long":
                return "Ceiling";
            case "table_block":
                return "Table";
            default:
                return "Floor";
        }
    }

    private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
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

        if (sideRgbCamera == null)
        {
            Transform child = transform.Find(DefaultSideCameraName);
            if (child != null)
            {
                sideRgbCamera = child.GetComponent<Camera>();
            }

            if (sideRgbCamera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] != null && cameras[i].name == DefaultSideCameraName)
                    {
                        sideRgbCamera = cameras[i];
                        break;
                    }
                }
            }
        }

        if (sceneController == null)
        {
            sceneController = FindAnyObjectByType<SimpleSceneCycleController>();
        }

        if (poseController == null)
        {
            poseController = FindAnyObjectByType<RobotPoseController>();
        }

        if (propSpawner == null)
        {
            propSpawner = FindAnyObjectByType<SimplePropSpawner>();
        }

        if (forbiddenZones == null)
        {
            forbiddenZones = FindAnyObjectByType<SimpleForbiddenZones>();
        }

        if (linearDepthShader == null)
        {
            linearDepthShader = Shader.Find("Hidden/DepthWriter");
        }

        if (prototypeDepthShader == null)
        {
            prototypeDepthShader = Shader.Find("Hidden/SimpleTrueDepthPrototype");
        }
    }

    private void OnDestroy()
    {
        if (linearDepthMaterial != null)
        {
            Destroy(linearDepthMaterial);
            linearDepthMaterial = null;
        }

        if (prototypeDepthMaterial != null)
        {
            Destroy(prototypeDepthMaterial);
            prototypeDepthMaterial = null;
        }
    }
}
