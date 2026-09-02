using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MiniGameSequence : MonoBehaviour, IRescueMiniGame
{
    [Header("Editable Scene UI")]
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private RectTransform keyContainer;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private List<Image> keyImages = new();

    [Header("Editable Text")]
    [SerializeField] private bool useCanvasTextAsFormat = true;
    [SerializeField] private string stageFormat = "단계 : {0}";
    [SerializeField] private string timeFormat = "남은 시간 : {0}초";
    [TextArea(2, 4)]
    [SerializeField] private string descriptionMessage = "WASD를 순서대로 입력하세요";

    [Header("Key Layout")]
    [SerializeField] private int maxVisibleKeys = 24;
    [SerializeField] private float keyPanelHorizontalPadding = 95f;
    [SerializeField] private float keyPanelVerticalPadding = 55f;
    [SerializeField] private float keySpacing = 18f;
    [SerializeField] private float maxKeySize = 150f;

    private readonly Sprite[] keySprites = new Sprite[4];
    private BalanceConfig config;
    private Action<int> onComplete;
    private bool active;
    private float timer;
    private int stagesCleared;
    private int index;
    private int[] sequence;
    private int startLength;
    private int lengthStep;

    private void Awake()
    {
        config = BalanceConfig.Resolve(null);
        keySprites[0] = UIRealTheme.LoadSprite("[UI]_Minigame/KeyImage_W");
        keySprites[1] = UIRealTheme.LoadSprite("[UI]_Minigame/KeyImage_S");
        keySprites[2] = UIRealTheme.LoadSprite("[UI]_Minigame/KeyImage_A");
        keySprites[3] = UIRealTheme.LoadSprite("[UI]_Minigame/KeyImage_D");
        if (uiRoot != null) uiRoot.SetActive(false);
        HideLegacyStatusText();
        CaptureCanvasTextFormat();
    }

    public void Begin(Action<int> callback)
    {
        onComplete = callback;
        timer = config != null ? config.sequenceTimeLimit : 15f;
        startLength = config != null ? Mathf.Max(1, config.sequenceStartLength) : 4;
        lengthStep = config != null ? Mathf.Max(1, config.sequenceLengthStep) : 2;
        stagesCleared = 0;
        active = true;
        if (uiRoot == null || keyContainer == null)
        {
            Debug.LogError("[MiniGameSequence] 씬 UI 참조가 없습니다.");
            Finish(0);
            return;
        }
        HideLegacyStatusText();
        GenerateSequence();
        uiRoot.SetActive(true);
        RefreshStatus();
    }

    public void Cancel()
    {
        active = false;
        onComplete = null;
        if (uiRoot != null) uiRoot.SetActive(false);
    }

    private void Update()
    {
        if (!active) return;
        if (PauseMenuController.IsPaused) return; // ESC 메뉴가 열려있는 동안엔 미니게임도 정지

        // 구출 미니게임 도중에는 Time.timeScale이 0으로 떨어져 있는 게 정상(월드 정지)이므로,
        // 미니게임 자체의 진행은 항상 실시간(unscaled)으로 처리한다.
        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f)
        {
            Finish(stagesCleared);
            return;
        }

        RefreshStatus();
        int direction = PressedDirection();
        if (direction < 0) return;

        if (direction == sequence[index])
        {
            index++;
            if (index >= sequence.Length)
            {
                stagesCleared++;
                GenerateSequence();
            }
        }
        else
        {
            index = 0;
        }

        RefreshSequence();
    }

    private int PressedDirection()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return -1;
        if (keyboard.wKey.wasPressedThisFrame) return 0;
        if (keyboard.sKey.wasPressedThisFrame) return 1;
        if (keyboard.aKey.wasPressedThisFrame) return 2;
        if (keyboard.dKey.wasPressedThisFrame) return 3;
        return -1;
    }

    private void GenerateSequence()
    {
        int length = startLength + stagesCleared / lengthStep;
        int slotLimit = keyImages.Count > 0 ? keyImages.Count : maxVisibleKeys;
        length = Mathf.Min(length, Mathf.Max(1, slotLimit));
        sequence = new int[Mathf.Max(1, length)];
        for (int i = 0; i < sequence.Length; i++)
            sequence[i] = UnityEngine.Random.Range(0, 4);
        index = 0;
        RefreshSequence();
    }

    private void RefreshSequence()
    {
        int visibleCount = sequence != null ? Mathf.Min(sequence.Length, keyImages.Count) : 0;
        ApplyKeyContainerLayout(visibleCount);

        for (int i = 0; i < keyImages.Count; i++)
        {
            bool visible = i < visibleCount;
            keyImages[i].gameObject.SetActive(visible);
            if (!visible) continue;
            keyImages[i].sprite = keySprites[sequence[i]];
            keyImages[i].color = i < index
                ? new Color(0.45f, 1f, 0.5f)
                : i == index ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }
    }

    private void ApplyKeyContainerLayout(int visibleCount)
    {
        if (keyContainer == null)
            return;

        RectTransform panelRect = keyContainer.parent as RectTransform;
        float panelWidth = panelRect != null ? Mathf.Max(panelRect.rect.width, panelRect.sizeDelta.x) : keyContainer.rect.width;
        float panelHeight = panelRect != null ? Mathf.Max(panelRect.rect.height, panelRect.sizeDelta.y) : keyContainer.rect.height;
        float containerWidth = Mathf.Max(1f, panelWidth - keyPanelHorizontalPadding * 2f);
        float containerHeight = Mathf.Max(1f, panelHeight - keyPanelVerticalPadding * 2f);

        keyContainer.anchorMin = keyContainer.anchorMax = new Vector2(0.5f, 0.5f);
        keyContainer.pivot = new Vector2(0.5f, 0.5f);
        keyContainer.anchoredPosition = Vector2.zero;
        keyContainer.sizeDelta = new Vector2(containerWidth, containerHeight);

        HorizontalLayoutGroup layout = keyContainer.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = keyContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = keySpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        int count = Mathf.Max(1, visibleCount);
        float availableWidth = Mathf.Max(1f, containerWidth - keySpacing * Mathf.Max(0, count - 1));
        float keySize = Mathf.Min(maxKeySize, containerHeight, availableWidth / count);

        for (int i = 0; i < keyImages.Count; i++)
        {
            Image keyImage = keyImages[i];
            if (keyImage == null)
                continue;

            RectTransform keyRect = keyImage.rectTransform;
            keyRect.sizeDelta = new Vector2(keySize, keySize);

            LayoutElement element = keyImage.GetComponent<LayoutElement>();
            if (element == null)
                element = keyImage.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 0f;
            element.minHeight = 0f;
            element.preferredWidth = keySize;
            element.preferredHeight = keySize;
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;
        }
    }

    private void RefreshStatus()
    {
        int stage = stagesCleared;
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, timer));

        if (stageText != null)
            stageText.text = FormatStatus(stageFormat, stage);
        if (timeText != null)
            timeText.text = FormatStatus(timeFormat, seconds);
        if (descriptionText != null)
            descriptionText.text = descriptionMessage;

        if (statusText != null)
            statusText.text = $"{FormatStatus(stageFormat, stage)}    {FormatStatus(timeFormat, seconds)}\n{descriptionMessage}";
    }

    private void HideLegacyStatusText()
    {
        if (statusText != null)
            statusText.gameObject.SetActive(false);
    }

    private void CaptureCanvasTextFormat()
    {
        if (!useCanvasTextAsFormat)
            return;

        if (stageText != null && IsEditableFormatSample(stageText.text))
            stageFormat = EnsureOneArgFormat(stageText.text);
        if (timeText != null && IsEditableFormatSample(timeText.text))
            timeFormat = EnsureOneArgFormat(timeText.text);
        if (descriptionText != null && !string.IsNullOrEmpty(descriptionText.text))
            descriptionMessage = descriptionText.text;
        else if (statusText != null && !string.IsNullOrEmpty(statusText.text))
            descriptionMessage = statusText.text;
    }

    private static string FormatStatus(string format, int value)
    {
        if (string.IsNullOrEmpty(format))
            return string.Empty;

        try
        {
            return string.Format(format, value);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private static string EnsureOneArgFormat(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Contains("{0}"))
            return text;

        int zeroIndex = text.IndexOf('0');
        return zeroIndex >= 0 ? text.Remove(zeroIndex, 1).Insert(zeroIndex, "{0}") : text + " {0}";
    }

    private static bool IsEditableFormatSample(string text)
    {
        return !string.IsNullOrEmpty(text) && (text.Contains("{0}") || text.IndexOf('0') >= 0);
    }

    private void Finish(int cleared)
    {
        active = false;
        if (uiRoot != null) uiRoot.SetActive(false);
        Action<int> callback = onComplete;
        onComplete = null;
        callback?.Invoke(cleared);
    }

#if UNITY_EDITOR
    public void SetupEditableUI()
    {
        GameObject canvasObject = EditableMiniGameUI.CreateCanvas(transform, "SequenceCanvas", 90);
        if (uiRoot == null)
        {
            uiRoot = canvasObject;
            EditableMiniGameUI.CreateDim(canvasObject.transform);
        }
        Transform existingPanel = canvasObject.transform.Find("QTEPanel");
        Image panel = existingPanel != null ? existingPanel.GetComponent<Image>() : null;
        if (panel == null)
            panel = EditableMiniGameUI.CreateImage(canvasObject.transform, "QTEPanel", null, new Vector2(1390f, 280f));
        panel.sprite = UIRealTheme.LoadSprite("[UI]_Minigame/QTEPanel 복사");

        Transform existingRow = panel.transform.Find("KeyContainer");
        if (keyContainer == null && existingRow != null) keyContainer = existingRow.GetComponent<RectTransform>();
        if (keyContainer == null)
        {
            GameObject row = new("KeyContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            UnityEditor.Undo.RegisterCreatedObjectUndo(row, "Create key container");
            row.transform.SetParent(panel.transform, false);
            keyContainer = row.GetComponent<RectTransform>();
            keyContainer.anchorMin = keyContainer.anchorMax = new Vector2(0.5f, 0.5f);
            keyContainer.pivot = new Vector2(0.5f, 0.5f);
            keyContainer.sizeDelta = new Vector2(1200f, 170f);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
        }
        else if (keyContainer.GetComponent<HorizontalLayoutGroup>() == null)
        {
            keyContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        keyImages.RemoveAll(image => image == null);
        for (int i = keyImages.Count; i < 24; i++)
        {
            string[] paths =
            {
                "[UI]_Minigame/KeyImage_W",
                "[UI]_Minigame/KeyImage_S",
                "[UI]_Minigame/KeyImage_A",
                "[UI]_Minigame/KeyImage_D"
            };
            Image key = EditableMiniGameUI.CreateImage(keyContainer, $"KeySlot_{i + 1}",
                UIRealTheme.LoadSprite(paths[i % paths.Length]), new Vector2(150f, 150f));
            LayoutElement element = key.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 150f;
            element.preferredHeight = 150f;
            keyImages.Add(key);
        }
        ApplyKeyContainerLayout(Mathf.Min(keyImages.Count, maxVisibleKeys));

        stageText = GetOrCreateText(canvasObject.transform, stageText, "StageText", 42f, new Vector2(-420f, 270f), new Vector2(420f, 70f), stageFormat);
        timeText = GetOrCreateText(canvasObject.transform, timeText, "TimeText", 42f, new Vector2(420f, 270f), new Vector2(520f, 70f), timeFormat);
        descriptionText = GetOrCreateText(canvasObject.transform, descriptionText, "DescriptionText", 38f, new Vector2(0f, -230f), new Vector2(1400f, 90f), descriptionMessage);

        Transform existingText = canvasObject.transform.Find("StatusText");
        if (statusText == null && existingText != null)
            statusText = existingText.GetComponent<TMP_Text>();
        if (statusText != null)
            statusText.gameObject.SetActive(false);
        uiRoot.SetActive(false);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private static TMP_Text GetOrCreateText(Transform parent, TMP_Text assigned, string name, float fontSize, Vector2 position, Vector2 size, string defaultText)
    {
        TMP_Text text = assigned;
        if (text == null)
        {
            Transform existing = parent.Find(name);
            if (existing != null) text = existing.GetComponent<TMP_Text>();
        }

        if (text == null)
            text = EditableMiniGameUI.CreateText(parent, name, fontSize, position, size);

        if (string.IsNullOrEmpty(text.text))
            text.text = defaultText;
        return text;
    }
#endif
}