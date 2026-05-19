using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Minimal glue layer for sampling, applying, and validating robot poses.
/// Export and capture code should depend on this workflow instead of reimplementing
/// robot-motion sequencing themselves.
/// </summary>
[DisallowMultipleComponent]
public class RobotPoseWorkflow : MonoBehaviour
{
    [Header("Dependencies")]
    public RobotPoseController controller;
    public RobotPoseRandomizer randomizer;
    public RobotPoseValidator validator;
    public RobotTableAvoidance tableAvoidance;
    public RobotOverlapDetector overlapDetector;

    [Header("Preview")]
    public RobotPoseProfile activeProfile = RobotPoseProfile.FreeRoomSnapshot;
    public int activeSeed = 1000;
    public bool incrementSeedAfterApply = true;
    public bool applyActivePoseOnStart = false;

    public RobotPoseTarget LastSampledTarget { get; private set; }
    public RobotPoseValidationReport LastValidationReport { get; private set; }
    public bool IsApplyingPose { get; private set; }

    [Header("Runtime Debug")]
    [SerializeField] private string lastAppliedProfile = "None";
    [SerializeField] private bool lastValidationWasValid;
    [SerializeField] private float lastValidationMaxError;
    [SerializeField] [TextArea(2, 8)] private string lastTableAvoidanceSummary = "";
    [SerializeField] [TextArea(2, 8)] private string lastRejectionReason = "";

    private void Start()
    {
        AutoResolveDependencies();

        if (Application.isPlaying && applyActivePoseOnStart)
        {
            SampleAndApplyActivePose();
        }
    }

    private void OnValidate()
    {
        AutoResolveDependencies();
    }

    [ContextMenu("Sample And Apply Active Pose")]
    public void SampleAndApplyActivePose()
    {
        ApplySampledPose(activeProfile, activeSeed, incrementSeedAfterApply);
    }

    public void ApplySampledPose(
        RobotPoseProfile profile,
        int seed,
        bool incrementSeed,
        Action<RobotPoseTarget, RobotPoseValidationReport> onComplete = null)
    {
        AutoResolveDependencies();

        if (controller == null || randomizer == null || validator == null)
        {
            Debug.LogWarning("RobotPoseWorkflow: Assign controller, randomizer, and validator first.");
            onComplete?.Invoke(null, null);
            return;
        }

        if (IsApplyingPose)
        {
            Debug.LogWarning("RobotPoseWorkflow: Pose application already in progress.");
            onComplete?.Invoke(null, LastValidationReport);
            return;
        }

        if (Application.isPlaying)
        {
            StartCoroutine(ApplySinglePlayModePose(profile, seed, incrementSeed, onComplete));
            return;
        }

        RobotPoseTarget sampled = randomizer.SamplePose(profile, seed);
        if (tableAvoidance != null)
        {
            sampled = tableAvoidance.BuildTableAwarePose(sampled, profile, seed, out lastTableAvoidanceSummary);
        }
        else
        {
            lastTableAvoidanceSummary = "table_avoidance_not_assigned";
        }

        BeginApplyPose(sampled, incrementSeed, onComplete);
    }

    [ContextMenu("Apply Parked Pose")]
    public void ApplyParkedPose()
    {
        if (controller == null)
        {
            Debug.LogWarning("RobotPoseWorkflow: Assign controller first.");
            return;
        }

        BeginApplyPose(controller.BuildParkedPose(), false, null);
    }

    public void ApplyExplicitPose(
        RobotPoseTarget target,
        Action<RobotPoseTarget, RobotPoseValidationReport> onComplete = null)
    {
        BeginApplyPose(target, false, onComplete);
    }

    private IEnumerator ApplySinglePlayModePose(
        RobotPoseProfile profile,
        int seed,
        bool incrementSeed,
        Action<RobotPoseTarget, RobotPoseValidationReport> onComplete)
    {
        IsApplyingPose = true;
        lastRejectionReason = "";

        RobotPoseTarget candidate = randomizer.SamplePose(profile, seed);
        if (tableAvoidance != null)
        {
            candidate = tableAvoidance.BuildTableAwarePose(candidate, profile, seed, out lastTableAvoidanceSummary);
        }
        else
        {
            lastTableAvoidanceSummary = "table_avoidance_not_assigned";
        }

        string error;
        if (!controller.TryApplyPoseTarget(candidate, out error))
        {
            IsApplyingPose = false;
            lastRejectionReason = "apply_failed: " + error;
            onComplete?.Invoke(null, null);
            yield break;
        }

        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForFixedUpdate();
        }

        RobotPoseValidationReport report = validator != null ? validator.Validate(candidate) : new RobotPoseValidationReport();

        string clearanceSummary = "clearance_not_checked";
        bool poseIsSafe =
            overlapDetector != null
                ? overlapDetector.ValidateRobotAgainstTable(out clearanceSummary)
                : (tableAvoidance == null || tableAvoidance.ValidateCurrentPoseClearance(out clearanceSummary));

        if (!poseIsSafe)
        {
            lastRejectionReason = clearanceSummary;
            IsApplyingPose = false;
            onComplete?.Invoke(null, null);
            yield break;
        }

        LastSampledTarget = candidate;
        LastValidationReport = report;
        lastAppliedProfile = candidate.profileName;
        lastValidationWasValid = report != null && report.isValid;
        lastValidationMaxError = report != null ? report.maxError : 0f;

        if (incrementSeed)
        {
            activeSeed = seed + 1;
        }

        IsApplyingPose = false;
        onComplete?.Invoke(candidate, report);
    }

    private void BeginApplyPose(
        RobotPoseTarget target,
        bool incrementSeed,
        Action<RobotPoseTarget, RobotPoseValidationReport> onComplete)
    {
        if (target == null || controller == null)
        {
            onComplete?.Invoke(target, null);
            return;
        }

        if (IsApplyingPose)
        {
            Debug.LogWarning("RobotPoseWorkflow: Pose application already in progress.");
            onComplete?.Invoke(target, LastValidationReport);
            return;
        }

        string error;
        if (!controller.TryApplyPoseTarget(target, out error))
        {
            Debug.LogError("RobotPoseWorkflow: " + error);
            onComplete?.Invoke(target, null);
            return;
        }

        LastSampledTarget = target;
        lastAppliedProfile = target.profileName;

        if (incrementSeed)
        {
            activeSeed++;
        }

        if (Application.isPlaying && validator != null)
        {
            StartCoroutine(ValidateAfterApply(target, onComplete));
            return;
        }

        LastValidationReport = validator != null ? validator.Validate(target) : null;
        if (LastValidationReport != null)
        {
            lastValidationWasValid = LastValidationReport.isValid;
            lastValidationMaxError = LastValidationReport.maxError;
        }

        onComplete?.Invoke(target, LastValidationReport);
    }

    private IEnumerator ValidateAfterApply(
        RobotPoseTarget target,
        Action<RobotPoseTarget, RobotPoseValidationReport> onComplete)
    {
        IsApplyingPose = true;

        yield return validator.WaitForValidation(target, report =>
        {
            OnValidationComplete(report);
            onComplete?.Invoke(target, report);
        });

        IsApplyingPose = false;
    }

    private void OnValidationComplete(RobotPoseValidationReport report)
    {
        if (report == null)
        {
            return;
        }

        LastValidationReport = report;
        lastValidationWasValid = report.isValid;
        lastValidationMaxError = report.maxError;
        Debug.Log(
            "RobotPoseWorkflow: Validation for " + report.profileName +
            " => valid=" + report.isValid +
            ", matched=" + report.matchedJointCount +
            ", maxError=" + report.maxError.ToString("F4"));
    }

    private void AutoResolveDependencies()
    {
        if (controller == null)
        {
            controller = GetComponent<RobotPoseController>();
            if (controller == null)
            {
                controller = FindAnyObjectByType<RobotPoseController>();
            }
        }

        if (randomizer == null)
        {
            randomizer = GetComponent<RobotPoseRandomizer>();
            if (randomizer == null)
            {
                randomizer = FindAnyObjectByType<RobotPoseRandomizer>();
            }
        }

        if (validator == null)
        {
            validator = GetComponent<RobotPoseValidator>();
            if (validator == null)
            {
                validator = FindAnyObjectByType<RobotPoseValidator>();
            }
        }

        if (tableAvoidance == null)
        {
            tableAvoidance = GetComponent<RobotTableAvoidance>();
            if (tableAvoidance == null)
            {
                tableAvoidance = FindAnyObjectByType<RobotTableAvoidance>();
            }
        }

        if (overlapDetector == null)
        {
            overlapDetector = GetComponent<RobotOverlapDetector>();
            if (overlapDetector == null)
            {
                overlapDetector = FindAnyObjectByType<RobotOverlapDetector>();
            }
        }
    }
}
