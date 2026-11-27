using UnityEngine;

/// <summary>
/// 쿼터뷰 카메라. 내 캐릭터를 따라간다(로컬 연출, 네트워크와 무관). Main Camera에 붙인다.
/// 내가 쓰러져 있는 동안에는 spectateDelaySeconds 뒤부터 상대를 따라가고(관전), 부활하면 돌아온다.
///
/// 스무딩을 넣지 않고 "보간된 캐릭터 위치 + 고정 오프셋"에 바로 붙인다.
/// Fusion은 Tick(60Hz)마다 위치를 확정하고 화면에서는 보간해 보여주는데, 카메라에 한 번 더 스무딩을 걸면
/// 프레임과 Tick이 어긋날 때 카메라가 한 박자씩 밀려 "툭" 끊겨 보이기 때문이다.
/// </summary>
public class NCCameraRig : MonoBehaviour
{
    private static NCCameraRig _instance;
    private static Transform _pendingTarget;

    [SerializeField] private Vector3 offset = new Vector3(0f, 13f, -8.5f);
    [Tooltip("카메라가 바라보는 지점의 높이(캐릭터 발 기준)")]
    [SerializeField] private float lookHeight = 0.8f;

    private Transform _target;

    public static void SetTarget(Transform target)
    {
        _pendingTarget = target;

        if (_instance != null)
        {
            _instance._target = target;
            _instance.Follow();
        }
    }

    private void Awake()
    {
        _instance = this;
        _target = _pendingTarget;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void LateUpdate()
    {
        Follow();
    }

    /// <summary>지금 상대를 관전 중인지(HUD 안내용).</summary>
    public static bool IsSpectating { get; private set; }

    private void Follow()
    {
        if (_target == null)
        {
            IsSpectating = false;
            return;
        }

        NCPlayer opponent = SpectateTarget();
        IsSpectating = opponent != null;
        Transform follow = IsSpectating ? opponent.transform : _target;

        // 각도는 오프셋으로 정해지는 고정값이라 위치만 옮긴다(회전 흔들림 없음).
        transform.position = follow.position + offset;
        transform.rotation = Quaternion.LookRotation(Vector3.up * lookHeight - offset, Vector3.up);
    }

    /// <summary>내가 쓰러진 지 지연 시간이 지났으면 따라갈 상대. 아니면 null.</summary>
    private static NCPlayer SpectateTarget()
    {
        NCPlayer me = NCPlayer.Local;
        if (me == null || me.Object == null || me.Object.IsValid == false || me.Health.IsDown == false)
        {
            return null;
        }

        NCBalance balance = NCBalance.Get;
        if (me.Health.RespawnRemaining > balance.respawnSeconds - balance.spectateDelaySeconds)
        {
            return null;
        }

        foreach (NCPlayer player in NCPlayer.All)
        {
            if (player != me && player.Object != null && player.Object.IsValid)
            {
                return player;
            }
        }

        return null;
    }
}
