using Fusion;       // NetworkBehaviour, NetworkCharacterController를 사용한다.
using UnityEngine;  // SerializeField, Vector3 등 Unity 기본 자료형을 사용한다.

/// <summary>
/// Fusion으로 전달된 플레이어 입력을 가져와
/// NetworkCharacterController를 통해 실제 캐릭터 이동으로 바꾸는 클래스이다.
///
/// 이 클래스는 "입력 수집"을 하지 않는다.
/// 입력 수집은 NetworkInputProvider가 담당하고,
/// 이 클래스는 이미 전달된 입력을 해당 네트워크 Tick에서 소비한다.
/// </summary>
[RequireComponent(typeof(NetworkCharacterController))]
public class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("이동 설정")]
    [SerializeField]
    private float moveSpeed = 5.0f;  // 초당 이동 속도. Inspector에서 조절할 수 있다.

    [Header("조준 회전 설정 (우리 게임 확장)")]
    [SerializeField]
    // 초당 회전 각도(도). 값이 클수록 커서를 빠르게 따라본다.
    private float turnSpeed = 720.0f;

    // 같은 GameObject에 있는 Fusion의 NetworkCharacterController를 보관한다.
    // NetworkCharacterController는 충돌 처리와 네트워크 이동/예측을 담당한다.
    private NetworkCharacterController _characterController;

    /// <summary>
    /// Unity가 오브젝트를 초기화할 때 한 번 호출된다.
    /// NetworkCharacterController 참조를 미리 찾아 저장한다.
    /// </summary>
    // 다운 상태면 이동·회전을 막기 위해 참조한다(없어도 동작).
    private NetworkPlayerHealth _health;

    private void Awake()
    {
        _characterController = GetComponent<NetworkCharacterController>();
        _health = GetComponent<NetworkPlayerHealth>();
    }

    /// <summary>
    /// Fusion의 네트워크 Simulation Tick마다 호출된다.
    /// 플레이어의 네트워크 상태를 변경하는 이동 코드는 여기에서 처리한다.
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        // 다운된 동안은 이동·회전하지 않는다(기획서 7장).
        if (_health != null && _health.IsDown)
        {
            return;
        }

        // 이 NetworkObject에 할당된 이번 Tick의 입력을 가져온다.
        // GetInput은 주로 Input Authority와 State Authority에서 true를 반환한다.
        if (GetInput(out NetworkInputData data) == false)
        {
            // 내가 처리할 유효한 입력이 없다면 이번 Tick에는 이동하지 않는다.
            return;
        }

        // 입력 구조체에 저장된 이동 방향을 지역 변수로 복사한다.
        Vector3 direction = data.MoveDirection;

        // 이번 게임은 바닥 위 XZ 평면 이동만 사용하므로 Y축 입력은 제거한다.
        direction.y = 0.0f;

        // 네트워크를 통해 들어온 값은 서버에서 다시 안전하게 보정하는 습관이 중요하다.
        // 비정상적으로 큰 벡터가 전달되어도 최대 길이를 1로 제한한다.
        if (direction.sqrMagnitude > 1.0f)
        {
            direction.Normalize();
        }

        // 이동 속도 × 방향 × 네트워크 Tick의 시간 간격을 계산한다.
        // Runner.DeltaTime을 사용해야 프레임 속도와 무관하게 일정한 속도로 움직인다.
        Vector3 movement = moveSpeed * direction * Runner.DeltaTime;

        // NetworkCharacterController에게 실제 이동을 맡긴다.
        // 이 컴포넌트는 CharacterController 기반 충돌과 Fusion의 예측/상태 복제를 지원한다.
        _characterController.Move(movement);

        // ───────── 우리 게임 확장: 마우스 커서 쪽으로 회전 ─────────
        // 회전도 "모두가 공유해야 할 게임 상태"이므로 카메라처럼 LateUpdate가 아니라
        // 반드시 FixedUpdateNetwork에서 처리해야 Host 권위와 클라 복제가 맞는다.
        RotateTowardAim(data.AimPoint);
    }

    /// <summary>
    /// [우리 게임 확장] 조준 지점을 바라보도록 캐릭터를 회전시킨다.
    /// 쿼터뷰 슈터라 위아래 기울임 없이 바닥 평면(XZ)에서만 회전한다.
    /// </summary>
    private void RotateTowardAim(Vector3 aimPoint)
    {
        // 내 위치에서 조준 지점까지의 방향을 구한다.
        Vector3 look = aimPoint - transform.position;

        // 캐릭터가 앞으로 기울지 않도록 높이 성분은 제거한다.
        look.y = 0.0f;

        // 조준 지점이 발밑과 거의 같으면 방향을 정할 수 없으므로 회전하지 않는다.
        if (look.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // 목표 회전을 만들고, 순간이동이 아니라 turnSpeed 속도로 부드럽게 돌린다.
        Quaternion targetRotation = Quaternion.LookRotation(look.normalized, Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Runner.DeltaTime);
    }
}
