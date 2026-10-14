using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대기실(UI 기획서 05장). 게임 전에 두 사람이 연결·준비됐는지 확인한다.
///  - 1P·2P 카드: 참가 대기 / 준비 중 / 준비 완료, 호스트 표식, 내 카드 표시
///  - [준비]/[준비 취소]: 자기 상태만 요청(호스트가 확정)
///  - [게임 시작]: 호스트만, 필요 인원이 모두 준비했을 때 활성. 참가자는 "호스트가 시작합니다"
///  - [방 나가기]: 확인 후 이탈 · 방 코드 [복사]
/// 캐릭터 선택은 없다(고정 두 청소부).
/// </summary>
public class NCLobbyScreen
{
    private readonly GameObject _root;
    private readonly NCConfirmPopup _confirm;
    private readonly TextMeshProUGUI _code;
    private readonly TextMeshProUGUI _copied;
    private readonly SlotCard[] _cards = new SlotCard[2];
    private readonly TextMeshProUGUI _status;
    private readonly Button _ready;
    private readonly TextMeshProUGUI _readyLabel;
    private readonly Button _start;
    private readonly TextMeshProUGUI _waitHost;
    private readonly GameObject _leftBanner;

    private float _copiedUntil;
    private float _startLockedUntil;
    private float _leftBannerUntil;
    private int _seenPartnerLeft = -1;
    private int _shownKey = -1;

    public NCLobbyScreen(RectTransform canvas, NCConfirmPopup confirm)
    {
        _confirm = confirm;

        RectTransform root = NCUiKit.NewRect("LobbyScreen", canvas);
        NCUiKit.Stretch(root);
        _root = root.gameObject;

        Image dim = NCUiKit.NewImage("Dim", root, null, new Color(0.03f, 0.05f, 0.09f, 0.6f));
        NCUiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        TextMeshProUGUI logo = NCUiKit.NewText("Logo", root, "NIGHTMARE CLEANER", 40f, NCStyle.Text, TextAlignmentOptions.Left, FontStyles.Bold);
        NCUiKit.Place(logo.rectTransform, new Vector2(0f, 1f), new Vector2(NCStyle.Margin, -NCStyle.Margin), new Vector2(600f, 50f));
        logo.characterSpacing = 4f;

        RectTransform panel = NCUiKit.NewPanel("Panel", root, deep: true);
        NCUiKit.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(900f, 680f));
        panel.Find("Frame").GetComponent<Image>().color = NCStyle.Brass;

        TextMeshProUGUI title = NCUiKit.NewText("Title", panel, "대기실", 52f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(500f, 64f));

        // 방 코드 + 복사
        RectTransform codeBar = NCUiKit.NewPanel("CodeBar", panel);
        NCUiKit.Place(codeBar, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(420f, 52f));
        _code = NCUiKit.NewText("Code", codeBar, "", 24f, NCStyle.Text, TextAlignmentOptions.Left);
        NCUiKit.Place(_code.rectTransform, new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(280f, 40f));
        Button copy = NCUiKit.NewButton("Copy", codeBar, "복사", true, Copy);
        NCUiKit.Place((RectTransform)copy.transform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(100f, 40f));
        _copied = NCUiKit.NewText("Copied", panel, "코드를 복사했습니다", 18f, NCStyle.Success);
        NCUiKit.Place(_copied.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(300f, 24f));

        // 1P·2P 카드
        _cards[0] = new SlotCard(panel, 0, new Vector2(-200f, -10f));
        _cards[1] = new SlotCard(panel, 1, new Vector2(200f, -10f));

        // 상태 문구
        _status = NCUiKit.NewText("Status", panel, "", 24f, NCStyle.Brass, style: FontStyles.Bold);
        NCUiKit.Place(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(700f, 34f));

        // 준비 / 게임 시작
        _ready = NCUiKit.NewButton("Ready", panel, "준비", false, ToggleReady);
        NCUiKit.Place((RectTransform)_ready.transform, new Vector2(0.5f, 0f), new Vector2(-150f, 30f), new Vector2(260f, 68f));
        _readyLabel = _ready.transform.Find("Label").GetComponent<TextMeshProUGUI>();

        _start = NCUiKit.NewButton("Start", panel, "게임 시작", true, StartGame);
        NCUiKit.Place((RectTransform)_start.transform, new Vector2(0.5f, 0f), new Vector2(150f, 30f), new Vector2(260f, 68f));

        _waitHost = NCUiKit.NewText("WaitHost", panel, "호스트가 시작합니다", 22f, NCStyle.TextDim);
        NCUiKit.Place(_waitHost.rectTransform, new Vector2(0.5f, 0f), new Vector2(150f, 48f), new Vector2(260f, 32f));

        Button leave = NCUiKit.NewButton("Leave", root, "방 나가기", false, AskLeave);
        NCUiKit.Place((RectTransform)leave.transform, new Vector2(0f, 0f), new Vector2(NCStyle.Margin, NCStyle.Margin), new Vector2(200f, 56f));

        // 동료 이탈 안내
        RectTransform banner = NCUiKit.NewPanel("PartnerLeft", root, deep: true);
        NCUiKit.Place(banner, new Vector2(0.5f, 1f), new Vector2(0f, -NCStyle.Margin), new Vector2(460f, 56f));
        banner.Find("Frame").GetComponent<Image>().color = NCStyle.Danger;
        TextMeshProUGUI bannerText = NCUiKit.NewText("Text", banner, "동료가 나갔습니다. 대기실로 돌아왔습니다", 22f, NCStyle.Text);
        NCUiKit.Stretch(bannerText.rectTransform);
        _leftBanner = banner.gameObject;
    }

    public void SetVisible(bool visible)
    {
        if (_root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    public void Tick(NCSession session, NCLauncher launcher)
    {
        NCPlayer me = NCPlayer.Local;
        bool isHost = session.Runner != null && session.Runner.IsServer;
        int required = NCBalance.Get.RequiredPlayers;

        // 동료 이탈 안내(새로 생겼을 때만)
        if (_seenPartnerLeft < 0)
        {
            _seenPartnerLeft = session.PartnerLeftCount;
        }
        else if (session.PartnerLeftCount != _seenPartnerLeft)
        {
            _seenPartnerLeft = session.PartnerLeftCount;
            _leftBannerUntil = Time.time + 3f;
        }

        _leftBanner.SetActive(Time.time < _leftBannerUntil);
        _copied.gameObject.SetActive(Time.time < _copiedUntil);

        for (int slot = 0; slot < 2; slot++)
        {
            _cards[slot].Show(NCPlayer.FindBySlot(slot), me);
        }

        int count = NCPlayer.All.Count;
        int ready = 0;
        foreach (NCPlayer player in NCPlayer.All)
        {
            ready += player.Ready ? 1 : 0;
        }

        bool meReady = me != null && me.Object != null && me.Object.IsValid && me.Ready;

        // 글자는 상태가 바뀔 때만 다시 만든다.
        int key = count * 100 + ready * 10 + (meReady ? 1 : 0);
        if (key != _shownKey || launcher != null && _code.text.Length == 0)
        {
            _shownKey = key;
            _code.text = launcher != null ? $"방 코드   <b><color=#F2C34E>{launcher.RoomCode}</color></b>" : "";
            _status.text = count < required
                ? $"◆  동료를 기다리는 중 ({count}/{required})  ◆"
                : (ready < count ? $"◆  준비를 기다리는 중 ({ready}/{count})  ◆" : "◆  모두 준비되었습니다  ◆");
            _readyLabel.text = meReady ? "준비 취소" : "준비";
        }

        _ready.interactable = me != null;
        _start.gameObject.SetActive(isHost);
        _start.interactable = isHost && session.CanStart && Time.time >= _startLockedUntil;
        _waitHost.gameObject.SetActive(isHost == false);
    }

    private static void ToggleReady()
    {
        NCPlayer me = NCPlayer.Local;
        if (me != null && me.Object != null && me.Object.IsValid)
        {
            me.RpcSetReady(me.Ready == false);
        }
    }

    private void StartGame()
    {
        // 한 번만 요청하도록 잠깐 잠근다(호스트가 다음 Tick에 확정).
        _startLockedUntil = Time.time + 1f;
        NCSession.Instance?.RequestStart();
    }

    private void Copy()
    {
        if (NCLauncher.Instance != null)
        {
            GUIUtility.systemCopyBuffer = NCLauncher.Instance.RoomCode;
            _copiedUntil = Time.time + 1.5f;
        }
    }

    private void AskLeave()
    {
        _confirm.Show("방에서 나가시겠습니까?", "나가기", () => NCLauncher.Instance?.Leave());
    }

    // ───────── 슬롯 카드 ─────────

    private class SlotCard
    {
        private readonly int _slot;
        private readonly Image _back;
        private readonly Image _frame;
        private readonly GameObject _figure;
        private readonly TextMeshProUGUI _state;
        private readonly Image _statePill;
        private readonly GameObject _me;
        private int _shownState = -1;

        public SlotCard(RectTransform parent, int slot, Vector2 position)
        {
            _slot = slot;
            Color color = NCStyle.SlotColor(slot);

            RectTransform card = NCUiKit.NewPanel("Card" + slot, parent);
            NCUiKit.Place(card, new Vector2(0.5f, 0.5f), position, new Vector2(340f, 330f));
            _back = card.GetComponent<Image>();
            _frame = card.Find("Frame").GetComponent<Image>();

            RectTransform tag = NCUiKit.NewSlotTag("Tag", card, slot, 36f);
            NCUiKit.Place(tag, new Vector2(0f, 1f), new Vector2(14f, -14f), tag.sizeDelta);

            if (slot == 0)
            {
                TextMeshProUGUI host = NCUiKit.NewText("Host", card, "◆ 호스트", 20f, NCStyle.Brass, TextAlignmentOptions.Left, FontStyles.Bold);
                NCUiKit.Place(host.rectTransform, new Vector2(0f, 1f), new Vector2(84f, -18f), new Vector2(140f, 30f));
            }

            TextMeshProUGUI me = NCUiKit.NewText("Me", card, "나", 20f, NCStyle.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            NCUiKit.Place(me.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -18f), new Vector2(60f, 30f));
            _me = me.gameObject;

            // 청소부 실루엣(슬롯 색): 머리 + 몸통 + 등의 탱크
            RectTransform figure = NCUiKit.NewRect("Figure", card);
            NCUiKit.Place(figure, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(160f, 180f));
            Image tank = NCUiKit.NewImage("Tank", figure, NCUiKit.RoundSprite, NCStyle.Hex("8C9099"));
            NCUiKit.Place(tank.rectTransform, new Vector2(0.5f, 0f), new Vector2(-44f, 34f), new Vector2(40f, 80f));
            Image body = NCUiKit.NewImage("Body", figure, NCUiKit.RoundSprite, color);
            NCUiKit.Place(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(110f, 120f));
            Image head = NCUiKit.NewImage("Head", figure, NCUiKit.CircleSprite, NCStyle.Hex("EDE6DA"));
            NCUiKit.Place(head.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(84f, 84f));
            Image goggles = NCUiKit.NewImage("Goggles", head.rectTransform, NCUiKit.RoundSprite, NCStyle.Hex("1E1E26"));
            NCUiKit.Place(goggles.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(60f, 22f));
            _figure = figure.gameObject;

            // 상태 표시
            _statePill = NCUiKit.NewImage("StatePill", card, NCUiKit.RoundSprite, NCStyle.BarBack);
            NCUiKit.Place(_statePill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(220f, 46f));
            _state = NCUiKit.NewText("State", _statePill.transform, "", 22f, NCStyle.Text, style: FontStyles.Bold);
            NCUiKit.Stretch(_state.rectTransform);
        }

        public void Show(NCPlayer player, NCPlayer me)
        {
            bool joined = player != null && player.Object != null && player.Object.IsValid;
            int state = joined == false ? 0 : (player.Ready ? 2 : 1);

            _me.SetActive(joined && player == me);

            if (state == _shownState)
            {
                return;
            }

            _shownState = state;
            Color color = NCStyle.SlotColor(_slot);

            _figure.SetActive(joined);
            _state.text = state == 0 ? "참가 대기" : (state == 1 ? "준비 중" : "● 준비 완료");
            _state.color = state == 2 ? NCStyle.Success : (state == 1 ? NCStyle.Text : NCStyle.TextDim);
            _statePill.color = state == 2 ? NCStyle.Hex("1E3A2B", 0.95f) : NCStyle.BarBack;
            _frame.color = state == 2 ? color : NCStyle.BrassDim;
            _back.color = joined ? NCStyle.Panel : NCStyle.Hex("172536", 0.45f);
        }
    }
}
