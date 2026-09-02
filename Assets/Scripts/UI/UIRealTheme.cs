using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UIRealTheme
{
    private const string Root = "UI/UI_REAL/";
    private static readonly Dictionary<string, Sprite> Cache = new();

    public static Sprite LoadSprite(string relativePath)
    {
        if (Cache.TryGetValue(relativePath, out Sprite cached))
            return cached;

        Sprite sprite = Resources.Load<Sprite>(Root + relativePath);
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(Root + relativePath);
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                sprite.name = texture.name;
            }
        }

        Cache[relativePath] = sprite;
        return sprite;
    }

    public static void SkinButton(Transform root, string objectName, string spritePath)
    {
        Transform target = FindDeep(root, objectName);
        if (target == null) return;

        Image image = target.GetComponent<Image>();
        Sprite sprite = LoadSprite(spritePath);
        if (image == null || sprite == null) return;

        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        // target.GetComponent<RectTransform>().sizeDelta = sprite.rect.size;

        foreach (TMP_Text label in target.GetComponentsInChildren<TMP_Text>(true))
        {
            label.gameObject.SetActive(true);
            if (label.color.a < 0.01f)
                label.color = Color.white;
        }
    }

    public static void SkinImage(Transform root, string objectName, string spritePath, bool preserveAspect = true)
    {
        Transform target = FindDeep(root, objectName);
        Image image = target != null ? target.GetComponent<Image>() : null;
        Sprite sprite = LoadSprite(spritePath);
        if (image == null || sprite == null) return;

        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = preserveAspect;
    }

    public static Transform FindDeep(Transform root, string objectName)
    {
        if (root == null) return null;
        if (root.name == objectName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), objectName);
            if (found != null) return found;
        }
        return null;
    }

    public static void ApplyOptionTheme(Transform root)
    {
        SkinImage(root, "OptionPanel", "[UI]_Option/OptionPanel", false);
        SkinImage(root, "PopupPanel", "[UI]_Option/PopupPanel", false);
        SkinImage(root, "Information", "[UI]_Option/OptionInformationPopup", false);
        SkinImage(root, "KeyGuide", "[UI]_Option/KeyGuideImage");
        SkinImage(root, "BarGauge", "[UI]_Option/BarGauge", false);
        SkinButton(root, "ApplyButton", "[UI]_Option/OptionButton_Apply");
        SkinButton(root, "CancelButton", "[UI]_Option/OptionButton_Cancle");
        SkinButton(root, "ResetButton", "[UI]_Option/OptionButton_Reset");
        SkinButton(root, "ConfirmButton", "[UI]_Option/OptionButton_OK");
    }

    public static void SkinToggle(Toggle toggle)
    {
        if (toggle == null) return;
        if (toggle.targetGraphic is Image track)
        {
            track.sprite = LoadSprite("[UI]_Option/ONOFF_Switch_Track");
            track.color = Color.white;
            track.preserveAspect = true;
        }
        if (toggle.graphic is Image handle)
        {
            handle.sprite = LoadSprite("[UI]_Option/ONOFF_Switch_Handle");
            handle.color = Color.white;
            handle.preserveAspect = true;
        }
    }
}
