using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SimpleRandomizationExport
{
    public string profile;
    public bool poseValid;
    public int robotAttempts;
    public int robotAttemptLimit;
    public int requestedProps;
    public int placedProps;
    public int rejectedProps;
    public int robotSeed;
    public int propSeed;
    public List<string> placedPropIds = new List<string>();
}

[Serializable]
public class SimpleVoxelMetadataExport
{
    public string fileName;
    public int sizeX;
    public int sizeY;
    public int sizeZ;
    public Vector3 origin;
    public Vector3 voxelSize;
    public int occupiedVoxels;
    public bool propsOnly;
    public string storageOrder = "x + sizeX * (z + sizeZ * y)";
}

[Serializable]
public class SimpleSampleMetadataExport
{
    public int sampleIndex;
    public string sampleName;
    public string timestampUtc;
    public string poseProfile;
    public int robotSeed;
    public int propSeed;
    public DepthMetadataExport depth;
    public RgbMetadataExport rgb;
    public RgbMetadataExport sideRgb;
    public LegacyRobotPoseExport robot;
    public CanonicalRobotStateExport robotState;
    public SimpleRandomizationExport randomization;
    public SceneObjectsExport sceneObjects;
    public SimpleVoxelMetadataExport voxel;
    public SimpleVoxelMetadataExport voxelScene;
}
