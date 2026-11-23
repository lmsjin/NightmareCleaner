using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 화면(UI 기획서 03장, SCR-01) 버튼 동작. NCMainView와 같은 오브젝트(UI_Main 루트)에 붙인다.
///  - MAIN-01 게임 시작 → 게임 씬(NC_Game) 로드. 두 번 눌러도 한 번만 로드한다.
///  - MAIN-02 설정     → 설정 화면이 아직 없어 눌러도 아무 일도 없다(OnSetting에 나중에 연결).
///  - MAIN-03 종료     → 프로그램 종료. (기획서의 "종료 확인 팝업"은 팝업 UI가 생기면 추가)
/// 버튼은 화면 전환만 요청하고 게임 상태는 만들지 않는다(기획서 03장 프로그래밍 기준).
/// </summary>
[RequireComponent(typeof(NCMainView))]
public class NCMainScreen : MonoBehaviour
{
    [Tooltip("게임 시작 시 불러올 씬 이름 (Build Profiles에 등록되어 있어야 함)")]
    [SerializeField] private string _gameSceneName = "NC_Game";

    private NCMainView _view;
    private bool _loading; // 게임 시작을 이미 눌렀는지 (중복 로드 방지)

    private void Awake()
    {
        _view = GetComponent<NCMainView>();
    }

    private void OnEnable()
    {
        if (_view.StartButton != null) _view.StartButton.onClick.AddListener(OnStart);
        if (_view.QuitButton != null) _view.QuitButton.onClick.AddListener(OnQuit);
        if (_view.SettingButton != null) _view.SettingButton.onClick.AddListener(OnSetting);
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제 (규칙: += 하면 반드시 -=)
        if (_view.StartButton != null) _view.StartButton.onClick.RemoveListener(OnStart);
        if (_view.QuitButton != null) _view.QuitButton.onClick.RemoveListener(OnQuit);
        if (_view.SettingButton != null) _view.SettingButton.onClick.RemoveListener(OnSetting);
    }

    // MAIN-02 설정 — 설정 화면(SCR-07)이 생기면 여기서 연다. 지금은 아무 일도 하지 않는다.
    private void OnSetting()
    {
        // TODO: 설정 화면 열기 (음량·조작 안내, 닫으면 메인으로 복귀)
    }

    // MAIN-01 게임 시작
    private void OnStart()
    {
        if (_loading) return;

        if (Application.CanStreamedLevelBeLoaded(_gameSceneName) == false)
        {
            Debug.LogError($"[UI] '{_gameSceneName}' 씬이 Build Profiles에 없음 — 씬 목록에 추가하세요");
            return;
        }

        _loading = true;
        _view.StartButton.interactable = false; // 로딩 중 다시 누르지 못하게
        SceneManager.LoadScene(_gameSceneName);
    }

    // MAIN-03 종료
    private void OnQuit()
    {
        Debug.Log("[UI] 게임 종료");
#if UNITY_EDITOR
        // 에디터에서는 Application.Quit()이 동작하지 않으므로 Play 모드를 멈춘다
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
