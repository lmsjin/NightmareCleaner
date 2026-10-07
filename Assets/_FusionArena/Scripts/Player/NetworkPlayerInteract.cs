using Fusion;
using UnityEngine;

/// <summary>
/// 플레이어가 E 키를 누르고 있는지를 **네트워크 상태로 공개**한다.
///
/// 왜 필요한가:
/// GetInput()은 "그 입력을 가진 오브젝트(플레이어)"에서만 꺼낼 수 있다.
/// 가구는 플레이어의 입력을 직접 읽을 수 없으므로, 플레이어가 자기 입력을 받아
/// [Networked] 값으로 올려두면 가구(Host)가 그 값을 읽어 판정한다.
///
/// 플레이어 프리팹 루트에 붙인다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerInteract : NetworkBehaviour
{
    /// <summary>이번 Tick에 E를 누르고 있는지(다운 상태면 항상 false).</summary>
    [Networked] public NetworkBool IsHoldingInteract { get; set; }

    /// <summary>이번 Tick에 이동 입력(WASD)이 있는지. 구조 취소 판정용.</summary>
    [Networked] public NetworkBool IsMoving { get; set; }

    /// <summary>이번 Tick에 사격 입력(좌클릭)이 있는지. 구조 취소 판정용.</summary>
    [Networked] public NetworkBool IsFiring { get; set; }

    private NetworkPlayerHealth _health;

    public override void Spawned()
    {
        _health = GetComponent<NetworkPlayerHealth>();
    }

    public override void FixedUpdateNetwork()
    {
        // 상태 결정은 Host만 한다.
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        // 다운된 플레이어는 상호작용할 수 없다.
        if (_health != null && _health.IsDown)
        {
            IsHoldingInteract = false;
            IsMoving = false;
            IsFiring = false;
            return;
        }

        // 입력이 없는 Tick(연결 지연 등)은 "안 누름"으로 본다 → 공동 진행이 초기화된다.
        if (GetInput(out NetworkInputData data))
        {
            IsHoldingInteract = data.Interact;
            IsMoving = data.MoveDirection.sqrMagnitude > 0.01f;
            IsFiring = data.Fire;
        }
        else
        {
            IsHoldingInteract = false;
            IsMoving = false;
            IsFiring = false;
        }
    }
}
