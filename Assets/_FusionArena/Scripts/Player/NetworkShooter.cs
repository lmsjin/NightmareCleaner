using Fusion;
using UnityEngine;

/// <summary>
/// 좌클릭 홀드로 연사하는 히트스캔(즉발) 사격을 처리한다.
///
/// 교안 6주차는 "날아가는 투사체" 방식이지만, 우리 게임은 총알이 즉시 도달하는
/// **히트스캔** 설계라 방식이 다르다(틀린 게 아니라 게임 디자인 차이).
///
/// 흐름:
///   NetworkInputProvider가 수집한 Fire/AimPoint
///     → FixedUpdateNetwork에서 GetInput으로 꺼냄
///     → Host(State Authority)가 명중 판정
///     → RPC로 모든 화면에 트레이서 연출
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkShooter : NetworkBehaviour
{
    [Header("사격 설정 (Spawned에서 BalanceConfig 값으로 덮어씀)")]
    [SerializeField]
    private float damage = 10.0f;        // 발당 데미지. 유령이 생기는 M4에서 사용한다.

    [SerializeField]
    private float range = 12.0f;         // 사거리(미터)

    [SerializeField]
    private float fireInterval = 0.15f;  // 연사 간격(초)

    [SerializeField]
    private int magazineSize = 12;       // 탄창 크기(예비 탄약은 무한)

    [SerializeField]
    private float reloadSeconds = 1.8f;  // 재장전 시간(초)

    [SerializeField]
    private float muzzleHeight = 1.0f;   // 총구 높이. 조준 평면 높이와 맞춰야 선이 수평이 된다.

    [Header("명중 판정 두께")]
    [SerializeField]
    private float beamRadius = 0.3f;     // 조준선 자체의 두께(관대하게 잡아 조작감을 확보)

    [SerializeField]
    private float ghostRadius = 0.5f;    // 유령 몸 반경(판정 여유)

    /// <summary>
    /// 발당 데미지. 다음 단계(M4 유령)에서 명중 처리에 사용한다.
    /// </summary>
    public float Damage => damage;

    // 연사 간격을 재는 Fusion 전용 타이머다.
    // [Networked]로 선언해야 Host와 클라이언트가 같은 기준으로 계산한다.
    [Networked]
    private TickTimer FireCooldown { get; set; }

    /// <summary>현재 탄창에 남은 탄 수.</summary>
    [Networked] public int Ammo { get; set; }

    // 재장전 진행 타이머. 재장전 중이 아니면 TickTimer.None이다.
    [Networked] private TickTimer ReloadTimer { get; set; }

    /// <summary>재장전 중인지. 재장전 중에는 사격할 수 없다.</summary>
    public bool IsReloading => ReloadTimer.IsRunning;

    /// <summary>탄창 크기(HUD 표시용).</summary>
    public int MagazineSize => magazineSize;

    // 다운 상태 확인용
    private NetworkPlayerHealth _health;

    /// <summary>
    /// 네트워크에 Spawn된 직후 한 번 호출된다.
    /// 수치는 코드에 박지 않고 CleanerBalance에서 읽는다.
    /// </summary>
    public override void Spawned()
    {
        _health = GetComponent<NetworkPlayerHealth>();

        // 2학기 기획서 수치는 CleanerBalance 한 곳에서 관리한다.
        CleanerBalance balance = CleanerBalance.Resolve(null);

        if (balance != null)
        {
            damage = balance.shotDamage;
            range = balance.shotRange;
            fireInterval = balance.fireInterval;
            magazineSize = balance.magazineSize;
            reloadSeconds = balance.reloadSeconds;
        }

        // 처음엔 탄창이 가득 찬 상태로 시작한다. 값은 Host만 정한다.
        if (Object.HasStateAuthority)
        {
            Ammo = magazineSize;
        }
    }

    /// <summary>
    /// Fusion의 네트워크 Tick마다 호출된다. 발사·재장전 판정은 여기에서만 한다.
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        // ① 재장전이 끝났으면 탄창을 채운다(입력과 무관하게 먼저 처리).
        if (ReloadTimer.IsRunning && ReloadTimer.Expired(Runner))
        {
            Ammo = magazineSize;
            ReloadTimer = TickTimer.None;
        }

        // ② 다운된 동안은 사격·재장전 모두 불가(기획서 7장). 진행 중이던 재장전은 취소한다.
        if (_health != null && _health.IsDown)
        {
            ReloadTimer = TickTimer.None;
            return;
        }

        // 이번 Tick에 이 플레이어에게 배정된 입력을 꺼낸다.
        if (GetInput(out NetworkInputData data) == false)
        {
            return;
        }

        // ③ 탄창이 비어 있으면 자동 재장전(구조 직후처럼 0발로 남은 경우 포함).
        //    R을 누르면 탄이 남아 있어도 재장전한다.
        if (IsReloading == false)
        {
            bool empty = Ammo <= 0;
            bool manual = data.Reload && Ammo < magazineSize;

            if (empty || manual)
            {
                StartReload();
            }
        }

        // 좌클릭을 누르지 않았거나 재장전 중이면 발사하지 않는다.
        if (data.Fire == false || IsReloading)
        {
            return;
        }

        // 아직 연사 쿨다운이 남아 있으면 발사하지 않는다.
        if (FireCooldown.ExpiredOrNotRunning(Runner) == false)
        {
            return;
        }

        // 다음 발사까지의 쿨다운을 걸고 탄을 하나 쓴다.
        FireCooldown = TickTimer.CreateFromSeconds(Runner, fireInterval);
        Ammo--;

        // 마지막 탄을 쏘면 바로 자동 재장전을 시작한다.
        if (Ammo <= 0)
        {
            StartReload();
        }

        // 명중 판정과 데미지는 권한을 가진 Host만 수행한다(서버 권위).
        // 클라이언트는 위의 쿨다운까지만 예측하고 판정은 하지 않는다.
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        Fire(data.AimPoint);
    }

    private void StartReload()
    {
        ReloadTimer = TickTimer.CreateFromSeconds(Runner, reloadSeconds);
    }

    /// <summary>
    /// 탄창을 즉시 가득 채우고 진행 중인 재장전을 취소한다. **Host에서만 호출.**
    /// Q 과충전 발동 시 사용할 예정(기획서 4장).
    /// </summary>
    public void ServerRefillMagazine()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        ReloadTimer = TickTimer.None;
        Ammo = magazineSize;
    }

    /// <summary>
    /// Host에서만 실행되는 실제 발사 처리.
    /// </summary>
    private void Fire(Vector3 aimPoint)
    {
        // 총구 위치: 발밑이 아니라 몸통 높이에서 나가야 선이 바닥에 박히지 않는다.
        Vector3 muzzle = transform.position + Vector3.up * muzzleHeight;

        // 조준 지점까지의 수평 방향을 구한다.
        Vector3 direction = aimPoint - muzzle;
        direction.y = 0.0f;

        // 방향을 못 구하면(조준점이 총구와 겹침) 바라보는 방향으로 쏜다.
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();

        // 벽에 막히면 그 지점까지, 아니면 최대 사거리까지 선이 나간다.
        Vector3 wallPoint = FindWallHit(muzzle, direction);

        // 벽까지의 거리 안에서만 유령을 찾는다(벽 뒤의 유령은 맞지 않는다).
        float maxDistance = Vector3.Distance(muzzle, wallPoint);
        Vector3 hitPoint = wallPoint;

        NetworkGhost ghost = FindFirstGhost(muzzle, direction, maxDistance, out Vector3 ghostPoint);

        if (ghost != null)
        {
            // 기획서: 탄환은 "처음 맞은 유령"에게만 피해를 주고 사라진다.
            hitPoint = ghostPoint;
            ghost.TakeDamage(damage);
        }

        // 모든 화면에 트레이서를 보여준다.
        RpcShowTracer(muzzle, hitPoint);
    }

    /// <summary>
    /// 총구에서 쏜 광선이 처음 부딪히는 벽/지형 지점을 찾는다.
    /// 플레이어(자기 자신과 팀원)는 통과시킨다.
    /// </summary>
    /// <returns>막힌 지점. 아무것도 없으면 최대 사거리 지점.</returns>
    private Vector3 FindWallHit(Vector3 from, Vector3 direction)
    {
        // 기본값은 "아무것도 안 맞고 끝까지 날아간 경우"다.
        Vector3 result = from + direction * range;

        RaycastHit[] hits = Physics.RaycastAll(
            from,
            direction,
            range,
            ~0,
            QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            // 플레이어는 벽이 아니다. 내 캐릭터와 팀원 몸을 통과시킨다.
            if (hit.collider.GetComponentInParent<NetworkPlayerMovement>() != null)
            {
                continue;
            }

            // 더 가까운 장애물을 찾으면 갱신한다.
            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                result = hit.point;
            }
        }

        return result;
    }

    /// <summary>
    /// 조준선 위에서 가장 가까운 유령을 찾는다.
    ///
    /// 높이(Y)를 무시하고 바닥 평면(XZ)에서만 판정한다.
    /// 플레이어와 유령의 모델 높이가 달라도 "보이는 대로" 맞게 하기 위함이다.
    /// </summary>
    /// <param name="hitPoint">명중 지점(총구 높이). 못 찾으면 사용하지 않는다.</param>
    private NetworkGhost FindFirstGhost(Vector3 muzzle, Vector3 direction, float maxDistance, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;

        NetworkGhost nearest = null;
        float nearestAlong = float.MaxValue;

        // 총구와 방향을 바닥 평면에 투영한다.
        Vector3 origin = new Vector3(muzzle.x, 0f, muzzle.z);
        Vector3 flatDir = new Vector3(direction.x, 0f, direction.z).normalized;

        foreach (NetworkGhost ghost in FindObjectsByType<NetworkGhost>(FindObjectsSortMode.None))
        {
            Vector3 ghostFlat = new Vector3(ghost.transform.position.x, 0f, ghost.transform.position.z);
            Vector3 toGhost = ghostFlat - origin;

            // 조준선을 따라 얼마나 앞에 있는지
            float along = Vector3.Dot(toGhost, flatDir);

            // 뒤에 있거나 벽/사거리 너머면 제외
            if (along < 0f || along > maxDistance)
            {
                continue;
            }

            // 조준선에서 옆으로 얼마나 떨어져 있는지
            float perpendicular = Vector3.Distance(ghostFlat, origin + flatDir * along);

            if (perpendicular > beamRadius + ghostRadius)
            {
                continue;
            }

            // 더 가까운 유령을 선택한다(처음 맞은 하나만 피해).
            if (along < nearestAlong)
            {
                nearestAlong = along;
                nearest = ghost;
            }
        }

        if (nearest != null)
        {
            // 트레이서가 유령 몸통 높이에서 끝나도록 총구 높이를 유지한다.
            hitPoint = new Vector3(nearest.transform.position.x, muzzle.y, nearest.transform.position.z);
        }

        return nearest;
    }

    /// <summary>
    /// Host → 모든 참가자. 트레이서 연출만 재생한다(게임 상태 변경 없음).
    /// Fusion 규칙상 RPC 메서드 이름은 Rpc로 시작해야 한다.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcShowTracer(Vector3 from, Vector3 to)
    {
        FusionTracer.Spawn(from, to);
    }

    /// <summary>
    /// 내 화면에만 탄창 상태를 임시로 표시한다(정식 HUD는 UI 담당이 제작).
    /// </summary>
    private void OnGUI()
    {
        if (Object == null || Object.IsValid == false || Object.HasInputAuthority == false)
        {
            return;
        }

        string text = IsReloading
            ? "재장전 중..."
            : $"탄창 {Ammo} / {magazineSize}";

        GUI.Label(new Rect(10, 34, 320, 24), text);
    }
}
