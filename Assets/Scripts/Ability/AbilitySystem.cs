using TMPro;
using UnityEngine;

public class AbilitySystem : MonoBehaviour
{
    [Header("Canvas UI")]
    [SerializeField] private GameObject abilityPanel;
    [SerializeField] private TMP_Text suctionPowerBonusText;
    [SerializeField] private TMP_Text suctionRangeBonusText;
    [SerializeField] private TMP_Text moveSpeedBonusText;

    [Header("효과음")]
    [SerializeField] private AudioSource cardClickAudioSource; // 비워두면 자동 생성
    [SerializeField] private AudioClip   cardClickClip;        // 카드 클릭 시 재생할 사운드
    [SerializeField, Range(0f, 1f)] private float cardClickVolume = 1f;

    private bool open;
    private int pending;
    private BalanceConfig config;
    private IAbilityStatsReadOnly playerStats;

    private void Awake()
    {
        config = BalanceConfig.Resolve(null);
        playerStats = FindPlayerStats();
        Transform panelRoot = abilityPanel != null ? abilityPanel.transform : transform;
        ResolveBonusTexts(panelRoot);
        UIRealTheme.SkinButton(panelRoot, "SuctionPowerButton", "[UI]_AbilitySelect/AbilitySelectBox_Inhalation");
        UIRealTheme.SkinButton(panelRoot, "SuctionRangeButton", "[UI]_AbilitySelect/AbilitySelectBox_Intersection");
        UIRealTheme.SkinButton(panelRoot, "MoveSpeedButton", "[UI]_AbilitySelect/AbilitySelectBox_Speed");
        RefreshBonusTexts();
        InitClickAudio();

        if (Application.isPlaying && abilityPanel != null)
            abilityPanel.SetActive(false);
    }

    private void InitClickAudio()
    {
        if (cardClickAudioSource == null)
            cardClickAudioSource = gameObject.AddComponent<AudioSource>();

        cardClickAudioSource.playOnAwake = false;
        cardClickAudioSource.loop        = false;
    }

    private void OnEnable()
    {
        config = BalanceConfig.Resolve(null);
        playerStats = FindPlayerStats();
        ResolveBonusTexts(abilityPanel != null ? abilityPanel.transform : transform);
        RefreshBonusTexts();
        GameEvents.OnAbilityTrigger += Show;
    }

    private void OnDisable()
    {
        GameEvents.OnAbilityTrigger -= Show;
    }

    private void Show()
    {
        playerStats = FindPlayerStats();
        RefreshBonusTexts();
        pending++;
        TryOpen();
    }

    private void TryOpen()
    {
        if (open || pending <= 0 || abilityPanel == null)
            return;

        open = true;
        abilityPanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ChooseSuctionPower() => Choose(AbilityType.SuctionPower);
    public void ChooseSuctionRange() => Choose(AbilityType.SuctionRange);
    public void ChooseMoveSpeed() => Choose(AbilityType.MoveSpeed);

    private void Choose(AbilityType type)
    {
        if (!open)
            return;

        PlayCardClickSfx();

        GameEvents.RaiseAbilitySelected(type);
        pending = Mathf.Max(0, pending - 1);
        if (pending > 0)
        {
            RefreshBonusTexts();
            return;
        }

        open = false;
        if (abilityPanel != null)
            abilityPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void PlayCardClickSfx()
    {
        if (cardClickAudioSource != null && cardClickClip != null)
            cardClickAudioSource.PlayOneShot(cardClickClip, cardClickVolume);
    }

    private void RefreshBonusTexts()
    {
        if (config == null) config = BalanceConfig.Resolve(null);
        SetBonusText(suctionPowerBonusText, AbilityType.SuctionPower);
        SetBonusText(suctionRangeBonusText, AbilityType.SuctionRange);
        SetBonusText(moveSpeedBonusText, AbilityType.MoveSpeed);
    }

    private void SetBonusText(TMP_Text target, AbilityType type)
    {
        if (target == null || config == null) return;
        float amount = config.GetAbilityBonus(type);
        float current = GetCurrentStat(type);
        float increased = current + amount;
        string title = GetAbilityTitle(type);
        string statName = GetAbilityStatName(type);
        target.richText = true;
        target.text = $"{title} +{amount:0.##}\n<color=#55E56B>{statName} {current:0.##} > {increased:0.##}</color>";
    }

    private float GetCurrentStat(AbilityType type)
    {
        if (playerStats != null)
        {
            return type switch
            {
                AbilityType.SuctionPower => playerStats.SuctionPower,
                AbilityType.SuctionRange => playerStats.SuctionRange,
                AbilityType.MoveSpeed => playerStats.MoveSpeed,
                _ => 0f
            };
        }

        return type switch
        {
            AbilityType.SuctionPower => config.suctionPower,
            AbilityType.SuctionRange => config.suctionRange,
            AbilityType.MoveSpeed => config.playerMoveSpeed,
            _ => 0f
        };
    }

    private static IAbilityStatsReadOnly FindPlayerStats()
    {
        foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (component is IAbilityStatsReadOnly stats)
                return stats;
        return null;
    }

    private void ResolveBonusTexts(Transform panelRoot)
    {
        suctionPowerBonusText = ResolveBonusText(panelRoot, "SuctionPowerButton", suctionPowerBonusText);
        suctionRangeBonusText = ResolveBonusText(panelRoot, "SuctionRangeButton", suctionRangeBonusText);
        moveSpeedBonusText = ResolveBonusText(panelRoot, "MoveSpeedButton", moveSpeedBonusText);
        HideLegacyLabel(panelRoot, "SuctionPowerButton", suctionPowerBonusText);
        HideLegacyLabel(panelRoot, "SuctionRangeButton", suctionRangeBonusText);
        HideLegacyLabel(panelRoot, "MoveSpeedButton", moveSpeedBonusText);
    }

    private static void HideLegacyLabel(Transform root, string cardName, TMP_Text activeText)
    {
        Transform card = UIRealTheme.FindDeep(root, cardName);
        Transform label = card != null ? card.Find("Label") : null;
        if (label != null && (activeText == null || label.gameObject != activeText.gameObject))
            label.gameObject.SetActive(false);
    }

    private static TMP_Text ResolveBonusText(Transform root, string cardName, TMP_Text assigned)
    {
        if (assigned != null) return assigned;
        Transform card = UIRealTheme.FindDeep(root, cardName);
        if (card == null) return null;
        Transform bonus = card.Find("BonusText");
        if (bonus != null) return bonus.GetComponent<TMP_Text>();
        Transform label = card.Find("Label");
        return label != null ? label.GetComponent<TMP_Text>() : null;
    }

    private static string GetAbilityTitle(AbilityType type)
    {
        return type switch
        {
            AbilityType.SuctionPower => "공격력 증가",
            AbilityType.SuctionRange => "사거리 증가",
            AbilityType.MoveSpeed => "이동속도 증가",
            _ => string.Empty
        };
    }

    private static string GetAbilityStatName(AbilityType type)
    {
        return type switch
        {
            AbilityType.SuctionPower => "공격력",
            AbilityType.SuctionRange => "사거리",
            AbilityType.MoveSpeed => "이동속도",
            _ => string.Empty
        };
    }

#if UNITY_EDITOR
    [ContextMenu("Setup Editable Ability Bonus Texts")]
    public void SetupEditableBonusTexts()
    {
        Transform panelRoot = abilityPanel != null ? abilityPanel.transform : transform;
        suctionPowerBonusText = GetOrCreateBonusText(panelRoot, "SuctionPowerButton", suctionPowerBonusText);
        suctionRangeBonusText = GetOrCreateBonusText(panelRoot, "SuctionRangeButton", suctionRangeBonusText);
        moveSpeedBonusText = GetOrCreateBonusText(panelRoot, "MoveSpeedButton", moveSpeedBonusText);
        config = BalanceConfig.Resolve(null);
        RefreshBonusTexts();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private static TMP_Text GetOrCreateBonusText(Transform root, string cardName, TMP_Text assigned)
    {
        if (assigned != null) return assigned;
        Transform card = UIRealTheme.FindDeep(root, cardName);
        if (card == null) return null;

        Transform existing = card.Find("BonusText");
        if (existing != null) return existing.GetComponent<TMP_Text>();

        GameObject textObject = new("BonusText", typeof(RectTransform), typeof(TextMeshProUGUI));
        UnityEditor.Undo.RegisterCreatedObjectUndo(textObject, $"Create {cardName} bonus text");
        textObject.transform.SetParent(card, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0f);
        rect.anchorMax = new Vector2(0.9f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 55f);
        rect.sizeDelta = new Vector2(0f, 90f);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        TMP_Text title = card.GetComponentInChildren<TMP_Text>(true);
        if (title != null) text.font = title.font;
        text.fontSize = 42f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }
#endif
}