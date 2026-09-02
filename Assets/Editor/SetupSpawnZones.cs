#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class SetupSpawnZones
{
    private const string SpawnZoneTag   = "SpawnZone";
    private const string SpawnPointName = "SpawnPoint";

    [MenuItem("Tools/Setup Spawn Zones")]
    static void Run()
    {
        // ── 0. 태그 확인 ───────────────────────────────────────────────
        bool tagExists = false;
        foreach (var t in UnityEditorInternal.InternalEditorUtility.tags)
            if (t == SpawnZoneTag) { tagExists = true; break; }
        if (!tagExists)
        {
            Debug.LogError($"태그 '{SpawnZoneTag}'가 없습니다. " +
                           "Edit > Project Settings > Tags and Layers 에서 먼저 추가하세요.");
            return;
        }

        // ── 1. "SpawnZone" 태그가 붙은 오브젝트만 탐색 ──────────────────
        var roomObjects = GameObject.FindGameObjectsWithTag(SpawnZoneTag);
        if (roomObjects.Length == 0)
        {
            Debug.LogError($"태그 '{SpawnZoneTag}'가 붙은 오브젝트가 씬에 없습니다. " +
                           "먼저 방 오브젝트에 태그를 수동으로 지정하세요.");
            return;
        }

        int zoneCount  = 0;
        int pointCount = 0;

        foreach (var roomGO in roomObjects)
        {
            var t = roomGO.transform;

            // ── 2. BoxCollider (isTrigger = true) ─────────────────────
            var col = roomGO.GetComponent<BoxCollider>();
            if (col == null) col = roomGO.AddComponent<BoxCollider>();
            col.isTrigger = true;

            Bounds bounds    = default;
            bool   hasBounds = false;
            var renderers = roomGO.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                hasBounds = true;

                col.center = t.InverseTransformPoint(bounds.center);
                col.size   = new Vector3(
                    bounds.size.x / t.lossyScale.x,
                    bounds.size.y / t.lossyScale.y,
                    bounds.size.z / t.lossyScale.z);
            }

            zoneCount++;

            // ── 3. 기존 SpawnPoint 제거 ────────────────────────────────
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i);
                if (c.name.StartsWith(SpawnPointName))
                    Object.DestroyImmediate(c.gameObject);
            }

            // ── 4. 방 바운드 중앙 바닥에 포인트 1개 배치 ──────────────
            if (hasBounds)
            {
                Vector3 center = new Vector3(
                    bounds.center.x,
                    bounds.min.y + 0.05f,
                    bounds.center.z);

                var sp = new GameObject($"{SpawnPointName}_0");
                sp.transform.SetParent(t, false);
                sp.transform.position = center;
                sp.AddComponent<ChildSpawnPoint>();
                pointCount++;
            }
            else
            {
                Debug.LogWarning($"[SetupSpawnZones] {roomGO.name} — Renderer가 없어 포인트를 배치하지 못했습니다.");
            }

            EditorUtility.SetDirty(roomGO);
        }

        Debug.Log($"[SetupSpawnZones] 완료 — 스폰존: {zoneCount}개 / 스폰포인트: {pointCount}개");
    }
}
#endif