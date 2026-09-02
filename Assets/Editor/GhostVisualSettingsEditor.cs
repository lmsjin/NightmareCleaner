using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GhostVisualSettings))]
public class GhostVisualSettingsEditor : Editor
{
    private const string PreviewRootName = "[GhostVisualSettings Preview]";
    private const float PreviewSpacing = 2.4f;

    private SerializedProperty _entries;
    private bool _autoPreview = true;

    private void OnEnable()
    {
        _entries = serializedObject.FindProperty("entries");
        SceneView.duringSceneGui -= DrawPreviewGizmos;
        SceneView.duringSceneGui += DrawPreviewGizmos;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawPreviewGizmos;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_entries, true);
        bool changed = EditorGUI.EndChangeCheck();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);
        _autoPreview = EditorGUILayout.ToggleLeft("Auto refresh scene preview", _autoPreview);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create / Refresh Preview"))
                RefreshPreview();
            if (GUILayout.Button("Clear Preview"))
                ClearPreview();
        }

        EditorGUILayout.HelpBox(
            "Preview objects are editor-only scene helpers. Adjust Model Prefab, Local Euler, Local Scale, and collider values while watching the Scene view.",
            MessageType.Info);

        if (changed && _autoPreview)
            EditorApplication.delayCall += RefreshPreview;
    }

    private void RefreshPreview()
    {
        if (target == null) return;

        ClearPreview();

        GhostVisualSettings settings = (GhostVisualSettings)target;
        GameObject root = new(PreviewRootName);
        root.hideFlags = HideFlags.DontSave;
        root.transform.position = FindPreviewPosition();

        int index = 0;
        foreach (GhostVisualId id in System.Enum.GetValues(typeof(GhostVisualId)))
        {
            if (!settings.TryGet(id, out GhostVisualSettings.Entry entry) || entry == null)
                continue;

            GameObject slot = new(id.ToString());
            slot.hideFlags = HideFlags.DontSave;
            slot.transform.SetParent(root.transform, false);
            slot.transform.localPosition = new Vector3(index * PreviewSpacing, 0f, 0f);

            if (entry.modelPrefab != null)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(entry.modelPrefab);
                if (visual == null)
                    visual = Instantiate(entry.modelPrefab);
                visual.name = "Visual";
                visual.hideFlags = HideFlags.DontSave;
                visual.transform.SetParent(slot.transform, false);
                visual.transform.localPosition = entry.localPosition;
                visual.transform.localRotation = Quaternion.Euler(entry.localEuler);
                visual.transform.localScale = entry.localScale;
                DisablePreviewPhysics(visual);
            }

            CreateLabel(slot.transform, id.ToString());
            index++;
        }

        Selection.activeObject = settings;
        SceneView.RepaintAll();
    }

    private static Vector3 FindPreviewPosition()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
            return sceneView.pivot + Vector3.right * -4f;
        return Vector3.zero;
    }

    private static void DisablePreviewPhysics(GameObject visual)
    {
        foreach (Collider col in visual.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            DestroyImmediate(body);
    }

    private static void CreateLabel(Transform parent, string value)
    {
        GameObject label = new("Label");
        label.hideFlags = HideFlags.DontSave;
        label.transform.SetParent(parent, false);
        label.transform.localPosition = new Vector3(0f, 2.1f, 0f);

        TextMesh text = label.AddComponent<TextMesh>();
        text.text = value;
        text.characterSize = 0.18f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
    }

    private static void ClearPreview()
    {
        foreach (GameObject root in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (root.name == PreviewRootName)
                DestroyImmediate(root);
        }
        SceneView.RepaintAll();
    }

    private void DrawPreviewGizmos(SceneView sceneView)
    {
        GhostVisualSettings settings = target as GhostVisualSettings;
        if (settings == null) return;

        GameObject root = GameObject.Find(PreviewRootName);
        if (root == null) return;

        Handles.color = new Color(0.3f, 1f, 0.8f, 0.9f);
        foreach (GhostVisualId id in System.Enum.GetValues(typeof(GhostVisualId)))
        {
            if (!settings.TryGet(id, out GhostVisualSettings.Entry entry) || entry == null)
                continue;

            Transform slot = root.transform.Find(id.ToString());
            if (slot == null) continue;

            DrawWireCapsule(slot.position, entry.colliderRadius, entry.colliderHeight);
        }
    }

    private static void DrawWireCapsule(Vector3 basePosition, float radius, float height)
    {
        radius = Mathf.Max(0.05f, radius);
        height = Mathf.Max(radius * 2f, height);

        Vector3 bottom = basePosition + Vector3.up * radius;
        Vector3 top = basePosition + Vector3.up * (height - radius);
        Handles.DrawWireDisc(bottom, Vector3.up, radius);
        Handles.DrawWireDisc(top, Vector3.up, radius);
        Handles.DrawWireArc(top, Vector3.forward, Vector3.left, 180f, radius);
        Handles.DrawWireArc(top, Vector3.right, Vector3.forward, 180f, radius);
        Handles.DrawWireArc(bottom, Vector3.forward, Vector3.right, 180f, radius);
        Handles.DrawWireArc(bottom, Vector3.right, -Vector3.forward, 180f, radius);

        Handles.DrawLine(bottom + Vector3.right * radius, top + Vector3.right * radius);
        Handles.DrawLine(bottom - Vector3.right * radius, top - Vector3.right * radius);
        Handles.DrawLine(bottom + Vector3.forward * radius, top + Vector3.forward * radius);
        Handles.DrawLine(bottom - Vector3.forward * radius, top - Vector3.forward * radius);
    }
}
