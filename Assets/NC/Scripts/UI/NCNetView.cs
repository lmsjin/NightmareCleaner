using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Net 프리팹(접속 화면)에 붙인다.
// 화면 요소의 "참조"만 들고 있고, 게임 로직은 없다.
public class NCNetView : MonoBehaviour
{
    [SerializeField] private Button _createButton;      // NET-01 방 만들기
    [SerializeField] private Button _joinButton;        // NET-02 참가하기
    [SerializeField] private TMP_InputField _codeInput; // NET-02 초대 코드 입력
    [SerializeField] private Button _backButton;        // NET-04 뒤로

    public Button CreateButton => _createButton;
    public Button JoinButton => _joinButton;
    public TMP_InputField CodeInput => _codeInput;
    public Button BackButton => _backButton;
}