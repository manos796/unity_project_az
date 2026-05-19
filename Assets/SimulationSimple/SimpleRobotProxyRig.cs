using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SimpleRobotProxyRig : MonoBehaviour
{
    [System.Serializable]
    public class ProxyDefinition
    {
        public string name;
        public string linkName;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 size = Vector3.one * 0.25f;
        public bool enabled = true;
    }

    private const string GeneratedRootName = "GeneratedSimpleProxies";

    [Header("Dependencies")]
    public RobotPoseController controller;

    [Header("Proxy Definitions")]
    public List<ProxyDefinition> proxies = new List<ProxyDefinition>
    {
        new ProxyDefinition
        {
            name = "SleeveProxy",
            linkName = "Sleeve",
            localPosition = new Vector3(0.05f, 0f, 0f),
            size = new Vector3(0.78f, 0.34f, 0.34f),
        },
        new ProxyDefinition
        {
            name = "CArcUpperProxy",
            linkName = "CArc",
            localPosition = new Vector3(0f, 0.58f, 0f),
            size = new Vector3(0.92f, 0.22f, 0.22f),
        },
        new ProxyDefinition
        {
            name = "CArcLowerProxy",
            linkName = "CArc",
            localPosition = new Vector3(0f, -0.58f, 0f),
            size = new Vector3(0.92f, 0.22f, 0.22f),
        },
        new ProxyDefinition
        {
            name = "VerBeamProxy",
            linkName = "VerBeam",
            localPosition = new Vector3(0.65f, 0f, 0f),
            size = new Vector3(1.05f, 0.28f, 0.28f),
            enabled = false,
        },
    };

    private readonly List<Collider> builtColliders = new List<Collider>();

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnValidate()
    {
        EnsureBuilt();
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

    [ContextMenu("Rebuild Simple Robot Proxies")]
    public void EnsureBuilt()
    {
        AutoResolveDependencies();
        builtColliders.Clear();

        if (controller == null || controller.robotRoot == null)
        {
            return;
        }

        for (int i = 0; i < proxies.Count; i++)
        {
            ProxyDefinition definition = proxies[i];
            if (definition == null || !definition.enabled)
            {
                continue;
            }

            Transform link = FindNamedTransform(controller.robotRoot.transform, definition.linkName);
            if (link == null)
            {
                continue;
            }

            Transform generatedRoot = link.Find(GeneratedRootName);
            if (generatedRoot == null)
            {
                GameObject generated = new GameObject(GeneratedRootName);
                generated.transform.SetParent(link, false);
                generatedRoot = generated.transform;
            }

            Transform existingProxy = generatedRoot.Find(definition.name);
            GameObject proxyObject;
            if (existingProxy == null)
            {
                proxyObject = new GameObject(definition.name);
                proxyObject.transform.SetParent(generatedRoot, false);
            }
            else
            {
                proxyObject = existingProxy.gameObject;
            }

            proxyObject.transform.localPosition = definition.localPosition;
            proxyObject.transform.localRotation = Quaternion.Euler(definition.localEulerAngles);
            proxyObject.transform.localScale = Vector3.one;

            BoxCollider collider = proxyObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = proxyObject.AddComponent<BoxCollider>();
            }

            collider.isTrigger = true;
            collider.center = Vector3.zero;
            collider.size = definition.size;
            collider.enabled = true;
            builtColliders.Add(collider);
        }
    }

    private void AutoResolveDependencies()
    {
        if (controller == null)
        {
            controller = FindAnyObjectByType<RobotPoseController>();
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
}
