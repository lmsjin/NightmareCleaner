using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD 체력 패널 한 개(HUD-03·04)의 UI 참조 묶음. NCEntryView 안에 1P·2P 두 개가 들어간다.
/// 참조만 들고 로직은 없다 — 표시 규칙은 NCHud가 정한다.
/// </summary>
[System.Serializable]
public class NCEntryHealthView
{
    [Tooltip("패널 전체 (1P_info_panel / 2P_info_panel) — 플레이어가 없으면 숨긴다")]
    [SerializeField] private GameObject _panel;

    [Tooltip("번호 글자 (Player_text) — 1P / 2P")]
    [SerializeField] private TextMeshProUGUI _slotText;

    [Tooltip("체력 숫자 (HP_text) — 100 / 100, 다운이면 '다운'")]
    [SerializeField] private TextMeshProUGUI _hpText;

    [Tooltip("체력 바 채움 (HP_Background/HP_Fill) — 왼쪽 기준으로 너비가 줄어든다")]
    [SerializeField] private Image _hpFill;

    public GameObject Panel => _panel;
    public TextMeshProUGUI SlotText => _slotText;
    public TextMeshProUGUI HpText => _hpText;
    public Image HpFill => _hpFill;

    /// <summary>(에디터 전용) 패널 오브젝트 아래에서 아트 프리팹 이름으로 참조를 찾아 채운다.</summary>
    public void AutoFill(Transform panel)
    {
        if (panel == null) return;

        _panel = panel.gameObject;
        _slotText = Find<TextMeshProUGUI>(panel, "Player_text");
        _hpText = Find<TextMeshProUGUI>(panel, "HP_text");
        _hpFill = Find<Image>(panel, "HP_Background/HP_Fill");
    }

    private static T Find<T>(Transform parent, string path) where T : Component
    {
        Transform found = parent.Find(path);
        return found != null ? found.GetComponent<T>() : null;
    }
}
