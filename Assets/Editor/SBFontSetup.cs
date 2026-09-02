using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class SBFontSetup
{
    private const string SourcePath = "Assets/Resources/UI/[UI]_Option/SB 어그로 L.ttf";
    private const string AssetPath = "Assets/Fonts/SB Aggro L SDF.asset";
    private const string BoldSourcePath = "Assets/Fonts/SB 어그로 B.ttf";
    private const string BoldAssetPath = "Assets/Fonts/SB Aggro B SDF.asset";

    public static TMP_FontAsset EnsureFont()
    {
        return EnsureFont(SourcePath, AssetPath, "SB Aggro L SDF", "SB 어그로 L");
    }

    public static TMP_FontAsset EnsureBoldFont()
    {
        return EnsureFont(BoldSourcePath, BoldAssetPath, "SB Aggro B SDF", "SB 어그로 B");
    }

    [MenuItem("NightmareCleaner/Setup SB Aggro B Font")]
    public static void SetupBoldFont()
    {
        TMP_FontAsset font = EnsureBoldFont();
        if (font != null)
            Debug.Log("[SBFontSetup] SB Aggro B SDF 폰트 생성/확인 완료");
    }

    [MenuItem("NightmareCleaner/Apply SB Aggro B To Mini Game Text")]
    public static void ApplyBoldFontToMiniGameText()
    {
        TMP_FontAsset font = EnsureBoldFont();
        if (font == null) return;

        int count = 0;
        foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsMiniGameText(text)) continue;
            Undo.RecordObject(text, "Apply SB Aggro B font");
            text.font = font;
            EditorUtility.SetDirty(text);
            count++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[SBFontSetup] 미니게임 텍스트 {count}개에 SB Aggro B SDF 적용 완료");
    }

    private static TMP_FontAsset EnsureFont(string sourcePath, string assetPath, string assetName, string logName)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
        {
            Debug.LogError($"{logName} 폰트를 찾을 수 없습니다: {sourcePath}");
            return null;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(source);
        fontAsset.name = assetName;
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        fontAsset.isMultiAtlasTexturesEnabled = true;
        AssetDatabase.CreateAsset(fontAsset, assetPath);

        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        foreach (Texture2D atlas in fontAsset.atlasTextures)
            if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, fontAsset);

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
    }

    private static bool IsMiniGameText(TMP_Text text)
    {
        if (text == null) return false;

        Transform current = text.transform;
        while (current != null)
        {
            string name = current.name;
            if (name == "SequenceCanvas" || name == "GaugeStopCanvas" || name == "CountdownCanvas" || name == "RescuePromptCanvas")
                return true;
            current = current.parent;
        }

        return false;
    }
}
