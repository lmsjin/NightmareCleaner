using UnityEngine;

/// <summary>탑다운 게임에서 자주 쓰는 계산. 높이(Y)는 무시하고 바닥 평면에서 판단한다.</summary>
public static class NCMath
{
    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    /// <summary>1/100초 값을 MM:SS.cc 로 표시한다.</summary>
    public static string FormatCentisec(int centisec)
    {
        int seconds = centisec / 100;
        return $"{seconds / 60:00}:{seconds % 60:00}.{centisec % 100:00}";
    }

    /// <summary>남은 시간(초)을 MM:SS 로 표시한다. 0 이상으로 제한한 뒤 올림.</summary>
    public static string FormatClock(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
