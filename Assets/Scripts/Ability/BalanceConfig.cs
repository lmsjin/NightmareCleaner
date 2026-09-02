using UnityEngine;

[CreateAssetMenu(fileName = "BalanceConfig", menuName = "NightmareCleaner/BalanceConfig")]
public class BalanceConfig : ScriptableObject
{
    [Header("게임 흐름")]
    public float gameDurationSeconds = 600f;

    [Header("플레이어 기본 스탯")]
    public int playerMaxHP = 5;
    public float playerMoveSpeed = 5f;

    [Header("사격 — 기본 스탯(스탯 재사용)")]
    [Tooltip("발당 데미지 — 유령 HP를 깎는 양(히트스캔 1발). 능력 강화 SuctionPower가 이 값을 올림")]
    public float suctionPower = 10f;
    [Tooltip("사격 사거리(레이 길이). 능력 강화 SuctionRange가 이 값을 올림")]
    public float suctionRange = 12f;
    [Tooltip("(사격 미사용) 옛 흡입 부채꼴 각도")]
    public float suctionAngle = 55f;

    [Header("(사격 미사용) 옛 흡입 당김 연출")]
    public float suctionPullSpeed = 4f;
    public float suctionPullResponsiveness = 3f;
    public float suctionSettleRadius = 1.2f;

    [Header("사격 — 발사/탄약")]
    [Tooltip("연사 간격(초). 좌클릭 홀드 시 이 간격마다 1발")]
    public float fireInterval = 0.15f;
    [Tooltip("탄창 크기(장전 1회 최대 탄약 수)")]
    public float magazineSize = 12f;
    [Tooltip("재장전 소요 시간(초)")]
    public float reloadSeconds = 1.2f;
    [Tooltip("트레이서 탄환이 총구→명중점까지 이동하는 속도(m/s). 클수록 즉발처럼 보임")]
    public float bulletTracerSpeed = 60f;
    [Tooltip("명중 시 유령을 밀어내는 넉백 세기")]
    public float ghostKnockbackForce = 6f;

    [Header("흡입 게이지(과부하) — 탄약/재장전으로 전용")]
    [Tooltip("(구) 게이지 상한. 현재 탄창은 magazineSize 사용")]
    public float suctionMaxSeconds = 3f;
    [Tooltip("초당 게이지 회복량 (사격 미사용)")]
    public float suctionRegenPerSec = 1f;
    [Tooltip("손 뗀 뒤 회복 시작까지 딜레이(초) (사격 미사용)")]
    public float suctionRegenDelay = 0.5f;
    [Tooltip("과부하 지속 시간(초) (사격 미사용)")]
    public float suctionOverheatSeconds = 2f;
    [Tooltip("아이 1명 구출당 탄창 상한 증가(발)")]
    public float suctionRescueBonus = 1f;

    [Header("피격 반응")]
    [Tooltip("피격 후 무적 지속 시간(초)")]
    public float invincibleSeconds = 1f;
    [Tooltip("피격 시 넉백 세기")]
    public float knockbackForce = 6f;

    [Header("능력치 증가폭 (1회 선택당)")]
    public float suctionPowerBonus = 3f;
    [Tooltip("능력 선택 1회당 흡입 거리 증가량")]
    public float suctionRangeBonus = 1f;
    public float moveSpeedBonus = 1f;

    public float GetAbilityBonus(AbilityType type)
    {
        return type switch
        {
            AbilityType.SuctionPower => suctionPowerBonus,
            AbilityType.SuctionRange => suctionRangeBonus,
            AbilityType.MoveSpeed => moveSpeedBonus,
            _ => 0f
        };
    }

    [Header("점수")]
    public int scorePerGhostKill = 10;
    public int scoreChildRescue = 100;
    public int scoreChildBonus = 50;
    public float retryPenaltyRate = 0.3f;

    [Header("미니게임")]
    public float miniGameTimeLimit = 5f;
    public int keyMashCount = 10;
    [Tooltip("미니게임 시작 전 준비 카운트다운 시간(초)")]
    public float miniGameCountdownSeconds = 1.5f;
    [Tooltip("게이지스톱: 0단계 중앙 성공 구간의 폭(0~1 비율)")]
    public float gaugeStopSuccessWidth = 0.18f;
    [Tooltip("게이지스톱: 0단계 슬라이더 왕복 속도")]
    public float gaugeStopSpeed = 1.2f;
    [Tooltip("게이지스톱: 다단계 전체 제한시간(초)")]
    public float gaugeStopTimeLimit = 15f;
    [Tooltip("게이지스톱: 단계당 마커 속도 증가량")]
    public float gaugeStopSpeedStep = 0.15f;
    [Tooltip("게이지스톱: 단계당 성공구간 폭 배율")]
    public float gaugeStopWidthShrink = 0.88f;
    [Tooltip("게이지스톱: 성공구간 최소 폭(0~1)")]
    public float gaugeStopMinWidth = 0.05f;

    [Header("미니게임 — 방향키 순서입력(시퀀스)")]
    [Tooltip("순서입력 게임 전체 제한시간(초)")]
    public float sequenceTimeLimit = 15f;
    [Tooltip("첫 시퀀스 길이(방향키 개수)")]
    public int sequenceStartLength = 4;
    [Tooltip("이 단계 수마다 시퀀스 길이 +1")]
    public int sequenceLengthStep = 2;
    [Tooltip("클리어한 단계 1개당 추가 구출 점수")]
    public int scorePerSequenceStage = 30;

    [Header("유령 스탯 — 젤리(일반)")]
    public float jellyHP = 10f;
    public float jellySpeed = 3f;
    public int jellyDamage = 1;

    [Header("유령 스탯 — 통통(탱커)")]
    public float chubbyHP = 20f;
    public float chubbySpeed = 1f;
    public int chubbyDamage = 3;

    [Header("유령 스탯 — 홀쭉(속공)")]
    public float slimHP = 5f;
    public float slimSpeed = 5f;
    public int slimDamage = 2;

    [Header("유령 스탯 — 뚝딱이(특별★)")]
    public float repairHP = 50f;
    public float repairSpeed = 4f;
    public int repairDamage = 5;
    public float repairDashSpeed = 14f;
    public float repairDashCooldown = 5f;
    public float repairPostDashStun = 2f;

    [Header("유령 스탯 — 요리사(특별★)")]
    public float chefHP = 40f;
    public float chefSpeed = 2f;
    public int chefDamage = 4;

    [Header("요리사 — 뜨거운 국물")]
    [Tooltip("국물 1개의 반지름 (단위: m). 클수록 넓은 웅덩이")]
    public float chefSoupRadius = 1.2f;
    [Tooltip("국물이 바닥에 깔리는 Y 오프셋. 0이면 NavMesh 높이 그대로")]
    public float chefSoupGroundOffset = 0.02f;
    [Tooltip("국물 1개 지속 시간(초)")]
    public float chefSoupLifetime = 5f;
    [Tooltip("국물 생성 간격(m). 유령이 이만큼 이동할 때마다 1개 생성")]
    public float chefSoupSpawnInterval = 1.2f;
    [Tooltip("Trailing 1회당 생성하는 국물 개수")]
    public int chefSoupCount = 6;
    [Tooltip("국물 소멸 후 다음 Trailing까지 추가 대기(초). 총 쿨타임 = Lifetime + 이 값")]
    public float chefSoupPostDelay = 1f;
    [Tooltip("국물 위를 밟았을 때 피해 간격(초). 너무 짧으면 매 프레임 피해")]
    public float chefSoupDamageInterval = 0.5f;

    [Header("스폰")]
    [Tooltip("게임 시작 시 스폰 간격(초) — 가장 느린 속도")]
    public float spawnIntervalSeconds = 5f;
    [Tooltip("게임 종료 시 스폰 간격(초) — 가장 빠른 속도. 시작~종료 사이를 선형 보간")]
    public float spawnIntervalMin = 1f;
    [Tooltip("한 번 스폰 타이머가 돌 때 소환할 유령의 고정 수치")]
    public int spawnCountEachTime = 1;
    [Tooltip("게임 시작 직후 맵에 존재할 수 있는 최대 유령 수")]
    public float maxGhostCountStart = 3f;
    [Tooltip("게임 종료 시점 맵에 존재할 수 있는 최대 유령 수")]
    public float maxGhostCountMax = 8f;
    [Tooltip("스폰 포인트가 없을 때 플레이어 주변 원형 스폰 반경")]
    public float spawnRadius = 10f;
    [Range(0f, 1f)]
    [Tooltip("Kitchen — 게임 시작 시 스폰 확률")]
    public float kitchenSpawnChanceStart = 0.2f;
    [Range(0f, 1f)]
    [Tooltip("Kitchen — 게임 종료 시 스폰 확률")]
    public float kitchenSpawnChanceEnd = 0.8f;
    [Range(0f, 1f)]
    [Tooltip("Storage — 게임 시작 시 스폰 확률")]
    public float storageSpawnChanceStart = 0.2f;
    [Range(0f, 1f)]
    [Tooltip("Storage — 게임 종료 시 스폰 확률")]
    public float storageSpawnChanceEnd = 0.8f;
    [Tooltip("시간(초)마다 한 단계씩 증가")]
    public float stageStepSeconds = 60f; //  

    [Header("아이 / 능력 트리거")]
    [Tooltip("동시에 맵에 존재하는 최대 아이 수")]
    public int childCount = 3;
    [Tooltip("아이 1명 스폰 주기(초)")]
    public float childSpawnInterval = 10f;
    [Tooltip("Child spawn NavMesh search radius")]
    public float childSpawnSearchRadius = 1.5f;
    [Tooltip("Required empty radius around a child spawn")]
    public float childSpawnClearanceRadius = 0.35f;
    [Tooltip("Required empty height around a child spawn")]
    public float childSpawnClearanceHeight = 1.2f;
    [Tooltip("Gap between the floor and the child spawn volume")]
    public float childSpawnGroundOffset = 0.05f;
    [Tooltip("아이 'E 구출' 인식 거리")]
    public float childInteractRange = 2.5f;
    [Tooltip("(미사용) N마리 처치마다 트리거")]
    public int abilityTriggerKillCount = 5;

    [Header("오디오(BGM)")]
    [Range(0f, 1f)]
    [Tooltip("BGM 기본 볼륨 — 마스터 볼륨(옵션 슬라이더)과 별도로 곱해진다")]
    public float bgmVolume = 0.6f;
    [Tooltip("곡 전환 시 페이드(크로스페이드) 시간(초). 0이면 즉시 전환")]
    public float bgmFadeSeconds = 0.5f;

    [Header("미니맵")]
    [Tooltip("미니맵 가장자리에 해당하는 월드 거리(반경). 작을수록 더 확대돼 보임")]
    public float minimapWorldRadius = 18f;
    [Tooltip("탑다운 지형 카메라가 플레이어 위로 떨어져 있는 높이(미터)")]
    public float minimapCameraHeight = 40f;

    [Header("능력 강화 — 경험치(XP)")]
    [Tooltip("일반 유령 처치 시 얻는 XP")]
    public int xpBasicGhost = 1;
    [Tooltip("특수 유령 처치 시 얻는 XP")]
    public int xpSpecialGhost = 5;
    [Tooltip("아이 1명 구출 시 얻는 XP")]
    public int xpChildRescue = 10;
    [Tooltip("1레벨업에 필요한 XP")]
    public int xpFirstThreshold = 8;
    [Tooltip("레벨업마다 필요 XP에 곱해지는 배율")]
    public float xpThresholdGrowth = 1.4f;

    // ---- 공용 로더 ----
    private static BalanceConfig _cached;
    public static BalanceConfig Resolve(BalanceConfig assigned)
    {
        if (assigned != null) return assigned;
        if (_cached == null) _cached = Resources.Load<BalanceConfig>("Configs/BalanceConfig");
        return _cached;
    }
}
