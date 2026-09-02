using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FinalUIHierarchySetup
{
    [MenuItem("NightmareCleaner/Setup Final UI Hierarchy")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid()) return;

        RescueBootstrap bootstrap = Object.FindFirstObjectByType<RescueBootstrap>(FindObjectsInactive.Include);
        if (bootstrap != null)
            InvokeContextMethod(bootstrap, "SetupEditableMiniGameUI");

        foreach (HUDController hud in Object.FindObjectsByType<HUDController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            hud.SetupEditableHealthUI();

        foreach (AbilitySystem ability in Object.FindObjectsByType<AbilitySystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ability.SetupEditableBonusTexts();

        MinimapController[] minimaps = Object.FindObjectsByType<MinimapController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (minimaps.Length == 0)
        {
            GameSystems systems = Object.FindFirstObjectByType<GameSystems>(FindObjectsInactive.Include);
            if (systems != null)
                minimaps = new[] { Undo.AddComponent<MinimapController>(systems.gameObject) };
        }

        foreach (MinimapController minimap in minimaps)
            minimap.SetupEditableUI();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FinalUIHierarchySetup] 모든 UI를 Hierarchy에 생성하고 씬을 저장했습니다.");
    }

    private static void InvokeContextMethod(Object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        method?.Invoke(target, null);
    }
}
