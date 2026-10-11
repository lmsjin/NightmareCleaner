using UnityEngine;

/// <summary>
/// 화면 스타일 수치(UI 기획서 19장). 기획 수치(NCBalance)와 분리해 둔다.
/// 저택 분위기: 남색 반투명 패널 + 얇은 황동 테두리 + 아이보리 글자.
/// </summary>
public static class NCStyle
{
    public static readonly Color Panel = Hex("172536", 0.84f);
    public static readonly Color PanelDeep = Hex("0E1824", 0.92f);
    public static readonly Color Brass = Hex("B6955E");
    public static readonly Color BrassDim = Hex("B6955E", 0.45f);
    public static readonly Color Text = Hex("F4F1E7");
    public static readonly Color TextDim = Hex("F4F1E7", 0.6f);
    public static readonly Color P1 = Hex("58B8EA");
    public static readonly Color P2 = Hex("F2C34E");
    public static readonly Color Danger = Hex("E5484D");
    public static readonly Color Success = Hex("6FCF97");
    public static readonly Color BarBack = Hex("0A111A", 0.85f);
    public static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);

    public static readonly Color GhostBasic = Hex("C9B6F2");
    public static readonly Color GhostFast = Hex("9EE6F0");
    public static readonly Color GhostTelegraph = Hex("FF5A6E");

    // 글자 크기(기준 해상도 1920×1080)
    public const float FontBody = 24f;
    public const float FontGuide = 28f;
    public const float FontTimer = 44f;
    public const float FontTitle = 64f;

    // 여백
    public const float Margin = 32f;
    public const float Gap = 12f;

    public static Color SlotColor(int slot) => slot == 0 ? P1 : P2;

    public static Color Hex(string hex, float alpha = 1f)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        color.a = alpha;
        return color;
    }
}
