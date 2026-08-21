using UnityEditor;
using UnityEngine;

/// <summary>
/// Replaces the built-in capsule mesh on Pin.prefab with the imported
/// "Pin Low Poly.fbx" model, scaled to the existing CapsuleCollider height and
/// centered on it so physics and gameplay tuning stay exactly as they were.
/// </summary>
static class PinPrefabMeshSetup
{
    const string PinPrefabPath = "Assets/Prefabs/Pin.prefab";
    const string PinModelPath = "Assets/ArtAssets/Bowling/Pin Low Poly.fbx";
    const string MeshChildName = "PinMesh";
    const string RunOnceKey = "PinPrefabMeshSetup.applied.v1";

    [MenuItem("Tools/Bowling/Attach Pin Mesh To Pin Prefab")]
    public static void Run()
    {
        Apply();
    }

    [InitializeOnLoadMethod]
    static void AutoRunOnce()
    {
        if (EditorPrefs.GetBool(RunOnceKey, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(RunOnceKey, false))
                return;

            EditorPrefs.SetBool(RunOnceKey, true);
            Apply();
        };
    }

    static void Apply()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(PinModelPath);
        if (model == null)
        {
            Debug.LogError($"[PinPrefabMeshSetup] Model not found at {PinModelPath}");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PinPrefabPath) == null)
        {
            Debug.LogError($"[PinPrefabMeshSetup] Prefab not found at {PinPrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PinPrefabPath);
        try
        {
            var capsule = root.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                Debug.LogError("[PinPrefabMeshSetup] Pin.prefab has no CapsuleCollider to size the mesh against.");
                return;
            }

            // Drop any previous run's child so re-running is idempotent.
            var existing = root.transform.Find(MeshChildName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            // The placeholder capsule renderer on the root is no longer needed.
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null)
                Object.DestroyImmediate(renderer);

            var filter = root.GetComponent<MeshFilter>();
            if (filter != null)
                Object.DestroyImmediate(filter);

            var child = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            child.name = MeshChildName;
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            if (!TryGetWorldBounds(child, out var bounds))
            {
                Debug.LogError("[PinPrefabMeshSetup] Pin Low Poly.fbx has no renderers to measure.");
                return;
            }

            // A pin is defined by its height, so match the collider along the
            // capsule's own axis rather than by overall bounding size.
            float modelHeight = AxisSize(bounds, capsule.direction);
            if (modelHeight <= Mathf.Epsilon)
            {
                Debug.LogError("[PinPrefabMeshSetup] Pin Low Poly.fbx bounds are degenerate.");
                return;
            }

            var lossy = root.transform.lossyScale;
            float axisScale = Mathf.Abs(AxisValue(lossy, capsule.direction));
            float targetHeight = capsule.height * axisScale;
            child.transform.localScale = Vector3.one * (targetHeight / modelHeight);

            // Re-measure after scaling, then slide the mesh onto the collider center.
            TryGetWorldBounds(child, out bounds);
            child.transform.position += root.transform.TransformPoint(capsule.center) - bounds.center;

            PrefabUtility.SaveAsPrefabAsset(root, PinPrefabPath);
            Debug.Log($"[PinPrefabMeshSetup] Pin mesh attached to {PinPrefabPath} " +
                      $"(child scale {child.transform.localScale.x:0.####}, " +
                      $"world height {targetHeight:0.###}).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static float AxisSize(Bounds bounds, int direction)
    {
        return AxisValue(bounds.size, direction);
    }

    static float AxisValue(Vector3 v, int direction)
    {
        switch (direction)
        {
            case 0: return v.x;
            case 2: return v.z;
            default: return v.y;
        }
    }

    static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return false;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return true;
    }
}
