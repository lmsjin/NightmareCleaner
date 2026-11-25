using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 한 명의 머리 위 표시. 체력바는 모두에게, 체력 숫자·탄창·재장전은 자기 것만 보여 준다.
/// 상대는 카메라에서 벽에 가려지면 숨긴다(위치를 알려 주지 않기 위해).
/// 카메라가 움직인 뒤(LateUpdate)에 Tick을 불러야 흔들리지 않는다.
/// </summary>
public class NCOverhead
{
    private const float HeadHeight = 2.15f; // 머리 위 기준점(발에서부터)
    private const float FaceHeight = 1.7f;
    private const float ChestHeight = 1f;
    private const float LowHpRatio = 0.3f;

    private static readonly Vector2 BarSize = new Vector2(150f, 20f);
    private static readonly Vector2 ThinBarSize = new Vector2(150f, 10f);
    private const float BarY = 34f;

    private readonly int _slot;
    private readonly RectTransform _canvas;
    private readonly RectTransform _root;
    private readonly Image _hpFill;
    private readonly TextMeshProUGUI _hpText;
    private readonly TextMeshProUGUI _ammoText;
    private readonly Image _reloadFill;
    private readonly RectTransform _reloadBar;
    private readonly Image _dashFill;
    private readonly RectTransform _dashBar;

    // 값이 바뀔 때만 글자를 새로 만든다.
    private int _shownHp = -1;
    private int _shownAmmo = int.MinValue;
    private int _shownMagazine = -1;

    public NCOverhead(RectTransform parent, int slot)
    {
        _slot = slot;
        _canvas = parent;
        _root = NCUiKit.NewRect("Overhead" + slot, parent);
        NCUiKit.Place(_root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        _hpFill = NCUiKit.NewBar("Hp", _root, NCStyle.SlotColor(slot), out RectTransform hpBar);
        NCUiKit.Place(hpBar, new Vector2(0.5f, 0.5f), new Vector2(0f, BarY), BarSize);

        _hpText = NCUiKit.NewText("HpText", hpBar, "", 16f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Stretch(_hpText.rectTransform);

        Image ammoBack = NCUiKit.NewImage("AmmoBack", _root, NCUiKit.RoundSprite, NCStyle.BarBack);
        NCUiKit.Place(ammoBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(BarSize.x * 0.5f + 44f, BarY), new Vector2(76f, 28f));
        _ammoText = NCUiKit.NewText("Ammo", ammoBack.transform, "", 20f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Stretch(_ammoText.rectTransform);

        _reloadFill = NCUiKit.NewBar("Reload", _root, NCStyle.Text, out _reloadBar);
        NCUiKit.Place(_reloadBar, new Vector2(0.5f, 0.5f), new Vector2(0f, BarY - 17f), ThinBarSize);

        // 대쉬 대기(대기 중에만)
        _dashFill = NCUiKit.NewBar("Dash", _root, NCStyle.Success, out _dashBar);
        NCUiKit.Place(_dashBar, new Vector2(0.5f, 0.5f), new Vector2(0f, BarY - 29f), ThinBarSize);
    }

    public void Tick()
    {
        NCPlayer player = NCPlayer.FindBySlot(_slot);
        Camera cam = Camera.main;

        bool show = player != null && player.Object != null && player.Object.IsValid && cam != null
                    && player.Health.IsDown == false;
        bool mine = show && player == NCPlayer.Local;

        if (show && mine == false)
        {
            show = IsVisible(cam.transform.position, player.transform.position);
        }

        if (Follow(_root, _canvas, cam, show, show ? player.transform.position + Vector3.up * HeadHeight : Vector3.zero) == false)
        {
            return;
        }

        ShowDash(player, mine);
        ShowHealth(player.Health, mine);
        ShowAmmo(player.Gun, mine);
    }

    /// <summary>월드 지점의 화면 위치로 옮긴다. 보이지 않으면 숨기고 false.</summary>
    public static bool Follow(RectTransform root, RectTransform canvas, Camera cam, bool show, Vector3 world)
    {
        Vector3 screen = show ? cam.WorldToScreenPoint(world) : Vector3.zero;
        show = show && screen.z > 0f;

        if (root.gameObject.activeSelf != show)
        {
            root.gameObject.SetActive(show);
        }

        if (show)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local);
            root.anchoredPosition = local;
        }

        return show;
    }

    /// <summary>머리나 가슴이 벽에 가리지 않고 카메라에 보이는지.</summary>
    public static bool IsVisible(Vector3 eye, Vector3 feet)
    {
        return IsClear(eye, feet + Vector3.up * FaceHeight) || IsClear(eye, feet + Vector3.up * ChestHeight);
    }

    private static bool IsClear(Vector3 eye, Vector3 point)
    {
        Vector3 to = point - eye;
        float distance = to.magnitude;
        return NCBulletSystem.WallDistance(eye, to / distance, distance, 0f) >= distance;
    }

    private void ShowDash(NCPlayer player, bool mine)
    {
        float ratio = player.DashReadyRatio;
        bool waiting = mine && ratio < 1f;

        if (_dashBar.gameObject.activeSelf != waiting)
        {
            _dashBar.gameObject.SetActive(waiting);
        }

        if (waiting)
        {
            NCUiKit.SetBar(_dashFill, ratio);
        }
    }

    private void ShowHealth(NCHealth health, bool mine)
    {
        float ratio = health.Ratio;
        NCUiKit.SetBar(_hpFill, ratio);
        _hpFill.color = ratio <= LowHpRatio ? NCStyle.Danger : NCStyle.SlotColor(_slot);

        _hpText.enabled = mine;
        if (mine && health.Hp != _shownHp)
        {
            _shownHp = health.Hp;
            _hpText.text = health.Hp.ToString();
        }
    }

    private void ShowAmmo(NCGun gun, bool mine)
    {
        GameObject ammoBack = _ammoText.transform.parent.gameObject;
        if (ammoBack.activeSelf != mine)
        {
            ammoBack.SetActive(mine);
        }

        bool reloading = mine && gun.IsReloading;
        if (_reloadBar.gameObject.activeSelf != reloading)
        {
            _reloadBar.gameObject.SetActive(reloading);
        }

        if (mine == false)
        {
            return;
        }

        NCUiKit.SetBar(_reloadFill, gun.ReloadRatio);

        bool unlimited = gun.IsOvercharged;
        int ammo = unlimited ? -100 : gun.Ammo;
        if (ammo == _shownAmmo && gun.MagazineSize == _shownMagazine)
        {
            return;
        }

        _shownAmmo = ammo;
        _shownMagazine = gun.MagazineSize;
        _ammoText.text = unlimited ? "무제한" : $"{gun.Ammo}/{gun.MagazineSize}";
        _ammoText.color = unlimited ? NCStyle.ShotOvercharge : (gun.Ammo == 0 ? NCStyle.Danger : NCStyle.Text);
    }
}
