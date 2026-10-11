using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// UI 부품을 코드로 만드는 도구. 외부 이미지 없이 둥근 패널·테두리·바·키캡을 그린다.
/// 그래픽 담당이 스프라이트를 주면 RoundSprite / FrameSprite 만 교체하면 된다(UI 기획서 20장 공통 부품).
/// </summary>
public static class NCUiKit
{
    public static TMP_FontAsset Font;

    private static Sprite _round;
    private static Sprite _frame;
    private static Sprite _circle;

    /// <summary>둥근 모서리 채움(9-slice).</summary>
    public static Sprite RoundSprite => _round != null ? _round : (_round = MakeRounded(64, 16, 0));

    /// <summary>둥근 모서리 테두리 2px(9-slice).</summary>
    public static Sprite FrameSprite => _frame != null ? _frame : (_frame = MakeRounded(64, 16, 2));

    /// <summary>원(키캡·점).</summary>
    public static Sprite CircleSprite => _circle != null ? _circle : (_circle = MakeRounded(64, 32, 0));

    private static Sprite MakeRounded(int size, int radius, int border)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float outside = RoundedDistance(x + 0.5f, y + 0.5f, size, radius);
                float alpha = Mathf.Clamp01(0.5f - outside);

                if (border > 0)
                {
                    // 안쪽 사각형까지의 거리로 테두리만 남긴다.
                    float inside = RoundedDistance(x + 0.5f - border, y + 0.5f - border, size - border * 2, Mathf.Max(1, radius - border));
                    alpha *= Mathf.Clamp01(inside + 0.5f);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        float slice = radius + 1;
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(slice, slice, slice, slice));
    }

    /// <summary>둥근 사각형 바깥으로의 거리(안쪽이면 음수).</summary>
    private static float RoundedDistance(float x, float y, float size, float radius)
    {
        float half = size * 0.5f;
        float dx = Mathf.Abs(x - half) - (half - radius);
        float dy = Mathf.Abs(y - half) - (half - radius);
        float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
    }

    // ───────── 배치 ─────────

    public static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>앵커·피벗을 같은 점에 두고 위치·크기를 정한다.</summary>
    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    // ───────── 부품 ─────────

    public static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>남색 반투명 패널 + 황동 테두리.</summary>
    public static RectTransform NewPanel(string name, Transform parent, bool deep = false, bool frame = true)
    {
        Image back = NewImage(name, parent, RoundSprite, deep ? NCStyle.PanelDeep : NCStyle.Panel);

        if (frame)
        {
            Image border = NewImage("Frame", back.transform, FrameSprite, NCStyle.BrassDim);
            Stretch(border.rectTransform);
        }

        return back.rectTransform;
    }

    public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null)
        {
            tmp.font = Font;
        }

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    /// <summary>바(배경 + 채움). 채움 비율은 SetBar로 바꾼다.</summary>
    public static Image NewBar(string name, Transform parent, Color fillColor, out RectTransform back)
    {
        Image background = NewImage(name, parent, RoundSprite, NCStyle.BarBack);
        back = background.rectTransform;

        Image fill = NewImage("Fill", back, RoundSprite, fillColor);
        RectTransform rect = fill.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(3f, 3f);
        rect.offsetMax = new Vector2(-3f, -3f);
        return fill;
    }

    public static void SetBar(Image fill, float ratio)
    {
        RectTransform rect = fill.rectTransform;
        rect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        fill.enabled = ratio > 0.001f;
    }

    /// <summary>키캡(E·R·Q 등).</summary>
    public static RectTransform NewKeyCap(string name, Transform parent, string key, float size = 34f)
    {
        Image cap = NewImage(name, parent, RoundSprite, NCStyle.Text);
        cap.rectTransform.sizeDelta = new Vector2(Mathf.Max(size, 18f * key.Length + 14f), size);
        TextMeshProUGUI label = NewText("Key", cap.transform, key, size * 0.6f, NCStyle.PanelDeep, style: FontStyles.Bold);
        Stretch(label.rectTransform);
        return cap.rectTransform;
    }

    /// <summary>슬롯 태그(1P 파랑 / 2P 노랑).</summary>
    public static RectTransform NewSlotTag(string name, Transform parent, int slot, float height = 30f)
    {
        Image tag = NewImage(name, parent, RoundSprite, NCStyle.SlotColor(slot));
        tag.rectTransform.sizeDelta = new Vector2(height * 1.6f, height);
        TextMeshProUGUI label = NewText("Label", tag.transform, slot == 0 ? "1P" : "2P", height * 0.6f, NCStyle.PanelDeep, style: FontStyles.Bold);
        Stretch(label.rectTransform);
        return tag.rectTransform;
    }

    public static Button NewButton(string name, Transform parent, string label, bool primary, UnityAction onClick)
    {
        RectTransform rect = NewPanel(name, parent, deep: primary == false);
        Image image = rect.GetComponent<Image>();
        image.raycastTarget = true;

        if (primary)
        {
            image.color = NCStyle.Hex("234A78", 0.95f);
            rect.Find("Frame").GetComponent<Image>().color = NCStyle.Brass;
        }

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        colors.colorMultiplier = 1.2f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = NewText("Label", rect, label, NCStyle.FontBody + 2f, NCStyle.Text, style: FontStyles.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    public static TMP_InputField NewInput(string name, Transform parent, string placeholder)
    {
        RectTransform rect = NewPanel(name, parent, deep: true);
        rect.GetComponent<Image>().raycastTarget = true;

        RectTransform area = NewRect("TextArea", rect);
        Stretch(area, 10f);
        area.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI hint = NewText("Placeholder", area, placeholder, NCStyle.FontBody, NCStyle.TextDim, TextAlignmentOptions.Center);
        Stretch(hint.rectTransform);
        TextMeshProUGUI value = NewText("Text", area, "", NCStyle.FontGuide, NCStyle.Text, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(value.rectTransform);

        TMP_InputField field = rect.gameObject.AddComponent<TMP_InputField>();
        field.textViewport = area;
        field.textComponent = value;
        field.placeholder = hint;
        field.fontAsset = Font;
        field.characterLimit = 6;
        field.contentType = TMP_InputField.ContentType.Alphanumeric;
        field.onValidateInput = (text, index, c) => char.ToUpperInvariant(c);
        return field;
    }
}
