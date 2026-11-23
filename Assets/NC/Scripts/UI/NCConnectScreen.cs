using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 접속 화면(UI 기획서 04장). 방을 여는 사람과 참가하는 사람의 행동을 나란히 둔다.
///  - 방 만들기: 한 번 요청, 연결 중 버튼 잠금
///  - 코드 + 참가하기: 빈 값·형식 오류면 비활성
///  - 연결 상태 영역을 미리 확보해 글자가 생겨도 버튼이 움직이지 않는다
///  - 뒤로(NET-04): 진행 요청 취소 후 메인
/// UI 모양은 아트 프리팹(Resources/UI/Scenes/UI_NET)에서 불러온다.
/// </summary>
public class NCConnectScreen
{
    private readonly GameObject _root;
    private readonly Button _createButton;
    private readonly Button _joinButton;
    private readonly TMP_InputField _codeInput;
    private readonly System.Action _onBack; // 뒤로를 눌렀을 때 NCUi에 알려 줄 함수

    public NCConnectScreen(RectTransform canvas, System.Action onBack = null)
    {
        _onBack = onBack;

        // 아트 파트 프리팹을 불러와 canvas 아래에 생성한다.
        GameObject prefab = Resources.Load<GameObject>("UI/Scenes/UI_NET");
        if (prefab == null)
        {
            Debug.LogError("[UI] UI_NET 프리팹을 찾을 수 없음 — 경로 확인");
            return;
        }

        NCNetView view = Object.Instantiate(prefab, canvas, false).GetComponent<NCNetView>();
        if (view == null)
        {
            Debug.LogError("[UI] UI_NET 루트에 NCNetView가 없음 — Variant인지, 루트에 붙었는지 확인");
            return;
        }

        _root = view.gameObject;

        // 프리팹 루트를 화면 전체에 맞춘다 (Scale 0·크기 0 방지)
        RectTransform rect = (RectTransform)view.transform;
        NCUiKit.Stretch(rect);
        rect.localScale = Vector3.one;
        rect.anchoredPosition3D = Vector3.zero;

        // 프리팹 안의 요소를 기존 변수에 넣는다 → Tick 코드는 그대로 동작
        _createButton = view.CreateButton;
        _joinButton = view.JoinButton;
        _codeInput = view.CodeInput;

        // 비어 있는 칸이 있으면 Console에 이름을 알려 준다
        if (_createButton == null) Debug.LogError("[UI] NCNetView: Create Button 칸이 비어 있음");
        if (_joinButton == null) Debug.LogError("[UI] NCNetView: Join Button 칸이 비어 있음");
        if (_codeInput == null) Debug.LogError("[UI] NCNetView: Code Input 칸이 비어 있음");
        if (view.BackButton == null) Debug.LogError("[UI] NCNetView: Back Button 칸이 비어 있음");

        // 버튼을 눌렀을 때 실행할 함수 연결
        _createButton.onClick.AddListener(OnCreate);
        _joinButton.onClick.AddListener(OnJoin);
        view.BackButton.onClick.AddListener(OnBack);
    }

    public void SetVisible(bool visible)
    {
        if (_root == null) return; // 프리팹을 못 불러왔으면 아무것도 안 함

        if (_root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    public void Tick(NCLauncher launcher)
    {
        if (_root == null) return;

        bool idle = launcher != null && launcher.State == NCConnectState.Idle;

        _createButton.interactable = idle;
        _codeInput.interactable = idle;
        _joinButton.interactable = idle && NCLauncher.IsValidCode(_codeInput.text);
    }

    private void OnCreate()
    {
        NCLauncher.Instance?.CreateRoom();
    }

    private void OnJoin()
    {
        NCLauncher.Instance?.JoinRoom(_codeInput.text);
    }

    // NET-04 뒤로: 진행 중인 연결 요청을 취소하고 메인으로
    private void OnBack()
    {
        NCLauncher launcher = NCLauncher.Instance;

        // 연결 중일 때만 연결을 취소한다 (예전 [취소] 버튼과 같은 조건)
        if (launcher != null && launcher.State == NCConnectState.Connecting)
        {
            launcher.Leave();
        }

        _onBack?.Invoke(); // 메인 화면으로 (실제 전환은 NCUi가 한다)
    }
}