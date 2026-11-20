using UnityEngine;
using UnityEditor;

public static class DropToFloor
{
    [MenuItem("Tools/선택한 오브젝트 바닥에 내리기 _END")] // End 키 단축키
    static void Drop()
    {
        foreach (Transform t in Selection.transforms)
        {
            // 자기 자신의 콜라이더에 맞지 않도록 잠시 끄기
            Collider[] own = t.GetComponentsInChildren<Collider>();
            foreach (var c in own) c.enabled = false;

            // 피벗보다 조금 위에서 아래로 레이 쏘기
            if (Physics.Raycast(t.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 100f))
            {
                Undo.RecordObject(t, "Drop To Floor"); // Ctrl+Z 가능
                t.position = hit.point;
            }

            foreach (var c in own) c.enabled = true;
        }
    }
}