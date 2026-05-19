using UnityEngine;

/// <summary>
/// Spawns simple obstacle props into the room using the catalog and validator.
/// Placement failures are recorded explicitly so later export can explain why
/// a requested prop did or did not appear in a scene.
/// </summary>
[DisallowMultipleComponent]
public class PropSpawner : MonoBehaviour
{
    [Header("Dependencies")]
    public PropCatalog catalog;
    public SpawnValidator validator;

    [Header("Sampling")]
    public string randomizationProfile = "SimpleDeterministicProps";
    public int previewSeed = 2000;
    public int requestedPropCount = 6;
    public int maxAttemptsPerProp = 8;
    public bool spawnOnStart = false;

    [Header("Free Space")]
    public float freeSpaceMinHeight = 0.4f;
    public float freeSpaceMaxHeight = 1.8f;
    public float supportSurfaceClearance = 0.01f;

    [Header("State")]
    public Transform spawnedPropsRoot;
    [SerializeField] private string lastReportJson = "";
    [SerializeField] private int lastPlacedProps;
    [SerializeField] private int lastRejectedProps;

    public SceneRandomizationReport LastReport { get; private set; }

    private void Start()
    {
        if (Application.isPlaying && spawnOnStart)
        {
            SpawnPreviewProps(previewSeed);
        }
    }

    [ContextMenu("Spawn Preview Props")]
    public void SpawnPreviewProps()
    {
        SpawnPreviewProps(previewSeed);
    }

    public SceneRandomizationReport SpawnPreviewProps(int seed)
    {
        EnsureSpawnRoot();
        if (catalog == null || validator == null)
        {
            Debug.LogWarning("PropSpawner: Assign catalog and validator first.");
            return null;
        }

        ClearSpawnedProps();

        System.Random random = new System.Random(seed);
        SceneRandomizationReport report = new SceneRandomizationReport();
        report.profile = randomizationProfile;
        report.seed = seed;
        report.requestedProps = requestedPropCount;

        for (int propIndex = 0; propIndex < requestedPropCount; propIndex++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < maxAttemptsPerProp; attempt++)
            {
                PropCatalogEntry entry;
                if (!catalog.TryGetRandomEntry(random, out entry))
                {
                    break;
                }

                Vector3 scale = SampleScale(entry, random);
                Quaternion rotation = SampleRotation(entry, random);
                GameObject candidate = BuildCandidateObject(entry, scale);
                if (candidate == null)
                {
                    AppendAttempt(report, propIndex, attempt, entry, Vector3.zero, rotation.eulerAngles, scale, "rejected", "failed_to_build_candidate");
                    continue;
                }

                candidate.name = "Candidate_" + propIndex.ToString("D2") + "_" + entry.id;
                candidate.transform.SetParent(spawnedPropsRoot, false);
                candidate.transform.rotation = rotation;

                Vector3 position;
                if (!TrySamplePosition(candidate, entry.surface, random, out position))
                {
                    AppendAttempt(report, propIndex, attempt, entry, position, rotation.eulerAngles, scale, "rejected", "failed_to_sample_position");
                    DestroySpawnedProp(candidate);
                    continue;
                }

                candidate.transform.position = position;

                string rejectionReason;
                if (!validator.TryValidateCandidate(candidate, out rejectionReason))
                {
                    AppendAttempt(report, propIndex, attempt, entry, position, rotation.eulerAngles, scale, "rejected", rejectionReason);
                    DestroySpawnedProp(candidate);
                    continue;
                }

                candidate.name = "Prop_" + propIndex.ToString("D2") + "_" + entry.id;
                SpawnedPropDescriptor descriptor = candidate.AddComponent<SpawnedPropDescriptor>();
                descriptor.propId = entry.id;
                descriptor.category = entry.category;
                descriptor.surface = entry.surface.ToString();

                AppendAttempt(report, propIndex, attempt, entry, position, rotation.eulerAngles, scale, "placed", "");
                report.placedProps++;
                placed = true;
                break;
            }

            if (!placed)
            {
                report.rejectedProps++;
            }
        }

        LastReport = report;
        lastReportJson = JsonUtility.ToJson(report, true);
        lastPlacedProps = report.placedProps;
        lastRejectedProps = report.rejectedProps;
        Debug.Log("PropSpawner: placed=" + report.placedProps + " requested=" + report.requestedProps + " seed=" + seed);
        return report;
    }

    [ContextMenu("Clear Spawned Props")]
    public void ClearSpawnedProps()
    {
        EnsureSpawnRoot();

        for (int i = spawnedPropsRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = spawnedPropsRoot.GetChild(i);
            Collider[] colliders = child.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < colliders.Length; c++)
            {
                colliders[c].enabled = false;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(child.gameObject);
            }
            else
            {
                Destroy(child.gameObject);
            }
#else
            Destroy(child.gameObject);
#endif
        }
    }

    public SceneObjectsExport CaptureSceneObjectsExport()
    {
        EnsureSpawnRoot();

        SceneObjectsExport export = new SceneObjectsExport();
        for (int i = 0; i < spawnedPropsRoot.childCount; i++)
        {
            Transform child = spawnedPropsRoot.GetChild(i);
            Renderer rendererComponent = child.GetComponent<Renderer>();
            SpawnedPropDescriptor descriptor = child.GetComponent<SpawnedPropDescriptor>();

            SceneObjectExport entry = new SceneObjectExport();
            entry.objectId = descriptor != null && !string.IsNullOrWhiteSpace(descriptor.propId) ? descriptor.propId : child.name;
            entry.category = descriptor != null && !string.IsNullOrWhiteSpace(descriptor.category) ? descriptor.category : ExtractCategory(child.name);
            entry.surface = descriptor != null && !string.IsNullOrWhiteSpace(descriptor.surface) ? descriptor.surface : "unknown";
            entry.position = child.position;
            entry.eulerRotation = child.eulerAngles;
            entry.scale = child.localScale;
            entry.boundsSize = rendererComponent != null ? rendererComponent.bounds.size : child.localScale;
            export.objects.Add(entry);
        }

        return export;
    }

    private bool TrySamplePosition(
        GameObject candidate,
        PropSpawnSurface surface,
        System.Random random,
        out Vector3 position)
    {
        position = Vector3.zero;

        Bounds roomBounds;
        if (!validator.TryGetRoomInteriorBounds(out roomBounds))
        {
            return false;
        }

        Collider[] colliders = candidate.GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        System.Collections.Generic.List<Collider> candidateColliders = new System.Collections.Generic.List<Collider>(colliders);
        Bounds candidateBounds;
        if (!ColliderOverlapUtility.TryGetCombinedBounds(candidateColliders, out candidateBounds))
        {
            return false;
        }

        float x;
        float z;
        float y;

        switch (surface)
        {
            case PropSpawnSurface.Table:
                Bounds tableBounds;
                if (!validator.TryGetTableSurfaceBounds(out tableBounds))
                {
                    return false;
                }

                x = SampleRootCoordinate(random, tableBounds.min.x, tableBounds.max.x, candidateBounds.min.x, candidateBounds.max.x);
                z = SampleRootCoordinate(random, tableBounds.min.z, tableBounds.max.z, candidateBounds.min.z, candidateBounds.max.z);
                y = tableBounds.center.y - candidateBounds.min.y + supportSurfaceClearance;
                break;

            case PropSpawnSurface.FreeSpace:
                x = SampleRootCoordinate(random, roomBounds.min.x, roomBounds.max.x, candidateBounds.min.x, candidateBounds.max.x);
                z = SampleRootCoordinate(random, roomBounds.min.z, roomBounds.max.z, candidateBounds.min.z, candidateBounds.max.z);
                float minY = freeSpaceMinHeight - candidateBounds.min.y;
                float maxY = Mathf.Min(freeSpaceMaxHeight, roomBounds.max.y - supportSurfaceClearance) - candidateBounds.max.y;
                y = SampleRange(random, minY, maxY);
                break;

            case PropSpawnSurface.Ceiling:
                x = SampleRootCoordinate(random, roomBounds.min.x, roomBounds.max.x, candidateBounds.min.x, candidateBounds.max.x);
                z = SampleRootCoordinate(random, roomBounds.min.z, roomBounds.max.z, candidateBounds.min.z, candidateBounds.max.z);
                y = (roomBounds.max.y - supportSurfaceClearance) - candidateBounds.max.y;
                break;

            case PropSpawnSurface.Floor:
            default:
                x = SampleRootCoordinate(random, roomBounds.min.x, roomBounds.max.x, candidateBounds.min.x, candidateBounds.max.x);
                z = SampleRootCoordinate(random, roomBounds.min.z, roomBounds.max.z, candidateBounds.min.z, candidateBounds.max.z);
                y = supportSurfaceClearance - candidateBounds.min.y;
                break;
        }

        position = new Vector3(x, y, z);
        return true;
    }

    private static Vector3 SampleScale(PropCatalogEntry entry, System.Random random)
    {
        return new Vector3(
            SampleRange(random, entry.minScale.x, entry.maxScale.x),
            SampleRange(random, entry.minScale.y, entry.maxScale.y),
            SampleRange(random, entry.minScale.z, entry.maxScale.z));
    }

    private static Quaternion SampleRotation(PropCatalogEntry entry, System.Random random)
    {
        return Quaternion.Euler(0f, SampleRange(random, 0f, 360f), 0f);
    }

    private static float SampleRange(System.Random random, float min, float max)
    {
        if (max <= min)
        {
            return min;
        }

        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private static float SampleRootCoordinate(
        System.Random random,
        float minSurface,
        float maxSurface,
        float candidateMin,
        float candidateMax)
    {
        float minRoot = minSurface - candidateMin;
        float maxRoot = maxSurface - candidateMax;
        return SampleRange(random, minRoot, maxRoot);
    }

    private GameObject BuildCandidateObject(PropCatalogEntry entry, Vector3 scale)
    {
        switch (entry.archetype)
        {
            case PropArchetype.RectShort:
            case PropArchetype.RectTall:
            case PropArchetype.TableBlock:
                return BuildPrimitiveRoot(PrimitiveType.Cube, scale, entry.color);
            case PropArchetype.HumanBlob:
                return BuildPrimitiveRoot(PrimitiveType.Capsule, scale, entry.color);
            case PropArchetype.CeilingLight:
                return BuildCeilingLight(scale, entry.color);
            case PropArchetype.LegacyPrimitive:
            default:
                return BuildPrimitiveRoot(entry.primitiveType, scale, entry.color);
        }
    }

    private static GameObject BuildPrimitiveRoot(PrimitiveType primitiveType, Vector3 scale, Color color)
    {
        GameObject root = GameObject.CreatePrimitive(primitiveType);
        root.transform.localScale = scale;
        ApplyVisuals(root, color);
        return root;
    }

    private static GameObject BuildCeilingLight(Vector3 scale, Color color)
    {
        GameObject root = new GameObject("CeilingLight");

        CreateCubePart(root.transform, "Stem", new Vector3(scale.x * 0.12f, scale.y * 0.45f, scale.z * 0.12f), new Vector3(0f, scale.y * 0.275f, 0f), color);
        CreateCubePart(root.transform, "Housing", new Vector3(scale.x, scale.y * 0.45f, scale.z), new Vector3(0f, -scale.y * 0.225f, 0f), color);

        return root;
    }

    private static void CreateCubePart(Transform parent, string name, Vector3 size, Vector3 localPosition, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = size;
        ApplyVisuals(part, color);
    }

    private static void ApplyVisuals(GameObject prop, Color color)
    {
        Renderer[] renderers = prop.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        material.name = prop.name + "_Mat";

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = material;
        }
    }

    private void EnsureSpawnRoot()
    {
        if (spawnedPropsRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("RandomizedProps");
        if (existing != null)
        {
            spawnedPropsRoot = existing;
        }
        else
        {
            GameObject root = new GameObject("RandomizedProps");
            root.transform.SetParent(transform, false);
            spawnedPropsRoot = root.transform;
        }

        if (validator != null)
        {
            validator.obstacleRegistry = GetComponent<SceneObstacleRegistry>();
        }
    }

    private static void DestroySpawnedProp(GameObject target)
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

    private static string ExtractCategory(string name)
    {
        int index = name.IndexOf('_');
        if (index < 0 || index >= name.Length - 1)
        {
            return "prop";
        }

        return name.Substring(index + 1);
    }

    private static void AppendAttempt(
        SceneRandomizationReport report,
        int propIndex,
        int attempt,
        PropCatalogEntry entry,
        Vector3 position,
        Vector3 eulerRotation,
        Vector3 scale,
        string status,
        string rejectionReason)
    {
        SpawnAttemptRecord record = new SpawnAttemptRecord();
        record.attemptIndex = (propIndex * 100) + attempt;
        record.propId = entry.id;
        record.category = entry.category;
        record.status = status;
        record.rejectionReason = rejectionReason;
        record.position = position;
        record.eulerRotation = eulerRotation;
        record.scale = scale;
        report.attempts.Add(record);
    }
}
