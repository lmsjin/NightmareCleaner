using UnityEngine;

// =========================================================================
//  부트스트래퍼 — 빈 GameObject 하나에 이 컴포넌트만 붙이면
//  모듈 B/C의 런타임 매니저들이 코드로 자동 생성된다.
//  (사용자의 씬 세팅 부담을 컴포넌트 1개로 축소)
//
//  씬에 이미 같은 매니저가 있으면 중복 생성하지 않는다.
// =========================================================================
public class GameSystems : MonoBehaviour
{
    void Awake()
    {
        Ensure<SpawnPointRegistry>();
        Ensure<ScoreManager>();
        Ensure<GhostSpawnManager>();
        Ensure<AbilitySystem>();    // 능력 선택 UI (OnAbilityTrigger 수신)
        Ensure<AbilityApplier>();   // 선택된 능력 → 플레이어 스탯 반영
        // PHASE 3~4에서 ChildRescueSystem / RecordManager 등이 여기에 추가된다.
    }

    // 씬에 T가 없으면 이 오브젝트에 붙여 생성
    void Ensure<T>() where T : Component
    {
        if (FindFirstObjectByType<T>() == null)
            gameObject.AddComponent<T>();
    }
}
