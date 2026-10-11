using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>판 전체의 진행 단계.</summary>
public enum GamePhase
{
    Waiting, // 동료 접속 대기 — 타이머가 가지 않는다
    Playing, // 제한 시간 진행 중
    Victory, // 모든 전투방 완료
    Defeat   // 시간 초과 또는 전원 다운
}

/// <summary>실패 사유(결과 화면 표시용).</summary>
public enum DefeatReason
{
    None,
    TimeUp, // 제한 시간 초과
    AllDown // 모든 플레이어가 다운
}

/// <summary>
/// 판 전체의 흐름을 관리한다. 기획서 기준:
///
///   2명 접속 → 10분 제한 시작 → 모든 전투방 완료 = 승리 / 시간 초과·전원 다운 = 실패 → 결과 → 재시작.
///
/// 재시작은 씬을 다시 불러오지 않고 **제자리 초기화**로 한다.
/// (씬에 NetworkManager·접속 UI가 함께 있어 씬 재로드 시 접속까지 끊길 위험이 있기 때문)
///   → 유령 제거, 전투방·가구 초기화, 플레이어 풀피·탄창 채움·시작 위치 이동.
///
/// 판정은 Host만 하고, 결과는 [Networked] 값으로 모든 화면에 전달된다.
/// 씬에 하나만 배치하는 NetworkObject다. 전투방 목록은 자동으로 찾는다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkGameFlow : NetworkBehaviour
{
    [Header("시작 조건")]
    [Tooltip("이 인원이 모여야 타이머가 시작된다. 기획서는 2인 고정. 혼자 테스트할 때만 1로 낮춘다")]
    [Min(1)]
    [SerializeField] private int requiredPlayers = 2;

    [Tooltip("중앙홀 시작 영역의 중심(바닥의 빈 오브젝트). 필요 인원 전원이 이 안에 들어오면 의뢰가 시작된다. " +
             "비워두면 인원만 모이면 바로 시작한다")]
    [SerializeField] private Transform startZone;

    [Tooltip("시작 영역 반경(m)")]
    [SerializeField] private float startZoneRadius = 3.0f;

    [Header("기록")]
    [Tooltip("최고 기록 저장 키. 규칙(수치)이 바뀌면 버전을 올려 이전 기록과 비교하지 않는다")]
    [SerializeField] private string bestRecordKey = "NC_Best_v1";

    [Header("재시작")]
    [Tooltip("재시작할 때 플레이어를 옮길 위치. PlayerSpawner의 Spawn Points와 같은 것을 연결한다")]
    [SerializeField] private Transform[] playerStartPoints;

    [Header("설정 (Spawned에서 CleanerBalance 값으로 덮어씀)")]
    [SerializeField] private float missionSeconds = 600f;

    // ───────── 네트워크 공유 상태 ─────────

    /// <summary>현재 진행 단계.</summary>
    [Networked] public GamePhase Phase { get; set; }

    /// <summary>실패 사유.</summary>
    [Networked] public DefeatReason Reason { get; set; }

    /// <summary>제한 시간 타이머.</summary>
    [Networked] private TickTimer MissionTimer { get; set; }

    /// <summary>완료한 전투방 수 / 전체 전투방 수.</summary>
    [Networked] public int ClearedRooms { get; set; }
    [Networked] public int TotalRooms { get; set; }

    /// <summary>현재 접속 인원(대기 화면 표시용).</summary>
    [Networked] private int PlayerCount { get; set; }

    /// <summary>시작 영역 안에 있는 인원(대기 안내용).</summary>
    [Networked] private int PlayersInStartZone { get; set; }

    /// <summary>게임이 끝났을 때까지 걸린 시간(1/100초, 내림). 결과 화면 표시용.</summary>
    [Networked] public int ElapsedCentisec { get; set; }

    /// <summary>이번 판에서 팀이 다운된 횟수(두 명 합계).</summary>
    [Networked] public int TeamDownCount { get; set; }

    /// <summary>호스트 PC에 저장된 최고 기록(1/100초). 0이면 기록 없음.</summary>
    [Networked] public int BestCentisec { get; set; }

    /// <summary>이번 판이 최고 기록을 갱신했는지.</summary>
    [Networked] public NetworkBool IsNewRecord { get; set; }

    /// <summary>의뢰 진행 중인지. 시작 전·종료 후에는 정리·보급·Q가 막힌다.</summary>
    public bool IsMissionRunning => Phase == GamePhase.Playing;

    // ───────── Host 전용 ─────────

    private NetworkCombatRoom[] _rooms;

    // 재시작 버튼(OnGUI)은 Tick 밖에서 눌리므로, 표시만 해 두고 다음 Tick에 처리한다.
    // (플레이어 순간이동 NetworkCharacterController.Teleport는 Tick 안에서 해야 한다)
    private bool _restartRequested;

    // 다운 횟수 집계용: 플레이어별 직전 Tick의 다운 여부
    private readonly Dictionary<PlayerRef, bool> _wasDown = new Dictionary<PlayerRef, bool>();

    /// <summary>남은 제한 시간(초). 진행 중이 아니면 0.</summary>
    public float RemainingSeconds => Phase == GamePhase.Playing
        ? Mathf.Max(0f, MissionTimer.RemainingTime(Runner) ?? 0f)
        : 0f;

    public override void Spawned()
    {
        CleanerBalance balance = CleanerBalance.Resolve(null);

        if (balance != null)
        {
            missionSeconds = balance.missionSeconds;
        }

        _rooms = FindObjectsByType<NetworkCombatRoom>(FindObjectsSortMode.None);

        if (Object.HasStateAuthority)
        {
            Phase = GamePhase.Waiting;
            TotalRooms = _rooms.Length;
            BestCentisec = PlayerPrefs.GetInt(bestRecordKey, 0);

            if (_rooms.Length == 0)
            {
                Debug.LogWarning("[NetworkGameFlow] 씬에 NetworkCombatRoom이 없습니다. 승리 조건을 판정할 수 없습니다.");
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 판정은 Host만 한다.
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        PlayerCount = CountPlayers(out bool allDown, out int inZone);
        PlayersInStartZone = inZone;

        if (_restartRequested)
        {
            _restartRequested = false;
            Restart();
            return;
        }

        switch (Phase)
        {
            case GamePhase.Waiting:
                // UI 기획서 06장: 필요 인원 전원이 중앙홀 시작 영역에 모여야 시작한다.
                bool zoneReady = startZone == null || inZone >= PlayerCount;
                if (PlayerCount >= requiredPlayers && zoneReady)
                {
                    StartMission();
                }
                break;

            case GamePhase.Playing:
                TickPlaying(allDown);
                break;
        }
    }

    // ───────── 진행 ─────────

    private void StartMission()
    {
        Phase = GamePhase.Playing;
        Reason = DefeatReason.None;
        ClearResultData();
        MissionTimer = TickTimer.CreateFromSeconds(Runner, missionSeconds);

        Debug.Log($"[NetworkGameFlow] 미션 시작 — 제한 시간 {missionSeconds}초");
    }

    private void TickPlaying(bool allDown)
    {
        ClearedRooms = CountClearedRooms();

        // UI 기획서 17장 판정 순서: 전원 다운 → 최종 방 완료(승리) → 시간 만료.
        // 마지막 처치와 전멸이 겹치면 실패, 최종 처치와 시간 만료가 겹치면 성공.
        if (allDown)
        {
            EndGame(GamePhase.Defeat, DefeatReason.AllDown);
        }
        else if (TotalRooms > 0 && ClearedRooms >= TotalRooms)
        {
            EndGame(GamePhase.Victory, DefeatReason.None);
        }
        else if (MissionTimer.Expired(Runner))
        {
            EndGame(GamePhase.Defeat, DefeatReason.TimeUp);
        }
    }

    private void EndGame(GamePhase result, DefeatReason reason)
    {
        // 실제 경과 시간을 0.01초 단위로 내려 확정한다.
        float elapsed = Mathf.Clamp(missionSeconds - RemainingSeconds, 0f, missionSeconds);
        ElapsedCentisec = Mathf.FloorToInt(elapsed * 100f);

        // 성공 기록만 호스트 PC에 저장한다. 같은 시간은 신기록이 아니다.
        if (result == GamePhase.Victory && (BestCentisec <= 0 || ElapsedCentisec < BestCentisec))
        {
            BestCentisec = ElapsedCentisec;
            IsNewRecord = true;
            PlayerPrefs.SetInt(bestRecordKey, ElapsedCentisec);
            PlayerPrefs.Save();
        }

        Phase = result;
        Reason = reason;
        MissionTimer = TickTimer.None;

        // 모든 방을 멈추고 남은 유령을 치운다.
        foreach (NetworkCombatRoom room in _rooms)
        {
            if (room != null)
            {
                room.ServerHalt();
            }
        }

        DespawnAllGhosts();

        Debug.Log($"[NetworkGameFlow] 게임 종료 — {result} ({reason}), {FormatTime(ElapsedCentisec)}, 팀 다운 {TeamDownCount}회");
    }

    /// <summary>
    /// 제자리 초기화. 유령 제거 → 방·가구 초기화 → 플레이어 풀피·탄창·시작 위치 → 대기 단계로.
    /// 인원이 충분하면 다음 Tick에 바로 타이머가 다시 시작된다.
    /// </summary>
    private void Restart()
    {
        DespawnAllGhosts();

        foreach (NetworkCombatRoom room in _rooms)
        {
            if (room != null)
            {
                room.ServerReset();
            }
        }

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerObject) == false)
            {
                continue;
            }

            NetworkPlayerHealth health = playerObject.GetComponent<NetworkPlayerHealth>();
            if (health != null)
            {
                health.ServerResetFull();
            }

            NetworkShooter shooter = playerObject.GetComponent<NetworkShooter>();
            if (shooter != null)
            {
                shooter.ServerRefillMagazine();
            }

            TeleportToStart(player, playerObject);
        }

        Phase = GamePhase.Waiting;
        Reason = DefeatReason.None;
        ClearResultData();
        MissionTimer = TickTimer.None;

        Debug.Log("[NetworkGameFlow] 재시작");
    }

    private void ClearResultData()
    {
        ElapsedCentisec = 0;
        ClearedRooms = 0;
        TeamDownCount = 0;
        IsNewRecord = false;
        _wasDown.Clear();
    }

    private void TeleportToStart(PlayerRef player, NetworkObject playerObject)
    {
        if (playerStartPoints == null || playerStartPoints.Length == 0)
        {
            return; // 연결 안 했으면 제자리에서 다시 시작
        }

        // PlayerSpawner와 같은 규칙으로 위치를 고른다(플레이어끼리 겹치지 않게).
        Transform point = playerStartPoints[Mathf.Abs(player.AsIndex) % playerStartPoints.Length];
        if (point == null)
        {
            return;
        }

        NetworkCharacterController controller = playerObject.GetComponent<NetworkCharacterController>();
        if (controller != null)
        {
            controller.Teleport(point.position, point.rotation);
        }
        else
        {
            playerObject.transform.SetPositionAndRotation(point.position, point.rotation);
        }
    }

    // ───────── 조회 보조 ─────────

    /// <summary>
    /// 접속 인원·전원 다운·시작 영역 인원을 센다.
    /// 진행 중이면 "살아 있다가 다운된 순간"을 팀 다운 횟수로 센다.
    /// </summary>
    private int CountPlayers(out bool allDown, out int inZone)
    {
        int count = 0;
        int down = 0;
        inZone = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerObject) == false)
            {
                continue;
            }

            count++;

            NetworkPlayerHealth health = playerObject.GetComponent<NetworkPlayerHealth>();
            bool isDown = health != null && health.IsDown;

            if (isDown)
            {
                down++;
            }

            if (Phase == GamePhase.Playing)
            {
                if (isDown && _wasDown.TryGetValue(player, out bool was) && was == false)
                {
                    TeamDownCount++;
                }

                _wasDown[player] = isDown;
            }

            if (startZone != null)
            {
                Vector3 to = playerObject.transform.position - startZone.position;
                to.y = 0f;
                if (to.magnitude <= startZoneRadius)
                {
                    inZone++;
                }
            }
        }

        allDown = count > 0 && down == count;
        return count;
    }

    private int CountClearedRooms()
    {
        int cleared = 0;

        foreach (NetworkCombatRoom room in _rooms)
        {
            if (room != null && room.IsCleared)
            {
                cleared++;
            }
        }

        return cleared;
    }

    private void DespawnAllGhosts()
    {
        // 테스트 스포너가 만든 유령까지 모두 치운다.
        foreach (NetworkGhost ghost in FindObjectsByType<NetworkGhost>(FindObjectsSortMode.None))
        {
            if (ghost.Object != null && ghost.Object.IsValid)
            {
                Runner.Despawn(ghost.Object);
            }
        }
    }

    // ───────── 임시 표시 (정식 UI는 UI 담당이 교체) ─────────

    private void OnGUI()
    {
        if (Object == null || Object.IsValid == false || Runner == null)
        {
            return;
        }

        switch (Phase)
        {
            case GamePhase.Waiting:
                // HUD-01·02 시작 전 상태 + 저택 입구 안내(ENTRY-01·02)
                DrawTopCenter(Mathf.CeilToInt(missionSeconds), "시작 전");
                string waiting = PlayerCount < requiredPlayers
                    ? $"동료를 기다리는 중... ({PlayerCount}/{requiredPlayers})"
                    : $"동료와 중앙홀로 이동하세요 ({PlayersInStartZone}/{PlayerCount})";
                GUI.Label(new Rect(Screen.width * 0.5f - 230f, 100f, 460f, 24f),
                    "다섯 전투방의 유령을 소탕하세요. 잡동사니를 함께 치우면 유령이 나타납니다.");
                GUI.Label(new Rect(Screen.width * 0.5f - 230f, 124f, 460f, 24f), waiting);
                break;

            case GamePhase.Playing:
                // HUD-01·02 남은 시간(0 이상, 올림) · 소탕 완료 N/5
                DrawTopCenter(Mathf.CeilToInt(Mathf.Max(0f, RemainingSeconds)), null);
                break;

            case GamePhase.Victory:
            case GamePhase.Defeat:
                DrawResult();
                break;
        }
    }

    private void DrawTopCenter(int seconds, string suffix)
    {
        string time = $"남은 시간 {seconds / 60:00}:{seconds % 60:00}" + (suffix != null ? $" ({suffix})" : "");
        GUI.Label(new Rect(Screen.width * 0.5f - 110f, 16f, 220f, 24f), time);
        GUI.Label(new Rect(Screen.width * 0.5f - 110f, 40f, 220f, 24f), $"소탕 완료 {ClearedRooms}/{TotalRooms}");
    }

    private void DrawResult()
    {
        Rect box = new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f - 120f, 360f, 240f);
        GUI.Box(box, GUIContent.none);

        bool victory = Phase == GamePhase.Victory;
        string cause = Reason == DefeatReason.TimeUp ? "시간 초과" : "전원 다운";
        string title = victory ? "의뢰 완료 · 다섯 전투방 소탕 완료" : $"의뢰 실패 · {cause}";
        string best = BestCentisec > 0 ? FormatTime(BestCentisec) : "기록 없음";

        float y = box.y + 16f;
        GUI.Label(new Rect(box.x + 20f, y, 320f, 24f), title); y += 30f;
        GUI.Label(new Rect(box.x + 20f, y, 320f, 24f), $"소탕 완료 {ClearedRooms}/{TotalRooms}"); y += 24f;
        GUI.Label(new Rect(box.x + 20f, y, 320f, 24f), $"{(victory ? "완료 시간" : "경과 시간")} {FormatTime(ElapsedCentisec)}"); y += 24f;
        GUI.Label(new Rect(box.x + 20f, y, 320f, 24f), $"호스트 PC 최고 기록 {best}{(IsNewRecord ? "  (신기록!)" : "")}"); y += 24f;
        GUI.Label(new Rect(box.x + 20f, y, 320f, 24f), $"팀 다운 횟수 {TeamDownCount}회"); y += 24f;

        if (victory == false)
        {
            GUI.Label(new Rect(box.x + 20f, y, 320f, 24f), "실패한 판은 기록에 등록되지 않습니다");
        }

        Rect bottom = new Rect(box.x + 20f, box.y + 190f, 320f, 36f);

        // 재시작은 Host만 결정한다.
        if (Runner.IsServer)
        {
            if (GUI.Button(bottom, "재도전"))
            {
                _restartRequested = true;
            }
        }
        else
        {
            GUI.Label(bottom, "호스트가 다시 시작하기를 기다리는 중...");
        }
    }

    /// <summary>1/100초 값을 MM:SS.cc 로 표시한다.</summary>
    private static string FormatTime(int centisec)
    {
        int totalSeconds = centisec / 100;
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}.{centisec % 100:00}";
    }
}
