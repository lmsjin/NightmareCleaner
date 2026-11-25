using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD 아트 프리팹(Resources/UI/Scenes/UI_Entry, Variant) 루트에 붙인다.
/// 화면 요소의 "참조"만 들고 있고 로직은 없다 — 동작은 NCHud가 맡는다.
/// 프리팹에 없는 것(미니맵·알림·머리 위 표시)은 NCHud가 기존처럼 코드로 만든다.
/// 컴포넌트를 처음 붙이면 Reset()이 아트 프리팹 이름으로 칸을 자동으로 채운다.
/// </summary>
public class NCEntryView : MonoBehaviour
{
    [Header("HUD-01·02 시간 · 점령 상태")]
    [Tooltip("'남은 시간' 글자 (Timepanel/text)")]
    [SerializeField] private TextMeshProUGUI _timeLabel;
    [Tooltip("시간 숫자 MM:SS (Timepanel/timertext)")]
    [SerializeField] private TextMeshProUGUI _timeText;
    [Tooltip("점령 구역 상태 (WaveInfo/Text (TMP))")]
    [SerializeField] private TextMeshProUGUI _progressText;

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

    public TextMeshProUGUI TimeLabel => _timeLabel;
    public TextMeshProUGUI TimeText => _timeText;
    public TextMeshProUGUI ProgressText => _progressText;
    public GameObject Controls => _controls;
    public RectTransform ChargePanel => _chargePanel;
    public Image ChargeIcon => _chargeIcon;
    public TextMeshProUGUI ChargeText => _chargeText;

    // 에디터에서 컴포넌트를 붙이거나 ⋮ > Reset 을 누르면 실행된다(빌드에서는 호출되지 않음).
    private void Reset()
    {
        _timeLabel = Find<TextMeshProUGUI>("Timepanel/text");
        _timeText = Find<TextMeshProUGUI>("Timepanel/timertext");
        _progressText = Find<TextMeshProUGUI>("WaveInfo/Text (TMP)");

        _controls = FindObject("ControlsGuide_panel");

        _chargePanel = Find<RectTransform>("Overcharge_panel");
        _chargeIcon = Find<Image>("Overcharge_panel/OverchargeIcon");
        _chargeText = Find<TextMeshProUGUI>("Overcharge_panel/OverchargeText");
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

    private GameObject FindObject(string path)
    {
        Transform found = Find<Transform>(path);
        return found != null ? found.gameObject : null;
    }
}
