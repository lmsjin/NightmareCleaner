using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StartSceneSettingsCopier
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string StartScenePath = "Assets/Scenes/start.unity";
    private const string TempPrefabPath = "Assets/__TempStartSettingsPanel.prefab";
    private const string BackgroundPath = "Assets/Resources/UI/MainMenu/bg_con.png";
    private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

    [MenuItem("NightmareCleaner/UI/Copy Sample Settings To Start")]
    public static void CopySampleSettingsToStart()
    {
        Scene originalScene = SceneManager.GetActiveScene();
        string originalPath = originalScene.path;

        EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        GameObject sourceSettings = FindSourceSettingsPanel();
        if (sourceSettings == null)
        {
            Debug.LogError("[StartSceneSettingsCopier] Could not find SettingsPanel in SampleScene.");
            RestoreOriginalScene(originalPath);
            return;
        }

        GameObject tempPrefab = PrefabUtility.SaveAsPrefabAsset(sourceSettings, TempPrefabPath);
        if (tempPrefab == null)
        {
            Debug.LogError("[StartSceneSettingsCopier] Could not create temporary SettingsPanel prefab.");
            RestoreOriginalScene(originalPath);
            return;
        }

        Scene startScene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
        RemoveConflictingStartObjects();

        StartMenuController controller = new GameObject("StartMenuController").AddComponent<StartMenuController>();
        Canvas canvas = CreateCanvas();
        GameObject mainPanel = CreateStartMainPanel(canvas.transform, controller);

        GameObject settingsPanel = (GameObject)PrefabUtility.InstantiatePrefab(tempPrefab, startScene);
        settingsPanel.name = "SettingsPanel";
        settingsPanel.transform.SetParent(canvas.transform, false);
        settingsPanel.transform.SetAsLastSibling();
        settingsPanel.SetActive(false);
        PrefabUtility.UnpackPrefabInstance(settingsPanel, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        WireStartController(controller, mainPanel, settingsPanel);
        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(startScene);
        EditorSceneManager.SaveScene(startScene);
        AssetDatabase.DeleteAsset(TempPrefabPath);
        AssetDatabase.Refresh();
        Selection.activeGameObject = settingsPanel;
        Debug.Log("[StartSceneSettingsCopier] Rebuilt start scene UI. Old start UI canvases/controllers were removed, and SampleScene SettingsPanel was wired.");
    }

    private static GameObject FindSourceSettingsPanel()
    {
        PauseMenuController pause = Object.FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);
        if (pause != null)
        {
            SerializedObject serialized = new(pause);
            Object panel = serialized.FindProperty("settingsPanel")?.objectReferenceValue;
            if (panel is GameObject panelObject)
                return panelObject;
        }

        Transform found = FindChildInAllRoots("SettingsPanel");
        return found != null ? found.gameObject : null;
    }

    private static void RemoveConflictingStartObjects()
    {
        HashSet<GameObject> targets = new();

        foreach (StartMenuController item in Object.FindObjectsByType<StartMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item != null) targets.Add(item.gameObject);

        foreach (Canvas item in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item != null) targets.Add(item.gameObject);

        foreach (EventSystem item in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item != null) targets.Add(item.gameObject);

        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "StartMenuController" ||
                root.name == "StartMenuCanvas" ||
                root.name == "SettingsPanel" ||
                root.name == "SettingsPanel_InactiveBackup" ||
                root.name == "MainPanel")
            {
                targets.Add(root);
            }
        }

        foreach (GameObject target in targets)
            if (target != null)
                Object.DestroyImmediate(target);
    }

    private static GameObject CreateStartMainPanel(Transform canvas, StartMenuController controller)
    {
        Image background = CreateImage("Background", canvas, AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath));
        Stretch(background.rectTransform);
        background.raycastTarget = false;

        RectTransform mainPanel = CreateRect("MainPanel", canvas);
        Stretch(mainPanel);
        mainPanel.gameObject.SetActive(true);

        RectTransform buttons = CreateRect("MainButtons", mainPanel);
        SetRect(buttons, new Vector2(510f, -80f), new Vector2(380f, 430f));
        VerticalLayoutGroup layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        Button play = CreateSpriteButton("PlayButton", buttons, "[UI]_MainLoby/Button_Gamestart", new Vector2(360f, 123f));
        Button option = CreateSpriteButton("OptionButton", buttons, "[UI]_MainLoby/Button_Option", new Vector2(360f, 126f));
        Button exit = CreateSpriteButton("ExitButton", buttons, "[UI]_MainLoby/Button_Exit", new Vector2(360f, 125f));

        UnityEventTools.AddPersistentListener(play.onClick, controller.StartGame);
        UnityEventTools.AddPersistentListener(option.onClick, controller.ShowSettings);
        UnityEventTools.AddPersistentListener(exit.onClick, controller.QuitGame);
        return mainPanel.gameObject;
    }

    private static void WireStartController(StartMenuController controller, GameObject mainPanel, GameObject settingsPanel)
    {
        GameObject soundOptions = FindChild(settingsPanel.transform, "SoundOptionsPanel")?.gameObject;
        GameObject resolutionOptions = FindChild(settingsPanel.transform, "ResolutionOptionsPanel")?.gameObject;
        GameObject controlOptions = FindChild(settingsPanel.transform, "ControlOptionsPanel")?.gameObject;
        GameObject resetConfirm = FindChild(settingsPanel.transform, "ResetConfirmDialog")?.gameObject;

        Slider masterSlider = FindComponent<Slider>(settingsPanel.transform, "MasterVolumeSlider");
        TMP_Text volumeValue = FindComponent<TMP_Text>(settingsPanel.transform, "VolumeValue");
        Toggle fullscreenToggle = FindComponent<Toggle>(settingsPanel.transform, "FullscreenToggle");
        TMP_Dropdown resolutionDropdown = FindComponent<TMP_Dropdown>(settingsPanel.transform, "ResolutionDropdown");
        TMP_Dropdown frameDropdown = FindComponent<TMP_Dropdown>(settingsPanel.transform, "FrameRateDropdown");
        Slider sensitivitySlider = FindComponent<Slider>(settingsPanel.transform, "MouseSensitivitySlider")
            ?? FindComponent<Slider>(controlOptions != null ? controlOptions.transform : settingsPanel.transform, "MasterVolumeSlider");
        TMP_Text sensitivityValue = FindComponent<TMP_Text>(settingsPanel.transform, "MouseSensitivityValue");

        Button soundTab = FindComponent<Button>(settingsPanel.transform, "SoundTab");
        Button resolutionTab = FindComponent<Button>(settingsPanel.transform, "ResolutionTab");
        Button controlTab = FindComponent<Button>(settingsPanel.transform, "ControlTab");
        Button apply = FindComponent<Button>(settingsPanel.transform, "ApplyButton");
        Button reset = FindComponent<Button>(settingsPanel.transform, "ResetButton");
        Button exit = FindComponent<Button>(settingsPanel.transform, "ExitButton");
        Button confirm = FindComponent<Button>(settingsPanel.transform, "ConfirmButton");
        Button cancel = FindComponent<Button>(settingsPanel.transform, "CancelButton");

        Assign(controller,
            "mainPanel", mainPanel,
            "settingsPanel", settingsPanel,
            "masterVolumeSlider", masterSlider,
            "masterVolumeValueText", volumeValue,
            "fullscreenToggle", fullscreenToggle,
            "resolutionDropdown", resolutionDropdown,
            "frameRateDropdown", frameDropdown,
            "soundOptionsPanel", soundOptions,
            "resolutionOptionsPanel", resolutionOptions,
            "controlOptionsPanel", controlOptions,
            "resetConfirmPanel", resetConfirm,
            "mouseSensitivitySlider", sensitivitySlider,
            "mouseSensitivityValueText", sensitivityValue,
            "soundTabImage", soundTab != null ? soundTab.image : null,
            "resolutionTabImage", resolutionTab != null ? resolutionTab.image : null,
            "controlTabImage", controlTab != null ? controlTab.image : null,
            "soundTabOn", UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_SoundON"),
            "soundTabOff", UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_SoundOFF"),
            "resolutionTabOn", UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_GraghicON"),
            "resolutionTabOff", UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_GraghicOFF"),
            "controlTabOn", UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_ControlON"),
            "controlTabOff", UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_ControlOFF"));

        ReplaceSliderListener(masterSlider, controller.SetMasterVolume);
        ReplaceSliderListener(sensitivitySlider, controller.SetMouseSensitivity);
        ReplaceToggleListener(fullscreenToggle, controller.SetFullscreen);
        ClearDropdownListeners(resolutionDropdown);
        ClearDropdownListeners(frameDropdown);
        ReplaceButtonListener(soundTab, controller.ShowSoundTab);
        ReplaceButtonListener(resolutionTab, controller.ShowResolutionTab);
        ReplaceButtonListener(controlTab, controller.ShowControlTab);
        ReplaceButtonListener(apply, controller.ApplyAndClose);
        ReplaceButtonListener(reset, controller.ShowResetConfirm);
        ReplaceButtonListener(exit, controller.ShowMainMenu);
        ReplaceButtonListener(confirm, controller.ResetSettings);
        ReplaceButtonListener(cancel, controller.HideResetConfirm);

        if (soundOptions != null) soundOptions.SetActive(true);
        if (resolutionOptions != null) resolutionOptions.SetActive(false);
        if (controlOptions != null) controlOptions.SetActive(false);
        if (resetConfirm != null) resetConfirm.SetActive(false);
        settingsPanel.SetActive(false);
    }

    private static Canvas CreateCanvas()
    {
        GameObject go = new("StartMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        GameObject go = new("EventSystem", typeof(EventSystem));
        System.Type inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null)
            go.AddComponent(inputType);
        else
            go.AddComponent<StandaloneInputModule>();
    }

    private static Button CreateSpriteButton(string name, Transform parent, string spritePath, Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent);
        SetRect(rect, Vector2.zero, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = UIRealTheme.LoadSprite(spritePath);
        image.color = Color.white;
        image.preserveAspect = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void ReplaceButtonListener(Button button, UnityAction action)
    {
        if (button == null) return;
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
    }

    private static void ReplaceSliderListener(Slider slider, UnityAction<float> action)
    {
        if (slider == null) return;
        slider.onValueChanged = new Slider.SliderEvent();
        UnityEventTools.AddPersistentListener(slider.onValueChanged, action);
        EditorUtility.SetDirty(slider);
    }

    private static void ReplaceToggleListener(Toggle toggle, UnityAction<bool> action)
    {
        if (toggle == null) return;
        toggle.onValueChanged = new Toggle.ToggleEvent();
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, action);
        EditorUtility.SetDirty(toggle);
    }

    private static void ClearDropdownListeners(TMP_Dropdown dropdown)
    {
        if (dropdown == null) return;
        dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        EditorUtility.SetDirty(dropdown);
    }

    private static T FindComponent<T>(Transform root, string name) where T : Component
    {
        Transform child = FindChild(root, name);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindChildInAllRoots(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform found = FindChild(root.transform, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    private static void Assign(Object target, params object[] values)
    {
        SerializedObject serialized = new(target);
        for (int i = 0; i < values.Length; i += 2)
        {
            SerializedProperty property = serialized.FindProperty((string)values[i]);
            if (property != null)
                property.objectReferenceValue = values[i + 1] as Object;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RestoreOriginalScene(string originalPath)
    {
        if (!string.IsNullOrEmpty(originalPath))
            EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
    }
}
