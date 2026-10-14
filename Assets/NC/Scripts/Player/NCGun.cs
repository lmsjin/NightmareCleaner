using Fusion;
using UnityEngine;

/// <summary>
/// 탄창·재장전·히트스캔 사격.
///
///  - 탄창 12, 발사 간격 0.25초, 발당 피해 20, 사거리 9m. 예비 탄약은 무한(표시 안 함).
///  - R로 재장전(1.8초), 재장전 중 사격 불가. 빈 탄창 자동 재장전은 NCBalance 설정(기본 끔).
///  - 판정: 바닥 평면(XZ)에서 조준선 위의 **첫 유령 하나**에만 피해. 벽·봉쇄 장벽 뒤는 맞지 않는다.
///  - 명중 판정과 피해는 Host만, 구름탄 연출은 RPC로 모든 화면에.
/// </summary>
public class NCGun : NetworkBehaviour
{
    [Networked] public int Ammo { get; set; }
    [Networked] private TickTimer ReloadTimer { get; set; }
    [Networked] private TickTimer FireCooldown { get; set; }

    /// <summary>과충전 보유 수(0 또는 1).</summary>
    [Networked] public int Charges { get; set; }

    // 과충전 효과가 끝나는 시점. 사용 중이 아니면 None.
    [Networked] private TickTimer OverchargeTimer { get; set; }

    public bool IsOvercharged => OverchargeTimer.IsRunning && OverchargeTimer.Expired(Runner) == false;

    /// <summary>과충전 효과 남은 비율(1 → 0). HUD 바용.</summary>
    public float OverchargeRatio => IsOvercharged
        ? Mathf.Clamp01((OverchargeTimer.RemainingTime(Runner) ?? 0f) / NCBalance.Get.overchargeSeconds)
        : 0f;

    /// <summary>
    /// 지금 Q를 쓸 수 있는 상황인지. 첫 전투가 시작되기 전엔 불가, 이후엔 전투방이 진행 중(예고·전투·차수 사이)일 때만.
    /// </summary>
    public static bool IsOverchargeAllowed
    {
        get
        {
            NCSession session = NCSession.Instance;
            if (session == null || session.IsMissionRunning == false || session.CombatStarted == false)
            {
                return false;
            }

            foreach (NCCombatRoom room in NCCombatRoom.All)
            {
                if (room.IsBusy)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool IsReloading => ReloadTimer.IsRunning;
    public int MagazineSize => NCBalance.Get.magazineSize;

    /// <summary>재장전 진행(0~1). 재장전 중이 아니면 0.</summary>
    public float ReloadRatio
    {
        get
        {
            if (IsReloading == false)
            {
                return 0f;
            }

            float remaining = ReloadTimer.RemainingTime(Runner) ?? 0f;
            return Mathf.Clamp01(1f - remaining / NCBalance.Get.reloadSeconds);
        }
    }

    private NCPlayer _player;

    private void Awake()
    {
        _player = GetComponent<NCPlayer>();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Ammo = MagazineSize;
        }
    }

    /// <summary>탄창을 즉시 가득 채우고 재장전을 취소한다(방 소탕 보상·재도전). Host 전용.</summary>
    public void ServerRefill()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        ReloadTimer = TickTimer.None;
        Ammo = MagazineSize;
    }

    /// <summary>방 소탕 보상: 과충전 1회 지급(보유 최대 1). Host 전용.</summary>
    public void ServerGrantCharge()
    {
        if (Object.HasStateAuthority)
        {
            Charges = 1;
        }
    }

    /// <summary>과충전 효과를 즉시 끝낸다(방 소탕·게임 종료). 환불 없음. Host 전용.</summary>
    public void ServerEndOvercharge()
    {
        if (Object.HasStateAuthority)
        {
            OverchargeTimer = TickTimer.None;
        }
    }

    /// <summary>재도전용: 보유·효과 모두 0으로.</summary>
    public void ServerResetCharges()
    {
        if (Object.HasStateAuthority)
        {
            Charges = 0;
            OverchargeTimer = TickTimer.None;
        }
    }

    public override void FixedUpdateNetwork()
    {
        NCBalance balance = NCBalance.Get;

        // ① 재장전 완료 · 과충전 시간 종료
        if (ReloadTimer.IsRunning && ReloadTimer.Expired(Runner))
        {
            ReloadTimer = TickTimer.None;
            Ammo = MagazineSize;
        }

        if (OverchargeTimer.IsRunning && OverchargeTimer.Expired(Runner))
        {
            OverchargeTimer = TickTimer.None;
        }

        // ② 행동 불가(다운·결과)면 진행 중 재장전과 과충전 효과도 끝낸다.
        if (_player.CanAct == false)
        {
            ReloadTimer = TickTimer.None;
            OverchargeTimer = TickTimer.None;
            return;
        }

        if (GetInput(out NCInput input) == false)
        {
            return;
        }

        // ③ 과충전 발동: 보유 소비 → 탄창 가득 · 재장전 취소 · 5초간 무소모 + 빠른 연사
        if (input.IsDown(NCButton.Overcharge) && Charges > 0 && IsOvercharged == false && IsOverchargeAllowed)
        {
            Charges--;
            ReloadTimer = TickTimer.None;
            Ammo = MagazineSize;
            OverchargeTimer = TickTimer.CreateFromSeconds(Runner, balance.overchargeSeconds);
        }

        // ④ 재장전 시작(과충전 중에는 탄이 줄지 않으니 필요 없음)
        if (IsReloading == false && IsOvercharged == false)
        {
            bool manual = input.IsDown(NCButton.Reload) && Ammo < MagazineSize;
            bool auto = balance.autoReloadWhenEmpty && Ammo <= 0;

            if (manual || auto)
            {
                ReloadTimer = TickTimer.CreateFromSeconds(Runner, balance.reloadSeconds);
            }
        }

        // ⑤ 발사
        if (input.IsDown(NCButton.Fire) == false || IsReloading || Ammo <= 0)
        {
            return;
        }

        if (FireCooldown.ExpiredOrNotRunning(Runner) == false)
        {
            return;
        }

        bool overcharged = IsOvercharged;
        FireCooldown = TickTimer.CreateFromSeconds(Runner, overcharged ? balance.overchargeFireInterval : balance.fireInterval);

        if (overcharged == false)
        {
            Ammo--;
        }

        if (Object.HasStateAuthority)
        {
            Fire(input.AimPoint);
        }
    }

    private void Fire(Vector3 aimPoint)
    {
        NCBalance balance = NCBalance.Get;
        Vector3 muzzle = _player.MuzzlePosition;

        Vector3 direction = NCMath.Flat(aimPoint - muzzle);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();

        float maxDistance = FindObstacleDistance(muzzle, direction, balance.shotRange);
        NCGhost target = FindFirstGhost(muzzle, direction, maxDistance, balance, out float hitDistance);

        Vector3 end = muzzle + direction * (target != null ? hitDistance : maxDistance);

        if (target != null)
        {
            target.ServerTakeDamage(balance.shotDamage);
        }

        RpcShowShot(muzzle, end, _player.Slot, target != null, IsOvercharged);
    }

    /// <summary>벽·봉쇄 장벽까지의 거리. 플레이어·유령·트리거는 통과.</summary>
    private static float FindObstacleDistance(Vector3 from, Vector3 direction, float range)
    {
        float nearest = range;

        foreach (RaycastHit hit in Physics.RaycastAll(from, direction, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<NCPlayer>() != null || hit.collider.GetComponentInParent<NCGhost>() != null)
            {
                continue;
            }

            nearest = Mathf.Min(nearest, hit.distance);
        }

        return nearest;
    }

    /// <summary>조준선 위의 가장 가까운 유령(높이 무시).</summary>
    private static NCGhost FindFirstGhost(Vector3 muzzle, Vector3 direction, float maxDistance, NCBalance balance, out float hitDistance)
    {
        NCGhost best = null;
        hitDistance = float.MaxValue;

        Vector3 origin = NCMath.Flat(muzzle);
        float width = balance.beamRadius + balance.ghostRadius;

        foreach (NCGhost ghost in NCGhost.All)
        {
            Vector3 to = NCMath.Flat(ghost.transform.position) - origin;
            float along = Vector3.Dot(to, direction);

            if (along < 0f || along > maxDistance)
            {
                continue;
            }

            float side = Vector3.Distance(to, direction * along);
            if (side > width || along >= hitDistance)
            {
                continue;
            }

            hitDistance = along;
            best = ghost;
        }

        return best;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcShowShot(Vector3 from, Vector3 to, int slot, NetworkBool hit, NetworkBool overcharged)
    {
        // 과충전 중에는 구름탄이 하얗게 빛난다.
        Color color = overcharged ? Color.Lerp(Color.white, NCStyle.P1, 0.15f) : NCStyle.SlotColor(slot);
        NCFx.Shot(from, to, color, hit);
    }
}
