using Fusion;
using UnityEngine;
using UnityEngine.AI;

/// <summary>유령 종류. 기획서 11장 기준 2종만 사용한다.</summary>
public enum GhostType
{
    Basic, // 기본형 — 느리고 단단함. 다방향 압박
    Fast   // 빠른형 — 빠르고 약함. 표적 우선순위를 만듦
}

/// <summary>
/// 유령 AI. 기획서 6장의 **접근 → 공격 준비(전조) → 공격 → 사망** 상태로 행동한다.
///
/// 핵심 규칙(기획서):
///  - 공격 준비에 들어가면 **그 공격의 목표는 바꾸지 않는다**.
///  - 전조 중에는 **멈춰서** 플레이어가 피할 기회를 준다.
///  - 전조가 끝나는 순간 **사거리 안에 남아 있는 목표에만** 1회 피해.
///  - 등장 후 0.7초 동안은 공격하지 않는다.
///
/// AI 계산은 Host(State Authority)만 수행하고, 위치는 NetworkTransform이 복제한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkGhost : NetworkBehaviour
{
    [Header("종류 (Spawn 시 스폰너가 지정)")]
    [SerializeField] private GhostType typeOnPrefab = GhostType.Basic;

    [Header("외형")]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Color basicColor = new Color(0.55f, 0.85f, 0.65f);
    [SerializeField] private Color fastColor = new Color(0.95f, 0.85f, 0.30f);
    [Tooltip("공격 준비(전조) 중에 몸이 바뀌는 색 — 플레이어에게 위험 신호를 준다")]
    [SerializeField] private Color telegraphColor = new Color(1.0f, 0.35f, 0.25f);

    // ───────── 네트워크 공유 상태 ─────────
    [Networked] public GhostType Type { get; set; }
    [Networked] public float Hp { get; set; }

    /// <summary>공격 준비(전조) 중인지. 클라이언트도 알아야 색을 바꿀 수 있어 네트워크로 공유한다.</summary>
    [Networked] public NetworkBool IsTelegraphing { get; set; }

    // 전조가 끝나는 시점
    [Networked] private TickTimer TelegraphTimer { get; set; }
    // 공격 후 다음 준비까지의 최소 간격
    [Networked] private TickTimer AttackCooldown { get; set; }
    // 등장 직후 공격 금지 시간
    [Networked] private TickTimer SpawnSafeTimer { get; set; }
    // 전조에 들어갈 때 고정해 둔 목표(중간에 바꾸지 않는다)
    [Networked] private PlayerRef LockedTarget { get; set; }

    // ───────── Host 전용 스탯 ─────────
    private float _moveSpeed;
    private int _damage;
    private float _telegraphSeconds;
    private float _attackInterval;
    private float _attackRange = 1.0f;

    private NavMeshAgent _agent;
    private bool _useAgent;
    private bool _visualApplied;

    /// <summary>스폰너가 Runner.Spawn 직후 종류를 정할 때 호출한다(Host 전용).</summary>
    public void ServerSetType(GhostType type)
    {
        if (Object.HasStateAuthority)
        {
            Type = type;
            ApplyStats();
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            // 스폰너가 따로 지정하지 않았다면 프리팹 기본값을 쓴다.
            if (Type == default && typeOnPrefab != default)
            {
                Type = typeOnPrefab;
            }

            ApplyStats();
            SetupAgent();

            CleanerBalance balance = CleanerBalance.Resolve(null);
            float safeSeconds = balance != null ? balance.ghostSpawnSafeSeconds : 0.7f;
            SpawnSafeTimer = TickTimer.CreateFromSeconds(Runner, safeSeconds);
        }
        else
        {
            // 클라이언트는 AI를 돌리지 않는다. agent가 NetworkTransform과 다투지 않도록 끈다.
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        ApplyVisual();
    }

    /// <summary>종류별 스탯을 CleanerBalance에서 읽는다(Host 전용).</summary>
    private void ApplyStats()
    {
        CleanerBalance balance = CleanerBalance.Resolve(null);

        if (balance == null)
        {
            return;
        }

        if (Type == GhostType.Fast)
        {
            Hp = balance.fastHp;
            _moveSpeed = balance.fastMoveSpeed;
            _damage = balance.fastDamage;
            _telegraphSeconds = balance.fastTelegraphSeconds;
            _attackInterval = balance.fastAttackInterval;
        }
        else
        {
            Hp = balance.basicHp;
            _moveSpeed = balance.basicMoveSpeed;
            _damage = balance.basicDamage;
            _telegraphSeconds = balance.basicTelegraphSeconds;
            _attackInterval = balance.basicAttackInterval;
        }

        _attackRange = balance.ghostAttackRange;

        if (_agent != null)
        {
            _agent.speed = _moveSpeed;
        }
    }

    /// <summary>NavMesh 위에 있으면 길찾기를 쓰고, 아니면 직진 추격으로 폴백한다.</summary>
    private void SetupAgent()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas) == false)
        {
            _useAgent = false;

            // 조용히 이상하게 동작하지 않도록 알린다.
            // (NavMesh가 바닥이 아니라 천장에만 구워졌을 때 여기로 빠진다)
            Debug.LogWarning(
                $"[NetworkGhost] {transform.position} 주변에서 NavMesh를 찾지 못해 직진 모드로 동작합니다. " +
                "맵 바닥에 NavMesh가 구워졌는지 확인하세요.");
            return;
        }

        transform.position = hit.position;

        _agent = GetComponent<NavMeshAgent>();
        if (_agent == null)
        {
            _agent = gameObject.AddComponent<NavMeshAgent>();
        }

        _agent.speed = _moveSpeed;
        _agent.angularSpeed = 720f;
        _agent.acceleration = 20f;
        _agent.stoppingDistance = _attackRange * 0.8f;
        _agent.updateRotation = false; // 회전은 직접 처리
        _agent.updateUpAxis = false;
        _agent.autoBraking = true;
        _useAgent = true;
    }

    public override void FixedUpdateNetwork()
    {
        // AI는 Host만 계산한다.
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        // ── 공격 준비(전조) 중 ──
        if (IsTelegraphing)
        {
            StopMoving();

            if (TelegraphTimer.Expired(Runner))
            {
                IsTelegraphing = false;
                AttackCooldown = TickTimer.CreateFromSeconds(Runner, _attackInterval);
                ResolveAttack(); // 사거리 안에 남아 있으면 피해
            }

            return;
        }

        // ── 추격 ──
        NetworkObject target = FindNearestAlivePlayer();

        if (target == null)
        {
            StopMoving();
            return;
        }

        MoveToward(target.transform.position);

        // ── 공격 준비 진입 판정 ──
        bool canAttack =
            SpawnSafeTimer.ExpiredOrNotRunning(Runner) &&
            AttackCooldown.ExpiredOrNotRunning(Runner) &&
            IsWithinAttackRange(target.transform.position);

        if (canAttack)
        {
            // 여기서 목표를 고정한다. 전조 중에는 바꾸지 않는다.
            LockedTarget = target.InputAuthority;
            IsTelegraphing = true;
            TelegraphTimer = TickTimer.CreateFromSeconds(Runner, _telegraphSeconds);
            StopMoving();
        }
    }

    /// <summary>전조가 끝난 순간, 고정한 목표가 아직 사거리 안이면 1회 피해를 준다.</summary>
    private void ResolveAttack()
    {
        if (Runner.TryGetPlayerObject(LockedTarget, out NetworkObject targetObject) == false)
        {
            return;
        }

        if (IsWithinAttackRange(targetObject.transform.position) == false)
        {
            return; // 플레이어가 전조를 보고 피했다 → 빗나감
        }

        NetworkPlayerHealth health = targetObject.GetComponent<NetworkPlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(_damage);
        }
    }

    /// <summary>총에 맞았을 때 Host가 호출한다.</summary>
    public void TakeDamage(float amount)
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        Hp -= amount;

        if (Hp <= 0f)
        {
            // TODO(다음 단계): 방 완료 판정을 위해 스폰너/전투방에 사망을 알린다.
            Runner.Despawn(Object);
        }
    }

    // ───────── 이동 보조 ─────────

    private void MoveToward(Vector3 destination)
    {
        Vector3 toTarget = destination - transform.position;
        toTarget.y = 0f;

        if (_useAgent && _agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.SetDestination(destination);

            // 실제 이동 방향을 보게 해서 옆으로 미끄러지는 느낌을 줄인다.
            Vector3 velocity = _agent.velocity;
            velocity.y = 0f;
            Face(velocity.sqrMagnitude > 0.05f ? velocity : toTarget);
            return;
        }

        // NavMesh가 없을 때의 폴백: 직진 + 지면 스냅
        if (toTarget.magnitude > _attackRange)
        {
            transform.position += toTarget.normalized * _moveSpeed * Runner.DeltaTime;
        }

        SnapToGround();
        Face(toTarget);
    }

    /// <summary>
    /// 폴백 모드에서 공중에 뜨거나 바닥에 박히지 않도록 아래로 광선을 쏴 지면에 붙인다.
    /// (NavMesh가 정상이면 agent가 알아서 처리하므로 이 함수는 쓰이지 않는다)
    /// </summary>
    private void SnapToGround()
    {
        // 발보다 조금 위에서 아래로 쏜다.
        Vector3 origin = transform.position + Vector3.up * 1.0f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            // 자기 자신이나 다른 유령은 바닥으로 치지 않는다.
            if (hit.collider.GetComponentInParent<NetworkGhost>() == null)
            {
                Vector3 position = transform.position;
                position.y = hit.point.y;
                transform.position = position;
            }
        }
    }

    private void StopMoving()
    {
        if (_useAgent && _agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
        }
    }

    private void Face(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private bool IsWithinAttackRange(Vector3 position)
    {
        Vector3 to = position - transform.position;
        to.y = 0f;
        return to.magnitude <= _attackRange;
    }

    /// <summary>세션에 접속한 플레이어 중 다운되지 않은 가장 가까운 대상을 찾는다.</summary>
    private NetworkObject FindNearestAlivePlayer()
    {
        NetworkObject nearest = null;
        float nearestSqr = float.MaxValue;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerObject) == false)
            {
                continue;
            }

            // 다운된 플레이어는 쫓지 않는다.
            NetworkPlayerHealth health = playerObject.GetComponent<NetworkPlayerHealth>();
            if (health != null && health.IsDown)
            {
                continue;
            }

            float sqr = (playerObject.transform.position - transform.position).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = playerObject;
            }
        }

        return nearest;
    }

    // ───────── 외형 ─────────

    private void Update()
    {
        // 네트워크에 등록되기 전에는 [Networked] 값을 읽을 수 없다.
        if (Object == null || Object.IsValid == false)
        {
            return;
        }

        // 전조 상태가 바뀌면 모든 화면에서 색을 갱신한다(순수 표현).
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<Renderer>();
        }

        if (bodyRenderer == null)
        {
            return;
        }

        Color target = IsTelegraphing
            ? telegraphColor
            : (Type == GhostType.Fast ? fastColor : basicColor);

        if (_visualApplied && bodyRenderer.material.color == target)
        {
            return;
        }

        bodyRenderer.material.color = target;
        _visualApplied = true;
    }
}
