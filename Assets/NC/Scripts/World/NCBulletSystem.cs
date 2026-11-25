using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 구체 탄환. Host가 이동과 첫 충돌을 계산하고, 발사·소멸만 RPC로 알려 각 화면이 그린다.
/// 명중은 모았다가 NCSession이 한꺼번에 확정한다. 씬의 Session 오브젝트에 붙인다.
/// </summary>
[RequireComponent(typeof(NCSession))]
public class NCBulletSystem : NetworkBehaviour
{
    public static NCBulletSystem Instance { get; private set; }

    private class Bullet
    {
        public int Id;
        public int Slot;
        public Vector3 Position;
        public Vector3 Direction;
        public float Remaining;
    }

    private struct Hit
    {
        public NCHealth Player;
        public NCGhost Ghost;
        public int Damage;
        public int Slot; // 유령의 공격이면 -1
        public Vector3 Origin; // 공격이 날아온 쪽(피격 방향 표시)
    }

    private readonly List<Bullet> _bullets = new List<Bullet>();
    private readonly List<Hit> _hits = new List<Hit>();
    private readonly Dictionary<int, NCFxShot> _shots = new Dictionary<int, NCFxShot>();
    private int _nextId;

    public override void Spawned()
    {
        Instance = this;
        NCSfx.Preload();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
        {
            Instance = null;
        }

        _shots.Clear();
    }

    // ───────── Host 전용 ─────────

    public void ServerFire(NCPlayer shooter, Vector3 aimPoint, bool overcharged)
    {
        NCBalance balance = NCBalance.Get;
        Vector3 muzzle = shooter.MuzzlePosition;
        Vector3 body = shooter.transform.position;
        body.y = muzzle.y;

        Vector3 direction = NCMath.Flat(aimPoint - body);
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = NCMath.Flat(shooter.transform.forward);
        }

        direction.Normalize();

        // 판정은 몸 중심에서 시작한다: 바로 앞의 대상이나 벽을 총구가 지나쳐도 놓치지 않는다. 사거리는 총구부터 잰다.
        float offset = WallDistance(body, direction, NCMath.FlatDistance(body, muzzle), balance.bulletRadius);

        Bullet bullet = new Bullet
        {
            Id = ++_nextId,
            Slot = shooter.Slot,
            Position = body,
            Direction = direction,
            Remaining = balance.shotRange + offset,
        };

        _bullets.Add(bullet);
        NCSession.Instance.ServerCountShot(shooter.Slot);
        RpcFire(bullet.Id, body, direction, offset, overcharged);
    }

    /// <summary>모든 탄환을 한 Tick 이동시키고 첫 충돌을 모은다.</summary>
    public void ServerStep()
    {
        NCBalance balance = NCBalance.Get;
        float radius = balance.bulletRadius;

        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            Bullet bullet = _bullets[i];
            float step = Mathf.Min(balance.bulletSpeed * Runner.DeltaTime, bullet.Remaining);
            float nearest = WallDistance(bullet.Position, bullet.Direction, step, radius);
            bool blocked = nearest < step;
            Hit hit = new Hit { Damage = balance.shotDamage, Slot = bullet.Slot, Origin = bullet.Position - bullet.Direction };

            foreach (NCPlayer player in NCPlayer.All)
            {
                if (player.Slot == bullet.Slot || player.Health.IsDown)
                {
                    continue;
                }

                if (Sweep(bullet, player.transform.position, radius + balance.playerRadius, out float distance) && distance <= nearest)
                {
                    nearest = distance;
                    hit.Player = player.Health;
                    hit.Ghost = null;
                }
            }

            foreach (NCGhost ghost in NCGhost.All)
            {
                if (Sweep(bullet, ghost.transform.position, radius + balance.GetGhost(ghost.Type).radius, out float distance) && distance < nearest)
                {
                    nearest = distance;
                    hit.Ghost = ghost;
                    hit.Player = null;
                }
            }

            bool struck = hit.Player != null || hit.Ghost != null;
            bullet.Position += bullet.Direction * nearest;
            bullet.Remaining -= nearest;

            if (struck)
            {
                _hits.Add(hit);
                NCSession.Instance.ServerCountHit(bullet.Slot);
            }

            if (struck || blocked || bullet.Remaining <= 0.0001f)
            {
                _bullets.RemoveAt(i);
                RpcEnd(bullet.Id, bullet.Position, struck || blocked);
            }
        }
    }

    /// <summary>유령의 공격도 같은 묶음으로 확정한다.</summary>
    public void ServerQueueGhostAttack(NCHealth target, int damage, Vector3 origin)
    {
        _hits.Add(new Hit { Player = target, Damage = damage, Slot = -1, Origin = origin });
    }

    public void ServerApplyHits()
    {
        // 플레이어 피해·사망을 먼저 확정하고 유령 처치(막타)를 기록한다.
        foreach (Hit hit in _hits)
        {
            if (hit.Player != null)
            {
                int dealt = hit.Player.ServerTakeDamage(hit.Damage);
                if (dealt > 0)
                {
                    NCPlayer victim = hit.Player.GetComponent<NCPlayer>();
                    bool killed = hit.Player.IsDown;

                    if (killed)
                    {
                        NCSession.Instance.ServerCountKill(hit.Slot, victim.Slot);
                    }

                    RpcHit(hit.Slot, victim.Slot, victim.transform.position, hit.Origin, dealt, killed);
                }
            }
        }

        foreach (Hit hit in _hits)
        {
            // 같은 Tick에 이미 제거된 유령은 건너뛴다. 제거되면 네트워크 값을 읽을 수 없으므로 먼저 읽어 둔다.
            if (hit.Ghost != null && hit.Ghost.Object != null && hit.Ghost.Object.IsValid)
            {
                float before = hit.Ghost.Hp;
                Vector3 position = hit.Ghost.transform.position;
                float dealt = hit.Ghost.ServerTakeDamage(hit.Damage, hit.Slot);
                if (dealt > 0f)
                {
                    RpcHit(hit.Slot, -1, position, hit.Origin, Mathf.RoundToInt(dealt), dealt >= before);
                }
            }
        }

        _hits.Clear();
    }

    public void ServerClear()
    {
        _bullets.Clear();
        _hits.Clear();
        RpcClear();
    }

    /// <summary>평면에서 탄환 경로가 원에 처음 닿는 거리. 탄환 뒤쪽 대상은 제외.</summary>
    private static bool Sweep(Bullet bullet, Vector3 target, float radius, out float distance)
    {
        Vector3 to = NCMath.Flat(target - bullet.Position);
        float along = Vector3.Dot(to, bullet.Direction);
        float sideSqr = to.sqrMagnitude - along * along;
        distance = 0f;

        if (along < 0f || sideSqr > radius * radius)
        {
            return false;
        }

        distance = Mathf.Max(0f, along - Mathf.Sqrt(Mathf.Max(0f, radius * radius - sideSqr)));
        return true;
    }

    /// <summary>차단 지형까지의 거리(플레이어·유령·트리거 제외). 없으면 range. radius 0이면 선으로 검사.</summary>
    public static float WallDistance(Vector3 from, Vector3 direction, float range, float radius)
    {
        float nearest = range;
        RaycastHit[] hits = radius > 0f
            ? Physics.SphereCastAll(from, radius, direction, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(from, direction, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.distance < nearest
                && hit.collider.GetComponentInParent<NCPlayer>() == null
                && hit.collider.GetComponentInParent<NCGhost>() == null)
            {
                nearest = hit.distance;
            }
        }

        return nearest;
    }

    /// <summary>가슴 높이에서 벽에 가리지 않는지.</summary>
    public static bool HasSight(Vector3 a, Vector3 b)
    {
        Vector3 to = NCMath.Flat(b - a);
        float distance = to.magnitude;
        return distance < 0.01f || WallDistance(a + Vector3.up, to / distance, distance, 0f) >= distance;
    }

    // ───────── 연출 ─────────

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcFire(int id, Vector3 body, Vector3 direction, float offset, NetworkBool overcharged)
    {
        NCBalance balance = NCBalance.Get;
        // 판정과 같은 몸 중심에서 출발시켜 보이는 위치와 판정이 어긋나지 않게 한다.
        _shots[id] = NCFx.Launch(body, direction, balance.bulletSpeed, balance.shotRange + offset,
            overcharged ? NCStyle.ShotOvercharge : NCStyle.ShotNormal);
        NCSfx.Shot(body, overcharged);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcEnd(int id, Vector3 position, NetworkBool impact)
    {
        if (_shots.TryGetValue(id, out NCFxShot shot))
        {
            _shots.Remove(id);

            if (shot != null)
            {
                shot.StopAt(position, impact);
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcHit(int attackerSlot, int victimSlot, Vector3 position, Vector3 origin, int damage, NetworkBool killed)
    {
        NCHitFeedback.OnHit(attackerSlot, victimSlot, position, origin, damage, killed);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcClear()
    {
        foreach (NCFxShot shot in _shots.Values)
        {
            if (shot != null)
            {
                shot.StopAt(shot.transform.position, false);
            }
        }

        _shots.Clear();
    }
}
