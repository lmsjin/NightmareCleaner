using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { InGame, Paused, GameOver, Result }
    public GameState State { get; private set; }

    public float TimeRemaining { get; private set; }

    [SerializeField] private BalanceConfig config;
    private bool _timerRunning;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        GameEvents.OnGameOver += HandleGameOver;
        StartGame();
    }

    void StartGame()
    {
        var c = BalanceConfig.Resolve(config);
        TimeRemaining = c != null ? c.gameDurationSeconds : 600f;
        _timerRunning = true;
        State         = GameState.InGame;
        GameEvents.RaiseGameStart();
    }

    void Update()
    {
        if (!_timerRunning) return;

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining > 0f) return;

        TimeRemaining = 0f;
        _timerRunning = false;
        State         = GameState.Result;
        GameEvents.RaiseTimerEnd();
    }

    void HandleGameOver()
    {
        _timerRunning = false;
        State         = GameState.GameOver;
    }

    void OnDestroy()
    {
        GameEvents.OnGameOver -= HandleGameOver;
        if (Instance == this) Instance = null;
    }
}
