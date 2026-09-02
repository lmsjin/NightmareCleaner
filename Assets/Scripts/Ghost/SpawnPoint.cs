using UnityEngine;

// 창문·벽장·변기 위치에 빈 GameObject로 두고 이 컴포넌트를 붙이면
// 그 "정확한 지점"에서 유령이 스폰된다(범위가 아니라 점 → 옆 방으로 새지 않음).
// zone 라벨이 그 지점에서 나올 유령 종류를 결정한다.
// 하나도 안 두면 GhostSpawnManager가 플레이어 주변 랜덤(젤리)으로 폴백한다.
public class SpawnPoint : MonoBehaviour
{
    [Tooltip("이 스폰포인트가 속한 방 — 나올 유령 종류를 결정")]
    public GhostZone zone = GhostZone.Living;

    void Start()
    {
        if (SpawnPointRegistry.Instance != null)
            SpawnPointRegistry.Instance.Register(transform);
    }

    void OnDestroy()
    {
        if (SpawnPointRegistry.Instance != null)
            SpawnPointRegistry.Instance.Unregister(transform);
    }

    // 에디터에서 위치+구역이 색으로 보이도록 (거실=초록, 주방=빨강, 창고=회색)
    void OnDrawGizmos()
    {
        switch (zone)
        {
            case GhostZone.Kitchen: Gizmos.color = new Color(0.9f, 0.4f, 0.35f, 0.9f); break;
            case GhostZone.Storage: Gizmos.color = new Color(0.6f, 0.6f, 0.65f, 0.9f); break;
            default:                Gizmos.color = new Color(0.5f, 0.85f, 0.5f, 0.9f); break;
        }
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
    }
}
