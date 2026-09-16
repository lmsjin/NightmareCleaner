using System.Collections.Generic;
using Fusion;
using UnityEngine;

// [교안 실습6] 플레이어가 세션에 들어오면 Host가 NetworkPlayer를 Spawn, 나가면 Despawn.
// Host Mode에서는 Host/Server만 Spawn 가능하므로 Runner.IsServer 확인 필수.
public class PlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    [Header("생성할 네트워크 플레이어")]
    [SerializeField] private NetworkPrefabRef playerPrefab; // 루트에 NetworkObject 필수

    [Header("플레이어 생성 위치")]
    [SerializeField] private Transform[] spawnPoints;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers
        = new Dictionary<PlayerRef, NetworkObject>();

    public void PlayerJoined(PlayerRef player)
    {
        Debug.Log($"[PlayerSpawner] Joined: {player.PlayerId}");

        // Host/Server만 실제 NetworkObject를 Spawn한다.
        if (!Runner.IsServer)
            return;

        Vector3 spawnPosition = GetSpawnPosition(player);

        NetworkObject playerObject = Runner.Spawn(
            playerPrefab,
            spawnPosition,
            Quaternion.identity,
            player); // 이 PlayerRef에게 Input Authority 부여

        // PlayerRef ↔ 대표 Player Object 연결(이후 카메라/점수/리스폰에서 조회).
        Runner.SetPlayerObject(player, playerObject);

        spawnedPlayers[player] = playerObject;
    }

    public void PlayerLeft(PlayerRef player)
    {
        Debug.Log($"[PlayerSpawner] Left: {player.PlayerId}");

        if (!Runner.IsServer)
            return;

        if (spawnedPlayers.TryGetValue(player, out NetworkObject playerObject))
        {
            Runner.Despawn(playerObject); // 모든 피어에서 제거
            spawnedPlayers.Remove(player);
        }
    }

    // PlayerRef에 따라 겹치지 않는 시작 위치 선택.
    private Vector3 GetSpawnPosition(PlayerRef player)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return Vector3.up;

        int index = Mathf.Abs(player.AsIndex) % spawnPoints.Length;
        return spawnPoints[index].position;
    }
}
