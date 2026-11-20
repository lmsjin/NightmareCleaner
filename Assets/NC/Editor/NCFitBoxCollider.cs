using UnityEngine;
using UnityEditor;

public static class FitBoxCollider
{
    // Box Collider 컴포넌트 우클릭 메뉴에 항목 추가
    [MenuItem("CONTEXT/BoxCollider/자식 모델에 맞추기")]
    static void Fit(MenuCommand command)
    {
        BoxCollider box = (BoxCollider)command.context;
        Transform root = box.transform;
        MeshFilter[] meshes = root.GetComponentsInChildren<MeshFilter>();

        bool first = true;
        Bounds total = new Bounds();

        foreach (MeshFilter mf in meshes)
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;

            // 메쉬를 감싸는 상자의 꼭짓점 8개를 부모 기준 좌표로 바꿔서 합치기
            for (int i = 0; i < 8; i++)
            {
                Vector3 sign = new Vector3((i & 1) == 0 ? -1 : 1,
                                           (i & 2) == 0 ? -1 : 1,
                                           (i & 4) == 0 ? -1 : 1);
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, sign);
                Vector3 local = root.InverseTransformPoint(mf.transform.TransformPoint(corner));

                if (first) { total = new Bounds(local, Vector3.zero); first = false; }
                else total.Encapsulate(local);
            }
        }

        if (first) { Debug.LogWarning("자식에 메쉬가 없어요."); return; }

        Undo.RecordObject(box, "Fit Box Collider"); // Ctrl+Z로 되돌리기 가능
        box.center = total.center;
        box.size = total.size;
    }
}