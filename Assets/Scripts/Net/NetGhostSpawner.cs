using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// [멀티 슬라이스] 서버(호스트)만 유령을 스폰한다. 씬의 빈 오브젝트에 붙이고 ghostPrefab을 연결.
// 인원수에 따라 최대 유령 수가 늘고(maxGhosts = baseMax + perPlayer × 접속인원), 인터벌마다 랜덤 종류로 채운다.
// ※ ghostPrefab은 NetworkManager의 Network Prefabs 목록에 등록돼 있어야 함(프리팹은 1개, 종류는 Kind로 표현).
public class NetGhostSpawner : MonoBehaviour
{
    [SerializeField] GameObject ghostPrefab; // NetGhost + NetworkObject 있는 프리팹(1개)
    [SerializeField] int   baseMax   = 2;    // 기본 최대 유령 수
    [SerializeField] int   perPlayer = 2;    // 접속 1명당 추가 최대치
    [SerializeField] float interval  = 2f;   // 스폰 간격(초)
    [SerializeField] float radius    = 8f;   // 스폰 반경

    readonly List<NetworkObject> _alive = new();
    float _timer;

    void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer || ghostPrefab == null) return;

        _alive.RemoveAll(o => o == null); // 죽은(Despawn된) 유령 정리

        int playerCount = Mathf.Max(1, nm.ConnectedClientsIds.Count);
        int maxGhosts = baseMax + perPlayer * playerCount;
        if (_alive.Count >= maxGhosts) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = interval;

        SpawnOne();
    }

    void SpawnOne()
    {
        Vector2 c = Random.insideUnitCircle * radius;
        Vector3 pos = transform.position + new Vector3(c.x, 0f, c.y);

        // NavMesh가 있으면 스폰 지점을 메시 위로 스냅(agent는 메시 위에서만 동작).
        // 메시가 없으면(Bake 안 함) 그대로 두고 유령은 직선 이동 폴백.
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            pos = hit.position;

        GameObject go = Instantiate(ghostPrefab, pos, Quaternion.identity);
        var kind = (NetGhost.GhostKind)Random.Range(0, 5); // 5종 중 랜덤
        go.GetComponent<NetGhost>().ServerInit(kind);

        var netObj = go.GetComponent<NetworkObject>();
        netObj.Spawn(true);
        _alive.Add(netObj);
    }
}
