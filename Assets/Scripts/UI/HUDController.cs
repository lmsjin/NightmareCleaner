using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
public class HUDController : MonoBehaviour
{
    [SerializeField] private string endSceneName = "end";

    [Header("Canvas UI")]
    [SerializeField] private TMP_Text heartText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text childText;
    [SerializeField] private Image suctionGaugeFill;
    [SerializeField] private List<Image> suctionGaugeStages = new();
    [SerializeField] private Image suctionNormalImage;
    [SerializeField] private Image suctionOverheatImage;
    [SerializeField] private TMP_Text suctionGaugeLabel;
    [SerializeField] private Image xpFill;
    [SerializeField] private TMP_Text xpLabel;

    [Header("Editable Experience UI")]
    [SerializeField] private Image expBarGauge;
    [SerializeField] private Image expBarBackground;
    [SerializeField] private GameObject legacyXpFill;
    [SerializeField] private GameObject legacyXpBackground;

    [Header("Editable Health UI")]
    [SerializeField] private RectTransform heartRow;
    [SerializeField] private List<Image> heartImages = new();
    [SerializeField] private GameObject legacyHpLabel;
    [SerializeField] private GameObject legacyHpBar;
    [SerializeField] private GameObject legacyHpBarBackground;
    private readonly Sprite[] gaugeSprites = new Sprite[5];
    private PlayerStats playerStats;
    private SuctionCharge charge;
    private Sprite heartFull;
    private Sprite heartBlank;
    private Sprite gaugeIconNormal;
    private Sprite gaugeIconOverload;
    private int lastHP = -1;
    private float hpPunch;

    private int Score => ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
    private int Children => ScoreManager.Instance != null ? ScoreManager.Instance.ChildCount : 0;

    private void Awake()
    {
        LoadFinalArt();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        GameEvents.OnGameOver += LoseGame;
        GameEvents.OnTimerEnd += WinGame;
    }

    private void Start()
    {
        playerStats = FindFirstObjectByType<PlayerStats>();
        charge = FindFirstObjectByType<SuctionCharge>();
        if (playerStats != null) EnsureHeartImages(playerStats.MaxHP);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        GameEvents.OnGameOver -= LoseGame;
        GameEvents.OnTimerEnd -= WinGame;
    }

    private void LoadFinalArt()
    {
        heartFull = UIRealTheme.LoadSprite("[UI]_Ingame/PlayerHPFull");
        heartBlank = UIRealTheme.LoadSprite("[UI]_Ingame/PlayerHPBlank");
        for (int i = 0; i < gaugeSprites.Length; i++)
            gaugeSprites[i] = UIRealTheme.LoadSprite($"[UI]_Ingame/GaugeBar({i + 1})");

        gaugeIconNormal = UIRealTheme.LoadSprite("[UI]_Ingame/GaugeIcon");
        gaugeIconOverload = UIRealTheme.LoadSprite("[UI]_Ingame/GaugeIconOverload");

        if (timerText != null && timerText.transform.parent.TryGetComponent(out Image timerBackground))
        {
            timerBackground.sprite = UIRealTheme.LoadSprite("[UI]_Ingame/TimerBackground");
            timerBackground.color = Color.white;
            timerBackground.preserveAspect = true;
        }

        ConfigureExperienceGauge(expBarGauge);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        UpdateHealth();
        UpdateTimer();

        if (scoreText != null) scoreText.text = $"점수: {Score}";
        UpdateTrailingNumber(childText, Children);

        UpdateSuctionGauge();
        UpdateExperience();
    }

    private void UpdateHealth()
    {
        if (playerStats == null || playerStats.MaxHP <= 0) return;

        int current = playerStats.CurrentHP;
        EnsureHeartImages(playerStats.MaxHP);
        if (lastHP >= 0 && current < lastHP) hpPunch = 1f;
        lastHP = current;

        hpPunch = Mathf.MoveTowards(hpPunch, 0f, Time.deltaTime * 4f);
        float scale = 1f + 0.2f * hpPunch;
        for (int i = 0; i < heartImages.Count; i++)
        {
            heartImages[i].sprite = i < current ? heartFull : heartBlank;
            heartImages[i].rectTransform.localScale = Vector3.one * scale;
        }
    }

    private void EnsureHeartImages(int count)
    {
        if (heartText == null || heartFull == null || heartBlank == null) return;
        if (heartRow == null)
        {
            GameObject row = new("HeartRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(heartText.transform.parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            RectTransform source = heartText.rectTransform;
            rowRect.anchorMin = source.anchorMin;
            rowRect.anchorMax = source.anchorMax;
            rowRect.pivot = source.pivot;
            rowRect.anchoredPosition = source.anchoredPosition;
            rowRect.sizeDelta = source.sizeDelta;
            heartRow = rowRect;
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            heartText.gameObject.SetActive(false);

            while (heartImages.Count < count)
            {
                GameObject heart = new($"Heart_{heartImages.Count + 1}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                heart.transform.SetParent(heartRow, false);
                Image image = heart.GetComponent<Image>();
                image.preserveAspect = true;
                LayoutElement element = heart.GetComponent<LayoutElement>();
                element.preferredWidth = 64f;
                element.preferredHeight = 64f;
                heartImages.Add(image);
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Setup Editable Health UI")]
    public void SetupEditableHealthUI()
    {
        LoadFinalArt();
        int count = 5;
        BalanceConfig config = BalanceConfig.Resolve(null);
        if (config != null) count = Mathf.Max(1, config.playerMaxHP);
        EnsureHeartImages(count);
        SetupEditableSuctionGauge();
        SetupEditableGaugeIcons();
        SetupEditableExperienceUI();
        DisableLegacyHealthUI();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private void SetupEditableSuctionGauge()
    {
        if (suctionGaugeFill == null) return;
        suctionGaugeStages.RemoveAll(image => image == null);
        Transform parent = suctionGaugeFill.transform.parent;
        RectTransform source = suctionGaugeFill.rectTransform;

        for (int i = suctionGaugeStages.Count; i < gaugeSprites.Length; i++)
        {
            GameObject stageObject = new($"GaugeStage_{i + 1}", typeof(RectTransform), typeof(Image));
            UnityEditor.Undo.RegisterCreatedObjectUndo(stageObject, "Create suction gauge stage");
            stageObject.transform.SetParent(parent, false);
            Image stage = stageObject.GetComponent<Image>();
            RectTransform rect = stage.rectTransform;
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = source.anchoredPosition;
            rect.sizeDelta = source.sizeDelta;
            stage.color = Color.white;
            stage.preserveAspect = true;
            stage.raycastTarget = false;
            suctionGaugeStages.Add(stage);
        }

        for (int i = 0; i < gaugeSprites.Length; i++)
        {
            suctionGaugeStages[i].sprite = gaugeSprites[i];
            suctionGaugeStages[i].gameObject.SetActive(i == gaugeSprites.Length - 1);
            suctionGaugeStages[i].transform.SetSiblingIndex(source.GetSiblingIndex() + i + 1);
        }
        suctionGaugeFill.color = Color.clear;
    }

    private void SetupEditableGaugeIcons()
    {
        if (suctionGaugeFill == null) return;
        Transform parent = suctionGaugeFill.transform.parent;

        suctionNormalImage = GetOrCreateGaugeIcon(parent, suctionNormalImage,
            "SuctionNormalImage", gaugeIconNormal);
        suctionOverheatImage = GetOrCreateGaugeIcon(parent, suctionOverheatImage,
            "SuctionOverheatImage", gaugeIconOverload);

        suctionNormalImage.gameObject.SetActive(true);
        suctionOverheatImage.gameObject.SetActive(false);
        suctionNormalImage.transform.SetAsLastSibling();
        suctionOverheatImage.transform.SetAsLastSibling();
    }

    private void SetupEditableExperienceUI()
    {
        if (legacyXpBackground == null)
            legacyXpBackground = FindSceneObject("XpBarBackground");
        if (legacyXpFill == null)
            legacyXpFill = FindSceneObject("XpBarFill");

        RectTransform source = legacyXpBackground != null
            ? legacyXpBackground.GetComponent<RectTransform>()
            : xpFill != null ? xpFill.rectTransform : null;
        if (source == null) return;

        Transform parent = source.parent;
        expBarBackground = GetOrCreateExperienceImage(parent, expBarBackground,
            "EXP BAR BACKGROUND", UIRealTheme.LoadSprite("[UI]_Ingame/ExpBarBackground"), source);
        expBarGauge = GetOrCreateExperienceImage(expBarBackground.transform, expBarGauge,
            "EXP BAR GAUGE", UIRealTheme.LoadSprite("[UI]_Ingame/ExpBarGauge"), null);

        RectTransform gaugeRect = expBarGauge.rectTransform;
        gaugeRect.anchorMin = Vector2.zero;
        gaugeRect.anchorMax = Vector2.one;
        gaugeRect.pivot = new Vector2(0.5f, 0.5f);
        gaugeRect.anchoredPosition = Vector2.zero;
        gaugeRect.sizeDelta = Vector2.zero;
        ConfigureExperienceGauge(expBarGauge);

        if (xpLabel != null && xpLabel.transform.parent != expBarBackground.transform)
            xpLabel.transform.SetParent(expBarBackground.transform, true);

        if (legacyXpFill != null) legacyXpFill.SetActive(false);
        if (legacyXpBackground != null) legacyXpBackground.SetActive(false);
        expBarBackground.gameObject.SetActive(true);
        expBarGauge.gameObject.SetActive(true);
    }

    private static Image GetOrCreateExperienceImage(Transform parent, Image assigned, string objectName,
        Sprite sprite, RectTransform source)
    {
        Image image = assigned;
        bool created = false;
        if (image == null)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null) image = existing.GetComponent<Image>();
        }

        if (image == null)
        {
            GameObject imageObject = new(objectName, typeof(RectTransform), typeof(Image));
            UnityEditor.Undo.RegisterCreatedObjectUndo(imageObject, $"Create {objectName}");
            imageObject.transform.SetParent(parent, false);
            image = imageObject.GetComponent<Image>();
            created = true;
        }

        if (created && source != null)
        {
            RectTransform rect = image.rectTransform;
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = source.anchoredPosition;
            rect.sizeDelta = source.sizeDelta;
            rect.localScale = source.localScale;
            image.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
        }

        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static Image GetOrCreateGaugeIcon(Transform parent, Image assigned, string objectName, Sprite sprite)
    {
        Image image = assigned;
        if (image == null)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
                image = existing.GetComponent<Image>();

            if (image == null)
            {
                GameObject iconObject = new(objectName, typeof(RectTransform), typeof(Image));
                UnityEditor.Undo.RegisterCreatedObjectUndo(iconObject, $"Create {objectName}");
                iconObject.transform.SetParent(parent, false);
                image = iconObject.GetComponent<Image>();
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(170f, 110f);
            }
        }

        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private void DisableLegacyHealthUI()
    {
        if (legacyHpLabel == null)
            legacyHpLabel = FindSceneObject("HPLabel");
        if (legacyHpBar == null)
            legacyHpBar = FindSceneObject("Placeholder_HPBarFill");
        if (legacyHpBarBackground == null)
            legacyHpBarBackground = FindSceneObject("HPBarBackground");

        if (heartRow != null && legacyHpBarBackground != null && heartRow.parent == legacyHpBarBackground.transform)
        {
            Transform newParent = legacyHpBarBackground.transform.parent;
            heartRow.SetParent(newParent, true);
            heartRow.SetSiblingIndex(legacyHpBarBackground.transform.GetSiblingIndex() + 1);
        }

        if (legacyHpLabel != null) legacyHpLabel.SetActive(false);
        if (legacyHpBar != null) legacyHpBar.SetActive(false);
        if (legacyHpBarBackground != null) legacyHpBarBackground.SetActive(false);
        if (heartText != null) heartText.gameObject.SetActive(false);
        if (heartRow != null) heartRow.gameObject.SetActive(true);
    }

    private GameObject FindSceneObject(string objectName)
    {
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            if (candidate.name == objectName && candidate.gameObject.scene == gameObject.scene)
                return candidate.gameObject;
        return null;
    }
#endif

    private void UpdateTimer()
    {
        if (GameManager.Instance == null || timerText == null) return;
        float time = GameManager.Instance.TimeRemaining;
        timerText.text = $"{(int)(time / 60):00}:{(int)(time % 60):00}";
    }

    private void UpdateSuctionGauge()
    {
        if (charge == null || suctionGaugeFill == null) return;

        bool showOverheatWarning = charge.IsOverheatWarning;
        int index = GetSuctionGaugeStage(charge.Ratio);
        if (suctionGaugeStages.Count == gaugeSprites.Length)
        {
            for (int i = 0; i < suctionGaugeStages.Count; i++)
                if (suctionGaugeStages[i] != null)
                    suctionGaugeStages[i].gameObject.SetActive(!showOverheatWarning && i == index);
        }
        else
        {
            suctionGaugeFill.sprite = gaugeSprites[index];
            suctionGaugeFill.color = Color.white;
        }

        if (suctionNormalImage != null)
            suctionNormalImage.gameObject.SetActive(!showOverheatWarning);
        if (suctionOverheatImage != null)
            suctionOverheatImage.gameObject.SetActive(showOverheatWarning);
        if (suctionGaugeLabel != null)
            suctionGaugeLabel.gameObject.SetActive(false);
    }

    private static void UpdateTrailingNumber(TMP_Text target, int value)
    {
        if (target == null) return;

        string current = target.text ?? string.Empty;
        int end = current.Length - 1;
        while (end >= 0 && !char.IsDigit(current[end])) end--;
        int start = end;
        while (start >= 0 && char.IsDigit(current[start])) start--;

        target.text = start < end
            ? current.Substring(0, start + 1) + value + current.Substring(end + 1)
            : current + value;
    }

    private static int GetSuctionGaugeStage(float ratio)
    {
        if (ratio <= 0.2f) return 0;
        if (ratio <= 0.4f) return 1;
        if (ratio <= 0.6f) return 2;
        if (ratio <= 0.8f) return 3;
        return 4;
    }

    private void UpdateExperience()
    {
        Image gauge = expBarGauge != null ? expBarGauge : xpFill;
        if (gauge == null) return;
        ConfigureExperienceGauge(gauge);
        gauge.fillAmount = ScoreManager.Instance != null ? ScoreManager.Instance.XpRatio : 0f;
        if (xpLabel != null)
            xpLabel.text = $"Lv {(ScoreManager.Instance != null ? ScoreManager.Instance.Level + 1 : 1)}";
    }

    private static void ConfigureExperienceGauge(Image gauge)
    {
        if (gauge == null) return;
        gauge.color = Color.white;
        gauge.type = Image.Type.Filled;
        gauge.fillMethod = Image.FillMethod.Horizontal;
        gauge.fillOrigin = 0;
        gauge.fillClockwise = true;
    }

    private void LoseGame()
    {
        GameResult.Set(false, Score);
        SceneManager.LoadScene(endSceneName);
    }

    private void WinGame()
    {
        GameResult.Set(true, Score);
        SceneManager.LoadScene(endSceneName);
    }
}
