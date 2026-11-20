using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonHoverScale : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] float hoverScale = 1.05f;
    [SerializeField] float speed = 15f;

    Vector3 originalScale;
    bool hovered;
    Button button;

    void Awake()
    {
        originalScale = transform.localScale;
        button = GetComponent<Button>();
    }

    void Update()
    {
        bool enlarge = hovered && (button == null || button.IsInteractable());
        Vector3 target = originalScale * (enlarge ? hoverScale : 1f);

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            target,
            1f - Mathf.Exp(-speed * Time.unscaledDeltaTime)
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
        => hovered = true;

    public void OnPointerExit(PointerEventData eventData)
        => hovered = false;

    void OnDisable()
    {
        hovered = false;
        transform.localScale = originalScale;
    }
}