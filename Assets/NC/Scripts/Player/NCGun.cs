using Fusion;
using UnityEngine;

/// <summary>
/// 탄창·자동 재장전·과충전·발사 요청. 탄환 이동과 명중은 NCBulletSystem(Host)이 맡는다.
///  - 마지막 탄을 쏘는 즉시 자동 재장전. 재장전 중 이동은 되고 사격은 안 된다.
///  - 과충전(Q): 탄창을 채우고 재장전을 취소한 뒤, 일정 시간 탄약 소모 없이 더 빠르게 쏜다.
///  - 대쉬 중에는 사격할 수 없고 재장전은 계속된다.
///  - 부활 뒤에는 사격 버튼을 놓았다 다시 눌러야 발사된다.
/// </summary>
public class NCGun : NetworkBehaviour
{
    [Networked] public int Ammo { get; set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    [Networked] private TickTimer FireCooldown { get; set; }
    [Networked] private TickTimer OverchargeTimer { get; set; }
    [Networked] private NetworkBool NeedRelease { get; set; }

    /// <summary>과충전 보유 수(0 또는 1).</summary>
    [Networked] public int Charges { get; set; }

    public bool IsOvercharged => OverchargeTimer.IsRunning && OverchargeTimer.Expired(Runner) == false;
    public float OverchargeRemaining => IsOvercharged ? OverchargeTimer.RemainingTime(Runner) ?? 0f : 0f;
    public float OverchargeRatio => Mathf.Clamp01(OverchargeRemaining / NCBalance.Get.overchargeSeconds);
    public bool IsReloading => ReloadTimer.IsRunning;
    public int MagazineSize => NCBalance.Get.magazineSize;

    public float ReloadRatio => IsReloading
        ? Mathf.Clamp01(1f - (ReloadTimer.RemainingTime(Runner) ?? 0f) / NCBalance.Get.reloadSeconds)
        : 0f;

    private NCPlayer _player;

    private void Awake()
    {
        _player = GetComponent<NCPlayer>();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            ServerResetAll();
        }
    }

    /// <summary>캠프 막타 보상. 발동 중이면 시간을 갱신하고, 아니면 1회 보유(누적 없음). Host 전용.</summary>
    public void ServerGrantOvercharge()
    {
        if (IsOvercharged)
        {
            OverchargeTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.overchargeSeconds);
        }
        else
        {
            Charges = 1;
        }
    }

    /// <summary>경기 종료: 보상 제거. Host 전용.</summary>
    public void ServerResetCharges()
    {
        Charges = 0;
        OverchargeTimer = TickTimer.None;
    }

    /// <summary>사망·부활·경기 초기화. Host 전용.</summary>
    public void ServerResetAll()
    {
        ServerResetCharges();
        Ammo = MagazineSize;
        ReloadTimer = TickTimer.None;
        FireCooldown = TickTimer.None;
        NeedRelease = true;
    }

    public override void FixedUpdateNetwork()
    {
        NCBalance balance = NCBalance.Get;

        if (ReloadTimer.IsRunning && ReloadTimer.Expired(Runner))
        {
            ReloadTimer = TickTimer.None;
            Ammo = MagazineSize;
        }

        if (_player.CanAct == false)
        {
            ReloadTimer = TickTimer.None;
            return;
        }

        if (GetInput(out NCInput input) == false)
        {
            return;
        }

        if (input.IsDown(NCButton.Overcharge) && Charges > 0 && IsOvercharged == false)
        {
            _player.Health.ServerEndProtection();
            Charges--;
            ReloadTimer = TickTimer.None;
            Ammo = MagazineSize;
            OverchargeTimer = TickTimer.CreateFromSeconds(Runner, balance.overchargeSeconds);
        }

        if (input.IsDown(NCButton.Fire) == false)
        {
            NeedRelease = false;
        }

        if (input.IsDown(NCButton.Reload) && IsReloading == false && IsOvercharged == false && Ammo < MagazineSize)
        {
            StartReload();
        }

        if (input.IsDown(NCButton.Fire) == false || NeedRelease || IsReloading || Ammo <= 0 || _player.IsDashing
            || FireCooldown.ExpiredOrNotRunning(Runner) == false)
        {
            return;
        }

        bool overcharged = IsOvercharged;
        FireCooldown = TickTimer.CreateFromSeconds(Runner, overcharged ? balance.overchargeFireInterval : balance.fireInterval);
        if (overcharged == false)
        {
            Ammo--;

            if (Ammo <= 0)
            {
                StartReload();
            }
        }

        if (Object.HasStateAuthority && NCBulletSystem.Instance != null)
        {
            _player.Health.ServerEndProtection();
            NCBulletSystem.Instance.ServerFire(_player, input.AimPoint, overcharged);
        }
    }

    private void StartReload()
    {
        ReloadTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.reloadSeconds);
    }
}
