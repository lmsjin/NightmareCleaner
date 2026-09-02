using Unity.Netcode;
using UnityEngine;

// [멀티 슬라이스] 화면 좌상단에 Host / Client 버튼. NetworkManager 오브젝트에 붙인다.
// Host = 방장으로 시작, Client = 그 방에 접속.
public class NetConnectHud : MonoBehaviour
{
    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 160, 120));
        if (!nm.IsClient && !nm.IsServer)
        {
            if (GUILayout.Button("Host"))   nm.StartHost();
            if (GUILayout.Button("Client")) nm.StartClient();
        }
        else
        {
            GUILayout.Label(nm.IsHost ? "HOST" : "CLIENT");
        }
        GUILayout.EndArea();
    }
}
