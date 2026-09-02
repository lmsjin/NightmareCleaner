using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GhostVisualSettingsSetup
{
    private const string AssetPath = "Assets/Resources/Configs/GhostVisualSettings.asset";

    [MenuItem("NightmareCleaner/Ghost/Create Default Visual Settings")]
    public static void CreateDefaultVisualSettings()
    {
        GhostVisualSettings settings = AssetDatabase.LoadAssetAtPath<GhostVisualSettings>(AssetPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<GhostVisualSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
        }

        SetEntries(settings, new[]
        {
            Entry(GhostVisualId.Jelly,  "Assets/Resources/Ghost/G_TypeA.fbx",    0.45f, 1.2f),
            Entry(GhostVisualId.Chubby, "Assets/Resources/Ghost/fat.fbx",        0.55f, 1.35f),
            Entry(GhostVisualId.Slim,   "Assets/Resources/Ghost/FLY.fbx",        0.35f, 1.1f),
            Entry(GhostVisualId.Chef,   "Assets/Resources/Ghost/all ghost_01.fbx", 0.5f,  1.25f),
            Entry(GhostVisualId.Repair, "Assets/Resources/Ghost/all ghost_01.fbx", 0.5f,  1.3f),
        });

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GhostVisualSettingsSetup] GhostVisualSettings.asset 생성/갱신 완료");
    }

    private static GhostVisualSettings.Entry Entry(GhostVisualId id, string modelPath, float radius, float height)
    {
        return new GhostVisualSettings.Entry
        {
            id = id,
            modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),
            localPosition = Vector3.zero,
            localEuler = Vector3.zero,
            localScale = Vector3.one,
            colliderRadius = radius,
            colliderHeight = height,
            useConfiguredCollider = true,
            tintMaterials = false
        };
    }

    private static void SetEntries(GhostVisualSettings settings, GhostVisualSettings.Entry[] entries)
    {
        FieldInfo field = typeof(GhostVisualSettings).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
            throw new MissingFieldException(nameof(GhostVisualSettings), "entries");
        field.SetValue(settings, entries ?? Array.Empty<GhostVisualSettings.Entry>());
    }
}
