using UnityEngine;

/// <summary>
/// 2학기 통합 기획서(2026.09.29) 기준의 모든 수치를 한 곳에서 관리한다.
/// 기획서 11장 "거리와 지속 시간은 한곳의 설정 데이터로 관리한다" 요구 사항.
///
/// 기존 BalanceConfig(싱글플레이 레거시)와 분리한 이유:
/// 게임이 로그라이크 → 2인 협동 슈팅으로 재정의되어 수치 체계가 완전히 다르다.
///
/// 에셋 만드는 법: Project 창 우클릭 → Create → NightmareCleaner → CleanerBalance
/// 저장 위치: Assets/Resources/Configs/CleanerBalance.asset (Resolve가 자동으로 찾는 경로)
/// </summary>
[CreateAssetMenu(fileName = "CleanerBalance", menuName = "NightmareCleaner/CleanerBalance")]
public class CleanerBalance : ScriptableObject
{
    [Header("플레이어")]
    public int playerMaxHp = 100;
    public float playerMoveSpeed = 4.5f;
    [Tooltip("일반 피격 후 무적 시간(초)")]
    public float hitInvincibleSeconds = 0.5f;
    [Tooltip("구조/방 완료로 복귀할 때의 체력")]
    public int reviveHp = 40;
    [Tooltip("복귀 후 피해를 받지 않는 시간(초)")]
    public float reviveProtectSeconds = 2.0f;
    [Tooltip("다운된 동료를 구조할 수 있는 거리(m)")]
    public float reviveRange = 1.5f;
    [Tooltip("구조에 필요한 E 유지 시간(초)")]
    public float reviveHoldSeconds = 3.0f;

    [Header("사격 (히트스캔 + 구름탄 비주얼)")]
    public float shotDamage = 20f;
    public float fireInterval = 0.25f;
    public int magazineSize = 12;
    public float reloadSeconds = 1.8f;
    [Tooltip("최대 사거리(m)")]
    public float shotRange = 9f;

    [Header("유령 — 기본형 (3발 처치)")]
    public float basicHp = 60f;
    public float basicMoveSpeed = 2.5f;
    public int basicDamage = 15;
    [Tooltip("공격 준비(전조) 시간. 이 동안 멈춰서 플레이어에게 회피 기회를 준다")]
    public float basicTelegraphSeconds = 0.6f;
    [Tooltip("공격 실행 시점부터 다음 준비 시작까지의 최소 간격")]
    public float basicAttackInterval = 1.2f;

    [Header("유령 — 빠른형 (2발 처치)")]
    public float fastHp = 40f;
    public float fastMoveSpeed = 4.8f;
    public int fastDamage = 10;
    public float fastTelegraphSeconds = 0.5f;
    public float fastAttackInterval = 1.4f;

    [Header("유령 — 공통")]
    [Tooltip("공격 시작 거리 겸 피해 판정 거리(m). 기획서는 1m부터 테스트")]
    public float ghostAttackRange = 1.0f;
    [Tooltip("등장 후 이 시간 동안은 공격하지 않는다(가독성 확보)")]
    public float ghostSpawnSafeSeconds = 0.7f;

    [Header("공동 가구 정리 (기획서 5장)")]
    [Tooltip("가구 중심으로부터 상호작용 가능한 거리(m)")]
    public float furnitureInteractRange = 2.0f;
    [Tooltip("두 명이 함께 E를 유지해야 하는 시간(초)")]
    public float furnitureHoldSeconds = 2.0f;

    [Header("전투방")]
    [Tooltip("유령이 나타나기 전 출현 지점에 예고를 보여주는 시간(초)")]
    public float spawnWarningSeconds = 1.5f;
    [Tooltip("한 차수를 다 잡은 뒤 다음 차수가 시작되기까지의 간격(초)")]
    public float waveGapSeconds = 2.0f;

    [Header("게임 흐름")]
    [Tooltip("제한 시간(초). 기획서 10분")]
    public float missionSeconds = 600f;

    // Resources에서 한 번만 찾아두고 재사용한다.
    private static CleanerBalance _cached;

    /// <summary>
    /// Inspector에 직접 연결한 값이 있으면 그것을, 없으면
    /// Resources/Configs/CleanerBalance 를 자동으로 불러온다.
    /// </summary>
    public static CleanerBalance Resolve(CleanerBalance assigned)
    {
        if (assigned != null)
        {
            return assigned;
        }

        if (_cached == null)
        {
            _cached = Resources.Load<CleanerBalance>("Configs/CleanerBalance");

            if (_cached == null)
            {
                Debug.LogWarning(
                    "[CleanerBalance] Resources/Configs/CleanerBalance.asset 을 찾을 수 없습니다. " +
                    "각 스크립트의 Inspector 기본값이 사용됩니다.");
            }
        }

        return _cached;
    }
}
