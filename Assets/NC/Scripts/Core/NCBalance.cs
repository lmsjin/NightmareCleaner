using UnityEngine;

/// <summary>
/// 기획 수치를 한곳에서 관리한다(통합 기획서 v7 + UI 기획서 기준).
/// 거리 단위는 1유닛 = 1m.
///
/// 에셋 위치: Assets/NC/Resources/NCBalance.asset (빌더가 자동 생성, Resolve가 자동으로 찾는다)
/// </summary>
[CreateAssetMenu(fileName = "NCBalance", menuName = "NightmareCleaner/NC Balance")]
public class NCBalance : ScriptableObject
{
    [Header("테스트")]
    [Tooltip("켜면 혼자서도 의뢰 시작·공동 정리가 된다. 2인 테스트 때는 반드시 끈다")]
    public bool soloTest = false;

    [Header("게임 흐름")]
    [Tooltip("제한 시간(초)")]
    public float missionSeconds = 600f;
    [Tooltip("중앙홀 시작 영역 반경(m)")]
    public float startZoneRadius = 2.5f;
    [Tooltip("일반 안내 표시 시간(초)")]
    public float messageSeconds = 2f;

    [Header("플레이어")]
    public int playerMaxHp = 100;
    public float playerMoveSpeed = 4.5f;
    [Tooltip("피격 후 무적 시간(초)")]
    public float hitInvincibleSeconds = 0.5f;

    [Header("다운·구조")]
    public int reviveHp = 40;
    [Tooltip("구조 직후 피해를 받지 않는 시간(초)")]
    public float reviveProtectSeconds = 2f;
    public float reviveRange = 1.5f;
    public float reviveHoldSeconds = 3f;

    [Header("사격 (히트스캔 + 구름탄 연출)")]
    public float shotDamage = 20f;
    public float fireInterval = 0.25f;
    public int magazineSize = 12;
    public float reloadSeconds = 1.8f;
    public float shotRange = 9f;
    [Tooltip("UI 기획서: 빈 탄창 자동 재장전은 규칙 확정 전 추가하지 않는다 → 기본 끔. 원 기획서는 자동")]
    public bool autoReloadWhenEmpty = false;
    [Tooltip("조준선 판정 두께(m). 클수록 맞히기 쉽다")]
    public float beamRadius = 0.3f;

    [Header("과충전 (Q)")]
    [Tooltip("과충전 효과 시간(초) — 탄약 무소모 + 빠른 연사")]
    public float overchargeSeconds = 5f;
    [Tooltip("과충전 중 사격 간격(초). 평소 fireInterval보다 짧게")]
    public float overchargeFireInterval = 0.18f;

    [Header("보급함")]
    [Tooltip("보급함 사용 거리(m)")]
    public float supplyRange = 1.5f;
    [Tooltip("보급 1회 회복량(두 명 모두)")]
    public int supplyHeal = 30;

    [Header("유령 — 기본형")]
    public float basicHp = 60f;
    public float basicMoveSpeed = 2.5f;
    public int basicDamage = 15;
    public float basicTelegraphSeconds = 0.6f;
    public float basicAttackInterval = 1.2f;

    [Header("유령 — 빠른형")]
    public float fastHp = 40f;
    public float fastMoveSpeed = 4.8f;
    public int fastDamage = 10;
    public float fastTelegraphSeconds = 0.5f;
    public float fastAttackInterval = 1.4f;

    [Header("유령 — 공통")]
    [Tooltip("공격 시작·피해 판정 거리(m, 중심 간 거리)")]
    public float ghostAttackRange = 1.1f;
    [Tooltip("등장 후 공격하지 않는 시간(초)")]
    public float ghostSpawnSafeSeconds = 0.7f;
    [Tooltip("명중 판정용 유령 몸 반경(m)")]
    public float ghostRadius = 0.45f;

    [Header("전투방")]
    [Tooltip("잡동사니 더미 상호작용 거리(m)")]
    public float dummyRange = 2f;
    [Tooltip("두 명이 함께 E를 유지해야 하는 시간(초)")]
    public float dummyHoldSeconds = 2f;
    [Tooltip("유령 출현 예고 시간(초)")]
    public float spawnWarningSeconds = 1.5f;
    [Tooltip("한 차수 전멸 후 다음 차수 예고까지의 간격(초)")]
    public float waveGapSeconds = 1.0f;

    [Header("기록")]
    [Tooltip("규칙(수치)이 바뀌면 버전을 올려 이전 기록과 비교하지 않는다")]
    public string recordKey = "NC_BestRecord_v1";

    private static NCBalance _cached;

    /// <summary>Resources/NCBalance 를 불러온다. 없으면 기본값으로 만든 임시 인스턴스를 쓴다.</summary>
    public static NCBalance Get
    {
        get
        {
            if (_cached == null)
            {
                _cached = Resources.Load<NCBalance>("NCBalance");

                if (_cached == null)
                {
                    Debug.LogWarning("[NCBalance] Resources/NCBalance.asset 이 없어 기본값을 사용합니다. " +
                                     "메뉴 NightmareCleaner → Build NC Scene 을 실행하세요.");
                    _cached = CreateInstance<NCBalance>();
                }
            }

            return _cached;
        }
    }

    /// <summary>의뢰 시작·공동 정리에 필요한 인원.</summary>
    public int RequiredPlayers => soloTest ? 1 : 2;
}
