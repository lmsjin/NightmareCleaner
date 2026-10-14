using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어 본체: 슬롯(1P/2P)·이동·조준 회전·행동 가능 여부.
/// 다른 판정(정리·구조)이 읽을 수 있도록 이번 Tick의 입력 상태(E 유지·이동·사격)를 [Networked]로 공개한다.
///
/// 프리팹 구성(빌더가 생성): NetworkObject · CharacterController · NetworkCharacterController ·
/// NCPlayer · NCHealth · NCGun, 자식 "Body"(색이 바뀌는 몸통)·"Nozzle"(총구).
/// </summary>
[RequireComponent(typeof(NetworkCharacterController))]
public class NCPlayer : NetworkBehaviour
{
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

    /// <summary>이번 Tick에 E를 누르고 있는지 / 이동 입력이 있는지 / 사격 입력이 있는지.</summary>
    [Networked] public NetworkBool HoldingInteract { get; set; }
    [Networked] public NetworkBool IsMoving { get; set; }
    [Networked] public NetworkBool IsFiring { get; set; }

    /// <summary>대기실 준비 상태(각자 요청, 호스트 확정).</summary>
    [Networked] public NetworkBool Ready { get; set; }

    /// <summary>슬롯 색이 칠해지는 몸통 렌더러(피격 깜빡임에도 쓴다).</summary>
    public Renderer BodyRenderer => bodyRenderer;

    public NCHealth Health { get; private set; }
    public NCGun Gun { get; private set; }
    public Vector3 MuzzlePosition => nozzle != null ? nozzle.position : transform.position + Vector3.up;
    public string SlotLabel => Slot == 0 ? "1P" : "2P";
    public Color SlotColor => NCStyle.SlotColor(Slot);

    /// <summary>이동·사격 등 행동이 가능한지(다운·결과 화면이면 불가).</summary>
    public bool CanAct
    {
        get
        {
            if (Health != null && Health.IsDown)
            {
                return false;
            }

            NCSession session = NCSession.Instance;
            return session == null || (session.IsFinished == false && session.Phase != NCPhase.Lobby);
        }
    }

    private NetworkCharacterController _controller;
    private float _turnSpeed = 900f;

    private void Awake()
    {
        _controller = GetComponent<NetworkCharacterController>();
        Health = GetComponent<NCHealth>();
        Gun = GetComponent<NCGun>();
    }

    public override void Spawned()
    {
        All.Add(this);

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

    public override void FixedUpdateNetwork()
    {
        bool hasInput = GetInput(out NCInput input);

        if (hasInput == false || CanAct == false)
        {
            HoldingInteract = false;
            IsMoving = false;
            IsFiring = false;
            _controller.Move(Vector3.zero); // 중력만 적용
            return;
        }

        Vector3 direction = new Vector3(input.Move.x, 0f, input.Move.y);
        _controller.Move(direction);

        HoldingInteract = input.IsDown(NCButton.Interact);
        IsMoving = direction.sqrMagnitude > 0.01f;
        IsFiring = input.IsDown(NCButton.Fire);

        FaceAim(input.AimPoint);
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

    /// <summary>재도전 때 입구로 옮긴다. Host의 Tick 안에서만 호출한다.</summary>
    public void ServerTeleport(Vector3 position, Quaternion rotation)
    {
        _controller.Teleport(position, rotation);
    }

    private void ApplyColor()
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = SlotColor;
        }
    }
}
