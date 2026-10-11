using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

/// <summary>유령 종류(통합 기획서 기준 2종).</summary>
public enum NCGhostType
{
    Basic, // 기본형 — 느리고 단단함
    Fast,  // 빠른형 — 빠르고 약함
}

/// <summary>
/// 유령 AI: 접근 → 공격 준비(전조) → 공격 → 다시 접근. 체력 0이면 바로 사라진다.
///  - 전조에 들어가면 그 공격의 목표를 바꾸지 않고, 멈춰서 몸이 붉어진다(회피 기회).
///  - 전조가 끝나는 순간 목표가 사거리 안에 남아 있을 때만 1회 피해.
///  - 등장 직후 0.7초는 공격하지 않는다. 다운된 플레이어는 쫓지 않는다.
/// AI와 길찾기(NavMeshAgent)는 Host만 돌리고, 위치는 NetworkTransform이 모든 화면에 복제한다.
/// </summary>
public class NCGhost : NetworkBehaviour
{
    public static readonly List<NCGhost> All = new List<NCGhost>();

    [SerializeField] private Renderer bodyRenderer;

    [Networked] public NCGhostType Type { get; set; }
    [Networked] public float Hp { get; set; }
    [Networked] public NetworkBool IsTelegraphing { get; set; }
    [Networked] private TickTimer TelegraphTimer { get; set; }
    [Networked] private TickTimer AttackCooldown { get; set; }
    [Networked] private TickTimer SpawnSafeTimer { get; set; }
    [Networked] private PlayerRef LockedTarget { get; set; }

    private NavMeshAgent _agent;

    // 연출용
    private float _seenHp;
    private float _flashTimer;
    private Vector3 _bodyScale = Vector3.one;

    /// <summary>모든 유령 제거(결과·재도전). Host 전용.</summary>
    public static void ServerDespawnAll(NetworkRunner runner)
    {
        foreach (NCGhost ghost in All.ToArray())
        {
            if (ghost.Object != null && ghost.Object.IsValid)
            {
                runner.Despawn(ghost.Object);
            }
        }
    }

    public override void Spawned()
    {
        All.Add(this);

        Stats stats = GetStats(Type);

        // 빠른형은 작게 보인다(몸통 자식의 크기만 바꾼다).
        if (bodyRenderer != null)
        {
            _bodyScale = bodyRenderer.transform.localScale * (Type == NCGhostType.Fast ? 0.8f : 1f);
            bodyRenderer.transform.localScale = _bodyScale;
        }

        if (Object.HasStateAuthority)
        {
            Hp = stats.Hp;
            SpawnSafeTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.ghostSpawnSafeSeconds);
            SetupAgent(stats);
        }

        _seenHp = Hp;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        All.Remove(this);

        // 상태를 읽을 수 없는 경우(세션 종료 등)엔 [Networked] 값을 건드리지 않는다.
        Color color = hasState ? BodyColor() : NCStyle.GhostBasic;
        NCFx.GhostPop(transform.position + Vector3.up * 0.9f, color);
    }

    private void SetupAgent(Stats stats)
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas) == false)
        {
            Debug.LogWarning($"[NCGhost] {transform.position} 근처에 NavMesh가 없습니다.");
            return;
        }

        _agent = gameObject.AddComponent<NavMeshAgent>();
        _agent.Warp(hit.position);
        _agent.speed = stats.Speed;
        _agent.acceleration = 30f;
        _agent.angularSpeed = 720f;
        _agent.radius = 0.35f;
        _agent.height = 1.6f;
        _agent.stoppingDistance = NCBalance.Get.ghostAttackRange * 0.7f;
        _agent.updateRotation = false;
        _agent.autoBraking = true;
    }

    // ───────── Host AI ─────────

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        NCBalance balance = NCBalance.Get;
        Stats stats = GetStats(Type);

        if (IsTelegraphing)
        {
            Stop();

            if (TelegraphTimer.Expired(Runner))
            {
                IsTelegraphing = false;
                AttackCooldown = TickTimer.CreateFromSeconds(Runner, stats.AttackInterval);
                ResolveAttack(stats, balance);
            }

            return;
        }

        NCPlayer target = FindNearestAlivePlayer();
        if (target == null)
        {
            Stop();
            return;
        }

        float distance = NCMath.FlatDistance(target.transform.position, transform.position);
        Chase(target.transform.position);

        bool ready = SpawnSafeTimer.ExpiredOrNotRunning(Runner) && AttackCooldown.ExpiredOrNotRunning(Runner);
        if (ready && distance <= balance.ghostAttackRange)
        {
            LockedTarget = target.Object.InputAuthority;
            IsTelegraphing = true;
            TelegraphTimer = TickTimer.CreateFromSeconds(Runner, stats.Telegraph);
            Face(target.transform.position - transform.position);
            Stop();
        }
    }

    private void ResolveAttack(Stats stats, NCBalance balance)
    {
        foreach (NCPlayer player in NCPlayer.All)
        {
            if (player.Object.InputAuthority != LockedTarget)
            {
                continue;
            }

            // 전조를 보고 피했다면 빗나간다.
            if (NCMath.FlatDistance(player.transform.position, transform.position) <= balance.ghostAttackRange)
            {
                player.Health.TakeDamage(stats.Damage);
            }

            return;
        }
    }

    /// <summary>총에 맞았을 때. Host 전용.</summary>
    public void ServerTakeDamage(float amount)
    {
        if (Object.HasStateAuthority == false || Hp <= 0f)
        {
            return;
        }

        Hp -= amount;

        if (Hp <= 0f)
        {
            Runner.Despawn(Object);
        }
    }

    private void Chase(Vector3 destination)
    {
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.SetDestination(destination);

            Vector3 velocity = NCMath.Flat(_agent.velocity);
            Face(velocity.sqrMagnitude > 0.05f ? velocity : destination - transform.position);
        }
    }

    private void Stop()
    {
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }
    }

    private void Face(Vector3 direction)
    {
        direction = NCMath.Flat(direction);
        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private NCPlayer FindNearestAlivePlayer()
    {
        NCPlayer nearest = null;
        float best = float.MaxValue;

        foreach (NCPlayer player in NCPlayer.All)
        {
            if (player.Health.IsDown)
            {
                continue;
            }

            float distance = NCMath.FlatDistance(player.transform.position, transform.position);
            if (distance < best)
            {
                best = distance;
                nearest = player;
            }
        }

        return nearest;
    }

    // ───────── 수치 ─────────

    private struct Stats
    {
        public float Hp;
        public float Speed;
        public int Damage;
        public float Telegraph;
        public float AttackInterval;
    }

    private static Stats GetStats(NCGhostType type)
    {
        NCBalance b = NCBalance.Get;

        return type == NCGhostType.Fast
            ? new Stats { Hp = b.fastHp, Speed = b.fastMoveSpeed, Damage = b.fastDamage, Telegraph = b.fastTelegraphSeconds, AttackInterval = b.fastAttackInterval }
            : new Stats { Hp = b.basicHp, Speed = b.basicMoveSpeed, Damage = b.basicDamage, Telegraph = b.basicTelegraphSeconds, AttackInterval = b.basicAttackInterval };
    }

    // ───────── 연출 (모든 화면) ─────────

    private Color BodyColor() => Type == NCGhostType.Fast ? NCStyle.GhostFast : NCStyle.GhostBasic;

    public override void Render()
    {
        if (Hp < _seenHp)
        {
            _flashTimer = 0.1f;
        }

        _seenHp = Hp;
        _flashTimer -= Time.deltaTime;

        Color color = BodyColor();
        float pulse = 1f;

        if (IsTelegraphing)
        {
            // 공격 준비: 붉게 깜빡이며 살짝 부풀어 오른다.
            float t = Mathf.PingPong(Time.time * 8f, 1f);
            color = Color.Lerp(NCStyle.GhostTelegraph, Color.white, t * 0.25f);
            pulse = 1f + 0.12f * t;
        }

        if (_flashTimer > 0f)
        {
            color = Color.white;
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = color;
        }

        // 둥실둥실 떠 있는 느낌 + 전조 때 부풀기(몸통 자식만 — 루트는 NetworkTransform이 다룬다)
        if (bodyRenderer != null)
        {
            Transform body = bodyRenderer.transform;
            body.localPosition = new Vector3(0f, 0.9f + Mathf.Sin(Time.time * 3f + GetInstanceID()) * 0.08f, 0f);
            body.localScale = _bodyScale * pulse;
        }
    }
}
