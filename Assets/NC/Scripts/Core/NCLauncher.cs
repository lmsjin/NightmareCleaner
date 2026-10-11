using System.Text.RegularExpressions;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>접속 화면의 상태.</summary>
public enum NCConnectState
{
    Idle,       // 방 만들기 / 참가 대기
    Connecting, // 요청 중 — 버튼 잠금
    Connected,  // 세션 안
}

/// <summary>
/// 접속(방 만들기·방 참가), 플레이어 생성, 입력 수집을 담당한다. 씬의 "NetworkManager" 오브젝트에 붙인다.
///
/// 교안(2~3주차) 방식 그대로 NetworkRunner를 런타임에 추가한다(Host Mode).
///  - 방 만들기: 호스트가 6자리 초대 코드(NC + 숫자 4자리)를 세션 이름으로 방을 연다.
///  - 방 참가: 같은 코드로 접속한다. 없는 방이면 실패한다.
/// </summary>
public class NCLauncher : MonoBehaviour
{
    public static NCLauncher Instance { get; private set; }

    [Header("빌더가 연결")]
    [SerializeField] private NetworkObject playerPrefab;
    [Tooltip("저택 입구의 1P·2P 시작 위치")]
    [SerializeField] private Transform[] entrySpawnPoints;

    public NCConnectState State { get; private set; } = NCConnectState.Idle;
    public string StatusMessage { get; private set; } = "";
    public bool StatusIsError { get; private set; }
    public string RoomCode { get; private set; } = "";
    public NetworkRunner Runner { get; private set; }

    // 늦게 도착한 응답이 취소된 요청을 되살리지 않게 요청마다 번호를 붙인다.
    private int _requestId;

    // 마우스 광선이 평면과 만나지 않을 때를 대비한 마지막 조준점
    private Vector3 _lastAim;

    private static readonly Regex CodePattern = new Regex("^NC[0-9]{4}$");

    private void Awake()
    {
        Instance = this;

        // 씬을 다시 불렀을 때 이전 세션의 목록이 남지 않게 비운다.
        NCPlayer.All.Clear();
        NCGhost.All.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ───────── 접속 ─────────

    /// <summary>참가 코드 형식 검사(NC + 숫자 4자리).</summary>
    public static bool IsValidCode(string code) => code != null && CodePattern.IsMatch(code.Trim().ToUpperInvariant());

    public void CreateRoom()
    {
        string code = "NC" + Random.Range(0, 10000).ToString("0000");
        StartSession(GameMode.Host, code);
    }

    public void JoinRoom(string code)
    {
        if (IsValidCode(code) == false)
        {
            SetStatus("코드 형식을 확인해 주세요. (예: NC4827)", true);
            return;
        }

        StartSession(GameMode.Client, code.Trim().ToUpperInvariant());
    }

    /// <summary>연결 중 요청을 취소하거나 세션에서 나간다.</summary>
    public void Leave()
    {
        _requestId++;

        if (Runner != null)
        {
            // 이 오브젝트(NetworkManager)는 남겨 둔다. 정리는 OnShutdown에서 한다.
            Runner.Shutdown(destroyGameObject: false);
        }
        else
        {
            State = NCConnectState.Idle;
            SetStatus("", false);
        }
    }

    private async void StartSession(GameMode mode, string code)
    {
        if (State != NCConnectState.Idle)
        {
            return;
        }

        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        if (buildIndex < 0)
        {
            SetStatus("NC_Game 씬이 Build Profiles 목록에 없습니다.", true);
            return;
        }

        int request = ++_requestId;
        State = NCConnectState.Connecting;
        RoomCode = code;
        SetStatus(mode == GameMode.Host ? "방을 만드는 중..." : "방에 참가하는 중...", false);

        Runner = gameObject.AddComponent<NetworkRunner>();
        Runner.ProvideInput = true;

        NetworkSceneManagerDefault sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        NetworkEvents events = gameObject.AddComponent<NetworkEvents>();

        // 코드로 붙인 컴포넌트는 Inspector 직렬화를 거치지 않아 이벤트 필드가 비어(null) 있다 → 직접 만든다.
        events.OnInput ??= new NetworkEvents.InputEvent();
        events.PlayerJoined ??= new NetworkEvents.PlayerEvent();
        events.PlayerLeft ??= new NetworkEvents.PlayerEvent();
        events.OnShutdown ??= new NetworkEvents.ShutdownEvent();

        events.OnInput.AddListener(OnInput);
        events.PlayerJoined.AddListener(OnPlayerJoined);
        events.PlayerLeft.AddListener(OnPlayerLeft);
        events.OnShutdown.AddListener(OnShutdown);

        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single);

        StartGameResult result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = code,
            PlayerCount = 2,
            Scene = sceneInfo,
            SceneManager = sceneManager,
        });

        // 기다리는 사이 취소됐으면 결과를 버린다.
        if (request != _requestId)
        {
            return;
        }

        if (result.Ok)
        {
            State = NCConnectState.Connected;
            SetStatus("", false);
            return;
        }

        Debug.Log($"[NCLauncher] 접속 실패: {result.ShutdownReason} {result.ErrorMessage}");

        string message = mode == GameMode.Host
            ? "접속하지 못했습니다. 다시 시도해 주세요."
            : (result.ShutdownReason == ShutdownReason.GameIsFull
                ? "방이 가득 찼습니다."
                : "방에 참가할 수 없습니다. 코드를 확인해 주세요.");

        ResetToIdle(message);
    }

    private void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        // 세션 안에서 끊긴 경우: 씬을 다시 불러 처음 상태로 돌린다(접속 화면).
        bool wasConnected = State == NCConnectState.Connected;
        Debug.Log($"[NCLauncher] 세션 종료: {reason}");

        if (wasConnected)
        {
            NCLauncherNotice.Pending = reason == ShutdownReason.Ok
                ? ""
                : "연결이 종료되어 의뢰를 중단합니다.";
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // 연결 중 취소·실패: 접속 화면으로 돌아간다(실패 문구는 StartSession이 덧붙인다).
        ResetToIdle("");
    }

    private void ResetToIdle(string message)
    {
        if (Runner != null)
        {
            Destroy(Runner);
        }

        foreach (Component component in GetComponents<NetworkSceneManagerDefault>())
        {
            Destroy(component);
        }

        foreach (Component component in GetComponents<NetworkEvents>())
        {
            Destroy(component);
        }

        Runner = null;
        State = NCConnectState.Idle;
        SetStatus(message, string.IsNullOrEmpty(message) == false);
    }

    private void Start()
    {
        // 이전 세션이 끊겨 씬을 다시 불렀다면 그 이유를 보여준다.
        if (string.IsNullOrEmpty(NCLauncherNotice.Pending) == false)
        {
            SetStatus(NCLauncherNotice.Pending, true);
            NCLauncherNotice.Pending = null;
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }

    // ───────── 플레이어 생성 (Host만) ─────────

    private void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer == false)
        {
            return;
        }

        int slot = FindFreeSlot();
        if (slot < 0)
        {
            runner.Disconnect(player);
            return;
        }

        Transform point = entrySpawnPoints != null && slot < entrySpawnPoints.Length ? entrySpawnPoints[slot] : null;
        Vector3 position = point != null ? point.position : Vector3.up;
        Quaternion rotation = point != null ? point.rotation : Quaternion.identity;

        NetworkObject spawned = runner.Spawn(playerPrefab, position, rotation, player,
            (r, obj) => obj.GetComponent<NCPlayer>().Slot = slot);

        runner.SetPlayerObject(player, spawned);
    }

    private void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer == false)
        {
            return;
        }

        if (runner.TryGetPlayerObject(player, out NetworkObject playerObject))
        {
            runner.Despawn(playerObject);
        }
    }

    private static int FindFreeSlot()
    {
        for (int slot = 0; slot < 2; slot++)
        {
            if (NCPlayer.FindBySlot(slot) == null)
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>재도전 때 플레이어를 되돌릴 입구 위치.</summary>
    public Transform GetEntrySpawn(int slot)
    {
        return entrySpawnPoints != null && slot >= 0 && slot < entrySpawnPoints.Length ? entrySpawnPoints[slot] : null;
    }

    // ───────── 입력 수집 ─────────

    private void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NCInput data = new NCInput();

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null)
        {
            Vector2 move = Vector2.zero;
            if (keyboard.wKey.isPressed) move.y += 1f;
            if (keyboard.sKey.isPressed) move.y -= 1f;
            if (keyboard.aKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed) move.x += 1f;
            data.Move = Vector2.ClampMagnitude(move, 1f);

            data.Buttons.Set(NCButton.Reload, keyboard.rKey.isPressed);
            data.Buttons.Set(NCButton.Interact, keyboard.eKey.isPressed);
        }

        if (mouse != null)
        {
            data.Buttons.Set(NCButton.Fire, mouse.leftButton.isPressed);

            Camera cam = Camera.main;
            if (cam != null)
            {
                // 내 캐릭터 발 높이 + 1m 평면에 조준한다(총구 높이와 같게).
                float height = NCPlayer.Local != null ? NCPlayer.Local.transform.position.y + 1f : 1f;
                Plane plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

                if (plane.Raycast(ray, out float distance))
                {
                    _lastAim = ray.GetPoint(distance);
                }
            }
        }

        data.AimPoint = _lastAim;
        input.Set(data);
    }
}

/// <summary>씬을 다시 불러도 남겨 둘 안내 문구(연결 종료 이유).</summary>
public static class NCLauncherNotice
{
    public static string Pending;
}
