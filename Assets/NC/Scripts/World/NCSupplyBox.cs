using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;

/// <summary>보급함 상태. 잠금 → 사용 가능 → 사용 완료로만 바뀐다.</summary>
public enum NCSupplyState
{
    Locked,
    Available,
    Used,
}

/// <summary>
/// 보급방의 보급함(UI 기획서 13·16장). 씬에 배치하는 NetworkObject다.
///  - 연결된 전투방 중 하나라도 소탕하면 해금(OR, 한 번만). 다시 소탕해도 재충전 없음.
///  - 생존자가 1.5m 안에서 E를 한 번 누르면 살아 있는 두 명 모두 체력 +30(최대 100). 한 판에 한 번.
///  - 둘 다 체력이 가득이면 사용권을 소비하지 않고 "체력이 충분합니다".
///  - 전투·출현 예고·공동 정리 중에는 사용 불가. 다운된 동료를 살리는 기능이 아니다.
/// 판정은 Host만, 두 명이 같은 Tick에 눌러도 상태가 바로 바뀌므로 한 번만 처리된다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NCSupplyBox : NetworkBehaviour
{
    public static readonly List<NCSupplyBox> All = new List<NCSupplyBox>();

    [SerializeField] private string boxName = "집무실";
    [Tooltip("이 중 하나라도 소탕하면 해금된다")]
    [SerializeField] private NCCombatRoom[] unlockRooms;

    [Header("연출(빌더가 연결)")]
    [SerializeField] private TextMeshPro label;
    [Tooltip("뚜껑의 회전축(뒤쪽 모서리)")]
    [SerializeField] private Transform lid;
    [SerializeField] private GameObject glow;

    [Networked] public NCSupplyState State { get; set; }

    /// <summary>보급이 사용된 횟수(알림용 — 0 또는 1).</summary>
    [Networked] public int UseCount { get; set; }

    public string BoxName => boxName;

    /// <summary>"서쪽 갤러리 또는 서쪽 응접실" 같은 해금 조건 문구.</summary>
    public string UnlockText
    {
        get
        {
            List<string> names = new List<string>();
            if (unlockRooms != null)
            {
                foreach (NCCombatRoom room in unlockRooms)
                {
                    if (room != null)
                    {
                        names.Add(room.RoomName);
                    }
                }
            }

            return string.Join(" 또는 ", names);
        }
    }

    // Host 전용: 플레이어별 직전 E 상태(새로 누른 순간만 요청으로 본다)
    private readonly Dictionary<PlayerRef, bool> _wasHolding = new Dictionary<PlayerRef, bool>();

    // 연출용
    private int _shownState = -1;
    private float _lidOpen;

    private void Awake()
    {
        All.Add(this);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    /// <summary>지금 보급함을 쓸 수 없는 상황(전투·예고·정리 중, 의뢰 시작 전)인지.</summary>
    public static bool IsBlockedByCombat()
    {
        NCSession session = NCSession.Instance;
        if (session == null || session.IsMissionRunning == false)
        {
            return true;
        }

        foreach (NCCombatRoom room in NCCombatRoom.All)
        {
            if (room.IsBusy || room.State == NCRoomState.Clearing)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsInRange(Vector3 position) => NCMath.FlatDistance(position, transform.position) <= NCBalance.Get.supplyRange;

    public void ServerReset()
    {
        if (Object.HasStateAuthority)
        {
            State = NCSupplyState.Locked;
            UseCount = 0;
            _wasHolding.Clear();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        // ① 해금: 연결된 방 중 하나라도 소탕(한 번만)
        if (State == NCSupplyState.Locked && IsAnyUnlockRoomCleared())
        {
            State = NCSupplyState.Available;
        }

        // ② 사용 요청: 범위 안 생존자가 E를 "새로 누른" Tick
        foreach (NCPlayer player in NCPlayer.All)
        {
            PlayerRef who = player.Object.InputAuthority;
            bool holding = player.HoldingInteract;
            bool pressed = holding && (_wasHolding.TryGetValue(who, out bool was) == false || was == false);
            _wasHolding[who] = holding;

            if (pressed == false || player.Health.IsDown || IsInRange(player.transform.position) == false)
            {
                continue;
            }

            TryUse(who);
        }
    }

    private void TryUse(PlayerRef requester)
    {
        if (State != NCSupplyState.Available || IsBlockedByCombat())
        {
            return;
        }

        bool anyoneHurt = false;
        foreach (NCPlayer player in NCPlayer.All)
        {
            if (player.Health.IsDown == false && player.Health.Hp < player.Health.MaxHp)
            {
                anyoneHurt = true;
            }
        }

        if (anyoneHurt == false)
        {
            RpcFullHealth(requester); // 사용권은 소비하지 않는다
            return;
        }

        int heal = NCBalance.Get.supplyHeal;
        foreach (NCPlayer player in NCPlayer.All)
        {
            // 동료가 다른 방에 있어도 두 명 모두 회복(다운자는 제외)
            player.Health.ServerHeal(heal);
        }

        State = NCSupplyState.Used;
        UseCount++;
        Debug.Log($"[NCSupplyBox] {boxName} 보급 사용");
    }

    private bool IsAnyUnlockRoomCleared()
    {
        if (unlockRooms == null)
        {
            return false;
        }

        foreach (NCCombatRoom room in unlockRooms)
        {
            if (room != null && room.State == NCRoomState.Cleared)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>둘 다 체력이 가득일 때 요청한 사람에게만 안내.</summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcFullHealth(PlayerRef requester)
    {
        if (Runner.LocalPlayer == requester)
        {
            NCHud.Notify("체력이 충분합니다", NCStyle.TextDim);
        }
    }

    // ───────── 연출 (모든 화면) ─────────

    public override void Render()
    {
        int state = (int)State;

        if (state != _shownState)
        {
            _shownState = state;

            if (label != null)
            {
                switch (State)
                {
                    case NCSupplyState.Locked:
                        label.text = $"{boxName}\n<size=55%>잠김 · {UnlockText} 소탕 후 사용</size>";
                        label.color = NCStyle.TextDim;
                        break;
                    case NCSupplyState.Available:
                        label.text = $"{boxName}\n<size=55%>보급 사용 가능</size>";
                        label.color = NCStyle.P2;
                        break;
                    default:
                        label.text = $"{boxName}\n<size=55%>사용 완료</size>";
                        label.color = NCStyle.Success;
                        break;
                }
            }

            if (glow != null)
            {
                glow.SetActive(State == NCSupplyState.Available);
            }
        }

        // 사용 완료면 뚜껑이 열린다.
        if (lid != null)
        {
            float target = State == NCSupplyState.Used ? 1f : 0f;
            _lidOpen = Mathf.MoveTowards(_lidOpen, target, Time.deltaTime * 3f);
            lid.localRotation = Quaternion.Euler(-70f * _lidOpen, 0f, 0f);
        }
    }
}
