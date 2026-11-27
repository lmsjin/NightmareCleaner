using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD. 확정된 값을 읽어 그리기만 한다.
///  - 상단 중앙: 남은 시간 · 점령 구역 상태, 양옆에 1P·2P 지배율(아트 프리팹 바)
///  - 머리 위 체력·탄창·대쉬(NCOverhead), 유령 체력(NCGhostBar), 오른쪽 하단 과충전(아트 프리팹 UI_Entry)
///  - 카운트다운 큰 숫자, 부활 대기, 보호 표시, 짧은 알림
/// </summary>
public class NCHud
{
    private readonly GameObject _root;
    private readonly NCEntryView _view; // 못 불러오면 null → Tick을 건너뛴다

    private readonly TextMeshProUGUI _timeText;
    private readonly TextMeshProUGUI _zoneText;
    private readonly DominanceBar _dominanceA;
    private readonly DominanceBar _dominanceB;

    private readonly NCOverhead[] _overheads;
    private readonly List<NCGhostBar> _ghostBars = new List<NCGhostBar>();
    private readonly RectTransform _rootRect;

    private readonly Image _chargeIcon;
    private readonly TextMeshProUGUI _chargeText;
    private readonly Image _chargeFill;
    private readonly RectTransform _chargeBar;

    // 마우스 커서 자리의 빨간 조준점(경기 중·살아 있음·메뉴 닫힘일 때만, 그동안 OS 커서는 숨긴다).
    // 갈색 바닥·붉은 카펫에 묻히지 않도록 검은 테두리 위에 선명한 빨강을 겹쳐 그린다.
    private const float CrosshairSize = 38f;     // 빨간 원 지름
    private const int CrosshairBorder = 8;       // 빨간 원 두께(64px 기준)
    private const float CrosshairOutline = 42f;  // 검은 테두리 지름
    private const int CrosshairOutlineBorder = 14;
    private const float CrosshairDot = 6f;
    private const float CrosshairDotOutline = 10f;
    private static readonly Color CrosshairRed = new Color(1f, 0.176f, 0.176f, 1f); // #FF2D2D
    private static readonly Color CrosshairShadow = new Color(0f, 0f, 0f, 0.8f);
    private readonly RectTransform _crosshair;

    private readonly TextMeshProUGUI _controlsHint; // 왼쪽 아래 "H 조작키"
    private bool _showControls;                     // H로 켜고 끈다(경기 중)

    private readonly TextMeshProUGUI _countdown;
    private readonly GameObject _respawnPanel;
    private readonly TextMeshProUGUI _respawnText;
    private readonly Image _respawnFill;
    private readonly Image _deathVignette;
    private readonly GameObject _protectPanel;

    private readonly CanvasGroup _message;
    private readonly TextMeshProUGUI _messageText;
    private readonly Image _messageAccent;
    private float _messageUntil;

    private readonly NCMinimap _minimap;
    private readonly NCHitFeedback _hitFeedback;

    // 값이 바뀔 때만 글자를 새로 만든다(매 프레임 문자열 생성 방지).
    private int _shownSeconds = -1;
    private int _shownZone = -1;
    private int _shownChargeState = -1;
    private int _shownChargeTenths = -1;
    private int _shownCountdown = -1;
    private int _shownRespawn = -1;

    private readonly Dictionary<NCGhostCamp, int> _seenRewards = new Dictionary<NCGhostCamp, int>();
    private readonly Dictionary<NCGhostCamp, bool> _seenActive = new Dictionary<NCGhostCamp, bool>();
    private bool _wasPlaying;

    public NCHud(RectTransform canvas)
    {
        RectTransform root = NCUiKit.NewRect("Hud", canvas);
        NCUiKit.Stretch(root);
        _root = root.gameObject;
        _rootRect = root;

        _deathVignette = NCUiKit.NewImage("DeathVignette", root, null, new Color(0.6f, 0.05f, 0.1f, 0.2f));
        NCUiKit.Stretch(_deathVignette.rectTransform);

        GameObject prefab = Resources.Load<GameObject>("UI/Scenes/UI_Entry");
        _view = prefab != null ? Object.Instantiate(prefab, root, false).GetComponent<NCEntryView>() : null;
        if (_view == null)
        {
            Debug.LogError("[UI] UI_Entry 프리팹 또는 루트의 NCEntryView를 찾을 수 없음 — 경로·Variant 확인");
            return;
        }

        _view.FillMissing();

        RectTransform entryRect = (RectTransform)_view.transform;
        NCUiKit.Stretch(entryRect);
        entryRect.localScale = Vector3.one;
        entryRect.anchoredPosition3D = Vector3.zero;

        AddDashGuide((RectTransform)_view.Controls.transform);

        _controlsHint = NCUiKit.NewText("ControlsHint", root, "H  조작키", 20f, NCStyle.TextDim, TextAlignmentOptions.Left);
        NCUiKit.Place(_controlsHint.rectTransform, new Vector2(0f, 0f), new Vector2(NCStyle.Margin + 80f, NCStyle.Margin + 14f), new Vector2(160f, 28f));

        // 아직 쓰지 않는 아트: 탄창은 머리 위에 있고, 효과 칸은 보상이 생기면 쓴다.
        HideUnused(_view.AmmoPanel);
        HideUnused(_view.EffectPanel);

        // 상단: 시간 · 점령 상태(시간 칸 바로 아래, 프리팹에 없어 코드로 만든다) · 1P·2P 지배율(프리팹 바)
        _timeText = _view.TimeText;
        _view.TimeLabel.text = "남은 시간";
        _zoneText = NCUiKit.NewText("ZoneText", _timeText.transform.parent, "", NCStyle.FontGuide, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(_zoneText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(320f, 36f));
        _dominanceA = new DominanceBar(_view, 0);
        _dominanceB = new DominanceBar(_view, 1);

        // 알림
        RectTransform message = NCUiKit.NewPanel("Message", root, deep: true);
        NCUiKit.Place(message, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(520f, 60f));
        _messageAccent = message.Find("Frame").GetComponent<Image>();
        _messageText = NCUiKit.NewText("Text", message, "", NCStyle.FontGuide, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Stretch(_messageText.rectTransform);
        _message = message.gameObject.AddComponent<CanvasGroup>();
        _message.alpha = 0f;

        // 머리 위 체력·탄창은 다른 HUD 뒤에 그린다.
        _overheads = new[] { new NCOverhead(root, 0), new NCOverhead(root, 1) };
        root.Find("Overhead1").SetAsFirstSibling();
        root.Find("Overhead0").SetAsFirstSibling();

        // 과충전 (프리팹)
        _chargeIcon = _view.ChargeIcon;
        _chargeText = _view.ChargeText;
        _chargeFill = NCUiKit.NewBar("Bar", _view.ChargePanel, NCStyle.ShotOvercharge, out _chargeBar);
        NCUiKit.Place(_chargeBar, new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(160f, 12f));

        _minimap = new NCMinimap(root);
        _hitFeedback = new NCHitFeedback(root, ShowMessage);

        // 카운트다운
        _countdown = NCUiKit.NewText("Countdown", root, "", 180f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(_countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(400f, 220f));

        // 부활 대기
        RectTransform respawn = NCUiKit.NewPanel("Respawn", root, deep: true);
        NCUiKit.Place(respawn, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(460f, 92f));
        respawn.Find("Frame").GetComponent<Image>().color = NCStyle.Danger;
        _respawnText = NCUiKit.NewText("Text", respawn, "", NCStyle.FontGuide, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(_respawnText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(440f, 36f));
        _respawnFill = NCUiKit.NewBar("Bar", respawn, NCStyle.Success, out RectTransform respawnBar);
        NCUiKit.Place(respawnBar, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(400f, 16f));
        _respawnPanel = respawn.gameObject;

        // 보호
        RectTransform protect = NCUiKit.NewPanel("Protect", root, deep: true);
        NCUiKit.Place(protect, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(420f, 48f));
        TextMeshProUGUI protectText = NCUiKit.NewText("Text", protect, "부활 보호 중 · 사격하면 해제", 22f, NCStyle.Text);
        NCUiKit.Stretch(protectText.rectTransform);
        _protectPanel = protect.gameObject;

        // 조준점: 다른 HUD보다 위에 그리도록 마지막에 만든다. 아래부터 검은 테두리 → 빨간 원 → 검은 점 → 빨간 점.
        _crosshair = NCUiKit.NewRect("Crosshair", root);
        NCUiKit.Place(_crosshair, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * CrosshairOutline);
        AddCrosshairPart("Outline", NCUiKit.Ring(CrosshairOutlineBorder), CrosshairShadow, CrosshairOutline);
        AddCrosshairPart("Ring", NCUiKit.Ring(CrosshairBorder), CrosshairRed, CrosshairSize);
        AddCrosshairPart("DotOutline", NCUiKit.CircleSprite, CrosshairShadow, CrosshairDotOutline);
        AddCrosshairPart("Dot", NCUiKit.CircleSprite, CrosshairRed, CrosshairDot);
        _crosshair.gameObject.SetActive(false);
    }

    private void AddCrosshairPart(string name, Sprite sprite, Color color, float size)
    {
        Image image = NCUiKit.NewImage(name, _crosshair, sprite, color);
        image.type = Image.Type.Simple;
        NCUiKit.Place(image.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * size);
    }

    /// <summary>마우스 커서 자리에 조준점을 옮기고, 보이는 동안 OS 커서를 숨긴다.</summary>
    private void TickCrosshair()
    {
        NCPlayer me = NCPlayer.Local;
        NCSession session = NCSession.Instance;
        Mouse mouse = Mouse.current;

        bool show = mouse != null
                    && me != null && me.Object != null && me.Object.IsValid && me.Health.IsDown == false
                    && session != null && session.Object != null && session.Object.IsValid && session.IsPlaying
                    && NCUi.InputBlocked == false;

        _crosshair.gameObject.SetActive(show);
        Cursor.visible = show == false;

        if (show && RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, mouse.position.ReadValue(), null, out Vector2 local))
        {
            // 조준점은 가운데 앵커(0.5, 0.5)라 HUD 중심 기준 좌표를 그대로 쓴다.
            _crosshair.anchoredPosition = local;
        }
    }

    /// <summary>
    /// 조작 안내 아랫줄 왼쪽에 대쉬를 넣는다. 아트 프리팹(UI_Entry_new)의 아랫줄은 재장전·과충전이 왼쪽부터 차 있어
    /// 두 묶음을 오른쪽으로 밀어 자리를 만든다(밀지 않으면 대쉬 키가 R 키와 겹친다).
    /// </summary>
    private static void AddDashGuide(RectTransform controls)
    {
        const float shift = 80f;
        foreach (string group in new[] { "reload", "overcharge" })
        {
            if (controls.Find(group) is RectTransform rect)
            {
                rect.anchoredPosition += new Vector2(shift, 0f);
            }
        }

        // 키캡 폭은 영문 글자 수 기준이라 한글 "우클릭"이 넘친다 → 폭을 직접 준다.
        RectTransform key = NCUiKit.NewKeyCap("KeyDash", controls, "우클릭", 34f);
        NCUiKit.Place(key, new Vector2(0.5f, 0.5f), new Vector2(-245f, -39f), new Vector2(88f, 34f));

        TextMeshProUGUI text = NCUiKit.NewText("DashText", controls, "대쉬", NCStyle.FontGuide, NCStyle.Text, TextAlignmentOptions.Left, FontStyles.Bold);
        NCUiKit.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-150f, -36f), new Vector2(70f, 34f));
    }

    private static void HideUnused(GameObject art)
    {
        if (art != null)
        {
            art.SetActive(false);
        }
    }

    public void SetVisible(bool visible)
    {
        if (_root.activeSelf != visible)
        {
            _root.SetActive(visible);
            _hitFeedback?.Clear();
        }

        if (visible == false)
        {
            Cursor.visible = true; // 조준점이 숨긴 OS 커서를 되돌린다(대기실·결과 화면)
        }
    }

    public void Tick(NCSession session, NCLauncher launcher)
    {
        if (_view == null)
        {
            return;
        }

        bool ready = session != null && session.Object != null && session.Object.IsValid;
        NCPlayer me = NCPlayer.Local;
        bool meReady = me != null && me.Object != null && me.Object.IsValid;

        TickTop(session, ready);
        _minimap.Tick(me, session);

        bool playing = ready && session.IsPlaying;
        bool down = meReady && playing && me.Health.IsDown;
        _deathVignette.enabled = down;
        _respawnPanel.SetActive(down);
        _protectPanel.SetActive(meReady && playing && me.Health.IsProtected);

        // 부활 대기·보호 패널이 조작 안내와 같은 높이라, 그동안은 조작 안내를 잠시 숨긴다(H 설정은 유지).
        if (_respawnPanel.activeSelf || _protectPanel.activeSelf)
        {
            _view.Controls.SetActive(false);
        }

        if (meReady)
        {
            TickCharge(me.Gun);

            if (down)
            {
                float remaining = me.Health.RespawnRemaining;
                int seconds = Mathf.CeilToInt(remaining);
                bool spectating = NCCameraRig.IsSpectating;
                int key = seconds * 2 + (spectating ? 1 : 0);
                if (key != _shownRespawn)
                {
                    _shownRespawn = key;
                    _respawnText.text = spectating ? $"{seconds}초 후 부활 · 상대 관전 중" : $"{seconds}초 후 부활";
                }

                NCUiKit.SetBar(_respawnFill, 1f - remaining / NCBalance.Get.respawnSeconds);
            }
        }

        DetectEvents(session, playing);
        _message.alpha = Mathf.MoveTowards(_message.alpha, Time.time < _messageUntil ? 1f : 0f, Time.deltaTime * 6f);
    }

    /// <summary>카메라가 움직인 뒤 머리 위 표시를 옮긴다(LateUpdate).</summary>
    public void LateTick()
    {
        if (_view == null || _root.activeSelf == false)
        {
            return;
        }

        TickCrosshair();

        foreach (NCOverhead overhead in _overheads)
        {
            overhead.Tick();
        }

        _hitFeedback.LateTick();

        while (_ghostBars.Count < NCGhost.All.Count)
        {
            _ghostBars.Add(new NCGhostBar(_rootRect));
        }

        for (int i = 0; i < _ghostBars.Count; i++)
        {
            _ghostBars[i].Tick(i < NCGhost.All.Count ? NCGhost.All[i] : null);
        }
    }

    private void TickTop(NCSession session, bool ready)
    {
        float remaining = ready ? session.RemainingSeconds : NCBalance.Get.matchSeconds;
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        if (seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            _timeText.text = NCMath.FormatClock(seconds);
        }

        bool urgent = ready && session.IsPlaying && remaining <= 30f;
        _timeText.color = urgent ? Color.Lerp(NCStyle.Danger, NCStyle.Text, Mathf.PingPong(Time.time * 2f, 1f)) : NCStyle.Text;

        bool countdown = ready && session.Phase == NCPhase.Countdown;
        bool playing = ready && session.IsPlaying;

        // H: 경기 중 조작 안내 켜기·끄기(메뉴가 열려 있으면 무시). 카운트다운에는 늘 보인다.
        Keyboard keyboard = Keyboard.current;
        if (playing && NCUi.InputBlocked == false && keyboard != null && keyboard.hKey.wasPressedThisFrame)
        {
            ToggleControls();
        }

        _view.Controls.SetActive(countdown || (playing && _showControls));
        _controlsHint.gameObject.SetActive(countdown || playing);
        _countdown.gameObject.SetActive(countdown);

        if (countdown)
        {
            int number = Mathf.Max(1, Mathf.CeilToInt(session.CountdownSeconds));
            if (number != _shownCountdown)
            {
                _shownCountdown = number;
                _countdown.text = number.ToString();
                NCUi.PlaySound(NCSound.Tick);
            }
        }
        else
        {
            _shownCountdown = -1;
        }

        if (ready == false)
        {
            return;
        }

        _dominanceA.Show(session.DominancePercent(0), session.Dominance[0] / NCSession.WinDominance);
        _dominanceB.Show(session.DominancePercent(1), session.Dominance[1] / NCSession.WinDominance);

        int zone = (int)session.Zone;
        if (zone != _shownZone)
        {
            bool first = _shownZone < 0;
            _shownZone = zone;
            _zoneText.text = ZoneLabel(session.Zone);
            _zoneText.color = NCSession.ZoneColor(session.Zone);

            if (first == false && session.IsPlaying)
            {
                NCUi.PlaySound(NCSound.Tick);
            }
        }
    }

    /// <summary>조작 안내 표시를 뒤집는다(H키).</summary>
    public void ToggleControls() => _showControls = _showControls == false;

    public bool ControlsShown => _view != null && _view.Controls.activeSelf;

    private static string ZoneLabel(NCZoneState zone)
    {
        switch (zone)
        {
            case NCZoneState.SlotA: return "1P 점령 중";
            case NCZoneState.SlotB: return "2P 점령 중";
            case NCZoneState.Contested: return "대치 중";
            default: return "중립";
        }
    }

    private void TickCharge(NCGun gun)
    {
        bool active = gun.IsOvercharged;
        int state = active ? 2 : (gun.Charges > 0 ? 1 : 0);

        if (state != _shownChargeState)
        {
            _shownChargeState = state;
            _shownChargeTenths = -1;
            _chargeText.text = state == 1 ? "사용 가능" : "충전 없음";
            _chargeText.color = state == 0 ? NCStyle.TextDim : NCStyle.ShotOvercharge;
            _chargeIcon.color = state == 0 ? new Color(1f, 1f, 1f, 0.3f) : (state == 1 ? Color.white : NCStyle.ShotOvercharge);
        }

        if (active)
        {
            int tenths = Mathf.CeilToInt(gun.OverchargeRemaining * 10f);
            if (tenths != _shownChargeTenths)
            {
                _shownChargeTenths = tenths;
                _chargeText.text = $"{tenths / 10f:0.0}초";
            }
        }

        _chargeBar.gameObject.SetActive(active);
        NCUiKit.SetBar(_chargeFill, gun.OverchargeRatio);
    }

    private void DetectEvents(NCSession session, bool playing)
    {
        if (playing && _wasPlaying == false)
        {
            ShowMessage("중앙을 점령하세요!", NCStyle.Brass);
        }

        foreach (NCGhostCamp camp in NCGhostCamp.All)
        {
            if (camp.Object == null || camp.Object.IsValid == false)
            {
                continue;
            }

            int rewards = camp.RewardCount;
            if (playing && _seenRewards.TryGetValue(camp, out int seen) && rewards > seen)
            {
                string who = camp.RewardSlot == 0 ? "1P" : "2P";
                bool mine = NCPlayer.Local != null && NCPlayer.Local.Slot == camp.RewardSlot;
                ShowMessage(mine ? "과충전 획득 · Q로 사용" : $"{who} 과충전 획득", NCStyle.ShotOvercharge);
                NCUi.PlaySound(NCSound.Reward);
            }

            _seenRewards[camp] = rewards;

            bool active = camp.IsActive;
            if (playing && _wasPlaying && active && _seenActive.TryGetValue(camp, out bool was) && was == false)
            {
                ShowMessage($"유령 등장 · {GhostLabel(camp.Type)}", NCStyle.GhostTelegraph);
            }

            _seenActive[camp] = active;
        }

        _wasPlaying = playing;
    }

    public static string GhostLabel(NCGhostType type)
    {
        switch (type)
        {
            case NCGhostType.Fast: return "빠른 유령";
            case NCGhostType.Tank: return "탱커 유령";
            default: return "기본 유령";
        }
    }

    private void ShowMessage(string text, Color accent)
    {
        _messageText.text = text;
        _messageAccent.color = accent;
        _messageUntil = Time.time + NCBalance.Get.messageSeconds;
    }

    // ───────── 지배율 ─────────

    /// <summary>
    /// 아트 프리팹의 지배율 바. 채움 이미지는 Sliced라 폭을 비율만큼 줄인다.
    /// 앵커·피벗은 아트가 정한 대로 두므로 바는 가운데 쪽 끝에서부터 찬다.
    /// </summary>
    private class DominanceBar
    {
        private readonly TextMeshProUGUI _text;
        private readonly RectTransform _fill;
        private readonly Image _fillImage;
        private readonly float _width;
        private int _shown = -1;

        public DominanceBar(NCEntryView view, int slot)
        {
            _text = view.DominanceText(slot);
            _fill = view.DominanceFill(slot);

            if (_fill != null)
            {
                _width = _fill.sizeDelta.x;
                _fillImage = _fill.GetComponent<Image>();
            }

            Show(0, 0f); // 프리팹은 가득 찬 상태로 그려져 있다
        }

        public void Show(int percent, float ratio)
        {
            if (_text != null && percent != _shown)
            {
                _shown = percent;
                _text.text = $"{percent}%";
            }

            if (_fill == null)
            {
                return;
            }

            ratio = Mathf.Clamp01(ratio);
            _fill.sizeDelta = new Vector2(_width * ratio, _fill.sizeDelta.y);

            if (_fillImage != null)
            {
                _fillImage.enabled = ratio > 0.001f;
            }
        }
    }
}
