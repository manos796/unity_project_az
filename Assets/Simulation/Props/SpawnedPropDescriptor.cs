using UnityEngine;

/// <summary>
/// Carries stable prop metadata from spawning through later export.
/// </summary>
[DisallowMultipleComponent]
public class SpawnedPropDescriptor : MonoBehaviour
{
    public string propId;
    public string category;
    public string surface;
}
