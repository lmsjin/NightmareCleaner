using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// [멀티 슬라이스] 사격. Owner가 좌클릭 → 서버에 "이 방향으로 쐈다" 요청(ServerRpc) →
// 서버가 히트 판정(권위)해서 유령에 데미지 → 전원 화면에 트레이서(ClientRpc).
// 히트 판정은 싱글 VacuumController.Fire의 OverlapCapsule(몸통 높이 빔) 방식을 서버로 이식.
[RequireComponent(typeof(NetPlayerController))]
public class NetShooter : NetworkBehaviour
{
    [SerializeField] float damage       = 10f; // 발당 데미지 (BalanceConfig.suctionPower)
    [SerializeField] float range        = 12f; // 사거리 (BalanceConfig.suctionRange)
    [SerializeField] float fireInterval = 0.15f; // 연사 간격 (BalanceConfig.fireInterval)
    [SerializeField] float aimHeight    = 0.7f;
    [SerializeField] float hitRadius    = 0.5f; // 빔(조준선) 두께
    [SerializeField] float ghostRadius  = 0.6f; // 유령 몸 반경(판정 여유)
    [SerializeField] bool  debugLog     = true; // 콘솔에 명중 로그(진단용)

    NetPlayerController _player;
    NetPlayerHealth     _health;
    float _fireTimer;

    void Awake()
    {
        _player = GetComponent<NetPlayerController>();
        _health = GetComponent<NetPlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        // 밸런스 수치는 기존 BalanceConfig 재사용
        var cfg = BalanceConfig.Resolve(null);
        if (cfg != null)
        {
            damage       = cfg.suctionPower;
            range        = cfg.suctionRange;
            fireInterval = cfg.fireInterval;
        }
    }

    void Update()
    {
        if (!IsOwner) return; // 내 캐릭터만 발사 입력
        if (_health != null && _health.Dead.Value) return; // 죽은 동안 발사 금지
        if (_fireTimer > 0f) _fireTimer -= Time.deltaTime;

        var mouse = Mouse.current;
        bool want = mouse != null && mouse.leftButton.isPressed;
        if (!want || _fireTimer > 0f) return;

        Vector3 origin = transform.position + Vector3.up * aimHeight;
        if (_player.TryGetAimDirection(origin, out Vector3 dir))
        {
            FireServerRpc(dir);
            _fireTimer = fireInterval;
        }
    }

    // 클라 → 서버 요청. 원점은 서버가 가진 플레이어 위치를 쓴다(위치 권위=서버 동기값).
    [ServerRpc]
    void FireServerRpc(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        Vector3 origin = transform.position + Vector3.up * aimHeight;

        // 높이 무시(2D/XZ) 빔 판정 — 캡슐 높이 차와 무관하게 명중.
        // 플레이어·유령을 바닥 평면(y=0)에 투영해, 전방 거리(along)와 조준선까지의 수직 거리(perp)로 판정.
        Vector3 p = transform.position; p.y = 0f;

        NetGhost best = null;
        float bestAlong = float.MaxValue;
        int candidates = 0;

        foreach (var g in FindObjectsByType<NetGhost>(FindObjectsSortMode.None))
        {
            Vector3 gp = g.transform.position; gp.y = 0f;
            Vector3 to = gp - p;
            float along = Vector3.Dot(to, dir);          // 전방 진행 거리
            if (along < 0f || along > range) continue;    // 뒤/사거리 밖
            Vector3 closest = p + dir * along;
            float perp = Vector3.Distance(gp, closest);   // 조준선까지 수직 거리
            if (perp > hitRadius + ghostRadius) continue; // 빔 밖

            // 벽 차폐(LOS): 총구~유령 사이에 벽이 있으면 안 맞음
            Vector3 ghostAtBeam = new Vector3(g.transform.position.x, origin.y, g.transform.position.z);
            if (BlockedByWall(origin, ghostAtBeam)) continue;

            candidates++;
            if (along < bestAlong) { bestAlong = along; best = g; }
        }

        Vector3 hitPoint = best != null
            ? new Vector3(best.transform.position.x, origin.y, best.transform.position.z)
            : origin + dir * range;

        if (best != null) best.TakeDamageServer(damage);

        if (debugLog)
            Debug.Log($"[NetShooter] fire dir={dir} range={range} 후보={candidates} 명중={(best != null ? "O" : "X")}");

        ShowTracerClientRpc(origin, hitPoint);
    }

    // 총구~대상 사이에 벽/지형이 있으면 true (유령·플레이어는 벽 아님).
    bool BlockedByWall(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float dist = d.magnitude;
        if (dist < 0.01f) return false;

        var hits = Physics.RaycastAll(from, d / dist, dist, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            var t = h.collider.transform;
            if (t.GetComponentInParent<NetGhost>() != null) continue;           // 유령 = 벽 아님
            if (t.GetComponentInParent<NetPlayerController>() != null) continue; // 플레이어(자신 포함) = 벽 아님
            return true; // 그 외 = 벽/지형
        }
        return false;
    }

    // 서버 → 전원. 트레이서 연출(순수 시각).
    [ClientRpc]
    void ShowTracerClientRpc(Vector3 from, Vector3 to)
    {
        NetTracer.Spawn(from, to);
    }
}
