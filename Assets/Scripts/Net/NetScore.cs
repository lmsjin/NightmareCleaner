using Unity.Netcode;
using UnityEngine;

// [멀티 슬라이스] 공유(팀) 점수. 서버만 값을 올리고, 클라는 NetworkVariable로 받아 표시만 한다.
// 씬에 빈 오브젝트를 만들고 NetworkObject + 이 스크립트를 붙인다(씬 배치 NetworkObject).
public class NetScore : NetworkBehaviour
{
    public static NetScore Instance { get; private set; }

    // 서버만 쓰기, 모두 읽기(기본 권한). 공유 점수 하나.
    public NetworkVariable<int> Score = new NetworkVariable<int>();

    public override void OnNetworkSpawn()   { Instance = this; }
    public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

    // 서버에서만 호출(유령 사망 등).
    public void AddScoreServer(int amount)
    {
        if (!IsServer) return;
        Score.Value += amount;
    }

    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return; // 접속 후에만 표시
        GUI.Label(new Rect(10, 140, 320, 30), $"SCORE (shared): {Score.Value}");
    }
}
