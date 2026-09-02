using System;
using UnityEngine;

public static class GameEvents
{
    // 도메인 리로드 비활성화(Enter Play Mode) 환경에서 static 이벤트가
    // 이전 실행의 죽은 구독자를 들고 있는 것을 막기 위해 Play 시작 시 전부 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        OnGameStart       = null;
        OnGameOver        = null;
        OnTimerEnd        = null;
        OnGhostKilled     = null;
        OnChildRescued    = null;
        OnMiniGameStart   = null;
        OnMiniGameEnd     = null;
        OnAbilityTrigger  = null;
        OnAbilitySelected = null;
    }

    // 모듈 A 발행
    public static event Action OnGameStart;
    public static event Action OnGameOver;
    public static event Action OnTimerEnd;

    // 모듈 B 발행
    public static event Action<GhostBase> OnGhostKilled;
    public static event Action<int>       OnChildRescued;   // 파라미터: 최종 획득 점수
    public static event Action            OnMiniGameStart;  // 모듈 A: 플레이어 이동 잠금
    public static event Action<bool>      OnMiniGameEnd;    // true=성공, false=실패

    // 모듈 C 발행
    public static event Action             OnAbilityTrigger;
    public static event Action<AbilityType> OnAbilitySelected;

    // --- Raise 메서드 (이벤트 소유 모듈만 호출) ---
    public static void RaiseGameStart()                     => OnGameStart?.Invoke();
    public static void RaiseGameOver()                      => OnGameOver?.Invoke();
    public static void RaiseTimerEnd()                      => OnTimerEnd?.Invoke();
    public static void RaiseGhostKilled(GhostBase g)        => OnGhostKilled?.Invoke(g);
    public static void RaiseChildRescued(int score)         => OnChildRescued?.Invoke(score);
    public static void RaiseMiniGameStart()                 => OnMiniGameStart?.Invoke();
    public static void RaiseMiniGameEnd(bool success)       => OnMiniGameEnd?.Invoke(success);
    public static void RaiseAbilityTrigger()                => OnAbilityTrigger?.Invoke();
    public static void RaiseAbilitySelected(AbilityType t)  => OnAbilitySelected?.Invoke(t);
}
