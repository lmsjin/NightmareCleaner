using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// [멀티 슬라이스] 네트워크 플레이어. Owner(내 캐릭터)만 WASD 이동 + 마우스 조준.
// 위치/회전은 프리팹의 NetworkTransform(Authority = Owner)이 자동 복제한다.
// 싱글용 PlayerController.FaceMouseCursor(마우스→몸통높이 평면)의 조준 방식을 그대로 이식.
// CharacterController가 있으면 벽 충돌 이동, 없으면 transform 이동으로 폴백(평면 테스트용).
[RequireComponent(typeof(NetworkObject))]
public class NetPlayerController : NetworkBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float aimHeight = 0.7f; // 조준 평면 높이(바닥 박힘 방지 — 싱글과 동일 개념)
    [SerializeField] float turnSpeed = 15f;
    [SerializeField] float gravity   = 20f;  // 중력(맵 바닥에 붙어있게)

    public Vector3 AimPoint { get; private set; }
    public bool    HasAim   { get; private set; }

    NetPlayerHealth     _health;
    CharacterController _cc;
    float _verticalVel;
    Vector3 _knockback;              // 피격 넉백 잔여 속도(감쇠)
    const float KnockbackDecay = 10f;

    // 서버가 HitClientRpc로 소유자에게 요청 → 여기서 실제 이동에 반영
    public void ApplyKnockback(Vector3 dir, float force)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f) _knockback = dir.normalized * force;
    }

    void Awake()
    {
        _health = GetComponent<NetPlayerHealth>();
        _cc     = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (!IsOwner) return; // 남의 캐릭터는 조작하지 않음
        if (_health != null && _health.Dead.Value) return; // 죽은 동안 이동/조준 정지
        Move();
        Aim();
    }

    void Move()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        Vector3 planar = new Vector3(h, 0f, v).normalized * moveSpeed + _knockback; // 입력 + 넉백

        if (_cc != null && _cc.enabled)
        {
            // 벽 충돌 + 중력
            if (_cc.isGrounded && _verticalVel < 0f) _verticalVel = -2f;
            else _verticalVel -= gravity * Time.deltaTime;
            Vector3 move = planar;
            move.y = _verticalVel;
            _cc.Move(move * Time.deltaTime);
        }
        else
        {
            // CharacterController 없으면(평면 테스트) 그냥 이동
            transform.position += planar * Time.deltaTime;
        }

        // 넉백 감쇠
        _knockback = Vector3.Lerp(_knockback, Vector3.zero, KnockbackDecay * Time.deltaTime);
    }

    void Aim()
    {
        var cam = Camera.main;
        var mouse = Mouse.current;
        if (cam == null || mouse == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        Plane plane = new Plane(Vector3.up, transform.position + Vector3.up * aimHeight);
        if (!plane.Raycast(ray, out float dist)) return;

        AimPoint = ray.GetPoint(dist);
        HasAim = true;

        Vector3 look = AimPoint - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(look), turnSpeed * Time.deltaTime);
    }

    // 총구(origin)에서 조준점까지의 수평 방향. NetShooter가 사용.
    public bool TryGetAimDirection(Vector3 origin, out Vector3 dir)
    {
        if (HasAim)
        {
            dir = AimPoint - origin;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) { dir.Normalize(); return true; }
        }
        Vector3 f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude > 0.0001f) { dir = f.normalized; return true; }
        dir = Vector3.zero;
        return false;
    }
}
