using System;
using System.Collections.Generic;
using UnityEngine;

public enum SimplePropSurface
{
    Floor,
    Ceiling,
    Table
}

public enum SimplePropKind
{
    RectShort,
    RectTall,
    HumanBlob,
    CeilingShort,
    CeilingLong,
    TableBlock
}

[Serializable]
public class SimplePropSpec
{
    public string id;
    public SimplePropKind kind;
    public SimplePropSurface surface;
    public Vector3 minScale = Vector3.one * 0.25f;
    public Vector3 maxScale = Vector3.one * 0.5f;
    public Color color = Color.gray;
    public GameObject prefab;
    public bool ignoreRoomBounds;
    public bool useCustomSpawnArea;
    public Vector2 spawnAreaMin;
    public Vector2 spawnAreaMax;
}

[Serializable]
public class SimpleSceneSummary
{
    public int sampleIndex = -1;
    public int robotSeed = -1;
    public int propSeed = -1;
    public bool accepted;
    public string status = "";
    public string rejectionReason = "";
    public int requestedProps;
    public int placedProps;
    public int rejectedProps;
    public List<string> placedPropIds = new List<string>();
}
