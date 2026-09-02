using Unity.Netcode;
using UnityEngine;

// [멀티] 소유자 로컬 조준 연출 — 상시 조준 레이저(총구→조준점) + 지면 동그란 레티클.
// 네트워크 동기화 없음: 각 클라가 "자기 캐릭터"에만 그린다(IsOwner). 싱글 VacuumController의 조준 연출을 이식.
// ※ 플레이어 프리팹에 추가.
[RequireComponent(typeof(NetworkObject))]
public class NetAimVisual : NetworkBehaviour
{
    [SerializeField] float aimHeight     = 0.7f;                       // 레이저 높이(= 조준 평면 높이)
    [SerializeField] float range         = 12f;                        // 최대 길이(BalanceConfig.suctionRange)
    [SerializeField] Color laserColor    = new Color(1f, 0.3f, 0.2f, 0.9f);
    [SerializeField] float laserWidth    = 0.05f;
    [SerializeField] Color reticleColor  = new Color(1f, 0.3f, 0.2f, 0.9f);
    [SerializeField] float reticleRadius = 0.4f;
    [SerializeField] float reticleWidth  = 0.05f;

    NetPlayerController _player;
    NetPlayerHealth     _health;
    LineRenderer _laser;
    LineRenderer _reticle;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return; // 내 조준선만 내가 그림
        _player = GetComponent<NetPlayerController>();
        _health = GetComponent<NetPlayerHealth>();

        var cfg = BalanceConfig.Resolve(null);
        if (cfg != null) range = cfg.suctionRange;

        _laser   = MakeLine("AimLaser", 2, laserColor, laserWidth, laserWidth * 0.5f, false);
        _reticle = MakeLine("AimReticle", 24, reticleColor, reticleWidth, reticleWidth, true);
    }

    void LateUpdate()
    {
        if (!IsOwner || _laser == null) return;

        bool show = _player != null && _player.HasAim
                    && (_health == null || !_health.Dead.Value);
        _laser.enabled   = show;
        _reticle.enabled = show;
        if (!show) return;

        // 레이저: 총구 높이에서 조준점까지 완전 수평(바닥에 안 박힘)
        Vector3 origin = transform.position + Vector3.up * aimHeight;
        Vector3 aim = _player.AimPoint;
        Vector3 dir = aim - origin; dir.y = 0f;
        float dist = Mathf.Min(dir.magnitude, range);
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

        _laser.SetPosition(0, origin);
        _laser.SetPosition(1, origin + dir * dist);

        // 레티클: 조준점(마우스 지면 지점)에 동그란 링
        Vector3 center = new Vector3(aim.x, aim.y, aim.z);
        int seg = _reticle.positionCount;
        for (int i = 0; i < seg; i++)
        {
            float a = (i / (float)seg) * Mathf.PI * 2f;
            _reticle.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * reticleRadius);
        }
    }

    LineRenderer MakeLine(string name, int points, Color color, float w0, float w1, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.positionCount = points;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = w0;
        lr.endWidth = w1;
        lr.enabled = false;
        return lr;
    }
}
