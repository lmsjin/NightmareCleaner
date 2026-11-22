using UnityEngine;
using UnityEditor;

public class ReplaceWithPrefab : EditorWindow
{
    GameObject newPrefab;      // 바꿔 넣을 새 가구 프리팹
    bool keepScale = false;    // 체크하면 기존 오브젝트 크기를 복사

    [MenuItem("Tools/선택한 오브젝트 프리팹으로 교체")]
    static void Open() => GetWindow<ReplaceWithPrefab>("프리팹 교체");

    void OnGUI()
    {
        newPrefab = (GameObject)EditorGUILayout.ObjectField("새 프리팹", newPrefab, typeof(GameObject), false);
        keepScale = EditorGUILayout.Toggle("기존 크기 유지", keepScale);

        if (GUILayout.Button("선택한 오브젝트 교체") && newPrefab != null)
        {
            // 프리팹 편집 모드에서는 실행하지 않기
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog("프리팹 교체", "프리팹 편집 모드에서 나온 뒤 씬에서 실행하세요.", "확인");
                return;
            }

            foreach (GameObject old in Selection.gameObjects)
            {
                // Project 창의 파일(에셋)은 건너뛰기 ? 실수로 원본 파일을 지우는 걸 막아요
                if (EditorUtility.IsPersistent(old)) continue;

                GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, old.transform.parent);
                obj.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);

                if (keepScale)
                    obj.transform.localScale = old.transform.localScale;

                obj.transform.SetSiblingIndex(old.transform.GetSiblingIndex());

                Undo.RegisterCreatedObjectUndo(obj, "Replace");
                Undo.DestroyObjectImmediate(old);
            }
        }
    }
}