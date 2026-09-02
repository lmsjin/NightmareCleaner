using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// 내 버전 미니게임 ② — 게이지 스톱(다단계).
// 좌우로 왕복하는 마커를 중앙 성공 구간에서 E로 멈추면 한 단계 클리어.
// 단계가 오를수록 마커가 빨라지고 성공구간이 좁아진다. 제한시간 안에 최대한 많은 단계를 깰수록 점수↑.
// 빗맞혀도(중앙 밖 E) 종료되지 않고 계속 — 시간이 유일한 압박. 코드 생성 UI + 신 Input.
public class MiniGameGaugeStop : MonoBehaviour, IRescueMiniGame
{
    [Header("Editable Scene UI")]
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private RectTransform movingMarker;
    [SerializeField] private RectTransform successZone;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Editable Text")]
    [SerializeField] private bool useCanvasTextAsFormat = true;
    [SerializeField] private string stageFormat = "단계 : {0}";
    [SerializeField] private string timeFormat = "남은 시간 : {0}초";
    [TextArea(2, 4)]
    [SerializeField] private string descriptionMessage = "성공 구간에서 E를 누르세요";

    private BalanceConfig _cfg;

    private Action<int> _onComplete;
    private bool  _active;
    private bool  _armed;
    private float _timer;
    private float _phase;        // 왕복 위상
    private int   _stagesCleared;

    // 단계별 난이도 계산용 (BalanceConfig에서 읽은 기준값)
    private float _baseSpeed;    // 0단계 속도
    private float _speedStep;    // 단계당 속도 증가
    private float _baseHalf;     // 0단계 성공 반폭(0~0.5)
    private float _widthShrink;  // 단계당 폭 배율
    private float _minHalf;      // 최소 반폭

    // 현재 단계에서 적용 중인 값
    private float _speed;
    private float _successHalf;  // 성공 구간 반폭(0~0.5)
    private float _zoneCenter;   // 성공 구간 중심(0~1) — 단계마다 랜덤 이동

    void Awake()
    {
        _cfg = BalanceConfig.Resolve(null);
        if (uiRoot != null) uiRoot.SetActive(false);
        HideLegacyStatusText();
        CaptureCanvasTextFormat();
    }

    public void Begin(Action<int> onComplete)
    {
        _onComplete = onComplete;
        _timer      = _cfg != null ? _cfg.gaugeStopTimeLimit : 15f;

        _baseSpeed   = _cfg != null ? Mathf.Max(0.1f, _cfg.gaugeStopSpeed) : 1.2f;
        _speedStep   = _cfg != null ? Mathf.Max(0f, _cfg.gaugeStopSpeedStep) : 0.15f;
        _baseHalf    = (_cfg != null ? Mathf.Clamp01(_cfg.gaugeStopSuccessWidth) : 0.18f) * 0.5f;
        _widthShrink = _cfg != null ? Mathf.Clamp(_cfg.gaugeStopWidthShrink, 0.1f, 1f) : 0.88f;
        _minHalf     = (_cfg != null ? Mathf.Clamp01(_cfg.gaugeStopMinWidth) : 0.05f) * 0.5f;

        _stagesCleared = 0;
        _phase  = 0f;
        _active = true;
        _armed  = false;

        ApplyStageDifficulty(); // 0단계 속도·구간 세팅
        if (uiRoot == null || movingMarker == null || successZone == null)
        {
            Debug.LogError("[MiniGameGaugeStop] 씬 UI 참조가 없습니다.");
            Finish(0);
            return;
        }
        HideLegacyStatusText();
        uiRoot.SetActive(true);
        RefreshStatus();
    }

    public void Cancel()
    {
        _active = false;
        _onComplete = null;
        if (uiRoot != null) uiRoot.SetActive(false);
    }

    void Update()
    {
        if (!_active) return;
        if (PauseMenuController.IsPaused) return; // ESC 메뉴가 열려있는 동안엔 미니게임도 정지

        // 구출 미니게임 도중에는 Time.timeScale이 0으로 떨어져 있는 게 정상(월드 정지)이므로,
        // 미니게임 자체의 진행은 항상 실시간(unscaled)으로 처리한다.
        float dt = Time.unscaledDeltaTime;

        _timer -= dt;
        if (_timer <= 0f) { Finish(_stagesCleared); return; }
        RefreshStatus();

        // 마커 0~1 왕복
        _phase += dt * _speed;
        float t = Mathf.PingPong(_phase, 1f);
        movingMarker.anchorMin = new Vector2(t, 0f);
        movingMarker.anchorMax = new Vector2(t, 1f);
        movingMarker.offsetMin = new Vector2(-7.5f, -5f);
        movingMarker.offsetMax = new Vector2(7.5f, 5f);

        if (!_armed) { _armed = true; return; } // 시작 프레임 E 스킵

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (Mathf.Abs(t - _zoneCenter) <= _successHalf)
            {
                _stagesCleared++;
                ApplyStageDifficulty();
                RefreshStatus();
            }
            // 빗맞힘 → 아무 일 없음(계속 진행)
        }
    }

    // 현재 단계 수에 맞춰 속도·성공구간 폭을 갱신
    void ApplyStageDifficulty()
    {
        _speed       = _baseSpeed + _speedStep * _stagesCleared;
        _successHalf = Mathf.Max(_minHalf, _baseHalf * Mathf.Pow(_widthShrink, _stagesCleared));

        
        // 성공 구간 중심을 단계마다 랜덤 이동 (양끝으로 새지 않게 반폭만큼 여유)
        _zoneCenter = UnityEngine.Random.Range(_successHalf, 1f - _successHalf);

        successZone.anchorMin = new Vector2(_zoneCenter - _successHalf, 0.5f);
        successZone.anchorMax = new Vector2(_zoneCenter + _successHalf, 0.5f);
        successZone.pivot = new Vector2(0.5f, 0.5f);
        successZone.offsetMin = new Vector2(0f, -15f);
        successZone.offsetMax = new Vector2(0f, 15f);
    }

    void RefreshStatus()
    {
        int stage = _stagesCleared;
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, _timer));

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

    void Finish(int stagesCleared)
    {
        _active = false;
        if (uiRoot != null) uiRoot.SetActive(false);
        var cb = _onComplete; _onComplete = null;
        cb?.Invoke(stagesCleared); // 클리어한 단계 수(0 = 실패)
    }

#if UNITY_EDITOR
    public void SetupEditableUI()
    {
        GameObject canvasObject = EditableMiniGameUI.CreateCanvas(transform, "GaugeStopCanvas", 90);
        if (uiRoot == null)
        {
            uiRoot = canvasObject;
            EditableMiniGameUI.CreateDim(canvasObject.transform);
        }
        Image panel = GetOrCreateImage(canvasObject.transform, "QTEPanel", "[UI]_Minigame/QTEPanel 복사", new Vector2(1390f, 280f));
        Image bar = GetOrCreateImage(panel.transform, "SkillCheckGaugeBar", "[UI]_Minigame/SkillCheckGaugeBar", new Vector2(870f, 60f));
        Image zone = GetOrCreateImage(bar.transform, "SuccessZone", "[UI]_Minigame/SkillCheckSuccessSection", new Vector2(100f, 30f));
        successZone = zone.rectTransform;
        successZone.anchorMin = new Vector2(0.45f, 0.5f);
        successZone.anchorMax = new Vector2(0.55f, 0.5f);
        successZone.offsetMin = new Vector2(0f, -15f);
        successZone.offsetMax = new Vector2(0f, 15f);
        Image marker = GetOrCreateImage(bar.transform, "MovingMarker", "[UI]_Minigame/SkillCheckHandle", new Vector2(15f, 70f));
        movingMarker = marker.rectTransform;
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

    private static Image GetOrCreateImage(Transform parent, string name, string path, Vector2 size)
    {
        Transform existing = parent.Find(name);
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image == null)
            image = EditableMiniGameUI.CreateImage(parent, name, null, size);
        image.sprite = UIRealTheme.LoadSprite(path);
        return image;
    }
#endif
}