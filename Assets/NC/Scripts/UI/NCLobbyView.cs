using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대기실 아트 프리팹(Resources/UI/Scenes/UI_Lobby, Variant) 루트에 붙인다.
/// 화면 요소의 "참조"만 들고 있고 로직은 없다 — 동작은 NCLobbyScreen이 맡는다.
/// 컴포넌트를 처음 붙이면 Reset()이 아트 프리팹 이름으로 칸을 자동으로 채운다.
/// </summary>
public class NCLobbyView : MonoBehaviour
{
    [Header("방 코드 (LOBBY-05)")]
    [Tooltip("방 코드 글자 (code_panel/Image/code/Text (TMP))")]
    [SerializeField] private TextMeshProUGUI _codeText;
    [Tooltip("코드 복사 버튼 (CopyButton)")]
    [SerializeField] private Button _copyButton;

    [Header("1P·2P 카드 (LOBBY-01)")]
    [SerializeField] private NCLobbySlotView _slot1P = new NCLobbySlotView();
    [SerializeField] private NCLobbySlotView _slot2P = new NCLobbySlotView();
    [Tooltip("준비 완료 아이콘 (체크) — Icon_Confirm")]
    [SerializeField] private Sprite _readySprite;
    [Tooltip("준비 전 아이콘 (엑스) — Icon_Cancle")]
    [SerializeField] private Sprite _notReadySprite;

    [Header("상태 문구")]
    [Tooltip("준비 진행 안내 (readynoticetext)")]
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("버튼 (LOBBY-02·03·04)")]
    [Tooltip("준비 / 준비 취소 (readybutton)")]
    [SerializeField] private Button _readyButton;
    [Tooltip("준비 버튼 글자 — '준비' ↔ '준비 취소'로 바뀐다 (readybutton/Text (TMP))")]
    [SerializeField] private TextMeshProUGUI _readyLabel;
    [Tooltip("게임 시작 — 호스트만 보인다 (startbutton)")]
    [SerializeField] private Button _startButton;
    [Tooltip("방 나가기 (Back)")]
    [SerializeField] private Button _leaveButton;

    public TextMeshProUGUI CodeText => _codeText;
    public Button CopyButton => _copyButton;
    public NCLobbySlotView Slot1P => _slot1P;
    public NCLobbySlotView Slot2P => _slot2P;
    public Sprite ReadySprite => _readySprite;
    public Sprite NotReadySprite => _notReadySprite;
    public TextMeshProUGUI StatusText => _statusText;
    public Button ReadyButton => _readyButton;
    public TextMeshProUGUI ReadyLabel => _readyLabel;
    public Button StartButton => _startButton;
    public Button LeaveButton => _leaveButton;

    /// <summary>슬롯 번호(0=1P, 1=2P)로 카드 참조를 꺼낸다.</summary>
    public NCLobbySlotView Slot(int slot) => slot == 0 ? _slot1P : _slot2P;

    // 에디터에서 컴포넌트를 붙이거나 ⋮ > Reset 을 누르면 실행된다(빌드에서는 호출되지 않음).
    // 아트 프리팹의 오브젝트 이름으로 찾아 채운다. 못 찾은 칸은 비어 있으니 Inspector에서 확인.
    private void Reset()
    {
        _codeText = Find<TextMeshProUGUI>("mainpanel/code_panel/Image/code/Text (TMP)");
        _copyButton = Find<Button>("mainpanel/code_panel/Image/CopyButton");

        _slot1P = new NCLobbySlotView();
        _slot2P = new NCLobbySlotView();
        _slot1P.AutoFill(transform.Find("mainpanel/playerpanel_1P"));
        _slot2P.AutoFill(transform.Find("mainpanel/playerpanel_2P"));

        _statusText = Find<TextMeshProUGUI>("mainpanel/readynoticetext");
        _readyButton = Find<Button>("mainpanel/readybutton");
        _readyLabel = Find<TextMeshProUGUI>("mainpanel/readybutton/Text (TMP)");
        _startButton = Find<Button>("mainpanel/startbutton");
        _leaveButton = Find<Button>("Back");
    }

    private T Find<T>(string path) where T : Component
    {
        Transform found = transform.Find(path);
        if (found == null)
        {
            Debug.LogWarning($"[UI] NCLobbyView: '{path}' 를 찾지 못함 — Inspector에서 직접 연결하세요");
            return null;
        }
        return found.GetComponent<T>();
    }
}
