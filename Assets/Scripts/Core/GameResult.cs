// 씬 전환을 넘어 결과(승/패 + 점수)를 end 씬으로 전달하는 static 홀더.
// 인게임 종료 시 Set() 호출 → end 씬의 EndMenuController가 읽어 표시.
public static class GameResult
{
    public static bool Win;
    public static int  Score;

    public static void Set(bool win, int score)
    {
        Win   = win;
        Score = score;
    }
}
