using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 클릭 가능한 모든 UI 오브젝트(버튼, 카드 등)에 그대로 붙이기만 하면 동작.
// Button 컴포넌트가 있으면 Button.onClick에 직접 연결(가장 안정적인 경로).
// Button이 없는 일반 클릭 UI(Image 등)는 포인터 클릭 감지로 보조 처리.
public class UiClickSfx : MonoBehaviour, IPointerClickHandler
{
    private Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(BgmManager.PlayUiClick);
    }

    void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(BgmManager.PlayUiClick);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button == null) // Button이 있으면 위에서 이미 처리되므로 중복 재생 방지
            BgmManager.PlayUiClick();
    }
}