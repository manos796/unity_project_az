using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SimplePropSpawner : MonoBehaviour
{
    private const string ActiveRootName = "ActiveSimpleProps";
    private const string CandidateRootName = "CandidateSimpleProps";

    private readonly List<Collider> tempColliders = new List<Collider>();
    private readonly List<Collider> robotColliders = new List<Collider>();
    private readonly List<Collider> forbiddenColliders = new List<Collider>();
    private readonly Dictionary<Color, Material> materialsByColor = new Dictionary<Color, Material>();

    [Header("Dependencies")]
    public SimplePropLibrary library;
    public SimpleRobotProxyRig robotProxyRig;
    public SimpleForbiddenZones forbiddenZones;

    [Header("Sampling")]
    public int minRequestedPropCount = 3;
    public int maxRequestedPropCount = 6;
    public int maxAttemptsPerProp = 8;
    public float supportSurfaceClearance = 0.01f;

    [Header("State")]
    public Transform activePropsRoot;
    public Transform candidatePropsRoot;
    [SerializeField] [TextArea(4, 10)] private string lastSummaryJson = "";

    public SimpleSceneSummary LastSummary { get; private set; }

    private void Awake()
    {
        EnsureRoots();
    }

    private void OnValidate()
    {
        EnsureRoots();
    }

    public bool TrySpawnScene(int sampleIndex, int robotSeed, int propSeed, out SimpleSceneSummary summary)
    {
        EnsureRoots();
        ClearRoot(candidatePropsRoot);

        summary = new SimpleSceneSummary
        {
            sampleIndex = sampleIndex,
            robotSeed = robotSeed,
            propSeed = propSeed,
            status = "rejected",
        };

        if (library == null || robotProxyRig == null || forbiddenZones == null)
        {
            summary.rejectionReason = "dependencies_missing";
            LastSummary = summary;
            lastSummaryJson = JsonUtility.ToJson(summary, true);
            return false;
        }

        System.Random random = new System.Random(propSeed);
        List<SimplePropSpec> sceneSet;
        if (!library.TryBuildSceneSet(random, minRequestedPropCount, maxRequestedPropCount, out sceneSet))
        {
            summary.rejectionReason = "failed_to_build_scene_set";
            LastSummary = summary;
            lastSummaryJson = JsonUtility.ToJson(summary, true);
            return false;
        }

        summary.requestedProps = sceneSet.Count;

        for (int propIndex = 0; propIndex < sceneSet.Count; propIndex++)
        {
            SimplePropSpec spec = sceneSet[propIndex];
            bool placed = false;
            for (int attempt = 0; attempt < maxAttemptsPerProp; attempt++)
            {
                Vector3 scale = SampleScale(spec, random);
                Quaternion rotation = SampleRotation(spec, random);
                GameObject candidate = BuildPropObject(spec, scale);
                candidate.name = "Candidate_" + propIndex.ToString("D2") + "_" + spec.id;
                candidate.transform.SetParent(candidatePropsRoot, false);
                candidate.transform.rotation = rotation;

                Vector3 position;
                if (!TrySamplePosition(candidate, spec.surface, random, out position, spec))
                {
                    DestroyProp(candidate);
                    continue;
                }

                candidate.transform.position = position;
                if (!IsValidCandidate(candidate, spec.surface, spec))
                {
                    DestroyProp(candidate);
                    continue;
                }

                candidate.name = "Prop_" + propIndex.ToString("D2") + "_" + spec.id;
                summary.placedProps++;
                summary.placedPropIds.Add(spec.id);
                placed = true;
                break;
            }

            if (!placed)
            {
                summary.rejectedProps++;
            }
        }

        if (summary.placedProps < minRequestedPropCount || !ContainsCeilingProp(summary.placedPropIds))
        {
            summary.rejectionReason = summary.placedProps < minRequestedPropCount ? "too_few_props" : "missing_ceiling_prop";
            ClearRoot(candidatePropsRoot);
            LastSummary = summary;
            lastSummaryJson = JsonUtility.ToJson(summary, true);
            return false;
        }

        if (!ValidateRobotAgainstCandidateProps(out string overlapReason))
        {
            summary.rejectionReason = overlapReason;
            ClearRoot(candidatePropsRoot);
            LastSummary = summary;
            lastSummaryJson = JsonUtility.ToJson(summary, true);
            return false;
        }

        ClearRoot(activePropsRoot);
        while (candidatePropsRoot.childCount > 0)
        {
            candidatePropsRoot.GetChild(0).SetParent(activePropsRoot, true);
        }

        summary.accepted = true;
        summary.status = "accepted";
        LastSummary = summary;
        lastSummaryJson = JsonUtility.ToJson(summary, true);
        return true;
    }

    public void ClearAllProps()
    {
        EnsureRoots();
        ClearRoot(activePropsRoot);
        ClearRoot(candidatePropsRoot);
    }

    public bool ValidateRobotAgainstActiveProps(out string summary)
    {
        summary = "robot_props_clear";

        if (robotProxyRig == null)
        {
            summary = "robot_proxies_missing";
            return false;
        }

        robotProxyRig.CollectColliders(robotColliders);
        CollectColliders(activePropsRoot, tempColliders);

        Collider a;
        Collider b;
        if (TryFindOverlap(robotColliders, tempColliders, out a, out b))
        {
            summary = "robot_prop_overlap robot=" + a.transform.name + " prop=" + b.transform.name;
            return false;
        }

        return true;
    }

    private void EnsureRoots()
    {
        if (activePropsRoot == null)
        {
            Transform existing = transform.Find(ActiveRootName);
            if (existing == null)
            {
                GameObject root = new GameObject(ActiveRootName);
                root.transform.SetParent(transform, false);
                existing = root.transform;
            }

            activePropsRoot = existing;
        }

        if (candidatePropsRoot == null)
        {
            Transform existing = transform.Find(CandidateRootName);
            if (existing == null)
            {
                GameObject root = new GameObject(CandidateRootName);
                root.transform.SetParent(transform, false);
                existing = root.transform;
            }

            candidatePropsRoot = existing;
        }
    }

    // private bool TrySamplePosition(GameObject candidate, SimplePropSurface surface, System.Random random, out Vector3 position)
    private bool TrySamplePosition(GameObject candidate, SimplePropSurface surface, System.Random random, out Vector3 position, SimplePropSpec spec = null)
    {
        position = Vector3.zero;

        CollectColliders(candidate.transform, tempColliders);
        Bounds candidateBounds;
         if (!TryGetCombinedBounds(tempColliders, out candidateBounds))
         {
             return false;
         }

        Bounds roomBounds;
        if (!forbiddenZones.TryGetRoomInteriorBounds(out roomBounds))
        {
            return false;
        }

        float x;
        float y;
        float z;

        switch (surface)
        {
            case SimplePropSurface.Ceiling:
                float ceilingY;
                if (!forbiddenZones.TryGetCeilingY(out ceilingY))
                {
                    return false;
                }

                if (spec != null && spec.useCustomSpawnArea)
                {
                    x = Mathf.Lerp(spec.spawnAreaMin.x, spec.spawnAreaMax.x, (float)random.NextDouble());
                    z = Mathf.Lerp(spec.spawnAreaMin.y, spec.spawnAreaMax.y, (float)random.NextDouble());
                }
                else
                {
                    x = SampleRootCoordinate(random, roomBounds.min.x, roomBounds.max.x, candidateBounds.min.x, candidateBounds.max.x);
                    z = SampleRootCoordinate(random, roomBounds.min.z, roomBounds.max.z, candidateBounds.min.z, candidateBounds.max.z);
                }
                y = ceilingY - candidateBounds.max.y;
                break;

            case SimplePropSurface.Table:
                Bounds tableSurface;
                if (!forbiddenZones.TryGetTableSurfaceBounds(out tableSurface))
                {
                    return false;
                }

                x = SampleRootCoordinate(random, tableSurface.min.x, tableSurface.max.x, candidateBounds.min.x, candidateBounds.max.x);
                z = SampleRootCoordinate(random, tableSurface.min.z, tableSurface.max.z, candidateBounds.min.z, candidateBounds.max.z);
                y = tableSurface.center.y - candidateBounds.min.y + supportSurfaceClearance;
                break;

            case SimplePropSurface.Floor:
            default:
                x = SampleRootCoordinate(random, roomBounds.min.x, roomBounds.max.x, candidateBounds.min.x, candidateBounds.max.x);
                z = SampleRootCoordinate(random, roomBounds.min.z, roomBounds.max.z, candidateBounds.min.z, candidateBounds.max.z);
                // y = supportSurfaceClearance - candidateBounds.min.y + 1.1f;
                float yOffset = 0f;
                if (spec.id == "doctor" || spec.id == "doctor_2" || spec.id == "doctor_3" )
                {
                    yOffset = 1.1f;
                }
                else if (spec.id == "medical_trolley")
                {
                    yOffset = -0.1f;
                }
                else if (spec.id == "ultrasound")
                {
                    yOffset = -0.1f;
                }
                y = supportSurfaceClearance - candidateBounds.min.y + yOffset;
                
                break;
        }

        position = new Vector3(x, y, z);
        return true;
    }

    private bool IsValidCandidate(GameObject candidate, SimplePropSurface surface, SimplePropSpec spec = null)
    {
        CollectColliders(candidate.transform, tempColliders);
        if (tempColliders.Count == 0)
        {
            return false;
        }

        Bounds roomBounds;
        Bounds candidateBounds;
        if (!forbiddenZones.TryGetRoomInteriorBounds(out roomBounds) || !TryGetCombinedBounds(tempColliders, out candidateBounds))
        {
            return false;
        }

        if (spec == null || !spec.ignoreRoomBounds)
        {
            if (!roomBounds.Contains(candidateBounds.min) || !roomBounds.Contains(candidateBounds.max))
            {
                return false;
            }
        }

        robotProxyRig.CollectColliders(robotColliders);
        Collider overlapA;
        Collider overlapB;
        if (TryFindOverlap(tempColliders, robotColliders, out overlapA, out overlapB))
        {
            return false;
        }

        forbiddenZones.CollectAllForbiddenColliders(forbiddenColliders);
        if (surface == SimplePropSurface.Table)
        {
            forbiddenZones.CollectTableForbiddenColliders(forbiddenColliders);
            for (int i = forbiddenColliders.Count - 1; i >= 0; i--)
            {
                if (forbiddenColliders[i] != null && forbiddenColliders[i].transform.name == "TableTopForbidden")
                {
                    forbiddenColliders.RemoveAt(i);
                }
            }
        }

        if (TryFindOverlap(tempColliders, forbiddenColliders, out overlapA, out overlapB))
        {
            return false;
        }

        List<Collider> existingCandidates = new List<Collider>();
        CollectColliders(candidatePropsRoot, existingCandidates, candidate.transform);
        if (TryFindOverlap(tempColliders, existingCandidates, out overlapA, out overlapB))
        {
            return false;
        }

        return true;
    }

    private bool ValidateRobotAgainstCandidateProps(out string reason)
    {
        reason = "robot_props_clear";
        robotProxyRig.CollectColliders(robotColliders);
        CollectColliders(candidatePropsRoot, tempColliders);

        Collider a;
        Collider b;
        if (TryFindOverlap(robotColliders, tempColliders, out a, out b))
        {
            reason = "robot_prop_overlap robot=" + a.transform.name + " prop=" + b.transform.name;
            return false;
        }

        return true;
    }

    private GameObject BuildPropObject(SimplePropSpec spec, Vector3 scale)
    {
        // Αν υπάρχει prefab στο spec, χρησιμοποίησέ το
        if (spec.prefab != null)
        {
            GameObject instance = GameObject.Instantiate(spec.prefab);
            instance.transform.localScale = scale;
            return instance;
        }

        // Αλλιώς συνέχισε με το παλιό σύστημα (primitives)
        PrimitiveType primitiveType = PrimitiveType.Cube;
        switch (spec.kind)
        {
            case SimplePropKind.HumanBlob:
                primitiveType = PrimitiveType.Capsule;
                break;
            case SimplePropKind.CeilingShort:
            case SimplePropKind.CeilingLong:
                primitiveType = PrimitiveType.Cylinder;
                break;
            default:
                primitiveType = PrimitiveType.Cube;
                break;
        }

        GameObject root = GameObject.CreatePrimitive(primitiveType);
        root.transform.localScale = scale;

        Renderer renderer = root.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetMaterial(spec.color);
        }

        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (rb != null)
        {
            DestroyImmediate(rb);
        }

        return root;
    }
    private Material GetMaterial(Color color)
    {
        Material material;
        if (materialsByColor.TryGetValue(color, out material) && material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader);
        material.color = color;
        materialsByColor[color] = material;
        return material;
    }

    private void ClearRoot(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            DestroyProp(child.gameObject);
        }
    }

    private static void DestroyProp(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return;
        }

        Collider[] colliders = gameObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(gameObject);
        }
        else
        {
            Object.Destroy(gameObject);
        }
#else
        Object.Destroy(gameObject);
#endif
    }

    private static Vector3 SampleScale(SimplePropSpec spec, System.Random random)
    {
        return new Vector3(
            Mathf.Lerp(spec.minScale.x, spec.maxScale.x, (float)random.NextDouble()),
            Mathf.Lerp(spec.minScale.y, spec.maxScale.y, (float)random.NextDouble()),
            Mathf.Lerp(spec.minScale.z, spec.maxScale.z, (float)random.NextDouble()));
    }

    private static Quaternion SampleRotation(SimplePropSpec spec, System.Random random)
    {
        switch (spec.surface)
        {
            case SimplePropSurface.Ceiling:
            case SimplePropSurface.Floor:
            case SimplePropSurface.Table:
            default:
                float xRot = (spec.id == "medical_trolley" || spec.id == "anesthesia_machine" || spec.id == "ultrasound") ? -90f : 0f;
                return Quaternion.Euler(xRot, Mathf.Lerp(0f, 360f, (float)random.NextDouble()), 0f);
        }
    }

    private static bool ContainsCeilingProp(List<string> ids)
    {
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == "ceiling_short" || ids[i] == "ceiling_long")
            {
                return true;
            }
        }

        return false;
    }

    private static float SampleRootCoordinate(System.Random random, float minSurface, float maxSurface, float candidateMin, float candidateMax)
    {
        float minRoot = minSurface - candidateMin;
        float maxRoot = maxSurface - candidateMax;
        if (maxRoot < minRoot)
        {
            return minRoot;
        }

        return Mathf.Lerp(minRoot, maxRoot, (float)random.NextDouble());
    }

    private static void CollectColliders(Transform root, List<Collider> results, Transform excludeRoot = null)
    {
        results.Clear();
        if (root == null)
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

            if (excludeRoot != null && collider.transform.IsChildOf(excludeRoot))
            {
                continue;
            }

            results.Add(collider);
        }
    }

    private static bool TryGetCombinedBounds(IReadOnlyList<Collider> colliders, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        Physics.SyncTransforms();

        for (int i = 0; i < colliders.Count; i++)
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

    private static bool TryFindOverlap(IReadOnlyList<Collider> first, IReadOnlyList<Collider> second, out Collider firstCollider, out Collider secondCollider)
    {
        firstCollider = null;
        secondCollider = null;
        Physics.SyncTransforms();

        for (int i = 0; i < first.Count; i++)
        {
            Collider a = first[i];
            if (a == null || !a.enabled)
            {
                continue;
            }

            for (int j = 0; j < second.Count; j++)
            {
                Collider b = second[j];
                if (b == null || !b.enabled)
                {
                    continue;
                }

                if (!a.bounds.Intersects(b.bounds))
                {
                    continue;
                }

                Vector3 direction;
                float distance;
                if (Physics.ComputePenetration(
                    a,
                    a.transform.position,
                    a.transform.rotation,
                    b,
                    b.transform.position,
                    b.transform.rotation,
                    out direction,
                    out distance))
                {
                    firstCollider = a;
                    secondCollider = b;
                    return true;
                }
            }
        }

        return false;
    }
}
