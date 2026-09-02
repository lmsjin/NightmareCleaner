using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameCanvasUIBuilder
{
    private const string InGameScenePath = "Assets/Scenes/SampleScene.unity";
    private const string EndScenePath = "Assets/Scenes/end.unity";
    private const string OptionSpritePath = "Assets/Resources/UI/[UI]_Option/";
    private static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

    [MenuItem("NightmareCleaner/UI/Build All Editable Canvas UI %#u")]
    public static void BuildAll()
    {
        SBFontSetup.EnsureFont();
        StartMenuUIBuilder.BuildStartMenu();
        BuildInGameUI();
        BuildEndUI();
        EditorSceneManager.OpenScene("Assets/Scenes/start.unity", OpenSceneMode.Single);
        Debug.Log("Editable Canvas UI created in start, SampleScene, and end scenes.");
    }

    [MenuItem("NightmareCleaner/UI/Build In-Game Canvas UI")]
    public static void BuildInGameUI()
    {
        Scene scene = EditorSceneManager.OpenScene(InGameScenePath, OpenSceneMode.Single);
        DestroyNamed("HUDCanvas");
        DestroyNamed("PauseCanvas");
        DestroyNamed("AbilityCanvas");
        DestroyNamed("FutureUIPlaceholderCanvas");

        EnsureEventSystem();
        BuildHud();
        BuildPause();
        BuildAbility();
        BuildFuturePlaceholders();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("NightmareCleaner/UI/Build End Canvas UI")]
    public static void BuildEndUI()
    {
        Scene scene = EditorSceneManager.OpenScene(EndScenePath, OpenSceneMode.Single);
        foreach (EndMenuController old in Object.FindObjectsByType<EndMenuController>(FindObjectsSortMode.None))
            Object.DestroyImmediate(old.gameObject);
        DestroyNamed("EndCanvas");
        EnsureEventSystem();

        EndMenuController controller = new GameObject("EndMenuController").AddComponent<EndMenuController>();
        Canvas canvas = CreateCanvas("EndCanvas", 0);
        Image background = CreateImage("Background", canvas.transform, new Color(0.06f, 0.05f, 0.15f, 1f));
        Stretch(background.rectTransform);
        TMP_Text title = CreateText("ResultTitle", canvas.transform, "GAME OVER", new Vector2(0f, 250f), new Vector2(1000f, 130f), 90f);
        TMP_Text score = CreateText("Score", canvas.transform, "점수: 0", new Vector2(0f, 110f), new Vector2(700f, 90f), 50f);
        Button retry = CreateButton("RetryButton", canvas.transform, "다시하기", new Vector2(0f, -20f), new Vector2(420f, 86f));
        Button start = CreateButton("StartButton", canvas.transform, "시작 화면", new Vector2(0f, -125f), new Vector2(420f, 86f));
        Button quit = CreateButton("QuitButton", canvas.transform, "종료", new Vector2(0f, -230f), new Vector2(420f, 86f));
        UnityEventTools.AddPersistentListener(retry.onClick, controller.Retry);
        UnityEventTools.AddPersistentListener(start.onClick, controller.GoToStart);
        UnityEventTools.AddPersistentListener(quit.onClick, controller.QuitGame);
        Assign(controller, "resultTitleText", title, "scoreText", score);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildHud()
    {
        HUDController controller = Object.FindFirstObjectByType<HUDController>();
        if (controller == null)
            controller = new GameObject("HUD").AddComponent<HUDController>();

        Canvas canvas = CreateCanvas("HUDCanvas", 10);
        RectTransform statusGroup = CreateRect("StatusGroup", canvas.transform);
        SetRect(statusGroup, new Vector2(45f, -35f), new Vector2(600f, 120f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        CreateText("HPLabel", statusGroup, "HP", new Vector2(0f, -5f), new Vector2(55f, 34f), 25f, new Vector2(0f, 1f), new Vector2(0f, 1f));
        Image hpBg = CreateImage("HPBarBackground", statusGroup, new Color(0.08f, 0.08f, 0.08f, 0.92f));
        SetRect(hpBg.rectTransform, new Vector2(75f, -8f), new Vector2(390f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        Image hpFill = CreateImage("Placeholder_HPBarFill", hpBg.transform, new Color(0.86f, 0.3f, 0.38f, 1f));
        Stretch(hpFill.rectTransform);
        TMP_Text hearts = CreateText("Hearts", hpBg.transform, "♥♥♥♥♥", Vector2.zero, Vector2.zero, 18f);
        Stretch(hearts.rectTransform);
        hearts.color = new Color(1f, 1f, 1f, 0.01f);
        CreateText("EXPLabel", statusGroup, "EXP", new Vector2(0f, -45f), new Vector2(55f, 34f), 22f, new Vector2(0f, 1f), new Vector2(0f, 1f));

        TMP_Text timer = CreateText("Timer", canvas.transform, "00:00", new Vector2(-55f, -35f), new Vector2(250f, 60f), 42f, Vector2.one, Vector2.one);
        TMP_Text score = CreateText("Score", canvas.transform, "점수: 0", new Vector2(-55f, -100f), new Vector2(300f, 45f), 28f, Vector2.one, Vector2.one);
        score.alignment = TextAlignmentOptions.Right;
        TMP_Text child = CreateText("Child", canvas.transform, "아이: 0", new Vector2(-55f, -145f), new Vector2(300f, 45f), 28f, Vector2.one, Vector2.one);
        child.alignment = TextAlignmentOptions.Right;
        CreateText("Placeholder_Objective", canvas.transform, "목표를 완료하세요", new Vector2(0f, -50f), new Vector2(720f, 60f), 34f, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

        Image xpBg = CreateImage("XpBarBackground", canvas.transform, new Color(0f, 0f, 0f, 0.5f));
        SetRect(xpBg.rectTransform, new Vector2(120f, -80f), new Vector2(390f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        Image xpFill = CreateImage("XpBarFill", xpBg.transform, new Color(0.7f, 0.5f, 1f, 1f));
        Stretch(xpFill.rectTransform);
        TMP_Text xpLabel = CreateText("XpLabel", xpBg.transform, "Lv 1", Vector2.zero, Vector2.zero, 14f);
        Stretch(xpLabel.rectTransform);

        Image suctionBg = CreateImage("SuctionGaugeBackground", canvas.transform, new Color(0f, 0f, 0f, 0.5f));
        SetRect(suctionBg.rectTransform, new Vector2(0f, 50f), new Vector2(420f, 30f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        Image suctionFill = CreateImage("SuctionGaugeFill", suctionBg.transform, new Color(0.3f, 0.85f, 1f, 1f));
        Stretch(suctionFill.rectTransform);
        TMP_Text suctionLabel = CreateText("SuctionGaugeLabel", suctionBg.transform, "탄약", Vector2.zero, Vector2.zero, 22f);
        Stretch(suctionLabel.rectTransform);

        RectTransform wave = CreateRect("Placeholder_Wave", canvas.transform);
        SetRect(wave, new Vector2(0f, 55f), new Vector2(260f, 80f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        CreateText("Label", wave, "WAVE\n5 / 5", Vector2.zero, new Vector2(260f, 80f), 26f);

        Assign(controller, "heartText", hearts, "timerText", timer, "scoreText", score, "childText", child,
            "suctionGaugeFill", suctionFill, "suctionGaugeLabel", suctionLabel, "xpFill", xpFill, "xpLabel", xpLabel);
    }

    private static void BuildPause()
    {
        PauseMenuController controller = Object.FindFirstObjectByType<PauseMenuController>();
        if (controller == null)
            controller = new GameObject("PauseMenu").AddComponent<PauseMenuController>();

        Canvas canvas = CreateCanvas("PauseCanvas", 100);
        RectTransform pausePanel = CreatePanel("PausePanel", canvas.transform, new Color(0f, 0f, 0f, 0.78f));
        RectTransform settingsPanel = CreatePanel("SettingsPanel", canvas.transform, new Color(0f, 0f, 0f, 0.84f));
        CreateText("Title", pausePanel, "일시정지", new Vector2(0f, 260f), new Vector2(700f, 100f), 64f);
        Button resume = CreateButton("ResumeButton", pausePanel, "계속하기", new Vector2(0f, 100f), new Vector2(420f, 86f));
        Button restart = CreateButton("RestartButton", pausePanel, "다시하기", new Vector2(0f, 0f), new Vector2(420f, 86f));
        Button settings = CreateButton("SettingsButton", pausePanel, "설정", new Vector2(0f, -100f), new Vector2(420f, 86f));
        Button exit = CreateButton("ExitButton", pausePanel, "나가기", new Vector2(0f, -200f), new Vector2(420f, 86f));
        UnityEventTools.AddPersistentListener(resume.onClick, controller.ResumeGame);
        UnityEventTools.AddPersistentListener(restart.onClick, controller.RestartGame);
        UnityEventTools.AddPersistentListener(settings.onClick, controller.ShowSettings);
        UnityEventTools.AddPersistentListener(exit.onClick, controller.ExitToStart);

        Image optionPanel = CreateSpriteImage("OptionPanel", settingsPanel, "OptionPanel.png", new Vector2(115f, 0f), new Vector2(920f, 640f));
        optionPanel.raycastTarget = false;

        RectTransform tabGroup = CreateRect("OptionTabs", settingsPanel);
        SetRect(tabGroup, new Vector2(-505f, 15f), new Vector2(270f, 420f));
        Button soundTab = CreateSpriteButton("SoundTab", tabGroup, "OptionTabButton_SoundON.png", new Vector2(0f, 150f), new Vector2(270f, 80f));
        Button resolutionTab = CreateSpriteButton("ResolutionTab", tabGroup, "OptionTabButton_ResolutionOFF.png", new Vector2(0f, 20f), new Vector2(270f, 80f));
        Button controlTab = CreateSpriteButton("ControlTab", tabGroup, "OptionTabButton_ControlOFF.png", new Vector2(0f, -110f), new Vector2(270f, 80f));

        RectTransform soundPanel = CreateRect("SoundOptionsPanel", optionPanel.transform);
        Stretch(soundPanel);
        CreateSpriteImage("Title", soundPanel, "OptionPanelTitleSound.png", new Vector2(0f, 230f), new Vector2(210f, 70f));
        CreateText("MasterVolumeLabel", soundPanel, "전체 음량", new Vector2(-245f, 80f), new Vector2(220f, 55f), 30f);
        TMP_Text volumeValue = CreateText("VolumeValue", soundPanel, "100%", new Vector2(300f, 80f), new Vector2(130f, 55f), 28f);
        Slider slider = CreateOptionSlider(soundPanel, new Vector2(55f, 25f));

        RectTransform resolutionPanel = CreatePlaceholderOptionPanel("ResolutionOptionsPanel", optionPanel.transform, "OptionTabButton_ResolutionON.png");
        CreateText("ResolutionLabel", resolutionPanel, "해상도", new Vector2(-230f, 110f), new Vector2(220f, 55f), 30f);
        TMP_Dropdown resolutionDropdown = CreateOptionDropdown("ResolutionDropdown", resolutionPanel, new Vector2(110f, 110f));
        CreateText("FullscreenLabel", resolutionPanel, "전체화면", new Vector2(-230f, 0f), new Vector2(220f, 55f), 30f);
        Toggle fullscreenToggle = CreateOptionToggle("FullscreenToggle", resolutionPanel, new Vector2(110f, 0f));
        CreateText("FrameRateLabel", resolutionPanel, "프레임", new Vector2(-230f, -110f), new Vector2(220f, 55f), 30f);
        TMP_Dropdown frameRateDropdown = CreateOptionDropdown("FrameRateDropdown", resolutionPanel, new Vector2(110f, -110f));
        RectTransform controlPanel = CreatePlaceholderOptionPanel("ControlOptionsPanel", optionPanel.transform, "OptionPanelTitleControl.png");
        CreateSpriteImage("KeyGuide", controlPanel, "KeyGuideImage.png", new Vector2(0f, 20f), new Vector2(620f, 268f));
        CreateText("MouseSensitivityLabel", controlPanel, "마우스 감도", new Vector2(-220f, -165f), new Vector2(230f, 50f), 28f);
        TMP_Text sensitivityValue = CreateText("MouseSensitivityValue", controlPanel, "1.0", new Vector2(315f, -165f), new Vector2(100f, 50f), 26f);
        Slider sensitivitySlider = CreateOptionSlider(controlPanel, new Vector2(55f, -165f));
        sensitivitySlider.minValue = 0.2f;
        sensitivitySlider.maxValue = 2f;
        sensitivitySlider.value = 1f;

        Button apply = CreateSpriteButton("ApplyButton", settingsPanel, "OptionButton_Apply.png", new Vector2(-150f, -400f), new Vector2(210f, 58f));
        Button reset = CreateSpriteButton("ResetButton", settingsPanel, "OptionButton_Reset.png", new Vector2(85f, -400f), new Vector2(210f, 58f));
        Button back = CreateSpriteButton("ExitButton", settingsPanel, "OptionButton_Exit.png", new Vector2(320f, -400f), new Vector2(210f, 58f));
        UnityEventTools.AddPersistentListener(slider.onValueChanged, controller.SetMasterVolume);
        UnityEventTools.AddPersistentListener(soundTab.onClick, controller.ShowSoundTab);
        UnityEventTools.AddPersistentListener(resolutionTab.onClick, controller.ShowResolutionTab);
        UnityEventTools.AddPersistentListener(controlTab.onClick, controller.ShowControlTab);
        UnityEventTools.AddPersistentListener(sensitivitySlider.onValueChanged, controller.SetMouseSensitivity);
        UnityEventTools.AddPersistentListener(apply.onClick, controller.ApplyAndClose);
        UnityEventTools.AddPersistentListener(reset.onClick, controller.ShowResetConfirm);
        UnityEventTools.AddPersistentListener(back.onClick, controller.ShowPauseMenu);

        RectTransform confirm = CreateRect("ResetConfirmDialog", settingsPanel);
        SetRect(confirm, Vector2.zero, new Vector2(940f, 660f));
        CreateSpriteImage("PopupPanel", confirm, "PopupPanel.png", Vector2.zero, new Vector2(940f, 660f));
        CreateSpriteImage("Information", confirm, "OptionInformationPopup.png", new Vector2(0f, 20f), new Vector2(640f, 76f));
        Button confirmButton = CreateSpriteButton("ConfirmButton", confirm, "OptionButton_OK.png", new Vector2(-125f, -170f), new Vector2(210f, 58f));
        Button cancelButton = CreateSpriteButton("CancelButton", confirm, "OptionButton_Cancle.png", new Vector2(125f, -170f), new Vector2(210f, 58f));
        UnityEventTools.AddPersistentListener(confirmButton.onClick, controller.ResetSettings);
        UnityEventTools.AddPersistentListener(cancelButton.onClick, controller.HideResetConfirm);
        confirm.gameObject.SetActive(false);
        Assign(controller, "pausePanel", pausePanel.gameObject, "settingsPanel", settingsPanel.gameObject,
            "masterVolumeSlider", slider, "masterVolumeValueText", volumeValue,
            "soundOptionsPanel", soundPanel.gameObject,
            "resolutionOptionsPanel", resolutionPanel.gameObject, "controlOptionsPanel", controlPanel.gameObject,
            "resetConfirmPanel", confirm.gameObject,
            "resolutionDropdown", resolutionDropdown, "frameRateDropdown", frameRateDropdown,
            "fullscreenToggle", fullscreenToggle, "mouseSensitivitySlider", sensitivitySlider,
            "mouseSensitivityValueText", sensitivityValue,
            "soundTabImage", soundTab.image,
            "resolutionTabImage", resolutionTab.image, "controlTabImage", controlTab.image,
            "soundTabOn", LoadOptionSprite("OptionTabButton_SoundON.png"),
            "soundTabOff", LoadOptionSprite("OptionTabButton_SoundOFF.png"),
            "resolutionTabOn", LoadOptionSprite("OptionTabButton_ResolutionON.png"),
            "resolutionTabOff", LoadOptionSprite("OptionTabButton_ResolutionOFF.png"),
            "controlTabOn", LoadOptionSprite("OptionTabButton_ControlON.png"),
            "controlTabOff", LoadOptionSprite("OptionTabButton_ControlOFF.png"));
        soundPanel.gameObject.SetActive(true);
        resolutionPanel.gameObject.SetActive(false);
        controlPanel.gameObject.SetActive(false);
        pausePanel.gameObject.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
    }

    private static void BuildAbility()
    {
        AbilitySystem controller = Object.FindFirstObjectByType<AbilitySystem>();
        if (controller == null)
        {
            GameObject systems = GameObject.Find("GameSystems") ?? new GameObject("GameSystems");
            controller = systems.AddComponent<AbilitySystem>();
        }

        Canvas canvas = CreateCanvas("AbilityCanvas", 110);
        RectTransform panel = CreatePanel("AbilityPanel", canvas.transform, new Color(0f, 0f, 0f, 0.65f));
        CreateText("Title", panel, "능력 강화!", new Vector2(0f, 300f), new Vector2(900f, 110f), 70f);
        CreateText("Subtitle", panel, "하나를 선택하세요", new Vector2(0f, 220f), new Vector2(900f, 60f), 34f);
        Button power = CreateButton("SuctionPowerButton", panel, "공격력 증가", new Vector2(-380f, -20f), new Vector2(330f, 360f));
        Button range = CreateButton("SuctionRangeButton", panel, "사거리 증가", new Vector2(0f, -20f), new Vector2(330f, 360f));
        Button speed = CreateButton("MoveSpeedButton", panel, "이동속도 증가", new Vector2(380f, -20f), new Vector2(330f, 360f));
        UnityEventTools.AddPersistentListener(power.onClick, controller.ChooseSuctionPower);
        UnityEventTools.AddPersistentListener(range.onClick, controller.ChooseSuctionRange);
        UnityEventTools.AddPersistentListener(speed.onClick, controller.ChooseMoveSpeed);
        Assign(controller, "abilityPanel", panel.gameObject);
        CreateText("Placeholder_Wave", panel, "WAVE\n5 / 5", new Vector2(0f, -430f), new Vector2(260f, 80f), 25f);
        panel.gameObject.SetActive(false);
    }

    private static void BuildFuturePlaceholders()
    {
        Canvas canvas = CreateCanvas("FutureUIPlaceholderCanvas", 120);

        RectTransform stageIntro = CreatePanel("Placeholder_MiniGameStageIntro", canvas.transform, new Color(0f, 0f, 0f, 0.72f));
        CreateText("Stage", stageIntro, "STAGE 1", new Vector2(0f, 80f), new Vector2(700f, 90f), 52f);
        Image progressBg = CreateImage("ProgressBackground", stageIntro, new Color(0.12f, 0.12f, 0.12f, 1f));
        SetRect(progressBg.rectTransform, new Vector2(0f, 0f), new Vector2(520f, 28f));
        Image progressFill = CreateImage("ProgressFill", progressBg.transform, new Color(0.85f, 0.63f, 0.24f, 1f));
        Stretch(progressFill.rectTransform);
        stageIntro.gameObject.SetActive(false);

        RectTransform gauge = CreatePanel("Placeholder_MiniGameGauge", canvas.transform, new Color(0f, 0f, 0f, 0.72f));
        Image gaugeFrame = CreateImage("GaugeFrame", gauge, new Color(0.08f, 0.08f, 0.08f, 0.96f));
        SetRect(gaugeFrame.rectTransform, Vector2.zero, new Vector2(760f, 110f));
        Image successZone = CreateImage("SuccessZone", gaugeFrame.transform, new Color(0.85f, 0.63f, 0.24f, 0.75f));
        SetRect(successZone.rectTransform, Vector2.zero, new Vector2(170f, 90f));
        Image marker = CreateImage("MovingMarker", gaugeFrame.transform, Color.white);
        SetRect(marker.rectTransform, new Vector2(-300f, 0f), new Vector2(12f, 96f));
        gauge.gameObject.SetActive(false);

        canvas.gameObject.SetActive(true);
    }

    private static Canvas CreateCanvas(string name, int sortingOrder)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        Image image = CreateImage(name, parent, color);
        Stretch(image.rectTransform);
        return image.rectTransform;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size)
    {
        Image image = CreateImage(name, parent, new Color(0.18f, 0.3f, 0.65f, 0.96f));
        SetRect(image.rectTransform, position, size);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText("Label", image.transform, label, Vector2.zero, Vector2.zero, 34f);
        Stretch(text.rectTransform);
        return button;
    }

    private static Slider CreateSlider(Transform parent, Vector2 position)
    {
        RectTransform root = CreateRect("MasterVolumeSlider", parent);
        SetRect(root, position, new Vector2(620f, 50f));
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        Image background = CreateImage("Background", root, new Color(0.08f, 0.1f, 0.2f, 0.9f));
        SetRect(background.rectTransform, Vector2.zero, new Vector2(620f, 18f));
        Image fill = CreateImage("Fill", root, new Color(0.2f, 0.8f, 1f, 1f));
        SetRect(fill.rectTransform, Vector2.zero, new Vector2(600f, 18f));
        Image handle = CreateImage("Handle", root, Color.white);
        SetRect(handle.rectTransform, Vector2.zero, new Vector2(38f, 38f));
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        return slider;
    }

    private static Slider CreateOptionSlider(Transform parent, Vector2 position)
    {
        RectTransform root = CreateRect("MasterVolumeSlider", parent);
        SetRect(root, position, new Vector2(470f, 64f));
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        Image background = CreateSpriteImage("BarGauge", root, "BarGauge.png", Vector2.zero, new Vector2(470f, 64f));
        Image fill = CreateSpriteImage("Fill", root, "BarGauge.png", Vector2.zero, new Vector2(470f, 64f));
        fill.color = new Color(1f, 0.78f, 0.2f, 1f);
        Image handle = CreateSpriteImage("Handle", root, "SoundBarAnchor.png", Vector2.zero, new Vector2(37f, 64f));
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        return slider;
    }

    public static TMP_Dropdown CreateOptionDropdown(string name, Transform parent, Vector2 position)
    {
        Image rootImage = CreateSpriteImage(name, parent, "DropBox.png", position, new Vector2(290f, 60f));
        rootImage.raycastTarget = true;
        TMP_Dropdown dropdown = rootImage.gameObject.AddComponent<TMP_Dropdown>();

        TMP_Text caption = CreateText("Label", rootImage.transform, "선택", new Vector2(-15f, 0f), new Vector2(220f, 50f), 24f);
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        Image arrow = CreateSpriteImage("Arrow", rootImage.transform, "DropBoxOpenIcon.png", new Vector2(112f, 0f), new Vector2(43f, 43f));

        RectTransform template = CreateRect("Template", rootImage.transform);
        SetRect(template, new Vector2(0f, -142f), new Vector2(290f, 230f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f));
        Image templateImage = template.gameObject.AddComponent<Image>();
        templateImage.sprite = LoadOptionSprite("DropBoxOpen.png");
        templateImage.type = Image.Type.Simple;
        ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
        Canvas templateCanvas = template.gameObject.AddComponent<Canvas>();
        templateCanvas.overrideSorting = true;
        templateCanvas.sortingOrder = 300;
        template.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform viewport = CreateRect("Viewport", template);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 210f);
        VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform item = CreateRect("Item", content);
        item.anchorMin = new Vector2(0f, 1f);
        item.anchorMax = new Vector2(1f, 1f);
        item.pivot = new Vector2(0.5f, 1f);
        item.sizeDelta = new Vector2(0f, 52f);
        Toggle itemToggle = item.gameObject.AddComponent<Toggle>();
        Image itemBackground = item.gameObject.AddComponent<Image>();
        itemBackground.color = new Color(1f, 1f, 1f, 0.01f);
        itemToggle.targetGraphic = itemBackground;
        TMP_Text itemLabel = CreateText("Item Label", item, "Option", new Vector2(-10f, 0f), new Vector2(245f, 48f), 22f);
        itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
        itemToggle.graphic = null;

        scroll.viewport = viewport;
        scroll.content = content;
        dropdown.targetGraphic = rootImage;
        dropdown.template = template;
        dropdown.captionText = caption;
        dropdown.itemText = itemLabel;
        dropdown.options.Add(new TMP_Dropdown.OptionData("Option"));
        template.gameObject.SetActive(false);
        arrow.raycastTarget = false;
        return dropdown;
    }

    public static Toggle CreateOptionToggle(string name, Transform parent, Vector2 position)
    {
        RectTransform root = CreateRect(name, parent);
        SetRect(root, position, new Vector2(90f, 50f));
        Toggle toggle = root.gameObject.AddComponent<Toggle>();
        Image track = CreateSpriteImage("Track", root, "ONOFF_Switch_Track.png", Vector2.zero, new Vector2(90f, 50f));
        Image handle = CreateSpriteImage("Handle", root, "ONOFF_Switch_Handle.png", new Vector2(20f, 0f), new Vector2(34f, 34f));
        toggle.targetGraphic = track;
        toggle.graphic = null;
        toggle.isOn = true;
        OptionToggleVisual visual = root.gameObject.AddComponent<OptionToggleVisual>();
        SerializedObject visualSerialized = new(visual);
        visualSerialized.FindProperty("handle").objectReferenceValue = handle.rectTransform;
        visualSerialized.ApplyModifiedPropertiesWithoutUndo();
        return toggle;
    }

    public static RectTransform CreatePlaceholderOptionPanel(string name, Transform parent, string titleSprite)
    {
        RectTransform panel = CreateRect(name, parent);
        Stretch(panel);
        CreateSpriteImage("Title", panel, titleSprite, new Vector2(0f, 230f), new Vector2(210f, 70f));
        RectTransform content = CreateRect("Placeholder_Content", panel);
        SetRect(content, new Vector2(0f, -10f), new Vector2(700f, 360f));
        return panel;
    }

    public static Image CreateSpriteImage(string name, Transform parent, string fileName, Vector2 position, Vector2 size)
    {
        Image image = CreateImage(name, parent, Color.white);
        image.sprite = LoadOptionSprite(fileName);
        image.preserveAspect = true;
        SetRect(image.rectTransform, position, size);
        return image;
    }

    public static Sprite LoadOptionSprite(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(OptionSpritePath + fileName);
    }

    public static Button CreateSpriteButton(string name, Transform parent, string fileName, Vector2 position, Vector2 size)
    {
        Image image = CreateSpriteImage(name, parent, fileName, position, size);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize,
        Vector2? anchor = null, Vector2? pivot = null)
    {
        RectTransform rect = CreateRect(name, parent);
        SetRect(rect, position, size, anchor ?? new Vector2(0.5f, 0.5f), pivot ?? new Vector2(0.5f, 0.5f));
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = SBFontSetup.EnsureFont();
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2? anchor = null, Vector2? pivot = null)
    {
        Vector2 selectedAnchor = anchor ?? new Vector2(0.5f, 0.5f);
        rect.anchorMin = rect.anchorMax = selectedAnchor;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
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

    private static void EnsureEventSystem()
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
        else if (go.GetComponent<StandaloneInputModule>() == null)
            go.AddComponent<StandaloneInputModule>();
    }

    private static void DestroyNamed(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) Object.DestroyImmediate(existing);
    }

    private static void Assign(Object target, params object[] values)
    {
        SerializedObject serialized = new(target);
        for (int i = 0; i < values.Length; i += 2)
            serialized.FindProperty((string)values[i]).objectReferenceValue = values[i + 1] as Object;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
