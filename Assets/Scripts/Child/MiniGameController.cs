using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class MiniGameController : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private GameObject miniGamePopupPanel = null; // 화면을 덮는 반투명 패널
    [SerializeField] private TextMeshProUGUI sequenceText;         // TAP D / HOLD W 글자 표시
    [SerializeField] private TextMeshProUGUI holdTimerText;        // 홀드 진행 시간 표시용
    [SerializeField] private TextMeshProUGUI statusText;           // 하단 상태 알림창

    [Header("밸런스 설정 데이터 (ScriptableObject)")]
    [Tooltip("비워두면 Resources/Configs/BalanceConfig 에셋을 자동으로 로드합니다.")]
    [SerializeField] private BalanceConfig config;                 // SO 데이터 연결 창

    [Header("미니게임 자체 난이도 설정")]
    [SerializeField] private int sequenceLength = 5;               // 입력할 총 커맨드 개수
    [SerializeField] private int maxFailures = 3;                  // 허용되는 최대 실패 횟수
    [SerializeField] private float holdSeconds = 0.7f;             // HOLD 지속 시간
    [SerializeField, Range(0f, 1f)] private float holdChance = 0.2f; // HOLD 키가 나올 확률

    private ChildrenText childrenText = null;

    // QTE 데이터 정의
    private static readonly Key[] Keys = { Key.W, Key.A, Key.S, Key.D };
    private static readonly string[] Labels = { "W", "A", "S", "D" };

    private Key[] sequence;
    private string[] labels;
    private bool[] holdSteps;

    private int index;
    private int failures;
    private float inputTimer;
    private float holdTimer;
    private bool bMiniGameActive = false;

    // 실시간으로 반영될 밸런스 값 보관용 변수
    private float currentInputTimeLimit = 3f;
    private int currentRescueScore = 150;

    // [버그 방지 시스템] 이전 단계에서 입력 성공한 키를 기억하고 완전히 뗄 때까지 연산을 대기시킵니다.
    private Key lastClearedKey = Key.None;
    private bool bWaitingForKeyRelease = false;

    // 외부(ChildrenText)에서 미니게임 진행 상태를 읽을 수 있게 해주는 프로퍼티
    public bool IsGameActive => bMiniGameActive;

    void Start()
    {
        childrenText = GetComponent<ChildrenText>();

        if (miniGamePopupPanel != null) miniGamePopupPanel.SetActive(false);

        config = BalanceConfig.Resolve(config);
    }

    void Update()
    {
        if (!bMiniGameActive || sequence == null) return;
        if (Keyboard.current == null) return;

        // 연속으로 같은 키를 입력해야 할 때, 손가락을 완전히 뗄 때까지 이번 프레임의 입력을 대기
        if (bWaitingForKeyRelease)
        {
            if (Keyboard.current[lastClearedKey].wasReleasedThisFrame)
            {
                bWaitingForKeyRelease = false; // 손을 뗐으므로 정상 입력 허용
            }
            else
            {
                inputTimer -= Time.unscaledDeltaTime;
                if (inputTimer <= 0f)
                {
                    FailCurrentStep("Too late. The child cries");
                }
                return;
            }
        }

        // 1. 제한 시간 타이머 카운트다운
        inputTimer -= Time.unscaledDeltaTime;
        if (inputTimer <= 0f)
        {
            FailCurrentStep("Too late. The child cries");
            return;
        }

        // 2. 현재 단계가 꾹 누르기(HOLD) 단계일 때 처리
        if (holdSteps[index])
        {
            HandleHoldInput();
            return;
        }

        // 3. 현재 단계가 연타(TAP) 단계일 때 처리
        for (int i = 0; i < Keys.Length; i++)
        {
            if (Keyboard.current[Keys[i]].wasPressedThisFrame)
            {
                HandleInput(Keys[i]);
                break;
            }
        }
    }

    // 미니게임 시작 함수 (ChildrenText에서 호출함)
    public void StartMiniGame()
    {
        bMiniGameActive = true;
        index = 0;
        failures = 0;
        lastClearedKey = Key.None;
        bWaitingForKeyRelease = false;

        config = BalanceConfig.Resolve(config);
        if (config != null)
        {
            currentInputTimeLimit = config.miniGameTimeLimit;
            currentRescueScore = config.scoreChildRescue;
        }
        else
        {
            currentInputTimeLimit = 3f;
            currentRescueScore = 100;
        }

        GameEvents.RaiseMiniGameStart();

        sequence = new Key[Mathf.Max(1, sequenceLength)];
        labels = new string[sequence.Length];
        holdSteps = new bool[sequence.Length];

        for (int i = 0; i < sequence.Length; i++)
        {
            int pick = UnityEngine.Random.Range(0, Keys.Length);
            sequence[i] = Keys[pick];
            labels[i] = Labels[pick];
            holdSteps[i] = UnityEngine.Random.value < holdChance;
        }

        if (miniGamePopupPanel != null) miniGamePopupPanel.SetActive(true);
        ResetCurrentStep("Calm the child");
    }

    // TAP 입력 검사
    private void HandleInput(Key key)
    {
        if (key == sequence[index])
        {
            CompleteCurrentStep("Good");
            return;
        }

        FailCurrentStep("Wrong key. The child cries");
    }

    // HOLD 입력 검사
    private void HandleHoldInput()
    {
        Key expected = sequence[index];

        for (int i = 0; i < Keys.Length; i++)
        {
            if (Keys[i] != expected && Keyboard.current[Keys[i]].wasPressedThisFrame)
            {
                FailCurrentStep("Wrong key. The child cries");
                return;
            }
        }

        if (Keyboard.current[expected].isPressed)
        {
            holdTimer += Time.unscaledDeltaTime;
            RefreshUI("Hold...");
            if (holdTimer >= holdSeconds)
                CompleteCurrentStep("Good");
            return;
        }

        if (holdTimer > 0f && Keyboard.current[expected].wasReleasedThisFrame)
            FailCurrentStep("Released too early. The child cries");
    }

    private void CompleteCurrentStep(string status)
    {
        lastClearedKey = sequence[index];
        bWaitingForKeyRelease = true;

        index++;
        if (index >= sequence.Length)
        {
            EndMiniGame(true);
            return;
        }

        ResetCurrentStep(status);
    }

    private void FailCurrentStep(string status)
    {
        failures++;
        index = 0;

        lastClearedKey = Key.None;
        bWaitingForKeyRelease = false;

        if (failures >= maxFailures)
        {
            EndMiniGame(false);
            return;
        }

        ResetCurrentStep(status);
    }

    private void ResetCurrentStep(string status)
    {
        inputTimer = currentInputTimeLimit;
        holdTimer = 0f;
        RefreshUI(status);
    }

    private void RefreshUI(string status)
    {
        string keyText = labels[index];

        if (sequenceText != null)
        {
            sequenceText.text = holdSteps[index] ? $"HOLD  {keyText}" : $"TAP  {keyText}";
        }

        if (holdTimerText != null)
        {
            if (holdSteps[index])
            {
                holdTimerText.gameObject.SetActive(true);
                holdTimerText.text = $"{holdTimer:0.0} / {holdSeconds:0.0}s";
            }
            else
            {
                holdTimerText.gameObject.SetActive(false);
            }
        }

        if (statusText != null)
        {
            int remaining = Mathf.CeilToInt(Mathf.Max(0f, inputTimer));
            statusText.text = $"{status}   Step {index + 1}/{sequence.Length}   Time {remaining}s   Fail {failures}/{maxFailures}";
        }
    }

    public void EndMiniGame(bool isSuccess)
    {
        bMiniGameActive = false;

        if (miniGamePopupPanel != null)
        {
            miniGamePopupPanel.SetActive(false);
        }

        try
        {
            GameEvents.RaiseMiniGameEnd(isSuccess);
        }
        catch (System.NullReferenceException)
        {
            Debug.LogError("GameEvents.RaiseMiniGameEnd 함수 내부에서 Null 에러가 발생했습니다.");
        }

        if (isSuccess)
        {
            try
            {
                GameEvents.RaiseChildRescued(currentRescueScore);
            }
            catch (System.NullReferenceException)
            {
                Debug.LogError("GameEvents.RaiseChildRescued 함수 내부에서 Null 에러가 발생했습니다.");
            }

            if (transform.parent != null)
            {
                Destroy(transform.parent.gameObject, 0.2f);
            }
            else
            {
                Destroy(gameObject, 0.2f);
            }
        }
        else
        {
            if (childrenText != null)
            {
                childrenText.ResetTextUI();
            }
        }
    }
}