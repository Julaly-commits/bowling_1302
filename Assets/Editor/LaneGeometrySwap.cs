using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Swaps the placeholder lane floor in Scenes01 for League Night geometry:
/// Cube becomes lane_section. The box GameObject, its transform and its
/// BoxCollider are left untouched, so collisions and gameplay tuning are
/// exactly what they were - only the visible mesh changes. The side walls stay
/// as plain boxes and are restored if an earlier run had swapped them. The
/// scene is left dirty on purpose; save it yourself.
/// </summary>
static class LaneGeometrySwap
{
    const string PrefabFolder = "Assets/ArtAssets/LeagueNight/Prefabs";
    const string VisualName = "Visual";
    const string TargetSceneName = "Scenes01";
    const string RunOnceKey = "LaneGeometrySwap.applied.v2";
    const string WallMaterialPath = "Assets/Materials/matCushion.mat";

    enum Anchor
    {
        /// <summary>Top of the mesh meets the top of the box - what a floor needs.</summary>
        Top,

        /// <summary>Base of the mesh meets the bottom of the box.</summary>
        Bottom,
    }

    struct Swap
    {
        public string TargetPath;
        public string PrefabName;
        public Anchor Anchor;
        public bool MirrorAroundY;
    }

    static readonly Swap[] Swaps =
    {
        new Swap
        {
            TargetPath = "Ground/Cube",
            PrefabName = "lane_section",
            Anchor = Anchor.Top,
            MirrorAroundY = false,
        },
    };

    /// <summary>
    /// Boxes that must stay plain boxes. An earlier version of this script also
    /// swapped the side walls, so undo that if it happened.
    /// </summary>
    static readonly string[] KeepAsBox =
    {
        "Ground/Cube1",
        "Ground/Cube1 (1)",
    };

    [MenuItem("Tools/Bowling/Swap Lane Boxes For League Night Geometry")]
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
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != TargetSceneName)
        {
            Debug.LogWarning($"[LaneGeometrySwap] Active scene is '{scene.name}', expected " +
                             $"'{TargetSceneName}'. Open it and run " +
                             "Tools > Bowling > Swap Lane Boxes For League Night Geometry.");
            return;
        }

        int done = 0;
        foreach (var swap in Swaps)
        {
            if (ApplyOne(swap))
                done++;
        }

        int restored = 0;
        foreach (var path in KeepAsBox)
        {
            if (RestoreBox(path))
                restored++;
        }

        if (done > 0 || restored > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[LaneGeometrySwap] Swapped {done} of {Swaps.Length} box(es), " +
                      $"restored {restored} wall(s). " +
                      "Scene is dirty - save it if you want to keep the change.");
        }
    }

    /// <summary>
    /// Puts a box back the way it was: no prop child, and the built-in cube mesh
    /// with the cushion material back on the renderer.
    /// </summary>
    static bool RestoreBox(string targetPath)
    {
        var target = GameObject.Find(targetPath);
        if (target == null)
            return false;

        bool changed = false;

        var visual = target.transform.Find(VisualName);
        if (visual != null)
        {
            Undo.DestroyObjectImmediate(visual.gameObject);
            changed = true;
        }

        var filter = target.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = Undo.AddComponent<MeshFilter>(target);
            changed = true;
        }

        if (filter.sharedMesh == null)
        {
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            changed = true;
        }

        var renderer = target.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = Undo.AddComponent<MeshRenderer>(target);
            changed = true;
        }

        if (renderer.sharedMaterial == null)
        {
            renderer.sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
            changed = true;
        }

        if (changed)
            Debug.Log($"[LaneGeometrySwap] Restored '{targetPath}' to a plain box.");

        return changed;
    }

    static bool ApplyOne(Swap swap)
    {
        var target = GameObject.Find(swap.TargetPath);
        if (target == null)
        {
            Debug.LogWarning($"[LaneGeometrySwap] '{swap.TargetPath}' not found in the scene.");
            return false;
        }

        var box = target.GetComponent<BoxCollider>();
        if (box == null)
        {
            Debug.LogWarning($"[LaneGeometrySwap] '{swap.TargetPath}' has no BoxCollider to fit against.");
            return false;
        }

        var prefab = LoadPrefab(swap.PrefabName);
        if (prefab == null)
        {
            Debug.LogError($"[LaneGeometrySwap] Prefab '{swap.PrefabName}' not found in {PrefabFolder}. " +
                           "Run Tools > Bowling > Extract League Night Props first.");
            return false;
        }

        // Rebuild from scratch so re-running never doubles up the geometry.
        var previous = target.transform.Find(VisualName);
        if (previous != null)
            Undo.DestroyObjectImmediate(previous.gameObject);

        var renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null)
            Undo.DestroyObjectImmediate(renderer);

        var filter = target.GetComponent<MeshFilter>();
        if (filter != null)
            Undo.DestroyObjectImmediate(filter);

        var propBounds = LocalBounds(prefab);
        if (propBounds.size.x <= Mathf.Epsilon || propBounds.size.z <= Mathf.Epsilon)
        {
            Debug.LogError($"[LaneGeometrySwap] '{swap.PrefabName}' bounds are degenerate.");
            return false;
        }

        var boxBounds = box.bounds;

        // A container that cancels the box's non-uniform scale, so everything
        // below it can be positioned and sized in plain world units.
        var container = new GameObject(VisualName);
        Undo.RegisterCreatedObjectUndo(container, "Swap Lane Geometry");
        container.transform.SetParent(target.transform, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Invert(target.transform.lossyScale);

        // Scale by width only - stretching a lane to fit would smear the texture,
        // so the length is covered by repeating the prop instead.
        float scale = boxBounds.size.x / propBounds.size.x;
        float tileLength = propBounds.size.z * scale;
        int tiles = Mathf.Max(1, Mathf.RoundToInt(boxBounds.size.z / tileLength));

        // Absorb the remainder into a small length stretch so the run ends exactly
        // where the collider does.
        float lengthCorrection = boxBounds.size.z / (tiles * tileLength);
        var tileScale = new Vector3(scale, scale, scale * lengthCorrection);
        float step = tileLength * lengthCorrection;

        float scaledHeight = propBounds.size.y * scale;
        float baseY = swap.Anchor == Anchor.Top
            ? boxBounds.max.y - scaledHeight
            : boxBounds.min.y;

        for (int i = 0; i < tiles; i++)
        {
            var tile = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
            tile.name = $"{swap.PrefabName}_{i:00}";
            tile.transform.localScale = tileScale;
            tile.transform.localRotation = swap.MirrorAroundY
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;

            // Place by bounds rather than by pivot, since the pack mixes
            // base_center and world_origin pivots.
            var scaledCenter = Vector3.Scale(propBounds.center, tileScale);
            if (swap.MirrorAroundY)
                scaledCenter = new Vector3(-scaledCenter.x, scaledCenter.y, -scaledCenter.z);

            float wantedCenterZ = boxBounds.min.z + step * (i + 0.5f);
            float wantedCenterY = baseY + scaledHeight * 0.5f;

            var wantedWorldCenter = new Vector3(boxBounds.center.x, wantedCenterY, wantedCenterZ);
            tile.transform.position = wantedWorldCenter - scaledCenter;

            Undo.RegisterCreatedObjectUndo(tile, "Swap Lane Geometry");
        }

        Debug.Log($"[LaneGeometrySwap] '{swap.TargetPath}' -> {swap.PrefabName}: " +
                  $"prop {propBounds.size.x:0.###} x {propBounds.size.y:0.###} x {propBounds.size.z:0.###} m, " +
                  $"box {boxBounds.size.x:0.###} x {boxBounds.size.y:0.###} x {boxBounds.size.z:0.###}, " +
                  $"scale {scale:0.###}, {tiles} tile(s), visible height {scaledHeight:0.###}.");

        return true;
    }

    static Vector3 Invert(Vector3 v)
    {
        return new Vector3(
            Mathf.Approximately(v.x, 0f) ? 1f : 1f / v.x,
            Mathf.Approximately(v.y, 0f) ? 1f : 1f / v.y,
            Mathf.Approximately(v.z, 0f) ? 1f : 1f / v.z);
    }

    static GameObject LoadPrefab(string name)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
    }

    static Bounds LocalBounds(GameObject prefab)
    {
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            probe.transform.position = Vector3.zero;
            probe.transform.rotation = Quaternion.identity;
            probe.transform.localScale = Vector3.one;

            var renderers = probe.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }
        finally
        {
            Object.DestroyImmediate(probe);
        }
    }
}
