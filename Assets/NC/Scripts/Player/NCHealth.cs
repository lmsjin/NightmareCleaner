using Fusion;
using UnityEngine;

/// <summary>
/// 체력·피격·다운·구조. 판정은 Host만 하고 화면은 [Networked] 값을 그린다.
///
///  - 체력 0이면 사망이 아니라 다운(이동·사격·E 불가, 유령이 노리지 않음). 출혈 타이머는 없다.
///  - 구조: 살아 있는 동료가 1.5m 안에서 E를 3초 유지 → 체력 40 + 2초 보호.
///    구조 대상은 입력 시작 때 고정되고, 구조자의 이동·사격·피격·E 해제·범위 이탈 시 진행이 0으로 돌아간다.
///  - 판정 주체는 다운된 쪽이다(주변의 구조자를 찾아 진행도를 올린다).
/// </summary>
public class NCHealth : NetworkBehaviour
{
    [Networked] public int Hp { get; set; }
    [Networked] public int MaxHp { get; set; }
    [Networked] public NetworkBool IsDown { get; set; }
    [Networked] private TickTimer InvincibleTimer { get; set; }

    /// <summary>실제로 피해를 입은 횟수. 바뀌면 "방금 맞았다"는 뜻(구조 취소·피격 연출).</summary>
    [Networked] public int HitCount { get; set; }

    /// <summary>구조 진행(초)과 구조자. 구조자가 없으면 PlayerRef.None.</summary>
    [Networked] public float ReviveProgress { get; set; }
    [Networked] public PlayerRef Rescuer { get; set; }
    [Networked] private int RescuerHitCount { get; set; }

    /// <summary>구조된 횟수(구조 성공 알림용).</summary>
    [Networked] public int ReviveCount { get; set; }

    public float Ratio => MaxHp > 0 ? Mathf.Clamp01((float)Hp / MaxHp) : 0f;
    public float ReviveRatio => Mathf.Clamp01(ReviveProgress / NCBalance.Get.reviveHoldSeconds);
    public bool IsInvincible => InvincibleTimer.ExpiredOrNotRunning(Runner) == false;

    private NCPlayer _player;

    // 연출용(로컬)
    private Color _shownColor = new Color(-1f, -1f, -1f, -1f);
    private int _seenHitCount;
    private float _flashTimer;
    private GameObject _downMarker;
    private Transform _body;

    private void Awake()
    {
        _player = GetComponent<NCPlayer>();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            MaxHp = NCBalance.Get.playerMaxHp;
            Hp = MaxHp;
            IsDown = false;
        }

        _seenHitCount = HitCount;
        _body = transform.Find("Body"); // 발밑 기준 회전축(다운 시 눕는다)
        _downMarker = NCFx.CreateDownMarker(transform);
    }

    // ───────── Host 전용 ─────────

    /// <summary>유령의 공격 등으로 피해를 입는다. Host에서만 호출.</summary>
    public void TakeDamage(int amount)
    {
        if (Object.HasStateAuthority == false || IsDown || IsInvincible)
        {
            return;
        }

        Hp = Mathf.Max(0, Hp - amount);
        HitCount++;
        InvincibleTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.hitInvincibleSeconds);

        if (Hp <= 0)
        {
            IsDown = true;
            ReviveProgress = 0f;
            Rescuer = PlayerRef.None;
        }
    }

    /// <summary>구조 완료·방 소탕으로 일어난다. Host에서만 호출.</summary>
    public void ServerRevive()
    {
        if (Object.HasStateAuthority == false || IsDown == false)
        {
            return;
        }

        NCBalance balance = NCBalance.Get;
        Hp = balance.reviveHp;
        IsDown = false;
        ReviveProgress = 0f;
        Rescuer = PlayerRef.None;
        ReviveCount++;
        InvincibleTimer = TickTimer.CreateFromSeconds(Runner, balance.reviveProtectSeconds);
    }

    /// <summary>재도전용: 체력 가득, 다운·무적·구조 진행 해제.</summary>
    public void ServerResetFull()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        Hp = MaxHp;
        IsDown = false;
        InvincibleTimer = TickTimer.None;
        ReviveProgress = 0f;
        Rescuer = PlayerRef.None;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false || IsDown == false)
        {
            return;
        }

        NCPlayer rescuer = FindRescuer();

        if (rescuer == null)
        {
            ReviveProgress = 0f;
            Rescuer = PlayerRef.None;
            return;
        }

        int hits = rescuer.Health.HitCount;

        // 새 구조 시작이거나 구조 중 구조자가 맞았으면 처음부터.
        if (Rescuer != rescuer.Object.InputAuthority || hits != RescuerHitCount)
        {
            Rescuer = rescuer.Object.InputAuthority;
            RescuerHitCount = hits;
            ReviveProgress = 0f;
            return;
        }

        ReviveProgress += Runner.DeltaTime;

        if (ReviveProgress >= NCBalance.Get.reviveHoldSeconds)
        {
            ServerRevive();
        }
    }

    /// <summary>구조 조건을 만족하는 동료. 이미 구조 중인 동료를 우선한다.</summary>
    private NCPlayer FindRescuer()
    {
        NCPlayer best = null;
        float bestDistance = float.MaxValue;
        float range = NCBalance.Get.reviveRange;

        foreach (NCPlayer other in NCPlayer.All)
        {
            if (other == _player || other.Health.IsDown)
            {
                continue;
            }

            float distance = NCMath.FlatDistance(other.transform.position, transform.position);
            bool ok = other.HoldingInteract && other.IsMoving == false && other.IsFiring == false && distance <= range;

            if (ok == false)
            {
                continue;
            }

            if (other.Object.InputAuthority == Rescuer)
            {
                return other;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = other;
            }
        }

        return best;
    }

    // ───────── 연출 (모든 화면) ─────────

    public override void Render()
    {
        // 피격 깜빡임
        if (HitCount != _seenHitCount)
        {
            _seenHitCount = HitCount;
            _flashTimer = 0.18f;
        }

        _flashTimer -= Time.deltaTime;

        if (_body != null)
        {
            // 다운이면 몸을 눕힌다.
            Quaternion targetTilt = IsDown ? Quaternion.Euler(80f, 0f, 0f) : Quaternion.identity;
            _body.localRotation = Quaternion.Slerp(_body.localRotation, targetTilt, 12f * Time.deltaTime);

            Renderer renderer = _player.BodyRenderer;
            if (renderer != null)
            {
                Color color = _player.SlotColor;
                if (_flashTimer > 0f)
                {
                    color = Color.white;
                }
                else if (IsInvincible && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f)
                {
                    color = Color.Lerp(color, Color.white, 0.5f);
                }

                // 색이 바뀔 때만 적용한다.
                if (color != _shownColor)
                {
                    _shownColor = color;
                    renderer.material.color = color;
                }
            }
        }

        if (_downMarker != null && _downMarker.activeSelf != IsDown)
        {
            _downMarker.SetActive(IsDown);
        }
    }
}
