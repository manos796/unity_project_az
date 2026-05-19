using System;
using System.Collections.Generic;
using UnityEngine;

public enum PropSpawnSurface
{
    Floor,
    Table,
    FreeSpace,
    Ceiling
}

public enum PropArchetype
{
    LegacyPrimitive,
    RectShort,
    RectTall,
    HumanBlob,
    CeilingLight,
    TableBlock
}

[Serializable]
public class PropCatalogEntry
{
    public string id;
    public string category;
    public PropArchetype archetype = PropArchetype.LegacyPrimitive;
    public PrimitiveType primitiveType = PrimitiveType.Cube;
    public PropSpawnSurface surface = PropSpawnSurface.Floor;
    public Vector3 minScale = Vector3.one * 0.2f;
    public Vector3 maxScale = Vector3.one * 0.4f;
    public Color color = Color.gray;
}

[Serializable]
public class SpawnAttemptRecord
{
    public int attemptIndex;
    public string propId;
    public string category;
    public string status;
    public string rejectionReason;
    public Vector3 position;
    public Vector3 eulerRotation;
    public Vector3 scale;
}

[Serializable]
public class SceneRandomizationReport
{
    public string profile;
    public int seed;
    public int requestedProps;
    public int placedProps;
    public int rejectedProps;
    public List<SpawnAttemptRecord> attempts = new List<SpawnAttemptRecord>();
}

[Serializable]
public class SceneObjectExport
{
    public string objectId;
    public string category;
    public string surface;
    public Vector3 position;
    public Vector3 eulerRotation;
    public Vector3 scale;
    public Vector3 boundsSize;
}

[Serializable]
public class SceneObjectsExport
{
    public List<SceneObjectExport> objects = new List<SceneObjectExport>();
}
