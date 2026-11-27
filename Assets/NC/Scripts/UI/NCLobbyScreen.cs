using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대기실(UI 기획서 05장). 게임 전에 두 사람이 연결·준비됐는지 확인한다.
///  - 1P·2P 카드: 참가 대기 / 준비 중 / 준비 완료, 호스트 표식, 내 카드 표시
///  - [준비]/[준비 취소]: 자기 상태만 요청(호스트가 확정)
///  - [게임 시작]: 호스트만, 필요 인원이 모두 준비했을 때 활성. 참가자는 "호스트가 시작합니다"
///  - [방 나가기]: 확인 후 이탈 · 방 코드 [복사]
/// 캐릭터 선택은 없다.
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

        // [UI 프리팹 방식] 아트 프리팹(Variant)을 불러와 canvas 아래에 생성한다. (NCConnectScreen과 같은 패턴)
        GameObject prefab = Resources.Load<GameObject>("UI/Scenes/UI_Lobby");
        if (prefab == null)
        {
            Debug.LogError("[UI] UI_Lobby 프리팹을 찾을 수 없음 — 경로 확인");
            return;
        }

        NCLobbyView view = Object.Instantiate(prefab, canvas, false).GetComponent<NCLobbyView>();
        if (view == null)
        {
            Debug.LogError("[UI] UI_Lobby 루트에 NCLobbyView가 없음 — Variant인지, 루트에 붙었는지 확인");
            return;
        }

        _root = view.gameObject;

        // 프리팹 루트를 화면 전체에 맞춘다 (Scale 0·크기 0 방지)
        RectTransform rect = (RectTransform)view.transform;
        NCUiKit.Stretch(rect);
        rect.localScale = Vector3.one;
        rect.anchoredPosition3D = Vector3.zero;

        // 프리팹 안의 요소를 기존 변수에 넣는다 → Tick 코드는 그대로 동작
        _code = view.CodeText;
        _status = view.StatusText;
        _ready = view.ReadyButton;
        _readyLabel = view.ReadyLabel;
        _start = view.StartButton;
        _cards[0] = new SlotCard(0, view.Slot1P, view.ReadySprite, view.NotReadySprite);
        _cards[1] = new SlotCard(1, view.Slot2P, view.ReadySprite, view.NotReadySprite);
        // 아직 프리팹에 없는 요소 — null이면 Tick에서 건너뛴다
        // _copied("코드를 복사했습니다"), _waitHost("호스트가 시작합니다"), _leftBanner("동료가 나갔습니다")

        // 버튼을 눌렀을 때 실행할 함수 연결 (함수 내용은 기존 그대로)
        view.CopyButton.onClick.AddListener(Copy);
        _ready.onClick.AddListener(ToggleReady);
        _start.onClick.AddListener(StartGame);
        view.LeaveButton.onClick.AddListener(AskLeave);
    }

    public void SetVisible(bool visible)
    {
        if (_root == null) return; // 프리팹을 못 불러왔으면 아무것도 안 함

        if (_root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }

        // 캐릭터 미리보기 카메라는 대기실이 보일 때만 돈다.
        foreach (SlotCard card in _cards)
        {
            card?.SetPreviewActive(visible);
        }
    }

    public void Tick(NCSession session, NCLauncher launcher)
    {
        if (_root == null) return; // 프리팹을 못 불러왔으면 아무것도 안 함

        NCPlayer me = NCPlayer.Local;
        bool isHost = session.Runner != null && session.Runner.IsServer;
        int required = NCBalance.Get.RequiredPlayers;

        // 상대 이탈 안내(새로 생겼을 때만)
        if (_seenPartnerLeft < 0)
        {
            _seenPartnerLeft = session.PartnerLeftCount;
        }
        else if (session.PartnerLeftCount != _seenPartnerLeft)
        {
            _seenPartnerLeft = session.PartnerLeftCount;
            _leftBannerUntil = Time.time + 3f;
        }

        if (_leftBanner != null) _leftBanner.SetActive(Time.time < _leftBannerUntil);
        if (_copied != null) _copied.gameObject.SetActive(Time.time < _copiedUntil);

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
            _code.text = launcher != null ? launcher.RoomCode : ""; // "방 코드" 라벨은 프리팹에 따로 있음
            _status.text = count < required
                ? $"◆  상대를 기다리는 중 ({count}/{required})  ◆"
                : (ready < count ? $"◆  준비를 기다리는 중 ({ready}/{count})  ◆" : "◆  모두 준비되었습니다  ◆");
            _readyLabel.text = meReady ? "준비 취소" : "준비";
        }

        _ready.interactable = me != null;
        _start.gameObject.SetActive(isHost);
        _start.interactable = isHost && session.CanStart && Time.time >= _startLockedUntil;
        if (_waitHost != null) _waitHost.gameObject.SetActive(isHost == false);
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
        // [UI 프리팹 방식] 카드 모양은 아트 프리팹에 있고, 여기서는 참조(NCLobbySlotView)만 받는다.
        // 카드 배경·테두리 색은 아트 그대로 두고 덮어쓰지 않는다.
        // deco_image(장식)는 항상 보이므로 코드에서 건드리지 않는다.
        private readonly TextMeshProUGUI _state; // readytext
        private readonly Image _stateIcon;       // readystatus/icon — 준비 완료=체크, 준비 중=엑스, 참가 대기=숨김
        private readonly Sprite _readySprite;
        private readonly Sprite _notReadySprite;
        private readonly GameObject _readyFrame;  // 준비 완료면 카드 둘레에 녹색 테두리(아트 바탕이 어두워 색 섞기로는 티가 안 남)
        private readonly NCLobbyPreview _preview; // 카드 가운데 캐릭터
        private int _shownState = -1;
        // "나" 표시(_me)는 아직 프리팹에 없어 생략 — 추가하면 여기서 다시 연결

        public SlotCard(int slot, NCLobbySlotView view, Sprite readySprite, Sprite notReadySprite)
        {
            _state = view.StateText;
            _stateIcon = view.StateIcon;
            _readySprite = readySprite;
            _notReadySprite = notReadySprite;
            if (view.Panel != null)
            {
                RectTransform card = view.Panel.rectTransform;
                _preview = new NCLobbyPreview(slot, card, view.Character != null ? view.Character.transform : null);

                Image frame = NCUiKit.NewImage("ReadyFrame", card, NCUiKit.FrameSprite, NCStyle.Success);
                frame.pixelsPerUnitMultiplier = 0.4f; // 공용 테두리(2px)를 굵게
                NCUiKit.Stretch(frame.rectTransform, -3f);
                _readyFrame = frame.gameObject;
                _readyFrame.SetActive(false);
            }
        }

        public void SetPreviewActive(bool active) => _preview?.SetActive(active);

        public void Show(NCPlayer player, NCPlayer me)
        {
            bool joined = player != null && player.Object != null && player.Object.IsValid;
            int state = joined == false ? 0 : (player.Ready ? 2 : 1);

            _preview?.Tick(state); // 회전·점프·빛은 매 프레임

            if (state == _shownState)
            {
                return;
            }

            _shownState = state;

            if (_readyFrame != null)
            {
                _readyFrame.SetActive(state == 2);
            }

            if (_stateIcon != null)
            {
                // 체크/엑스 그림이 비어 있으면 프리팹에 원래 있던 그림을 그대로 쓴다
                Sprite sprite = state == 2 ? _readySprite : _notReadySprite;
                if (sprite != null) _stateIcon.sprite = sprite;
                _stateIcon.gameObject.SetActive(joined);
            }
            if (_state != null)
            {
                _state.text = state == 0 ? "참가 대기" : (state == 1 ? "준비 중" : "준비 완료");
                _state.color = state == 2 ? NCStyle.Success : (state == 1 ? NCStyle.Text : NCStyle.TextDim);
            }
        }
    }
}
