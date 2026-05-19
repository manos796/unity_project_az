using UnityEngine;

/// <summary>
/// Performs deterministic spawn validation against the current room,
/// table colliders, robot collision hulls, and already spawned props.
/// </summary>
[DisallowMultipleComponent]
public class SpawnValidator : MonoBehaviour
{
    private readonly System.Collections.Generic.List<Collider> candidateColliders = new System.Collections.Generic.List<Collider>();
    private readonly System.Collections.Generic.List<Collider> obstacleColliders = new System.Collections.Generic.List<Collider>();
    private readonly System.Collections.Generic.List<Collider> robotColliders = new System.Collections.Generic.List<Collider>();

    [Header("Dependencies")]
    public SceneObstacleRegistry obstacleRegistry;
    public RobotCollisionRig robotCollisionRig;

    [Header("Clearance")]
    public float roomWallClearance = 0.05f;

    private void Awake()
    {
        AutoResolveDependencies();
    }

    private void OnValidate()
    {
        AutoResolveDependencies();
    }

    public bool TryValidateCandidate(GameObject candidateRoot, out string rejectionReason)
    {
        rejectionReason = null;
        AutoResolveDependencies();

        if (candidateRoot == null || obstacleRegistry == null || robotCollisionRig == null)
        {
            return false;
        }

        candidateColliders.Clear();
        ColliderOverlapUtility.CollectColliders(candidateRoot, candidateColliders);
        if (candidateColliders.Count == 0)
        {
            rejectionReason = "candidate_has_no_colliders";
            return false;
        }

        if (!IsInsideRoom(candidateColliders))
        {
            rejectionReason = "outside_room_bounds";
            return false;
        }

        obstacleRegistry.CollectTableColliders(obstacleColliders);
        if (FindOverlap(candidateColliders, obstacleColliders))
        {
            rejectionReason = "overlaps_table";
            return false;
        }

        robotCollisionRig.CollectColliders(robotColliders);
        if (FindOverlap(candidateColliders, robotColliders))
        {
            rejectionReason = "overlaps_robot";
            return false;
        }

        obstacleRegistry.CollectSpawnedPropColliders(obstacleColliders, candidateRoot.transform);
        if (FindOverlap(candidateColliders, obstacleColliders))
        {
            rejectionReason = "overlaps_existing_prop";
            return false;
        }

        return true;
    }

    public bool TryGetRoomInteriorBounds(out Bounds bounds)
    {
        AutoResolveDependencies();
        if (obstacleRegistry == null)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            return false;
        }

        return obstacleRegistry.TryGetRoomInteriorBounds(roomWallClearance, out bounds);
    }

    public bool TryGetTableSurfaceBounds(out Bounds bounds)
    {
        AutoResolveDependencies();
        if (obstacleRegistry == null)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            return false;
        }

        return obstacleRegistry.TryGetTableSurfaceBounds(out bounds);
    }

    private bool IsInsideRoom(System.Collections.Generic.IReadOnlyList<Collider> colliders)
    {
        Bounds roomBounds;
        Bounds candidateBounds;
        if (!TryGetRoomInteriorBounds(out roomBounds) ||
            !ColliderOverlapUtility.TryGetCombinedBounds(colliders, out candidateBounds))
        {
            return false;
        }

        return roomBounds.Contains(candidateBounds.min) && roomBounds.Contains(candidateBounds.max);
    }

    private static bool FindOverlap(
        System.Collections.Generic.IReadOnlyList<Collider> candidate,
        System.Collections.Generic.IReadOnlyList<Collider> obstacles)
    {
        Collider first;
        Collider second;
        float penetrationDistance;
        return ColliderOverlapUtility.TryFindOverlap(candidate, obstacles, out first, out second, out penetrationDistance);
    }

    private void AutoResolveDependencies()
    {
        if (obstacleRegistry == null)
        {
            obstacleRegistry = GetComponent<SceneObstacleRegistry>();
            if (obstacleRegistry == null)
            {
                obstacleRegistry = FindAnyObjectByType<SceneObstacleRegistry>();
            }
        }

        if (robotCollisionRig == null)
        {
            robotCollisionRig = FindAnyObjectByType<RobotCollisionRig>();
        }
    }
}
