using UnityEngine;

// 모듈 C — 점수 + 경험치(XP) 권한 보유자.
// 유령 처치 / 아이 구출을 구독해 점수를 합산하고, XP를 누적한다.
// XP 바가 차면(레벨업) 능력 선택을 트리거(RaiseAbilityTrigger). 임계값은 레벨마다 점증.
// HUD는 이 값을 "읽어서 표시만" 한다.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public int CurrentScore { get; private set; }
    public int ChildCount   { get; private set; }
    public int GhostKills    { get; private set; }

    // 경험치(XP) 기반 능력 강화
    public int   Level   { get; private set; }
    public float XpRatio => _xpThreshold > 0 ? Mathf.Clamp01((float)_xp / _xpThreshold) : 0f;

    private BalanceConfig _cfg;
    private int _xp;
    private int _xpThreshold;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _cfg = BalanceConfig.Resolve(null);
        _xpThreshold = ComputeThreshold(0);
    }

    void OnEnable()
    {
        GameEvents.OnGhostKilled  += OnGhostKilled;
        GameEvents.OnChildRescued += OnChildRescued;
    }

    void OnDisable()
    {
        GameEvents.OnGhostKilled  -= OnGhostKilled;
        GameEvents.OnChildRescued -= OnChildRescued;
        if (Instance == this) Instance = null;
    }

    void OnGhostKilled(GhostBase g)
    {
        CurrentScore += _cfg != null ? _cfg.scorePerGhostKill : 10;
        GhostKills++;
        AddXp(g != null ? g.XpReward : 1); // 일반=소, 특수=대 (GhostBase가 결정)
    }

    void OnChildRescued(int points)
    {
        CurrentScore += points;
        ChildCount++;
        AddXp(_cfg != null ? _cfg.xpChildRescue : 10); // 구출은 큰 XP
    }

    // XP 누적 → 임계값 도달 시마다 레벨업 + 능력 선택 트리거(여러 번 가능 → AbilitySystem이 큐잉)
    void AddXp(int amount)
    {
        if (amount <= 0) return;
        _xp += amount;
        while (_xpThreshold > 0 && _xp >= _xpThreshold)
        {
            _xp -= _xpThreshold;
            Level++;
            _xpThreshold = ComputeThreshold(Level);
            GameEvents.RaiseAbilityTrigger();
        }
    }

    // 레벨업에 필요한 XP = 첫 임계값 × 성장배율^레벨
    int ComputeThreshold(int level)
    {
        if (_cfg == null) return 8;
        float t = _cfg.xpFirstThreshold * Mathf.Pow(_cfg.xpThresholdGrowth, level);
        return Mathf.Max(1, Mathf.CeilToInt(t));
    }
}
