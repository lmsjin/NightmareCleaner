// Assets/Editor/UISetupTool.cs
// HUD는 HUDController가 런타임에 코드로 자동 생성하므로 Canvas 빌드 코드는 없음.
// 이 툴은 "한글 폰트 1회 세팅"만 담당한다.
//   - Atlas가 포함된 MalgunGothic SDF를 TMP 기본 폰트로 지정
//   - UI 버튼 클릭용 EventSystem 보장 (New Input System 호환)
// 메뉴: NightmareCleaner > Setup Korean Font

using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public static class UISetupTool
{
    [MenuItem("NightmareCleaner/Setup Korean Font")]
    static void SetupKoreanFont()
    {
        TMP_FontAsset korFont = EnsureKoreanFont();
        EnsureEventSystem();

        if (korFont != null)
            EditorUtility.DisplayDialog("완료",
                "정상 맑은 고딕 TMP 폰트를 기본 폰트로 설정했습니다.",
                "확인");
    }

    // ──────────────────────────────────────────
    // 한글 폰트: 맑은 고딕 Dynamic TMP FontAsset
    // ──────────────────────────────────────────
    static TMP_FontAsset EnsureKoreanFont()
    {
        const string assetPath = "Assets/Fonts/MalgunGothic SDF.asset";
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fontAsset == null)
        {
            Debug.LogError("[UISetupTool] 정상 폰트를 찾을 수 없습니다: " + assetPath);
            return null;
        }

        TMP_Settings settings = TMP_Settings.instance;
        if (settings == null) return fontAsset;

        SerializedObject so = new SerializedObject(settings);
        SerializedProperty defaultFont = so.FindProperty("m_defaultFontAsset");
        SerializedProperty fallbacks = so.FindProperty("m_fallbackFontAssets");
        if (defaultFont != null) defaultFont.objectReferenceValue = fontAsset;
        if (fallbacks != null) fallbacks.ClearArray();
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log("[UISetupTool] MalgunGothic SDF를 TMP 기본 폰트로 설정했습니다.");
        return fontAsset;
    }

    // ──────────────────────────────────────────
    // EventSystem (UI 버튼 클릭용, 없을 때만 생성)
    // ──────────────────────────────────────────
    static void EnsureEventSystem()
    {
        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            go.AddComponent<EventSystem>();
        }

        StandaloneInputModule old = go.GetComponent<StandaloneInputModule>();
        if (old != null) Object.DestroyImmediate(old);

        TryAddInputSystemUIInputModule(go);
    }

    // UGUI 입력 모듈이 없을 때만 추가. 패키지 없으면 StandaloneInputModule 폴백.
    static void TryAddInputSystemUIInputModule(GameObject go)
    {
        System.Type t = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem.ForUI");
        if (t == null)
            t = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (t != null)
        {
            if (go.GetComponent(t) == null) go.AddComponent(t);
        }
        else if (go.GetComponent<StandaloneInputModule>() == null)
        {
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
