using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float blinkInterval = 0.08f; // 피격 깜빡임 주기
    [SerializeField] private float aimHeight = 0.7f;       // 조준 평면 높이(지면 위) — 몸통 높이 조준/레이저용

    private CharacterController _cc;
    private PlayerStats         _stats;
    private bool                _locked;
    private float               _verticalVelocity;
    private float               _mouseSensitivity;

    private Vector3   _knockback;   // 넉백 잔여 속도
    private Renderer[] _renderers;
    private float     _blinkTimer;
    private float     _blinkTick;

    // 마우스→바닥 조준점 (VacuumController 사격이 레이저/히트스캔 방향에 재사용)
    public Vector3 AimPoint { get; private set; }
    public bool    HasAim   { get; private set; }

    void Start()
    {
        _cc    = GetComponent<CharacterController>();
        _stats = GetComponent<PlayerStats>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mouseSensitivity = PlayerPrefs.GetFloat("Settings.MouseSensitivity", 1f);
        GameEvents.OnMiniGameStart += OnLock;
        GameEvents.OnMiniGameEnd   += OnUnlock;
    }

    void Update()
    {
        if (Time.timeScale == 0f) return; // 일시정지 중 정지
        UpdateBlink();                    // 깜빡임은 락 중에도 유지
        if (_locked) return;
        Move();
        FaceMouseCursor();
    }

    void Move()
    {
        var kb = Keyboard.current;
        float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        Vector3 moveDir = new Vector3(h, 0f, v).normalized;

        if (_cc.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;
        else
            _verticalVelocity -= 20f * Time.deltaTime;

        Vector3 velocity = moveDir * _stats.MoveSpeed + _knockback; // 넉백 합산
        velocity.y = _verticalVelocity;
        _cc.Move(velocity * Time.deltaTime);

        _knockback = Vector3.Lerp(_knockback, Vector3.zero, 10f * Time.deltaTime); // 감쇠
    }

    // PlayerStats가 피격 시 호출 — 넉백 + 깜빡임 시작
    public void OnHit(Vector3 dir, float duration, float force)
    {
        _knockback  = dir * force;
        _blinkTimer = duration;
        _blinkTick  = 0f;
    }

    void UpdateBlink()
    {
        if (_blinkTimer <= 0f) return;

        _blinkTimer -= Time.deltaTime;
        _blinkTick  -= Time.deltaTime;
        if (_blinkTick <= 0f)
        {
            _blinkTick = blinkInterval;
            foreach (var r in _renderers) if (r != null) r.enabled = !r.enabled;
        }

        if (_blinkTimer <= 0f)
            foreach (var r in _renderers) if (r != null) r.enabled = true; // 복구
    }

    void FaceMouseCursor()
    {
        Camera cam = Camera.main;
        Mouse mouse = Mouse.current;
        if (cam == null || mouse == null) return;

        _mouseSensitivity = PlayerPrefs.GetFloat("Settings.MouseSensitivity", _mouseSensitivity);

        Vector2 mousePosition = mouse.position.ReadValue();
        if (!IsFinite(mousePosition)) return;
        if (mousePosition.x < 0f || mousePosition.x > cam.pixelWidth ||
            mousePosition.y < 0f || mousePosition.y > cam.pixelHeight)
            return;

        Ray   ray    = cam.ScreenPointToRay(mousePosition);
        // 조준 평면을 지면이 아닌 몸통 높이로 올림 → 조준점/레티클/레이저가 바닥에 박히지 않음.
        Plane ground = new Plane(Vector3.up, transform.position + Vector3.up * aimHeight);
        if (!ground.Raycast(ray, out float dist)) return;

        Vector3 aim = ray.GetPoint(dist);
        AimPoint = aim;
        HasAim   = true;

        Vector3 look = aim - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(look);
            float responsiveness = Mathf.Lerp(6f, 30f, Mathf.InverseLerp(0.2f, 2f, _mouseSensitivity));
            transform.rotation = Quaternion.Slerp(transform.rotation, target, responsiveness * Time.deltaTime);
        }
    }

    bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }

    void OnLock()         => _locked = true;
    void OnUnlock(bool _) => _locked = false;

    void OnDestroy()
    {
        GameEvents.OnMiniGameStart -= OnLock;
        GameEvents.OnMiniGameEnd   -= OnUnlock;
    }
}
