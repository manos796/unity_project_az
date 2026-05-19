using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the fixed 4-camera rig used by the legacy Python reconstruction flow.
/// Keep camera names and ordering stable unless the Python side is versioned.
/// </summary>
[DisallowMultipleComponent]
public class FixedSensorRig : MonoBehaviour
{
    public enum ResolutionPreset
    {
        FullHD = 0,
        UHD4K = 1,
        Custom = 2,
        LUCID = 3,
    }

    private struct SlotDefinition
    {
        public string name;
        public Vector3 position;
        public Vector3 eulerRotation;

        public SlotDefinition(string name, Vector3 position, Vector3 eulerRotation)
        {
            this.name = name;
            this.position = position;
            this.eulerRotation = eulerRotation;
        }
    }

    private static readonly SlotDefinition[] SlotDefinitions =
    {
        new SlotDefinition("DepthCam_BL", new Vector3(-4.5f, 1.35f, -2.5f), new Vector3(10.4626548f, 60.9453964f, 0f)),
        new SlotDefinition("DepthCam_BR", new Vector3( 4.5f, 1.35f, -2.5f), new Vector3(10.4626548f, 299.054596f, 0f)),
        new SlotDefinition("DepthCam_FL", new Vector3(-4.5f, 1.35f,  2.5f), new Vector3(10.4626548f, 119.054611f, 0f)),
        new SlotDefinition("DepthCam_FR", new Vector3( 4.5f, 1.35f,  2.5f), new Vector3(10.4626548f, 240.945389f, 0f)),
    };

    [Header("Camera Settings")]
    public ResolutionPreset resolutionPreset = ResolutionPreset.UHD4K;
    public int width = 3840;
    public int height = 2160;
    public float nearClip = 0.1f;
    public float farClip = 15f;
    public float horizontalFovDegrees = -1f;
    public float verticalFovDegrees = 70f;
    public bool camerasEnabledInGameView = false;

    private void Start()
    {
        ApplyResolutionPreset();
        ApplyConfigurationToChildren();
    }

    private void OnValidate()
    {
        ApplyResolutionPreset();
        ApplyConfigurationToChildren();
    }

    private void ApplyResolutionPreset()
    {
        switch (resolutionPreset)
        {
            case ResolutionPreset.FullHD:
                width = 1920;
                height = 1080;
                verticalFovDegrees = 70f;
                horizontalFovDegrees = CalculateHorizontalFov(verticalFovDegrees, width, height);
                break;
            case ResolutionPreset.UHD4K:
                width = 3840;
                height = 2160;
                verticalFovDegrees = 70f;
                horizontalFovDegrees = CalculateHorizontalFov(verticalFovDegrees, width, height);
                break;
            case ResolutionPreset.LUCID:
                width = 640;
                height = 480;
                horizontalFovDegrees = 108f;
                verticalFovDegrees = 78f;
                break;
            case ResolutionPreset.Custom:
                width = Mathf.Max(1, width);
                height = Mathf.Max(1, height);
                verticalFovDegrees = Mathf.Clamp(verticalFovDegrees, 1f, 179f);
                horizontalFovDegrees = horizontalFovDegrees > 0f
                    ? Mathf.Clamp(horizontalFovDegrees, 1f, 179f)
                    : CalculateHorizontalFov(verticalFovDegrees, width, height);
                break;
        }
    }

    public Camera[] GetOrderedCameras()
    {
        List<Camera> cameras = new List<Camera>();

        for (int i = 0; i < SlotDefinitions.Length; i++)
        {
            Transform child = transform.Find(SlotDefinitions[i].name);
            if (child == null)
            {
                continue;
            }

            Camera cameraComponent = child.GetComponent<Camera>();
            if (cameraComponent != null)
            {
                cameras.Add(cameraComponent);
            }
        }

        return cameras.ToArray();
    }

    [ContextMenu("Apply Configuration To Child Cameras")]
    public void ApplyConfigurationToChildren()
    {
        ApplyResolutionPreset();

        for (int i = 0; i < SlotDefinitions.Length; i++)
        {
            Transform child = transform.Find(SlotDefinitions[i].name);
            if (child == null)
            {
                continue;
            }

            child.localPosition = SlotDefinitions[i].position;
            child.localEulerAngles = SlotDefinitions[i].eulerRotation;

            Camera cameraComponent = child.GetComponent<Camera>();
            if (cameraComponent != null)
            {
                ConfigureCamera(cameraComponent);
            }
        }
    }

    public void ConfigureCamera(Camera cameraComponent)
    {
        if (cameraComponent == null)
        {
            return;
        }

        cameraComponent.enabled = camerasEnabledInGameView;
        cameraComponent.nearClipPlane = nearClip;
        cameraComponent.farClipPlane = farClip;
        cameraComponent.fieldOfView = verticalFovDegrees;
        cameraComponent.ResetProjectionMatrix();
        cameraComponent.projectionMatrix = BuildProjectionMatrix(nearClip, farClip, horizontalFovDegrees, verticalFovDegrees);
        cameraComponent.allowHDR = false;
        cameraComponent.allowMSAA = false;
        cameraComponent.depthTextureMode = DepthTextureMode.Depth;
    }

    public DepthMetadataExport BuildDepthMetadata(int sampleIndex, string sampleName, string timestampUtc)
    {
        DepthMetadataExport export = new DepthMetadataExport();
        export.sampleIndex = sampleIndex;
        export.sampleName = sampleName;
        export.timestampUtc = timestampUtc;
        export.width = width;
        export.height = height;
        export.nearClip = nearClip;
        export.farClip = farClip;
        export.fovDegrees = verticalFovDegrees;
        export.horizontalFovDegrees = horizontalFovDegrees;
        export.verticalFovDegrees = verticalFovDegrees;

        Camera[] cameras = GetOrderedCameras();
        for (int i = 0; i < cameras.Length; i++)
        {
            export.fullDepthRawFiles.Add("cam" + i + "_depth.raw");
            export.fullDepthPreviewFiles.Add("cam" + i + "_depth_vis.png");
            export.cameras.Add(BuildCameraMetadata(i, cameras[i]));
        }

        return export;
    }

    public RgbMetadataExport BuildRgbMetadata(int sampleIndex, string sampleName, string timestampUtc)
    {
        RgbMetadataExport export = new RgbMetadataExport();
        export.sampleIndex = sampleIndex;
        export.sampleName = sampleName;
        export.timestampUtc = timestampUtc;
        export.width = width;
        export.height = height;

        Camera[] cameras = GetOrderedCameras();
        for (int i = 0; i < cameras.Length; i++)
        {
            export.rgbFiles.Add("cam" + i + "_rgb.png");
            export.cameras.Add(BuildCameraMetadata(i, cameras[i]));
        }

        return export;
    }

    public RgbMetadataExport BuildSingleRgbMetadata(
        int sampleIndex,
        string sampleName,
        string timestampUtc,
        Camera cameraComponent,
        string fileName)
    {
        RgbMetadataExport export = new RgbMetadataExport();
        export.sampleIndex = sampleIndex;
        export.sampleName = sampleName;
        export.timestampUtc = timestampUtc;
        export.width = width;
        export.height = height;

        if (cameraComponent != null)
        {
            export.rgbFiles.Add(fileName);
            export.cameras.Add(BuildCameraMetadata(0, cameraComponent));
        }

        return export;
    }

    public SensorCameraMetadata BuildMetadataForCamera(Camera cameraComponent, int index)
    {
        if (cameraComponent == null)
        {
            return null;
        }

        return BuildCameraMetadata(index, cameraComponent);
    }

    private SensorCameraMetadata BuildCameraMetadata(int index, Camera cameraComponent)
    {
        SensorCameraMetadata metadata = new SensorCameraMetadata();
        metadata.index = index;
        metadata.name = cameraComponent.name;
        metadata.position = cameraComponent.transform.position;
        metadata.eulerRotation = cameraComponent.transform.eulerAngles;
        metadata.forward = cameraComponent.transform.forward;
        metadata.nearClip = cameraComponent.nearClipPlane;
        metadata.farClip = cameraComponent.farClipPlane;
        metadata.verticalFovDegrees = cameraComponent.fieldOfView;
        metadata.horizontalFovDegrees = horizontalFovDegrees;
        metadata.fy = (height * 0.5f) / Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad);
        metadata.fx = (width * 0.5f) / Mathf.Tan(horizontalFovDegrees * 0.5f * Mathf.Deg2Rad);
        metadata.cx = (width - 1) * 0.5f;
        metadata.cy = (height - 1) * 0.5f;
        metadata.worldToCameraMatrix = FlattenMatrix(cameraComponent.worldToCameraMatrix);
        metadata.cameraToWorldMatrix = FlattenMatrix(cameraComponent.cameraToWorldMatrix);
        metadata.projectionMatrix = FlattenMatrix(cameraComponent.projectionMatrix);
        metadata.inverseProjectionMatrix = FlattenMatrix(cameraComponent.projectionMatrix.inverse);
        return metadata;
    }

    private static float CalculateHorizontalFov(float verticalFovDegrees, int width, int height)
    {
        float aspect = height > 0 ? (float)width / height : 1f;
        float verticalRadians = verticalFovDegrees * Mathf.Deg2Rad;
        return 2f * Mathf.Atan(Mathf.Tan(verticalRadians * 0.5f) * aspect) * Mathf.Rad2Deg;
    }

    private static Matrix4x4 BuildProjectionMatrix(float nearClip, float farClip, float horizontalFovDegrees, float verticalFovDegrees)
    {
        float halfWidth = Mathf.Tan(horizontalFovDegrees * 0.5f * Mathf.Deg2Rad) * nearClip;
        float halfHeight = Mathf.Tan(verticalFovDegrees * 0.5f * Mathf.Deg2Rad) * nearClip;
        return Matrix4x4.Frustum(-halfWidth, halfWidth, -halfHeight, halfHeight, nearClip, farClip);
    }

    private static List<float> FlattenMatrix(Matrix4x4 matrix)
    {
        List<float> values = new List<float>(16);
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                values.Add(matrix[row, column]);
            }
        }
        return values;
    }
}
