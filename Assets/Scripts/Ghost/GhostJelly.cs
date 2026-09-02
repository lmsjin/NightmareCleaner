using UnityEngine;

// 젤리 — 일반 유령. 평범한 추격 + 접촉 피해.
public class GhostJelly : GhostBase
{
    protected override Color BodyColor => new Color(0.55f, 0.85f, 0.65f, 1f); // 연두
    protected override float BodyScale => 1f;
    protected override GhostVisualId VisualId => GhostVisualId.Jelly;
    protected override string DefaultVisualResourcePath => "Ghost/G_TypeA";

    protected override void Configure(BalanceConfig cfg)
    {
        _maxHP         = cfg.jellyHP;
        _moveSpeed     = cfg.jellySpeed;
        _contactDamage = cfg.jellyDamage;
    }
}
