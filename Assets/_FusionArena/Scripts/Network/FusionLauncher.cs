using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// [교안 실습4] 네트워크 접속 담당. Host는 방을 만들고 서버 역할도 겸하며, Client는 같은 이름의 방에 접속.
// 2주차 범위: "접속" 자체 이해. 이동/공격 등 게임 기능은 이후 단계.
public class FusionLauncher : MonoBehaviour
{
    [Header("접속 UI")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button leaveButton;

    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private bool isBusy;

    private void Awake()
    {
        SetStatus("방 이름을 입력하고 Host 또는 Client를 선택하세요.");
        SetUiState(canConnect: true, canLeave: false);
    }

    // HostButton OnClick
    public void StartHost()
    {
        StartSession(GameMode.Host);
    }

    // ClientButton OnClick
    public void StartClient()
    {
        StartSession(GameMode.Client);
    }

    // Host/Client 공통 접속. Photon Cloud 접속은 비동기라 async/await 사용.
    private async void StartSession(GameMode mode)
    {
        if (isBusy || runner != null)
            return;

        string roomName = roomNameInput.text.Trim();
        if (string.IsNullOrWhiteSpace(roomName))
            roomName = "FusionArenaRoom";

        int buildIndex = SceneManager.GetActiveScene().buildIndex;
        if (buildIndex < 0)
        {
            SetStatus("현재 Scene을 Build Profiles의 Scene List에 추가하세요.");
            return;
        }

        isBusy = true;
        SetUiState(canConnect: false, canLeave: false);
        SetStatus($"{mode} 접속 중...");

        // NetworkRunner를 런타임으로 추가(Fusion 네트워크 실행의 중심).
        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true; // 3주차 입력 대비 미리 켜둠

        sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        SceneRef sceneRef = SceneRef.FromIndex(buildIndex);
        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = roomName,
            PlayerCount = 4,
            Scene = sceneInfo,
            SceneManager = sceneManager
        });

        isBusy = false;

        if (result.Ok)
        {
            SetStatus($"접속 성공 / Mode: {mode} / Room: {runner.SessionInfo.Name}");
            SetUiState(canConnect: false, canLeave: true);
        }
        else
        {
            SetStatus($"접속 실패: {result.ErrorMessage}");
            CleanupRunner();
            SetUiState(canConnect: true, canLeave: false);
        }
    }

    // LeaveButton OnClick — 현재 세션 정상 종료.
    public async void LeaveSession()
    {
        if (isBusy || runner == null)
            return;

        isBusy = true;
        SetStatus("접속 종료 중...");
        SetUiState(canConnect: false, canLeave: false);

        await runner.Shutdown(destroyGameObject: false);

        CleanupRunner();
        isBusy = false;

        SetStatus("접속이 종료되었습니다.");
        SetUiState(canConnect: true, canLeave: false);
    }

    // 종료/실패한 Runner는 재사용하지 않고 정리(다음 접속 때 새로 생성).
    private void CleanupRunner()
    {
        if (runner != null)
            Destroy(runner);

        if (sceneManager != null)
            Destroy(sceneManager);

        runner = null;
        sceneManager = null;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log($"[FusionLauncher] {message}");
    }

    private void SetUiState(bool canConnect, bool canLeave)
    {
        if (hostButton != null)
            hostButton.interactable = canConnect;

        if (clientButton != null)
            clientButton.interactable = canConnect;

        if (roomNameInput != null)
            roomNameInput.interactable = canConnect;

        if (leaveButton != null)
            leaveButton.interactable = canLeave;
    }
}
