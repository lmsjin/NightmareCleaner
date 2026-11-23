using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대기실 1P·2P 카드 한 장(LOBBY-01)의 UI 참조 묶음.
/// MonoBehaviour가 아니라 NCLobbyView 안에 두 개(1P·2P) 들어가는 데이터 묶음이다.
/// 참조만 들고 로직은 없다 — 표시 규칙은 NCLobbyScreen이 정한다.
/// </summary>
[System.Serializable]
public class NCLobbySlotView
{
    [Tooltip("카드 배경 (playerpanel_1P / playerpanel_2P)")]
    [SerializeField] private Image _panel;

    [Tooltip("장식 이미지 (deco_image) — 항상 보인다. 지금은 코드에서 쓰지 않음")]
    [SerializeField] private Image _character;

    [Tooltip("준비 상태 바탕 (readystatus)")]
    [SerializeField] private Image _stateBox;

    [Tooltip("준비 상태 글자 (readystatus/readytext) — 참가 대기 / 준비 중 / 준비 완료")]
    [SerializeField] private TextMeshProUGUI _stateText;

    [Tooltip("준비 상태 아이콘 (readystatus/icon) — 준비 완료=체크, 준비 중=엑스, 참가 대기=숨김")]
    [SerializeField] private Image _stateIcon;

    [Tooltip("호스트 표식 (host_icon) — 1P 카드에만 있음. 2P는 비워 둔다")]
    [SerializeField] private GameObject _hostMark;

    public Image Panel => _panel;
    public Image Character => _character;
    public Image StateBox => _stateBox;
    public TextMeshProUGUI StateText => _stateText;
    public Image StateIcon => _stateIcon;
    public GameObject HostMark => _hostMark;

    /// <summary>
    /// (에디터 전용) 카드 오브젝트 아래에서 아트 프리팹 이름으로 참조를 찾아 채운다.
    /// NCLobbyView.Reset()에서 호출한다.
    /// </summary>
    public void AutoFill(Transform card)
    {
        if (card == null) return;

        _panel = card.GetComponent<Image>();
        _character = Find<Image>(card, "deco_image");
        _stateBox = Find<Image>(card, "readystatus");
        _stateText = Find<TextMeshProUGUI>(card, "readystatus/readytext");
        _stateIcon = Find<Image>(card, "readystatus/icon");

        Transform host = card.Find("host_icon");
        _hostMark = host != null ? host.gameObject : null;
    }

    private static T Find<T>(Transform parent, string path) where T : Component
    {
        Transform found = parent.Find(path);
        return found != null ? found.GetComponent<T>() : null;
    }
}
