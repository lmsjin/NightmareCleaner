using Fusion;            // INetworkInput 인터페이스를 사용하기 위해 Fusion 네임스페이스를 포함한다.
using UnityEngine;       // Vector3 자료형을 사용하기 위해 UnityEngine을 포함한다.

/// <summary>
/// 한 번의 Fusion Tick에서 사용할 플레이어 입력 데이터를 정의한다.
///
/// 네트워크 게임에서는 키보드 값을 직접 플레이어 Transform에 적용하지 않고,
/// 먼저 이 구조체에 저장한 뒤 Fusion에게 전달한다.
/// Fusion은 이 입력을 Tick별로 보관하고 Host(State Authority)에 전달한다.
/// </summary>
public struct NetworkInputData : INetworkInput
{
    /// <summary>
    /// 플레이어가 이동하려는 방향이다.
    ///
    /// X축 : 왼쪽(-1) / 오른쪽(+1)
    /// Y축 : 이번 수업에서는 사용하지 않으므로 항상 0
    /// Z축 : 뒤(-1) / 앞(+1)
    ///
    /// 예) W만 누르면 (0, 0, 1)
    ///     W+D를 누르면 (1, 0, 1)
    /// </summary>
    public Vector3 MoveDirection;

    // ───────── 아래는 우리 게임(쿼터뷰 슈터) 확장 ─────────
    // 교안 3주차는 WASD 이동만 다루지만, 우리 게임은 마우스로 조준하고 사격하므로 2개를 추가한다.

    /// <summary>
    /// 마우스 커서가 가리키는 **월드 좌표 지점**이다.
    ///
    /// 방향(Direction)이 아니라 "점"을 보내는 이유:
    /// 입력을 수집하는 NetworkInputProvider는 NetworkManager에 붙어 있어서
    /// "내 캐릭터가 어디 있는지"를 모른다. 점을 보내면 플레이어 쪽에서
    /// (AimPoint - 내 위치)로 방향을 직접 계산할 수 있다.
    /// </summary>
    public Vector3 AimPoint;

    /// <summary>
    /// 마우스 왼쪽 버튼을 누르고 있는지 여부(홀드 연사).
    /// bool 대신 NetworkBool을 쓰는 이유는 Fusion이 네트워크 전송용으로 쓰는 자료형이기 때문이다.
    /// </summary>
    public NetworkBool Fire;

    /// <summary>
    /// E 키를 누르고 있는지 여부. 공동 가구 정리(두 명이 함께 2초 유지)에 사용한다.
    /// 나중에 동료 구조(E 3초)·보급함(E 1회)에도 같은 입력을 재사용한다.
    /// </summary>
    public NetworkBool Interact;

    /// <summary>
    /// R 키를 눌렀는지 여부. 탄창이 남아 있어도 수동으로 재장전한다.
    /// (탄창이 비면 입력 없이도 자동 재장전된다)
    /// </summary>
    public NetworkBool Reload;
}
