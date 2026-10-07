using Fusion;
using UnityEngine;

/// <summary>
/// 다운된 동료 구조. 기획서 7장 규칙:
///
///  - 살아 있는 동료가 다운자 1.5m 이내에서 **E를 3초 유지**하면 다운자가 체력 40으로 일어난다(2초 보호).
///  - 구조 중 구조자가 **이동·사격·피격·E 해제** 중 하나라도 하면 즉시 취소, 진행도 0.
///
/// 판정 주체는 **다운된 쪽**이다. 다운자 오브젝트(Host)가 주변의 구조자를 찾아 진행도를 올린다.
/// 구조자의 입력은 NetworkPlayerInteract가 [Networked] 값으로 공개해 둔 것을 읽는다.
///
/// 플레이어 프리팹 루트에 붙인다(연결할 필드 없음).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerRevive : NetworkBehaviour
{
    [Header("설정 (Spawned에서 CleanerBalance 값으로 덮어씀)")]
    [SerializeField] private float reviveRange = 1.5f;
    [SerializeField] private float reviveHoldSeconds = 3.0f;

    /// <summary>구조 진행 시간(초). reviveHoldSeconds에 도달하면 기상.</summary>
    [Networked] public float ReviveProgress { get; set; }

    /// <summary>지금 나를 구조 중인 플레이어. 없으면 PlayerRef.None.</summary>
    [Networked] public PlayerRef Rescuer { get; set; }

    // 구조를 시작할 때 구조자의 피격 횟수. 이 값이 바뀌면 "구조 중 맞았다" → 취소.
    [Networked] private int RescuerHitCount { get; set; }

    /// <summary>진행 비율(0~1). UI 진행 바에 쓴다.</summary>
    public float ProgressRatio => reviveHoldSeconds > 0f ? Mathf.Clamp01(ReviveProgress / reviveHoldSeconds) : 0f;

    private NetworkPlayerHealth _health;

    public override void Spawned()
    {
        _health = GetComponent<NetworkPlayerHealth>();

        CleanerBalance balance = CleanerBalance.Resolve(null);

        if (balance != null)
        {
            reviveRange = balance.reviveRange;
            reviveHoldSeconds = balance.reviveHoldSeconds;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 판정은 Host만 한다.
        if (Object.HasStateAuthority == false || _health == null)
        {
            return;
        }

        // 다운 상태가 아니면 구조할 것도 없다.
        if (_health.IsDown == false)
        {
            ResetProgress();
            return;
        }

        NetworkObject rescuerObject = FindRescuer();

        if (rescuerObject == null)
        {
            // 조건을 만족하는 구조자가 없다 = 이동·사격·E 해제·범위 이탈 → 즉시 초기화.
            ResetProgress();
            return;
        }

        NetworkPlayerHealth rescuerHealth = rescuerObject.GetComponent<NetworkPlayerHealth>();
        int hitCount = rescuerHealth != null ? rescuerHealth.HitCount : 0;

        // 새로 구조를 시작했거나, 구조 중 구조자가 맞았으면 처음부터 다시.
        if (Rescuer != rescuerObject.InputAuthority || hitCount != RescuerHitCount)
        {
            Rescuer = rescuerObject.InputAuthority;
            RescuerHitCount = hitCount;
            ReviveProgress = 0f;
            return;
        }

        ReviveProgress += Runner.DeltaTime;

        if (ReviveProgress >= reviveHoldSeconds)
        {
            // 체력 40 + 2초 보호는 NetworkPlayerHealth.Revive()가 처리한다.
            _health.Revive();
            ResetProgress();
            Debug.Log("[NetworkPlayerRevive] 동료 구조 완료");
        }
    }

    /// <summary>
    /// 지금 나를 구조할 수 있는 동료를 찾는다.
    /// 이미 구조 중인 동료가 여전히 조건을 만족하면 그 동료를 우선한다.
    /// </summary>
    private NetworkObject FindRescuer()
    {
        NetworkObject first = null;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (Runner.TryGetPlayerObject(player, out NetworkObject playerObject) == false)
            {
                continue;
            }

            // 자기 자신은 스스로를 구조할 수 없다.
            if (playerObject == Object)
            {
                continue;
            }

            if (CanRescue(playerObject) == false)
            {
                continue;
            }

            if (player == Rescuer)
            {
                return playerObject;
            }

            if (first == null)
            {
                first = playerObject;
            }
        }

        return first;
    }

    /// <summary>
    /// 구조 조건: 살아 있음 + 범위 안 + E 유지 + 이동 안 함 + 사격 안 함.
    /// </summary>
    private bool CanRescue(NetworkObject playerObject)
    {
        NetworkPlayerHealth health = playerObject.GetComponent<NetworkPlayerHealth>();
        NetworkPlayerInteract interact = playerObject.GetComponent<NetworkPlayerInteract>();

        if (interact == null || (health != null && health.IsDown))
        {
            return false;
        }

        return interact.IsHoldingInteract
            && interact.IsMoving == false
            && interact.IsFiring == false
            && IsWithinRange(playerObject.transform.position);
    }

    private bool IsWithinRange(Vector3 position)
    {
        Vector3 to = position - transform.position;
        to.y = 0f;
        return to.magnitude <= reviveRange;
    }

    private void ResetProgress()
    {
        ReviveProgress = 0f;
        Rescuer = PlayerRef.None;
    }

    /// <summary>
    /// 임시 안내 문구(정식 UI는 UI 담당이 교체).
    ///  - 다운자 본인 화면: 구조 진행도
    ///  - 근처의 동료 화면: "[E] 동료 구조" 또는 진행도
    /// </summary>
    private void OnGUI()
    {
        if (Object == null || Object.IsValid == false || Runner == null || _health == null || _health.IsDown == false)
        {
            return;
        }

        Rect rect = new Rect(Screen.width * 0.5f - 120f, Screen.height * 0.65f, 240f, 30f);
        int percent = Mathf.RoundToInt(ProgressRatio * 100f);

        // 내가 다운된 사람이다.
        if (Object.HasInputAuthority)
        {
            if (ReviveProgress > 0f)
            {
                GUI.Label(rect, $"구조 받는 중... {percent}%");
            }

            return;
        }

        // 내가 근처의 동료다.
        if (Runner.TryGetPlayerObject(Runner.LocalPlayer, out NetworkObject me) == false)
        {
            return;
        }

        if (IsWithinRange(me.transform.position) == false)
        {
            return;
        }

        string message = ReviveProgress > 0f && Rescuer == Runner.LocalPlayer
            ? $"동료 구조 중... {percent}%"
            : "[E] 동료 구조 (움직이지 마세요)";

        GUI.Label(rect, message);
    }
}
