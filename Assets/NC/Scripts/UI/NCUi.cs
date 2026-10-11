using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 전체를 코드로 만들고 상태에 따라 전환한다: 접속 → 인게임 HUD → 결과.
/// 화면은 게임 상태를 바꾸지 않고 확정된 값(NCSession·NCCombatRoom·NCPlayer)을 읽어 그리기만 한다.
/// 버튼은 요청만 한다(방 만들기·참가·재도전·나가기).
///
/// 기준 해상도 1920×1080, Scale With Screen Size(Match 0.5). 씬의 "UI" 오브젝트에 붙인다.
/// </summary>
public class NCUi : MonoBehaviour
{
    [Tooltip("한글 TMP 폰트(빌더가 맑은 고딕 SDF를 연결)")]
    [SerializeField] private TMP_FontAsset font;

    private RectTransform _root;
    private NCConnectScreen _connect;
    private NCHud _hud;
    private NCResultScreen _result;

    public RectTransform Root => _root;

    private void Awake()
    {
        NCUiKit.Font = font;

        GameObject canvasObject = new GameObject("NC_Canvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        _root = (RectTransform)canvasObject.transform;

        // 그리는 순서: HUD → 결과 → 접속(가장 위)
        _hud = new NCHud(_root);
        _result = new NCResultScreen(_root);
        _connect = new NCConnectScreen(_root);
    }

    private void Update()
    {
        NCLauncher launcher = NCLauncher.Instance;
        bool connected = launcher != null && launcher.State == NCConnectState.Connected;
        NCSession session = NCSession.Instance;
        bool finished = connected && session != null && session.Object != null && session.Object.IsValid && session.IsFinished;

        _connect.SetVisible(connected == false);
        _hud.SetVisible(connected && finished == false);
        _result.SetVisible(finished);

        if (connected == false)
        {
            _connect.Tick(launcher);
        }
        else if (finished)
        {
            _result.Tick(session, launcher);
        }
        else
        {
            _hud.Tick(session, launcher);
        }
    }
}
