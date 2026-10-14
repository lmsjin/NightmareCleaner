using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>전투방 진행 상태(UI 기획서 16장).</summary>
public enum NCRoomState
{
    Untouched,    // 미완료 — 잡동사니 더미가 있다
    Clearing,     // 공동 정리 중(취소하면 미완료로)
    Warning,      // n차 출현 예고
    Fighting,     // n차 전투
    Intermission, // 차수 사이 잠깐의 간격
    Cleared,      // 소탕 완료
}

/// <summary>공동 정리를 시작할 수 없는 이유(안내 문구용).</summary>
public enum NCRoomBlock
{
    None,
    NotStarted,   // 의뢰 시작 전
    OtherRoom,    // 다른 방 전투 중
    DoorOccupied, // 출입구 봉쇄 영역에 사람이 있다
}

/// <summary>
/// 전투방 하나의 관리자. 방 상태 하나를 기준으로 더미·봉쇄·유령·안내가 함께 바뀐다.
///
///  미완료 → (두 명이 더미 2m 안에서 E 2초) → 더미 제거·봉쇄·1차 예고 → 1차 전투
///        → 2차 예고 → 2차 전투 → (예약된 출현과 살아 있는 유령이 모두 0) → 소탕 완료
///
/// 소탕 완료 1회 처리: 봉쇄 해제 · 두 명 탄창 가득 · 다운 동료 체력 40 부활(전원 다운이면 부활 없음).
/// 판정은 Host만, 화면은 [Networked] 상태를 보고 그린다. 씬 배치 NetworkObject.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NCCombatRoom : NetworkBehaviour
{
    /// <summary>씬의 모든 전투방(씬 로드 시 등록).</summary>
    public static readonly List<NCCombatRoom> All = new List<NCCombatRoom>();

    [System.Serializable]
    public class Wave
    {
        [Min(0)] public int basic = 3;
        [Min(0)] public int fast = 1;
    }

    [Header("방 정보")]
    [SerializeField] private string roomName = "연회장";

    [Header("빌더가 연결")]
    [SerializeField] private NetworkObject ghostPrefab;
    [Tooltip("잡동사니 더미(정리 완료 시 사라짐)")]
    [SerializeField] private Transform dummy;
    [Tooltip("방 내부 영역(트리거 박스). 두 명이 이 안에 있어야 정리할 수 있다")]
    [SerializeField] private BoxCollider area;
    [Tooltip("출입구 봉쇄 영역. 정리 완료 순간 여기에 사람이 있으면 봉쇄를 보류한다")]
    [SerializeField] private BoxCollider[] sealZones;
    [Tooltip("봉쇄 장벽 오브젝트(콜라이더 + NavMeshObstacle + 표시)")]
    [SerializeField] private GameObject[] barriers;
    [Tooltip("유령 출현 지점")]
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("방 위에 떠 있는 이름표(상태에 따라 문구·색이 바뀜)")]
    [SerializeField] private TextMeshPro label;

    [Header("유령 구성")]
    [SerializeField] private Wave[] waves =
    {
        new Wave { basic = 3, fast = 1 },
        new Wave { basic = 3, fast = 2 },
    };

    [Tooltip("동시에 존재할 수 있는 유령 수(예고 중 포함)")]
    [Min(1)]
    [SerializeField] private int maxAlive = 4;

    // ───────── 확정 상태 ─────────

    [Networked] public NCRoomState State { get; set; }
    [Networked] public int WaveIndex { get; set; }
    [Networked] public float ClearProgress { get; set; }
    [Networked] public NCRoomBlock Block { get; set; }

    /// <summary>더미 근처에서 E를 누르고 있는 슬롯(비트 0 = 1P, 비트 1 = 2P).</summary>
    [Networked] public int HoldingMask { get; set; }

    /// <summary>이번 차수에 남은 유령(안 나온 수 + 예고 중 + 살아 있음).</summary>
    [Networked] public int Remaining { get; set; }

    /// <summary>게임 종료로 멈춘 상태.</summary>
    [Networked] public NetworkBool IsHalted { get; set; }

    [Networked] private TickTimer PhaseTimer { get; set; }

    public string RoomName => roomName;
    public int WaveCount => waves != null ? waves.Length : 0;
    public Vector3 DummyPosition => dummy != null ? dummy.position : transform.position;
    public bool IsBusy => State == NCRoomState.Warning || State == NCRoomState.Fighting || State == NCRoomState.Intermission;
    public float ClearRatio => Mathf.Clamp01(ClearProgress / NCBalance.Get.dummyHoldSeconds);

    // ───────── Host 전용 ─────────

    private struct Pending
    {
        public Vector3 Position;
        public NCGhostType Type;
        public TickTimer Timer;
    }

    private readonly List<Pending> _pending = new List<Pending>();
    private readonly List<NCGhost> _alive = new List<NCGhost>();
    private int _basicLeft;
    private int _fastLeft;

    // 이름표를 상태가 바뀔 때만 갱신하기 위한 이전 값(연출용)
    private int _shownLabelState = -1;

    private void Awake()
    {
        All.Add(this);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    public bool ContainsPoint(Vector3 position)
    {
        return area != null && area.bounds.Contains(new Vector3(position.x, area.bounds.center.y, position.z));
    }

    // ───────── 게임 흐름이 부르는 제어 ─────────

    /// <summary>게임 종료: 내 유령·예약을 없애고 멈춘다.</summary>
    public void ServerHalt()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        foreach (NCGhost ghost in _alive)
        {
            if (ghost != null && ghost.Object != null && ghost.Object.IsValid)
            {
                Runner.Despawn(ghost.Object);
            }
        }

        _alive.Clear();
        _pending.Clear();
        IsHalted = true;
    }

    /// <summary>재도전: 처음(미완료) 상태로.</summary>
    public void ServerReset()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        ServerHalt();
        State = NCRoomState.Untouched;
        WaveIndex = 0;
        ClearProgress = 0f;
        Block = NCRoomBlock.None;
        HoldingMask = 0;
        Remaining = 0;
        PhaseTimer = TickTimer.None;
        _basicLeft = 0;
        _fastLeft = 0;
        IsHalted = false;
    }

    // ───────── Host 진행 ─────────

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false || IsHalted)
        {
            return;
        }

        switch (State)
        {
            case NCRoomState.Untouched:
            case NCRoomState.Clearing:
                TickClearing();
                break;

            case NCRoomState.Warning:
            case NCRoomState.Fighting:
            case NCRoomState.Intermission:
                TickCombat();
                break;
        }
    }

    private void TickClearing()
    {
        NCBalance balance = NCBalance.Get;
        NCSession session = NCSession.Instance;

        HoldingMask = 0;

        if (session == null || session.IsMissionRunning == false)
        {
            ResetClearing(NCRoomBlock.NotStarted);
            return;
        }

        foreach (NCCombatRoom other in All)
        {
            if (other != this && other.IsBusy)
            {
                ResetClearing(NCRoomBlock.OtherRoom);
                return;
            }
        }

        int count = 0;
        int ready = 0;

        foreach (NCPlayer player in NCPlayer.All)
        {
            count++;

            bool near = NCMath.FlatDistance(player.transform.position, DummyPosition) <= balance.dummyRange;
            bool holding = player.HoldingInteract && near;

            if (holding)
            {
                HoldingMask |= 1 << player.Slot;
            }

            if (holding && player.Health.IsDown == false && ContainsPoint(player.transform.position))
            {
                ready++;
            }
        }

        // 두 명(혼자 테스트면 한 명)이 모두 조건을 채워야 한다. 하나라도 빠지면 즉시 0.
        if (count < balance.RequiredPlayers || ready < count)
        {
            ResetClearing(NCRoomBlock.None);
            return;
        }

        State = NCRoomState.Clearing;
        ClearProgress = Mathf.Min(ClearProgress + Runner.DeltaTime, balance.dummyHoldSeconds);

        if (ClearProgress < balance.dummyHoldSeconds)
        {
            Block = NCRoomBlock.None;
            return;
        }

        // 완료 직전: 봉쇄 영역에 사람이 있으면 시작을 보류한다.
        if (IsAnyPlayerInSealZone())
        {
            Block = NCRoomBlock.DoorOccupied;
            return;
        }

        Block = NCRoomBlock.None;
        HoldingMask = 0;
        Debug.Log($"[NCCombatRoom] {roomName}: 정리 완료 → 봉쇄·1차 예고");
        StartWave(0);
    }

    private void ResetClearing(NCRoomBlock block)
    {
        ClearProgress = 0f;
        Block = block;
        State = NCRoomState.Untouched;
    }

    private void TickCombat()
    {
        _alive.RemoveAll(ghost => ghost == null || ghost.Object == null || ghost.Object.IsValid == false);
        SpawnDuePending();

        if (State == NCRoomState.Intermission)
        {
            if (PhaseTimer.Expired(Runner))
            {
                StartWave(WaveIndex + 1);
            }

            return;
        }

        if (State == NCRoomState.Warning && PhaseTimer.Expired(Runner))
        {
            State = NCRoomState.Fighting;

            // 이번 판의 첫 전투가 시작됐다 → 이후로는 과충전(Q) 사용 가능.
            if (NCSession.Instance != null)
            {
                NCSession.Instance.CombatStarted = true;
            }
        }

        // 동시 상한까지 예약(예고)을 채운다.
        while (_alive.Count + _pending.Count < maxAlive && _basicLeft + _fastLeft > 0)
        {
            if (Reserve() == false)
            {
                break;
            }
        }

        Remaining = _basicLeft + _fastLeft + _pending.Count + _alive.Count;

        if (State == NCRoomState.Fighting && Remaining == 0)
        {
            if (WaveIndex + 1 < WaveCount)
            {
                State = NCRoomState.Intermission;
                PhaseTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.waveGapSeconds);
            }
            else
            {
                ClearRoom();
            }
        }
    }

    private void StartWave(int index)
    {
        WaveIndex = index;
        _basicLeft = waves[index].basic;
        _fastLeft = waves[index].fast;
        State = NCRoomState.Warning;
        PhaseTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.spawnWarningSeconds);

        // 첫 예고 묶음을 바로 건다(예고 문구와 출현 지점 표시가 같은 순간에 나오도록).
        while (_pending.Count < maxAlive && _basicLeft + _fastLeft > 0)
        {
            if (Reserve() == false)
            {
                break;
            }
        }

        Remaining = _basicLeft + _fastLeft + _pending.Count;
    }

    private void ClearRoom()
    {
        State = NCRoomState.Cleared;
        Remaining = 0;

        // 마지막 처치와 전멸이 겹치면 실패이므로, 전원 다운이면 부활시키지 않는다.
        bool anyoneAlive = false;
        foreach (NCPlayer player in NCPlayer.All)
        {
            anyoneAlive |= player.Health.IsDown == false;
        }

        // 마지막 방이면 보상 없이 결과로 간다(세션이 다음 Tick에 성공 확정).
        bool isFinalRoom = true;
        foreach (NCCombatRoom room in All)
        {
            if (room != this && room.State != NCRoomState.Cleared)
            {
                isFinalRoom = false;
            }
        }

        foreach (NCPlayer player in NCPlayer.All)
        {
            // 사용 중이던 과충전은 먼저 끝낸다(환불 없음).
            player.Gun.ServerEndOvercharge();

            if (anyoneAlive)
            {
                player.Health.ServerRevive();
            }

            player.Gun.ServerRefill();

            // 비최종 방 소탕: 각자 과충전 1회(보유 최대 1).
            if (isFinalRoom == false)
            {
                player.Gun.ServerGrantCharge();
            }
        }

        Debug.Log($"[NCCombatRoom] {roomName}: 소탕 완료");
    }

    // ───────── 출현 ─────────

    /// <summary>다음 유령의 종류·위치를 정하고 예고를 시작한다.</summary>
    private bool Reserve()
    {
        if (TryPickSpawnPosition(out Vector3 position) == false)
        {
            return false;
        }

        NCGhostType type = Random.Range(0, _basicLeft + _fastLeft) < _basicLeft ? NCGhostType.Basic : NCGhostType.Fast;

        if (type == NCGhostType.Basic)
        {
            _basicLeft--;
        }
        else
        {
            _fastLeft--;
        }

        float seconds = NCBalance.Get.spawnWarningSeconds;
        _pending.Add(new Pending
        {
            Position = position,
            Type = type,
            Timer = TickTimer.CreateFromSeconds(Runner, seconds),
        });

        RpcShowWarning(position, seconds);
        return true;
    }

    private void SpawnDuePending()
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].Timer.Expired(Runner) == false)
            {
                continue;
            }

            Pending pending = _pending[i];
            _pending.RemoveAt(i);

            // 씬 연결이 비어 있으면 Resources에서 불러온다.
            NetworkObject prefab = ghostPrefab != null ? ghostPrefab : Resources.Load<NetworkObject>("NC_Ghost");
            NetworkObject spawned = Runner.Spawn(prefab, pending.Position, Quaternion.identity, null,
                (runner, obj) => obj.GetComponent<NCGhost>().Type = pending.Type);

            if (spawned != null)
            {
                _alive.Add(spawned.GetComponent<NCGhost>());
            }
        }
    }

    /// <summary>플레이어 바로 옆·이미 예고 중인 자리는 피해서 출현 지점을 고른다.</summary>
    private bool TryPickSpawnPosition(out Vector3 position)
    {
        position = default;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return false;
        }

        List<Vector3> good = new List<Vector3>();
        List<Vector3> any = new List<Vector3>();

        foreach (Transform point in spawnPoints)
        {
            Vector2 jitter = Random.insideUnitCircle * 0.8f;
            Vector3 candidate = point.position + new Vector3(jitter.x, 0f, jitter.y);
            any.Add(candidate);

            if (IsNearPlayer(candidate, 2f) == false && IsNearPending(candidate, 0.9f) == false)
            {
                good.Add(candidate);
            }
        }

        List<Vector3> pool = good.Count > 0 ? good : any;
        Vector3 raw = pool[Random.Range(0, pool.Count)];

        position = NavMesh.SamplePosition(raw, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) ? hit.position : raw;
        return true;
    }

    private static bool IsNearPlayer(Vector3 position, float radius)
    {
        foreach (NCPlayer player in NCPlayer.All)
        {
            if (NCMath.FlatDistance(player.transform.position, position) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsNearPending(Vector3 position, float radius)
    {
        foreach (Pending pending in _pending)
        {
            if (NCMath.FlatDistance(pending.Position, position) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAnyPlayerInSealZone()
    {
        if (sealZones == null)
        {
            return false;
        }

        foreach (NCPlayer player in NCPlayer.All)
        {
            foreach (BoxCollider zone in sealZones)
            {
                Vector3 p = player.transform.position;
                if (zone != null && zone.bounds.Contains(new Vector3(p.x, zone.bounds.center.y, p.z)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>방 이름표: 미소탕(아이보리) / 전투 중(붉게) / 소탕 완료(초록).</summary>
    private void UpdateLabel()
    {
        if (label == null)
        {
            return;
        }

        int state = State == NCRoomState.Cleared ? 2 : (IsBusy ? 1 : 0);
        if (state == _shownLabelState)
        {
            return;
        }

        _shownLabelState = state;
        label.text = state == 2
            ? $"{roomName}\n<size=60%>소탕 완료</size>"
            : (state == 1 ? $"{roomName}\n<size=60%>전투 중</size>" : roomName);
        label.color = state == 2 ? NCStyle.Success : (state == 1 ? NCStyle.GhostTelegraph : NCStyle.Text);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcShowWarning(Vector3 position, float seconds)
    {
        NCFx.SpawnWarning(position, seconds);
    }

    // ───────── 월드 연출 (모든 화면) ─────────

    public override void Render()
    {
        // 봉쇄는 정리 완료부터 소탕 완료까지.
        bool sealed_ = IsBusy;
        if (barriers != null)
        {
            foreach (GameObject barrier in barriers)
            {
                if (barrier != null && barrier.activeSelf != sealed_)
                {
                    barrier.SetActive(sealed_);
                }
            }
        }

        // 더미는 정리 전에만 보인다.
        bool showDummy = State == NCRoomState.Untouched || State == NCRoomState.Clearing;
        if (dummy != null && dummy.gameObject.activeSelf != showDummy)
        {
            dummy.gameObject.SetActive(showDummy);

            if (showDummy == false)
            {
                NCFx.DustPuff(dummy.position + Vector3.up * 0.5f);
            }
        }

        UpdateLabel();

        // 정리 중에는 더미가 살짝 흔들린다.
        if (dummy != null && showDummy)
        {
            float shake = State == NCRoomState.Clearing ? Mathf.Sin(Time.time * 40f) * 2.5f : 0f;
            dummy.localRotation = Quaternion.Euler(0f, 20f + shake, 0f);
        }
    }
}
