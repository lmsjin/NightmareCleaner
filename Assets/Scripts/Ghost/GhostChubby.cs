using UnityEngine;

// 통통 — 탱커. HP 높고 느림. 접촉 피해 큼.
public class GhostChubby : GhostBase
{
    protected override Color BodyColor => new Color(0.45f, 0.6f, 0.95f, 1f); // 파랑
    protected override float BodyScale => 1.4f;
    protected override GhostVisualId VisualId => GhostVisualId.Chubby;
    protected override string DefaultVisualResourcePath => "Ghost/fat";
    protected override float ColliderRadius => 0.55f;
    protected override float ColliderHeight => 1.35f;

    protected override void Configure(BalanceConfig cfg)
    {
        _maxHP         = cfg.chubbyHP;
        _moveSpeed     = cfg.chubbySpeed;
        _contactDamage = cfg.chubbyDamage;
    }
}
