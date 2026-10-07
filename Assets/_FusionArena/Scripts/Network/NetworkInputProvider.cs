using Fusion;                    // NetworkInput 자료형을 사용한다.
using UnityEngine;               // MonoBehaviour, Vector3를 사용한다.
using UnityEngine.InputSystem;   // Unity 6의 새로운 Input System에서 Keyboard를 사용한다.

/// <summary>
/// 현재 컴퓨터에서 눌린 키보드 입력을 읽고,
/// 그 값을 NetworkInputData에 담아 Fusion에게 전달한다.
///
/// 이 스크립트는 네트워크 상태를 직접 변경하지 않는다.
/// 역할은 오직 "입력을 수집해서 Fusion에 넘기는 것"이다.
/// </summary>
public class NetworkInputProvider : MonoBehaviour
{
    [Header("조준 설정 (우리 게임 확장)")]
    [SerializeField]
    // 마우스 광선과 교차시킬 가상의 수평 평면 높이(월드 Y).
    // 캐릭터 가슴 높이쯤으로 두면 조준선이 바닥에 박히지 않는다.
    private float aimPlaneHeight = 1.0f;

    // 마우스 광선이 평면과 만나지 않는 예외 상황(거의 수평으로 볼 때)에 대비해
    // 마지막으로 성공한 조준 지점을 기억해 두었다가 재사용한다.
    private Vector3 _lastAimPoint;

    /// <summary>
    /// NetworkEvents의 OnInput 이벤트에서 호출할 함수이다.
    /// Fusion이 새로운 입력이 필요한 시점마다 NetworkInput을 전달한다.
    /// </summary>
    /// <param name="input">
    /// 이번 Tick에 사용할 입력 데이터를 담아 Fusion으로 되돌려 보낼 컨테이너이다.
    /// input.Set(...)을 호출해야 실제로 Fusion에 입력이 전달된다.
    /// </param>
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // 이번 Tick에 사용할 입력 구조체를 하나 만든다.
        NetworkInputData data = new NetworkInputData();

        // ───────── 우리 게임 확장: 마우스 조준 + 발사 ─────────
        // 키보드가 없는 예외 상황에서도 조준은 유효해야 하므로 키보드 처리보다 먼저 수집한다.
        CollectAimAndFire(ref data);

        // Unity Input System에서 현재 키보드 장치를 가져온다.
        // 실행 환경에 키보드가 없으면 Keyboard.current가 null일 수 있다.
        Keyboard keyboard = Keyboard.current;

        // 키보드가 없는 경우에도 "입력 없음" 상태를 Fusion에 전달하고 종료한다.
        if (keyboard == null)
        {
            input.Set(data);
            return;
        }

        // 아직 어떤 방향키도 누르지 않았으므로 이동 방향은 (0, 0, 0)에서 시작한다.
        Vector3 direction = Vector3.zero;

        // W키가 눌려 있으면 월드의 +Z 방향(앞쪽)을 더한다.
        if (keyboard.wKey.isPressed)
        {
            direction += Vector3.forward;
        }

        // S키가 눌려 있으면 월드의 -Z 방향(뒤쪽)을 더한다.
        if (keyboard.sKey.isPressed)
        {
            direction += Vector3.back;
        }

        // A키가 눌려 있으면 월드의 -X 방향(왼쪽)을 더한다.
        if (keyboard.aKey.isPressed)
        {
            direction += Vector3.left;
        }

        // D키가 눌려 있으면 월드의 +X 방향(오른쪽)을 더한다.
        if (keyboard.dKey.isPressed)
        {
            direction += Vector3.right;
        }

        // W+D처럼 대각선 입력은 벡터 길이가 1보다 커질 수 있다.
        // 그대로 사용하면 대각선 이동이 직선 이동보다 빨라지므로 길이를 1로 맞춘다.
        if (direction.sqrMagnitude > 1.0f)
        {
            direction.Normalize();
        }

        // 계산한 방향을 네트워크 입력 구조체에 저장한다.
        data.MoveDirection = direction;

        // [우리 게임 확장] E 키 — 공동 가구 정리 등 상호작용
        data.Interact = keyboard.eKey.isPressed;

        // [우리 게임 확장] R 키 — 수동 재장전
        data.Reload = keyboard.rKey.isPressed;

        // 완성된 입력 데이터를 Fusion에게 전달한다.
        // 이후 해당 입력은 Tick별로 저장되고 Input Authority와 State Authority가 사용할 수 있다.
        input.Set(data);
    }

    /// <summary>
    /// [우리 게임 확장] 마우스 커서가 가리키는 월드 지점과 좌클릭 상태를 입력 구조체에 담는다.
    ///
    /// 화면의 마우스 좌표는 2D이므로, 카메라에서 광선을 쏘아
    /// 높이 aimPlaneHeight인 가상의 수평 평면과 만나는 3D 지점을 구한다.
    /// </summary>
    private void CollectAimAndFire(ref NetworkInputData data)
    {
        Mouse mouse = Mouse.current;
        Camera cam = Camera.main;

        // 마우스나 카메라가 아직 없으면 직전 조준 지점을 그대로 사용한다.
        if (mouse == null || cam == null)
        {
            data.AimPoint = _lastAimPoint;
            return;
        }

        // 좌클릭을 누르고 있으면 발사 입력으로 표시한다(홀드 연사).
        data.Fire = mouse.leftButton.isPressed;

        // 화면 좌표(마우스) → 월드 광선
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        // 높이 aimPlaneHeight에 놓인 수평 평면
        Plane aimPlane = new Plane(Vector3.up, new Vector3(0.0f, aimPlaneHeight, 0.0f));

        // 광선이 평면과 만나면 그 지점이 조준 지점이다.
        if (aimPlane.Raycast(ray, out float distance))
        {
            _lastAimPoint = ray.GetPoint(distance);
        }

        data.AimPoint = _lastAimPoint;
    }
}
