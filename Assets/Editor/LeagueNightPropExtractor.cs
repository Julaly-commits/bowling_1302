using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Splits the League Night pack.fbx into one prefab per prop so each can be
/// picked and placed on its own. Every prefab gets an identity-transform root
/// with the model geometry as a child, so the authored pivot lands on the
/// prefab origin and dragging one into a scene needs no fixups.
/// </summary>
static class LeagueNightPropExtractor
{
    const string PackModelPath = "Assets/ArtAssets/LeagueNight/pack.fbx";
    const string OutputFolder = "Assets/ArtAssets/LeagueNight/Prefabs";
    const string RunOnceKey = "LeagueNightPropExtractor.applied.v1";

    [MenuItem("Tools/Bowling/Extract League Night Props")]
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
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(PackModelPath);
        if (model == null)
        {
            Debug.LogError($"[LeagueNightPropExtractor] Model not found at {PackModelPath}");
            return;
        }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/ArtAssets/LeagueNight", "Prefabs");

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                                           InteractionMode.AutomatedAction);

        var props = new List<Transform>();
        foreach (Transform child in instance.transform)
            props.Add(child);

        var created = new List<string>();
        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var prop in props)
            {
                if (prop.GetComponentInChildren<Renderer>(true) == null)
                    continue;

                var holder = new GameObject(prop.name);

                // Keep the world transform so the FBX axis conversion survives,
                // then drop the prop onto its own pivot at the prefab origin.
                prop.SetParent(holder.transform, true);
                prop.localPosition = Vector3.zero;

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{OutputFolder}/{prop.name}.prefab");
                PrefabUtility.SaveAsPrefabAsset(holder, path);
                created.Add(path);

                Object.DestroyImmediate(holder);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            Object.DestroyImmediate(instance);
            AssetDatabase.Refresh();
        }

        Debug.Log($"[LeagueNightPropExtractor] Created {created.Count} prop prefabs in {OutputFolder}");
    }
}
