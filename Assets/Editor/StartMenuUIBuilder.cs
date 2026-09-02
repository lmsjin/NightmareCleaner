using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StartMenuUIBuilder
{
    private const string ScenePath = "Assets/Scenes/start.unity";
    private const string BackgroundPath = "Assets/Resources/UI/MainMenu/bg_con.png";
    private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

    [MenuItem("NightmareCleaner/UI/Build Start Menu")]
    public static void BuildStartMenu()
    {
        SBFontSetup.EnsureFont();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveExistingMenuObjects();
        StartMenuController controller = new GameObject("StartMenuController").AddComponent<StartMenuController>();
        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        Image background = CreateImage("Background", canvas.transform, AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath));
        Stretch(background.rectTransform);
        RectTransform mainPanel = CreatePanel("MainPanel", canvas.transform, true);
        RectTransform settingsPanel = CreatePanel("SettingsPanel", canvas.transform, false);

        RectTransform buttons = CreateRect("MainButtons", mainPanel);
        SetRect(buttons, new Vector2(510f, -80f), new Vector2(380f, 430f));
        VerticalLayoutGroup layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        Button play = GameCanvasUIBuilder.CreateSpriteButton("PlayButton", buttons, "play2.png", Vector2.zero, new Vector2(360f, 123f));
        Button option = GameCanvasUIBuilder.CreateSpriteButton("OptionButton", buttons, "opt2.png", Vector2.zero, new Vector2(360f, 126f));
        Button exit = GameCanvasUIBuilder.CreateSpriteButton("ExitButton", buttons, "exit2.png", Vector2.zero, new Vector2(360f, 125f));
        play.gameObject.AddComponent<LayoutElement>().preferredHeight = 123f;
        option.gameObject.AddComponent<LayoutElement>().preferredHeight = 126f;
        exit.gameObject.AddComponent<LayoutElement>().preferredHeight = 125f;
        UnityEventTools.AddPersistentListener(play.onClick, controller.StartGame);
        UnityEventTools.AddPersistentListener(option.onClick, controller.ShowSettings);
        UnityEventTools.AddPersistentListener(exit.onClick, controller.QuitGame);

        Image optionPanel = GameCanvasUIBuilder.CreateSpriteImage("OptionPanel", settingsPanel, "OptionPanel.png", new Vector2(115f, 0f), new Vector2(920f, 640f));
        RectTransform tabs = CreateRect("OptionTabs", settingsPanel);
        SetRect(tabs, new Vector2(-505f, 15f), new Vector2(270f, 420f));
        Button soundTab = GameCanvasUIBuilder.CreateSpriteButton("SoundTab", tabs, "OptionTabButton_SoundON.png", new Vector2(0f, 150f), new Vector2(270f, 80f));
        Button resolutionTab = GameCanvasUIBuilder.CreateSpriteButton("ResolutionTab", tabs, "OptionTabButton_ResolutionOFF.png", new Vector2(0f, 20f), new Vector2(270f, 80f));
        Button controlTab = GameCanvasUIBuilder.CreateSpriteButton("ControlTab", tabs, "OptionTabButton_ControlOFF.png", new Vector2(0f, -110f), new Vector2(270f, 80f));

        RectTransform soundPanel = CreatePanel("SoundOptionsPanel", optionPanel.transform, true);
        GameCanvasUIBuilder.CreateSpriteImage("Title", soundPanel, "OptionPanelTitleSound.png", new Vector2(0f, 230f), new Vector2(210f, 70f));
        CreateText("VolumeLabel", soundPanel, "전체 음량", new Vector2(-245f, 80f), new Vector2(220f, 55f), 30f);
        TMP_Text volumeValue = CreateText("VolumeValue", soundPanel, "100%", new Vector2(300f, 80f), new Vector2(130f, 55f), 28f);
        Slider volumeSlider = CreateSlider(soundPanel, new Vector2(55f, 15f));

        RectTransform resolutionPanel = GameCanvasUIBuilder.CreatePlaceholderOptionPanel("ResolutionOptionsPanel", optionPanel.transform, "OptionTabButton_ResolutionON.png");
        CreateText("ResolutionLabel", resolutionPanel, "해상도", new Vector2(-230f, 110f), new Vector2(220f, 55f), 30f);
        TMP_Dropdown resolutionDropdown = GameCanvasUIBuilder.CreateOptionDropdown("ResolutionDropdown", resolutionPanel, new Vector2(110f, 110f));
        CreateText("FullscreenLabel", resolutionPanel, "전체화면", new Vector2(-230f, 0f), new Vector2(220f, 55f), 30f);
        Toggle fullscreenToggle = GameCanvasUIBuilder.CreateOptionToggle("FullscreenToggle", resolutionPanel, new Vector2(110f, 0f));
        CreateText("FrameRateLabel", resolutionPanel, "프레임", new Vector2(-230f, -110f), new Vector2(220f, 55f), 30f);
        TMP_Dropdown frameRateDropdown = GameCanvasUIBuilder.CreateOptionDropdown("FrameRateDropdown", resolutionPanel, new Vector2(110f, -110f));
        RectTransform controlPanel = GameCanvasUIBuilder.CreatePlaceholderOptionPanel("ControlOptionsPanel", optionPanel.transform, "OptionPanelTitleControl.png");
        GameCanvasUIBuilder.CreateSpriteImage("KeyGuide", controlPanel, "KeyGuideImage.png", new Vector2(0f, 20f), new Vector2(620f, 268f));
        CreateText("MouseSensitivityLabel", controlPanel, "마우스 감도", new Vector2(-220f, -165f), new Vector2(230f, 50f), 28f);
        TMP_Text sensitivityValue = CreateText("MouseSensitivityValue", controlPanel, "1.0", new Vector2(315f, -165f), new Vector2(100f, 50f), 26f);
        Slider sensitivitySlider = CreateSlider(controlPanel, new Vector2(55f, -165f));
        sensitivitySlider.minValue = 0.2f; sensitivitySlider.maxValue = 2f; sensitivitySlider.value = 1f;

        Button apply = GameCanvasUIBuilder.CreateSpriteButton("ApplyButton", settingsPanel, "OptionButton_Apply.png", new Vector2(-150f, -400f), new Vector2(210f, 58f));
        Button reset = GameCanvasUIBuilder.CreateSpriteButton("ResetButton", settingsPanel, "OptionButton_Reset.png", new Vector2(85f, -400f), new Vector2(210f, 58f));
        Button close = GameCanvasUIBuilder.CreateSpriteButton("ExitButton", settingsPanel, "OptionButton_Exit.png", new Vector2(320f, -400f), new Vector2(210f, 58f));

        RectTransform confirm = CreateRect("ResetConfirmDialog", settingsPanel);
        SetRect(confirm, Vector2.zero, new Vector2(940f, 660f));
        GameCanvasUIBuilder.CreateSpriteImage("PopupPanel", confirm, "PopupPanel.png", Vector2.zero, new Vector2(940f, 660f));
        GameCanvasUIBuilder.CreateSpriteImage("Information", confirm, "OptionInformationPopup.png", new Vector2(0f, 20f), new Vector2(640f, 76f));
        Button ok = GameCanvasUIBuilder.CreateSpriteButton("ConfirmButton", confirm, "OptionButton_OK.png", new Vector2(-125f, -170f), new Vector2(210f, 58f));
        Button cancel = GameCanvasUIBuilder.CreateSpriteButton("CancelButton", confirm, "OptionButton_Cancle.png", new Vector2(125f, -170f), new Vector2(210f, 58f));

        UnityEventTools.AddPersistentListener(volumeSlider.onValueChanged, controller.SetMasterVolume);
        UnityEventTools.AddPersistentListener(soundTab.onClick, controller.ShowSoundTab);
        UnityEventTools.AddPersistentListener(resolutionTab.onClick, controller.ShowResolutionTab);
        UnityEventTools.AddPersistentListener(controlTab.onClick, controller.ShowControlTab);
        UnityEventTools.AddPersistentListener(sensitivitySlider.onValueChanged, controller.SetMouseSensitivity);
        UnityEventTools.AddPersistentListener(apply.onClick, controller.ApplyAndClose);
        UnityEventTools.AddPersistentListener(reset.onClick, controller.ShowResetConfirm);
        UnityEventTools.AddPersistentListener(close.onClick, controller.ShowMainMenu);
        UnityEventTools.AddPersistentListener(ok.onClick, controller.ResetSettings);
        UnityEventTools.AddPersistentListener(cancel.onClick, controller.HideResetConfirm);

        Assign(controller, "mainPanel", mainPanel.gameObject, "settingsPanel", settingsPanel.gameObject,
            "masterVolumeSlider", volumeSlider, "masterVolumeValueText", volumeValue, "fullscreenToggle", fullscreenToggle,
            "resolutionDropdown", resolutionDropdown, "frameRateDropdown", frameRateDropdown,
            "soundOptionsPanel", soundPanel.gameObject,
            "resolutionOptionsPanel", resolutionPanel.gameObject, "controlOptionsPanel", controlPanel.gameObject,
            "resetConfirmPanel", confirm.gameObject, "mouseSensitivitySlider", sensitivitySlider,
            "mouseSensitivityValueText", sensitivityValue,
            "soundTabImage", soundTab.image, "resolutionTabImage", resolutionTab.image, "controlTabImage", controlTab.image,
            "soundTabOn", GameCanvasUIBuilder.LoadOptionSprite("OptionTabButton_SoundON.png"),
            "soundTabOff", GameCanvasUIBuilder.LoadOptionSprite("OptionTabButton_SoundOFF.png"),
            "resolutionTabOn", GameCanvasUIBuilder.LoadOptionSprite("OptionTabButton_ResolutionON.png"),
            "resolutionTabOff", GameCanvasUIBuilder.LoadOptionSprite("OptionTabButton_ResolutionOFF.png"),
            "controlTabOn", GameCanvasUIBuilder.LoadOptionSprite("OptionTabButton_ControlON.png"),
            "controlTabOff", GameCanvasUIBuilder.LoadOptionSprite("OptionTabButton_ControlOFF.png"));
        resolutionPanel.gameObject.SetActive(false);
        controlPanel.gameObject.SetActive(false);
        confirm.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = canvas.gameObject;
    }

    private static Slider CreateSlider(Transform parent, Vector2 position)
    {
        RectTransform root = CreateRect("MasterVolumeSlider", parent);
        SetRect(root, position, new Vector2(470f, 64f));
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
        Image background = GameCanvasUIBuilder.CreateSpriteImage("BarGauge", root, "BarGauge.png", Vector2.zero, new Vector2(470f, 64f));
        Image fill = GameCanvasUIBuilder.CreateSpriteImage("Fill", root, "BarGauge.png", Vector2.zero, new Vector2(470f, 64f));
        Image handle = GameCanvasUIBuilder.CreateSpriteImage("Handle", root, "SoundBarAnchor.png", Vector2.zero, new Vector2(37f, 64f));
        slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        return slider;
    }

    private static Canvas CreateCanvas()
    {
        GameObject go = new("StartMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution; scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static RectTransform CreatePanel(string name, Transform parent, bool active)
    {
        RectTransform panel = CreateRect(name, parent); Stretch(panel); panel.gameObject.SetActive(active); return panel;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        RectTransform rect = CreateRect(name, parent); Image image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize)
    {
        RectTransform rect = CreateRect(name, parent); SetRect(rect, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = fontSize;
        text.font = SBFontSetup.EnsureFont();
        text.color = Color.white; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform)); RectTransform rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private static void Assign(Object target, params object[] values)
    {
        SerializedObject serialized = new(target);
        for (int i = 0; i < values.Length; i += 2)
            serialized.FindProperty((string)values[i]).objectReferenceValue = values[i + 1] as Object;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateEventSystem()
    {
        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        GameObject go = existing != null ? existing.gameObject : new GameObject("EventSystem", typeof(EventSystem));
        foreach (MonoBehaviour component in go.GetComponents<MonoBehaviour>())
            if (component != null && component.GetType().FullName == "UnityEngine.UIElements.InputSystem.InputSystemEventSystem")
                Object.DestroyImmediate(component);
        System.Type inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputType != null)
        {
            if (go.GetComponent(inputType) == null) go.AddComponent(inputType);
            StandaloneInputModule old = go.GetComponent<StandaloneInputModule>();
            if (old != null) Object.DestroyImmediate(old);
        }
        else if (go.GetComponent<StandaloneInputModule>() == null) go.AddComponent<StandaloneInputModule>();
    }

    private static void RemoveExistingMenuObjects()
    {
        foreach (StartMenuController item in Object.FindObjectsByType<StartMenuController>(FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
        foreach (Canvas item in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
        foreach (EventSystem item in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)) Object.DestroyImmediate(item.gameObject);
    }
}
