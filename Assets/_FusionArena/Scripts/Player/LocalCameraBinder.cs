using Fusion;       // NetworkBehaviour와 Input Authority 정보를 사용한다.
using UnityEngine;  // Camera, Debug를 사용한다.

/// <summary>
/// 네트워크 플레이어가 생성되었을 때,
/// "이 컴퓨터가 조작하는 로컬 플레이어"만 Main Camera의 추적 대상으로 지정한다.
/// </summary>
public class LocalCameraBinder : NetworkBehaviour
{
    /// <summary>
    /// NetworkObject가 Fusion 네트워크에 정상적으로 Spawn된 뒤 호출된다.
    /// 이 시점에는 Object.HasInputAuthority를 안전하게 확인할 수 있다.
    /// </summary>
    public override void Spawned()
    {
        // 이 오브젝트의 입력 권한이 현재 실행 프로그램에 없다면
        // 다른 사용자의 원격 플레이어이므로 카메라를 연결하지 않는다.
        if (Object.HasInputAuthority == false)
        {
            return;
        }

        // "MainCamera" 태그가 붙은 현재 Scene의 카메라를 찾는다.
        Camera mainCamera = Camera.main;

        // Main Camera가 없으면 NullReferenceException을 막고 원인을 알려준다.
        if (mainCamera == null)
        {
            Debug.LogWarning("[LocalCameraBinder] MainCamera 태그가 있는 카메라를 찾을 수 없습니다.");
            return;
        }

        // Main Camera에 추가한 LocalCameraFollow 컴포넌트를 가져온다.
        LocalCameraFollow cameraFollow = mainCamera.GetComponent<LocalCameraFollow>();

        // 카메라 추적 스크립트가 빠져 있으면 경고를 출력하고 종료한다.
        if (cameraFollow == null)
        {
            Debug.LogWarning("[LocalCameraBinder] Main Camera에 LocalCameraFollow가 없습니다.");
            return;
        }

        // 자신의 NetworkPlayer Transform을 카메라의 추적 대상으로 지정한다.
        cameraFollow.SetTarget(transform);

        // 수업 중 어떤 플레이어가 카메라를 연결했는지 확인할 수 있도록 로그를 남긴다.
        Debug.Log($"[LocalCameraBinder] Local Player Camera 연결: {Object.InputAuthority}");
    }
}
