using Unity.Netcode;
using UnityEngine;

// [멀티 데모] 소유자(내 캐릭터)의 카메라가 나를 쿼터뷰로 따라오게 한다.
// 각 클라이언트는 자기 Camera.main을 자기 플레이어에 붙이므로, MPPM 두 창이 각자 자기 캐릭터를 비춘다.
// ※ 플레이어 프리팹에 추가.
[RequireComponent(typeof(NetworkObject))]
public class NetCameraFollow : NetworkBehaviour
{
    [SerializeField] Vector3 offset      = new Vector3(0f, 14f, -10f); // 카메라 위치 오프셋(위·뒤)
    [SerializeField] float   followLerp  = 10f;   // 따라오는 부드러움
    [SerializeField] float   lookAtHeight = 1f;   // 바라보는 지점 높이(발밑 말고 몸통)

    Camera _cam;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;         // 내 카메라만 내가 조종
        _cam = Camera.main;
    }

    void LateUpdate()
    {
        if (_cam == null) return;
        Vector3 targetPos = transform.position + offset;
        _cam.transform.position = Vector3.Lerp(_cam.transform.position, targetPos, followLerp * Time.deltaTime);
        _cam.transform.LookAt(transform.position + Vector3.up * lookAtHeight);
    }
}
