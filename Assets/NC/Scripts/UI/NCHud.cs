using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD(UI 기획서 02장). 왼쪽 상단은 비우고, 전투 중엔 시야를 가리지 않는다.
///  - 상단 중앙: 남은 시간(MM:SS) · 아래 소탕 완료 N/M
///  - 그 아래: 잠깐의 안내(MSG) / 입구에서는 의뢰 안내
///  - 왼쪽 하단: 내 체력(크게) · 그 위 동료(작게, 다운 표시)
///  - 오른쪽 하단: 탄약 n/12 · R 재장전 · 재장전 진행
///  - 대상 근처: E 안내 하나 + 진행 바(정리·구조)
///  - 아래 중앙(입구에서만): 기본 조작
/// </summary>
public class NCHud
{
    private readonly GameObject _root;
    private readonly RectTransform _canvas;

    // 상단
    private readonly TextMeshProUGUI _timeText;
    private readonly TextMeshProUGUI _timeLabel;
    private readonly TextMeshProUGUI _progressText;
    private readonly GameObject _roomCode;
    private readonly TextMeshProUGUI _roomCodeText;

    // 입구 안내
    private readonly GameObject _entryGuide;
    private readonly TextMeshProUGUI _entryStatus;
    private readonly GameObject _controls;

    // 알림
    private readonly CanvasGroup _message;
    private readonly TextMeshProUGUI _messageText;
    private readonly Image _messageAccent;
    private float _messageUntil;
    private int _messagePriority;

    // 체력
    private readonly HealthPanel _mine;
    private readonly HealthPanel _mate;

    // 탄약
    private readonly TextMeshProUGUI _ammoText;
    private readonly TextMeshProUGUI _ammoHint;
    private readonly Image _reloadFill;
    private readonly RectTransform _reloadBack;

    // 상호작용 안내
    private readonly RectTransform _prompt;
    private readonly TextMeshProUGUI _promptText;
    private readonly RectTransform _promptKey;
    private readonly Image _promptFill;
    private readonly RectTransform _promptBar;
    private readonly GameObject _promptChips;
    private readonly Image _chip1;
    private readonly Image _chip2;

    // 다운 화면
    private readonly Image _downVignette;
    private readonly GameObject _downPanel;
    private readonly TextMeshProUGUI _downText;
    private readonly Image _downFill;

    // 값이 바뀔 때만 글자를 새로 만들기 위한 이전 값(매 프레임 문자열 생성 → GC 끊김 방지)
    private int _shownSeconds = -1;
    private int _shownEntrance = -1;
    private int _shownCleared = -1;
    private int _shownTotal = -1;
    private string _shownCode;
    private int _shownEntryKey = -1;
    private int _shownAmmo = -1;
    private int _shownAmmoState = -1;

    // 알림 감지용(이전 프레임 값)
    private readonly System.Collections.Generic.Dictionary<NCCombatRoom, NCRoomState> _lastRoomStates =
        new System.Collections.Generic.Dictionary<NCCombatRoom, NCRoomState>();
    private bool _lastMateDown;
    private int _lastReviveTotal;

    public NCHud(RectTransform canvas)
    {
        _canvas = canvas;
        RectTransform root = NCUiKit.NewRect("Hud", canvas);
        NCUiKit.Stretch(root);
        _root = root.gameObject;

        // 다운 비네트(가장 아래)
        _downVignette = NCUiKit.NewImage("DownVignette", root, null, new Color(0.6f, 0.05f, 0.1f, 0.22f));
        NCUiKit.Stretch(_downVignette.rectTransform);

        // ── 상단 중앙: 시간 ──
        RectTransform timePanel = NCUiKit.NewPanel("Time", root);
        NCUiKit.Place(timePanel, new Vector2(0.5f, 1f), new Vector2(0f, -NCStyle.Margin + 8f), new Vector2(260f, 92f));
        timePanel.Find("Frame").GetComponent<Image>().color = NCStyle.Brass;
        _timeLabel = NCUiKit.NewText("Label", timePanel, "남은 시간", 20f, NCStyle.TextDim);
        NCUiKit.Place(_timeLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(240f, 26f));
        _timeText = NCUiKit.NewText("Value", timePanel, "10:00", NCStyle.FontTimer + 4f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(_timeText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(240f, 56f));

        RectTransform progress = NCUiKit.NewPanel("Progress", root, deep: true);
        NCUiKit.Place(progress, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(220f, 38f));
        _progressText = NCUiKit.NewText("Text", progress, "소탕 완료 0/1", 22f, NCStyle.Text);
        NCUiKit.Stretch(_progressText.rectTransform);

        // 방 코드(입구에서만, 오른쪽 위)
        RectTransform code = NCUiKit.NewPanel("RoomCode", root, deep: true);
        NCUiKit.Place(code, new Vector2(1f, 1f), new Vector2(-NCStyle.Margin, -NCStyle.Margin), new Vector2(260f, 52f));
        _roomCodeText = NCUiKit.NewText("Text", code, "방 코드", 22f, NCStyle.Text);
        NCUiKit.Stretch(_roomCodeText.rectTransform);
        _roomCode = code.gameObject;

        // ── 입구 안내 ──
        RectTransform guide = NCUiKit.NewPanel("EntryGuide", root);
        NCUiKit.Place(guide, new Vector2(0.5f, 1f), new Vector2(0f, -184f), new Vector2(760f, 132f));
        TextMeshProUGUI guideTitle = NCUiKit.NewText("Title", guide, "◆  의뢰 안내  ◆", NCStyle.FontGuide, NCStyle.Brass, style: FontStyles.Bold);
        NCUiKit.Place(guideTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(700f, 36f));
        TextMeshProUGUI guideBody = NCUiKit.NewText("Body", guide,
            "전투방의 유령을 모두 소탕하세요. 잡동사니를 함께 치우면 유령이 나타납니다.", 22f, NCStyle.Text);
        NCUiKit.Place(guideBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(720f, 30f));
        _entryStatus = NCUiKit.NewText("Status", guide, "", 24f, NCStyle.P1, style: FontStyles.Bold);
        NCUiKit.Place(_entryStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(720f, 32f));
        _entryGuide = guide.gameObject;

        _controls = BuildControls(root);

        // ── 알림(MSG) ──
        RectTransform message = NCUiKit.NewPanel("Message", root, deep: true);
        NCUiKit.Place(message, new Vector2(0.5f, 1f), new Vector2(0f, -184f), new Vector2(480f, 60f));
        _messageAccent = message.Find("Frame").GetComponent<Image>();
        _messageText = NCUiKit.NewText("Text", message, "", NCStyle.FontGuide, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Stretch(_messageText.rectTransform);
        _message = message.gameObject.AddComponent<CanvasGroup>();
        _message.alpha = 0f;

        // ── 체력(왼쪽 하단) ──
        _mine = new HealthPanel(root, "MyHealth", new Vector2(NCStyle.Margin, NCStyle.Margin), new Vector2(460f, 104f), big: true);
        _mate = new HealthPanel(root, "MateHealth", new Vector2(NCStyle.Margin, NCStyle.Margin + 104f + NCStyle.Gap), new Vector2(380f, 74f), big: false);

        // ── 탄약(오른쪽 하단) ──
        RectTransform ammo = NCUiKit.NewPanel("Ammo", root);
        NCUiKit.Place(ammo, new Vector2(1f, 0f), new Vector2(-NCStyle.Margin, NCStyle.Margin), new Vector2(300f, 104f));
        TextMeshProUGUI cloud = NCUiKit.NewText("Icon", ammo, "●", 34f, NCStyle.Text);
        NCUiKit.Place(cloud.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(40f, 44f));
        _ammoText = NCUiKit.NewText("Value", ammo, "12 / 12", NCStyle.FontTimer, NCStyle.Text, TextAlignmentOptions.Left, FontStyles.Bold);
        NCUiKit.Place(_ammoText.rectTransform, new Vector2(0f, 1f), new Vector2(64f, -8f), new Vector2(220f, 54f));
        RectTransform reloadKey = NCUiKit.NewKeyCap("ReloadKey", ammo, "R", 28f);
        NCUiKit.Place(reloadKey, new Vector2(0f, 0f), new Vector2(18f, 16f), reloadKey.sizeDelta);
        _ammoHint = NCUiKit.NewText("Hint", ammo, "재장전", 20f, NCStyle.TextDim, TextAlignmentOptions.Left);
        NCUiKit.Place(_ammoHint.rectTransform, new Vector2(0f, 0f), new Vector2(58f, 16f), new Vector2(220f, 28f));
        _reloadFill = NCUiKit.NewBar("ReloadBar", ammo, NCStyle.Text, out _reloadBack);
        NCUiKit.Place(_reloadBack, new Vector2(0.5f, 0f), new Vector2(0f, -14f), new Vector2(280f, 12f));

        // ── 상호작용 안내 ──
        _prompt = NCUiKit.NewPanel("Prompt", root, deep: true);
        _prompt.anchorMin = _prompt.anchorMax = new Vector2(0.5f, 0.5f);
        _prompt.pivot = new Vector2(0.5f, 0f);
        _prompt.sizeDelta = new Vector2(340f, 96f);
        _promptKey = NCUiKit.NewKeyCap("Key", _prompt, "E", 30f);
        NCUiKit.Place(_promptKey, new Vector2(0f, 1f), new Vector2(14f, -12f), _promptKey.sizeDelta);
        _promptText = NCUiKit.NewText("Text", _prompt, "", 22f, NCStyle.Text, TextAlignmentOptions.Left);
        NCUiKit.Place(_promptText.rectTransform, new Vector2(0f, 1f), new Vector2(56f, -12f), new Vector2(270f, 30f));
        _promptChips = NCUiKit.NewRect("Chips", _prompt).gameObject;
        RectTransform chips = (RectTransform)_promptChips.transform;
        NCUiKit.Place(chips, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(200f, 26f));
        _chip1 = BuildChip(chips, 0, new Vector2(-50f, 0f));
        _chip2 = BuildChip(chips, 1, new Vector2(50f, 0f));
        _promptFill = NCUiKit.NewBar("Bar", _prompt, NCStyle.P1, out _promptBar);
        NCUiKit.Place(_promptBar, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(310f, 14f));

        // ── 다운 안내(가운데 아래) ──
        RectTransform down = NCUiKit.NewPanel("DownPanel", root, deep: true);
        NCUiKit.Place(down, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(560f, 92f));
        down.Find("Frame").GetComponent<Image>().color = NCStyle.Danger;
        _downText = NCUiKit.NewText("Text", down, "다운 · 동료의 구조를 기다리세요", NCStyle.FontGuide, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(_downText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(540f, 36f));
        _downFill = NCUiKit.NewBar("Bar", down, NCStyle.Success, out RectTransform downBar);
        NCUiKit.Place(downBar, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(500f, 16f));
        _downPanel = down.gameObject;
    }

    private static Image BuildChip(RectTransform parent, int slot, Vector2 position)
    {
        RectTransform tag = NCUiKit.NewSlotTag("Chip" + slot, parent, slot, 26f);
        NCUiKit.Place(tag, new Vector2(0.5f, 0.5f), position, tag.sizeDelta);
        TextMeshProUGUI e = NCUiKit.NewText("E", tag, "E", 16f, NCStyle.PanelDeep, style: FontStyles.Bold);
        NCUiKit.Place(e.rectTransform, new Vector2(1f, 0.5f), new Vector2(18f, 0f), new Vector2(20f, 26f));
        return tag.GetComponent<Image>();
    }

    private static GameObject BuildControls(RectTransform root)
    {
        RectTransform strip = NCUiKit.NewPanel("Controls", root, deep: true);
        NCUiKit.Place(strip, new Vector2(0.5f, 0f), new Vector2(0f, NCStyle.Margin), new Vector2(900f, 60f));

        HorizontalLayoutGroup layout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 8f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.padding = new RectOffset(16, 16, 0, 0);

        void Key(string key) => NCUiKit.NewKeyCap("Key", strip, key, 30f);
        void Label(string text)
        {
            TextMeshProUGUI label = NCUiKit.NewText("Label", strip, text, 20f, NCStyle.Text);
            label.rectTransform.sizeDelta = new Vector2(label.preferredWidth + 10f, 30f);
        }

        Key("W"); Key("A"); Key("S"); Key("D"); Label("이동");
        Label("·  마우스 조준  ·  좌클릭 사격  ·");
        Key("R"); Label("재장전");
        Key("E"); Label("상호작용");
        return strip.gameObject;
    }

    public void SetVisible(bool visible)
    {
        if (_root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    // ───────── 매 프레임 ─────────

    public void Tick(NCSession session, NCLauncher launcher)
    {
        bool sessionReady = session != null && session.Object != null && session.Object.IsValid;
        NCPlayer me = NCPlayer.Local;
        bool playing = sessionReady && session.Phase == NCPhase.Playing;
        bool entrance = sessionReady && session.Phase == NCPhase.Entrance;

        TickTop(session, sessionReady, launcher, entrance);
        TickEntrance(session, entrance);
        TickHealth(me);
        TickAmmo(me);
        TickPrompt(me, playing);
        TickDown(me);
        DetectEvents(session, me, playing);
        TickMessage();
    }

    private void TickTop(NCSession session, bool ready, NCLauncher launcher, bool entrance)
    {
        float remaining = ready ? session.RemainingSeconds : NCBalance.Get.missionSeconds;
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        if (seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            _timeText.text = NCMath.FormatClock(seconds);
        }

        int entranceKey = entrance ? 1 : 0;
        if (entranceKey != _shownEntrance)
        {
            _shownEntrance = entranceKey;
            _timeLabel.text = entrance ? "남은 시간 · 시작 전" : "남은 시간";
        }

        // 60초 이하는 붉게 깜빡여 강조
        bool urgent = ready && session.Phase == NCPhase.Playing && remaining <= 60f;
        _timeText.color = urgent ? Color.Lerp(NCStyle.Danger, NCStyle.Text, Mathf.PingPong(Time.time * 2f, 1f)) : NCStyle.Text;

        int total = ready ? Mathf.Max(1, session.TotalRooms) : NCCombatRoom.All.Count;
        int cleared = ready ? CountCleared() : 0;
        if (cleared != _shownCleared || total != _shownTotal)
        {
            _shownCleared = cleared;
            _shownTotal = total;
            _progressText.text = $"소탕 완료 {cleared}/{total}";
        }

        _roomCode.SetActive(entrance && launcher != null);
        if (launcher != null && launcher.RoomCode != _shownCode)
        {
            _shownCode = launcher.RoomCode;
            _roomCodeText.text = $"방 코드  <b><color=#F2C34E>{launcher.RoomCode}</color></b>";
        }
    }

    private static int CountCleared()
    {
        // 방 상태가 바뀐 즉시 반영되도록 방을 직접 센다(세션 값은 다음 Tick에 갱신).
        int cleared = 0;
        foreach (NCCombatRoom room in NCCombatRoom.All)
        {
            if (room.Object != null && room.Object.IsValid && room.State == NCRoomState.Cleared)
            {
                cleared++;
            }
        }

        return cleared;
    }

    private void TickEntrance(NCSession session, bool entrance)
    {
        _entryGuide.SetActive(entrance);
        _controls.SetActive(entrance);

        if (entrance == false)
        {
            return;
        }

        int required = NCBalance.Get.RequiredPlayers;
        int key = session.PlayerCount * 100 + session.PlayersInZone * 10 + required;
        if (key == _shownEntryKey)
        {
            return;
        }

        _shownEntryKey = key;
        _entryStatus.text = session.PlayerCount < required
            ? $"동료의 접속을 기다리는 중...  ({session.PlayerCount}/{required})"
            : $"동료와 중앙홀의 빛나는 원으로 이동하세요  ({session.PlayersInZone}/{session.PlayerCount})";
    }

    private void TickHealth(NCPlayer me)
    {
        _mine.Show(me, "나");

        NCPlayer mate = null;
        foreach (NCPlayer player in NCPlayer.All)
        {
            if (player != me)
            {
                mate = player;
            }
        }

        _mate.Show(mate, "동료");
    }

    private void TickAmmo(NCPlayer me)
    {
        if (me == null || me.Gun == null || me.Object == null || me.Object.IsValid == false)
        {
            return;
        }

        NCGun gun = me.Gun;
        bool reloading = gun.IsReloading;

        if (gun.Ammo != _shownAmmo)
        {
            _shownAmmo = gun.Ammo;
            _ammoText.text = $"{gun.Ammo} <size=60%><color=#F4F1E7AA>/ {gun.MagazineSize}</color></size>";
            _ammoText.color = gun.Ammo == 0 ? NCStyle.Danger : NCStyle.Text;
        }

        int state = reloading ? 1 : (gun.Ammo == 0 ? 2 : 0);
        if (state != _shownAmmoState)
        {
            _shownAmmoState = state;
            _ammoHint.text = state == 1 ? "재장전 중..." : (state == 2 ? "재장전 필요!" : "재장전");
        }

        _ammoHint.color = state == 1
            ? NCStyle.Text
            : (state == 2 ? Color.Lerp(NCStyle.Danger, NCStyle.Text, Mathf.PingPong(Time.time * 3f, 1f)) : NCStyle.TextDim);

        _reloadBack.gameObject.SetActive(reloading);
        NCUiKit.SetBar(_reloadFill, gun.ReloadRatio);
    }

    // ───────── E 안내: 실행 가능한 대상 하나만(구조 → 정리) ─────────

    private void TickPrompt(NCPlayer me, bool playing)
    {
        bool shown = false;

        if (me != null && me.Object != null && me.Object.IsValid && me.Health.IsDown == false)
        {
            shown = TryRevivePrompt(me) || TryDummyPrompt(me);
        }

        _prompt.gameObject.SetActive(shown);
    }

    private bool TryRevivePrompt(NCPlayer me)
    {
        float range = NCBalance.Get.reviveRange;

        foreach (NCPlayer other in NCPlayer.All)
        {
            if (other == me || other.Health.IsDown == false)
            {
                continue;
            }

            if (NCMath.FlatDistance(other.transform.position, me.transform.position) > range + 0.3f)
            {
                continue;
            }

            bool reviving = other.Health.Rescuer == me.Object.InputAuthority && other.Health.ReviveProgress > 0f;
            ShowPrompt(other.transform.position + Vector3.up * 2.8f,
                reviving ? "구조 중... 움직이지 마세요" : "길게 눌러 동료 구조",
                reviving ? other.Health.ReviveRatio : 0f, NCStyle.Success, chips: false, mask: 0);
            return true;
        }

        return false;
    }

    private bool TryDummyPrompt(NCPlayer me)
    {
        float range = NCBalance.Get.dummyRange;

        foreach (NCCombatRoom room in NCCombatRoom.All)
        {
            if (room.Object == null || room.Object.IsValid == false)
            {
                continue;
            }

            bool hasDummy = room.State == NCRoomState.Untouched || room.State == NCRoomState.Clearing;
            if (hasDummy == false || NCMath.FlatDistance(room.DummyPosition, me.transform.position) > range + 0.5f)
            {
                continue;
            }

            string text;
            bool mineHeld = (room.HoldingMask & (1 << me.Slot)) != 0;

            switch (room.Block)
            {
                case NCRoomBlock.NotStarted:
                    text = "중앙홀에서 의뢰를 먼저 시작하세요";
                    break;
                case NCRoomBlock.OtherRoom:
                    text = "다른 방의 전투가 끝나야 합니다";
                    break;
                case NCRoomBlock.DoorOccupied:
                    text = "출입구에서 떨어져 주세요";
                    break;
                default:
                    if (room.ClearProgress > 0f)
                    {
                        text = "정리 중...";
                    }
                    else if (mineHeld)
                    {
                        text = "동료와 함께 E를 누르세요";
                    }
                    else
                    {
                        text = "길게 눌러 함께 치우기";
                    }
                    break;
            }

            bool solo = NCBalance.Get.RequiredPlayers == 1;
            ShowPrompt(room.DummyPosition + Vector3.up * 1.8f, text, room.ClearRatio, NCStyle.P1, chips: solo == false, mask: room.HoldingMask);
            return true;
        }

        return false;
    }

    private void ShowPrompt(Vector3 world, string text, float ratio, Color barColor, bool chips, int mask)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z < 0f)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out Vector2 local);

        // 안전 영역 안으로 제한
        Vector2 half = _canvas.rect.size * 0.5f;
        local.x = Mathf.Clamp(local.x, -half.x + 200f, half.x - 200f);
        local.y = Mathf.Clamp(local.y, -half.y + 160f, half.y - 260f);
        _prompt.anchoredPosition = local;

        _promptText.text = text;
        _promptChips.SetActive(chips);
        _chip1.color = (mask & 1) != 0 ? NCStyle.P1 : NCStyle.Hex("58B8EA", 0.25f);
        _chip2.color = (mask & 2) != 0 ? NCStyle.P2 : NCStyle.Hex("F2C34E", 0.25f);
        _promptFill.color = barColor;
        NCUiKit.SetBar(_promptFill, ratio);
    }

    private void TickDown(NCPlayer me)
    {
        bool down = me != null && me.Object != null && me.Object.IsValid && me.Health.IsDown;
        _downPanel.SetActive(down);
        _downVignette.enabled = down;

        if (down)
        {
            float ratio = me.Health.ReviveRatio;
            _downText.text = ratio > 0f ? "동료가 구조하는 중..." : "다운 · 동료의 구조를 기다리세요";
            NCUiKit.SetBar(_downFill, ratio);
            _downVignette.color = new Color(0.6f, 0.05f, 0.1f, 0.18f + 0.06f * Mathf.Sin(Time.time * 3f));
        }
    }

    // ───────── 알림(MSG) ─────────

    private void DetectEvents(NCSession session, NCPlayer me, bool playing)
    {
        // 방마다 이전 상태를 기억해, 어느 방이든 상태가 바뀌는 순간 알린다.
        foreach (NCCombatRoom room in NCCombatRoom.All)
        {
            if (room.Object == null || room.Object.IsValid == false)
            {
                continue;
            }

            NCRoomState state = room.State;
            if (_lastRoomStates.TryGetValue(room, out NCRoomState last) == false)
            {
                _lastRoomStates[room] = state;
                continue;
            }

            if (state == last)
            {
                continue;
            }

            if (state == NCRoomState.Warning)
            {
                // 차수는 예고 1.5초 동안만 알린다.
                ShowMessage($"유령 출현 · {room.WaveIndex + 1}차", NCBalance.Get.spawnWarningSeconds, 2, NCStyle.GhostTelegraph);
            }
            else if (state == NCRoomState.Cleared && playing)
            {
                int total = Mathf.Max(1, NCCombatRoom.All.Count);
                ShowMessage($"◆ {room.RoomName} 소탕 완료 ({CountCleared()}/{total}) ◆", NCBalance.Get.messageSeconds, 1, NCStyle.Success);
            }

            _lastRoomStates[room] = state;
        }

        NCPlayer mate = null;
        int reviveTotal = 0;
        foreach (NCPlayer player in NCPlayer.All)
        {
            reviveTotal += player.Health.ReviveCount;
            if (player != me)
            {
                mate = player;
            }
        }

        bool mateDown = mate != null && mate.Health.IsDown;
        if (mateDown && _lastMateDown == false)
        {
            ShowMessage("동료가 다운되었습니다 · 구조하세요", NCBalance.Get.messageSeconds, 3, NCStyle.Danger);
        }

        if (reviveTotal > _lastReviveTotal)
        {
            ShowMessage("구조 성공!", NCBalance.Get.messageSeconds, 3, NCStyle.Success);
        }

        _lastMateDown = mateDown;
        _lastReviveTotal = reviveTotal;
    }

    /// <summary>우선순위가 같거나 높을 때만 바꾼다. 늦게 큐에서 재생하지 않는다.</summary>
    private void ShowMessage(string text, float seconds, int priority, Color accent)
    {
        bool active = Time.time < _messageUntil;
        if (active && priority < _messagePriority)
        {
            return;
        }

        _messageText.text = text;
        _messageAccent.color = accent;
        _messageUntil = Time.time + seconds;
        _messagePriority = priority;
    }

    private void TickMessage()
    {
        float target = Time.time < _messageUntil ? 1f : 0f;
        _message.alpha = Mathf.MoveTowards(_message.alpha, target, Time.deltaTime * 6f);

        // 입구 안내와 같은 자리를 쓰므로 안내가 있으면 숨긴다.
        _message.gameObject.SetActive(_entryGuide.activeSelf == false);
    }

    // ───────── 체력 패널 ─────────

    private class HealthPanel
    {
        private readonly GameObject _root;
        private readonly Image _tag;
        private readonly TextMeshProUGUI _tagText;
        private readonly TextMeshProUGUI _name;
        private readonly TextMeshProUGUI _value;
        private readonly Image _fill;
        private readonly GameObject _downBadge;
        private int _shownHp = -1;
        private int _shownMax = -1;
        private int _shownSlot = -1;
        private string _shownLabel;

        public HealthPanel(RectTransform parent, string name, Vector2 position, Vector2 size, bool big)
        {
            RectTransform panel = NCUiKit.NewPanel(name, parent);
            NCUiKit.Place(panel, new Vector2(0f, 0f), position, size);
            _root = panel.gameObject;

            float tagHeight = big ? 36f : 28f;
            RectTransform tag = NCUiKit.NewSlotTag("Tag", panel, 0, tagHeight);
            NCUiKit.Place(tag, new Vector2(0f, 1f), new Vector2(16f, -12f), tag.sizeDelta);
            _tag = tag.GetComponent<Image>();
            _tagText = tag.Find("Label").GetComponent<TextMeshProUGUI>();

            _name = NCUiKit.NewText("Name", panel, "", big ? 24f : 20f, NCStyle.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            NCUiKit.Place(_name.rectTransform, new Vector2(0f, 1f), new Vector2(16f + tagHeight * 1.6f + 10f, -12f), new Vector2(160f, tagHeight));

            _value = NCUiKit.NewText("Value", panel, "", big ? 26f : 20f, NCStyle.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            NCUiKit.Place(_value.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(160f, tagHeight));

            _fill = NCUiKit.NewBar("Bar", panel, NCStyle.P1, out RectTransform back);
            NCUiKit.Place(back, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(size.x - 32f, big ? 24f : 16f));

            RectTransform badge = NCUiKit.NewImage("Down", panel, NCUiKit.RoundSprite, NCStyle.Danger).rectTransform;
            NCUiKit.Place(badge, new Vector2(1f, 1f), new Vector2(-110f, -12f), new Vector2(64f, tagHeight));
            TextMeshProUGUI badgeText = NCUiKit.NewText("Text", badge, "다운", 18f, NCStyle.Text, style: FontStyles.Bold);
            NCUiKit.Stretch(badgeText.rectTransform);
            _downBadge = badge.gameObject;
        }

        public void Show(NCPlayer player, string label)
        {
            bool valid = player != null && player.Object != null && player.Object.IsValid;
            _root.SetActive(valid);

            if (valid == false)
            {
                return;
            }

            NCHealth health = player.Health;
            Color slotColor = player.SlotColor;

            if (player.Slot != _shownSlot)
            {
                _shownSlot = player.Slot;
                _tag.color = slotColor;
                _tagText.text = player.SlotLabel;
            }

            if (label != _shownLabel)
            {
                _shownLabel = label;
                _name.text = label;
            }

            if (health.Hp != _shownHp || health.MaxHp != _shownMax)
            {
                _shownHp = health.Hp;
                _shownMax = health.MaxHp;
                _value.text = $"{health.Hp}<size=70%><color=#F4F1E7AA>/{health.MaxHp}</color></size>";
            }

            _fill.color = health.Ratio <= 0.3f ? NCStyle.Danger : slotColor;
            NCUiKit.SetBar(_fill, health.Ratio);
            _downBadge.SetActive(health.IsDown);
        }
    }
}
