using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미니맵(오른쪽 상단, 위 = 북쪽). 맵 윤곽 위에 점령 구역·유령 캠프·내 위치만 표시한다.
/// 상대 위치는 공개하지 않는다.
/// </summary>
public class NCMinimap
{
    private const float MaxWidth = 380f;
    private const float MaxHeight = 230f;

    private readonly RectTransform _panel;
    private readonly RectTransform _content;
    private readonly RawImage _map;
    private readonly List<CampMark> _camps = new List<CampMark>();
    private Image _zone;
    private Image _me;
    private Image _meDot;
    private Bounds _bounds;
    private bool _ready;

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

    private void TryInit(NCSession session)
    {
        if (_ready || NCMapSnapshot.IsReady == false || session == null)
        {
            return;
        }

        _ready = true;
        _bounds = NCMapSnapshot.Bounds;
        _map.texture = NCMapSnapshot.Texture;

        float aspect = _bounds.size.x / Mathf.Max(0.01f, _bounds.size.z);
        Vector2 size = aspect >= MaxWidth / MaxHeight
            ? new Vector2(MaxWidth, MaxWidth / aspect)
            : new Vector2(MaxHeight * aspect, MaxHeight);

        _content.sizeDelta = size;
        _panel.sizeDelta = size + new Vector2(16f, 16f);

        foreach (NCGhostCamp camp in NCGhostCamp.All)
        {
            _camps.Add(new CampMark(_content, camp, this));
        }

        float diameter = NCBalance.Get.captureRadius * 2f;
        Vector2 zoneSize = ToMapSize(new Vector3(diameter, 0f, diameter));
        _zone = NCUiKit.NewImage("Zone", _content, NCUiKit.CircleSprite, NCStyle.Text);
        NCUiKit.Place(_zone.rectTransform, new Vector2(0.5f, 0.5f), ToMap(session.ZonePosition), Vector2.Max(zoneSize, Vector2.one * 14f));

        _me = NCUiKit.NewImage("Me", _content, NCUiKit.CircleSprite, Color.white);
        NCUiKit.Place(_me.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 14f);
        _meDot = NCUiKit.NewImage("Dot", _me.rectTransform, NCUiKit.CircleSprite, NCStyle.P1);
        NCUiKit.Place(_meDot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 10f);
    }

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

    public void Tick(NCPlayer me, NCSession session)
    {
        TryInit(session);
        if (_ready == false)
        {
            return;
        }

        foreach (CampMark camp in _camps)
        {
            camp.Tick();
        }

        if (session != null && session.Object != null && session.Object.IsValid)
        {
            Color color = NCSession.ZoneColor(session.Zone);
            color.a = session.Zone == NCZoneState.Contested ? 0.4f + 0.5f * Mathf.PingPong(Time.time * 2f, 1f) : 0.7f;
            _zone.color = color;
        }

        bool valid = me != null && me.Object != null && me.Object.IsValid;
        _me.gameObject.SetActive(valid);

        if (valid)
        {
            _me.rectTransform.anchoredPosition = ToMap(me.transform.position);
            _meDot.color = me.SlotColor;
        }
    }

    private class CampMark
    {
        private readonly NCGhostCamp _camp;
        private readonly Image _frame;
        private readonly TextMeshProUGUI _text;
        private int _shown = -1;

        public CampMark(RectTransform parent, NCGhostCamp camp, NCMinimap map)
        {
            _camp = camp;
            Bounds b = camp.AreaBounds;
            RectTransform rect = NCUiKit.NewRect("Camp", parent);
            NCUiKit.Place(rect, new Vector2(0.5f, 0.5f), map.ToMap(b.center), map.ToMapSize(b.size));
            _frame = NCUiKit.NewImage("Frame", rect, NCUiKit.FrameSprite, NCStyle.BrassDim);
            NCUiKit.Stretch(_frame.rectTransform);
            _text = NCUiKit.NewText("Text", rect, "", 13f, NCStyle.Text, style: FontStyles.Bold);
            NCUiKit.Stretch(_text.rectTransform);
        }

        public void Tick()
        {
            if (_camp == null || _camp.Object == null || _camp.Object.IsValid == false)
            {
                return;
            }

            int state = _camp.IsActive ? 1 : 0;
            if (state != _shown)
            {
                _shown = state;
                _frame.color = state == 1 ? NCStyle.GhostTelegraph : NCStyle.BrassDim;
                _text.text = state == 1 ? "유령" : "";
            }
        }
    }
}
