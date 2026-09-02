using UnityEngine;
using UnityEngine.AI;

// 뚝딱이 — 특별★. 창고 출몰. HP 매우 높음.
// [패턴]: 기본 이동 > 플레이어 방향 4칸 돌진 > 2초 정지 > 기본 이동 반복
//
// [수정 이력]
// - Start()를 override로 변경 + base.Start() 호출 → 플레이어 참조·물리 초기화 복구
// - _myAgent 제거 → GhostBase.protected _agent 직접 사용 (중복 GetComponent 제거)
// - SuppressChase 오버라이드: Chasing 상태일 때만 false → 돌진·정지 중 SetDestination 간섭 차단
public class GhostRepair : GhostBase
{
    protected override Color BodyColor => new Color(0.55f, 0.55f, 0.6f, 1f); // 금속 회색
    protected override float BodyScale => 1.25f;
    protected override GhostVisualId VisualId => GhostVisualId.Repair;
    protected override string DefaultVisualResourcePath => "Ghost/all ghost_01";
    protected override float ColliderRadius => 0.5f;
    protected override float ColliderHeight => 1.3f;
    protected override bool IsSpecial => true;

    // --- BalanceConfig에서 가져올 돌진 스탯 ---
    private float _dashSpeed;
    private float _dashCooldown;
    private float _postDashStun;

    // --- 패턴 상태 머신 ---
    private enum DashState { Chasing, Dashing, Stunned }
    private DashState _dashState = DashState.Chasing;

    private float _cooldownTimer;
    private float _stunTimer;
    private Vector3 _dashDirection;
    private float _dashDistanceLeft;

    // [수정] SuppressChase: Chasing 상태가 아니면 GhostBase의 SetDestination 루프를 막는다.
    // Dashing·Stunned 중 SetDestination이 호출되면 에이전트가 velocity를 재계산해
    // 돌진 방향이 플레이어 추적 방향으로 왜곡되거나 정지가 풀리는 문제가 있었다.
    protected override bool SuppressChase => _dashState != DashState.Chasing;

    protected override void Configure(BalanceConfig cfg)
    {
        _maxHP = cfg.repairHP;
        _moveSpeed = cfg.repairSpeed;
        _contactDamage = cfg.repairDamage;

        _dashSpeed = cfg.repairDashSpeed;
        _dashCooldown = cfg.repairDashCooldown;
        _postDashStun = cfg.repairPostDashStun;
    }

    // [수정] private void Start() → protected override void Start()
    // base.Start()를 호출하지 않으면 _playerTf / _playerStats / _vacuum 등
    // GhostBase의 핵심 참조가 전부 null로 남아 유령이 완전히 먹통이 된다.
    protected override void Start()
    {
        base.Start(); // ← GhostBase 초기화 (플레이어 참조, 물리 세팅 등) 반드시 먼저

        _cooldownTimer = _dashCooldown;
        // [수정] GhostBase._agent(protected)를 그대로 사용 — 별도 GetComponent 불필요
    }

    // GhostBase.Update() 내부에서 흡입 중이 아닐 때만 매 프레임 호출됨
    protected override void OnTick()
    {
        switch (_dashState)
        {
            case DashState.Chasing: UpdateChasing(); break;
            case DashState.Dashing: UpdateDashing(); break;
            case DashState.Stunned: UpdateStunned(); break;
        }
    }

    // [상태 1] 기본 추격 — GhostBase의 SetDestination이 이미 이동을 처리하므로
    //           여기서는 속도 복원과 쿨다운 감소만 담당한다.
    private void UpdateChasing()
    {
        if (_agent != null && _agent.enabled)
            _agent.speed = _moveSpeed;

        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer <= 0f)
            StartDash();
    }

    private void StartDash()
    {
        Transform playerTf = FindFirstObjectByType<PlayerStats>()?.transform;
        if (playerTf == null) return;

        _dashDirection = playerTf.position - transform.position;
        _dashDirection.y = 0f;
        if (_dashDirection.sqrMagnitude < 0.001f) _dashDirection = transform.forward;
        _dashDirection.Normalize();

        _dashDistanceLeft = BodyScale * 4f;
        _dashState = DashState.Dashing;
        // SuppressChase가 즉시 true가 되므로 이 프레임부터 SetDestination 간섭 없음
    }

    // [상태 2] 돌진 — SuppressChase=true이므로 GhostBase의 SetDestination은 호출되지 않는다.
    //           에이전트 velocity를 직접 주입해 원하는 방향·속도로 이동.
    private void UpdateDashing()
    {
        float distThisFrame = _dashSpeed * Time.deltaTime;

        if (_dashDistanceLeft <= distThisFrame)
        {
            EndDash();
            return;
        }

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.velocity = _dashDirection * _dashSpeed;
        }
        else
        {
            // navmesh 폴백: transform 직접 이동
            transform.position += _dashDirection * distThisFrame;
        }

        _dashDistanceLeft -= distThisFrame;
    }

    private void EndDash()
    {
        // velocity 잔량 제거 — 관성으로 계속 미끄러지는 것 방지
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.velocity = Vector3.zero;

        _stunTimer = _postDashStun;
        _dashState = DashState.Stunned;
    }

    // [상태 3] 정지 — SuppressChase=true이므로 SetDestination은 호출되지 않는다.
    //           매 프레임 velocity=0을 강제해 에이전트 내부 관성도 차단.
    private void UpdateStunned()
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.velocity = Vector3.zero;

        _stunTimer -= Time.deltaTime;
        if (_stunTimer <= 0f)
        {
            _cooldownTimer = _dashCooldown;
            _dashState = DashState.Chasing;
            // SuppressChase가 false로 돌아오므로 다음 프레임부터 SetDestination 재개
        }
    }

    protected override void OnConsumeBegin()
    {
        // TODO: 아이템 드랍
    }
}
