using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Final-state robot overlap checks against table and spawned obstacles.
/// This is intentionally separate from steering so motion diversity stays intact.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-360)]
public class RobotOverlapDetector : MonoBehaviour
{
    private readonly List<Collider> robotColliders = new List<Collider>();
    private readonly List<Collider> obstacleColliders = new List<Collider>();

    [Header("Dependencies")]
    public RobotCollisionRig collisionRig;
    public SceneObstacleRegistry obstacleRegistry;

    [Header("Observed State")]
    [SerializeField] private bool lastValidationWasSafe = true;
    [SerializeField] [TextArea(2, 8)] private string lastValidationSummary = "";

    private void Awake()
    {
        AutoResolveDependencies();
    }

    private void OnValidate()
    {
        AutoResolveDependencies();
    }

    public bool ValidateRobotAgainstTable(out string summary)
    {
        AutoResolveDependencies();
        return ValidateAgainstObstacles(CollectTableObstacles, "table", out summary);
    }

    public bool ValidateRobotAgainstScene(out string summary)
    {
        AutoResolveDependencies();

        if (!TryCollectRobotColliders())
        {
            summary = "robot_hulls_missing";
            lastValidationWasSafe = false;
            lastValidationSummary = summary;
            return false;
        }

        obstacleRegistry.CollectTableColliders(obstacleColliders);
        List<Collider> propColliders = new List<Collider>();
        obstacleRegistry.CollectSpawnedPropColliders(propColliders);
        obstacleColliders.AddRange(propColliders);

        return CompleteValidation("scene", out summary);
    }

    private bool ValidateAgainstObstacles(System.Action collectObstacles, string label, out string summary)
    {
        if (!TryCollectRobotColliders())
        {
            summary = "robot_hulls_missing";
            lastValidationWasSafe = false;
            lastValidationSummary = summary;
            return false;
        }

        collectObstacles();
        return CompleteValidation(label, out summary);
    }

    private bool CompleteValidation(string label, out string summary)
    {
        Collider robotCollider;
        Collider obstacleCollider;
        float penetrationDistance;
        bool safe = !ColliderOverlapUtility.TryFindOverlap(
            robotColliders,
            obstacleColliders,
            out robotCollider,
            out obstacleCollider,
            out penetrationDistance);

        if (safe)
        {
            summary = label + "_clear";
        }
        else
        {
            summary =
                label +
                "_overlap robot=" + robotCollider.transform.name +
                " obstacle=" + obstacleCollider.transform.name +
                " penetration=" + penetrationDistance.ToString("F3");
        }

        lastValidationWasSafe = safe;
        lastValidationSummary = summary;
        return safe;
    }

    private bool TryCollectRobotColliders()
    {
        robotColliders.Clear();

        if (collisionRig == null || obstacleRegistry == null)
        {
            return false;
        }

        collisionRig.CollectColliders(robotColliders);
        return robotColliders.Count > 0;
    }

    private void CollectTableObstacles()
    {
        obstacleColliders.Clear();
        obstacleRegistry.CollectTableColliders(obstacleColliders);
    }

    private void AutoResolveDependencies()
    {
        if (collisionRig == null)
        {
            collisionRig = GetComponent<RobotCollisionRig>();
            if (collisionRig == null)
            {
                collisionRig = FindAnyObjectByType<RobotCollisionRig>();
            }
        }

        if (obstacleRegistry == null)
        {
            obstacleRegistry = FindAnyObjectByType<SceneObstacleRegistry>();
        }
    }
}
