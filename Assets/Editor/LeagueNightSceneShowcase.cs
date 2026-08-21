using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Drops every extracted League Night prop into the scene as a labelled grid,
/// parked well clear of the lane so the props can be browsed and picked. The
/// scene is left dirty on purpose - nothing is saved until you press Ctrl+S.
/// </summary>
static class LeagueNightSceneShowcase
{
    const string PrefabFolder = "Assets/ArtAssets/LeagueNight/Prefabs";
    const string ShowcaseName = "LeagueNight_Showcase";
    const string TargetSceneName = "Scenes01";
    const string RunOnceKey = "LeagueNightSceneShowcase.applied.v1";

    // The lane occupies roughly x -5..5, z -12.5..12.5, so park the grid to the side.
    static readonly Vector3 ShowcaseOrigin = new Vector3(40f, 0f, -12f);
    const int Columns = 5;
    const float MinSpacing = 4f;
    const float Padding = 2f;

    [MenuItem("Tools/Bowling/Show League Night Props In Scene")]
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
            Debug.LogWarning($"[LeagueNightSceneShowcase] Active scene is '{scene.name}', " +
                             $"expected '{TargetSceneName}'. Open it and run " +
                             "Tools > Bowling > Show League Night Props In Scene.");
            return;
        }

        var prefabs = LoadPrefabs();
        if (prefabs.Count == 0)
        {
            // The extractor may not have produced the prefabs yet this session.
            LeagueNightPropExtractor.Run();
            prefabs = LoadPrefabs();
        }

        if (prefabs.Count == 0)
        {
            Debug.LogError($"[LeagueNightSceneShowcase] No prefabs found in {PrefabFolder}");
            return;
        }

        // Rebuild from scratch so re-running never stacks two grids on top of each other.
        var previous = GameObject.Find(ShowcaseName);
        if (previous != null)
            Undo.DestroyObjectImmediate(previous);

        var root = new GameObject(ShowcaseName);
        Undo.RegisterCreatedObjectUndo(root, "Show League Night Props");
        root.transform.position = ShowcaseOrigin;

        // One spacing for the whole grid keeps it readable, so size it off the
        // widest prop rather than letting big pieces overlap their neighbours.
        float spacing = MinSpacing;
        var footprints = new List<Bounds>();
        foreach (var prefab in prefabs)
        {
            var bounds = LocalBounds(prefab);
            footprints.Add(bounds);
            spacing = Mathf.Max(spacing, Mathf.Max(bounds.size.x, bounds.size.z) + Padding);
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene);
            instance.transform.SetParent(root.transform, false);

            int column = i % Columns;
            int row = i / Columns;
            var bounds = footprints[i];

            instance.transform.localPosition = new Vector3(
                column * spacing - bounds.center.x,
                -bounds.min.y,
                row * spacing - bounds.center.z);

            Undo.RegisterCreatedObjectUndo(instance, "Show League Night Props");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        Debug.Log($"[LeagueNightSceneShowcase] Placed {prefabs.Count} props under " +
                  $"'{ShowcaseName}' at {ShowcaseOrigin}, spacing {spacing:0.##} m. " +
                  "Scene is dirty - save it if you want to keep the layout.");
    }

    static List<GameObject> LoadPrefabs()
    {
        var result = new List<GameObject>();
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            return result;

        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
        var paths = new List<string>();
        foreach (var guid in guids)
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));

        paths.Sort((a, b) => string.CompareOrdinal(
            Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b)));

        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                result.Add(prefab);
        }

        return result;
    }

    /// <summary>
    /// Bounds of the prefab expressed relative to its own root, measured from a
    /// throwaway instance because prefab assets have no world transform.
    /// </summary>
    static Bounds LocalBounds(GameObject prefab)
    {
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            probe.transform.position = Vector3.zero;
            probe.transform.rotation = Quaternion.identity;

            var renderers = probe.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one);

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
