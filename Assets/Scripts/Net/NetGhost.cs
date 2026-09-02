using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// [멀티 슬라이스] 유령 5종. 서버(호스트)만 체력·AI를 계산하고, 위치는 NetworkTransform(서버 권위)로 복제.
// 종류(Kind)는 NetworkVariable로 동기화 → 프리팹 1개로 5종을 표현(클라가 Kind 보고 색/크기 적용).
// 총에 맞으면 서버가 체력을 깎고, 0이 되면 공유 점수를 올린 뒤 사라진다.
// ※ 공격(접촉 데미지)·요리사 국물은 다음 단계. 지금은 이동+외형+사망만.
[RequireComponent(typeof(NetworkObject))]
public class NetGhost : NetworkBehaviour
{
    public enum GhostKind { Jelly, Chubby, Slim, Repair, Chef }

    // 서버가 스폰 직후 설정, 모두에게 동기화. 클라는 이걸로 색/크기 적용.
    public NetworkVariable<GhostKind> Kind = new NetworkVariable<GhostKind>();

    // 서버 권위 체력(디버그/체력바용)
    public NetworkVariable<float> Hp = new NetworkVariable<float>();

    [SerializeField] float stopRange      = 1.2f; // 이 거리 안이면 멈춤(기본 추격)
    [SerializeField] float attackRange    = 1.5f; // 접촉 공격 사거리
    [SerializeField] float attackInterval = 1f;   // 접촉 공격 간격(초)

    // 서버 전용 스탯
    private float _maxHp;
    private float _moveSpeed;
    private int   _scoreReward;
    private int   _contactDamage;
    private float _atkTimer;

    // 뚝딱이(Repair) 돌진 상태머신 — 서버 전용
    private enum DashState { Chasing, Dashing, Stunned }
    private DashState _dash = DashState.Chasing;
    private float _dashSpeed, _dashCooldown, _postDashStun;
    private float _cooldownTimer, _stunTimer, _dashLeft;
    private Vector3 _dashDir;

    // 길찾기(NavMesh) — 서버 전용. 스폰 지점이 메시 위일 때만 사용, 아니면 직선 이동 폴백(벽 통과).
    private NavMeshAgent _agent;
    private bool  _useAgent;
    private float _repathTimer;
    private const float RepathInterval = 0.15f; // 목적지 갱신 간격(초)
    private const float NavSampleDist  = 3f;    // 메시 위 지점 탐색 반경

    private GhostKind _pendingKind = GhostKind.Jelly; // 스폰너가 Spawn 전에 지정

    // 스폰너가 Instantiate 직후(Spawn 전) 호출해 종류를 예약한다.
    public void ServerInit(GhostKind kind) => _pendingKind = kind;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            Kind.Value = _pendingKind; // 서버가 종류 확정 → 클라에 동기화
            ServerConfigure();
        }

        ApplyVisual(Kind.Value);                 // 스폰 시점 즉시 반영(색·스케일)
        Kind.OnValueChanged += (_, v) => ApplyVisual(v); // 이후 변경도 반영

        if (IsServer)
        {
            SetupAgentServer(); // 서버만 길찾기 구동(ApplyVisual 뒤 — 스케일 반영된 후 radius 계산)
        }
        else
        {
            // 클라는 위치를 NetworkTransform으로만 받는다 → 혹시 있는 agent는 꺼서 이동 권한 충돌 방지
            var a = GetComponent<NavMeshAgent>();
            if (a != null) a.enabled = false;
        }
    }

    // 서버: 스폰 지점이 NavMesh 위이면 NavMeshAgent로 길찾기, 아니면 직선 이동으로 폴백.
    void SetupAgentServer()
    {
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavSampleDist, NavMesh.AllAreas))
        {
            _useAgent = false; // 메시가 없으면(=Bake 안 함) 기존 직선 이동 유지
            return;
        }
        transform.position = hit.position;

        _agent = GetComponent<NavMeshAgent>();
        if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();

        float s = transform.localScale.y;
        _agent.speed            = _moveSpeed;
        _agent.angularSpeed     = 720f;
        _agent.acceleration     = 20f;
        _agent.stoppingDistance = stopRange;
        _agent.radius           = 0.4f * s;
        _agent.height           = Mathf.Max(0.5f, 2f * s);
        _agent.baseOffset       = s;      // 기본 캡슐(높이2·중심피벗)이 바닥에 서도록 반높이만큼 올림
        _agent.updateRotation   = false;  // 회전은 Face()로 직접
        _agent.updateUpAxis     = false;
        _agent.autoBraking      = true;
        _useAgent = true;
    }

    // 서버: kind별 스탯을 BalanceConfig에서 읽어 세팅
    void ServerConfigure()
    {
        var c = BalanceConfig.Resolve(null);
        switch (Kind.Value)
        {
            case GhostKind.Chubby: _maxHp = G(c?.chubbyHP, 20); _moveSpeed = G(c?.chubbySpeed, 1); _contactDamage = c != null ? c.chubbyDamage : 3; break;
            case GhostKind.Slim:   _maxHp = G(c?.slimHP, 5);    _moveSpeed = G(c?.slimSpeed, 5);   _contactDamage = c != null ? c.slimDamage   : 2; break;
            case GhostKind.Repair: _maxHp = G(c?.repairHP, 50); _moveSpeed = G(c?.repairSpeed, 4); _contactDamage = c != null ? c.repairDamage : 5; break;
            case GhostKind.Chef:   _maxHp = G(c?.chefHP, 40);   _moveSpeed = G(c?.chefSpeed, 2);   _contactDamage = c != null ? c.chefDamage   : 4; break;
            default:               _maxHp = G(c?.jellyHP, 10);  _moveSpeed = G(c?.jellySpeed, 3);  _contactDamage = c != null ? c.jellyDamage  : 1; break;
        }
        _scoreReward = c != null ? c.scorePerGhostKill : 10;
        Hp.Value = _maxHp;

        if (Kind.Value == GhostKind.Repair)
        {
            _dashSpeed    = c != null ? c.repairDashSpeed    : 14f;
            _dashCooldown = c != null ? c.repairDashCooldown : 5f;
            _postDashStun = c != null ? c.repairPostDashStun : 2f;
            _cooldownTimer = _dashCooldown;
        }
    }

    static float G(float? v, float fallback) => v ?? fallback;

    void Update()
    {
        if (!IsServer) return; // 서버만 이동/AI (클라는 NetworkTransform으로 받아 그림)

        NetPlayerController target = FindNearestPlayer();
        if (target == null) return;

        if (Kind.Value == GhostKind.Repair) RepairMove(target.transform);
        else                                ChaseMove(target.transform);

        AttackIfClose(target); // 접촉 공격
    }

    // 사거리 안이면 간격마다 그 플레이어에게 데미지(무적/사망은 NetPlayerHealth가 걸러냄)
    void AttackIfClose(NetPlayerController target)
    {
        _atkTimer -= Time.deltaTime;

        Vector3 to = target.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > attackRange || _atkTimer > 0f) return;

        var hp = target.GetComponent<NetPlayerHealth>();
        if (hp == null)
        {
            Debug.LogWarning("[NetGhost] 플레이어에 NetPlayerHealth 컴포넌트가 없습니다! 플레이어 프리팹에 추가하세요.");
        }
        else
        {
            hp.TakeDamageServer(_contactDamage, transform.position); // 넉백 방향용으로 유령 위치 전달
        }
        _atkTimer = attackInterval;
    }

    // 기본 추격: NavMesh가 있으면 길찾기(벽 우회), 없으면 직선 이동 폴백.
    void ChaseMove(Transform target)
    {
        if (_useAgent && _agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _agent.SetDestination(target.position);
                _repathTimer = RepathInterval;
            }
            Face(FacingDir(target));
            return;
        }

        // 폴백(메시 없음): 직진(벽 통과)
        Vector3 to = target.position - transform.position; to.y = 0f;
        if (to.magnitude > stopRange)
            transform.position += to.normalized * _moveSpeed * Time.deltaTime;
        Face(to);
    }

    // 바라볼 방향: 길찾기로 우회 이동 중이면 실제 이동 방향(agent 속도), 거의 멈췄으면 플레이어 쪽.
    Vector3 FacingDir(Transform target)
    {
        if (_useAgent && _agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            Vector3 v = _agent.velocity; v.y = 0f;
            if (v.sqrMagnitude > 0.05f) return v;
        }
        Vector3 to = target.position - transform.position; to.y = 0f;
        return to;
    }

    // 돌진 등으로 잠시 끈 agent를 메시 위로 복귀시켜 재활성.
    void ReenableAgent()
    {
        if (!_useAgent || _agent == null) return;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavSampleDist, NavMesh.AllAreas))
        {
            _agent.enabled = true;
            _agent.Warp(hit.position);
        }
    }

    // 뚝딱이: 추격 → 쿨다운 후 돌진 → 정지 → 반복
    void RepairMove(Transform target)
    {
        switch (_dash)
        {
            case DashState.Chasing:
                ChaseMove(target);
                _cooldownTimer -= Time.deltaTime;
                if (_cooldownTimer <= 0f)
                {
                    _dashDir = target.position - transform.position; _dashDir.y = 0f;
                    _dashDir = _dashDir.sqrMagnitude > 0.001f ? _dashDir.normalized : transform.forward;
                    _dashLeft = 4f; // 돌진 거리
                    // 돌진은 직선 수동 이동 → 길찾기 agent를 잠시 끈다(둘이 위치를 다투지 않게)
                    if (_agent != null && _agent.enabled) _agent.enabled = false;
                    _dash = DashState.Dashing;
                }
                break;

            case DashState.Dashing:
                float step = _dashSpeed * Time.deltaTime;
                transform.position += _dashDir * step;
                Face(_dashDir);
                _dashLeft -= step;
                if (_dashLeft <= 0f) { _stunTimer = _postDashStun; _dash = DashState.Stunned; }
                break;

            case DashState.Stunned:
                _stunTimer -= Time.deltaTime;
                if (_stunTimer <= 0f)
                {
                    ReenableAgent();               // 메시로 복귀 후 다시 길찾기
                    _cooldownTimer = _dashCooldown;
                    _dash = DashState.Chasing;
                }
                break;
        }
    }

    void Face(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    // 서버에서만 호출(NetShooter의 FireServerRpc 안).
    public void TakeDamageServer(float amount)
    {
        if (!IsServer) return;
        Hp.Value -= amount;
        if (Hp.Value <= 0f)
        {
            if (NetScore.Instance != null) NetScore.Instance.AddScoreServer(_scoreReward);
            NetworkObject.Despawn(true);
        }
    }

    NetPlayerController FindNearestPlayer()
    {
        NetPlayerController best = null;
        float bestSqr = float.MaxValue;
        foreach (var p in FindObjectsByType<NetPlayerController>(FindObjectsSortMode.None))
        {
            float d = (p.transform.position - transform.position).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = p; }
        }
        return best;
    }

    // 모든 클라: kind별 색/크기 적용
    void ApplyVisual(GhostKind kind)
    {
        Color color; float scale;
        switch (kind)
        {
            case GhostKind.Chubby: color = new Color(0.45f, 0.6f, 0.95f); scale = 1.4f;  break; // 파랑
            case GhostKind.Slim:   color = new Color(0.95f, 0.85f, 0.3f); scale = 0.75f; break; // 노랑
            case GhostKind.Repair: color = new Color(0.55f, 0.55f, 0.6f); scale = 1.25f; break; // 회색
            case GhostKind.Chef:   color = new Color(0.9f, 0.4f, 0.35f);  scale = 1.15f; break; // 빨강
            default:               color = new Color(0.55f, 0.85f, 0.65f); scale = 1f;   break; // 연두
        }
        transform.localScale = Vector3.one * scale;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.color = color;
        }
    }
}
