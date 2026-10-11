using System.Collections.Generic;
using System.IO;
using Fusion;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 메뉴 한 번으로 NC 테스트 씬을 만든다: 머티리얼 · 프리팹(플레이어·유령) · 맵 · 전투방 · UI · 빌드 목록.
/// 연결 필드는 코드가 직접 넣으므로 MCP 연결 오류가 생기지 않는다. 다시 실행하면 씬을 새로 만든다.
///
/// 맵(1유닛 = 1m, 벽 높이 3m):
///   저택 입구 복도(3×8) → 중앙홀(8×8, 시작 영역) → 출입구 A → 전투방 "연회장"(12×10) ↔ 출입구 B ↔ 창고(4×4)
/// </summary>
public static class NCSceneBuilder
{
    private const string Root = "Assets/NC";
    private const string ScenePath = Root + "/Scenes/NC_Game.unity";
    private const string FontPath = "Assets/Fonts/MalgunGothic SDF.asset";
    private const float WallHeight = 3f;
    private const float WallThickness = 0.3f;
    private const int IgnoreRaycastLayer = 2; // 연출 오브젝트: 탄환·NavMesh에 영향 없음

    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    [MenuItem("NightmareCleaner/Build NC Scene", priority = 0)]
    public static void Build()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
        {
            return;
        }

        EnsureFolders();
        EnsureBalance();
        Materials.Clear();

        BuildPlayerPrefab();
        BuildGhostPrefab();

        // Fusion이 새 프리팹을 네트워크 프리팹 목록에 올리도록 한다.
        // 이 과정에서 프리팹이 다시 임포트되어 앞서 받은 참조가 무효가 되므로, 경로로 다시 불러온다.
        EditorApplication.ExecuteMenuItem("Tools/Fusion/Rebuild Prefab Table");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        NetworkObject playerPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(Root + "/Prefabs/NC_Player.prefab");
        NetworkObject ghostPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(Root + "/Prefabs/NC_Ghost.prefab");

        if (playerPrefab == null || ghostPrefab == null)
        {
            Debug.LogError("[NCSceneBuilder] 프리팹을 불러오지 못했습니다. 메뉴를 한 번 더 실행해 주세요.");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SetupCameraAndLight();

        Transform[] entrySpawns = BuildMap(out Transform startZone, out GameObject mapRoot);
        BuildCombatRoom(ghostPrefab);
        BuildSystems(playerPrefab, entrySpawns, startZone);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildList();

        Debug.Log("[NCSceneBuilder] 완료: " + ScenePath + " — Play 후 [방 만들기]로 시작하세요. " +
                  "혼자 테스트하려면 Assets/NC/Resources/NCBalance 의 Solo Test 를 켜세요.");
    }

    // ───────── 준비 ─────────

    private static void EnsureFolders()
    {
        foreach (string folder in new[] { "Prefabs", "Materials", "Scenes", "Resources" })
        {
            if (AssetDatabase.IsValidFolder(Root + "/" + folder) == false)
            {
                AssetDatabase.CreateFolder(Root, folder);
            }
        }
    }

    private static void EnsureBalance()
    {
        string path = Root + "/Resources/NCBalance.asset";
        if (AssetDatabase.LoadAssetAtPath<NCBalance>(path) == null)
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<NCBalance>(), path);
        }
    }

    private static Material Mat(string name, Color color, bool transparent = false)
    {
        if (Materials.TryGetValue(name, out Material cached))
        {
            return cached;
        }

        string path = $"{Root}/Materials/{name}.mat";
        Shader shader = transparent ? Shader.Find("Sprites/Default") : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.15f);
        }

        EditorUtility.SetDirty(material);
        Materials[name] = material;
        return material;
    }

    private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;

        if (collider == false)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        return go;
    }

    private static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    private static void Assign(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignArray(Object target, string field, Object[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────── 프리팹 ─────────

    private static NetworkObject BuildPlayerPrefab()
    {
        GameObject root = new GameObject("NC_Player");
        root.AddComponent<NetworkObject>();

        CharacterController cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.3f;

        root.AddComponent<NetworkCharacterController>();
        NCPlayer player = root.AddComponent<NCPlayer>();
        root.AddComponent<NCHealth>();
        root.AddComponent<NCGun>();

        // 발밑 기준 회전축(다운 시 눕는다) 아래에 모든 외형을 둔다.
        Transform body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);

        Material suit = Mat("NC_PlayerSuit", Color.white);
        Material dark = Mat("NC_Dark", new Color(0.12f, 0.12f, 0.15f));
        Material metal = Mat("NC_Metal", new Color(0.55f, 0.58f, 0.62f));

        GameObject torso = Shape(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 0.9f, 0f), new Vector3(0.75f, 0.9f, 0.75f), suit);
        Shape(PrimitiveType.Sphere, "Goggles", body, new Vector3(0f, 1.45f, 0.3f), new Vector3(0.5f, 0.2f, 0.2f), dark);
        Shape(PrimitiveType.Cube, "Tank", body, new Vector3(0f, 1.05f, -0.42f), new Vector3(0.42f, 0.6f, 0.26f), metal);
        Shape(PrimitiveType.Cube, "Gun", body, new Vector3(0.28f, 1.0f, 0.42f), new Vector3(0.14f, 0.14f, 0.62f), metal);

        Transform nozzle = new GameObject("Nozzle").transform;
        nozzle.SetParent(body, false);
        nozzle.localPosition = new Vector3(0.28f, 1.0f, 0.8f);

        Assign(player, "bodyRenderer", torso.GetComponent<Renderer>());
        Assign(player, "nozzle", nozzle);

        string path = Root + "/Prefabs/NC_Player.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<NetworkObject>();
    }

    private static NetworkObject BuildGhostPrefab()
    {
        GameObject root = new GameObject("NC_Ghost");
        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkTransform>();
        NCGhost ghost = root.AddComponent<NCGhost>();

        SphereCollider collider = root.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.45f;
        collider.center = new Vector3(0f, 0.9f, 0f);

        Material skin = Mat("NC_GhostSkin", NCStyle.GhostBasic, transparent: true);
        Material eye = Mat("NC_GhostEye", new Color(0.15f, 0.1f, 0.25f));

        GameObject body = Shape(PrimitiveType.Sphere, "Body", root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.85f, 1f, 0.85f), skin);
        Shape(PrimitiveType.Sphere, "EyeL", body.transform, new Vector3(-0.17f, 0.12f, 0.43f), new Vector3(0.14f, 0.2f, 0.06f), eye);
        Shape(PrimitiveType.Sphere, "EyeR", body.transform, new Vector3(0.17f, 0.12f, 0.43f), new Vector3(0.14f, 0.2f, 0.06f), eye);

        Assign(ghost, "bodyRenderer", body.GetComponent<Renderer>());

        string path = Root + "/Prefabs/NC_Ghost.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<NetworkObject>();
    }

    // ───────── 씬 ─────────

    private static void SetupCameraAndLight()
    {
        Camera cam = Camera.main;
        cam.transform.position = new Vector3(0f, 13f, -18.5f);
        cam.transform.LookAt(new Vector3(0f, 0.8f, -10f));
        cam.fieldOfView = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = NCStyle.Hex("0B0F18");
        cam.gameObject.AddComponent<NCCameraRig>();

        Light sun = Object.FindFirstObjectByType<Light>();
        sun.color = NCStyle.Hex("FFE2B8");
        sun.intensity = 0.9f;
        sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = NCStyle.Hex("4A4060");
    }

    private static Transform[] BuildMap(out Transform startZone, out GameObject mapRoot)
    {
        mapRoot = new GameObject("Map");
        Transform map = mapRoot.transform;

        NavMeshSurface surface = mapRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = ~(1 << IgnoreRaycastLayer);
        mapRoot.AddComponent<NCNavBaker>();

        Material wood = Mat("NC_FloorWood", NCStyle.Hex("7A5236"));
        Material stone = Mat("NC_FloorStone", NCStyle.Hex("6B5E52"));
        Material wall = Mat("NC_Wall", NCStyle.Hex("3A2E3E"));
        Material trim = Mat("NC_WallTrim", NCStyle.Hex("8C6B3E"));
        Material carpet = Mat("NC_Carpet", NCStyle.Hex("7A2430"));
        Material carpetHall = Mat("NC_CarpetBlue", NCStyle.Hex("2C3F66"));
        Material table = Mat("NC_Table", NCStyle.Hex("5A3A24"));
        Material cloth = Mat("NC_TableCloth", NCStyle.Hex("E8DFCF"));

        // 바닥(윗면 y = 0)
        Floor("Floor_Entrance", map, new Vector3(0f, 0f, -10f), new Vector2(3f, 8f), stone);
        Floor("Floor_Hall", map, new Vector3(0f, 0f, -2f), new Vector2(8f, 8f), stone);
        Floor("Floor_Banquet", map, new Vector3(0f, 0f, 7f), new Vector2(12f, 10f), wood);
        Floor("Floor_Storage", map, new Vector3(8f, 0f, 7f), new Vector2(4f, 4f), wood);

        // 벽 (출입구 자리는 비운다)
        WallZ(map, -1.5f, -14f, -6f, wall, trim);   // 복도 서
        WallZ(map, 1.5f, -14f, -6f, wall, trim);    // 복도 동
        WallX(map, -14f, -1.5f, 1.5f, wall, trim);  // 복도 남
        WallX(map, -6f, -4f, -1.5f, wall, trim);    // 홀 남(서쪽)
        WallX(map, -6f, 1.5f, 4f, wall, trim);      // 홀 남(동쪽)
        WallZ(map, -4f, -6f, 2f, wall, trim);       // 홀 서
        WallZ(map, 4f, -6f, 2f, wall, trim);        // 홀 동
        WallX(map, 2f, -6f, -1f, wall, trim);       // 연회장 남(서) — 출입구 A: x -1~1
        WallX(map, 2f, 1f, 6f, wall, trim);         // 연회장 남(동)
        WallZ(map, -6f, 2f, 12f, wall, trim);       // 연회장 서
        WallX(map, 12f, -6f, 6f, wall, trim);       // 연회장 북
        WallZ(map, 6f, 2f, 6f, wall, trim);         // 연회장 동(남) — 출입구 B: z 6~8
        WallZ(map, 6f, 8f, 12f, wall, trim);        // 연회장 동(북)
        WallX(map, 5f, 6f, 10f, wall, trim);        // 창고 남
        WallX(map, 9f, 6f, 10f, wall, trim);        // 창고 북
        WallZ(map, 10f, 5f, 9f, wall, trim);        // 창고 동

        // 장식(연출 레이어: 길찾기·탄환에 영향 없음)
        Decor(Box("Carpet_Hall", map, new Vector3(0f, 0.01f, -4f), new Vector3(2.4f, 0.02f, 11.5f), carpetHall, false));
        Decor(Box("Carpet_Banquet", map, new Vector3(0f, 0.01f, 7f), new Vector3(9f, 0.02f, 7f), carpet, false));

        // 연회장 양옆의 케이크 테이블(엄폐물)
        foreach (float x in new[] { -4.4f, 4.4f })
        {
            GameObject t = Box("Table", map, new Vector3(x, 0.4f, 7f), new Vector3(1.1f, 0.8f, 2.2f), table);
            Box("Cloth", t.transform, new Vector3(0f, 0.52f, 0f), new Vector3(1.05f, 0.06f, 1.05f), cloth, false);
        }

        // 창고의 상자 더미(장식)
        Box("Crate", map, new Vector3(9.1f, 0.4f, 8.1f), new Vector3(0.8f, 0.8f, 0.8f), table);
        Box("Crate", map, new Vector3(9.2f, 0.3f, 6.0f), new Vector3(0.6f, 0.6f, 0.6f), table);

        // 촛불 조명
        Candle(map, new Vector3(0f, 2.4f, -10f), 6f);
        Candle(map, new Vector3(-3f, 2.4f, -2f), 7f);
        Candle(map, new Vector3(3f, 2.4f, -2f), 7f);
        Candle(map, new Vector3(-4.5f, 2.4f, 4f), 8f);
        Candle(map, new Vector3(4.5f, 2.4f, 4f), 8f);
        Candle(map, new Vector3(-4.5f, 2.4f, 10.5f), 8f);
        Candle(map, new Vector3(4.5f, 2.4f, 10.5f), 8f);
        Candle(map, new Vector3(8f, 2.4f, 7f), 5f);

        // 중앙홀 시작 영역 표시(빛나는 원)
        GameObject zone = new GameObject("StartZone");
        zone.transform.SetParent(map, false);
        zone.transform.localPosition = new Vector3(0f, 0.03f, -2f);
        float radius = NCBalance.Get.startZoneRadius;
        Shape(PrimitiveType.Cylinder, "Glow", zone.transform, Vector3.zero, new Vector3(radius * 2f, 0.01f, radius * 2f),
            Mat("NC_StartGlow", NCStyle.Hex("58B8EA", 0.18f), transparent: true));
        Shape(PrimitiveType.Cylinder, "Ring", zone.transform, Vector3.up * -0.005f, new Vector3(radius * 2f + 0.25f, 0.005f, radius * 2f + 0.25f),
            Mat("NC_StartRing", NCStyle.Hex("B6955E", 0.7f), transparent: true));
        SetLayerRecursive(zone, IgnoreRaycastLayer);
        startZone = zone.transform;

        // 입구 1P·2P 시작 위치
        Transform spawn1 = Marker("EntrySpawn_1P", map, new Vector3(-0.7f, 0.1f, -12.5f));
        Transform spawn2 = Marker("EntrySpawn_2P", map, new Vector3(0.7f, 0.1f, -12.5f));
        return new[] { spawn1, spawn2 };
    }

    private static void Floor(string name, Transform parent, Vector3 center, Vector2 size, Material material)
    {
        Box(name, parent, center + Vector3.down * 0.1f, new Vector3(size.x, 0.2f, size.y), material);
    }

    /// <summary>X축 방향 벽(z 고정).</summary>
    private static void WallX(Transform parent, float z, float x0, float x1, Material wall, Material trim)
    {
        float length = x1 - x0;
        GameObject w = Box("Wall", parent, new Vector3((x0 + x1) * 0.5f, WallHeight * 0.5f, z), new Vector3(length + WallThickness, WallHeight, WallThickness), wall);
        Box("Trim", w.transform, new Vector3(0f, 0.5f + 0.02f, 0f), new Vector3(1.002f, 0.04f / WallHeight, 1.1f), trim, false);
    }

    /// <summary>Z축 방향 벽(x 고정).</summary>
    private static void WallZ(Transform parent, float x, float z0, float z1, Material wall, Material trim)
    {
        float length = z1 - z0;
        GameObject w = Box("Wall", parent, new Vector3(x, WallHeight * 0.5f, (z0 + z1) * 0.5f), new Vector3(WallThickness, WallHeight, length + WallThickness), wall);
        Box("Trim", w.transform, new Vector3(0f, 0.5f + 0.02f, 0f), new Vector3(1.1f, 0.04f / WallHeight, 1.002f), trim, false);
    }

    private static void Decor(GameObject go)
    {
        SetLayerRecursive(go, IgnoreRaycastLayer);
    }

    private static void Candle(Transform parent, Vector3 position, float range)
    {
        GameObject go = new GameObject("CandleLight");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = NCStyle.Hex("FFB35C");
        light.range = range;
        light.intensity = 2.2f;
        light.shadows = LightShadows.None;
    }

    private static Transform Marker(string name, Transform parent, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        return go.transform;
    }

    private static void BuildCombatRoom(NetworkObject ghostPrefab)
    {
        GameObject roomObject = new GameObject("Room_Banquet");
        roomObject.transform.position = new Vector3(0f, 0f, 7f);
        roomObject.AddComponent<NetworkObject>();
        NCCombatRoom room = roomObject.AddComponent<NCCombatRoom>();
        Transform r = roomObject.transform;

        // 방 영역(트리거)
        GameObject area = new GameObject("Area");
        area.transform.SetParent(r, false);
        area.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        BoxCollider areaBox = area.AddComponent<BoxCollider>();
        areaBox.isTrigger = true;
        areaBox.size = new Vector3(11.6f, 3f, 9.6f);
        area.layer = IgnoreRaycastLayer;

        // 잡동사니 더미(상자·책) — 정리 완료 시 사라진다
        GameObject dummy = new GameObject("Dummy");
        dummy.transform.SetParent(r, false);
        dummy.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
        Material crate = Mat("NC_Crate", NCStyle.Hex("A0703F"));
        Material bookA = Mat("NC_BookBlue", NCStyle.Hex("3E5E8A"));
        Material bookB = Mat("NC_BookRed", NCStyle.Hex("8A3E46"));
        Box("Crate", dummy.transform, new Vector3(-0.3f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), crate);
        Box("Crate", dummy.transform, new Vector3(0.35f, 0.25f, 0.2f), new Vector3(0.5f, 0.5f, 0.5f), crate);
        Box("Crate", dummy.transform, new Vector3(-0.1f, 0.75f, 0.05f), new Vector3(0.4f, 0.3f, 0.4f), crate, false);
        Box("Books", dummy.transform, new Vector3(0.35f, 0.58f, 0.2f), new Vector3(0.4f, 0.16f, 0.3f), bookA, false);
        Box("Books", dummy.transform, new Vector3(0.32f, 0.72f, 0.18f), new Vector3(0.36f, 0.12f, 0.28f), bookB, false);
        Shape(PrimitiveType.Cylinder, "Hint", dummy.transform, new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.005f, 2.2f),
            Mat("NC_DummyHint", NCStyle.Hex("F2C34E", 0.18f), transparent: true));
        SetLayerRecursive(dummy, IgnoreRaycastLayer);

        // 봉쇄 장벽 + 봉쇄 영역(출입구 A: 남쪽, 출입구 B: 동쪽)
        Material barrierMat = Mat("NC_Barrier", NCStyle.Hex("58B8EA", 0.38f), transparent: true);
        GameObject barrierA = Barrier("Barrier_A", r, new Vector3(0f, 1.5f, -5f), new Vector3(2.1f, 3f, 0.2f), barrierMat);
        GameObject barrierB = Barrier("Barrier_B", r, new Vector3(6f, 1.5f, 0f), new Vector3(0.2f, 3f, 2.1f), barrierMat);
        BoxCollider zoneA = SealZone("SealZone_A", r, new Vector3(0f, 1f, -5f), new Vector3(2.2f, 2f, 1.8f));
        BoxCollider zoneB = SealZone("SealZone_B", r, new Vector3(6f, 1f, 0f), new Vector3(1.8f, 2f, 2.2f));

        // 유령 출현 지점 3곳(출입구에서 먼 구석)
        Transform s1 = Marker("SpawnPoint_1", r, new Vector3(-4.6f, 0.1f, 3.6f));
        Transform s2 = Marker("SpawnPoint_2", r, new Vector3(4.4f, 0.1f, 3.7f));
        Transform s3 = Marker("SpawnPoint_3", r, new Vector3(-4.7f, 0.1f, -3.4f));

        Assign(room, "ghostPrefab", ghostPrefab);
        Assign(room, "dummy", dummy.transform);
        Assign(room, "area", areaBox);
        AssignArray(room, "sealZones", new Object[] { zoneA, zoneB });
        AssignArray(room, "barriers", new Object[] { barrierA, barrierB });
        AssignArray(room, "spawnPoints", new Object[] { s1, s2, s3 });
    }

    private static GameObject Barrier(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrier.name = name;
        barrier.transform.SetParent(parent, false);
        barrier.transform.localPosition = position;
        barrier.transform.localScale = size;
        barrier.GetComponent<Renderer>().sharedMaterial = material;

        NavMeshObstacle obstacle = barrier.AddComponent<NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.shape = NavMeshObstacleShape.Box;

        barrier.SetActive(false); // 정리 완료부터 소탕 완료까지만 켜진다(NavMesh도 열린 상태로 굽힌다)
        return barrier;
    }

    private static BoxCollider SealZone(string name, Transform parent, Vector3 position, Vector3 size)
    {
        GameObject zone = new GameObject(name);
        zone.transform.SetParent(parent, false);
        zone.transform.localPosition = position;
        zone.layer = IgnoreRaycastLayer;
        BoxCollider box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        return box;
    }

    private static void BuildSystems(NetworkObject playerPrefab, Transform[] entrySpawns, Transform startZone)
    {
        GameObject manager = new GameObject("NetworkManager");
        NCLauncher launcher = manager.AddComponent<NCLauncher>();
        Assign(launcher, "playerPrefab", playerPrefab);
        AssignArray(launcher, "entrySpawnPoints", entrySpawns);

        GameObject session = new GameObject("Session");
        session.AddComponent<NetworkObject>();
        NCSession sessionComponent = session.AddComponent<NCSession>();
        Assign(sessionComponent, "startZone", startZone);

        GameObject ui = new GameObject("UI");
        NCUi uiComponent = ui.AddComponent<NCUi>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogWarning("[NCSceneBuilder] 한글 폰트를 찾지 못했습니다: " + FontPath);
        }

        Assign(uiComponent, "font", font);

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    /// <summary>NC_Game을 빌드 목록 맨 앞에 둔다(빌드와 에디터의 씬 번호를 같게 — 이전 버그 방지).</summary>
    private static void AddSceneToBuildList()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
