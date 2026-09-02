using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
public static class EditableMiniGameUI
{
    public static GameObject CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        GameObject canvasObject = new(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        UnityEditor.Undo.RegisterCreatedObjectUndo(canvasObject, $"Create {name}");
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        return canvasObject;
    }

    public static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 size)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
        UnityEditor.Undo.RegisterCreatedObjectUndo(imageObject, $"Create {name}");
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return image;
    }

    public static void CreateDim(Transform parent)
    {
        Image dim = CreateImage(parent, "Dim", null, Vector2.zero);
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform rect = dim.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static TMP_Text CreateText(Transform parent, string name, float fontSize, Vector2 position, Vector2 size)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        UnityEditor.Undo.RegisterCreatedObjectUndo(textObject, $"Create {name}");
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }
}
#endif
