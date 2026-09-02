using UnityEngine;

// 홀쭉 — 속공. HP 낮고 빠름.
public class GhostSlim : GhostBase
{
    protected override Color BodyColor => new Color(0.95f, 0.85f, 0.3f, 1f); // 노랑
    protected override float BodyScale => 0.75f;
    protected override GhostVisualId VisualId => GhostVisualId.Slim;
    protected override string DefaultVisualResourcePath => "Ghost/FLY";
    protected override float ColliderRadius => 0.35f;
    protected override float ColliderHeight => 1.1f;

    protected override void Configure(BalanceConfig cfg)
    {
        _maxHP         = cfg.slimHP;
        _moveSpeed     = cfg.slimSpeed;
        _contactDamage = cfg.slimDamage;
    }
}
