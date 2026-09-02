using UnityEngine;

// 탄약/탄창/재장전을 관리하는 전용 컴포넌트(사격 로직과 분리).
// VacuumController(사격)가 발사 직전 TryConsume()로 1발을 소모한다.
// 탄창 소진 시 자동 재장전, R키 등으로 StartReload() 수동 재장전.
// 아이 구출 시(OnChildRescued) 탄창 상한이 늘어난다 — 모듈 B는 기존 이벤트만 발행하면 됨.
//
// HUD 호환을 위해 옛 흡입 게이지 프로퍼티명을 그대로 유지한다:
//   Ratio            = 남은 탄약 비율(0~1)
//   IsOverheated     = 재장전 중
//   IsOverheatWarning= 재장전 중(경고 아이콘 표시용)
public class SuctionCharge : MonoBehaviour
{
    [SerializeField] private BalanceConfig config;

    public float Max          { get; private set; } // 탄창 크기(발)
    public float Current      { get; private set; } // 남은 탄약(발)
    public bool  IsOverheated => _reloading;         // 재장전 중
    public bool  IsOverheatWarning => _reloading;    // 재장전 중 경고 표시
    public bool  IsReloading  => _reloading;
    public float Ratio => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;

    private float _reloadSeconds;
    private float _rescueBonus;

    private bool  _reloading;
    private float _reloadTimer;

    void Start()
    {
        var c = BalanceConfig.Resolve(config);
        Max            = c != null ? c.magazineSize  : 12f;
        _reloadSeconds = c != null ? c.reloadSeconds : 1.2f;
        _rescueBonus   = c != null ? c.suctionRescueBonus : 1f;
        Current        = Max;
    }

    void OnEnable()  => GameEvents.OnChildRescued += OnRescued;
    void OnDisable() => GameEvents.OnChildRescued -= OnRescued;

    // 아이 구출 시 탄창 상한 증가 (모듈 B가 RaiseChildRescued만 발행하면 자동 적용)
    void OnRescued(int _)
    {
        Max += _rescueBonus;
        if (!_reloading) Current += _rescueBonus;
    }

    void Update()
    {
        if (!_reloading) return;

        _reloadTimer -= Time.deltaTime;
        if (_reloadTimer <= 0f)
        {
            Current    = Max;
            _reloading = false;
        }
    }

    // 발사 직전 호출. 탄약 1발 소모 시 true. 탄창이 비었거나 재장전 중이면 false.
    // 마지막 탄을 쏘면 자동으로 재장전을 시작한다.
    public bool TryConsume()
    {
        if (_reloading) return false;
        if (Current <= 0f) { StartReload(); return false; }

        Current -= 1f;
        if (Current <= 0f)
        {
            Current = 0f;
            StartReload();
        }
        return true;
    }

    // 수동/자동 재장전 시작. 이미 재장전 중이거나 탄창이 가득이면 무시.
    public void StartReload()
    {
        if (_reloading || Current >= Max) return;
        _reloading   = true;
        _reloadTimer = _reloadSeconds;
    }
}
