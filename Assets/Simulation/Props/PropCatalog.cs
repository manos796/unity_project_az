using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the prop types that random scene generation can place.
/// Export and spawning code should depend on this catalog instead of hardcoding
/// primitive choices in multiple places.
/// </summary>
[DisallowMultipleComponent]
public class PropCatalog : MonoBehaviour
{
    private static readonly PropCatalogEntry[] CuratedDefaults =
    {
        new PropCatalogEntry
        {
            id = "equipment_block_tall",
            category = "equipment",
            archetype = PropArchetype.RectTall,
            surface = PropSpawnSurface.Floor,
            minScale = new Vector3(0.35f, 0.80f, 0.35f),
            maxScale = new Vector3(0.70f, 1.35f, 0.70f),
            color = new Color(0.65f, 0.74f, 0.83f, 1f)
        },
        new PropCatalogEntry
        {
            id = "equipment_block_short",
            category = "equipment",
            archetype = PropArchetype.RectShort,
            surface = PropSpawnSurface.Floor,
            minScale = new Vector3(0.30f, 0.25f, 0.30f),
            maxScale = new Vector3(0.90f, 0.70f, 0.80f),
            color = new Color(0.77f, 0.80f, 0.82f, 1f)
        },
        new PropCatalogEntry
        {
            id = "instrument_tray",
            category = "tabletop",
            archetype = PropArchetype.TableBlock,
            surface = PropSpawnSurface.Table,
            minScale = new Vector3(0.18f, 0.05f, 0.18f),
            maxScale = new Vector3(0.40f, 0.12f, 0.30f),
            color = new Color(0.88f, 0.88f, 0.90f, 1f)
        },
        new PropCatalogEntry
        {
            id = "human_blob",
            category = "human",
            archetype = PropArchetype.HumanBlob,
            surface = PropSpawnSurface.Floor,
            minScale = new Vector3(0.22f, 0.65f, 0.22f),
            maxScale = new Vector3(0.34f, 0.95f, 0.34f),
            color = new Color(0.87f, 0.67f, 0.58f, 1f)
        },
        new PropCatalogEntry
        {
            id = "ceiling_light",
            category = "overhead_gear",
            archetype = PropArchetype.CeilingLight,
            surface = PropSpawnSurface.Ceiling,
            minScale = new Vector3(0.55f, 0.35f, 0.55f),
            maxScale = new Vector3(1.20f, 0.60f, 1.20f),
            color = new Color(0.92f, 0.92f, 0.94f, 1f)
        },
    };

    public List<PropCatalogEntry> entries = new List<PropCatalogEntry>();
    public bool useCuratedDefaultEntries = true;

    public IReadOnlyList<PropCatalogEntry> Entries
    {
        get { return GetActiveEntries(); }
    }

    private void Reset()
    {
        if (entries.Count > 0)
        {
            return;
        }

        entries.Add(new PropCatalogEntry
        {
            id = "equipment_block",
            category = "equipment",
            primitiveType = PrimitiveType.Cube,
            surface = PropSpawnSurface.Floor,
            minScale = new Vector3(0.35f, 0.40f, 0.35f),
            maxScale = new Vector3(0.80f, 1.20f, 0.60f),
            color = new Color(0.65f, 0.74f, 0.83f, 1f)
        });

        entries.Add(new PropCatalogEntry
        {
            id = "waste_bin",
            category = "container",
            primitiveType = PrimitiveType.Cylinder,
            surface = PropSpawnSurface.Floor,
            minScale = new Vector3(0.18f, 0.35f, 0.18f),
            maxScale = new Vector3(0.30f, 0.55f, 0.30f),
            color = new Color(0.77f, 0.80f, 0.82f, 1f)
        });

        entries.Add(new PropCatalogEntry
        {
            id = "instrument_tray",
            category = "tabletop",
            primitiveType = PrimitiveType.Cube,
            surface = PropSpawnSurface.Table,
            minScale = new Vector3(0.18f, 0.03f, 0.18f),
            maxScale = new Vector3(0.35f, 0.06f, 0.28f),
            color = new Color(0.88f, 0.88f, 0.90f, 1f)
        });

        entries.Add(new PropCatalogEntry
        {
            id = "free_space_marker",
            category = "floating_obstacle",
            primitiveType = PrimitiveType.Sphere,
            surface = PropSpawnSurface.FreeSpace,
            minScale = new Vector3(0.15f, 0.15f, 0.15f),
            maxScale = new Vector3(0.35f, 0.35f, 0.35f),
            color = new Color(0.92f, 0.62f, 0.34f, 1f)
        });
    }

    public bool TryGetRandomEntry(System.Random random, out PropCatalogEntry entry)
    {
        entry = null;

        IReadOnlyList<PropCatalogEntry> activeEntries = GetActiveEntries();
        if (activeEntries == null || activeEntries.Count == 0)
        {
            return false;
        }

        int index = random.Next(0, activeEntries.Count);
        entry = activeEntries[index];
        return entry != null;
    }

    private IReadOnlyList<PropCatalogEntry> GetActiveEntries()
    {
        if (useCuratedDefaultEntries)
        {
            return CuratedDefaults;
        }

        return entries;
    }
}
