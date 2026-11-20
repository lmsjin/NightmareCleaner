using UnityEngine;
using UnityEditor;

public class ReplaceWithPrefab : EditorWindow
{
    GameObject newPrefab; // 바꿔 넣을 새 가구 프리팹

    [MenuItem("Tools/선택한 오브젝트 프리팹으로 교체")]
    static void Open() => GetWindow<ReplaceWithPrefab>("프리팹 교체");

    void OnGUI()
    {
        newPrefab = (GameObject)EditorGUILayout.ObjectField("새 프리팹", newPrefab, typeof(GameObject), false);

        if (GUILayout.Button("선택한 오브젝트 교체") && newPrefab != null)
        {
            foreach (GameObject old in Selection.gameObjects)
            {
                // 새 프리팹을 같은 부모 아래에 생성
                GameObject obj = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, old.transform.parent);
                // 위치, 회전, 크기 복사
                obj.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
                obj.transform.localScale = old.transform.localScale;
                obj.transform.SetSiblingIndex(old.transform.GetSiblingIndex());

                Undo.RegisterCreatedObjectUndo(obj, "Replace");
                Undo.DestroyObjectImmediate(old); // Ctrl+Z로 되돌리기 가능
            }
        }
    }
}