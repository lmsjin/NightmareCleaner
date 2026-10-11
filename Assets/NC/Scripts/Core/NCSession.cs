using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>한 판의 진행 단계.</summary>
public enum NCPhase
{
    Entrance, // 저택 입구 — 두 명이 중앙홀 시작 영역에 모이길 기다림. 시간 정지
    Playing,  // 의뢰 진행 — 시간 흐름
    Victory,  // 의뢰 완료
    Defeat,   // 의뢰 실패
}

/// <summary>실패 원인.</summary>
public enum NCDefeatCause
{
    None,
    AllDown, // 전원 다운
    TimeUp,  // 시간 초과
}

/// <summary>
/// 한 판(의뢰)의 진행을 확정하는 호스트 관리자. 씬에 하나 배치하는 NetworkObject다.
///
///  입구 → (두 명이 시작 영역에) → 진행(10분) → 성공/실패 → [재도전] → 입구
///
/// 판정 순서(UI 기획서 17장): 전원 다운 → 모든 전투방 소탕(성공) → 시간 만료.
/// 마지막 처치와 전멸이 겹치면 실패, 마지막 처치와 시간 만료가 겹치면 성공이다.
/// 화면은 이 값들을 읽어 그리기만 한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NCSession : NetworkBehaviour
{
    public static NCSession Instance { get; private set; }

    [Header("빌더가 연결")]
    [Tooltip("중앙홀 시작 영역의 중심")]
    [SerializeField] private Transform startZone;

    // ───────── 확정 상태 ─────────

    [Networked] public NCPhase Phase { get; set; }
    [Networked] public NCDefeatCause Cause { get; set; }
    [Networked] private TickTimer MissionTimer { get; set; }

    /// <summary>시작 영역 안에 있는 인원 / 접속 인원(입구 안내용).</summary>
    [Networked] public int PlayersInZone { get; set; }
    [Networked] public int PlayerCount { get; set; }

    [Networked] public int ClearedRooms { get; set; }
    [Networked] public int TotalRooms { get; set; }

    /// <summary>결과: 경과 시간(1/100초, 내림), 팀 다운 횟수, 호스트 PC 최고 기록(0=없음), 신기록 여부.</summary>
    [Networked] public int ElapsedCentisec { get; set; }
    [Networked] public int TeamDownCount { get; set; }
    [Networked] public int BestCentisec { get; set; }
    [Networked] public NetworkBool IsNewRecord { get; set; }

    public bool IsMissionRunning => Phase == NCPhase.Playing;
    public bool IsFinished => Phase == NCPhase.Victory || Phase == NCPhase.Defeat;
    public Vector3 StartZonePosition => startZone != null ? startZone.position : Vector3.zero;

    /// <summary>남은 시간(초). 입구에서는 제한 시간 그대로.</summary>
    public float RemainingSeconds
    {
        get
        {
            NCBalance balance = NCBalance.Get;

            switch (Phase)
            {
                case NCPhase.Entrance:
                    return balance.missionSeconds;
                case NCPhase.Playing:
                    return Mathf.Max(0f, MissionTimer.RemainingTime(Runner) ?? 0f);
                default:
                    return Mathf.Max(0f, balance.missionSeconds - ElapsedCentisec / 100f);
            }
        }
    }

    // ───────── Host 전용 ─────────

    private readonly Dictionary<PlayerRef, bool> _wasDown = new Dictionary<PlayerRef, bool>();
    private bool _retryRequested;

    public override void Spawned()
    {
        Instance = this;

        if (Object.HasStateAuthority)
        {
            Phase = NCPhase.Entrance;
            TotalRooms = NCCombatRoom.All.Count;
            BestCentisec = PlayerPrefs.GetInt(NCBalance.Get.recordKey, 0);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>결과 화면의 [재도전]. 호스트만 의미가 있다(다음 Tick에 처리).</summary>
    public void RequestRetry()
    {
        if (Object != null && Object.HasStateAuthority && IsFinished)
        {
            _retryRequested = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        if (_retryRequested)
        {
            _retryRequested = false;
            ResetMission();
            return;
        }

        CountPlayers(out int count, out int inZone, out bool allDown);
        PlayerCount = count;
        PlayersInZone = inZone;

        switch (Phase)
        {
            case NCPhase.Entrance:
                if (count >= NCBalance.Get.RequiredPlayers && inZone == count)
                {
                    StartMission();
                }
                break;

            case NCPhase.Playing:
                Judge(allDown);
                break;
        }
    }

    private void StartMission()
    {
        Phase = NCPhase.Playing;
        Cause = NCDefeatCause.None;
        MissionTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.missionSeconds);
        _wasDown.Clear();
        Debug.Log("[NCSession] 의뢰 시작");
    }

    private void Judge(bool allDown)
    {
        ClearedRooms = CountClearedRooms();

        if (allDown)
        {
            Finish(NCPhase.Defeat, NCDefeatCause.AllDown);
        }
        else if (TotalRooms > 0 && ClearedRooms >= TotalRooms)
        {
            Finish(NCPhase.Victory, NCDefeatCause.None);
        }
        else if (MissionTimer.Expired(Runner))
        {
            Finish(NCPhase.Defeat, NCDefeatCause.TimeUp);
        }
    }

    private void Finish(NCPhase result, NCDefeatCause cause)
    {
        float total = NCBalance.Get.missionSeconds;
        float elapsed = Mathf.Clamp(total - RemainingSeconds, 0f, total);
        ElapsedCentisec = Mathf.FloorToInt(elapsed * 100f);

        // 성공 기록만 호스트 PC에 저장한다. 같은 표시 시간은 신기록이 아니다.
        if (result == NCPhase.Victory && (BestCentisec <= 0 || ElapsedCentisec < BestCentisec))
        {
            BestCentisec = ElapsedCentisec;
            IsNewRecord = true;
            PlayerPrefs.SetInt(NCBalance.Get.recordKey, ElapsedCentisec);
            PlayerPrefs.Save();
        }

        Phase = result;
        Cause = cause;
        MissionTimer = TickTimer.None;

        foreach (NCCombatRoom room in NCCombatRoom.All)
        {
            room.ServerHalt();
        }

        NCGhost.ServerDespawnAll(Runner);
        Debug.Log($"[NCSession] 결과: {result} {cause} {ElapsedCentisec / 100f:0.00}초 다운 {TeamDownCount}회");
    }

    /// <summary>재도전: 유령 제거 → 방 초기화 → 플레이어 풀피·탄창·입구 위치 → 입구 단계.</summary>
    private void ResetMission()
    {
        NCGhost.ServerDespawnAll(Runner);

        foreach (NCCombatRoom room in NCCombatRoom.All)
        {
            room.ServerReset();
        }

        foreach (NCPlayer player in NCPlayer.All)
        {
            player.Health.ServerResetFull();
            player.Gun.ServerRefill();

            Transform point = NCLauncher.Instance != null ? NCLauncher.Instance.GetEntrySpawn(player.Slot) : null;
            if (point != null)
            {
                player.ServerTeleport(point.position, point.rotation);
            }
        }

        Phase = NCPhase.Entrance;
        Cause = NCDefeatCause.None;
        MissionTimer = TickTimer.None;
        ClearedRooms = 0;
        ElapsedCentisec = 0;
        TeamDownCount = 0;
        IsNewRecord = false;
        _wasDown.Clear();
        Debug.Log("[NCSession] 재도전 — 입구로");
    }

    private void CountPlayers(out int count, out int inZone, out bool allDown)
    {
        count = 0;
        inZone = 0;
        int down = 0;
        float radius = NCBalance.Get.startZoneRadius;

        foreach (NCPlayer player in NCPlayer.All)
        {
            count++;
            bool isDown = player.Health.IsDown;

            if (isDown)
            {
                down++;
            }

            // 진행 중 "살아 있다가 다운된 순간"만 센다.
            if (Phase == NCPhase.Playing)
            {
                if (isDown && _wasDown.TryGetValue(player.Object.InputAuthority, out bool was) && was == false)
                {
                    TeamDownCount++;
                }

                _wasDown[player.Object.InputAuthority] = isDown;
            }

            if (startZone != null && NCMath.FlatDistance(player.transform.position, startZone.position) <= radius)
            {
                inZone++;
            }
        }

        allDown = count > 0 && down == count;
    }

    private static int CountClearedRooms()
    {
        int cleared = 0;

        foreach (NCCombatRoom room in NCCombatRoom.All)
        {
            if (room.State == NCRoomState.Cleared)
            {
                cleared++;
            }
        }

        return cleared;
    }
}
