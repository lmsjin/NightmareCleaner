using Fusion;
using UnityEngine;

/// <summary>입력 버튼 번호. NetworkButtons의 비트 위치로 쓴다.</summary>
public enum NCButton
{
    Fire = 0,     // 좌클릭(홀드 연사)
    Reload = 1,   // R
    Interact = 2, // E(홀드)
}

/// <summary>
/// 한 Tick의 플레이어 입력. 입력은 "요청"일 뿐이고, 결과(명중·피해·진행도)는 Host가 확정한다.
/// </summary>
public struct NCInput : INetworkInput
{
    /// <summary>이동 방향(WASD). x = 좌우, y = 앞뒤(월드 Z).</summary>
    public Vector2 Move;

    /// <summary>마우스가 가리키는 월드 지점(바닥에서 1m 높이 평면).</summary>
    public Vector3 AimPoint;

    /// <summary>눌린 버튼들.</summary>
    public NetworkButtons Buttons;

    public bool IsDown(NCButton button) => Buttons.IsSet(button);
}
