using UnityEngine;

/// <summary>
/// 쿼터뷰 카메라. 내 캐릭터만 부드럽게 따라간다(로컬 연출, 네트워크와 무관).
/// Main Camera에 붙인다.
/// </summary>
public class NCCameraRig : MonoBehaviour
{
    private static NCCameraRig _instance;
    private static Transform _pendingTarget;

    [SerializeField] private Vector3 offset = new Vector3(0f, 13f, -8.5f);
    [SerializeField] private float followSmoothTime = 0.12f;

    private Transform _target;
    private Vector3 _velocity;

    public static void SetTarget(Transform target)
    {
        _pendingTarget = target;

        if (_instance != null)
        {
            _instance._target = target;
            _instance.SnapToTarget();
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

    private void SnapToTarget()
    {
        if (_target == null)
        {
            return;
        }

        transform.position = _target.position + offset;
        transform.LookAt(_target.position + Vector3.up * 0.8f);
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        Vector3 desired = _target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, followSmoothTime);
        transform.rotation = Quaternion.LookRotation(_target.position + Vector3.up * 0.8f - transform.position, Vector3.up);
    }
}
