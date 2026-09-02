using UnityEngine;

[ExecuteAlways]
public class RescueBootstrap : MonoBehaviour
{
    [SerializeField] private ChildRescueSystem rescueSystem;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        if (rescueSystem == null)
            rescueSystem = GetComponentInChildren<ChildRescueSystem>(true);

        if (rescueSystem == null)
            Debug.LogError("[RescueBootstrap] CleanerChildRescue가 없습니다. 컴포넌트 메뉴에서 Setup Editable Mini Game UI를 실행하세요.");
    }

#if UNITY_EDITOR
    [ContextMenu("Setup Editable Mini Game UI")]
    private void SetupEditableMiniGameUI()
    {
        gameObject.SetActive(true);
        Transform child = transform.Find("CleanerChildRescue");
        GameObject systemObject;
        if (child == null)
        {
            systemObject = new GameObject("CleanerChildRescue");
            UnityEditor.Undo.RegisterCreatedObjectUndo(systemObject, "Create editable mini game UI");
            systemObject.transform.SetParent(transform, false);
        }
        else
        {
            systemObject = child.gameObject;
        }

        rescueSystem = GetOrAdd<ChildRescueSystem>(systemObject);
        RescueMiniGameManager manager = GetOrAdd<RescueMiniGameManager>(systemObject);
        MiniGameSequence sequence = GetOrAdd<MiniGameSequence>(systemObject);
        MiniGameGaugeStop gauge = GetOrAdd<MiniGameGaugeStop>(systemObject);

        sequence.SetupEditableUI();
        gauge.SetupEditableUI();
        manager.SetupEditableUI(sequence, gauge);
        rescueSystem.SetupEditablePrompt();

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : UnityEditor.Undo.AddComponent<T>(target);
    }
#endif
}
