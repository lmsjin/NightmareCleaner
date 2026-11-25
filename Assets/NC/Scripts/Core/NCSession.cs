using Fusion;
using UnityEngine;

/// <summary>경기 단계.</summary>
public enum NCPhase
{
    Lobby,
    Countdown,
    Playing,
    Result,
}

/// <summary>점령 구역 상태.</summary>
public enum NCZoneState
{
    Empty,
    SlotA,
    SlotB,
    Contested,
}

/// <summary>종료 사유.</summary>
public enum NCEndCause
{
    None,
    Dominance,
    TimeUp,
    Draw,
    OpponentLeft,
}

/// <summary>
/// 1:1 점령전 한 경기를 확정하는 호스트 관리자(씬 배치 NetworkObject).
///  대기실 → 카운트다운 → 진행 → 결과 → 대기실
/// 한 Tick 순서(기획서 12장): 피해·사망 → 캠프 보상 → 점령 인원 → 지배율 → 100%·시간 → 결과 1회.
/// 플레이어 이동·사격 뒤에 돌도록 실행 순서를 늦춘다.
/// </summary>
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(NetworkObject))]
public class NCSession : NetworkBehaviour
{
    public const float WinDominance = 100f;

    public static NCSession Instance { get; private set; }

    [Tooltip("중앙 점령 구역의 중심")]
    [SerializeField] private Transform startZone;

    [Networked] public NCPhase Phase { get; set; }
    [Networked] public NCEndCause Cause { get; set; }
    [Networked] public NCZoneState Zone { get; set; }
    [Networked] private TickTimer PhaseTimer { get; set; }

    /// <summary>승자 슬롯. 무승부면 -1.</summary>
    [Networked] public int WinnerSlot { get; set; }

    /// <summary>슬롯별 지배율(0~100, 내부 소수).</summary>
    [Networked, Capacity(2)] public NetworkArray<float> Dominance => default;

    // 결과 화면 통계(슬롯별, 판정에는 쓰지 않는다). 플레이어가 나가도 남도록 여기에 둔다.
    [Networked, Capacity(2)] public NetworkArray<int> Kills => default;
    [Networked, Capacity(2)] public NetworkArray<int> Deaths => default;
    [Networked, Capacity(2)] public NetworkArray<int> LastHits => default;
    [Networked, Capacity(2)] public NetworkArray<int> Shots => default;
    [Networked, Capacity(2)] public NetworkArray<int> Hits => default;
    [Networked, Capacity(2)] public NetworkArray<float> ZoneSeconds => default;

    /// <summary>결과에 남길 남은 시간.</summary>
    [Networked] private float FinalRemaining { get; set; }

    /// <summary>준비·카운트다운 중 상대가 나간 횟수(대기실 안내용).</summary>
    [Networked] public int PartnerLeftCount { get; set; }

    public bool IsPlaying => Phase == NCPhase.Playing;
    public bool IsFinished => Phase == NCPhase.Result;
    public Vector3 ZonePosition => startZone != null ? startZone.position : Vector3.zero;

    public float RemainingSeconds
    {
        get
        {
            switch (Phase)
            {
                case NCPhase.Playing: return Mathf.Max(0f, PhaseTimer.RemainingTime(Runner) ?? 0f);
                case NCPhase.Result: return FinalRemaining;
                default: return NCBalance.Get.matchSeconds;
            }
        }
    }

    public float CountdownSeconds => Phase == NCPhase.Countdown ? Mathf.Max(0f, PhaseTimer.RemainingTime(Runner) ?? 0f) : 0f;

    /// <summary>화면 표시용 정수 지배율. 내림이라 100% 전에는 100이 나오지 않는다.</summary>
    public int DominancePercent(int slot) => Mathf.FloorToInt(Dominance[slot]);

    private bool _retryRequested;
    private bool _startRequested;
    private int _lastPlayerCount;
    private Renderer[] _zoneRenderers;
    private int _shownZone = -1;

    public override void Spawned()
    {
        Instance = this;

        // 두 플레이어는 서로의 이동을 막지 않는다.
        Physics.IgnoreLayerCollision(NCPlayer.Layer, NCPlayer.Layer, true);

        if (startZone != null)
        {
            _zoneRenderers = startZone.GetComponentsInChildren<Renderer>();
        }

        if (Object.HasStateAuthority)
        {
            Phase = NCPhase.Lobby;
            WinnerSlot = -1;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>결과 화면의 [대기실로]. 호스트만.</summary>
    public void RequestRetry()
    {
        if (Object != null && Object.HasStateAuthority && IsFinished)
        {
            _retryRequested = true;
        }
    }

    /// <summary>대기실의 [게임 시작]. 호스트만.</summary>
    public void RequestStart()
    {
        if (Object != null && Object.HasStateAuthority && Phase == NCPhase.Lobby)
        {
            _startRequested = true;
        }
    }

    public bool CanStart
    {
        get
        {
            int ready = 0;
            foreach (NCPlayer player in NCPlayer.All)
            {
                ready += player.Ready ? 1 : 0;
            }

            int count = NCPlayer.All.Count;
            return Phase == NCPhase.Lobby && count >= NCBalance.Get.RequiredPlayers && ready == count;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        int count = NCPlayer.All.Count;
        bool someoneLeft = count < _lastPlayerCount;
        _lastPlayerCount = count;

        if (someoneLeft)
        {
            HandleLeave();
        }

        switch (Phase)
        {
            case NCPhase.Lobby:
                if (_startRequested)
                {
                    _startRequested = false;

                    if (CanStart)
                    {
                        StartCountdown();
                    }
                }
                break;

            case NCPhase.Countdown:
                if (PhaseTimer.Expired(Runner))
                {
                    Phase = NCPhase.Playing;
                    PhaseTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.matchSeconds);
                    Debug.Log("[NCSession] 경기 시작");
                }
                break;

            case NCPhase.Playing:
                TickMatch();
                break;

            case NCPhase.Result:
                if (_retryRequested)
                {
                    ResetMatch();
                    Phase = NCPhase.Lobby;
                }
                break;
        }

        _retryRequested = false;
    }

    private void HandleLeave()
    {
        switch (Phase)
        {
            case NCPhase.Lobby:
                PartnerLeftCount++;
                break;

            case NCPhase.Countdown:
                PartnerLeftCount++;
                ResetMatch();
                Phase = NCPhase.Lobby;
                break;

            case NCPhase.Playing:
                // 호스트가 남아 있어야 이 코드가 돌므로 남은 사람이 호스트다.
                Finish(NCPlayer.Local != null ? NCPlayer.Local.Slot : 0, NCEndCause.OpponentLeft);
                break;
        }
    }

    private void StartCountdown()
    {
        ResetMatch();
        Phase = NCPhase.Countdown;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.countdownSeconds);

        foreach (NCGhostCamp camp in NCGhostCamp.All)
        {
            camp.ServerStart();
        }
    }

    private void TickMatch()
    {
        // ① 피해를 모두 모은 뒤 한꺼번에 확정 → 사망·부활·회복
        NCBulletSystem bullets = NCBulletSystem.Instance;
        if (bullets != null)
        {
            bullets.ServerStep();
        }

        NCGhost.ServerTickAll();

        if (bullets != null)
        {
            bullets.ServerApplyHits();
        }

        foreach (NCPlayer player in NCPlayer.All)
        {
            player.Health.ServerTick();
        }

        // ② 캠프 전멸·보상
        foreach (NCGhostCamp camp in NCGhostCamp.All)
        {
            camp.ServerTick();
        }

        // ③ 점령 인원
        bool a = false;
        bool b = false;

        foreach (NCPlayer player in NCPlayer.All)
        {
            if (player.Health.IsDown || IsInZone(player.transform.position) == false)
            {
                continue;
            }

            player.Health.ServerEndProtection();
            ZoneSeconds.Set(player.Slot, ZoneSeconds[player.Slot] + Runner.DeltaTime);
            a |= player.Slot == 0;
            b |= player.Slot == 1;
        }

        Zone = a && b ? NCZoneState.Contested : (a ? NCZoneState.SlotA : (b ? NCZoneState.SlotB : NCZoneState.Empty));

        // ④ 지배율
        if (a != b)
        {
            int slot = a ? 0 : 1;
            float next = Dominance[slot] + NCBalance.Get.dominancePerSecond * Runner.DeltaTime;
            Dominance.Set(slot, Mathf.Min(WinDominance, next));
        }

        // ⑤ 100% → 시간 종료 순서로 검사
        if (Dominance[0] >= WinDominance)
        {
            Finish(0, NCEndCause.Dominance);
        }
        else if (Dominance[1] >= WinDominance)
        {
            Finish(1, NCEndCause.Dominance);
        }
        else if (PhaseTimer.Expired(Runner))
        {
            float diff = Dominance[0] - Dominance[1];
            if (diff == 0f)
            {
                Finish(-1, NCEndCause.Draw);
            }
            else
            {
                Finish(diff > 0f ? 0 : 1, NCEndCause.TimeUp);
            }
        }
    }

    // ───────── 통계 (Host 전용) ─────────

    public void ServerCountShot(int slot) => Shots.Set(slot, Shots[slot] + 1);
    public void ServerCountHit(int slot) => Hits.Set(slot, Hits[slot] + 1);
    public void ServerCountLastHit(int slot) => LastHits.Set(slot, LastHits[slot] + 1);

    /// <summary>killerSlot이 -1이면 유령에게 죽은 것.</summary>
    public void ServerCountKill(int killerSlot, int victimSlot)
    {
        Deaths.Set(victimSlot, Deaths[victimSlot] + 1);

        if (killerSlot >= 0)
        {
            Kills.Set(killerSlot, Kills[killerSlot] + 1);
        }
    }

    public bool IsInZone(Vector3 position)
    {
        return startZone != null && NCMath.FlatDistance(position, startZone.position) <= NCBalance.Get.captureRadius;
    }

    private void Finish(int winner, NCEndCause cause)
    {
        FinalRemaining = RemainingSeconds;
        WinnerSlot = winner;
        Cause = cause;
        Phase = NCPhase.Result;
        PhaseTimer = TickTimer.None;

        if (NCBulletSystem.Instance != null)
        {
            NCBulletSystem.Instance.ServerClear();
        }

        NCGhost.ServerStopAll();

        foreach (NCPlayer player in NCPlayer.All)
        {
            player.Gun.ServerResetCharges();
            player.Ready = false;
        }

        Debug.Log($"[NCSession] 결과: 승자 {winner} {cause} A {Dominance[0]:0.00} B {Dominance[1]:0.00}");
    }

    /// <summary>경기 시작·대기실 복귀의 공통 초기화.</summary>
    private void ResetMatch()
    {
        if (NCBulletSystem.Instance != null)
        {
            NCBulletSystem.Instance.ServerClear();
        }

        foreach (NCGhostCamp camp in NCGhostCamp.All)
        {
            camp.ServerReset();
        }

        NCGhost.ServerDespawnAll(Runner);

        foreach (NCPlayer player in NCPlayer.All)
        {
            player.Ready = false;
            player.Health.ServerResetFull();
            player.Gun.ServerResetAll();
            player.ServerMoveToSpawn();
        }

        for (int slot = 0; slot < 2; slot++)
        {
            Dominance.Set(slot, 0f);
            Kills.Set(slot, 0);
            Deaths.Set(slot, 0);
            LastHits.Set(slot, 0);
            Shots.Set(slot, 0);
            Hits.Set(slot, 0);
            ZoneSeconds.Set(slot, 0f);
        }

        Zone = NCZoneState.Empty;
        Cause = NCEndCause.None;
        WinnerSlot = -1;
        FinalRemaining = 0f;
        PhaseTimer = TickTimer.None;
        _startRequested = false;
    }

    public static Color ZoneColor(NCZoneState zone)
    {
        switch (zone)
        {
            case NCZoneState.SlotA: return NCStyle.P1;
            case NCZoneState.SlotB: return NCStyle.P2;
            case NCZoneState.Contested: return NCStyle.Danger;
            default: return NCStyle.Text;
        }
    }

    public override void Render()
    {
        if (_zoneRenderers == null || _shownZone == (int)Zone)
        {
            return;
        }

        _shownZone = (int)Zone;
        Color color = ZoneColor(Zone);

        foreach (Renderer r in _zoneRenderers)
        {
            color.a = r.name == "Ring" ? 0.3f : 0.15f;
            NCFx.SetColor(r, color);
        }
    }
}
