using UnityEditor;
using UnityEngine;

/// <summary>
/// Replaces the built-in sphere mesh on Bowl.prefab with the imported Ball.fbx
/// model, scaled and centered to match the existing SphereCollider so physics
/// and gameplay tuning stay exactly as they were.
/// </summary>
static class BowlPrefabBallSetup
{
    const string BowlPrefabPath = "Assets/Prefabs/Bowl.prefab";
    const string BallModelPath = "Assets/ArtAssets/Bowling/Ball.fbx";
    const string MeshChildName = "BallMesh";
    const string RunOnceKey = "BowlPrefabBallSetup.applied.v1";

    [MenuItem("Tools/Bowling/Attach Ball Mesh To Bowl Prefab")]
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
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(BallModelPath);
        if (model == null)
        {
            Debug.LogError($"[BowlPrefabBallSetup] Model not found at {BallModelPath}");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BowlPrefabPath) == null)
        {
            Debug.LogError($"[BowlPrefabBallSetup] Prefab not found at {BowlPrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(BowlPrefabPath);
        try
        {
            var sphere = root.GetComponent<SphereCollider>();
            if (sphere == null)
            {
                Debug.LogError("[BowlPrefabBallSetup] Bowl.prefab has no SphereCollider to size the mesh against.");
                return;
            }

            // Drop any previous run's child so re-running is idempotent.
            var existing = root.transform.Find(MeshChildName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            // The placeholder sphere renderer on the root is no longer needed.
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
                Debug.LogError("[BowlPrefabBallSetup] Ball.fbx has no renderers to measure.");
                return;
            }

            // Match the collider's world diameter so the visual and the physics
            // shape agree regardless of what units the FBX was authored in.
            float modelDiameter = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (modelDiameter <= Mathf.Epsilon)
            {
                Debug.LogError("[BowlPrefabBallSetup] Ball.fbx bounds are degenerate.");
                return;
            }

            var lossy = root.transform.lossyScale;
            float rootScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
            float targetDiameter = sphere.radius * 2f * rootScale;
            child.transform.localScale = Vector3.one * (targetDiameter / modelDiameter);

            // Re-measure after scaling, then slide the mesh onto the collider center.
            TryGetWorldBounds(child, out bounds);
            child.transform.position += root.transform.TransformPoint(sphere.center) - bounds.center;

            PrefabUtility.SaveAsPrefabAsset(root, BowlPrefabPath);
            Debug.Log($"[BowlPrefabBallSetup] Ball mesh attached to {BowlPrefabPath} " +
                      $"(child scale {child.transform.localScale.x:0.####}, " +
                      $"world diameter {targetDiameter:0.###}).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
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
