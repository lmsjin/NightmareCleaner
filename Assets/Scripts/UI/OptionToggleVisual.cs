using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class OptionToggleVisual : MonoBehaviour
{
    [SerializeField] private RectTransform handle;
    [SerializeField] private float handleOffset = 20f;

    private Toggle toggle;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(Refresh);
        Refresh(toggle.isOn);
    }

    public void Refresh(bool isOn)
    {
        if (handle == null) return;
        Vector2 position = handle.anchoredPosition;
        position.x = isOn ? handleOffset : -handleOffset;
        handle.anchoredPosition = position;
    }

    private void OnDestroy()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(Refresh);
    }
}
