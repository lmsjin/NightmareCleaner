using UnityEngine;

/// <summary>
/// 미니맵 배경용 "맵 윤곽" 사진. 씬이 열릴 때 한 번만 위에서 찍어 둔다(매 프레임 렌더링 없음).
/// 정식 지도 그림(UI 담당의 ui_map_mansion)이 오면 미니맵이 이 사진 대신 그 그림을 쓰면 된다 — 좌표 범위(Bounds)는 그대로.
/// </summary>
public static class NCMapSnapshot
{
    private const int IgnoreRaycastLayer = 2; // 이름표·빛 원 같은 연출은 찍지 않는다
    private const int UiLayer = 5;
    private const int Width = 1024;

    /// <summary>찍은 사진. 아직 없으면 null.</summary>
    public static RenderTexture Texture { get; private set; }

    /// <summary>사진이 담고 있는 월드 범위(XZ). 미니맵 좌표 변환에 쓴다.</summary>
    public static Bounds Bounds { get; private set; }

    public static bool IsReady => Texture != null;

    /// <summary>맵 루트 아래 모든 렌더러를 감싸는 범위를 위에서 정사영으로 찍는다.</summary>
    public static void Capture(Transform mapRoot)
    {
        Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        // 가장자리가 잘리지 않게 약간 여유
        bounds.Expand(new Vector3(2f, 0f, 2f));
        Bounds = bounds;

        int height = Mathf.Max(64, Mathf.RoundToInt(Width * bounds.size.z / Mathf.Max(0.01f, bounds.size.x)));

        if (Texture != null)
        {
            Texture.Release();
            Object.Destroy(Texture);
        }

        Texture = new RenderTexture(Width, height, 24, RenderTextureFormat.ARGB32) { name = "NC_MinimapSnapshot" };

        GameObject camObject = new GameObject("NC_SnapshotCamera");
        Camera cam = camObject.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = bounds.size.z * 0.5f;
        cam.aspect = (float)Width / height;
        cam.transform.position = new Vector3(bounds.center.x, bounds.max.y + 30f, bounds.center.z);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 화면 위 = 북쪽(+Z), 오른쪽 = +X
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = bounds.size.y + 60f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = ~((1 << IgnoreRaycastLayer) | (1 << UiLayer));
        cam.targetTexture = Texture;
        cam.Render();
        cam.targetTexture = null;
        Object.Destroy(camObject);
    }
}
