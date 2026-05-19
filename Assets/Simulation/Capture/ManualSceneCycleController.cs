using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Generates one new runtime scene state per Space press in Play Mode.
/// Bulk export stays out of scope until the generated motion looks believable.
/// </summary>
[DisallowMultipleComponent]
public class ManualSceneCycleController : MonoBehaviour
{
    [Header("Dependencies")]
    public RobotPoseWorkflow robotPoseWorkflow;
    public RobotOverlapDetector overlapDetector;
    public PropSpawner propSpawner;
    public SampleCapturePipeline sampleCapturePipeline;

    [Header("Runtime Control")]
    public bool generateInitialSceneOnStart = false;
    public bool logSceneSummary = true;
    public int currentSampleIndex = -1;
    public int baseRobotSeed = 1000;
    public int basePropSeed = 2000;

    [Header("Pose Profile")]
    public RobotPoseProfile playModeProfile = RobotPoseProfile.FreeRoomSnapshot;

    [Header("Observed State")]
    [SerializeField] private bool generationInProgress;
    [SerializeField] private string lastPoseProfile = "";
    [SerializeField] private int lastRobotSeed = -1;
    [SerializeField] private int lastPropSeed = -1;
    [SerializeField] private bool lastPoseValid;
    [SerializeField] private int lastPlacedProps;
    [SerializeField] private int lastRejectedProps;
    [SerializeField] [TextArea(4, 12)] private string lastSceneSummary = "";

    public SampleMetadataExport LastBuiltSampleMetadata
    {
        get { return sampleCapturePipeline != null ? sampleCapturePipeline.LastBuiltSampleMetadata : null; }
    }

    private void Awake()
    {
        AutoResolveDependencies();
    }

    private void OnValidate()
    {
        AutoResolveDependencies();
    }

    private void Start()
    {
        if (Application.isPlaying && generateInitialSceneOnStart)
        {
            GenerateNextScene();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || generationInProgress)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            GenerateNextScene();
        }
    }

    [ContextMenu("Generate Next Scene")]
    public void GenerateNextScene()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("ManualSceneCycleController: Space stepping is Play Mode only.");
            return;
        }

        if (generationInProgress)
        {
            return;
        }

        if (robotPoseWorkflow == null || propSpawner == null || sampleCapturePipeline == null)
        {
            AutoResolveDependencies();
        }

        if (robotPoseWorkflow == null || propSpawner == null || sampleCapturePipeline == null)
        {
            Debug.LogWarning("ManualSceneCycleController: Assign workflow, spawner, and capture pipeline.");
            return;
        }

        StartCoroutine(GenerateNextSceneRoutine());
    }

    private System.Collections.IEnumerator GenerateNextSceneRoutine()
    {
        generationInProgress = true;
        int candidateSampleIndex = Mathf.Max(0, currentSampleIndex + 1);

        RobotPoseProfile profile = playModeProfile;
        int robotSeed = baseRobotSeed + candidateSampleIndex;
        int propSeed = basePropSeed + candidateSampleIndex;

        bool poseFinished = false;
        RobotPoseTarget poseTarget = null;
        RobotPoseValidationReport validationReport = null;

        robotPoseWorkflow.ApplySampledPose(profile, robotSeed, false, (target, report) =>
        {
            poseTarget = target;
            validationReport = report;
            poseFinished = true;
        });

        while (!poseFinished)
        {
            yield return null;
        }

        if (poseTarget == null || validationReport == null)
        {
            currentSampleIndex = candidateSampleIndex;
            lastPoseProfile = profile.ToString();
            lastRobotSeed = robotSeed;
            lastPropSeed = propSeed;
            lastPoseValid = false;
            lastPlacedProps = 0;
            lastRejectedProps = 0;

            bool propsRefreshed = false;
            string rejectionReason = "pose_generation_failed";
            string tableSummary = "";

            if (overlapDetector != null && overlapDetector.ValidateRobotAgainstTable(out tableSummary))
            {
                SceneRandomizationReport refreshReport = propSpawner.SpawnPreviewProps(propSeed);
                lastPlacedProps = refreshReport != null ? refreshReport.placedProps : 0;
                lastRejectedProps = refreshReport != null ? refreshReport.rejectedProps : 0;
                propsRefreshed = refreshReport != null;
            }
            else
            {
                if (overlapDetector != null)
                {
                    rejectionReason += " " + tableSummary;
                }

                propSpawner.ClearSpawnedProps();
            }

            lastSceneSummary =
                "sample=" + candidateSampleIndex.ToString("D4") +
                " profile=" + profile +
                " robotSeed=" + robotSeed +
                " propSeed=" + propSeed +
                " status=rejected reason=" + rejectionReason +
                " propsRefreshed=" + propsRefreshed +
                " propsPlaced=" + lastPlacedProps +
                " propsRejected=" + lastRejectedProps;

            if (logSceneSummary)
            {
                Debug.LogWarning(lastSceneSummary);
            }

            generationInProgress = false;
            yield break;
        }

        currentSampleIndex = candidateSampleIndex;
        SceneRandomizationReport randomizationReport = propSpawner.SpawnPreviewProps(propSeed);

        if (overlapDetector != null && !overlapDetector.ValidateRobotAgainstScene(out string overlapSummary))
        {
            propSpawner.ClearSpawnedProps();
            lastPoseProfile = profile.ToString();
            lastRobotSeed = robotSeed;
            lastPropSeed = propSeed;
            lastPoseValid = false;
            lastPlacedProps = 0;
            lastRejectedProps = randomizationReport != null ? randomizationReport.rejectedProps : 0;
            lastSceneSummary =
                "sample=" + currentSampleIndex.ToString("D4") +
                " profile=" + lastPoseProfile +
                " robotSeed=" + lastRobotSeed +
                " propSeed=" + lastPropSeed +
                " status=rejected reason=" + overlapSummary;

            if (logSceneSummary)
            {
                Debug.LogWarning(lastSceneSummary);
            }

            generationInProgress = false;
            yield break;
        }

        SampleMetadataExport sample = sampleCapturePipeline.RebuildCurrentSampleState(
            currentSampleIndex,
            poseTarget,
            validationReport,
            randomizationReport,
            robotSeed,
            propSeed);

        lastPoseProfile = profile.ToString();
        lastRobotSeed = robotSeed;
        lastPropSeed = propSeed;
        lastPoseValid = true;
        lastPlacedProps = randomizationReport != null ? randomizationReport.placedProps : 0;
        lastRejectedProps = randomizationReport != null ? randomizationReport.rejectedProps : 0;
        lastSceneSummary =
            "sample=" + currentSampleIndex.ToString("D4") +
            " profile=" + lastPoseProfile +
            " robotSeed=" + lastRobotSeed +
            " propSeed=" + lastPropSeed +
            " poseValid=" + lastPoseValid +
            " propsPlaced=" + lastPlacedProps +
            " propsRejected=" + lastRejectedProps +
            " cameras=" + (sample != null && sample.depth != null ? sample.depth.cameras.Count : 0);

        if (logSceneSummary)
        {
            Debug.Log(lastSceneSummary);
        }

        generationInProgress = false;
    }

    private void AutoResolveDependencies()
    {
        if (robotPoseWorkflow == null)
        {
            robotPoseWorkflow = FindAnyObjectByType<RobotPoseWorkflow>();
        }

        if (overlapDetector == null)
        {
            overlapDetector = FindAnyObjectByType<RobotOverlapDetector>();
        }

        if (propSpawner == null)
        {
            propSpawner = FindAnyObjectByType<PropSpawner>();
        }

        if (sampleCapturePipeline == null)
        {
            sampleCapturePipeline = GetComponent<SampleCapturePipeline>();
            if (sampleCapturePipeline == null)
            {
                sampleCapturePipeline = FindAnyObjectByType<SampleCapturePipeline>();
            }
        }
    }
}
