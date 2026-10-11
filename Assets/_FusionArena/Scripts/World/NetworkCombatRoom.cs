using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

/// <summary>전투방 진행 상태.</summary>
public enum RoomState
{
    Idle,     // 가구 정리 전 — 유령 없음
    Fighting, // 차수 진행 중
    Cleared   // 모든 차수 소탕 완료
}

/// <summary>
/// 전투방 하나의 전투 흐름. 기획서 기준:
///
///   가구 공동 정리 → 출현 지점에 1.5초 예고 → 유령 등장 →
///   동시 상한까지만 채우며 차수의 유령을 모두 처치 → 다음 차수 → 마지막 차수 소탕 = 방 완료.
///   방 완료 순간 다운된 플레이어는 체력 40으로 일어난다(기획서 7장).
///
/// 방마다 하나씩 씬에 배치하는 NetworkObject다. 유령 구성(차수별 기본형/빠른형 수)과
/// 동시 상한은 Inspector에서 방마다 따로 정한다(방을 복제한 뒤 숫자만 바꾸면 된다).
///
/// 판정은 Host만 하고, 상태는 [Networked] 값과 RPC 연출로 모든 화면에 전달된다.
///
/// TODO(다음 단계): Fighting 동안 출입구 봉쇄, 완료 시 과충전 지급·승리 판정이 IsCleared를 읽는다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkCombatRoom : NetworkBehaviour
{
    /// <summary>한 차수에 나올 유령 구성.</summary>
    [System.Serializable]
    public class Wave
    {
        [Min(0)] public int basicCount = 3;
        [Min(0)] public int fastCount = 1;
    }

    [Header("연결")]
    [Tooltip("이 방의 공동 정리 가구. 정리가 끝나면 전투가 시작된다. 비워두면 즉시 시작(테스트용)")]
    [SerializeField] private NetworkFurniture furniture;

    [Tooltip("생성할 유령 프리팹(NetworkGhost)")]
    [SerializeField] private NetworkPrefabRef ghostPrefab;

    [Tooltip("유령 출현 지점들(방 안 바닥의 빈 오브젝트). 기획서는 방당 3개")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("유령 구성 (방마다 다르게)")]
    [SerializeField] private Wave[] waves =
    {
        new Wave { basicCount = 3, fastCount = 1 },
        new Wave { basicCount = 3, fastCount = 2 },
    };

    [Tooltip("동시에 존재할 수 있는 유령 수(예고 중 포함). 기획서: 4~6")]
    [Min(1)]
    [SerializeField] private int maxAlive = 4;

    [Header("출현 지점 선택")]
    [Tooltip("플레이어가 이 거리 안에 있는 출현 지점은 가능하면 피한다(m)")]
    [SerializeField] private float avoidPlayerRadius = 1.5f;

    [Header("설정 (Spawned에서 CleanerBalance 값으로 덮어씀)")]
    [SerializeField] private float spawnWarningSeconds = 1.5f;
    [SerializeField] private float waveGapSeconds = 2.0f;

    // ───────── 네트워크 공유 상태 ─────────

    /// <summary>방 진행 상태.</summary>
    [Networked] public RoomState State { get; set; }

    /// <summary>현재 차수(0부터).</summary>
    [Networked] public int WaveIndex { get; set; }

    /// <summary>이번 차수에 남은 유령 수(아직 안 나온 수 + 예고 중 + 살아 있는 수). 표시용.</summary>
    [Networked] public int RemainingInWave { get; set; }

    // 차수 사이 쉬는 시간
    [Networked] private TickTimer WaveGapTimer { get; set; }

    // "방 정리 완료!" 문구를 잠깐 보여주는 시간
    [Networked] private TickTimer ClearedBannerTimer { get; set; }

    // UI 기획서: 차수는 출현 예고 1.5초 동안만 "유령 출현 · N차"로 알린다.
    [Networked] private TickTimer WaveBannerTimer { get; set; }

    /// <summary>게임이 끝나 이 방의 진행을 멈춘 상태인지(승리/실패 후).</summary>
    [Networked] public NetworkBool IsHalted { get; set; }

    /// <summary>방 완료 여부. 봉쇄 해제·과충전 지급·승리 판정이 이 값을 본다.</summary>
    public bool IsCleared => State == RoomState.Cleared;

    /// <summary>이 방의 공동 정리 더미. 가구가 "자기 방이 아닌 전투"를 구분할 때 쓴다.</summary>
    public NetworkFurniture Furniture => furniture;

    // ───────── Host 전용 (네트워크로 보낼 필요 없음) ─────────

    // 예고 중인 출현 하나
    private struct PendingSpawn
    {
        public Vector3 Position;
        public GhostType Type;
        public TickTimer Timer;
    }

    private readonly List<PendingSpawn> _pending = new List<PendingSpawn>();
    private readonly List<NetworkGhost> _ghosts = new List<NetworkGhost>();
    private int _basicLeft; // 이번 차수에서 아직 예고도 안 한 기본형 수
    private int _fastLeft;  // 이번 차수에서 아직 예고도 안 한 빠른형 수
    private bool _spawnBroken; // 프리팹 연결 오류로 생성이 불가능한 상태(에러 반복 방지)

    public override void Spawned()
    {
        CleanerBalance balance = CleanerBalance.Resolve(null);

        if (balance != null)
        {
            spawnWarningSeconds = balance.spawnWarningSeconds;
            waveGapSeconds = balance.waveGapSeconds;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 판정은 Host만 한다. 게임이 끝나 멈춘 방은 아무것도 하지 않는다.
        if (Object.HasStateAuthority == false || IsHalted)
        {
            return;
        }

        switch (State)
        {
            case RoomState.Idle:
                TickIdle();
                break;

            case RoomState.Fighting:
                TickFighting();
                break;
        }
    }

    // ───────── 게임 흐름(NetworkGameFlow)이 부르는 제어 ─────────

    /// <summary>
    /// 게임 종료(승리/실패) 시 호출. 이 방의 유령·예고를 없애고 진행을 멈춘다. **Host에서만 호출.**
    /// </summary>
    public void ServerHalt()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        foreach (NetworkGhost ghost in _ghosts)
        {
            if (ghost != null && ghost.Object != null && ghost.Object.IsValid)
            {
                Runner.Despawn(ghost.Object);
            }
        }

        _ghosts.Clear();
        _pending.Clear();
        IsHalted = true;
    }

    /// <summary>
    /// 재시작 시 호출. 방과 가구를 처음 상태(정리 전)로 되돌린다. **Host에서만 호출.**
    /// </summary>
    public void ServerReset()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        ServerHalt();

        State = RoomState.Idle;
        WaveIndex = 0;
        RemainingInWave = 0;
        WaveGapTimer = TickTimer.None;
        ClearedBannerTimer = TickTimer.None;
        WaveBannerTimer = TickTimer.None;
        _basicLeft = 0;
        _fastLeft = 0;
        _spawnBroken = false;
        IsHalted = false;

        if (furniture != null)
        {
            furniture.ServerReset();
        }
    }

    // ───────── 상태별 처리 ─────────

    private void TickIdle()
    {
        // 가구 정리가 끝나야 전투가 시작된다(기획서: 정리 → 등장 → 전투).
        if (furniture != null && furniture.IsRestored == false)
        {
            return;
        }

        if (ghostPrefab == default)
        {
            Debug.LogWarning($"[NetworkCombatRoom] {name}: Ghost Prefab이 비어 있어 전투를 시작할 수 없습니다.");
            return;
        }

        State = RoomState.Fighting;
        Debug.Log($"[NetworkCombatRoom] {name}: 전투 시작");
        StartWave(0);
    }

    private void TickFighting()
    {
        // 생성이 불가능하면 전투를 멈춘 채 둔다(유령 없이 방이 완료되지 않게).
        if (_spawnBroken)
        {
            return;
        }

        // 죽어서 Despawn된 유령을 목록에서 뺀다.
        _ghosts.RemoveAll(ghost => ghost == null || ghost.Object == null || ghost.Object.IsValid == false);

        // 예고가 끝난 출현은 실제 유령으로 만든다.
        SpawnExpiredWarnings();

        // 차수 사이 쉬는 시간
        if (WaveGapTimer.IsRunning)
        {
            if (WaveGapTimer.Expired(Runner))
            {
                WaveGapTimer = TickTimer.None;
                StartWave(WaveIndex + 1);
            }

            return;
        }

        // 동시 상한까지만 새 예고를 건다(예고 중인 것도 한 자리로 센다).
        while (_ghosts.Count + _pending.Count < maxAlive && _basicLeft + _fastLeft > 0)
        {
            if (BeginWarning() == false)
            {
                break; // 출현 지점이 없으면 이번 Tick은 포기
            }
        }

        RemainingInWave = _basicLeft + _fastLeft + _pending.Count + _ghosts.Count;

        // 이번 차수를 모두 처치했다.
        if (RemainingInWave == 0)
        {
            if (WaveIndex + 1 < waves.Length)
            {
                WaveGapTimer = TickTimer.CreateFromSeconds(Runner, waveGapSeconds);
                Debug.Log($"[NetworkCombatRoom] {name}: {WaveIndex + 1}차수 완료");
            }
            else
            {
                ClearRoom();
            }
        }
    }

    private void StartWave(int index)
    {
        if (waves == null || index >= waves.Length)
        {
            ClearRoom();
            return;
        }

        WaveIndex = index;
        WaveBannerTimer = TickTimer.CreateFromSeconds(Runner, spawnWarningSeconds);
        _basicLeft = waves[index].basicCount;
        _fastLeft = waves[index].fastCount;
        RemainingInWave = _basicLeft + _fastLeft;

        Debug.Log($"[NetworkCombatRoom] {name}: {index + 1}차수 시작 (기본형 {_basicLeft}, 빠른형 {_fastLeft})");
    }

    private void ClearRoom()
    {
        State = RoomState.Cleared;
        RemainingInWave = 0;
        ClearedBannerTimer = TickTimer.CreateFromSeconds(Runner, 3.0f);

        // UI 기획서 17장: 마지막 처치와 전멸이 겹치면 실패다.
        // 전원이 다운된 상태라면 부활시키지 않고, 게임 흐름이 실패로 판정하게 둔다.
        bool anyoneAlive = IsAnyPlayerAlive();

        // 기획서 7장: 방을 완료하면 다운된 플레이어가 체력 40으로 일어난다.
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerObject) == false)
            {
                continue;
            }

            NetworkPlayerHealth health = playerObject.GetComponent<NetworkPlayerHealth>();
            if (anyoneAlive && health != null && health.IsDown)
            {
                health.Revive();
            }

            // UI 기획서 12장: 방 완료 시 두 명의 탄창을 가득 채운다(생존자 체력 회복은 없음).
            NetworkShooter shooter = playerObject.GetComponent<NetworkShooter>();
            if (shooter != null)
            {
                shooter.ServerRefillMagazine();
            }
        }

        Debug.Log($"[NetworkCombatRoom] {name}: 방 정리 완료");
    }

    private bool IsAnyPlayerAlive()
    {
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerObject) == false)
            {
                continue;
            }

            NetworkPlayerHealth health = playerObject.GetComponent<NetworkPlayerHealth>();
            if (health == null || health.IsDown == false)
            {
                return true;
            }
        }

        return false;
    }

    // ───────── 출현 ─────────

    /// <summary>다음 유령의 종류와 위치를 정하고 1.5초 예고를 시작한다.</summary>
    private bool BeginWarning()
    {
        if (TryPickSpawnPosition(out Vector3 position) == false)
        {
            return false;
        }

        // 남은 기본형/빠른형 수에 비례해 무작위로 하나 뽑는다.
        GhostType type = Random.Range(0, _basicLeft + _fastLeft) < _basicLeft
            ? GhostType.Basic
            : GhostType.Fast;

        if (type == GhostType.Basic)
        {
            _basicLeft--;
        }
        else
        {
            _fastLeft--;
        }

        _pending.Add(new PendingSpawn
        {
            Position = position,
            Type = type,
            Timer = TickTimer.CreateFromSeconds(Runner, spawnWarningSeconds),
        });

        RpcShowSpawnWarning(position, spawnWarningSeconds);
        return true;
    }

    private void SpawnExpiredWarnings()
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].Timer.Expired(Runner) == false)
            {
                continue;
            }

            PendingSpawn pending = _pending[i];
            _pending.RemoveAt(i);

            NetworkGhost ghost = TrySpawnGhost(pending.Position);

            if (ghost == null)
            {
                // 생성 실패한 유령은 "처치"로 치지 않는다 → 방이 유령 없이 완료되는 것 방지.
                ReturnToWave(pending.Type);
                continue;
            }

            ghost.ServerSetType(pending.Type);
            _ghosts.Add(ghost);
        }
    }

    /// <summary>
    /// 유령을 생성한다. Ghost Prefab 연결이 잘못돼 있으면(예: 등록되지 않은 프리팹) 예외 대신
    /// 원인을 알려주는 에러를 한 번 남기고 이 방의 생성을 멈춘다.
    /// </summary>
    private NetworkGhost TrySpawnGhost(Vector3 position)
    {
        try
        {
            NetworkObject spawned = Runner.Spawn(ghostPrefab, position, Quaternion.identity);
            return spawned != null ? spawned.GetComponent<NetworkGhost>() : null;
        }
        catch (System.Exception e)
        {
            _spawnBroken = true;
            Debug.LogError(
                $"[NetworkCombatRoom] {name}: 유령 생성 실패 — Ghost Prefab 필드에 NetworkGhost.prefab을 " +
                $"Inspector에서 다시 드래그해 연결하세요. ({e.Message})");
            return null;
        }
    }

    private void ReturnToWave(GhostType type)
    {
        if (type == GhostType.Basic)
        {
            _basicLeft++;
        }
        else
        {
            _fastLeft++;
        }
    }

    /// <summary>
    /// 출현 지점을 고른다. 플레이어 바로 옆이나 이미 예고 중인 지점은 가능하면 피한다.
    /// 위치는 NavMesh 바닥에 맞춘다.
    /// </summary>
    private bool TryPickSpawnPosition(out Vector3 position)
    {
        position = default;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"[NetworkCombatRoom] {name}: Spawn Points가 비어 있습니다.");
            return false;
        }

        List<Transform> good = new List<Transform>();
        List<Transform> any = new List<Transform>();

        foreach (Transform point in spawnPoints)
        {
            if (point == null)
            {
                continue;
            }

            any.Add(point);

            if (IsNearPlayer(point.position) == false && IsPending(point.position) == false)
            {
                good.Add(point);
            }
        }

        // 좋은 지점이 하나도 없으면 아무 지점이나 쓴다(유령이 아예 안 나오는 것보다 낫다).
        List<Transform> candidates = good.Count > 0 ? good : any;

        if (candidates.Count == 0)
        {
            return false;
        }

        Vector3 raw = candidates[Random.Range(0, candidates.Count)].position;

        // 바닥(NavMesh)에 맞춘다. 못 찾으면 지점 위치를 그대로 쓴다.
        position = NavMesh.SamplePosition(raw, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)
            ? hit.position
            : raw;

        return true;
    }

    private bool IsNearPlayer(Vector3 position)
    {
        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerObject) == false)
            {
                continue;
            }

            Vector3 to = playerObject.transform.position - position;
            to.y = 0f;

            if (to.magnitude <= avoidPlayerRadius)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPending(Vector3 position)
    {
        foreach (PendingSpawn pending in _pending)
        {
            Vector3 to = pending.Position - position;
            to.y = 0f;

            // NavMesh 스냅으로 약간 어긋날 수 있어 여유를 둔다.
            if (to.magnitude < 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Host → 모든 참가자. 출현 예고 연출만 재생한다(게임 상태 변경 없음).
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcShowSpawnWarning(Vector3 position, float seconds)
    {
        FusionSpawnWarning.Spawn(position, seconds);
    }

    // ───────── 임시 표시 (정식 UI는 UI 담당이 교체) ─────────

    private void OnGUI()
    {
        // 게임이 끝나 멈춘 방은 결과 화면과 겹치지 않도록 표시하지 않는다.
        if (Object == null || Object.IsValid == false || Runner == null || IsHalted)
        {
            return;
        }

        string message = null;

        // UI 기획서 02장: 차수·남은 수는 상시 표시하지 않는다. 예고 순간과 완료 순간에만 잠깐 알린다.
        if (State == RoomState.Fighting && WaveBannerTimer.IsRunning && WaveBannerTimer.Expired(Runner) == false)
        {
            message = $"유령 출현 · {WaveIndex + 1}차";
        }
        else if (State == RoomState.Cleared && ClearedBannerTimer.IsRunning && ClearedBannerTimer.Expired(Runner) == false)
        {
            // 최종 방이면 게임이 끝나 방이 멈추므로(IsHalted) 이 문구는 자동으로 생략된다.
            message = "전투방 소탕 완료";
        }

        if (message != null)
        {
            // MSG: 중앙 HUD(남은 시간·소탕 완료) 아래
            GUI.Label(new Rect(Screen.width * 0.5f - 140f, 70f, 280f, 30f), message);
        }
    }
}
