using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD 아트 프리팹(Resources/UI/Scenes/UI_Entry, Variant · 바탕 UI_Entry_new) 루트에 붙인다.
/// 화면 요소의 "참조"만 들고 있고 로직은 없다 — 동작은 NCHud가 맡는다.
/// 프리팹에 없는 것(점령 상태 글자·미니맵·알림·머리 위 표시)은 NCHud가 코드로 만든다.
/// 컴포넌트를 처음 붙이거나 ⋮ > Reset 을 누르면 아트 프리팹 이름으로 칸을 채운다.
/// 실행할 때도 비어 있는 칸은 같은 이름으로 채운다(FillMissing, Variant에 저장하지 않아도 동작).
/// </summary>
public class NCEntryView : MonoBehaviour
{
    private const string Top = "CaptureInfo_panel/";

    [Header("HUD-01 시간")]
    [Tooltip("'남은 시간' 글자 (Time_panel/text)")]
    [SerializeField] private TextMeshProUGUI _timeLabel;
    [Tooltip("시간 숫자 MM:SS (Time_panel/timertext)")]
    [SerializeField] private TextMeshProUGUI _timeText;

    [Header("HUD-02 지배율 (1P·2P)")]
    [Tooltip("1P 지배율 글자 (1P_panel/CapturePercentage_text)")]
    [SerializeField] private TextMeshProUGUI _dominanceText1P;
    [Tooltip("1P 지배율 채움 바 (1P_panel/…/CapturePercentage_Full) — 폭을 비율만큼 줄인다")]
    [SerializeField] private RectTransform _dominanceFill1P;
    [Tooltip("2P 지배율 글자 (2P_panel/CapturePercentage_text)")]
    [SerializeField] private TextMeshProUGUI _dominanceText2P;
    [Tooltip("2P 지배율 채움 바 (2P_panel/…/CapturePercentage_Full)")]
    [SerializeField] private RectTransform _dominanceFill2P;

    [Header("조작 안내 (카운트다운에만 보임)")]
    [Tooltip("기본 조작 안내 (ControlsGuide_panel)")]
    [SerializeField] private GameObject _controls;

    [Header("HUD-06 과충전")]
    [Tooltip("과충전 패널 (Overcharge_panel) — 효과 남은 바를 이 아래에 붙인다")]
    [SerializeField] private RectTransform _chargePanel;
    [Tooltip("과충전 아이콘 (OverchargeIcon) — 상태에 따라 밝기·색이 바뀐다")]
    [SerializeField] private Image _chargeIcon;
    [Tooltip("과충전 상태 글자 (OverchargeText)")]
    [SerializeField] private TextMeshProUGUI _chargeText;

    [Header("아직 쓰지 않는 아트 (경기 중 숨김)")]
    [Tooltip("탄약 패널 (Bullet_panel) — 탄창은 머리 위(NCOverhead)에 표시한다")]
    [SerializeField] private GameObject _ammoPanel;
    [Tooltip("효과 칸 (Effect_panel) — 최대 체력·이동속도 보상이 생기면 연결")]
    [SerializeField] private GameObject _effectPanel;

    public TextMeshProUGUI TimeLabel => _timeLabel;
    public TextMeshProUGUI TimeText => _timeText;
    public TextMeshProUGUI DominanceText(int slot) => slot == 0 ? _dominanceText1P : _dominanceText2P;
    public RectTransform DominanceFill(int slot) => slot == 0 ? _dominanceFill1P : _dominanceFill2P;
    public GameObject Controls => _controls;
    public RectTransform ChargePanel => _chargePanel;
    public Image ChargeIcon => _chargeIcon;
    public TextMeshProUGUI ChargeText => _chargeText;
    public GameObject AmmoPanel => _ammoPanel;
    public GameObject EffectPanel => _effectPanel;

    // 에디터에서 컴포넌트를 붙이거나 ⋮ > Reset 을 누르면 실행된다(빌드에서는 호출되지 않음).
    private void Reset() => Fill(true);

    /// <summary>Variant에 저장되지 않은 칸만 이름으로 채운다. NCHud가 Instantiate 직후 부른다.</summary>
    public void FillMissing() => Fill(false);

    private void Fill(bool overwrite)
    {
        Set(ref _timeLabel, Top + "Time_panel/text", overwrite);
        Set(ref _timeText, Top + "Time_panel/timertext", overwrite);

        Set(ref _dominanceText1P, Top + "1P_panel/CapturePercentage_text", overwrite);
        Set(ref _dominanceFill1P, Top + "1P_panel/CapturePercentage_Background (1)/CapturePercentage_Full", overwrite);
        Set(ref _dominanceText2P, Top + "2P_panel/CapturePercentage_text", overwrite);
        Set(ref _dominanceFill2P, Top + "2P_panel/CapturePercentage_Background (1)/CapturePercentage_Full", overwrite);

        SetObject(ref _controls, "ControlsGuide_panel", overwrite);

        Set(ref _chargePanel, "Overcharge_panel", overwrite);
        Set(ref _chargeIcon, "Overcharge_panel/OverchargeIcon", overwrite);
        Set(ref _chargeText, "Overcharge_panel/OverchargeText", overwrite);

        SetObject(ref _ammoPanel, "Bullet_panel", overwrite);
        SetObject(ref _effectPanel, "Effect_panel", overwrite);
    }

    private void Set<T>(ref T field, string path, bool overwrite) where T : Component
    {
        if (overwrite || field == null)
        {
            field = Find<T>(path);
        }
    }

    private void SetObject(ref GameObject field, string path, bool overwrite)
    {
        if (overwrite || field == null)
        {
            Transform found = Find<Transform>(path);
            field = found != null ? found.gameObject : null;
        }
    }

    private T Find<T>(string path) where T : Component
    {
        Transform found = transform.Find(path);
        if (found == null)
        {
            Debug.LogWarning($"[UI] NCEntryView: '{path}' 를 찾지 못함 — Inspector에서 직접 연결하세요");
            return null;
        }
        return found.GetComponent<T>();
    }
}
