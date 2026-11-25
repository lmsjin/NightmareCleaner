using UnityEngine;

/// <summary>
/// Mansion Clash 수치(통합 기획서 1.0의 5·6장). 거리 단위는 1유닛.
/// 에셋: Assets/NC/Resources/NCBalance.asset
/// </summary>
[CreateAssetMenu(fileName = "NCBalance", menuName = "NightmareCleaner/NC Balance")]
public class NCBalance : ScriptableObject
{
    [Header("테스트")]
    [Tooltip("켜면 혼자서도 시작할 수 있다. 2인 플레이 때는 반드시 끈다")]
    public bool soloTest = false;

    [Header("경기")]
    public float countdownSeconds = 3f;
    public float matchSeconds = 300f;
    public float dominancePerSecond = 1f;
    [Tooltip("중앙 점령 구역 반경")]
    public float captureRadius = 4f;
    public float messageSeconds = 2f;

    [Header("플레이어")]
    public int playerMaxHp = 100;
    public float playerMoveSpeed = 4.5f;
    [Tooltip("탄환 판정용 몸 반경")]
    public float playerRadius = 0.35f;

    [Header("회복·부활")]
    public float regenDelaySeconds = 7f;
    public float regenPerSecond = 10f;
    public float respawnSeconds = 5f;
    public float protectSeconds = 3f;

    [Header("사격 (투사체)")]
    public int shotDamage = 20;
    public float fireInterval = 0.3f;
    public int magazineSize = 12;
    public float reloadSeconds = 1.8f;
    public float shotRange = 9f;
    public float bulletSpeed = 14f;
    public float bulletRadius = 0.15f;

    [Header("과충전 (Q) — 탄약 무제한 + 발사 간격 감소")]
    public float overchargeSeconds = 8f;
    public float overchargeFireInterval = 0.2f;

    [Header("대쉬 (Space) — 무적 없음, 대쉬 중 사격 불가")]
    public float dashDistance = 4f;
    public float dashSeconds = 0.2f;
    [Tooltip("사용한 순간부터 다시 쓸 수 있을 때까지")]
    public float dashCooldownSeconds = 4f;

    [System.Serializable]
    public class GhostStats
    {
        public int count;
        public float hp;
        public float speed;
        public int damage;
        [Tooltip("공격 시작부터 다음 공격 시작까지(준비 포함)")]
        public float attackInterval;
        public float telegraph;
        [Tooltip("탄환 판정용 몸 반경")]
        public float radius;
        public float bodyScale;
    }

    [Header("유령 (캠프가 등장할 때 한 종류를 랜덤 선택)")]
    public GhostStats basic = new GhostStats { count = 1, hp = 480, speed = 3.2f, damage = 8, attackInterval = 1.2f, telegraph = 0.35f, radius = 0.45f, bodyScale = 1f };
    public GhostStats fast = new GhostStats { count = 1, hp = 480, speed = 4.5f, damage = 6, attackInterval = 1f, telegraph = 0.25f, radius = 0.4f, bodyScale = 0.8f };
    public GhostStats tank = new GhostStats { count = 1, hp = 480, speed = 2.5f, damage = 20, attackInterval = 1.8f, telegraph = 0.6f, radius = 0.65f, bodyScale = 1.5f };
    public float ghostDetectRange = 5f;
    public float ghostAttackRange = 1f;
    [Tooltip("맞아서 추적할 때 활동 영역 밖으로 쫓아가는 거리")]
    public float ghostChaseMargin = 4f;
    [Tooltip("캠프 전멸 후 재등장까지")]
    public float campRespawnSeconds = 40f;

    private static NCBalance _cached;

    public static NCBalance Get
    {
        get
        {
            if (_cached == null)
            {
                _cached = Resources.Load<NCBalance>("NCBalance");

                if (_cached == null)
                {
                    Debug.LogWarning("[NCBalance] Resources/NCBalance.asset 이 없어 기본값을 사용합니다.");
                    _cached = CreateInstance<NCBalance>();
                }
            }

            return _cached;
        }
    }

    public int RequiredPlayers => soloTest ? 1 : 2;

    public GhostStats GetGhost(NCGhostType type)
    {
        switch (type)
        {
            case NCGhostType.Fast: return fast;
            case NCGhostType.Tank: return tank;
            default: return basic;
        }
    }
}
