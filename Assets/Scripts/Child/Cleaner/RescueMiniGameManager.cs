using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RescueMiniGameManager : MonoBehaviour
{
    [Header("Scene Mini Games")]
    [SerializeField] private MiniGameSequence sequenceGame;
    [SerializeField] private MiniGameGaugeStop gaugeGame;

    [Header("Editable Countdown UI")]
    [SerializeField] private GameObject countdownRoot;
    [SerializeField] private TMP_Text countdownText;

    [Header("Editable Text")]
    [SerializeField] private bool useCanvasStartText = true;
    [TextArea(1, 3)]
    [SerializeField] private string countdownStartMessage = "시작!";
    [SerializeField] private int countdownNumberCount = 3;
    [SerializeField] private float countdownStartFlashSeconds = 1f;

    // 구출 미니게임이 진행 중인 동안 true — 유령 등 다른 스크립트가 이걸 보고 동작을 멈춤
    public static bool IsActive { get; private set; }

    private BalanceConfig config;
    private IRescueMiniGame[] games;
    private Coroutine routine;

    private void Awake()
    {
        config = BalanceConfig.Resolve(null);
        if (sequenceGame == null) sequenceGame = GetComponent<MiniGameSequence>();
        if (gaugeGame == null) gaugeGame = GetComponent<MiniGameGaugeStop>();
        games = new IRescueMiniGame[] { sequenceGame, gaugeGame };
        if (countdownRoot != null) countdownRoot.SetActive(false);
        CaptureCanvasStartText();
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        StopAllMiniGames();
        IsActive = false;
        if (countdownRoot != null) countdownRoot.SetActive(false);
    }

    public void Play(Action<int> onComplete)
    {
        if (sequenceGame == null || gaugeGame == null)
        {
            Debug.LogError("[RescueMiniGameManager] 씬 미니게임 참조가 없습니다.");
            onComplete?.Invoke(0);
            return;
        }

        IsActive = true;
        IRescueMiniGame game = games[UnityEngine.Random.Range(0, games.Length)];
        if (routine != null) StopCoroutine(routine);
        StopAllMiniGames();
        PrepareCountdownView(false);
        routine = StartCoroutine(CountdownThenBegin(game, onComplete));
    }

    private IEnumerator CountdownThenBegin(IRescueMiniGame game, Action<int> onComplete)
    {
        int count = Mathf.Max(1, countdownNumberCount);
        float totalSeconds = config != null ? Mathf.Max(0.01f, config.miniGameCountdownSeconds) : 1.5f;
        float secondsPerNumber = totalSeconds / count;

        CaptureCanvasStartText();
        PrepareCountdownView(true);
        for (int i = count; i >= 1; i--)
        {
            if (countdownText != null) countdownText.text = i.ToString();
            yield return new WaitForSecondsRealtime(secondsPerNumber);
        }
        if (countdownText != null) countdownText.text = countdownStartMessage;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, countdownStartFlashSeconds));
        if (countdownRoot != null) countdownRoot.SetActive(false);
        routine = null;
        StopAllMiniGames();
        game.Begin(stagesCleared =>
        {
            IsActive = false;
            StopAllMiniGames();
            onComplete?.Invoke(stagesCleared);
        });
    }

    private void StopAllMiniGames()
    {
        if (games == null)
            return;

        foreach (IRescueMiniGame game in games)
            game?.Cancel();
    }

    private void PrepareCountdownView(bool visible)
    {
        if (countdownRoot != null)
        {
            countdownRoot.transform.localScale = Vector3.one;
            countdownRoot.SetActive(visible);
        }

        if (countdownText != null)
        {
            countdownText.transform.localScale = Vector3.one;
            countdownText.gameObject.SetActive(visible || countdownRoot != null);
        }
    }

    private void CaptureCanvasStartText()
    {
        if (!useCanvasStartText || countdownText == null)
            return;

        string canvasText = countdownText.text;
        if (!string.IsNullOrEmpty(canvasText) && !int.TryParse(canvasText, out _))
            countdownStartMessage = canvasText;
    }

#if UNITY_EDITOR
    public void SetupEditableUI(MiniGameSequence sequence, MiniGameGaugeStop gauge)
    {
        sequenceGame = sequence;
        gaugeGame = gauge;
        if (countdownRoot == null)
        {
            GameObject canvasObject = EditableMiniGameUI.CreateCanvas(transform, "CountdownCanvas", 95);
            countdownRoot = canvasObject;
            EditableMiniGameUI.CreateDim(canvasObject.transform);
            countdownText = EditableMiniGameUI.CreateText(canvasObject.transform, "CountdownText", 220f, Vector2.zero, new Vector2(800f, 400f));
        }
        if (countdownText != null && string.IsNullOrEmpty(countdownText.text))
            countdownText.text = countdownStartMessage;
        PrepareCountdownView(false);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}