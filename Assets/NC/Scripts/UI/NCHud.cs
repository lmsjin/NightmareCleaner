using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD. 확정된 값을 읽어 그리기만 한다.
///  - 상단 중앙: 남은 시간 · 점령 구역 상태, 양옆에 1P·2P 지배율
///  - 머리 위 체력·탄창·대쉬(NCOverhead), 유령 체력(NCGhostBar), 오른쪽 하단 과충전(아트 프리팹 UI_Entry)
///  - 카운트다운 큰 숫자, 부활 대기, 보호 표시, 짧은 알림
/// </summary>
public class NCHud
{
    private readonly GameObject _root;
    private readonly NCEntryView _view; // 못 불러오면 null → Tick을 건너뛴다

    private readonly TextMeshProUGUI _timeText;
    private readonly TextMeshProUGUI _zoneText;
    private readonly DominancePanel _dominanceA;
    private readonly DominancePanel _dominanceB;

    private readonly NCOverhead[] _overheads;
    private readonly List<NCGhostBar> _ghostBars = new List<NCGhostBar>();
    private readonly RectTransform _rootRect;

    private readonly Image _chargeIcon;
    private readonly TextMeshProUGUI _chargeText;
    private readonly Image _chargeFill;
    private readonly RectTransform _chargeBar;

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

        RectTransform entryRect = (RectTransform)_view.transform;
        NCUiKit.Stretch(entryRect);
        entryRect.localScale = Vector3.one;
        entryRect.anchoredPosition3D = Vector3.zero;

        AddDashGuide((RectTransform)_view.Controls.transform);

        // 상단
        _timeText = _view.TimeText;
        _view.TimeLabel.text = "남은 시간";
        _zoneText = _view.ProgressText;
        _dominanceA = new DominancePanel(root, 0);
        _dominanceB = new DominancePanel(root, 1);

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
    }

    /// <summary>카운트다운 조작 안내의 빈 칸(아랫줄 왼쪽)에 대쉬를 넣는다.</summary>
    private static void AddDashGuide(RectTransform controls)
    {
        RectTransform key = NCUiKit.NewKeyCap("KeyDash", controls, "Space", 34f);
        NCUiKit.Place(key, new Vector2(0.5f, 0.5f), new Vector2(-225f, -39f), key.sizeDelta);

        TextMeshProUGUI text = NCUiKit.NewText("DashText", controls, "대쉬", NCStyle.FontGuide, NCStyle.Text, TextAlignmentOptions.Left, FontStyles.Bold);
        NCUiKit.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-110f, -36f), new Vector2(90f, 34f));
    }

    public void SetVisible(bool visible)
    {
        if (_root.activeSelf != visible)
        {
            _root.SetActive(visible);
            _hitFeedback?.Clear();
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

        if (meReady)
        {
            TickCharge(me.Gun);

            if (down)
            {
                float remaining = me.Health.RespawnRemaining;
                int seconds = Mathf.CeilToInt(remaining);
                if (seconds != _shownRespawn)
                {
                    _shownRespawn = seconds;
                    _respawnText.text = $"{seconds}초 후 부활";
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
        _view.Controls.SetActive(countdown);
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

    private class DominancePanel
    {
        private readonly TextMeshProUGUI _text;
        private readonly Image _fill;
        private readonly string _name;
        private int _shown = -1;

        public DominancePanel(RectTransform root, int slot)
        {
            _name = slot == 0 ? "1P" : "2P";
            RectTransform panel = NCUiKit.NewPanel("Dominance" + _name, root, deep: true);
            NCUiKit.Place(panel, new Vector2(0.5f, 1f), new Vector2(slot == 0 ? -264f : 264f, -32f), new Vector2(186f, 76f));
            panel.Find("Frame").GetComponent<Image>().color = NCStyle.SlotColor(slot);

            _text = NCUiKit.NewText("Text", panel, "", 30f, NCStyle.SlotColor(slot), style: FontStyles.Bold);
            NCUiKit.Place(_text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(176f, 38f));
            _fill = NCUiKit.NewBar("Bar", panel, NCStyle.SlotColor(slot), out RectTransform bar);
            NCUiKit.Place(bar, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(162f, 18f));
        }

        public void Show(int percent, float ratio)
        {
            if (percent != _shown)
            {
                _shown = percent;
                _text.text = $"{_name}  {percent}%";
            }

            NCUiKit.SetBar(_fill, ratio);
        }
    }
}
