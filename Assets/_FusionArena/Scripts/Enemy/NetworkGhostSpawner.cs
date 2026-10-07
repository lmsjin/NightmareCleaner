using Fusion;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 테스트용 최소 스폰너. Host만 유령을 생성한다.
///
/// 기획서의 정식 스폰(방별 고정 목록 · 2차수 · 동시 상한 4~6 · 1.5초 출현 예고)은
/// 3~4주차 "전투방" 작업에서 만든다. 지금은 **1~2주차 통과 조건**인
/// "임시 방 1개에서 유령을 쏴서 처치"를 확인하기 위한 단순 버전이다.
///
/// ⚠️ 정식 전투는 NetworkCombatRoom이 담당한다. 전투방을 쓰는 씬에서는 이 컴포넌트를 꺼둘 것
///    (둘 다 켜져 있으면 유령이 두 군데서 생성된다). 무한 생성 테스트가 필요할 때만 켠다.
/// </summary>
public class NetworkGhostSpawner : SimulationBehaviour
{
    [Header("생성할 유령")]
    [SerializeField] private NetworkPrefabRef ghostPrefab;

    [Header("전투 시작 조건")]
    [Tooltip("연결하면 이 가구의 공동 정리가 끝나기 전엔 유령을 만들지 않는다. 비워두면 즉시 생성(테스트용)")]
    [SerializeField] private NetworkFurniture triggerFurniture;

    [Header("생성 중심")]
    [Tooltip("비워두면 이 오브젝트 위치를 쓴다. " +
             "이 스크립트는 NetworkManager(Runner와 같은 오브젝트)에 붙여야 하므로, " +
             "방 안의 빈 오브젝트를 만들어 여기에 연결하는 것을 권장한다.")]
    [SerializeField] private Transform spawnCenter;

    [Header("테스트 설정")]
    [Tooltip("동시에 유지할 유령 수")]
    [SerializeField] private int aliveTarget = 4;

    [Tooltip("생성 간격(초)")]
    [SerializeField] private float spawnInterval = 2.0f;

    [Tooltip("이 오브젝트를 중심으로 한 생성 반경(m)")]
    [SerializeField] private float spawnRadius = 6.0f;

    [Tooltip("빠른형이 나올 확률(0~1). 기획서 방별 구성은 3~4주차에 반영")]
    [Range(0f, 1f)]
    [SerializeField] private float fastRatio = 0.3f;

    [Header("안전장치 — 이상한 곳에 스폰되는 것 방지")]
    [Tooltip("무작위 지점에서 NavMesh를 찾을 검색 반경(m). 크면 벽/천장 위로 끌려간다")]
    [SerializeField] private float sampleRadius = 1.5f;

    [Tooltip("생성 중심과의 허용 높이 차(m). 이보다 높거나 낮으면 버린다")]
    [SerializeField] private float maxSpawnHeightDiff = 2.0f;

    [Tooltip("유효한 지점을 찾기 위한 최대 시도 횟수")]
    [SerializeField] private int maxSpawnAttempts = 10;

    // NavMesh가 아예 없을 때 경고를 한 번만 찍기 위한 플래그
    private bool _warnedNoNavMesh;

    private float _timer;

    public override void FixedUpdateNetwork()
    {
        // 생성은 Host만 한다.
        if (Runner == null || Runner.IsServer == false || ghostPrefab == default)
        {
            return;
        }

        // 가구 정리가 끝나야 유령이 나온다(기획서: 정리 → 등장 → 전투).
        if (triggerFurniture != null && triggerFurniture.IsRestored == false)
        {
            return;
        }

        // 현재 살아 있는 유령 수를 센다.
        NetworkGhost[] alive = FindObjectsByType<NetworkGhost>(FindObjectsSortMode.None);
        if (alive.Length >= aliveTarget)
        {
            return;
        }

        _timer -= Runner.DeltaTime;
        if (_timer > 0f)
        {
            return;
        }

        _timer = spawnInterval;
        SpawnOne();
    }

    private void SpawnOne()
    {
        // 생성 중심을 정한다(지정 안 했으면 이 오브젝트 위치).
        Vector3 center = spawnCenter != null ? spawnCenter.position : transform.position;

        if (TryFindSpawnPoint(center, out Vector3 position) == false)
        {
            // 이번 틱은 포기하고 다음 틱에 다시 시도한다.
            return;
        }

        NetworkObject spawned = Runner.Spawn(ghostPrefab, position, Quaternion.identity);

        // 종류를 정해준다.
        NetworkGhost ghost = spawned.GetComponent<NetworkGhost>();
        if (ghost != null)
        {
            ghost.ServerSetType(Random.value < fastRatio ? GhostType.Fast : GhostType.Basic);
        }
    }

    /// <summary>
    /// 바닥에 있고, 중심에서 실제로 걸어갈 수 있는 생성 지점을 찾는다.
    ///
    /// 벽 위·천장처럼 "떨어져 있는 NavMesh 섬"에 스폰되는 것을 막기 위해
    /// ① 높이 차 ② 경로 도달 가능성 두 가지를 모두 검사한다.
    /// </summary>
    private bool TryFindSpawnPoint(Vector3 center, out Vector3 result)
    {
        result = center;

        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            // 중심 주변 무작위 지점
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = center + new Vector3(circle.x, 0f, circle.y);

            // 가까운 NavMesh 위로 스냅한다. 반경을 좁게 둬야 엉뚱한 층으로 끌려가지 않는다.
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas) == false)
            {
                continue;
            }

            // ① 높이 검사 — 중심(바닥)과 크게 차이 나면 벽 위나 천장이다.
            if (Mathf.Abs(hit.position.y - center.y) > maxSpawnHeightDiff)
            {
                continue;
            }

            // ② 도달 가능성 검사 — 중심에서 그 지점까지 실제로 걸어갈 수 있어야 한다.
            //    끊긴 NavMesh 섬(천장·벽 위)은 여기서 전부 걸러진다.
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(center, hit.position, NavMesh.AllAreas, path) == false ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            result = hit.position;
            return true;
        }

        WarnNoNavMeshOnce(center);
        return false;
    }

    /// <summary>
    /// 계속 실패하면 NavMesh가 바닥에 구워지지 않았을 가능성이 크다. 한 번만 알린다.
    /// </summary>
    private void WarnNoNavMeshOnce(Vector3 center)
    {
        if (_warnedNoNavMesh)
        {
            return;
        }

        _warnedNoNavMesh = true;

        Debug.LogWarning(
            $"[NetworkGhostSpawner] 생성 중심 {center} 주변에서 걸어갈 수 있는 NavMesh를 찾지 못했습니다. " +
            "① Spawn Center가 방 바닥 위에 있는지 ② 바닥에 NavMesh가 구워졌는지(천장에만 생기지 않았는지) 확인하세요.");
    }
}
