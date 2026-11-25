using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유령 머리 위 체력바(모든 화면). 벽에 가리면 숨긴다.
/// 복귀 중에는 맞지 않고 도착하면 회복하므로 어둡게 표시한다.
/// </summary>
public class NCGhostBar
{
    private static readonly Vector2 BarSize = new Vector2(170f, 22f);

    private readonly RectTransform _canvas;
    private readonly RectTransform _root;
    private readonly Image _fill;
    private readonly TextMeshProUGUI _text;
    private int _shownHp = -1;

    public NCGhostBar(RectTransform parent)
    {
        _canvas = parent;
        _root = NCUiKit.NewRect("GhostBar", parent);
        _root.SetAsFirstSibling(); // 다른 HUD 뒤에 그린다.
        NCUiKit.Place(_root, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        _fill = NCUiKit.NewBar("Hp", _root, Color.white, out RectTransform bar);
        NCUiKit.Place(bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), BarSize);

        _text = NCUiKit.NewText("Text", bar, "", 16f, Color.white, style: FontStyles.Bold);
        NCUiKit.Stretch(_text.rectTransform);
    }

    public void Tick(NCGhost ghost)
    {
        Camera cam = Camera.main;
        bool show = ghost != null && ghost.Object != null && ghost.Object.IsValid && ghost.Hp > 0f && cam != null
                    && NCOverhead.IsVisible(cam.transform.position, ghost.transform.position);

        Vector3 top = show ? ghost.transform.position + Vector3.up * ghost.TopHeight : Vector3.zero;
        if (NCOverhead.Follow(_root, _canvas, cam, show, top) == false)
        {
            return;
        }

        NCUiKit.SetBar(_fill, ghost.HpRatio);

        bool returning = ghost.State == NCGhostState.Return;
        _fill.color = returning ? ghost.BodyColor() * new Color(0.4f, 0.4f, 0.4f, 1f) : ghost.BodyColor();

        int hp = Mathf.CeilToInt(ghost.Hp);
        if (hp != _shownHp)
        {
            _shownHp = hp;
            _text.text = hp.ToString();
        }

        // 밝은 채움 위에서는 어두운 글자, 어두운 배경에서는 밝은 글자
        _text.color = returning || ghost.HpRatio < 0.55f ? NCStyle.Text : NCStyle.PanelDeep;
    }
}
