using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds collision-only trigger colliders for the moving robot links.
/// These colliders stay trigger-only so they can be used for overlap checks
/// without changing the approved robot motion baseline.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-380)]
public class RobotCollisionRig : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedCollisionHulls";

    private static readonly string[] CollisionLinks =
    {
        "Carriage",
        "HorBeam",
        "VerBeam",
        "Sleeve",
        "CArc",
    };

    private readonly List<Collider> builtColliders = new List<Collider>();

    [Header("Dependencies")]
    public RobotPoseController controller;

    public IReadOnlyList<Collider> HullColliders
    {
        get { return builtColliders; }
    }

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnValidate()
    {
        EnsureBuilt();
    }

    [ContextMenu("Rebuild Robot Collision Hulls")]
    public void EnsureBuilt()
    {
        AutoResolveDependencies();
        RebuildInternal();
    }

    public void CollectColliders(List<Collider> results)
    {
        if (results == null)
        {
            return;
        }

        EnsureBuilt();
        results.Clear();
        for (int i = 0; i < builtColliders.Count; i++)
        {
            Collider collider = builtColliders[i];
            if (collider != null && collider.enabled)
            {
                results.Add(collider);
            }
        }
    }

    private void RebuildInternal()
    {
        builtColliders.Clear();

        if (controller == null || controller.robotRoot == null)
        {
            return;
        }

        for (int i = 0; i < CollisionLinks.Length; i++)
        {
            Transform linkTransform = FindNamedTransform(controller.robotRoot.transform, CollisionLinks[i]);
            if (linkTransform == null)
            {
                continue;
            }

            Transform collisionsRoot = linkTransform.Find("Collisions");
            if (collisionsRoot == null)
            {
                continue;
            }

            Transform generatedRoot = collisionsRoot.Find(GeneratedRootName);
            if (generatedRoot != null)
            {
                DestroyChildImmediate(generatedRoot.gameObject);
            }

            GameObject root = new GameObject(GeneratedRootName);
            root.transform.SetParent(collisionsRoot, false);

            Transform visualsRoot = linkTransform.Find("Visuals");
            if (visualsRoot == null)
            {
                continue;
            }

            MeshFilter[] meshFilters = visualsRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int meshIndex = 0; meshIndex < meshFilters.Length; meshIndex++)
            {
                MeshFilter meshFilter = meshFilters[meshIndex];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                GameObject hull = new GameObject(meshFilter.name + "_Collider");
                hull.transform.SetParent(root.transform, false);
                hull.transform.position = meshFilter.transform.position;
                hull.transform.rotation = meshFilter.transform.rotation;
                hull.transform.localScale = meshFilter.transform.lossyScale;
                MeshCollider collider = hull.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
                collider.convex = true;
                collider.isTrigger = true;
                builtColliders.Add(collider);
            }
        }
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
    }

    private static Transform FindNamedTransform(Transform root, string targetName)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == targetName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private static void DestroyChildImmediate(GameObject target)
    {
        if (target == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(target);
        }
        else
        {
            Object.Destroy(target);
        }
#else
        Object.Destroy(target);
#endif
    }

}
