using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(VacuumController))]
public class PlayerStats : MonoBehaviour, IPlayerStats, IAbilityStatsReadOnly
{
    [SerializeField] private BalanceConfig config;

    [Header("효과음")]
    [SerializeField] private AudioSource hitAudioSource; // 비워두면 자동 생성
    [SerializeField] private AudioClip   hitClip;        // 피격 시 재생할 사운드
    [SerializeField, Range(0f, 1f)] private float hitVolume = 1f;

    public int   MaxHP        { get; private set; }
    public int   CurrentHP    { get; private set; }
    public float MoveSpeed    { get; private set; }
    public float SuctionPower { get; private set; }
    public float SuctionRange { get; private set; }
    public float SuctionAngle { get; private set; }
    public float SuctionPullSpeed          { get; private set; }
    public float SuctionPullResponsiveness { get; private set; }
    public float SuctionSettleRadius       { get; private set; }

    public bool IsInvincible => _iframe > 0f;

    private PlayerController _controller;
    private float _invincibleSeconds;
    private float _knockbackForce;
    private float _iframe;

    void Awake()
    {
        InitHitAudio();
    }

    void InitHitAudio()
    {
        if (hitAudioSource == null)
            hitAudioSource = gameObject.AddComponent<AudioSource>();

        hitAudioSource.playOnAwake = false;
        hitAudioSource.loop        = false;
    }

    void Start()
    {
        MaxHP                     = config.playerMaxHP;
        CurrentHP                 = MaxHP;
        MoveSpeed                 = config.playerMoveSpeed;
        SuctionPower              = config.suctionPower;
        SuctionRange              = config.suctionRange;
        SuctionAngle              = config.suctionAngle;
        SuctionPullSpeed          = config.suctionPullSpeed;
        SuctionPullResponsiveness = config.suctionPullResponsiveness;
        SuctionSettleRadius       = config.suctionSettleRadius;
        _invincibleSeconds        = config.invincibleSeconds;
        _knockbackForce           = config.knockbackForce;

        _controller = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (_iframe > 0f) _iframe -= Time.deltaTime;
    }

    public void TakeDamage(int amount)
    {
        if (IsInvincible) return; // 무적 중 피해 무시

        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        PlayHitSfx();
        if (CurrentHP == 0) { GameEvents.RaiseGameOver(); return; }

        _iframe = _invincibleSeconds;
        if (_controller != null)
            _controller.OnHit(ComputeKnockbackDir(), _invincibleSeconds, _knockbackForce);
    }

    void PlayHitSfx()
    {
        if (hitAudioSource != null && hitClip != null)
            hitAudioSource.PlayOneShot(hitClip, hitVolume);
    }

    // 가장 가까운 유령(IDamageable) 반대 방향(수평). 없으면 등 뒤로.
    Vector3 ComputeKnockbackDir()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 2.5f);
        float   best   = float.MaxValue;
        Vector3 dir    = -transform.forward;
        foreach (var col in hits)
        {
            if (col.GetComponentInParent<IDamageable>() == null) continue;
            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            float d = away.sqrMagnitude;
            if (d < best && d > 0.0001f) { best = d; dir = away.normalized; }
        }
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;
    }

    public void AddSuctionPower(float amount) => SuctionPower += amount;
    public void AddSuctionRange(float amount) => SuctionRange += amount;
    public void AddMoveSpeed(float amount)    => MoveSpeed    += amount;
}