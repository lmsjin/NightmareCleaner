using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 아트 맵(FBX)을 분석하는 도구. 씬을 바꾸지 않는다(임시 씬에서 측정 후 버림).
/// 메뉴: NightmareCleaner → Survey Art Map
///  - 각 조각(바닥 plan_*, 벽 wall_*)의 월드 크기·위치를 미터 단위로 Temp/NC_MapSurvey.txt 에 기록
///  - 위에서 내려다본 그림(조각 이름 표시)을 Temp/NC_MapSurvey.png 로 저장
/// 이 결과로 전투방·출입구 위치를 아트 맵에 맞춘다.
/// </summary>
public static class NCMapSurvey
{
    private const string MapPath = "Assets/Map/map_dummy_v01.fbx";

    [MenuItem("NightmareCleaner/Survey Art Map", priority = 20)]
    public static void Survey()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
        {
            return;
        }

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(MapPath);
        if (asset == null)
        {
            Debug.LogError("[NCMapSurvey] 맵을 찾지 못했습니다: " + MapPath);
            return;
        }

        string previousScene = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject map = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        Renderer[] renderers = map.GetComponentsInChildren<Renderer>();

        Bounds all = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            all.Encapsulate(r.bounds);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"맵 전체(m): center={F(all.center)} size={F(all.size)} min={F(all.min)} max={F(all.max)}");
        sb.AppendLine($"루트 스케일={F(map.transform.lossyScale)}  조각 수={renderers.Length}");
        sb.AppendLine();

        foreach (Renderer r in renderers.OrderBy(r => r.name))
        {
            Bounds b = r.bounds;
            sb.AppendLine($"{r.name,-10} min=({b.min.x:0.00},{b.min.y:0.00},{b.min.z:0.00}) max=({b.max.x:0.00},{b.max.y:0.00},{b.max.z:0.00}) size=({b.size.x:0.00},{b.size.y:0.00},{b.size.z:0.00})");
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/NC_MapSurvey.txt", sb.ToString(), Encoding.UTF8);

        RenderTopDown(all, renderers);

        Object.DestroyImmediate(map);
        if (string.IsNullOrEmpty(previousScene) == false)
        {
            EditorSceneManager.OpenScene(previousScene);
        }

        Debug.Log("[NCMapSurvey] 완료: Temp/NC_MapSurvey.txt, Temp/NC_MapSurvey.png");
    }

    private static void RenderTopDown(Bounds all, Renderer[] renderers)
    {
        // 바닥은 밝게, 벽은 어둡게 칠하고 이름표를 띄운다.
        Material floor = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.85f, 0.8f, 0.7f) };
        Material wall = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.2f, 0.15f, 0.25f) };
        List<GameObject> temp = new List<GameObject>();

        foreach (Renderer r in renderers)
        {
            bool isWall = r.name.StartsWith("wall");
            r.sharedMaterial = isWall ? wall : floor;

            if (isWall == false)
            {
                GameObject label = new GameObject("Label");
                label.transform.position = r.bounds.center + Vector3.up * (all.size.y + 1f);
                label.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                TextMesh text = label.AddComponent<TextMesh>();
                text.text = r.name + $"\n{r.bounds.size.x:0.0}x{r.bounds.size.z:0.0}";
                text.anchor = TextAnchor.MiddleCenter;
                text.characterSize = Mathf.Max(all.size.x, all.size.z) / 220f;
                text.fontSize = 40;
                text.color = Color.red;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
                temp.Add(label);
            }
        }

        GameObject camObject = new GameObject("SurveyCamera");
        Camera cam = camObject.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(all.size.x, all.size.z) * 0.55f;
        cam.transform.position = all.center + Vector3.up * (all.size.y + 20f);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.white;
        cam.farClipPlane = all.size.y + 100f;

        RenderTexture rt = new RenderTexture(2048, 2048, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(2048, 2048, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 2048, 2048), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Temp/NC_MapSurvey.png", tex.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camObject);
        foreach (GameObject go in temp)
        {
            Object.DestroyImmediate(go);
        }
    }

    private static string F(Vector3 v) => $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";
}
