using UnityEngine;

/// <summary>
/// Drives one joint at a time from a parked baseline so the imported
/// articulation behavior can be audited without guessing semantics.
/// </summary>
[DisallowMultipleComponent]
public class RobotJointAuditTool : MonoBehaviour
{
    [Header("Dependencies")]
    public RobotPoseController controller;
    public RobotPoseValidator validator;

    [Header("Audit Input")]
    public RobotArticulationJointId selectedJoint = RobotArticulationJointId.Z1Rot;
    public float selectedTarget = 15f;
    public bool logAuditStateOnApply = true;

    [Header("Observed State")]
    [SerializeField] private string selectedJointName = "";
    [SerializeField] private string semanticRole = "";
    [SerializeField] private string unit = "";
    [SerializeField] private float conservativeMin;
    [SerializeField] private float conservativeMax;
    [SerializeField] private float measuredValue;
    [SerializeField] private float currentDriveTarget;
    [SerializeField] private float absoluteError;
    [SerializeField] private Vector3 affectedLinkPosition;
    [SerializeField] private Vector3 affectedLinkEulerRotation;

    private void Awake()
    {
        AutoResolveDependencies();
        RefreshObservedState();
    }

    private void OnValidate()
    {
        AutoResolveDependencies();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            RefreshObservedState();
        }
    }

    [ContextMenu("Apply Selected Joint From Parked")]
    public void ApplySelectedJointFromParked()
    {
        if (controller == null)
        {
            Debug.LogWarning("RobotJointAuditTool: Assign controller first.");
            return;
        }

        RobotPoseController.JointDefinition definition;
        if (!controller.TryGetJointDefinition(selectedJoint, out definition))
        {
            Debug.LogWarning("RobotJointAuditTool: Missing definition for " + selectedJoint);
            return;
        }

        RobotPoseTarget target = controller.BuildParkedPose();
        for (int i = 0; i < target.joints.Count; i++)
        {
            if (target.joints[i].jointName != definition.jointName)
            {
                continue;
            }

            target.joints[i].target = Mathf.Clamp(selectedTarget, definition.conservativeMin, definition.conservativeMax);
            target.joints[i].semanticRole = definition.semanticRole.ToString();
            target.joints[i].unit = definition.exportUnit == RobotJointUnit.Meters ? "meters" : "degrees";
            break;
        }

        string error;
        if (!controller.TryApplyPoseTarget(target, out error))
        {
            Debug.LogError("RobotJointAuditTool: " + error);
            return;
        }

        RefreshObservedState();

        if (validator != null)
        {
            RobotPoseValidationReport report = validator.Validate(target);
            absoluteError = report.maxError;
        }

        if (logAuditStateOnApply)
        {
            Debug.Log(
                "RobotJointAuditTool: Applied " + definition.jointName +
                " (" + definition.semanticRole + ") target=" + selectedTarget.ToString("F2") +
                " " + (definition.exportUnit == RobotJointUnit.Meters ? "m" : "deg") +
                ", measured=" + measuredValue.ToString("F2"));
        }
    }

    [ContextMenu("Refresh Observed State")]
    public void RefreshObservedState()
    {
        AutoResolveDependencies();

        if (controller == null)
        {
            return;
        }

        RobotPoseController.JointDefinition definition;
        if (!controller.TryGetJointDefinition(selectedJoint, out definition))
        {
            return;
        }

        selectedJointName = definition.jointName;
        semanticRole = definition.semanticRole.ToString();
        unit = definition.exportUnit == RobotJointUnit.Meters ? "meters" : "degrees";
        conservativeMin = definition.conservativeMin;
        conservativeMax = definition.conservativeMax;

        controller.TryGetMeasuredJointPosition(definition.jointName, out measuredValue);
        controller.TryGetDriveTarget(definition.jointName, out currentDriveTarget);

        Transform jointTransform;
        if (controller.TryGetJointTransform(definition.jointName, out jointTransform) && jointTransform != null)
        {
            affectedLinkPosition = jointTransform.position;
            affectedLinkEulerRotation = jointTransform.eulerAngles;
        }

        absoluteError = Mathf.Abs(measuredValue - selectedTarget);
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

        if (validator == null)
        {
            validator = GetComponent<RobotPoseValidator>();
            if (validator == null)
            {
                validator = FindAnyObjectByType<RobotPoseValidator>();
            }
        }
    }
}
