using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 접속 화면(UI 기획서 04장). 방을 여는 사람과 참가하는 사람의 행동을 나란히 둔다.
///  - 방 만들기: 한 번 요청, 연결 중 버튼 잠금
///  - 코드 + 참가하기: 빈 값·형식 오류면 비활성
///  - 연결 상태 영역을 미리 확보해 글자가 생겨도 버튼이 움직이지 않는다
/// </summary>
public class NCConnectScreen
{
    private readonly GameObject _root;
    private readonly Button _createButton;
    private readonly Button _joinButton;
    private readonly Button _cancelButton;
    private readonly TMP_InputField _codeInput;
    private readonly TextMeshProUGUI _status;

    public NCConnectScreen(RectTransform canvas)
    {
        RectTransform root = NCUiKit.NewRect("ConnectScreen", canvas);
        NCUiKit.Stretch(root);
        _root = root.gameObject;

        // 뒤의 저택이 비치도록 반투명하게 어둡게
        Image dim = NCUiKit.NewImage("Dim", root, null, new Color(0.03f, 0.05f, 0.09f, 0.72f));
        NCUiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // 로고
        TextMeshProUGUI logo = NCUiKit.NewText("Logo", root, "NIGHTMARE CLEANER", 92f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1200f, 110f));
        logo.characterSpacing = 6f;
        logo.outlineWidth = 0.18f;
        logo.outlineColor = NCStyle.Hex("234A78");

        TextMeshProUGUI subtitle = NCUiKit.NewText("Subtitle", root, "◆  2인 협동 유령 소탕  ◆", NCStyle.FontGuide, NCStyle.Brass);
        NCUiKit.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(800f, 40f));

        // 가운데 패널
        RectTransform panel = NCUiKit.NewPanel("Panel", root, deep: true);
        NCUiKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1000f, 460f));
        panel.Find("Frame").GetComponent<Image>().color = NCStyle.Brass;

        TextMeshProUGUI title = NCUiKit.NewText("Title", panel, "멀티 플레이", 44f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(600f, 56f));

        // 방 만들기 카드
        RectTransform createCard = Card(panel, "CreateCard", new Vector2(-235f, -40f), "방 만들기", "새 방을 만들고 동료를 초대하세요");
        _createButton = NCUiKit.NewButton("CreateButton", createCard, "방 만들기", true, OnCreate);
        NCUiKit.Place((RectTransform)_createButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(360f, 64f));

        TextMeshProUGUI people = NCUiKit.NewText("Icon", createCard, "●  ●", 46f, NCStyle.Brass);
        NCUiKit.Place(people.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(200f, 60f));

        // 방 참가 카드
        RectTransform joinCard = Card(panel, "JoinCard", new Vector2(235f, -40f), "방 참가", "동료에게 받은 초대 코드를 입력하세요");
        _codeInput = NCUiKit.NewInput("CodeInput", joinCard, "초대 코드 (예: NC4827)");
        NCUiKit.Place((RectTransform)_codeInput.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(360f, 58f));
        _joinButton = NCUiKit.NewButton("JoinButton", joinCard, "참가하기", true, OnJoin);
        NCUiKit.Place((RectTransform)_joinButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(360f, 64f));

        // 연결 상태(자리 미리 확보)
        _status = NCUiKit.NewText("Status", root, "", NCStyle.FontBody, NCStyle.TextDim);
        NCUiKit.Place(_status.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -310f), new Vector2(1000f, 40f));

        _cancelButton = NCUiKit.NewButton("CancelButton", root, "취소", false, () => NCLauncher.Instance?.Leave());
        NCUiKit.Place((RectTransform)_cancelButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -370f), new Vector2(200f, 52f));

        Button quit = NCUiKit.NewButton("QuitButton", root, "종료", false, Quit);
        NCUiKit.Place((RectTransform)quit.transform, new Vector2(0f, 0f), new Vector2(NCStyle.Margin, NCStyle.Margin), new Vector2(180f, 56f));
    }

    private static RectTransform Card(RectTransform parent, string name, Vector2 position, string title, string description)
    {
        RectTransform card = NCUiKit.NewPanel(name, parent);
        NCUiKit.Place(card, new Vector2(0.5f, 0.5f), position, new Vector2(440f, 300f));

        TextMeshProUGUI heading = NCUiKit.NewText("Heading", card, title, 34f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(400f, 44f));

        TextMeshProUGUI body = NCUiKit.NewText("Description", card, description, 20f, NCStyle.TextDim);
        NCUiKit.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(400f, 30f));
        return card;
    }

    public void SetVisible(bool visible)
    {
        if (_root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    public void Tick(NCLauncher launcher)
    {
        bool idle = launcher != null && launcher.State == NCConnectState.Idle;
        bool connecting = launcher != null && launcher.State == NCConnectState.Connecting;

        _createButton.interactable = idle;
        _codeInput.interactable = idle;
        _joinButton.interactable = idle && NCLauncher.IsValidCode(_codeInput.text);
        _cancelButton.gameObject.SetActive(connecting);

        if (launcher != null)
        {
            _status.text = launcher.StatusMessage;
            _status.color = launcher.StatusIsError ? NCStyle.Danger : NCStyle.Text;
        }
    }

    private void OnCreate()
    {
        NCLauncher.Instance?.CreateRoom();
    }

    private void OnJoin()
    {
        NCLauncher.Instance?.JoinRoom(_codeInput.text);
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
