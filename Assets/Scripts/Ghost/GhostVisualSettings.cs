using System;
using UnityEngine;

public enum GhostVisualId
{
    Jelly,
    Chubby,
    Slim,
    Chef,
    Repair
}

[CreateAssetMenu(fileName = "GhostVisualSettings", menuName = "NightmareCleaner/Ghost Visual Settings")]
public class GhostVisualSettings : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public GhostVisualId id;
        public GameObject modelPrefab;
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localEuler = Vector3.zero;
        public Vector3 localScale = Vector3.one;
        public float colliderRadius = 0.45f;
        public float colliderHeight = 1.2f;
        public bool useConfiguredCollider = true;
        public bool tintMaterials = false;
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    public static GhostVisualSettings Load()
    {
        return Resources.Load<GhostVisualSettings>("Configs/GhostVisualSettings");
    }

    public bool TryGet(GhostVisualId id, out Entry entry)
    {
        foreach (Entry candidate in entries)
        {
            if (candidate != null && candidate.id == id)
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }
}
