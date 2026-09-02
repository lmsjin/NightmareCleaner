using UnityEngine;

// 모듈 C — 선택된 능력을 플레이어 스탯에 반영.
// OnAbilitySelected를 구독해 IPlayerStats 인터페이스로만 스탯을 올린다(직접 참조 금지).
// 증가폭은 BalanceConfig에서 읽음.
public class AbilityApplier : MonoBehaviour
{
    private BalanceConfig _cfg;

    void Awake() => _cfg = BalanceConfig.Resolve(null);

    void OnEnable()  => GameEvents.OnAbilitySelected += Apply;
    void OnDisable() => GameEvents.OnAbilitySelected -= Apply;

    void Apply(AbilityType type)
    {
        // PlayerStats가 IPlayerStats를 구현 — 인터페이스로만 접근
        if (FindFirstObjectByType<PlayerStats>() is not IPlayerStats stats) return;

        float amount = _cfg != null ? _cfg.GetAbilityBonus(type) : 0f;
        switch (type)
        {
            case AbilityType.SuctionPower:
                stats.AddSuctionPower(amount);
                break;
            case AbilityType.SuctionRange:
                stats.AddSuctionRange(amount);
                break;
            case AbilityType.MoveSpeed:
                stats.AddMoveSpeed(amount);
                break;
        }
    }
}
