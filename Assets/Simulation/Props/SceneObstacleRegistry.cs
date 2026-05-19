using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central lookup for obstacle colliders and room/table placement surfaces.
/// Keeps robot overlap checks and prop validation on one scene truth source.
/// </summary>
[DisallowMultipleComponent]
public class SceneObstacleRegistry : MonoBehaviour
{
    [Header("Scene References")]
    public GameObject roomRoot;
    public GameObject tableRoot;
    public PropSpawner propSpawner;

    private void Awake()
    {
        AutoResolveDependencies();
    }

    private void OnValidate()
    {
        AutoResolveDependencies();
    }

    public void CollectTableColliders(List<Collider> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        ColliderOverlapUtility.CollectColliders(tableRoot, results);
    }

    public void CollectRoomColliders(List<Collider> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        ColliderOverlapUtility.CollectColliders(roomRoot, results);
    }

    public void CollectSpawnedPropColliders(List<Collider> results, Transform excludeRoot = null)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();

        Transform propsRoot = propSpawner != null ? propSpawner.spawnedPropsRoot : null;
        if (propsRoot == null)
        {
            return;
        }

        Collider[] colliders = propsRoot.GetComponentsInChildren<Collider>(true);
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

    public bool TryGetRoomInteriorBounds(float wallClearance, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        if (roomRoot == null)
        {
            return false;
        }

        CleanRoomBuilder room = roomRoot.GetComponent<CleanRoomBuilder>();
        if (room == null)
        {
            return false;
        }

        Vector3 size = new Vector3(
            Mathf.Max(0f, room.lengthX - (wallClearance * 2f)),
            Mathf.Max(0f, room.heightY),
            Mathf.Max(0f, room.widthZ - (wallClearance * 2f)));

        bounds = new Bounds(
            roomRoot.transform.position + new Vector3(0f, room.heightY * 0.5f, 0f),
            size);
        return true;
    }

    public bool TryGetTableSurfaceBounds(out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        if (tableRoot == null)
        {
            return false;
        }

        SurgeryTableBuilder table = tableRoot.GetComponent<SurgeryTableBuilder>();
        if (table == null)
        {
            return false;
        }

        Vector3 center = tableRoot.transform.position +
            new Vector3(table.longitudinalOffset, table.topSurfaceHeight, table.lateralOffset);
        Vector3 size = new Vector3(table.topLength, 0.001f, table.topWidth);
        bounds = new Bounds(center, size);
        return true;
    }

    private void AutoResolveDependencies()
    {
        if (roomRoot == null)
        {
            CleanRoomBuilder room = FindAnyObjectByType<CleanRoomBuilder>();
            if (room != null)
            {
                roomRoot = room.gameObject;
            }
        }

        if (tableRoot == null)
        {
            SurgeryTableBuilder table = FindAnyObjectByType<SurgeryTableBuilder>();
            if (table != null)
            {
                tableRoot = table.gameObject;
            }
        }

        if (propSpawner == null)
        {
            propSpawner = GetComponent<PropSpawner>();
            if (propSpawner == null)
            {
                propSpawner = FindAnyObjectByType<PropSpawner>();
            }
        }
    }
}
