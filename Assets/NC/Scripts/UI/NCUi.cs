using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>알림음 종류.</summary>
public enum NCSound
{
    Tick,
    Reward,
    Win,
    Lose,
}

/// <summary>
/// 화면 전체를 코드로 만들고 상태에 따라 전환한다: 접속 → 대기실 → 인게임 HUD(+Esc 메뉴) → 결과.
/// 화면은 게임 상태를 바꾸지 않고 확정된 값(NCSession·NCGhostCamp·NCPlayer)을 읽어 그리기만 한다.
/// 버튼은 요청만 한다(방 만들기·참가·재도전·나가기).
///
/// 기준 해상도 1920×1080, Scale With Screen Size(Match 0.5). 씬의 "UI" 오브젝트에 붙인다.
/// </summary>
[DefaultExecutionOrder(500)] // NCCameraRig.LateUpdate 뒤에 머리 위 표시를 옮긴다
public class NCUi : MonoBehaviour
{
    [Tooltip("한글 TMP 폰트(빌더가 맑은 고딕 SDF를 연결)")]
    [SerializeField] private TMP_FontAsset font;

    private RectTransform _root;
    private NCConnectScreen _connect;
    private NCHud _hud;
    private NCResultScreen _result;
    private NCLobbyScreen _lobby;
    private NCMenuScreen _menu;
    private NCConfirmPopup _confirm;

    /// <summary>메뉴·확인 팝업이 열려 있어 내 게임 입력을 막아야 하는지(NCLauncher가 입력 수집 때 확인).</summary>
    public static bool InputBlocked { get; private set; }

    public RectTransform Root => _root;

    private static NCUi _instance;
    private AudioSource _audio;

    /// <summary>짧은 알림음(기존 Resources/Audio 클립 재사용).</summary>
    public static void PlaySound(NCSound sound)
    {
        if (_instance == null)
        {
            return;
        }

        string clip = sound == NCSound.Win ? "bgm_clear" : sound == NCSound.Lose ? "bgm_gameover" : sound == NCSound.Reward ? "bgm_levelup" : "bgm_uiclick";
        AudioClip asset = Resources.Load<AudioClip>("Audio/" + clip);
        if (asset != null)
        {
            _instance._audio.PlayOneShot(asset, 0.6f);
        }
    }

    // 게임에서 쓰는 글자들. 시작할 때 한 번에 폰트에 등록해 플레이 중 "처음 나오는 글자"로 멈추지 않게 한다.
    private const string Glyphs =
        "0123456789:/.%()!?+~- " +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
        "·▲◆●" +
        "가간게겠격결경계과근기까나남뉴는능니다단닫달대도돌동되됩두드득등라령로록료른를리립만메면모목못무방배보본부비빠사상서성세소속수쉬스습승시식실씬아악앙어없었에연예온완요용우유율은을음이인일임장재전점접제종주준중지진참찼초최충취치커코탈탱트패포하한할합해했행현형호확활회획횟후류률막망명처체타";

    private void Awake()
    {
        _instance = this;
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
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

        // 그리는 순서: HUD → 대기실 → 결과 → 메뉴 → 접속 → 확인 팝업(가장 위)
        _hud = new NCHud(_root);
        _confirm = new NCConfirmPopup(_root);
        _lobby = new NCLobbyScreen(_root, _confirm);
        _result = new NCResultScreen(_root);
        _menu = new NCMenuScreen(_root, _confirm);
        _connect = new NCConnectScreen(_root);
        ((RectTransform)_root.Find("ConfirmPopup")).SetAsLastSibling();
    }

    private void OnDestroy()
    {
        InputBlocked = false;
    }

    private void Update()
    {
        NCLauncher launcher = NCLauncher.Instance;
        bool connected = launcher != null && launcher.State == NCConnectState.Connected;
        NCSession session = NCSession.Instance;
        bool ready = connected && session != null && session.Object != null && session.Object.IsValid;
        bool lobby = ready && session.Phase == NCPhase.Lobby;
        bool finished = ready && session.IsFinished;
        bool inGame = connected && lobby == false && finished == false;

        // 메뉴는 카운트다운·진행 중에만. 결과가 확정되거나 대기실로 돌아가면 닫는다.
        if (inGame == false && _menu.IsOpen)
        {
            _menu.Close();
        }

        if (connected == false && _confirm.IsOpen)
        {
            _confirm.Hide();
        }

        HandleEscape(inGame);

        _connect.SetVisible(connected == false);
        _lobby.SetVisible(lobby);
        _hud.SetVisible(inGame);
        _result.SetVisible(finished);

        if (connected == false)
        {
            _connect.Tick(launcher);
        }
        else if (lobby)
        {
            _lobby.Tick(session, launcher);
        }
        else if (finished)
        {
            _result.Tick(session, launcher);
        }
        else
        {
            _hud.Tick(session, launcher);
        }

        InputBlocked = _menu.IsOpen || _confirm.IsOpen;
    }

    private void LateUpdate()
    {
        _hud.LateTick();
    }

    /// <summary>Esc는 위에 열린 것부터 하나씩 닫는다: 확인 팝업 → 메뉴. 게임 중이면 메뉴를 연다.</summary>
    private void HandleEscape(bool inGame)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || keyboard.escapeKey.wasPressedThisFrame == false)
        {
            return;
        }

        if (_confirm.IsOpen)
        {
            _confirm.Hide();
        }
        else if (_menu.IsOpen)
        {
            _menu.Close();
        }
        else if (inGame)
        {
            _menu.Open();
        }
    }
}
