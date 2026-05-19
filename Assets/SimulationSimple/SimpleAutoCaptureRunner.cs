using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SimpleAutoCaptureRunner : MonoBehaviour
{
    [Header("Dependencies")]
    public SimpleSceneCycleController sceneController;
    public SimpleSampleExporter sampleExporter;

    [Header("Automation")]
    public bool runOnStart = false;
    public int targetAcceptedSamples = 5;
    public int settlingFixedUpdatesBeforeExport = 18;
    public float exportDelaySeconds = 0.05f;
    public bool logProgress = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool runInProgress;
    [SerializeField] private int attemptedScenes;
    [SerializeField] private int exportedSamples;
    [SerializeField] private string lastRunSummary = "";

    public bool RunInProgress
    {
        get { return runInProgress; }
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
        if (Application.isPlaying && runOnStart)
        {
            StartAcceptedCaptureBatch();
        }
    }

    [ContextMenu("Start Accepted Capture Batch")]
    public void StartAcceptedCaptureBatch()
    {
        if (!Application.isPlaying || runInProgress)
        {
            return;
        }

        AutoResolveDependencies();
        StartCoroutine(RunAcceptedCaptureBatchRoutine());
    }

    private IEnumerator RunAcceptedCaptureBatchRoutine()
    {
        runInProgress = true;
        attemptedScenes = 0;
        exportedSamples = 0;
        lastRunSummary = "";

        while (exportedSamples < Mathf.Max(1, targetAcceptedSamples))
        {
            attemptedScenes++;
            sceneController.GenerateNextScene();
            yield return new WaitUntil(() => !sceneController.IsGenerationInProgress);

            if (!sceneController.LastGenerationAccepted)
            {
                continue;
            }

            int settleFrames = Mathf.Max(0, settlingFixedUpdatesBeforeExport);
            for (int i = 0; i < settleFrames; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Physics.SyncTransforms();
            yield return new WaitForEndOfFrame();
            if (exportDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(exportDelaySeconds);
            }

            string exportSummary;
            if (!sampleExporter.TryExportAcceptedSample(sceneController.LastAcceptedSampleIndex, out exportSummary))
            {
                if (logProgress)
                {
                    Debug.LogWarning("SimpleAutoCaptureRunner: export_failed reason=" + exportSummary);
                }
                continue;
            }

            exportedSamples++;
            lastRunSummary =
                "acceptedExports=" + exportedSamples +
                " attemptedScenes=" + attemptedScenes +
                " lastSample=" + sceneController.LastAcceptedSampleIndex;

            if (logProgress)
            {
                Debug.Log("SimpleAutoCaptureRunner: " + lastRunSummary);
            }
        }

        runInProgress = false;
    }

    private void AutoResolveDependencies()
    {
        if (sceneController == null)
        {
            sceneController = FindAnyObjectByType<SimpleSceneCycleController>();
        }

        if (sampleExporter == null)
        {
            sampleExporter = GetComponent<SimpleSampleExporter>();
            if (sampleExporter == null)
            {
                sampleExporter = FindAnyObjectByType<SimpleSampleExporter>();
            }
        }
    }
}
