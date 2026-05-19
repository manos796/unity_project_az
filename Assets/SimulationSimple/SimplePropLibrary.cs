using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SimplePropLibrary : MonoBehaviour
{
    private static readonly SimplePropSpec[] CuratedDefaults =
    {
        new SimplePropSpec
        {
            id = "rect_short",
            kind = SimplePropKind.RectShort,
            surface = SimplePropSurface.Floor,
            minScale = new Vector3(0.35f, 0.22f, 0.35f),
            maxScale = new Vector3(0.95f, 0.58f, 0.85f),
            color = new Color(0.77f, 0.80f, 0.82f, 1f),
        },
        new SimplePropSpec
        {
            id = "rect_tall",
            kind = SimplePropKind.RectTall,
            surface = SimplePropSurface.Floor,
            minScale = new Vector3(0.35f, 0.85f, 0.35f),
            maxScale = new Vector3(0.72f, 1.45f, 0.70f),
            color = new Color(0.65f, 0.74f, 0.83f, 1f),
        },
        new SimplePropSpec
        {
            id = "human_blob",
            kind = SimplePropKind.HumanBlob,
            surface = SimplePropSurface.Floor,
            minScale = new Vector3(0.24f, 0.70f, 0.24f),
            maxScale = new Vector3(0.36f, 1.00f, 0.36f),
            color = new Color(0.87f, 0.67f, 0.58f, 1f),
        },
        new SimplePropSpec
        {
            id = "ceiling_short",
            kind = SimplePropKind.CeilingShort,
            surface = SimplePropSurface.Ceiling,
            minScale = new Vector3(0.45f, 0.18f, 0.45f),
            maxScale = new Vector3(0.80f, 0.32f, 0.80f),
            color = new Color(0.92f, 0.92f, 0.94f, 1f),
        },
        new SimplePropSpec
        {
            id = "ceiling_long",
            kind = SimplePropKind.CeilingLong,
            surface = SimplePropSurface.Ceiling,
            minScale = new Vector3(0.80f, 0.28f, 0.80f),
            maxScale = new Vector3(1.30f, 0.46f, 1.30f),
            color = new Color(0.92f, 0.92f, 0.94f, 1f),
        },
        new SimplePropSpec
        {
            id = "table_block",
            kind = SimplePropKind.TableBlock,
            surface = SimplePropSurface.Table,
            minScale = new Vector3(0.18f, 0.05f, 0.18f),
            maxScale = new Vector3(0.40f, 0.10f, 0.30f),
            color = new Color(0.88f, 0.88f, 0.90f, 1f),
        },
    };

    public bool useCuratedDefaults = true;
    public List<SimplePropSpec> specs = new List<SimplePropSpec>();

    public IReadOnlyList<SimplePropSpec> Specs
    {
        get { return useCuratedDefaults ? CuratedDefaults : specs; }
    }

    public bool TryBuildSceneSet(System.Random random, int minCount, int maxCount, out List<SimplePropSpec> sceneSet)
    {
        sceneSet = new List<SimplePropSpec>();
        IReadOnlyList<SimplePropSpec> available = Specs;
        if (available == null || available.Count == 0)
        {
            return false;
        }

        List<SimplePropSpec> pool = new List<SimplePropSpec>(available.Count);
        List<SimplePropSpec> ceilingPool = new List<SimplePropSpec>();
        for (int i = 0; i < available.Count; i++)
        {
            SimplePropSpec spec = available[i];
            if (spec == null)
            {
                continue;
            }

            pool.Add(spec);
            if (spec.surface == SimplePropSurface.Ceiling)
            {
                ceilingPool.Add(spec);
            }
        }

        if (pool.Count == 0 || ceilingPool.Count == 0)
        {
            return false;
        }

        int clampedMin = Mathf.Clamp(minCount, 1, pool.Count);
        int clampedMax = Mathf.Clamp(maxCount, clampedMin, pool.Count);
        int targetCount = random.Next(clampedMin, clampedMax + 1);

        SimplePropSpec requiredCeiling = ceilingPool[random.Next(0, ceilingPool.Count)];
        sceneSet.Add(requiredCeiling);
        RemoveById(pool, requiredCeiling.id);

        while (sceneSet.Count < targetCount && pool.Count > 0)
        {
            int index = random.Next(0, pool.Count);
            sceneSet.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return sceneSet.Count >= clampedMin;
    }

    private static void RemoveById(List<SimplePropSpec> entries, string id)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i] != null && entries[i].id == id)
            {
                entries.RemoveAt(i);
            }
        }
    }
}
