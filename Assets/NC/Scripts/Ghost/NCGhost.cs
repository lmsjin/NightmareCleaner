using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public enum NCGhostType
{
    Basic,
    Fast,
    Tank,
}

public enum NCGhostState
{
    Idle,
    Chase,
    Attack,
    Return,
}

/// <summary>
/// 중립 유령 FSM: 대기 → 추적 → 공격(준비) → 복귀. 판단은 Host만(NCSession이 순서대로 호출).
/// 맞으면 쏜 플레이어를 추적하고, 이때는 활동 영역 밖으로 조금 더 쫓아간다.
/// 활동 영역을 벗어나거나 목표를 잃으면 복귀하며, 복귀 중에는 공격하지도 맞지도 않고 도착하면 회복한다.
/// 위치는 NetworkTransform이 복제한다.
/// </summary>
public class NCGhost : NetworkBehaviour
{
    public static readonly List<NCGhost> All = new List<NCGhost>();

    private const float ReturnTimeout = 8f;

    [SerializeField] private Renderer bodyRenderer;

    [Networked] public NCGhostType Type { get; set; }
    [Networked] public NCGhostState State { get; set; }
    [Networked] public float Hp { get; set; }
    [Networked] private TickTimer TelegraphTimer { get; set; }
    [Networked] private TickTimer AttackCooldown { get; set; }
    [Networked] private TickTimer ReturnTimer { get; set; }

    // Host 전용
    private NavMeshAgent _agent;
    private NCGhostCamp _camp;
    private Vector3 _home;
    private NCPlayer _target;
    private bool _provoked; // 맞아서 _target을 쫓는 중

    // 연출
    private Color _shownColor = new Color(-1f, -1f, -1f, -1f);
    private float _seenHp;
    private float _flashTimer;
    private Vector3 _bodyScale = Vector3.one;

    public float HpRatio => Mathf.Clamp01(Hp / Stats.hp);

    /// <summary>체력바를 띄울 높이(발에서부터, 몸의 흔들림은 무시).</summary>
    public float TopHeight => 0.9f * Mathf.Max(1f, Stats.bodyScale) + 0.9f * Stats.bodyScale + 0.35f;

    private NCBalance.GhostStats Stats => NCBalance.Get.GetGhost(Type);

    public void ServerInit(NCGhostCamp camp, Vector3 home)
    {
        _camp = camp;
        _home = home;
    }

    public static void ServerTickAll()
    {
        foreach (NCGhost ghost in All)
        {
            ghost.ServerTick();
        }
    }

    public static void ServerStopAll()
    {
        foreach (NCGhost ghost in All)
        {
            ghost.Stop();
            ghost.State = NCGhostState.Idle;
            ghost._target = null;
            ghost._provoked = false;
        }
    }

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

        if (bodyRenderer != null)
        {
            _bodyScale = bodyRenderer.transform.localScale * Stats.bodyScale;
            bodyRenderer.transform.localScale = _bodyScale;
        }

        if (Object.HasStateAuthority)
        {
            Hp = Stats.hp;
            SetupAgent();
        }

        _seenHp = Hp;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        All.Remove(this);
        NCFx.GhostPop(transform.position + Vector3.up * 0.9f, hasState ? BodyColor() : NCStyle.GhostBasic);
    }

    private void SetupAgent()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas) == false)
        {
            Debug.LogWarning($"[NCGhost] {transform.position} 근처에 NavMesh가 없습니다.");
            return;
        }

        _agent = gameObject.AddComponent<NavMeshAgent>();
        _agent.Warp(hit.position);
        _agent.speed = Stats.speed;
        _agent.radius = 0.35f;
        _agent.height = 1.6f;
        _agent.updateRotation = false;

        // 길만 찾게 하고 이동은 Tick에서 직접 한다. 에이전트가 프레임마다 옮긴 위치는
        // NetworkTransform이 Tick 시작에 되돌려, 호스트 FPS에 따라 속도가 달라진다.
        _agent.updatePosition = false;
    }

    // ───────── Host AI ─────────

    private void ServerTick()
    {
        NCBalance balance = NCBalance.Get;

        if (State == NCGhostState.Return)
        {
            TickReturn();
            return;
        }

        if (_camp != null && _camp.Contains(transform.position, _provoked ? balance.ghostChaseMargin : 0f) == false)
        {
            BeginReturn();
            return;
        }

        if (State == NCGhostState.Attack)
        {
            Stop();

            if (TelegraphTimer.Expired(Runner))
            {
                State = NCGhostState.Chase;
                ResolveAttack(balance);
            }

            return;
        }

        if (IsTargetValid(_target) == false)
        {
            _provoked = false;
            _target = FindTarget(balance);
        }

        if (_target == null)
        {
            if (State == NCGhostState.Chase)
            {
                BeginReturn();
            }

            return;
        }

        State = NCGhostState.Chase;
        Vector3 to = NCMath.Flat(_target.transform.position - transform.position);

        if (to.magnitude > balance.ghostAttackRange)
        {
            MoveTo(_target.transform.position, balance.ghostAttackRange * 0.7f);
            return;
        }

        Stop();
        Face(to);

        if (AttackCooldown.ExpiredOrNotRunning(Runner))
        {
            State = NCGhostState.Attack;
            TelegraphTimer = TickTimer.CreateFromSeconds(Runner, Stats.telegraph);
            AttackCooldown = TickTimer.CreateFromSeconds(Runner, Stats.attackInterval);
        }
    }

    private void ResolveAttack(NCBalance balance)
    {
        if (_target == null || _target.Health.IsDown || NCBulletSystem.Instance == null)
        {
            return;
        }

        Vector3 position = _target.transform.position;
        if (NCMath.FlatDistance(position, transform.position) <= balance.ghostAttackRange
            && NCBulletSystem.HasSight(transform.position, position))
        {
            NCBulletSystem.Instance.ServerQueueGhostAttack(_target.Health, Stats.damage, transform.position);
        }
    }

    private bool IsTargetValid(NCPlayer player)
    {
        return player != null && player.Object != null && player.Object.IsValid && player.Health.IsDown == false
               && (_camp == null || (_provoked && player == _target) || _camp.Contains(player.transform.position));
    }

    /// <summary>감지 거리·활동 영역·시야를 만족하는 가장 가까운 생존자(같으면 1P 우선). 보호 중이면 제외.</summary>
    private NCPlayer FindTarget(NCBalance balance)
    {
        NCPlayer best = null;
        float bestDistance = balance.ghostDetectRange;

        for (int slot = 0; slot < 2; slot++)
        {
            NCPlayer player = NCPlayer.FindBySlot(slot);
            if (IsTargetValid(player) == false || player.Health.IsProtected)
            {
                continue;
            }

            float distance = NCMath.FlatDistance(player.transform.position, transform.position);
            bool closer = best == null ? distance <= bestDistance : distance < bestDistance;
            if (closer && NCBulletSystem.HasSight(transform.position, player.transform.position))
            {
                best = player;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void BeginReturn()
    {
        _target = null;
        _provoked = false;
        State = NCGhostState.Return;
        ReturnTimer = TickTimer.CreateFromSeconds(Runner, ReturnTimeout);
    }

    private void TickReturn()
    {
        MoveTo(_home, 0.1f);
        bool arrived = NCMath.FlatDistance(transform.position, _home) <= 0.5f;

        // 길이 막혀 돌아가지 못하면 원위치로 옮긴다.
        if (arrived == false && ReturnTimer.Expired(Runner))
        {
            if (_agent != null)
            {
                _agent.Warp(_home);
                transform.position = _home;
            }

            arrived = true;
        }

        if (arrived)
        {
            Stop();
            Hp = Stats.hp;
            State = NCGhostState.Idle;
        }
    }

    /// <summary>탄환 피해. 사망은 캠프에 한 번만 통보한다. 실제로 들어간 피해량을 돌려준다(복귀 중이면 0). Host 전용.</summary>
    public float ServerTakeDamage(float amount, int attackerSlot)
    {
        if (Object == null || Object.IsValid == false || Hp <= 0f || State == NCGhostState.Return)
        {
            return 0f;
        }

        float dealt = Mathf.Min(Hp, amount);
        Hp -= amount;

        if (Hp <= 0f)
        {
            if (_camp != null)
            {
                _camp.ServerNotifyDeath(attackerSlot);
            }

            Runner.Despawn(Object);
            return dealt;
        }

        Provoke(attackerSlot);
        return dealt;
    }

    /// <summary>쏜 플레이어를 목표로 삼는다. 이미 유효한 다른 목표가 있으면 바꾸지 않는다.</summary>
    private void Provoke(int attackerSlot)
    {
        NCPlayer attacker = NCPlayer.FindBySlot(attackerSlot);
        if (attacker == null || attacker.Health.IsDown || (attacker != _target && IsTargetValid(_target)))
        {
            return;
        }

        _target = attacker;
        _provoked = true;
    }

    private void MoveTo(Vector3 destination, float stopping)
    {
        if (_agent == null || _agent.isOnNavMesh == false)
        {
            return;
        }

        Vector3 position = transform.position;
        _agent.nextPosition = position;
        _agent.velocity = Vector3.zero;
        _agent.SetDestination(destination);

        if (_agent.hasPath == false || NCMath.FlatDistance(position, destination) <= stopping)
        {
            Face(destination - position);
            return;
        }

        Vector3 to = NCMath.Flat(_agent.steeringTarget - position);
        Vector3 next = position + Vector3.ClampMagnitude(to, Stats.speed * Runner.DeltaTime);

        // 걸을 수 있는 곳을 벗어나지 않게 가장 가까운 NavMesh 위로 붙인다(모서리에서는 미끄러진다).
        if (NavMesh.SamplePosition(next, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
        {
            next = hit.position;
        }

        transform.position = next;
        _agent.nextPosition = next;
        Face(to);
    }

    private void Stop()
    {
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.nextPosition = transform.position;
            _agent.velocity = Vector3.zero;
            if (_agent.hasPath)
            {
                _agent.ResetPath();
            }
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

    // ───────── 연출 ─────────

    public Color BodyColor()
    {
        switch (Type)
        {
            case NCGhostType.Fast: return NCStyle.GhostFast;
            case NCGhostType.Tank: return NCStyle.GhostTank;
            default: return NCStyle.GhostBasic;
        }
    }

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

        if (State == NCGhostState.Attack)
        {
            float t = Mathf.PingPong(Time.time * 8f, 1f);
            color = Color.Lerp(NCStyle.GhostTelegraph, Color.white, t * 0.25f);
            pulse = 1f + 0.12f * t;
        }
        else if (State == NCGhostState.Return)
        {
            color = Color.Lerp(color, Color.black, 0.45f); // 복귀 중(무적)
        }

        if (_flashTimer > 0f)
        {
            color = Color.white;
        }

        if (bodyRenderer == null)
        {
            return;
        }

        if (color != _shownColor)
        {
            _shownColor = color;
            bodyRenderer.material.color = color;
        }

        Transform body = bodyRenderer.transform;
        float height = 0.9f * Mathf.Max(1f, Stats.bodyScale);
        body.localPosition = new Vector3(0f, height + Mathf.Sin(Time.time * 3f + GetInstanceID()) * 0.08f, 0f);
        body.localScale = _bodyScale * pulse;
    }
}
