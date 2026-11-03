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
///
/// Assets/Map 안의 모든 FBX를 **빌더와 같은 배치(Y 180° 회전 · 배율 · 바닥 높이 0)** 로 놓고 측정한다.
///  - Temp/NC_MapSurvey_파일이름.txt : 조각별 범위(m), 그룹(방 가구 묶음)별 중심
///  - Temp/NC_MapSurvey_파일이름.png : 위에서 본 배치도(바닥=연한색, 벽=진한색, 가구=파랑, 위=북쪽)
/// 이 결과로 빌더의 방·출입구 표를 맞춘다.
/// </summary>
public static class NCMapSurvey
{
    private const string MapFolder = "Assets/Map";
    private const float RotationY = 180f; // NCSceneBuilder.PlaceArtMap 과 같게
    private const float Scale = 1f;       // NCSceneBuilder.ArtScale 과 같게
    private const int ImageSize = 2048;

    [MenuItem("NightmareCleaner/Survey Art Map", priority = 20)]
    public static void Survey()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
        {
            return;
        }

        string previousScene = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { MapFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                SurveyOne(path);
            }
        }

        if (string.IsNullOrEmpty(previousScene) == false)
        {
            EditorSceneManager.OpenScene(previousScene);
        }

        Debug.Log("[NCMapSurvey] 완료: Temp/NC_MapSurvey_*.txt / *.png");
    }

    private static void SurveyOne(string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            return;
        }

        GameObject map = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        map.transform.rotation = Quaternion.Euler(0f, RotationY, 0f);
        map.transform.localScale = Vector3.one * Scale;

        Renderer[] renderers = map.GetComponentsInChildren<Renderer>();
        float floorY = renderers.Where(r => r.name.StartsWith("plan")).Select(r => r.bounds.max.y).DefaultIfEmpty(0f).Min();
        map.transform.position = Vector3.down * floorY;

        Bounds all = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            all.Encapsulate(r.bounds);
        }

        string name = Path.GetFileNameWithoutExtension(path);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"{path}  (Y {RotationY}° 회전, 배율 {Scale}, 바닥 높이 0으로 맞춤)");
        sb.AppendLine($"전체(m): min={F(all.min)} max={F(all.max)} size={F(all.size)}  조각 수={renderers.Length}");
        sb.AppendLine();

        sb.AppendLine("[그룹별 중심]");
        foreach (Transform group in map.transform)
        {
            Renderer[] inGroup = group.GetComponentsInChildren<Renderer>();
            if (inGroup.Length == 0)
            {
                continue;
            }

            Bounds b = inGroup[0].bounds;
            foreach (Renderer r in inGroup)
            {
                b.Encapsulate(r.bounds);
            }

            sb.AppendLine($"  {group.name,-16} 조각 {inGroup.Length,3}  center=({b.center.x:0.00},{b.center.z:0.00})  x[{b.min.x:0.00},{b.max.x:0.00}] z[{b.min.z:0.00},{b.max.z:0.00}]");
        }

        sb.AppendLine();
        sb.AppendLine("[조각별 범위]  x[min,max] z[min,max] h");
        foreach (Renderer r in renderers.OrderBy(r => Kind(r)).ThenBy(r => r.name))
        {
            Bounds b = r.bounds;
            string parent = r.transform.parent != null ? r.transform.parent.name : "-";
            sb.AppendLine($"  {Kind(r),-5} {r.name,-30} ({parent,-14}) x[{b.min.x:0.00},{b.max.x:0.00}] z[{b.min.z:0.00},{b.max.z:0.00}] h={b.size.y:0.00}");
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText($"Temp/NC_MapSurvey_{name}.txt", sb.ToString(), Encoding.UTF8);
        File.WriteAllBytes($"Temp/NC_MapSurvey_{name}.png", Draw(all, renderers));

        Object.DestroyImmediate(map);
    }

    private static string Kind(Renderer r)
    {
        if (r.name.StartsWith("plan") || r.name.StartsWith("pPlane"))
        {
            return "floor";
        }

        return r.name.StartsWith("wall") ? "wall" : "prop";
    }

    /// <summary>각 조각의 범위를 사각형으로 칠한 배치도(카메라 렌더링 없이 직접 그림).</summary>
    private static byte[] Draw(Bounds all, Renderer[] renderers)
    {
        float span = Mathf.Max(all.size.x, all.size.z) * 1.05f;
        Vector2 origin = new Vector2(all.center.x - span * 0.5f, all.center.z - span * 0.5f);

        Texture2D tex = new Texture2D(ImageSize, ImageSize, TextureFormat.RGB24, false);
        Color32[] pixels = Enumerable.Repeat(new Color32(255, 255, 255, 255), ImageSize * ImageSize).ToArray();

        void Fill(Bounds b, Color32 color, bool outline)
        {
            int x0 = Mathf.Clamp((int)((b.min.x - origin.x) / span * ImageSize), 0, ImageSize - 1);
            int x1 = Mathf.Clamp((int)((b.max.x - origin.x) / span * ImageSize), 0, ImageSize - 1);
            int y0 = Mathf.Clamp((int)((b.min.z - origin.y) / span * ImageSize), 0, ImageSize - 1);
            int y1 = Mathf.Clamp((int)((b.max.z - origin.y) / span * ImageSize), 0, ImageSize - 1);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    bool edge = x == x0 || x == x1 || y == y0 || y == y1;
                    if (outline == false || edge)
                    {
                        pixels[y * ImageSize + x] = color;
                    }
                }
            }
        }

        // 바닥(연한색) → 가구(파랑) → 벽(진한색) 순서로 칠한다.
        foreach (Renderer r in renderers.Where(r => Kind(r) == "floor" && r.name != "pPlane14"))
        {
            Fill(r.bounds, new Color32(235, 220, 195, 255), false);
            Fill(r.bounds, new Color32(170, 140, 100, 255), true);
        }

        foreach (Renderer r in renderers.Where(r => Kind(r) == "prop"))
        {
            Fill(r.bounds, new Color32(80, 130, 220, 255), false);
        }

        // 벽은 방 전체를 감싸는 큰 껍데기 조각이 있어 테두리만, 문틀 같은 작은 조각은 칠한다.
        foreach (Renderer r in renderers.Where(r => Kind(r) == "wall"))
        {
            bool shell = r.bounds.size.x > 4f && r.bounds.size.z > 4f;
            Fill(r.bounds, new Color32(60, 40, 70, 255), shell);
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return png;
    }

    private static string F(Vector3 v) => $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";
}
