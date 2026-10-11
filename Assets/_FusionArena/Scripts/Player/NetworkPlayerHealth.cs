using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 체력·피격·다운을 관리한다. 기획서 7장 기준.
///
/// 체력이 0이 되면 사망이 아니라 **다운**이다(이동·사격·Q 불가).
/// 동료의 구조(E 3초, NetworkPlayerRevive)나 전투방 완료로 체력 40으로 복귀한다.
///
/// 모든 판정은 Host(State Authority)가 하고, 클라이언트는 [Networked] 값을 받아 표시만 한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerHealth : NetworkBehaviour
{
    [Header("설정 (Spawned에서 CleanerBalance 값으로 덮어씀)")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private float invincibleSeconds = 0.5f;
    [SerializeField] private int reviveHp = 40;
    [SerializeField] private float reviveProtectSeconds = 2.0f;

    /// <summary>현재 체력. 0이면 다운 상태다.</summary>
    [Networked] public int Hp { get; set; }

    /// <summary>최대 체력. HUD의 비율 계산에 쓴다.</summary>
    [Networked] public int MaxHp { get; set; }

    /// <summary>다운 여부. 이동·사격·Q를 막는 기준이다.</summary>
    [Networked] public NetworkBool IsDown { get; set; }

    /// <summary>
    /// 실제로 피해를 입은 횟수. 값이 바뀌었으면 "방금 맞았다"는 뜻이다.
    /// 구조 중인 동료가 맞았는지(→ 구조 취소) 판정할 때 쓴다.
    /// </summary>
    [Networked] public int HitCount { get; set; }

    // 피격 후 잠시 무적인 동안을 재는 타이머(네트워크 공유).
    [Networked] private TickTimer InvincibleTimer { get; set; }

    /// <summary>지금 피해를 받지 않는 상태인지.</summary>
    public bool IsInvincible => InvincibleTimer.ExpiredOrNotRunning(Runner) == false;

    public override void Spawned()
    {
        CleanerBalance balance = CleanerBalance.Resolve(null);

        if (balance != null)
        {
            maxHp = balance.playerMaxHp;
            invincibleSeconds = balance.hitInvincibleSeconds;
            reviveHp = balance.reviveHp;
            reviveProtectSeconds = balance.reviveProtectSeconds;
        }

        // 초기 체력은 권한을 가진 Host만 정한다.
        if (Object.HasStateAuthority)
        {
            MaxHp = maxHp;
            Hp = maxHp;
            IsDown = false;
        }
    }

    /// <summary>
    /// 유령의 공격 등으로 피해를 입는다. **Host에서만 호출해야 한다.**
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        // 이미 다운됐거나 무적 시간 중이면 무시한다.
        if (IsDown || IsInvincible)
        {
            return;
        }

        Hp = Mathf.Max(0, Hp - amount);
        HitCount++;

        // 연속 피격을 막기 위해 짧은 무적을 건다.
        InvincibleTimer = TickTimer.CreateFromSeconds(Runner, invincibleSeconds);

        if (Hp <= 0)
        {
            // 사망이 아니라 다운이다. 동료가 구조하면 일어난다.
            IsDown = true;
        }
    }

    /// <summary>
    /// 구조 완료 또는 전투방 완료로 다시 일어난다. **Host에서만 호출.**
    /// 복귀 직후 잠시 무적을 준다(기획서: 2초 보호).
    /// </summary>
    public void Revive()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        if (IsDown == false)
        {
            return;
        }

        Hp = reviveHp;
        IsDown = false;
        InvincibleTimer = TickTimer.CreateFromSeconds(Runner, reviveProtectSeconds);
    }

    /// <summary>
    /// 재시작용. 체력을 가득 채우고 다운·무적 상태를 지운다. **Host에서만 호출.**
    /// </summary>
    public void ServerResetFull()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        Hp = MaxHp;
        IsDown = false;
        InvincibleTimer = TickTimer.None;
    }

    /// <summary>
    /// 내 화면에만 내 체력을 임시로 표시한다(정식 HUD는 UI 담당이 제작).
    /// </summary>
    private void OnGUI()
    {
        if (Object == null || Object.IsValid == false || Object.HasInputAuthority == false)
        {
            return;
        }

        // UI 기획서 HUD-03: 왼쪽 하단. (왼쪽 상단은 비워 둔다)
        string text = IsDown
            ? $"체력 0 / {MaxHp} · 다운 · 동료의 구조를 기다리세요"
            : $"체력 {Hp} / {MaxHp}";

        GUI.Label(new Rect(16f, Screen.height - 40f, 360f, 24f), text);
    }
}
