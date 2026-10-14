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

    // 게임에서 쓰는 글자들. 시작할 때 한 번에 폰트에 등록해 플레이 중 "처음 나오는 글자"로 멈추지 않게 한다.
    private const string Glyphs =
        "0123456789:/.%()!?·◆●- " +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
        "남은시간시작전소탕완료방코드의뢰안내전투방유령을모두하세요잡동사니를함께치우면이나타납니다" +
        "동료접속기다리는중앙홀빛나원으로이동마우스조준좌클릭사격재장전상호작용" +
        "출현차정리길게눌러구조움직이지마세요먼저다른끝나야합니다출입구에서떨어져주" +
        "다운되었습니다성공필요나체력무" +
        "실패초과두명쓰러졌습니다제한안에하지못했경과최고기록없음팀횟수회신기록등록않습니도전가기" +
        "호스트가선택하기를는멀티플레이인협새만들고초대참가받은입력예취종형식확인해할수없가득찼연결이되어단합" +
        "부족";

    private void Awake()
    {
        NCUiKit.Font = font;

        if (font != null)
        {
            font.TryAddCharacters(Glyphs);
        }

        gameObject.AddComponent<NCDebugOverlay>();

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
