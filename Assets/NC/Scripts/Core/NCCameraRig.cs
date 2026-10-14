using UnityEngine;

/// <summary>
/// 쿼터뷰 카메라. 내 캐릭터만 따라간다(로컬 연출, 네트워크와 무관). Main Camera에 붙인다.
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

    private void Follow()
    {
        if (_target == null)
        {
            return;
        }

        // 각도는 오프셋으로 정해지는 고정값이라 위치만 옮긴다(회전 흔들림 없음).
        transform.position = _target.position + offset;
        transform.rotation = Quaternion.LookRotation(Vector3.up * lookHeight - offset, Vector3.up);
    }
}
