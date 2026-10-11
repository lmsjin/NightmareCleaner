using Fusion;
using UnityEngine;

/// <summary>
/// 전투방의 "공동 정리" 가구. 기획서 5장 규칙을 최소 범위로 구현한다.
///
///  - 접속한 **모든 플레이어**가 가구 중심 2m 이내에서, 살아 있는 채로, **동시에 E를 누르고 있을 때만** 진행도가 오른다.
///  - 한 명이라도 E를 떼거나 범위를 벗어나면 진행도는 **즉시 0**으로 돌아간다.
///  - 2초 유지하면 정리 완료 → 전투 시작 신호(IsRestored). 완료 후에는 다시 시작되지 않는다.
///  - 가구는 들거나 밀지 않는다. "어질러짐"과 "정리됨" 두 자세 사이를 보간만 한다.
///
/// 판정은 Host만 하고, 결과는 [Networked] 값으로 모든 화면에 전달된다.
/// 씬에 직접 배치하는 NetworkObject다.
///
/// TODO(3~4주차): 방 안 판정·출입구 봉쇄·1.5초 출현 예고·웃음소리 연출.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkFurniture : NetworkBehaviour
{
    [Header("연출 (자식 오브젝트를 연결)")]
    [Tooltip("움직일 가구 모델(자식). 이 오브젝트만 두 자세 사이를 오간다")]
    [SerializeField] private Transform visual;
    [Tooltip("어질러진 자세 — 위치·회전만 참고하는 빈 오브젝트")]
    [SerializeField] private Transform messyPose;
    [Tooltip("정리된 자세 — 위치·회전만 참고하는 빈 오브젝트")]
    [SerializeField] private Transform restoredPose;
    [Tooltip("정리 연출 속도. 기획서: 복구와 첫 출현 예고가 1.5초 안에 겹쳐 보이도록")]
    [SerializeField] private float restoreLerpSpeed = 4.0f;
    [Tooltip("UI 기획서: 정리가 끝나면 잡동사니 더미가 사라진다. 끄면 두 자세 사이 보간 연출을 쓴다")]
    [SerializeField] private bool hideWhenRestored = true;

    [Header("테스트")]
    [Tooltip("시작에 필요한 인원. 기획서는 2인 고정. 혼자 테스트할 때만 1로 낮춘다")]
    [SerializeField] private int requiredPlayers = 2;

    [Header("설정 (Spawned에서 CleanerBalance 값으로 덮어씀)")]
    [SerializeField] private float interactRange = 2.0f;
    [SerializeField] private float holdSeconds = 2.0f;

    /// <summary>공동 정리 진행 시간(초). holdSeconds에 도달하면 완료.</summary>
    [Networked] public float Progress { get; set; }

    /// <summary>정리 완료 여부 = 전투 시작 신호. 스폰너가 이 값을 본다.</summary>
    [Networked] public NetworkBool IsRestored { get; set; }

    /// <summary>지금 E를 누르고 있는 인원(안내 문구용).</summary>
    [Networked] private int HoldingCount { get; set; }

    // 정리를 시작할 수 없는 이유(안내 문구용). 0=없음, 1=의뢰 시작 전, 2=다른 방 전투 중
    [Networked] private int BlockReason { get; set; }

    /// <summary>진행 비율(0~1). UI 진행 바에 쓴다.</summary>
    public float ProgressRatio => holdSeconds > 0f ? Mathf.Clamp01(Progress / holdSeconds) : 0f;

    public override void Spawned()
    {
        CleanerBalance balance = CleanerBalance.Resolve(null);

        if (balance != null)
        {
            interactRange = balance.furnitureInteractRange;
            holdSeconds = balance.furnitureHoldSeconds;
        }

        // 처음 보는 순간 자세를 바로 맞춘다(접속 직후 엉뚱한 자세에서 움직이지 않게).
        SnapVisual();
    }

    public override void FixedUpdateNetwork()
    {
        // 판정은 Host만 한다. 이미 정리된 가구는 다시 시작하지 않는다(기획서 13장 필수 예외).
        if (Object.HasStateAuthority == false || IsRestored)
        {
            return;
        }

        // UI 기획서: 의뢰 시작 전에는 정리 불가, 동시에 전투하는 방은 1개뿐.
        BlockReason = FindBlockReason();

        if (BlockReason != 0)
        {
            Progress = 0f;
            HoldingCount = 0;
            return;
        }

        int totalPlayers = 0;
        int readyPlayers = 0;
        int holding = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerObject) == false)
            {
                continue;
            }

            totalPlayers++;

            NetworkPlayerInteract interact = playerObject.GetComponent<NetworkPlayerInteract>();
            NetworkPlayerHealth health = playerObject.GetComponent<NetworkPlayerHealth>();

            bool isHolding = interact != null && interact.IsHoldingInteract;
            bool isAlive = health == null || health.IsDown == false;
            bool isNear = IsWithinRange(playerObject.transform.position);

            if (isHolding && isNear)
            {
                holding++;
            }

            if (isHolding && isAlive && isNear)
            {
                readyPlayers++;
            }
        }

        HoldingCount = holding;

        // 인원이 모자라거나, 한 명이라도 조건을 못 채우면 진행도는 즉시 0.
        bool everyoneReady = totalPlayers >= requiredPlayers && readyPlayers == totalPlayers;

        if (everyoneReady == false)
        {
            Progress = 0f;
            return;
        }

        Progress += Runner.DeltaTime;

        if (Progress >= holdSeconds)
        {
            Progress = holdSeconds;
            IsRestored = true; // 전투 시작은 이 순간 한 번만 일어난다.
            Debug.Log("[NetworkFurniture] 공동 정리 완료 → 전투 시작");
        }
    }

    /// <summary>
    /// 정리를 시작할 수 없는 이유를 찾는다. 게임 흐름·전투방이 씬에 없으면 제한하지 않는다(단독 테스트용).
    /// </summary>
    private int FindBlockReason()
    {
        NetworkGameFlow flow = FindFirstObjectByType<NetworkGameFlow>();
        if (flow != null && flow.Object != null && flow.Object.IsValid && flow.IsMissionRunning == false)
        {
            return 1;
        }

        foreach (NetworkCombatRoom room in FindObjectsByType<NetworkCombatRoom>(FindObjectsSortMode.None))
        {
            if (room.Furniture != this && room.Object != null && room.Object.IsValid && room.State == RoomState.Fighting)
            {
                return 2;
            }
        }

        return 0;
    }

    /// <summary>
    /// 재시작용. 가구를 어질러진 상태로 되돌린다. **Host에서만 호출.**
    /// </summary>
    public void ServerReset()
    {
        if (Object.HasStateAuthority == false)
        {
            return;
        }

        Progress = 0f;
        IsRestored = false;
        HoldingCount = 0;
        BlockReason = 0;
    }

    private bool IsWithinRange(Vector3 position)
    {
        Vector3 to = position - transform.position;
        to.y = 0f;
        return to.magnitude <= interactRange;
    }

    // ───────── 연출 (모든 화면) ─────────

    private void Update()
    {
        if (visual == null || Object == null || Object.IsValid == false)
        {
            return;
        }

        // 정리 완료 → 더미를 숨긴다. 재시작으로 초기화되면 다시 보인다.
        if (hideWhenRestored)
        {
            bool show = IsRestored == false;
            if (visual.gameObject.activeSelf != show)
            {
                visual.gameObject.SetActive(show);
            }
            return;
        }

        if (messyPose == null || restoredPose == null)
        {
            return;
        }

        Transform target = IsRestored ? restoredPose : messyPose;
        float t = restoreLerpSpeed * Time.deltaTime;

        visual.position = Vector3.Lerp(visual.position, target.position, t);
        visual.rotation = Quaternion.Slerp(visual.rotation, target.rotation, t);
    }

    private void SnapVisual()
    {
        if (visual == null || messyPose == null || restoredPose == null)
        {
            return;
        }

        Transform target = IsRestored ? restoredPose : messyPose;
        visual.SetPositionAndRotation(target.position, target.rotation);
    }

    /// <summary>
    /// 임시 안내 문구. 내 캐릭터가 가구 근처에 있을 때만 보인다.
    /// (정식 UI·1P/2P 입력 표시는 UI 담당이 교체)
    /// </summary>
    private void OnGUI()
    {
        if (Object == null || Object.IsValid == false || Runner == null || IsRestored)
        {
            return;
        }

        if (Runner.TryGetPlayerObject(Runner.LocalPlayer, out NetworkObject me) == false)
        {
            return;
        }

        if (IsWithinRange(me.transform.position) == false)
        {
            return;
        }

        string message;

        // UI 기획서 07장 CTX-01~03 문구
        if (BlockReason == 1)
        {
            message = "동료와 중앙홀로 이동해 의뢰를 시작하세요";
        }
        else if (BlockReason == 2)
        {
            message = "다른 방의 전투가 끝나야 정리할 수 있습니다";
        }
        else if (Progress > 0f)
        {
            message = $"정리 중... {Mathf.RoundToInt(ProgressRatio * 100f)}%";
        }
        else if (HoldingCount > 0)
        {
            message = "동료와 함께 E를 누르세요";
        }
        else
        {
            message = "E 길게 눌러 함께 치우기";
        }

        GUI.Label(new Rect(Screen.width * 0.5f - 120f, Screen.height * 0.7f, 240f, 30f), message);
    }
}
