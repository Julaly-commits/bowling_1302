using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the Reset button on top of the existing Shoot button and wires the
/// shot flow together: PinRack on the pins, ShotButtons on the panel, and the
/// Bowling component hooked to both buttons.
/// </summary>
static class ShotUISetup
{
    const string TargetSceneName = "Scenes01";
    const string ShootButtonPath = "Canvas/Panel/Button";
    const string PinsPath = "Pins";
    const string ResetButtonName = "ResetButton";
    const string ResetLabel = "Reset";
    const string RunOnceKey = "ShotUISetup.applied.v1";

    [MenuItem("Tools/Bowling/Set Up Shoot And Reset Buttons")]
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
            Debug.LogWarning($"[ShotUISetup] Active scene is '{scene.name}', expected " +
                             $"'{TargetSceneName}'. Open it and run " +
                             "Tools > Bowling > Set Up Shoot And Reset Buttons.");
            return;
        }

        var bowling = Object.FindAnyObjectByType<Bowling>();
        if (bowling == null)
        {
            Debug.LogError("[ShotUISetup] No Bowling component in the scene.");
            return;
        }

        var shootObject = GameObject.Find(ShootButtonPath);
        if (shootObject == null)
        {
            Debug.LogError($"[ShotUISetup] '{ShootButtonPath}' not found in the scene.");
            return;
        }

        var shootButton = shootObject.GetComponent<Button>();
        if (shootButton == null)
        {
            Debug.LogError($"[ShotUISetup] '{ShootButtonPath}' has no Button component.");
            return;
        }

        var panel = shootObject.transform.parent;

        // The shipped wiring points at a null target, so it never fired.
        // ShotButtons hooks the clicks up in code instead.
        ClearPersistentCalls(shootButton);

        var resetButton = FindOrCreateResetButton(panel, shootObject);
        ClearPersistentCalls(resetButton);

        var pinRack = SetUpPinRack();
        if (pinRack != null)
            AssignReference(bowling, "pinRack", pinRack);

        var shotButtons = panel.GetComponent<ShotButtons>();
        if (shotButtons == null)
            shotButtons = Undo.AddComponent<ShotButtons>(panel.gameObject);

        AssignReference(shotButtons, "bowling", bowling);
        AssignReference(shotButtons, "shootButton", shootButton);
        AssignReference(shotButtons, "resetButton", resetButton);

        // Before the first throw only Shoot is on screen.
        Undo.RecordObject(shootObject, "Set Up Shot Buttons");
        shootObject.SetActive(true);
        Undo.RecordObject(resetButton.gameObject, "Set Up Shot Buttons");
        resetButton.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[ShotUISetup] Shoot and Reset buttons wired. " +
                  "Scene is dirty - save it if you want to keep the change.");
    }

    static Button FindOrCreateResetButton(Transform panel, GameObject shootObject)
    {
        var existing = panel.Find(ResetButtonName);
        if (existing != null)
            return existing.GetComponent<Button>();

        // Cloning the Shoot button is the cheapest way to inherit its exact
        // look, size and placement.
        var clone = Object.Instantiate(shootObject, panel);
        clone.name = ResetButtonName;
        Undo.RegisterCreatedObjectUndo(clone, "Set Up Shot Buttons");

        var source = (RectTransform)shootObject.transform;
        var rect = (RectTransform)clone.transform;
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        rect.sizeDelta = source.sizeDelta;
        rect.anchoredPosition = source.anchoredPosition;
        rect.localRotation = source.localRotation;
        rect.localScale = source.localScale;

        var label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.text = ResetLabel;

        return clone.GetComponent<Button>();
    }

    static PinRack SetUpPinRack()
    {
        var pins = GameObject.Find(PinsPath);
        if (pins == null)
        {
            Debug.LogWarning($"[ShotUISetup] '{PinsPath}' not found - pins will not be reset.");
            return null;
        }

        var rack = pins.GetComponent<PinRack>();
        if (rack == null)
            rack = Undo.AddComponent<PinRack>(pins);

        return rack;
    }

    static void ClearPersistentCalls(Button button)
    {
        if (button == null)
            return;

        var so = new SerializedObject(button);
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        if (calls != null && calls.arraySize > 0)
        {
            calls.ClearArray();
            so.ApplyModifiedProperties();
        }
    }

    static void AssignReference(Object owner, string fieldName, Object value)
    {
        var so = new SerializedObject(owner);
        var property = so.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning($"[ShotUISetup] '{owner.GetType().Name}' has no serialized field '{fieldName}'.");
            return;
        }

        property.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}
