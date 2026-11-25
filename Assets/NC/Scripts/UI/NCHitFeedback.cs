using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 명중·피격 피드백(내 화면만). Host가 확정한 피해를 NCBulletSystem의 RPC로 받아 그린다.
///  - 내가 맞히면: 명중음, 커서의 X 표시, 피해 숫자
///  - 내가 맞으면: 피격음, 화면 가장자리 번쩍임, 맞은 방향
/// 조준을 흔들지 않도록 카메라는 건드리지 않는다.
/// </summary>
public class NCHitFeedback
{
    private const float MarkerSeconds = 0.12f;
    private const float KillMarkerSeconds = 0.3f;
    private const float HurtSeconds = 0.25f;
    private const float DirectionSeconds = 0.6f;
    private const float DirectionRadius = 180f;
    private const float NumberSeconds = 0.6f;
    private const int NumberCount = 12;

    private static NCHitFeedback _instance;

    private class Number
    {
        public TextMeshProUGUI Text;
        public RectTransform Root;
        public Vector3 World;
        public float Age = NumberSeconds;
    }

    private readonly RectTransform _canvas;
    private readonly System.Action<string, Color> _notify;
    private readonly Number[] _numbers = new Number[NumberCount];
    private readonly RectTransform _marker;
    private readonly Image[] _markerBars = new Image[4];
    private readonly Image _hurt;
    private readonly RectTransform _direction;
    private readonly Image _directionBar;

    private int _nextNumber;
    private float _markerLeft;
    private float _markerTotal = 1f;
    private float _hurtLeft;
    private float _directionLeft;
    private Vector3 _hurtOrigin;

    public NCHitFeedback(RectTransform root, System.Action<string, Color> notify)
    {
        _instance = this;
        _canvas = root;
        _notify = notify;

        _hurt = NCUiKit.NewImage("HurtFlash", root, EdgeSprite(), NCStyle.Danger);
        _hurt.type = Image.Type.Simple;
        NCUiKit.Stretch(_hurt.rectTransform);
        _hurt.transform.SetAsFirstSibling(); // HUD를 가리지 않게 가장 뒤에 그린다.

        _direction = NCUiKit.NewRect("HurtDirection", root);
        NCUiKit.Place(_direction, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        _directionBar = NCUiKit.NewImage("Bar", _direction, NCUiKit.RoundSprite, NCStyle.Danger);
        NCUiKit.Place(_directionBar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(DirectionRadius, 0f), new Vector2(16f, 84f));

        for (int i = 0; i < NumberCount; i++)
        {
            RectTransform rect = NCUiKit.NewRect("Damage" + i, root);
            NCUiKit.Place(rect, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI text = NCUiKit.NewText("Text", rect, "", 30f, NCStyle.Text, style: FontStyles.Bold);
            NCUiKit.Place(text.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 40f));
            rect.gameObject.SetActive(false);
            _numbers[i] = new Number { Root = rect, Text = text };
        }

        _marker = NCUiKit.NewRect("HitMarker", root);
        NCUiKit.Place(_marker, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        for (int i = 0; i < 4; i++)
        {
            _markerBars[i] = NCUiKit.NewImage("Bar" + i, _marker, null, Color.white);
            RectTransform bar = _markerBars[i].rectTransform;
            float angle = 45f + 90f * i;
            Vector2 outward = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            NCUiKit.Place(bar, new Vector2(0.5f, 0.5f), outward * 18f, new Vector2(14f, 4f));
            bar.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        Clear();
    }

    /// <summary>확정된 피해 한 건. victimSlot이 -1이면 유령, attackerSlot이 -1이면 유령의 공격.</summary>
    public static void OnHit(int attackerSlot, int victimSlot, Vector3 position, Vector3 origin, int damage, bool killed)
    {
        NCPlayer me = NCPlayer.Local;
        bool mine = me != null && me.Slot == attackerSlot;
        bool hurt = me != null && me.Slot == victimSlot;

        if (mine)
        {
            NCSfx.Play(killed ? NCSfxKind.Kill : NCSfxKind.Hit);
            _instance?.ShowHit(position, victimSlot >= 0, damage, killed);
        }
        else if (victimSlot < 0)
        {
            NCSfx.PlayAt(NCSfxKind.GhostHit, position);
        }

        if (hurt)
        {
            NCSfx.Play(killed ? NCSfxKind.Death : NCSfxKind.Hurt);
            _instance?.ShowHurt(origin);
        }
    }

    private void ShowHit(Vector3 position, bool player, int damage, bool killed)
    {
        _markerTotal = killed ? KillMarkerSeconds : MarkerSeconds;
        _markerLeft = _markerTotal;

        foreach (Image bar in _markerBars)
        {
            bar.color = killed ? NCStyle.Danger : Color.white;
        }

        Number number = _numbers[_nextNumber];
        _nextNumber = (_nextNumber + 1) % NumberCount;
        number.Age = 0f;
        number.World = position + new Vector3(Random.Range(-0.3f, 0.3f), 1.5f, 0f);
        number.Text.text = damage.ToString();
        number.Text.color = killed ? NCStyle.Danger : NCStyle.ShotNormal;

        if (killed && player)
        {
            _notify("처치!", NCStyle.Danger);
        }
    }

    private void ShowHurt(Vector3 origin)
    {
        _hurtLeft = HurtSeconds;
        _directionLeft = DirectionSeconds;
        _hurtOrigin = origin;
    }

    /// <summary>카메라가 움직인 뒤(LateUpdate) 호출한다.</summary>
    public void LateTick()
    {
        Camera cam = Camera.main;
        float dt = Time.deltaTime;

        foreach (Number number in _numbers)
        {
            number.Age += dt;
            bool show = number.Age < NumberSeconds && cam != null;
            float t = number.Age / NumberSeconds;

            if (NCOverhead.Follow(number.Root, _canvas, cam, show, number.World + Vector3.up * (0.9f * t)))
            {
                number.Text.alpha = 1f - t * t;
            }
        }

        _markerLeft -= dt;
        bool marker = _markerLeft > 0f && Mouse.current != null;
        SetActive(_marker, marker);

        if (marker)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, Mouse.current.position.ReadValue(), null, out Vector2 local);
            _marker.anchoredPosition = local;
            _marker.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, _markerLeft / _markerTotal);
        }

        _hurtLeft -= dt;
        _hurt.enabled = _hurtLeft > 0f;
        if (_hurt.enabled)
        {
            Color color = NCStyle.Danger;
            color.a = 0.5f * _hurtLeft / HurtSeconds;
            _hurt.color = color;
        }

        TickDirection(cam, dt);
    }

    private void TickDirection(Camera cam, float dt)
    {
        _directionLeft -= dt;
        NCPlayer me = NCPlayer.Local;
        bool show = _directionLeft > 0f && me != null && cam != null;
        Vector3 center = show ? me.transform.position + Vector3.up : Vector3.zero;

        if (NCOverhead.Follow(_direction, _canvas, cam, show, center) == false)
        {
            return;
        }

        // 카메라가 돌지 않으므로 월드 X·Z가 화면 가로·세로와 같다.
        Vector3 to = NCMath.Flat(_hurtOrigin - me.transform.position);
        _direction.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.z, to.x) * Mathf.Rad2Deg);

        Color color = NCStyle.Danger;
        color.a = Mathf.Clamp01(_directionLeft / DirectionSeconds * 1.5f);
        _directionBar.color = color;
    }

    /// <summary>화면이 닫히거나 경기가 바뀔 때 남은 표시를 지운다.</summary>
    public void Clear()
    {
        _markerLeft = 0f;
        _hurtLeft = 0f;
        _directionLeft = 0f;
        _hurt.enabled = false;
        SetActive(_marker, false);
        SetActive(_direction, false);

        foreach (Number number in _numbers)
        {
            number.Age = NumberSeconds;
            SetActive(number.Root, false);
        }
    }

    private static void SetActive(RectTransform rect, bool active)
    {
        if (rect.gameObject.activeSelf != active)
        {
            rect.gameObject.SetActive(active);
        }
    }

    /// <summary>가운데는 투명하고 가장자리로 갈수록 진해지는 그림.</summary>
    private static Sprite EdgeSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                float v = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                float edge = Mathf.InverseLerp(0.8f, 1f, Mathf.Max(u, v));
                pixels[y * size + x] = new Color(1f, 1f, 1f, edge * edge);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
