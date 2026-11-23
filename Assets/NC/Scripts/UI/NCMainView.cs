using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 아트 프리팹(Resources/UI/Scenes/UI_Main, Variant) 루트에 붙인다.
/// 버튼 "참조"만 들고 있고 로직은 없다 — 동작은 NCMainScreen이 맡는다.
/// 컴포넌트를 처음 붙이면 Reset()이 아트 프리팹 이름으로 칸을 자동으로 채운다.
/// </summary>
public class NCMainView : MonoBehaviour
{
    [Tooltip("MAIN-01 게임 시작 (start)")]
    [SerializeField] private Button _startButton;
    [Tooltip("MAIN-02 설정 (setting) — 설정 화면이 생기면 NCMainScreen.OnSetting에서 연결")]
    [SerializeField] private Button _settingButton;
    [Tooltip("MAIN-03 종료 (quit)")]
    [SerializeField] private Button _quitButton;

    public Button StartButton => _startButton;
    public Button SettingButton => _settingButton;
    public Button QuitButton => _quitButton;

    // 에디터에서 컴포넌트를 붙이거나 ⋮ > Reset 을 누르면 실행된다(빌드에서는 호출되지 않음).
    private void Reset()
    {
        _startButton = Find("start");
        _settingButton = Find("setting");
        _quitButton = Find("quit");
    }

    private Button Find(string path)
    {
        Transform found = transform.Find(path);
        if (found == null)
        {
            Debug.LogWarning($"[UI] NCMainView: '{path}' 를 찾지 못함 — Inspector에서 직접 연결하세요");
            return null;
        }
        return found.GetComponent<Button>();
    }
}
