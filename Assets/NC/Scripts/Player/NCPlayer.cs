using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 본체: 슬롯(1P/2P)·이동·대쉬·조준 회전·행동 가능 여부.
///
/// 프리팹 구성(빌더가 생성): NetworkObject · CharacterController · NetworkCharacterController ·
/// NCPlayer · NCHealth · NCGun, 자식 "Body"(색이 바뀌는 몸통)·"Nozzle"(총구).
/// </summary>
[RequireComponent(typeof(NetworkCharacterController))]
public class NCPlayer : NetworkBehaviour
{
    /// <summary>플레이어끼리 서로 막지 않도록 쓰는 전용 레이어(이름 없는 빈 레이어).</summary>
    public const int Layer = 10;

    /// <summary>접속 중인 모든 플레이어(모든 PC에서 유지).</summary>
    public static readonly List<NCPlayer> All = new List<NCPlayer>();

    /// <summary>이 PC가 조작하는 플레이어.</summary>
    public static NCPlayer Local { get; private set; }

    public static NCPlayer FindBySlot(int slot)
    {
        foreach (NCPlayer player in All)
        {
            if (player.Slot == slot)
            {
                return player;
            }
        }

        return null;
    }

    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Transform nozzle;

    /// <summary>0 = 1P(파랑), 1 = 2P(노랑).</summary>
    [Networked] public int Slot { get; set; }

    /// <summary>대기실 준비 상태(각자 요청, 호스트 확정).</summary>
    [Networked] public NetworkBool Ready { get; set; }

    /// <summary>솔로 테스트 허수아비(조종하는 사람 없음, 입력은 Host가 NCDummyBrain으로 계산).</summary>
    [Networked] public NetworkBool IsDummy { get; set; }

    [Networked] private TickTimer DashTimer { get; set; }
    [Networked] private TickTimer DashCooldown { get; set; }
    [Networked] private Vector3 DashDirection { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }

    /// <summary>대쉬로 이동하는 중(무적 아님, 사격 불가).</summary>
    public bool IsDashing => DashTimer.IsRunning && DashTimer.Expired(Runner) == false;

    /// <summary>대쉬를 다시 쓸 수 있을 때까지의 진행도(1이면 사용 가능).</summary>
    public float DashReadyRatio => Mathf.Clamp01(1f - (DashCooldown.RemainingTime(Runner) ?? 0f) / NCBalance.Get.dashCooldownSeconds);

    /// <summary>슬롯 색이 칠해지는 몸통 렌더러(피격 깜빡임에도 쓴다).</summary>
    public Renderer BodyRenderer => bodyRenderer;

    public NCHealth Health { get; private set; }
    public NCGun Gun { get; private set; }
    public Vector3 MuzzlePosition => nozzle != null ? nozzle.position : transform.position + Vector3.up;
    public Color SlotColor => NCStyle.SlotColor(Slot);

    /// <summary>이동·사격 등 행동이 가능한지(다운·결과 화면이면 불가).</summary>
    public bool CanAct
    {
        get
        {
            NCSession session = NCSession.Instance;
            return Health != null && Health.IsDown == false
                   && session != null && session.Object != null && session.Object.IsValid && session.IsPlaying;
        }
    }

    private NetworkCharacterController _controller;
    private float _turnSpeed = 900f;
    private float _puffTimer;

    private void Awake()
    {
        _controller = GetComponent<NetworkCharacterController>();
        Health = GetComponent<NCHealth>();
        Gun = GetComponent<NCGun>();
    }

    public override void Spawned()
    {
        All.Add(this);
        gameObject.layer = Layer;

        NCBalance balance = NCBalance.Get;
        _controller.maxSpeed = balance.playerMoveSpeed;
        _controller.acceleration = 60f;
        _controller.braking = 60f;
        _controller.rotationSpeed = 0f; // 회전은 이동 방향이 아니라 조준 방향으로 직접 한다.

        if (Object.HasInputAuthority)
        {
            Local = this;
            NCCameraRig.SetTarget(transform);
        }

        ApplyColor();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        All.Remove(this);

        if (Local == this)
        {
            Local = null;
        }
    }

    /// <summary>
    /// 이번 Tick 입력. 사람이면 Fusion 입력, 허수아비면 Host가 계산한 입력.
    /// 이동·대쉬(NCPlayer)와 사격(NCGun)이 같은 입력을 쓴다.
    /// </summary>
    public bool TryGetInput(out NCInput input)
    {
        if (GetInput(out input))
        {
            return true;
        }

        if (IsDummy && Object.HasStateAuthority)
        {
            input = NCDummyBrain.Compute(this);
            return true;
        }

        return false;
    }

    public override void FixedUpdateNetwork()
    {
        NCBalance balance = NCBalance.Get;

        // 허수아비는 대기실에서 늘 준비 완료(경기 종료·초기화 때 꺼지므로 다시 켠다).
        if (IsDummy && Object.HasStateAuthority && NCSession.Instance != null
            && NCSession.Instance.Object != null && NCSession.Instance.Phase == NCPhase.Lobby)
        {
            Ready = true;
        }

        bool hasInput = TryGetInput(out NCInput input);
        bool canAct = CanAct;

        if (hasInput)
        {
            bool pressed = input.Buttons.WasPressed(PreviousButtons, NCButton.Dash);
            PreviousButtons = input.Buttons; // 행동 불가 중에 누르고 있던 키는 새로 누른 것으로 치지 않는다.

            if (canAct && pressed && DashCooldown.ExpiredOrNotRunning(Runner))
            {
                StartDash(input);
            }
        }

        // maxSpeed는 네트워크 값이 아니므로 재시뮬레이션에 대비해 매 Tick 다시 정한다.
        _controller.maxSpeed = balance.playerMoveSpeed;

        if (canAct && IsDashing)
        {
            MoveDash(balance.dashDistance / balance.dashSeconds);
        }
        else if (hasInput && canAct)
        {
            _controller.Move(new Vector3(input.Move.x, 0f, input.Move.y));
        }
        else
        {
            _controller.Move(Vector3.zero); // 중력만 적용
        }

        ClampUpwardVelocity();

        if (hasInput && canAct)
        {
            FaceAim(input.AimPoint);
        }
    }

    private void StartDash(NCInput input)
    {
        Vector3 direction = new Vector3(input.Move.x, 0f, input.Move.y);
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = NCMath.Flat(input.AimPoint - transform.position);
        }

        if (direction.sqrMagnitude < 0.01f)
        {
            direction = NCMath.Flat(transform.forward);
        }

        DashDirection = direction.normalized;
        DashTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.dashSeconds);
        DashCooldown = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.dashCooldownSeconds);
    }

    /// <summary>가속 없이 바로 대쉬 속도로 움직인다. 벽·가구에는 CharacterController가 막힌다.</summary>
    private void MoveDash(float speed)
    {
        Vector3 velocity = DashDirection * speed;
        velocity.y = _controller.Velocity.y;
        _controller.maxSpeed = speed;
        _controller.Velocity = velocity;
        _controller.Move(DashDirection);
    }

    /// <summary>대쉬 자국(모든 화면).</summary>
    public override void Render()
    {
        if (IsDashing == false)
        {
            _puffTimer = 0f;
            return;
        }

        _puffTimer -= Time.deltaTime;
        if (_puffTimer <= 0f)
        {
            _puffTimer = 0.03f;
            NCFx.DashTrail(transform.position + Vector3.up * 0.6f, SlotColor);
        }
    }

    /// <summary>
    /// 이 게임엔 점프가 없다. 턱·물체 모서리를 밟아 순간 올라간 높이가 위쪽 속도로 남으면
    /// (NetworkCharacterController는 이동량으로 속도를 다시 계산한다) 다음 Tick부터 하늘로 솟구친다 → 위쪽 속도를 지운다.
    /// </summary>
    private void ClampUpwardVelocity()
    {
        Vector3 velocity = _controller.Velocity;
        if (velocity.y > 0f)
        {
            velocity.y = 0f;
            _controller.Velocity = velocity;
        }
    }

    private void FaceAim(Vector3 aimPoint)
    {
        Vector3 look = NCMath.Flat(aimPoint - transform.position);
        if (look.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion target = Quaternion.LookRotation(look.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * Runner.DeltaTime);
    }

    /// <summary>대기실에서 내 준비 상태를 바꿔 달라고 호스트에게 요청한다(내 플레이어만 보낼 수 있다).</summary>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcSetReady(NetworkBool ready)
    {
        NCSession session = NCSession.Instance;
        if (session != null && session.Phase == NCPhase.Lobby)
        {
            Ready = ready;
        }
    }

    /// <summary>사망: 대쉬를 끊고 대기를 없앤다. Host 전용.</summary>
    public void ServerResetDash()
    {
        DashTimer = TickTimer.None;
        DashCooldown = TickTimer.None;
    }

    /// <summary>자기 스폰으로 옮기고 대쉬를 초기화한다. Host의 Tick 안에서만 호출한다.</summary>
    public void ServerMoveToSpawn()
    {
        ServerResetDash();

        Transform point = NCLauncher.Instance != null ? NCLauncher.Instance.GetEntrySpawn(Slot) : null;
        if (point != null)
        {
            _controller.Teleport(point.position, point.rotation);
            _controller.Velocity = Vector3.zero;
        }
    }

    private void ApplyColor()
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = SlotColor;
        }
    }
}
