using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미니맵(UI 기획서 HUD-07, 오른쪽 상단). 맵 전체를 한 번에 보여준다(위 = 북쪽, 쿼터뷰 카메라와 같은 방향).
///  - 배경: 시작할 때 찍은 맵 윤곽(NCMapSnapshot). 정식 지도 그림이 오면 배경만 교체.
///  - 표식(별도 레이어): 전투방 테두리(미소탕 / 전투 중 붉게 깜빡임 / 소탕 완료 초록 "완료"),
///    보급함(잠김 회색 / 사용 가능 노랑 깜빡임 / 사용 완료 어둡게), 플레이어(1P 파랑·2P 노랑 + 번호, 나는 크게, 다운은 붉게).
/// 색·글자는 상태가 바뀔 때만 바꾸고, 위치만 매 프레임 갱신한다.
/// </summary>
public class NCMinimap
{
    private const float MaxWidth = 380f;
    private const float MaxHeight = 230f;

    private readonly RectTransform _panel;
    private readonly RectTransform _content;
    private readonly RawImage _map;
    private bool _ready;
    private Bounds _bounds;

    private readonly List<RoomMark> _rooms = new List<RoomMark>();
    private readonly List<SupplyMark> _supplies = new List<SupplyMark>();
    private readonly Dictionary<NCPlayer, PlayerMark> _players = new Dictionary<NCPlayer, PlayerMark>();
    private readonly List<NCPlayer> _gone = new List<NCPlayer>();

    public NCMinimap(RectTransform root)
    {
        _panel = NCUiKit.NewPanel("Minimap", root, deep: true);
        NCUiKit.Place(_panel, new Vector2(1f, 1f), new Vector2(-NCStyle.Margin, -NCStyle.Margin), new Vector2(MaxWidth + 16f, MaxHeight + 16f));
        _panel.Find("Frame").GetComponent<Image>().color = NCStyle.Brass;

        _content = NCUiKit.NewRect("Content", _panel);
        NCUiKit.Place(_content, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MaxWidth, MaxHeight));

        GameObject mapObject = new GameObject("Map", typeof(RectTransform));
        mapObject.transform.SetParent(_content, false);
        _map = mapObject.AddComponent<RawImage>();
        _map.raycastTarget = false;
        _map.color = new Color(1f, 0.92f, 0.82f, 0.9f);
        NCUiKit.Stretch(_map.rectTransform);
    }

    public void SetVisible(bool visible)
    {
        if (_panel.gameObject.activeSelf != visible)
        {
            _panel.gameObject.SetActive(visible);
        }
    }

    /// <summary>맵 윤곽 사진이 준비되면 크기·방·보급함 표식을 한 번 만든다.</summary>
    private void TryInit()
    {
        if (_ready || NCMapSnapshot.IsReady == false)
        {
            return;
        }

        _ready = true;
        _bounds = NCMapSnapshot.Bounds;
        _map.texture = NCMapSnapshot.Texture;

        // 맵 비율에 맞춰 최대 크기 안에 맞춘다.
        float aspect = _bounds.size.x / Mathf.Max(0.01f, _bounds.size.z);
        Vector2 size = aspect >= MaxWidth / MaxHeight
            ? new Vector2(MaxWidth, MaxWidth / aspect)
            : new Vector2(MaxHeight * aspect, MaxHeight);

        _content.sizeDelta = size;
        _panel.sizeDelta = size + new Vector2(16f, 16f);

        foreach (NCCombatRoom room in NCCombatRoom.All)
        {
            _rooms.Add(new RoomMark(_content, room, this));
        }

        foreach (NCSupplyBox box in NCSupplyBox.All)
        {
            _supplies.Add(new SupplyMark(_content, box, this));
        }
    }

    /// <summary>월드 위치 → 미니맵 안의 위치.</summary>
    private Vector2 ToMap(Vector3 world)
    {
        Vector2 size = _content.rect.size;
        float u = (world.x - _bounds.min.x) / Mathf.Max(0.01f, _bounds.size.x);
        float v = (world.z - _bounds.min.z) / Mathf.Max(0.01f, _bounds.size.z);
        return new Vector2((u - 0.5f) * size.x, (v - 0.5f) * size.y);
    }

    private Vector2 ToMapSize(Vector3 worldSize)
    {
        Vector2 size = _content.rect.size;
        return new Vector2(worldSize.x / _bounds.size.x * size.x, worldSize.z / _bounds.size.z * size.y);
    }

    public void Tick(NCPlayer me)
    {
        TryInit();
        if (_ready == false)
        {
            return;
        }

        foreach (RoomMark mark in _rooms)
        {
            mark.Tick();
        }

        foreach (SupplyMark mark in _supplies)
        {
            mark.Tick();
        }

        // 플레이어: 새로 들어오면 만들고, 나가면 지운다.
        foreach (NCPlayer player in NCPlayer.All)
        {
            if (player.Object == null || player.Object.IsValid == false)
            {
                continue;
            }

            if (_players.TryGetValue(player, out PlayerMark mark) == false)
            {
                mark = new PlayerMark(_content);
                _players[player] = mark;
            }

            mark.Tick(player, player == me, this);
        }

        _gone.Clear();
        foreach (KeyValuePair<NCPlayer, PlayerMark> pair in _players)
        {
            if (pair.Key == null || NCPlayer.All.Contains(pair.Key) == false)
            {
                _gone.Add(pair.Key);
            }
        }

        foreach (NCPlayer player in _gone)
        {
            _players[player].Destroy();
            _players.Remove(player);
        }
    }

    // ───────── 표식 ─────────

    private class RoomMark
    {
        private readonly NCCombatRoom _room;
        private readonly Image _frame;
        private readonly Image _fill;
        private readonly TextMeshProUGUI _text;
        private int _shown = -1;

        public RoomMark(RectTransform parent, NCCombatRoom room, NCMinimap map)
        {
            _room = room;
            Bounds b = room.AreaBounds;

            _fill = NCUiKit.NewImage("Room_" + room.RoomName, parent, NCUiKit.RoundSprite, Color.clear);
            NCUiKit.Place(_fill.rectTransform, new Vector2(0.5f, 0.5f), map.ToMap(b.center), map.ToMapSize(b.size));

            _frame = NCUiKit.NewImage("Frame", _fill.rectTransform, NCUiKit.FrameSprite, NCStyle.BrassDim);
            NCUiKit.Stretch(_frame.rectTransform);

            _text = NCUiKit.NewText("Text", _fill.rectTransform, "", 13f, NCStyle.Success, style: FontStyles.Bold);
            NCUiKit.Stretch(_text.rectTransform);
        }

        public void Tick()
        {
            if (_room == null || _room.Object == null || _room.Object.IsValid == false)
            {
                return;
            }

            int state = _room.State == NCRoomState.Cleared ? 2 : (_room.IsBusy ? 1 : 0);

            if (state != _shown)
            {
                _shown = state;
                _fill.color = state == 2 ? NCStyle.Hex("6FCF97", 0.28f) : (state == 1 ? NCStyle.Hex("E5484D", 0.18f) : Color.clear);
                _frame.color = state == 2 ? NCStyle.Success : (state == 1 ? NCStyle.Danger : NCStyle.Hex("B6955E", 0.35f));
                _text.text = state == 2 ? "완료" : "";
            }

            // 전투 중인 방은 테두리가 깜빡인다.
            if (state == 1)
            {
                float a = 0.45f + 0.55f * Mathf.PingPong(Time.time * 2.5f, 1f);
                _frame.color = new Color(NCStyle.Danger.r, NCStyle.Danger.g, NCStyle.Danger.b, a);
            }
        }
    }

    private class SupplyMark
    {
        private readonly NCSupplyBox _box;
        private readonly Image _icon;
        private int _shown = -1;

        public SupplyMark(RectTransform parent, NCSupplyBox box, NCMinimap map)
        {
            _box = box;
            _icon = NCUiKit.NewImage("Supply_" + box.BoxName, parent, NCUiKit.RoundSprite, NCStyle.TextDim);
            NCUiKit.Place(_icon.rectTransform, new Vector2(0.5f, 0.5f), map.ToMap(box.transform.position), new Vector2(11f, 11f));
        }

        public void Tick()
        {
            if (_box == null || _box.Object == null || _box.Object.IsValid == false)
            {
                return;
            }

            int state = (int)_box.State;
            if (state != _shown)
            {
                _shown = state;
                _icon.color = _box.State == NCSupplyState.Used ? NCStyle.Hex("F4F1E7", 0.25f) : NCStyle.Hex("9A9A9A");
            }

            if (_box.State == NCSupplyState.Available)
            {
                _icon.color = Color.Lerp(NCStyle.P2, Color.white, Mathf.PingPong(Time.time * 2f, 1f) * 0.5f);
            }
        }
    }

    private class PlayerMark
    {
        private readonly Image _ring;
        private readonly Image _dot;
        private readonly TextMeshProUGUI _label;
        private int _shownSlot = -1;
        private int _shownMe = -1;

        public PlayerMark(RectTransform parent)
        {
            _ring = NCUiKit.NewImage("Player", parent, NCUiKit.CircleSprite, Color.white);
            _dot = NCUiKit.NewImage("Dot", _ring.rectTransform, NCUiKit.CircleSprite, NCStyle.P1);
            _label = NCUiKit.NewText("Label", _ring.rectTransform, "", 12f, NCStyle.Text, style: FontStyles.Bold);
            NCUiKit.Place(_label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -14f), new Vector2(30f, 14f));
        }

        public void Tick(NCPlayer player, bool isMe, NCMinimap map)
        {
            int meKey = isMe ? 1 : 0;
            if (player.Slot != _shownSlot || meKey != _shownMe)
            {
                _shownSlot = player.Slot;
                _shownMe = meKey;
                float size = isMe ? 14f : 11f;
                _ring.rectTransform.sizeDelta = Vector2.one * size;
                NCUiKit.Place(_dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (size - (isMe ? 4f : 2f)));
                _ring.color = isMe ? Color.white : NCStyle.PanelDeep;
                _label.text = player.SlotLabel;
                _label.color = player.SlotColor;
                _ring.transform.SetAsLastSibling(); // 다른 표식 위에 그린다
            }

            _ring.rectTransform.anchorMin = _ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _ring.rectTransform.anchoredPosition = map.ToMap(player.transform.position);

            // 다운이면 붉게 깜빡인다.
            _dot.color = player.Health.IsDown
                ? Color.Lerp(NCStyle.Danger, Color.white, Mathf.PingPong(Time.time * 4f, 1f) * 0.5f)
                : player.SlotColor;
        }

        public void Destroy()
        {
            if (_ring != null)
            {
                Object.Destroy(_ring.gameObject);
            }
        }
    }
}
