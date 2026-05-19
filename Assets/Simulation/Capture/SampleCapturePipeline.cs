using System;
using UnityEngine;

/// <summary>
/// Aggregates sample metadata from the scene without making export format
/// decisions in multiple places. File-writing and image capture can grow here
/// later while preserving the metadata contract built today.
/// </summary>
[DisallowMultipleComponent]
public class SampleCapturePipeline : MonoBehaviour
{
    [Header("Dependencies")]
    public FixedSensorRig sensorRig;
    public RobotPoseController robotPoseController;
    public PropSpawner propSpawner;

    [Header("Preview")]
    public int previewSampleIndex = 0;

    public SampleMetadataExport LastBuiltSampleMetadata { get; private set; }

    [Header("Runtime Debug")]
    [SerializeField] private string lastBuiltSampleName = "";
    [SerializeField] private string lastBuiltPoseProfile = "";
    [SerializeField] private int lastRobotSeed = -1;
    [SerializeField] private int lastPropSeed = -1;

    private void Awake()
    {
        AutoResolveDependencies();
    }

    private void OnValidate()
    {
        AutoResolveDependencies();
    }

    public SampleMetadataExport BuildSampleMetadata(int sampleIndex)
    {
        return RebuildCurrentSampleState(sampleIndex, null, null, null, -1, -1);
    }

    public SampleMetadataExport RebuildCurrentSampleState(
        int sampleIndex,
        RobotPoseTarget poseTarget,
        RobotPoseValidationReport validationReport,
        SceneRandomizationReport randomizationReport,
        int robotSeed,
        int propSeed)
    {
        AutoResolveDependencies();

        string sampleName = "sample_" + sampleIndex.ToString("D4");
        string timestampUtc = DateTime.UtcNow.ToString("O");

        SampleMetadataExport export = new SampleMetadataExport();
        export.sampleIndex = sampleIndex;
        export.sampleName = sampleName;
        export.timestampUtc = timestampUtc;
        export.poseProfile = poseTarget != null ? poseTarget.profileName : "Unknown";
        export.robotSeed = robotSeed;
        export.propSeed = propSeed;
        export.depth = sensorRig != null ? sensorRig.BuildDepthMetadata(sampleIndex, sampleName, timestampUtc) : null;
        export.rgb = sensorRig != null ? sensorRig.BuildRgbMetadata(sampleIndex, sampleName, timestampUtc) : null;
        export.robot = robotPoseController != null ? robotPoseController.CaptureLegacyPoseExport() : null;
        export.robotState = robotPoseController != null ? robotPoseController.CaptureCanonicalStateExport() : null;
        export.robotValidation = validationReport;
        export.randomization = randomizationReport != null ? randomizationReport : (propSpawner != null ? propSpawner.LastReport : null);
        export.sceneObjects = propSpawner != null ? propSpawner.CaptureSceneObjectsExport() : null;

        LastBuiltSampleMetadata = export;
        lastBuiltSampleName = export.sampleName;
        lastBuiltPoseProfile = export.poseProfile;
        lastRobotSeed = robotSeed;
        lastPropSeed = propSeed;
        return export;
    }

    [ContextMenu("Log Preview Sample Metadata")]
    public void LogPreviewSampleMetadata()
    {
        SampleMetadataExport export = BuildSampleMetadata(previewSampleIndex);
        Debug.Log(JsonUtility.ToJson(export, true));
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

        if (robotPoseController == null)
        {
            robotPoseController = FindAnyObjectByType<RobotPoseController>();
        }

        if (propSpawner == null)
        {
            propSpawner = FindAnyObjectByType<PropSpawner>();
        }
    }
}
