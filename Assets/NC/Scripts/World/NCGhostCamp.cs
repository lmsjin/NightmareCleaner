using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 중립 유령 캠프(연회장). 등장할 때마다 기본·빠른·탱커 중 한 종류를 랜덤으로 고른다.
/// 마지막 유령을 처치한 생존자에게 과충전을 주고 40초 뒤 다시 등장한다.
/// 부분 처치는 유지된다(죽은 개체는 전멸 전까지 다시 나오지 않음).
/// 씬 배치 NetworkObject — 영역·출현 지점은 씬에서 옮길 수 있다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NCGhostCamp : NetworkBehaviour
{
    public static readonly List<NCGhostCamp> All = new List<NCGhostCamp>();

    [SerializeField] private NetworkObject ghostPrefab;
    [Tooltip("활동 영역(트리거 박스). 벗어나면 복귀한다")]
    [SerializeField] private BoxCollider area;
    [SerializeField] private Transform[] spawnPoints;

    [Networked] public NCGhostType Type { get; set; }
    [Networked] public int AliveCount { get; set; }
    [Networked] private TickTimer RespawnTimer { get; set; }

    /// <summary>보상 지급 횟수와 받은 슬롯(HUD 알림용).</summary>
    [Networked] public int RewardCount { get; set; }
    [Networked] public int RewardSlot { get; set; }

    public bool IsActive => AliveCount > 0;
    public Bounds AreaBounds => area != null ? area.bounds : new Bounds(transform.position, Vector3.one * 4f);

    private bool _wiped;
    private int _lastHitSlot = -1;

    private void Awake()
    {
        All.Add(this);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    /// <summary>활동 영역(margin만큼 넓힌) 안인지.</summary>
    public bool Contains(Vector3 position, float margin = 0f)
    {
        Bounds b = AreaBounds;
        b.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
        return b.Contains(new Vector3(position.x, b.center.y, position.z));
    }

    // ───────── Host 전용 ─────────

    public void ServerReset()
    {
        AliveCount = 0;
        RespawnTimer = TickTimer.None;
        _wiped = false;
        _lastHitSlot = -1;
    }

    /// <summary>새 세대를 생성한다.</summary>
    public void ServerStart()
    {
        ServerReset();
        NCGhostType type = (NCGhostType)Random.Range(0, 3);
        Type = type;
        int count = NCBalance.Get.GetGhost(type).count;
        NetworkObject prefab = ghostPrefab != null ? ghostPrefab : Resources.Load<NetworkObject>("NC_Ghost");

        for (int i = 0; i < count; i++)
        {
            Vector3 home = PickPosition(i);
            NetworkObject spawned = Runner.Spawn(prefab, home, Quaternion.identity, null, (runner, obj) =>
            {
                NCGhost ghost = obj.GetComponent<NCGhost>();
                ghost.Type = type;
                ghost.ServerInit(this, home);
            });

            if (spawned != null)
            {
                AliveCount++;
            }
        }

        Debug.Log($"[NCGhostCamp] {type} {AliveCount}마리 등장");
    }

    public void ServerNotifyDeath(int attackerSlot)
    {
        AliveCount = Mathf.Max(0, AliveCount - 1);
        _lastHitSlot = attackerSlot;
    }

    public void ServerTick()
    {
        if (AliveCount > 0)
        {
            return;
        }

        if (_wiped == false)
        {
            _wiped = true;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, NCBalance.Get.campRespawnSeconds);

            NCPlayer killer = NCPlayer.FindBySlot(_lastHitSlot);
            if (killer != null && killer.Health.IsDown == false)
            {
                killer.Health.ServerEndProtection();
                killer.Gun.ServerGrantOvercharge();
                RewardSlot = killer.Slot;
                RewardCount++;
                NCSession.Instance.ServerCountLastHit(killer.Slot);
            }

            return;
        }

        if (RespawnTimer.Expired(Runner))
        {
            ServerStart();
        }
    }

    private Vector3 PickPosition(int index)
    {
        Vector3 origin = spawnPoints != null && spawnPoints.Length > 0
            ? spawnPoints[index % spawnPoints.Length].position
            : AreaBounds.center;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 jitter = Random.insideUnitCircle * 1.5f;
            Vector3 raw = new Vector3(origin.x + jitter.x, 0.1f, origin.z + jitter.y);

            if (NavMesh.SamplePosition(raw, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) && Contains(hit.position))
            {
                return hit.position;
            }
        }

        return origin;
    }
}
