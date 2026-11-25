using Fusion;
using UnityEngine;

/// <summary>
/// 체력·자연 회복·사망·부활·부활 보호. 판정은 Host만 하고 화면은 [Networked] 값을 그린다.
/// 피격 무적은 없고, 보호 중에는 피해가 무효이며 회복 대기도 초기화하지 않는다.
/// </summary>
public class NCHealth : NetworkBehaviour
{
    [Networked] public int Hp { get; set; }
    [Networked] public int MaxHp { get; set; }

    /// <summary>사망 대기 중.</summary>
    [Networked] public NetworkBool IsDown { get; set; }
    [Networked] private TickTimer RespawnTimer { get; set; }
    [Networked] private TickTimer ProtectTimer { get; set; }
    [Networked] private TickTimer RegenDelay { get; set; }

    /// <summary>실제 피해 횟수(피격 연출).</summary>
    [Networked] public int HitCount { get; set; }

    public float Ratio => MaxHp > 0 ? Mathf.Clamp01((float)Hp / MaxHp) : 0f;
    public bool IsProtected => IsDown == false && ProtectTimer.IsRunning && ProtectTimer.Expired(Runner) == false;
    public float RespawnRemaining => IsDown ? Mathf.Max(0f, RespawnTimer.RemainingTime(Runner) ?? 0f) : 0f;

    private NCPlayer _player;
    private float _regen; // Host: 1 미만의 회복 누적

    private Color _shownColor = new Color(-1f, -1f, -1f, -1f);
    private int _seenHitCount;
    private float _flashTimer;
    private Transform _body;
    private GameObject _shield;

    private void Awake()
    {
        _player = GetComponent<NCPlayer>();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            ServerResetFull();
        }

        _seenHitCount = HitCount;
        _body = transform.Find("Body");

        MeshRenderer shield = NCFx.NewShape(NCFx.SphereMesh, "Shield", transform);
        shield.transform.localPosition = Vector3.up * 0.9f;
        shield.transform.localScale = new Vector3(1.5f, 2.1f, 1.5f);
        NCFx.SetColor(shield, new Color(0.9f, 0.95f, 1f, 0.2f));
        _shield = shield.gameObject;
        _shield.SetActive(false);
    }

    // ───────── Host 전용 ─────────

    /// <summary>실제로 들어간 피해량을 돌려준다(보호·사망 중이면 0).</summary>
    public int ServerTakeDamage(int amount)
    {
        if (Object.HasStateAuthority == false || IsDown || IsProtected || amount <= 0)
        {
            return 0;
        }

        int dealt = Mathf.Min(Hp, amount);
        Hp -= dealt;
        HitCount++;
        _regen = 0f;
        RegenDelay = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.regenDelaySeconds);

        if (Hp <= 0)
        {
            IsDown = true;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.respawnSeconds);
            _player.ServerResetDash();
            _player.Gun.ServerResetAll();
        }

        return dealt;
    }

    public void ServerEndProtection()
    {
        if (Object.HasStateAuthority)
        {
            ProtectTimer = TickTimer.None;
        }
    }

    public void ServerResetFull()
    {
        MaxHp = NCBalance.Get.playerMaxHp;
        Hp = MaxHp;
        IsDown = false;
        RespawnTimer = TickTimer.None;
        ProtectTimer = TickTimer.None;
        RegenDelay = TickTimer.None;
        _regen = 0f;
    }

    /// <summary>경기 진행 중 매 Tick: 부활 또는 자연 회복.</summary>
    public void ServerTick()
    {
        if (IsDown)
        {
            if (RespawnTimer.Expired(Runner))
            {
                ServerResetFull();
                _player.Gun.ServerResetAll();
                _player.ServerMoveToSpawn();
                ProtectTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.protectSeconds);
            }

            return;
        }

        if (Hp >= MaxHp || RegenDelay.ExpiredOrNotRunning(Runner) == false)
        {
            return;
        }

        _regen += NCBalance.Get.regenPerSecond * Runner.DeltaTime;
        int whole = Mathf.FloorToInt(_regen);
        if (whole > 0)
        {
            _regen -= whole;
            Hp = Mathf.Min(MaxHp, Hp + whole);
        }
    }

    // ───────── 연출 (모든 화면) ─────────

    public override void Render()
    {
        if (HitCount != _seenHitCount)
        {
            _seenHitCount = HitCount;
            _flashTimer = 0.15f;
        }

        _flashTimer -= Time.deltaTime;

        if (_body != null)
        {
            Quaternion tilt = IsDown ? Quaternion.Euler(80f, 0f, 0f) : Quaternion.identity;
            _body.localRotation = Quaternion.Slerp(_body.localRotation, tilt, 12f * Time.deltaTime);
        }

        Renderer body = _player.BodyRenderer;
        if (body != null)
        {
            Color color = _flashTimer > 0f ? NCStyle.Danger : (IsDown ? _player.SlotColor * 0.45f : _player.SlotColor);
            if (color != _shownColor)
            {
                _shownColor = color;
                body.material.color = color;
            }
        }

        if (_shield != null && _shield.activeSelf != IsProtected)
        {
            _shield.SetActive(IsProtected);
        }
    }
}
