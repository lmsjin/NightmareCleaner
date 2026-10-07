using UnityEngine;  // Camera, Transform, Vector3, LateUpdate 등을 사용한다.

/// <summary>
/// 로컬 플레이어를 부드럽게 따라가는 간단한 3인칭/탑다운 카메라 스크립트이다.
///
/// 카메라는 네트워크 상태가 아니다.
/// 각 실행 프로그램이 자기 플레이어를 따로 바라보면 되므로
/// Fusion의 NetworkBehaviour가 아니라 일반 MonoBehaviour를 사용한다.
/// </summary>
public class LocalCameraFollow : MonoBehaviour
{
    [Header("카메라 위치 설정")]
    [SerializeField]
    private Vector3 offset = new Vector3(0.0f, 8.0f, -7.0f); // 플레이어 기준 카메라 상대 위치

    [SerializeField]
    private float followSpeed = 8.0f; // 값이 클수록 플레이어를 빠르게 따라간다.

    [SerializeField]
    private float lookHeight = 1.0f;  // 플레이어의 발이 아니라 몸통 쪽을 바라보기 위한 높이

    // 현재 이 카메라가 따라갈 로컬 플레이어 Transform이다.
    // 네트워크 플레이어가 Spawn된 뒤 LocalCameraBinder가 이 값을 지정한다.
    private Transform _target;

    /// <summary>
    /// 카메라가 따라갈 플레이어를 지정한다.
    /// 로컬 플레이어가 Spawn될 때 한 번 호출한다.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        _target = newTarget;

        // 처음 지정될 때는 카메라가 멀리서 천천히 이동하지 않도록 즉시 목표 위치로 옮긴다.
        if (_target != null)
        {
            SnapToTarget();
        }
    }

    /// <summary>
    /// 모든 Update 처리가 끝난 뒤 호출된다.
    /// 플레이어가 먼저 이동한 뒤 카메라가 따라가면 화면 떨림을 줄이기 쉽다.
    /// </summary>
    private void LateUpdate()
    {
        // 아직 로컬 플레이어가 생성되지 않았다면 카메라를 움직이지 않는다.
        if (_target == null)
        {
            return;
        }

        // 플레이어 현재 위치에 offset을 더해 카메라가 가야 할 목표 위치를 계산한다.
        Vector3 desiredPosition = _target.position + offset;

        // 현재 위치에서 목표 위치로 부드럽게 보간한다.
        // Time.deltaTime을 곱해 PC 성능에 따른 프레임 차이를 줄인다.
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime);

        // 플레이어의 루트(발)보다 약간 위쪽을 바라보도록 목표점을 만든다.
        Vector3 lookTarget = _target.position + Vector3.up * lookHeight;

        // 카메라가 항상 플레이어 쪽을 바라보게 한다.
        transform.LookAt(lookTarget);
    }

    /// <summary>
    /// 카메라를 플레이어 기준 위치로 즉시 이동시키는 보조 함수이다.
    /// </summary>
    private void SnapToTarget()
    {
        // 플레이어 위치 + offset으로 즉시 배치한다.
        transform.position = _target.position + offset;

        // 처음부터 플레이어 몸통 방향을 바라보게 한다.
        transform.LookAt(_target.position + Vector3.up * lookHeight);
    }
}
