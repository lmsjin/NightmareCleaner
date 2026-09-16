using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using TMPro;

// 좌측 상단 미니맵 — 플레이어 중심. 실제 맵 지형은 탑다운 직교 카메라를 RenderTexture로 찍어 배경에 깔고,
// 그 위에 아이·적 위치를 점(블립)으로 실시간 표시한다(코드 생성, self-wiring).
//  · 흰 화살표 = 플레이어(바라보는 방향으로 회전)
//  · 빨강 점   = 유령(GhostBase)
//  · 노랑 점   = 아이(RescueChild)
// 카메라 직교크기를 점 매핑과 같은 스케일로 맞춰 지형·점이 자동 정렬된다(수동 튜닝 불필요).
[ExecuteAlways]
public class MinimapController : MonoBehaviour
{
    private BalanceConfig _cfg;
    private Transform     _playerTf;

    [Header("Editable Scene UI")]
    [SerializeField] private GameObject minimapRoot;
    [SerializeField] private RectTransform _view;        // 점이 놓이는 중심 기준 영역(패널)
    [SerializeField] private RectTransform _playerArrow; // 중앙 플레이어 마커
    [SerializeField] private RawImage mapImage;
    private float _viewRadiusPx;        // 중심→가장자리 픽셀(점 클램프 반경)
    private float _panelHalfPx;         // 패널 반폭(px) — 카메라 직교크기 정렬용
    private float _worldRadius;         // 가장자리에 해당하는 월드 거리

    private Camera        _mapCam;      // 탑다운 지형 카메라
    private RenderTexture _mapRT;       // 카메라 출력
    private float         _camHeight;   // 플레이어 위로 떨어진 카메라 높이
    private Sprite        _circleSprite; // 코드 생성 원(마커 등 공용)

    // 전체 지도(M 토글) — 불투명 전체화면, 맵 전체 + 플레이어 마커만(적 없음), 일시정지
    private bool          _mapOpen;
    private float         _prevTimeScale;
    private Camera        _fullCam;
    private RenderTexture _fullRT;
    [SerializeField] private GameObject _fullRoot;
    [SerializeField] private RawImage _fullImage;
    [SerializeField] private RectTransform _fullPlayerMarker;
    private Vector3       _mapCenter;     // 맵 전체 중심(월드)
    private float         _mapHalfExtent; // 맵 전체 반경(월드)
    private bool          _boundsReady;
    [SerializeField] private List<Image> _fullChildDots = new();

    [SerializeField] private List<Image> _ghostDots = new();
    [SerializeField] private List<Image> _childDots = new();

    private GhostBase[]   _ghosts;
    private RescueChild[] _children;
    private float _findTimer;

    private static readonly Color ColGhost = new Color(1f, 0.35f, 0.30f, 1f);
    private static readonly Color ColChild = new Color(1f, 0.85f, 0.30f, 1f);

    void Awake()
    {
        _cfg = BalanceConfig.Resolve(null);
        _worldRadius = _cfg != null ? Mathf.Max(1f, _cfg.minimapWorldRadius) : 18f;
        _camHeight   = _cfg != null ? Mathf.Max(1f, _cfg.minimapCameraHeight) : 40f;
        if (!Application.isPlaying) return;
        if (minimapRoot == null || _view == null || mapImage == null)
            BuildUI();
        else
            InitializeRuntimeRenderTargets();
    }

#if UNITY_EDITOR
    [ContextMenu("Setup Editable Minimap UI")]
    public void SetupEditableUI()
    {
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid()) return;
        _cfg = BalanceConfig.Resolve(null);
        _worldRadius = _cfg != null ? Mathf.Max(1f, _cfg.minimapWorldRadius) : 18f;
        _camHeight = _cfg != null ? Mathf.Max(1f, _cfg.minimapCameraHeight) : 40f;
        if (minimapRoot == null) BuildUI();
        SetupEditableDotPools();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    private void SetupEditableDotPools()
    {
        const int poolSize = 24;
        while (_ghostDots.Count < poolSize)
        {
            Image dot = MakeDot(_ghostDots, ColGhost);
            dot.name = $"GhostDot_{_ghostDots.Count:00}";
            dot.gameObject.SetActive(false);
        }
        while (_childDots.Count < poolSize)
        {
            Image dot = MakeDot(_childDots, ColChild);
            dot.name = $"ChildDot_{_childDots.Count:00}";
            dot.gameObject.SetActive(false);
        }
        while (_fullChildDots.Count < poolSize)
        {
            Image dot = MakeFullChildDot();
            dot.name = $"FullMapChildDot_{_fullChildDots.Count:00}";
            dot.gameObject.SetActive(false);
        }
    }
#endif

    private void InitializeRuntimeRenderTargets()
    {
        float panelSize = _view != null && _view.rect.width > 0f ? _view.rect.width : 240f;
        _panelHalfPx = panelSize * 0.5f;
        _viewRadiusPx = Mathf.Max(1f, _panelHalfPx - 15f);
        _circleSprite = MakeCircleSprite(128);
        RestoreRuntimeSprites();
        if (minimapRoot != null) minimapRoot.SetActive(true);

        _mapRT = new RenderTexture(256, 256, 16) { name = "MinimapRT" };
        _mapRT.Create();
        mapImage.texture = _mapRT;
        _mapCam = transform.Find("MinimapCamera")?.GetComponent<Camera>();
        if (_mapCam == null) CreateMapCamera();
        _mapCam.targetTexture = _mapRT;
        _mapCam.enabled = true;
        _mapCam.orthographic = true;
        _mapCam.clearFlags = CameraClearFlags.SolidColor;
        _mapCam.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
        _mapCam.cullingMask = BuildCullingMask();

        _fullRT = new RenderTexture(512, 512, 16) { name = "FullMapRT" };
        _fullRT.Create();
        if (_fullImage != null) _fullImage.texture = _fullRT;
        _fullCam = transform.Find("FullMapCamera")?.GetComponent<Camera>();
        if (_fullCam != null)
        {
            _fullCam.targetTexture = _fullRT;
            _fullCam.enabled = false;
        }
        if (_fullRoot != null) _fullRoot.SetActive(false);
    }

    private void RestoreRuntimeSprites()
    {
        if (_view != null && _view.TryGetComponent(out Image viewImage))
            viewImage.sprite = _circleSprite;
        if (_playerArrow != null && _playerArrow.TryGetComponent(out Image playerImage))
            playerImage.sprite = _circleSprite;
        if (_fullPlayerMarker != null && _fullPlayerMarker.TryGetComponent(out Image fullPlayerImage))
            fullPlayerImage.sprite = _circleSprite;

        foreach (Image dot in _ghostDots)
            if (dot != null) dot.sprite = _circleSprite;
        foreach (Image dot in _childDots)
            if (dot != null) dot.sprite = _circleSprite;
        foreach (Image dot in _fullChildDots)
            if (dot != null) dot.sprite = _circleSprite;
    }

    void Start()
    {
        var ps = FindFirstObjectByType<PlayerStats>();
        if (ps != null) _playerTf = ps.transform;
        if (_playerTf == null)
            Debug.LogError("[MinimapController] PlayerStats를 찾지 못했습니다.");
    }

    void Update()
    {
        if (_playerTf == null)
        {
            PlayerStats stats = FindFirstObjectByType<PlayerStats>();
            if (stats == null) return;
            _playerTf = stats.transform;
        }

        if (_mapCam == null || mapImage == null)
        {
            InitializeRuntimeRenderTargets();
            if (_mapCam == null || mapImage == null) return;
        }

        // 존재 목록은 자주 안 바뀌므로 주기적으로만 다시 찾고, 위치는 매 프레임 갱신
        _findTimer -= Time.unscaledDeltaTime;
        if (_ghosts == null || _findTimer <= 0f)
        {
            _findTimer = 0.2f;
            _ghosts   = FindObjectsByType<GhostBase>(FindObjectsSortMode.None);
            _children = FindObjectsByType<RescueChild>(FindObjectsSortMode.None);
        }

        // M = 전체 지도 토글
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            ToggleFullMap();
        if (_mapOpen) { UpdateFullChildDots(); UpdateFullPlayerMarker(); }

        UpdateMapCamera();
        UpdatePlayerArrow();
        PlaceDots(_ghosts,   _ghostDots, ColGhost);
        PlaceDots(_children, _childDots, ColChild);
    }

    // ---------- 전체 지도(M) ----------
    void ToggleFullMap()
    {
        if (_mapOpen) CloseFullMap(); else OpenFullMap();
    }

    void OpenFullMap()
    {
        EnsureFullBounds();
        if (_fullCam == null) CreateFullCam();
        _fullCam.targetTexture = _fullRT;
        ConfigureFullCam();
        _fullCam.enabled = true;
        if (_fullRoot != null) _fullRoot.SetActive(true);
        UpdateFullPlayerMarker();

        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;   // 보는 동안 일시정지
        _mapOpen = true;
    }

    void CloseFullMap()
    {
        _mapOpen = false;
        if (_fullCam != null)  _fullCam.enabled = false;
        if (_fullRoot != null) _fullRoot.SetActive(false);
        Time.timeScale = _prevTimeScale; // 열기 전 상태로 복원
    }

    // 맵 전체 범위를 NavMesh로 자동 계산(1회 캐시)
    void EnsureFullBounds()
    {
        if (_boundsReady) return;
        _boundsReady = true;

        var tri = NavMesh.CalculateTriangulation();
        if (tri.vertices != null && tri.vertices.Length > 0)
        {
            Vector3 min = tri.vertices[0], max = tri.vertices[0];
            foreach (var v in tri.vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
            _mapCenter     = (min + max) * 0.5f;
            float half     = Mathf.Max(max.x - min.x, max.z - min.z) * 0.5f;
            _mapHalfExtent = Mathf.Max(1f, half * 1.1f); // 약간 여백
        }
        else
        {
            // NavMesh 없으면 플레이어 주변 폴백
            _mapCenter     = _playerTf != null ? _playerTf.position : Vector3.zero;
            _mapHalfExtent = _worldRadius * 2f;
        }
    }

    void CreateFullCam()
    {
        var camGO = new GameObject("FullMapCamera");
        camGO.transform.SetParent(transform, false);
        _fullCam = camGO.AddComponent<Camera>();
        _fullCam.orthographic   = true;
        _fullCam.targetTexture  = _fullRT;
        _fullCam.clearFlags      = CameraClearFlags.SolidColor;
        _fullCam.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
        _fullCam.allowHDR  = false;
        _fullCam.allowMSAA = false;
        _fullCam.cullingMask = BuildCullingMask(); // 유령·UI 제외 → 적 안 보임
        _fullCam.enabled   = false;
    }

    void ConfigureFullCam()
    {
        const float h = 200f; // 충분히 높은 곳에서 내려다봄(직교라 스케일 무관)
        _fullCam.transform.SetPositionAndRotation(
            new Vector3(_mapCenter.x, _mapCenter.y + h, _mapCenter.z),
            Quaternion.Euler(90f, 0f, 0f));
        _fullCam.nearClipPlane    = 0.1f;
        _fullCam.farClipPlane     = h + 100f;
        _fullCam.orthographicSize = _mapHalfExtent;
    }

    void UpdateFullPlayerMarker()
    {
        if (_fullPlayerMarker == null || _playerTf == null || _fullImage == null) return;
        float side  = _fullImage.rectTransform.sizeDelta.x;     // 정사각
        float scale = side / (2f * _mapHalfExtent);             // px per world
        Vector3 off = _playerTf.position - _mapCenter;
        _fullPlayerMarker.anchoredPosition = new Vector2(off.x, off.z) * scale; // 북쪽 위
        Vector3 f = _playerTf.forward;
        _fullPlayerMarker.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-f.x, f.z) * Mathf.Rad2Deg);
        _fullPlayerMarker.SetAsLastSibling(); // 아이 점 위로
    }

    // 전체 지도 위에 아이 위치를 큰 노란 점으로 표시(전체맵 전용)
    void UpdateFullChildDots()
    {
        if (_fullImage == null) return;
        float side  = _fullImage.rectTransform.sizeDelta.x;
        float scale = side / (2f * _mapHalfExtent);

        int used = 0;
        if (_children != null)
        {
            foreach (var c in _children)
            {
                if (c == null) continue;
                Vector3 off = c.transform.position - _mapCenter;
                if (used >= _fullChildDots.Count) break;
                Image dot = _fullChildDots[used];
                dot.rectTransform.anchoredPosition = new Vector2(off.x, off.z) * scale; // 북쪽 위
                dot.gameObject.SetActive(true);
                used++;
            }
        }
        for (int i = used; i < _fullChildDots.Count; i++) _fullChildDots[i].gameObject.SetActive(false);
    }

    Image MakeFullChildDot()
    {
        var go = new GameObject("FullChildDot", typeof(Image));
        go.transform.SetParent(_fullImage.rectTransform, false);
        var img = go.GetComponent<Image>();
        img.sprite = _circleSprite;
        img.color  = ColChild;
        img.raycastTarget = false;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(20f, 20f); // 전체맵 전용 큰 점
        _fullChildDots.Add(img);
        return img;
    }

    // 탑다운 카메라를 플레이어 위로 따라가게 + 직교크기를 점 매핑과 같은 스케일로 (자동 정렬)
    void UpdateMapCamera()
    {
        if (_mapCam == null) return;
        Vector3 p = _playerTf.position;
        _mapCam.transform.SetPositionAndRotation(
            new Vector3(p.x, p.y + _camHeight, p.z),
            Quaternion.Euler(90f, 0f, 0f)); // 똑바로 아래 보기 → RT 위쪽=월드 +Z(북)

        // 점 스케일: viewRadiusPx px = worldRadius. 배경 RawImage는 패널 전체(panelHalfPx)이므로
        // 같은 px/월드 비율이 되도록 직교크기(월드 반높이)를 맞춤.
        float scale = _viewRadiusPx / _worldRadius;       // px per world
        _mapCam.orthographicSize = _panelHalfPx / scale;  // = panelHalf * worldRadius / viewRadiusPx
    }

    // 플레이어가 바라보는 방향으로 중앙 화살표 회전 (미니맵 위=월드 +Z)
    void UpdatePlayerArrow()
    {
        if (_playerArrow == null) return;
        Vector3 f = _playerTf.forward;
        // 위(0,1) 화살표를 (fx,fz) 방향으로 돌리는 각: atan2(-fx, fz)
        float deg = Mathf.Atan2(-f.x, f.z) * Mathf.Rad2Deg;
        _playerArrow.localRotation = Quaternion.Euler(0f, 0f, deg);
    }

    // 대상들을 점으로 배치(없으면 풀 점 숨김). Component[] 공변성으로 Ghost/Child 공용.
    void PlaceDots(Component[] items, List<Image> pool, Color color)
    {
        float scale = _viewRadiusPx / _worldRadius;
        int used = 0;

        if (items != null)
        {
            foreach (var it in items)
            {
                if (it == null) continue;
                Vector3 off = it.transform.position - _playerTf.position;
                Vector2 p = new Vector2(off.x, off.z) * scale;          // 월드 X→우, Z→상
                if (p.magnitude > _viewRadiusPx) p = p.normalized * _viewRadiusPx; // 범위 밖=가장자리

                if (used >= pool.Count) break;
                Image dot = pool[used];
                dot.color = color;
                dot.rectTransform.anchoredPosition = p;
                dot.gameObject.SetActive(true);
                used++;
            }
        }
        for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
    }

    // ---------- UI 코드 생성 ----------
    void BuildUI()
    {
        var canvasGO = new GameObject("MinimapCanvas",
            typeof(Canvas), typeof(CanvasScaler)); // 상호작용 없음 → Raycaster 불필요
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70; // HUD 위, 팝업(미니게임90/능력100) 아래
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        var rt = canvasGO.GetComponent<RectTransform>();
        minimapRoot = canvasGO;

        const float panel = 240f;
        _panelHalfPx  = panel * 0.5f;
        _viewRadiusPx = _panelHalfPx - 15f; // 가장자리 여유(점 반쯤 안 잘리게)

        // 원형 스프라이트를 코드로 생성(빌트인 의존 X). 마스크·테두리·배경·마커에 공용 사용.
        _circleSprite = MakeCircleSprite(128);
        var circle = _circleSprite;
        const float border = 6f; // 테두리(링) 두께

        // 원형 테두리(링) — View보다 살짝 크게 깔아 가장자리에 링으로 보임
        var borderGO = new GameObject("Border", typeof(Image));
        borderGO.transform.SetParent(rt, false);
        var bimg = borderGO.GetComponent<Image>();
        bimg.sprite = circle;
        bimg.type   = Image.Type.Simple;
        bimg.color  = new Color(0.85f, 0.9f, 1f, 0.95f);
        bimg.raycastTarget = false;
        var brt = bimg.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f); // 좌상단 기준
        brt.pivot = new Vector2(0f, 1f);
        brt.anchoredPosition = new Vector2(20f - border, -20f + border);
        brt.sizeDelta = new Vector2(panel + border * 2f, panel + border * 2f);

        // 어두운 원형 배경(지형 RT가 비어도 빈 원이 보이게) — 테두리 위, 콘텐츠 아래
        var bgGO = new GameObject("CircleBG", typeof(Image));
        bgGO.transform.SetParent(rt, false);
        var bgimg = bgGO.GetComponent<Image>();
        bgimg.sprite = circle;
        bgimg.type   = Image.Type.Simple;
        bgimg.color  = new Color(0.05f, 0.06f, 0.09f, 1f);
        bgimg.raycastTarget = false;
        var bgrt = bgimg.rectTransform;
        bgrt.anchorMin = bgrt.anchorMax = new Vector2(0f, 1f);
        bgrt.pivot = new Vector2(0f, 1f);
        bgrt.anchoredPosition = new Vector2(20f, -20f);
        bgrt.sizeDelta = new Vector2(panel, panel);

        // 원형 클립 영역(View) — 원형 스프라이트 Mask로 RT·점을 원으로 잘라냄
        var viewGO = new GameObject("View", typeof(Image), typeof(Mask));
        viewGO.transform.SetParent(rt, false);
        var vimg = viewGO.GetComponent<Image>();
        vimg.sprite = circle;
        vimg.type   = Image.Type.Simple;
        vimg.color  = Color.white;
        vimg.raycastTarget = false;
        viewGO.GetComponent<Mask>().showMaskGraphic = false; // 마스크용 원 자체는 안 그림
        _view = vimg.rectTransform;
        _view.anchorMin = _view.anchorMax = new Vector2(0f, 1f);
        _view.pivot = new Vector2(0f, 1f);
        _view.anchoredPosition = new Vector2(20f, -20f);
        _view.sizeDelta = new Vector2(panel, panel);

        // 지형 렌더(탑다운 카메라 RT) — 점·화살표보다 먼저 만들어 맨 뒤에 깔림
        BuildMapRender();
        EnsureMapCameraCreated();

        // 중앙 플레이어 화살표
        _playerArrow = MakeArrow();

        // 전체 지도(M) 오버레이 — 처음엔 숨김
        BuildFullMap();
    }

    // 전체 지도 오버레이(불투명 전체화면 + 큰 지도 + 플레이어 마커) 구성, 숨김 상태로 시작
    void BuildFullMap()
    {
        var canvasGO = new GameObject("FullMapCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95; // 미니맵/HUD 위
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        _fullRoot = canvasGO;
        var crt = canvasGO.GetComponent<RectTransform>();

        // 딤(게임화면 가림) — 너무 어둡지 않은 중간 톤
        var dim = new GameObject("Dim", typeof(Image));
        dim.transform.SetParent(crt, false);
        dim.GetComponent<Image>().color = new Color(0.28f, 0.31f, 0.38f, 0.92f);
        dim.GetComponent<Image>().raycastTarget = false;
        var drt = dim.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

        // 전체지도 RenderTexture
        _fullRT = new RenderTexture(512, 512, 16) { name = "FullMapRT" };
        _fullRT.Create();

        // 중앙 큰 지도(정사각)
        var imgGO = new GameObject("FullMap", typeof(RawImage));
        imgGO.transform.SetParent(crt, false);
        _fullImage = imgGO.GetComponent<RawImage>();
        _fullImage.texture       = _fullRT;
        _fullImage.raycastTarget = false;
        var irt = _fullImage.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = Vector2.zero;
        irt.sizeDelta = new Vector2(900f, 900f);

        // 플레이어 마커(흰 원 + 방향 코)
        _fullPlayerMarker = MakeMarker(irt, 26f);

        // 안내 텍스트
        var hint = new GameObject("Hint", typeof(TextMeshProUGUI));
        hint.transform.SetParent(crt, false);
        var t = hint.GetComponent<TextMeshProUGUI>();
        t.text = "전체 지도    (M: 닫기)";
        t.fontSize = 40;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        var hrt = t.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
        hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.anchoredPosition = new Vector2(0f, 500f);
        hrt.sizeDelta = new Vector2(900f, 70f);

        _fullRoot.SetActive(false);
    }

    // 플레이어 마커(흰 원 + 위로 뻗는 방향 코) 생성
    RectTransform MakeMarker(RectTransform parent, float size)
    {
        var go = new GameObject("PlayerMarker", typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = _circleSprite;
        img.color  = Color.white;
        img.raycastTarget = false;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(size, size);

        var nose = new GameObject("Nose", typeof(Image));
        nose.transform.SetParent(r, false);
        nose.GetComponent<Image>().color = Color.white;
        nose.GetComponent<Image>().raycastTarget = false;
        var nrt = nose.GetComponent<RectTransform>();
        nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
        nrt.pivot = new Vector2(0.5f, 0f);
        nrt.anchoredPosition = new Vector2(0f, size * 0.2f);
        nrt.sizeDelta = new Vector2(size * 0.3f, size * 0.85f);
        return r;
    }

    // 탑다운 직교 카메라 → RenderTexture → RawImage 배경 구성
    void BuildMapRender()
    {
        // 출력용 RenderTexture
        _mapRT = new RenderTexture(256, 256, 16) { name = "MinimapRT" };
        _mapRT.Create();

        // 배경 RawImage(View 맨 뒤, 패널 전체)
        var imgGO = new GameObject("MapRender", typeof(RawImage));
        imgGO.transform.SetParent(_view, false);
        mapImage = imgGO.GetComponent<RawImage>();
        mapImage.texture       = _mapRT;
        mapImage.raycastTarget = false;
        var rrt = mapImage.rectTransform;
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
        rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
        imgGO.transform.SetAsFirstSibling(); // 점·화살표 아래

        // 탑다운 카메라
        CreateMapCamera();
    }

    void EnsureMapCameraCreated()
    {
        if (_mapCam == null) CreateMapCamera();
    }

    void CreateMapCamera()
    {
        var camGO = new GameObject("MinimapCamera");
        camGO.transform.SetParent(transform, false);
        _mapCam = camGO.AddComponent<Camera>();
        _mapCam.orthographic   = true;
        _mapCam.targetTexture  = _mapRT;
        _mapCam.clearFlags      = CameraClearFlags.SolidColor;
        _mapCam.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
        _mapCam.nearClipPlane  = 0.1f;
        _mapCam.farClipPlane   = _camHeight + 60f;
        _mapCam.allowHDR       = false;
        _mapCam.allowMSAA      = false;
        _mapCam.cullingMask    = BuildCullingMask(); // 유령·UI 제외(유령은 점으로 표시)
    }

    // 지형만 찍도록: 유령 레이어와 UI 레이어를 컬링에서 제외
    int BuildCullingMask()
    {
        int mask  = ~0; // 전부
        int ghost = LayerMask.NameToLayer("Ghost");
        if (ghost >= 0) mask &= ~(1 << ghost);
        mask &= ~(1 << 5); // UI 레이어 제외(미니맵 캔버스 등)
        return mask;
    }

    void OnDestroy()
    {
        if (_mapCam != null)  _mapCam.targetTexture  = null;
        if (_fullCam != null) _fullCam.targetTexture = null;
        ReleaseRenderTexture(ref _mapRT);
        ReleaseRenderTexture(ref _fullRT);

        // 혹시 전체지도 연 채로 파괴되면 타임스케일 복원
        if (_mapOpen) Time.timeScale = _prevTimeScale;
    }

    // RenderTexture 해제 — 에디트 모드에선 Destroy가 안 되므로 DestroyImmediate로 분기.
    void ReleaseRenderTexture(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        if (Application.isPlaying) Destroy(rt);
        else                       DestroyImmediate(rt);
        rt = null;
    }

    // 가운데 플레이어 마커(삼각형 느낌의 작은 화살표) — 흰색
    RectTransform MakeArrow()
    {
        var go = new GameObject("PlayerArrow", typeof(Image));
        go.transform.SetParent(_view, false);
        var img = go.GetComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(14f, 14f);

        // 위쪽으로 뾰족하게 보이도록 작은 막대를 덧대 방향감 강조
        var nose = new GameObject("Nose", typeof(Image));
        nose.transform.SetParent(r, false);
        nose.GetComponent<Image>().color = Color.white;
        nose.GetComponent<Image>().raycastTarget = false;
        var nrt = nose.GetComponent<RectTransform>();
        nrt.anchorMin = nrt.anchorMax = new Vector2(0.5f, 0.5f);
        nrt.pivot = new Vector2(0.5f, 0f);
        nrt.anchoredPosition = new Vector2(0f, 2f);
        nrt.sizeDelta = new Vector2(4f, 12f); // 위로 뻗는 코
        return r;
    }

    // 점 1개 생성 후 풀에 등록
    Image MakeDot(List<Image> pool, Color color)
    {
        var go = new GameObject("Dot", typeof(Image));
        go.transform.SetParent(_view, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(11f, 11f);
        pool.Add(img);
        return img;
    }

    // 흰 원(가장자리 1px 안티앨리어싱) 스프라이트를 코드로 생성 — 마스크/테두리/배경 공용
    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float r = size * 0.5f;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d); // 경계 1px 부드럽게
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
