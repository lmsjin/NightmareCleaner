using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public abstract class GhostBase : MonoBehaviour, IDamageable, IKnockbackable
{
    protected float _maxHP = 10f;
    protected float _moveSpeed = 3f;
    protected int _contactDamage = 1;
    protected float _attackInterval = 1f;
    protected float _attackRange = 1.5f;
    protected float _consumeDuration = 0.2f;

    protected virtual Color BodyColor => new Color(0.7f, 0.85f, 1f, 1f);
    protected virtual float BodyScale => 1f;
    protected virtual GhostVisualId VisualId => GhostVisualId.Jelly;
    protected virtual string DefaultVisualResourcePath => null;
    protected virtual Vector3 DefaultVisualLocalPosition => Vector3.zero;
    protected virtual Vector3 DefaultVisualLocalEuler => Vector3.zero;
    protected virtual Vector3 DefaultVisualLocalScale => Vector3.one;
    protected virtual float ColliderRadius => 0.45f;
    protected virtual float ColliderHeight => 1.2f;
    protected virtual bool IsSpecial => false;
    protected virtual bool SuppressChase => false;

    private const float RepathInterval = 0.15f;
    private const float NavSampleDist = 3f;
    private const float FaceTurnSpeed = 720f;
    private const float MinFacingSpeedSqr = 0.05f; // 이 속도 미만이면 "정지 중"으로 보고 플레이어를 바로 봄
    private const float KnockbackDuration = 0.18f; // 명중 넉백 지속(초)

    private float _hp;
    private float _atkTimer;
    private float _knockbackTimer;
    private bool _knockingBack;
    private float _repathTimer;
    private bool _consumed;
    private float _consumeT;
    private Vector3 _startPos;
    private Vector3 _startScale;
    private int _xpReward;
    private bool _tintVisualMaterials;

    protected bool _useAgent;
    protected Rigidbody _rb;
    protected NavMeshAgent _agent;
    protected Transform _visualRoot;

    private Transform _playerTf;
    private IPlayerStats _playerStats;

    public int XpReward => _xpReward;

    public static T Create<T>(Vector3 position) where T : GhostBase
    {
        GameObject go = new(typeof(T).Name);
        go.transform.position = position;

        int layer = LayerMask.NameToLayer("Ghost");
        if (layer >= 0) go.layer = layer;

        go.AddComponent<Rigidbody>();
        CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
        capsule.radius = 0.45f;
        capsule.height = 1.2f;
        capsule.center = new Vector3(0f, 0.6f, 0f);

        return go.AddComponent<T>();
    }

    protected abstract void Configure(BalanceConfig cfg);

    protected virtual void OnTick() { }

    private void Awake()
    {
        BalanceConfig cfg = BalanceConfig.Resolve(null);
        if (cfg != null) Configure(cfg);

        _hp = _maxHP;
        _xpReward = cfg != null ? (IsSpecial ? cfg.xpSpecialGhost : cfg.xpBasicGhost) : (IsSpecial ? 5 : 1);

        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.linearDamping = 0.8f;
        _rb.constraints |= RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        transform.localScale = Vector3.one * BodyScale;
        SetupCollider();
        SetupVisual();

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavSampleDist, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            SetupAgent();
            _useAgent = true;
        }
    }

    private void SetupAgent()
    {
        _agent = gameObject.AddComponent<NavMeshAgent>();
        _agent.speed = _moveSpeed;
        _agent.angularSpeed = 720f;
        _agent.acceleration = 20f;
        _agent.stoppingDistance = _attackRange * 0.8f;
        _agent.radius = 0.4f * BodyScale;
        _agent.height = Mathf.Max(0.5f, BodyScale);
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
        _agent.autoBraking = true;
    }

    protected virtual void Start()
    {
        PlayerStats ps = FindFirstObjectByType<PlayerStats>();
        if (ps != null)
        {
            _playerTf = ps.transform;
            _playerStats = ps;
        }

        if (ps != null && TryGetComponent(out Collider myCol))
        {
            foreach (Collider pc in ps.GetComponentsInChildren<Collider>())
            {
                if (pc != null) Physics.IgnoreCollision(myCol, pc, true);
            }
        }

        float originY = _playerTf != null ? _playerTf.position.y : transform.position.y;

        if (_useAgent && _agent != null)
        {
            float navY = NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavSampleDist, NavMesh.AllAreas)
                ? hit.position.y
                : transform.position.y;
            _agent.baseOffset = originY - navY;
            _rb.isKinematic = true;
        }
        else
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, originY, p.z);
            _rb.isKinematic = false;
        }
    }

    private void Update()
    {
        if (_consumed)
        {
            ConsumeStep();
            return;
        }

          if (RescueMiniGameManager.IsActive) return;

        // 명중 넉백 중에는 추격/공격을 멈추고 물리로 밀려난다.
        if (_knockingBack)
        {
            _knockbackTimer -= Time.deltaTime;
            if (_knockbackTimer <= 0f) EndKnockback();
            return;
        }

        if (_playerTf == null) return;

        if (!SuppressChase)
        {
            if (_useAgent && _agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _repathTimer -= Time.deltaTime;
                if (_repathTimer <= 0f)
                {
                    _agent.SetDestination(_playerTf.position);
                    _repathTimer = RepathInterval;
                }
            }
            else
            {
                DirectChase();
            }
        }

        Vector3 toPlayer = _playerTf.position - transform.position;
        toPlayer.y = 0f;
        FaceDirection(GetFacingDirection(toPlayer));
        float dist = toPlayer.magnitude;

        _atkTimer -= Time.deltaTime;
        if (dist <= _attackRange && _atkTimer <= 0f)
        {
            _playerStats?.TakeDamage(_contactDamage);
            _atkTimer = _attackInterval;
        }

        OnTick();
    }

    private void DirectChase()
    {
        Vector3 toPlayer = _playerTf.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.magnitude > _attackRange)
            transform.position += toPlayer.normalized * _moveSpeed * Time.deltaTime;
    }

    // 실제로 바라봐야 할 방향을 결정.
    // NavMeshAgent로 우회 이동 중일 때는 진짜 이동 방향(에이전트 속도)을 봐서
    // "플레이어를 보면서 옆으로 미끄러지는" 부자연스러운 모습을 방지.
    // 거의 멈춰있을 때(공격 사거리 진입 등)는 플레이어를 바로 봄.
    private Vector3 GetFacingDirection(Vector3 toPlayer)
    {
        if (_useAgent && _agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            Vector3 vel = _agent.velocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > MinFacingSpeedSqr)
                return vel;
        }
        return toPlayer;
    }

    protected void FaceDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, FaceTurnSpeed * Time.deltaTime);
    }

    // 사격 명중 시 VacuumController가 호출 — 뒤로 밀리는 넉백 타격감.
    // agent를 잠시 끄고 Rigidbody 물리로 밀린 뒤 NavMesh로 복귀.
    public void ApplyKnockback(Vector3 dir, float force)
    {
        if (_consumed) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        _knockingBack = true;
        _knockbackTimer = KnockbackDuration;

        if (_agent != null && _agent.enabled) _agent.enabled = false;
        if (_rb != null)
        {
            _rb.isKinematic = false;
            _rb.linearVelocity = dir.normalized * force;
        }
    }

    private void EndKnockback()
    {
        _knockingBack = false;
        if (!_useAgent || _agent == null)
        {
            if (_rb != null) _rb.linearVelocity = Vector3.zero;
            return;
        }

        if (_rb != null) _rb.isKinematic = true;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, NavSampleDist, NavMesh.AllAreas))
        {
            _agent.enabled = true;
            _agent.Warp(hit.position);
        }
    }

    public virtual void TakeDamage(float amount)
    {
        if (_consumed) return;
        _hp -= amount;
        if (_hp <= 0f) BeginConsume();
    }

    private void BeginConsume()
    {
        _consumed = true;
        _startPos = transform.position;
        _startScale = transform.localScale;
        if (_agent != null && _agent.enabled) _agent.enabled = false;
        if (_rb != null) _rb.isKinematic = true;
        OnConsumeBegin();
    }

    protected virtual void OnConsumeBegin() { }

    // 사격 처치 연출: 그 자리에서 스케일 축소 + 회전하며 소멸("펑").
    private void ConsumeStep()
    {
        _consumeT += Time.deltaTime / Mathf.Max(0.01f, _consumeDuration);
        float t = Mathf.Clamp01(_consumeT);

        transform.position = _startPos;
        transform.localScale = Vector3.Lerp(_startScale, Vector3.zero, t);
        transform.Rotate(0f, 1080f * Time.deltaTime, 0f);

        if (_consumeT >= 1f)
        {
            GameEvents.RaiseGhostKilled(this);
            Destroy(gameObject);
        }
    }

    private void SetupCollider()
    {
        float radius = Mathf.Max(0.05f, ColliderRadius);
        float height = Mathf.Max(radius * 2f, ColliderHeight);

        GhostVisualSettings settings = GhostVisualSettings.Load();
        if (settings != null && settings.TryGet(VisualId, out GhostVisualSettings.Entry entry) && entry.useConfiguredCollider)
        {
            radius = Mathf.Max(0.05f, entry.colliderRadius);
            height = Mathf.Max(radius * 2f, entry.colliderHeight);
        }

        CapsuleCollider capsule = null;
        foreach (Collider col in GetComponents<Collider>())
        {
            if (col is CapsuleCollider found)
                capsule = found;
            else
                Destroy(col);
        }

        if (capsule == null)
            capsule = gameObject.AddComponent<CapsuleCollider>();

        capsule.direction = 1;
        capsule.radius = radius;
        capsule.height = height;
        capsule.center = new Vector3(0f, height * 0.5f, 0f);
        capsule.isTrigger = false;
    }

    private void SetupVisual()
    {
        if (_visualRoot != null)
            Destroy(_visualRoot.gameObject);

        GameObject prefab = null;
        Vector3 localPosition = DefaultVisualLocalPosition;
        Vector3 localEuler = DefaultVisualLocalEuler;
        Vector3 localScale = DefaultVisualLocalScale;
        bool tintMaterials = false;

        GhostVisualSettings settings = GhostVisualSettings.Load();
        if (settings != null && settings.TryGet(VisualId, out GhostVisualSettings.Entry entry))
        {
            prefab = entry.modelPrefab;
            localPosition = entry.localPosition;
            localEuler = entry.localEuler;
            localScale = entry.localScale;
            tintMaterials = entry.tintMaterials;
        }

        if (prefab == null && !string.IsNullOrEmpty(DefaultVisualResourcePath))
            prefab = Resources.Load<GameObject>(DefaultVisualResourcePath);

        bool usingFallbackPrimitive = prefab == null;
        GameObject visual = prefab != null
            ? Instantiate(prefab, transform)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);

        visual.name = "Visual";
        if (visual.transform.parent != transform)
            visual.transform.SetParent(transform, false);

        visual.transform.localPosition = localPosition;
        visual.transform.localRotation = Quaternion.Euler(localEuler);
        visual.transform.localScale = localScale;
        _visualRoot = visual.transform;
        _tintVisualMaterials = tintMaterials || usingFallbackPrimitive;

        DisableVisualPhysics(visual);
        ApplyColor();
    }

    private void DisableVisualPhysics(GameObject visual)
    {
        foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            if (body != _rb)
                Destroy(body);
        }

        foreach (Collider col in visual.GetComponentsInChildren<Collider>(true))
        {
            if (col.transform != transform)
                col.enabled = false;
        }
    }

    private void ApplyColor()
    {
        if (!_tintVisualMaterials)
            return;

        Renderer[] renderers = _visualRoot != null
            ? _visualRoot.GetComponentsInChildren<Renderer>(true)
            : GetComponentsInChildren<Renderer>(true);

        foreach (Renderer rend in renderers)
        {
            Material m = rend.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", BodyColor);
            if (m.HasProperty("_Color")) m.color = BodyColor;
        }
    }
}