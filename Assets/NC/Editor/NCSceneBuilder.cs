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
///   아트 맵(Assets/Map/map_dummy_v01.fbx, 180° 회전·0.5배): 현관 → 중앙홀(시작 영역) →
///   전투방 5개(서쪽 갤러리·서쪽 응접실·동쪽 응접실·연회장·창고) + 보급방 3개(집무실·서고·주방)
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
        EditorApplication.ExecuteMenuItem("Tools/Fusion/Rebuild Prefab Table");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SetupCameraAndLight();

        Transform[] entrySpawns = BuildMap(out Transform startZone, out GameObject mapRoot);
        BuildCombatRooms();
        BuildSupplyBoxes();
        BuildSystems(entrySpawns, startZone);

        // 프리팹 참조는 모든 에셋 생성·임포트가 끝난 "맨 마지막"에 경로로 다시 불러와 연결한다.
        // (중간에 프리팹이 다시 임포트되면 먼저 받아 둔 참조가 무효가 되어 빈 칸으로 저장되는 문제 방지)
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AssignPrefabs();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildList();
        VerifyPrefabLinks();

        Debug.Log("[NCSceneBuilder] 완료: " + ScenePath + " — Play 후 [방 만들기]로 시작하세요. " +
                  "혼자 테스트하려면 Assets/NC/Resources/NCBalance 의 Solo Test 를 켜세요.");
    }

    // ───────── 준비 ─────────

    // 프리팹은 Resources에 둔다 → 씬 연결이 비어도 게임이 Resources.Load로 대신 불러올 수 있다.
    private const string PlayerPrefabPath = Root + "/Resources/NC_Player.prefab";
    private const string GhostPrefabPath = Root + "/Resources/NC_Ghost.prefab";

    private static void EnsureFolders()
    {
        foreach (string folder in new[] { "Prefabs", "Materials", "Scenes", "Resources" })
        {
            if (AssetDatabase.IsValidFolder(Root + "/" + folder) == false)
            {
                AssetDatabase.CreateFolder(Root, folder);
            }
        }

        // 예전 위치(Prefabs)의 프리팹은 GUID를 유지한 채 Resources로 옮긴다.
        MoveIfExists(Root + "/Prefabs/NC_Player.prefab", PlayerPrefabPath);
        MoveIfExists(Root + "/Prefabs/NC_Ghost.prefab", GhostPrefabPath);
    }

    private static void MoveIfExists(string from, string to)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(from) != null && AssetDatabase.LoadAssetAtPath<GameObject>(to) == null)
        {
            string error = AssetDatabase.MoveAsset(from, to);
            if (string.IsNullOrEmpty(error) == false)
            {
                Debug.LogWarning("[NCSceneBuilder] 프리팹 이동 실패: " + error);
            }
        }
    }

    private static void AssignPrefabs()
    {
        NetworkObject playerPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(PlayerPrefabPath);
        NetworkObject ghostPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(GhostPrefabPath);

        foreach (NCLauncher launcher in Object.FindObjectsByType<NCLauncher>(FindObjectsSortMode.None))
        {
            Assign(launcher, "playerPrefab", playerPrefab);
        }

        foreach (NCCombatRoom room in Object.FindObjectsByType<NCCombatRoom>(FindObjectsSortMode.None))
        {
            Assign(room, "ghostPrefab", ghostPrefab);
        }
    }

    /// <summary>저장된 씬의 프리팹 연결을 확인한다. 비어 있어도 게임은 Resources에서 불러오지만, 알려는 준다.</summary>
    private static void VerifyPrefabLinks()
    {
        int missing = 0;

        foreach (NCLauncher launcher in Object.FindObjectsByType<NCLauncher>(FindObjectsSortMode.None))
        {
            missing += new SerializedObject(launcher).FindProperty("playerPrefab").objectReferenceValue == null ? 1 : 0;
        }

        foreach (NCCombatRoom room in Object.FindObjectsByType<NCCombatRoom>(FindObjectsSortMode.None))
        {
            missing += new SerializedObject(room).FindProperty("ghostPrefab").objectReferenceValue == null ? 1 : 0;
        }

        if (missing > 0)
        {
            Debug.LogWarning($"[NCSceneBuilder] 프리팹 연결 {missing}곳이 비어 있습니다. 실행 중에는 Resources에서 자동으로 불러옵니다.");
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
        cc.stepOffset = 0.2f;

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

        string path = PlayerPrefabPath;
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

        string path = GhostPrefabPath;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<NetworkObject>();
    }

    // ───────── 씬 ─────────

    private static void SetupCameraAndLight()
    {
        Camera cam = Camera.main;
        cam.transform.position = new Vector3(0f, 13f, -18f);
        cam.transform.LookAt(new Vector3(0f, 0.8f, -9.5f));
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

    // ───────── 맵 데이터 (아트 맵 map_dummy_v01 기준) ─────────
    //
    // 아트 맵은 배치도와 남북이 반대로 내보내져 있어 Y축 180° 회전, 기획서 기준(1유닛=1m)에 맞춰 0.5배로 쓴다.
    // 아래 좌표는 회전·축소한 뒤의 월드 좌표(m)다. (Survey Art Map 측정값에서 계산)
    //
    //   [A-1]   [ 연회장 ]  [주방]  [창고]
    //       \       │        │      │(동쪽 복도)
    //  [서쪽 응접실]─[서쪽 갤러리]─[ 중앙홀 ]─[동쪽 응접실]
    //                                │            │
    //     [집무실]  [  ]  [ 현관(시작) ]  [  ]  [서고]

    private const string ArtMapPath = "Assets/Map/map_dummy_v01.fbx";

    // 아트 맵 배율. 1 = 아트 원래 크기(문 약 4.2m, 벽 약 6.3m).
    private const float ArtScale = 1f;

    // 아래 방·출입구 표는 0.5배로 놓고 측정한 값이다. 배율을 바꾸면 표가 자동으로 같은 비율로 커진다.
    private const float MeasuredScale = 0.5f;
    private static float K => ArtScale / MeasuredScale;

    private enum AreaKind { Entrance, Hall, Combat, Supply, Empty }

    private class Area
    {
        public string Name;
        public Rect MeasuredRect; // 0.5배 기준 측정값. x = 월드 X, y = 월드 Z
        public Rect Rect => new Rect(MeasuredRect.x * K, MeasuredRect.y * K, MeasuredRect.width * K, MeasuredRect.height * K);
        public AreaKind Kind;
        public int[,] Waves; // 차수별 {기본형, 빠른형}
        public int MaxAlive = 4;

        public Vector3 Center => new Vector3(Rect.center.x, 0f, Rect.center.y);
    }

    /// <summary>출입구. 두 구역 사이의 문 자리(문틀 벽 조각 쌍 사이). AlongX = true면 가로 방향(문이 X축으로 벌어짐).</summary>
    private struct Door
    {
        public string A;
        public string B;
        public Vector3 MeasuredCenter; // 0.5배 기준 측정값
        public bool AlongX;
        public float MeasuredWidth;

        public Door(string a, string b, float x, float z, bool alongX, float width = 2.09f)
        {
            A = a;
            B = b;
            MeasuredCenter = new Vector3(x, 0f, z);
            AlongX = alongX;
            MeasuredWidth = width;
        }

        public Vector3 Center => MeasuredCenter * K;
        public float Width => MeasuredWidth * K;

        public bool Touches(string area) => A == area || B == area;
    }

    private static readonly Area[] Areas =
    {
        new Area { Name = "현관", MeasuredRect = new Rect(-2.61f, -12.02f, 5.22f, 6.27f), Kind = AreaKind.Entrance },
        new Area { Name = "중앙홀", MeasuredRect = new Rect(-6.79f, -4.70f, 13.58f, 10.45f), Kind = AreaKind.Hall },
        new Area { Name = "서쪽 갤러리", MeasuredRect = new Rect(-17.24f, -2.61f, 9.41f, 7.31f), Kind = AreaKind.Combat, Waves = new[,] { { 3, 1 }, { 3, 2 } } },
        new Area { Name = "서쪽 응접실", MeasuredRect = new Rect(-25.60f, -3.66f, 7.31f, 8.36f), Kind = AreaKind.Combat, Waves = new[,] { { 2, 2 }, { 3, 2 } } },
        new Area { Name = "동쪽 응접실", MeasuredRect = new Rect(7.84f, -3.66f, 7.31f, 7.31f), Kind = AreaKind.Combat, Waves = new[,] { { 3, 1 }, { 3, 2 } } },
        new Area { Name = "연회장", MeasuredRect = new Rect(-4.70f, 6.79f, 9.40f, 7.32f), Kind = AreaKind.Combat, Waves = new[,] { { 4, 2 }, { 4, 3 } }, MaxAlive = 6 },
        new Area { Name = "창고", MeasuredRect = new Rect(14.11f, 1.57f, 7.31f, 9.40f), Kind = AreaKind.Combat, Waves = new[,] { { 3, 1 }, { 2, 3 } } },
        new Area { Name = "집무실", MeasuredRect = new Rect(-17.24f, -9.93f, 7.31f, 6.27f), Kind = AreaKind.Supply },
        new Area { Name = "서고", MeasuredRect = new Rect(9.93f, -10.97f, 10.45f, 9.40f), Kind = AreaKind.Supply },
        new Area { Name = "주방", MeasuredRect = new Rect(5.75f, 4.70f, 7.31f, 6.27f), Kind = AreaKind.Supply },
        new Area { Name = "A-1", MeasuredRect = new Rect(-14.11f, 5.75f, 8.36f, 6.27f), Kind = AreaKind.Empty },
        new Area { Name = "현관 서쪽 방", MeasuredRect = new Rect(-8.88f, -9.93f, 5.23f, 6.27f), Kind = AreaKind.Empty },
        new Area { Name = "현관 동쪽 방", MeasuredRect = new Rect(3.66f, -9.93f, 5.22f, 7.32f), Kind = AreaKind.Empty },
    };

    private static readonly Door[] Doors =
    {
        new Door("중앙홀", "현관", 0f, -5.22f, true, 3.13f),
        new Door("중앙홀", "연회장", 0f, 6.27f, true, 3.13f),
        new Door("중앙홀", "서쪽 갤러리", -7.31f, 0.52f, false),
        new Door("중앙홀", "동쪽 응접실", 7.31f, 0.52f, false),
        new Door("서쪽 갤러리", "서쪽 응접실", -17.76f, 0.52f, false),
        new Door("동쪽 응접실", "서고", 12.02f, -4.18f, true),
        new Door("동쪽 응접실", "주방", 9.93f, 4.18f, true),
        new Door("주방", "창고", 13.58f, 6.79f, false),
        new Door("연회장", "A-1", -5.22f, 8.88f, false),
        new Door("연회장", "주방", 5.22f, 8.88f, false),
        new Door("창고", "서고", 18.28f, 1.57f, true),           // 동쪽 복도 북쪽 끝
        new Door("집무실", "현관 서쪽 방", -9.40f, -5.75f, false),
        new Door("현관 동쪽 방", "서고", 9.40f, -6.79f, false),
    };

    // 카메라가 내려다보는 각도(이름표가 카메라를 향하도록)
    private static readonly Quaternion LabelRotation = Quaternion.Euler(57f, 0f, 0f);

    private static Transform[] BuildMap(out Transform startZone, out GameObject mapRoot)
    {
        mapRoot = new GameObject("Map");
        Transform map = mapRoot.transform;

        NavMeshSurface surface = mapRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = ~(1 << IgnoreRaycastLayer);
        mapRoot.AddComponent<NCNavBaker>();

        PlaceArtMap(map);

        foreach (Area area in Areas)
        {
            // 방 크기에 맞춘 촛불 조명
            if (area.Kind == AreaKind.Combat || area.Kind == AreaKind.Hall)
            {
                float dx = area.Rect.width * 0.28f;
                float dz = area.Rect.height * 0.28f;
                foreach (Vector2 c in new[] { new Vector2(-dx, -dz), new Vector2(dx, -dz), new Vector2(-dx, dz), new Vector2(dx, dz) })
                {
                    Candle(map, area.Center + new Vector3(c.x, 2.4f, c.y), 8f);
                }
            }
            else
            {
                Candle(map, area.Center + Vector3.up * 2.4f, 7f);
            }
        }

        // 중앙홀 시작 영역 표시(빛나는 원)
        Area hall = System.Array.Find(Areas, a => a.Kind == AreaKind.Hall);
        GameObject zone = new GameObject("StartZone");
        zone.transform.SetParent(map, false);
        zone.transform.localPosition = hall.Center + Vector3.up * 0.03f;
        float radius = NCBalance.Get.startZoneRadius;
        Shape(PrimitiveType.Cylinder, "Glow", zone.transform, Vector3.zero, new Vector3(radius * 2f, 0.01f, radius * 2f),
            Mat("NC_StartGlow", NCStyle.Hex("58B8EA", 0.18f), transparent: true));
        Shape(PrimitiveType.Cylinder, "Ring", zone.transform, Vector3.up * -0.005f, new Vector3(radius * 2f + 0.25f, 0.005f, radius * 2f + 0.25f),
            Mat("NC_StartRing", NCStyle.Hex("B6955E", 0.7f), transparent: true));
        SetLayerRecursive(zone, IgnoreRaycastLayer);
        startZone = zone.transform;

        // 현관 1P·2P 시작 위치(시작지점)
        Area entrance = System.Array.Find(Areas, a => a.Kind == AreaKind.Entrance);
        Vector3 entry = entrance.Center + new Vector3(0f, 0.1f, -1.5f);
        Transform spawn1 = Marker("EntrySpawn_1P", map, entry + Vector3.left * 0.8f);
        Transform spawn2 = Marker("EntrySpawn_2P", map, entry + Vector3.right * 0.8f);
        return new[] { spawn1, spawn2 };
    }

    /// <summary>
    /// 아트 맵을 놓는다: 180° 회전 · 0.5배 · 바닥 높이를 0에 맞춤 · 모든 조각에 메시 콜라이더 · 임시 재질.
    /// (아트 담당이 재질을 입히면 재질 지정 부분만 빼면 된다)
    /// </summary>
    private static void PlaceArtMap(Transform map)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtMapPath);
        if (asset == null)
        {
            Debug.LogError("[NCSceneBuilder] 아트 맵을 찾지 못했습니다: " + ArtMapPath);
            return;
        }

        GameObject art = (GameObject)PrefabUtility.InstantiatePrefab(asset, map);
        PrefabUtility.UnpackPrefabInstance(art, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        art.name = "ArtMap";
        art.transform.localPosition = Vector3.zero;
        art.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        art.transform.localScale = Vector3.one * ArtScale;

        // 바닥(plan_*) 윗면이 y = 0이 되도록 내린다.
        float floorY = float.MaxValue;
        foreach (Renderer r in art.GetComponentsInChildren<Renderer>())
        {
            if (r.name.StartsWith("plan"))
            {
                floorY = Mathf.Min(floorY, r.bounds.max.y);
            }
        }

        if (floorY < float.MaxValue)
        {
            art.transform.localPosition = Vector3.down * floorY;
        }

        Material floor = Mat("NC_FloorWood", NCStyle.Hex("7A5236"));
        Material ground = Mat("NC_FloorStone", NCStyle.Hex("4A4038"));
        Material wall = Mat("NC_Wall", NCStyle.Hex("3A2E3E"));

        foreach (MeshFilter filter in art.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;

            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            string name = filter.name;
            Material material = name.StartsWith("plan") ? floor : (name == "pPlane14" ? ground : wall);
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
        }
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

    /// <summary>방 위에 떠 있는 3D 이름표(카메라를 향해 기울임, 연출 레이어).</summary>
    private static TextMeshPro WorldLabel(string text, Transform parent, Vector3 position, Color color)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = LabelRotation;
        go.layer = IgnoreRaycastLayer;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null)
        {
            tmp.font = font;
        }

        tmp.text = text;
        tmp.fontSize = 7f;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.rectTransform.sizeDelta = new Vector2(12f, 4f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    // ───────── 보급함 ─────────

    // 보급방 ← 해금 전투방(OR). UI 기획서 16장.
    private static readonly (string Box, string[] Rooms)[] SupplyLinks =
    {
        ("집무실", new[] { "서쪽 갤러리", "서쪽 응접실" }),
        ("서고", new[] { "동쪽 응접실" }),
        ("주방", new[] { "연회장", "창고" }),
    };

    private static void BuildSupplyBoxes()
    {
        Material wood = Mat("NC_Chest", NCStyle.Hex("6B4226"));
        Material brass = Mat("NC_Brass", NCStyle.Hex("B6955E"));
        Material glowMat = Mat("NC_SupplyGlow", NCStyle.Hex("F2C34E", 0.25f), transparent: true);

        foreach ((string boxName, string[] roomNames) in SupplyLinks)
        {
            Area area = System.Array.Find(Areas, a => a.Name == boxName);
            if (area == null)
            {
                continue;
            }

            GameObject boxObject = new GameObject("Supply_" + boxName);
            boxObject.transform.position = area.Center + new Vector3(-area.Rect.width * 0.15f, 0f, 0f);
            boxObject.AddComponent<NetworkObject>();
            NCSupplyBox box = boxObject.AddComponent<NCSupplyBox>();
            Transform b = boxObject.transform;

            // 상자 본체 + 황동 띠 + 뚜껑(뒤쪽 모서리를 축으로 열림)
            Box("Body", b, new Vector3(0f, 0.32f, 0f), new Vector3(1.2f, 0.64f, 0.8f), wood);
            Box("Band", b, new Vector3(0f, 0.32f, 0f), new Vector3(1.22f, 0.1f, 0.82f), brass, false);
            Transform lidPivot = new GameObject("LidPivot").transform;
            lidPivot.SetParent(b, false);
            lidPivot.localPosition = new Vector3(0f, 0.64f, -0.4f);
            Box("Lid", lidPivot, new Vector3(0f, 0.07f, 0.4f), new Vector3(1.24f, 0.14f, 0.84f), wood, false);
            Box("Lock", lidPivot, new Vector3(0f, 0.02f, 0.83f), new Vector3(0.14f, 0.16f, 0.04f), brass, false);

            GameObject glow = Shape(PrimitiveType.Cylinder, "Glow", b, new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.005f, 2.2f), glowMat);
            glow.layer = IgnoreRaycastLayer;
            glow.SetActive(false);

            TextMeshPro label = WorldLabel(boxName, b, boxObject.transform.position + Vector3.up * 2.4f, NCStyle.TextDim);
            label.fontSize = 5.5f;
            label.rectTransform.sizeDelta = new Vector2(14f, 4f);

            List<Object> rooms = new List<Object>();
            foreach (string roomName in roomNames)
            {
                GameObject roomObject = GameObject.Find("Room_" + roomName);
                if (roomObject != null)
                {
                    rooms.Add(roomObject.GetComponent<NCCombatRoom>());
                }
            }

            SerializedObject so = new SerializedObject(box);
            so.FindProperty("boxName").stringValue = boxName;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssignArray(box, "unlockRooms", rooms.ToArray());
            Assign(box, "label", label);
            Assign(box, "lid", lidPivot);
            Assign(box, "glow", glow);
        }
    }

    // ───────── 전투방 ─────────

    private static void BuildCombatRooms()
    {
        Material crate = Mat("NC_Crate", NCStyle.Hex("A0703F"));
        Material bookA = Mat("NC_BookBlue", NCStyle.Hex("3E5E8A"));
        Material bookB = Mat("NC_BookRed", NCStyle.Hex("8A3E46"));
        Material hint = Mat("NC_DummyHint", NCStyle.Hex("F2C34E", 0.18f), transparent: true);
        Material barrierMat = Mat("NC_Barrier", NCStyle.Hex("58B8EA", 0.38f), transparent: true);

        foreach (Area area in Areas)
        {
            if (area.Kind != AreaKind.Combat)
            {
                continue;
            }

            GameObject roomObject = new GameObject("Room_" + area.Name);
            roomObject.transform.position = area.Center;
            roomObject.AddComponent<NetworkObject>();
            NCCombatRoom room = roomObject.AddComponent<NCCombatRoom>();
            Transform r = roomObject.transform;

            // 방 영역(트리거)
            GameObject areaObject = new GameObject("Area");
            areaObject.transform.SetParent(r, false);
            areaObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            areaObject.layer = IgnoreRaycastLayer;
            BoxCollider areaBox = areaObject.AddComponent<BoxCollider>();
            areaBox.isTrigger = true;
            areaBox.size = new Vector3(area.Rect.width - 0.4f, 3f, area.Rect.height - 0.4f);

            // 잡동사니 더미(방 가운데)
            GameObject dummy = new GameObject("Dummy");
            dummy.transform.SetParent(r, false);
            dummy.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            Box("Crate", dummy.transform, new Vector3(-0.3f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), crate, false);
            Box("Crate", dummy.transform, new Vector3(0.35f, 0.25f, 0.2f), new Vector3(0.5f, 0.5f, 0.5f), crate, false);

            // 상자 하나하나에 콜라이더를 두면 캐릭터가 턱처럼 올라탄다 → 더미 전체를 감싸는 높은 벽 하나로 막는다.
            BoxCollider block = dummy.AddComponent<BoxCollider>();
            block.center = new Vector3(0f, 1f, 0.1f);
            block.size = new Vector3(1.4f, 2f, 1f);
            Box("Crate", dummy.transform, new Vector3(-0.1f, 0.75f, 0.05f), new Vector3(0.4f, 0.3f, 0.4f), crate, false);
            Box("Books", dummy.transform, new Vector3(0.35f, 0.58f, 0.2f), new Vector3(0.4f, 0.16f, 0.3f), bookA, false);
            Box("Books", dummy.transform, new Vector3(0.32f, 0.72f, 0.18f), new Vector3(0.36f, 0.12f, 0.28f), bookB, false);
            Shape(PrimitiveType.Cylinder, "Hint", dummy.transform, new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.005f, 2.2f), hint);
            SetLayerRecursive(dummy, IgnoreRaycastLayer);

            // 이 방에 닿은 모든 출입구에 봉쇄 장벽 + 봉쇄 영역
            List<Object> barriers = new List<Object>();
            List<Object> zones = new List<Object>();
            List<Vector3> doorCenters = new List<Vector3>();
            int index = 0;

            foreach (Door door in Doors)
            {
                if (door.Touches(area.Name) == false)
                {
                    continue;
                }

                Vector3 local = door.Center - area.Center;
                doorCenters.Add(door.Center);

                // 장벽 높이는 벽 높이(약 3.1m × 배율)에 맞춘다.
                float barrierHeight = 3.2f * K;
                Vector3 barrierSize = door.AlongX ? new Vector3(door.Width + 0.1f, barrierHeight, 0.2f) : new Vector3(0.2f, barrierHeight, door.Width + 0.1f);
                Vector3 zoneSize = door.AlongX ? new Vector3(door.Width + 0.2f, 2f, 1.8f) : new Vector3(1.8f, 2f, door.Width + 0.2f);

                barriers.Add(Barrier("Barrier_" + index, r, local + Vector3.up * (barrierHeight * 0.5f), barrierSize, barrierMat));
                zones.Add(SealZone("SealZone_" + index, r, local + Vector3.up, zoneSize));
                index++;
            }

            // 출현 지점: 방 안쪽 네 점(팔각형 방의 모서리 벽을 피하도록 중심에서 28%) 중 출입구에서 가장 먼 3곳
            float sx = area.Rect.width * 0.28f;
            float sz = area.Rect.height * 0.28f;
            List<Vector3> corners = new List<Vector3>
            {
                area.Center + new Vector3(-sx, 0f, -sz),
                area.Center + new Vector3(sx, 0f, -sz),
                area.Center + new Vector3(-sx, 0f, sz),
                area.Center + new Vector3(sx, 0f, sz),
            };

            corners.Sort((a, b) => NearestDoor(b, doorCenters).CompareTo(NearestDoor(a, doorCenters)));

            Object[] spawnPoints = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                spawnPoints[i] = Marker("SpawnPoint_" + (i + 1), r, corners[i] - area.Center + Vector3.up * 0.1f);
            }

            TextMeshPro label = WorldLabel(area.Name, r, area.Center + Vector3.up * 2.8f + Vector3.forward * (area.Rect.height * 0.25f), NCStyle.Text);

            Assign(room, "dummy", dummy.transform);
            Assign(room, "area", areaBox);
            Assign(room, "label", label);
            AssignArray(room, "sealZones", zones.ToArray());
            AssignArray(room, "barriers", barriers.ToArray());
            AssignArray(room, "spawnPoints", spawnPoints);
            SetRoomData(room, area);
        }
    }

    private static float NearestDoor(Vector3 point, List<Vector3> doors)
    {
        float best = float.MaxValue;
        foreach (Vector3 door in doors)
        {
            best = Mathf.Min(best, Vector3.Distance(point, door));
        }

        return best;
    }

    private static void SetRoomData(NCCombatRoom room, Area area)
    {
        SerializedObject so = new SerializedObject(room);
        so.FindProperty("roomName").stringValue = area.Name;
        so.FindProperty("maxAlive").intValue = area.MaxAlive;

        SerializedProperty waves = so.FindProperty("waves");
        int count = area.Waves.GetLength(0);
        waves.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            SerializedProperty wave = waves.GetArrayElementAtIndex(i);
            wave.FindPropertyRelative("basic").intValue = area.Waves[i, 0];
            wave.FindPropertyRelative("fast").intValue = area.Waves[i, 1];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
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

    private static void BuildSystems(Transform[] entrySpawns, Transform startZone)
    {
        GameObject manager = new GameObject("NetworkManager");
        NCLauncher launcher = manager.AddComponent<NCLauncher>();
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
