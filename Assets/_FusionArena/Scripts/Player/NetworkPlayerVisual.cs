using Fusion;
using UnityEngine;

// [교안 실습7] 내 캐릭터(Input Authority)는 녹색, 다른 플레이어는 주황색으로 로컬 표시.
// 색 자체는 네트워크 동기화가 아니라 각 실행 인스턴스가 자기 관점에서 결정.
public class NetworkPlayerVisual : NetworkBehaviour
{
    [SerializeField] private Renderer bodyRenderer;

    [SerializeField] private Color localColor
        = new Color(0.20f, 0.90f, 0.30f);

    [SerializeField] private Color remoteColor
        = new Color(1.00f, 0.50f, 0.10f);

    // NetworkObject가 정상 Spawn된 뒤 호출 → Authority 확인 안전 시점.
    public override void Spawned()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();

        if (bodyRenderer == null)
        {
            Debug.LogWarning("Body Renderer를 찾을 수 없습니다.");
            return;
        }

        bool isLocalPlayer = Object.HasInputAuthority;

        bodyRenderer.material.color = isLocalPlayer
            ? localColor
            : remoteColor;

        gameObject.name = isLocalPlayer
            ? "NetworkPlayer_Local"
            : "NetworkPlayer_Remote";
    }
}
