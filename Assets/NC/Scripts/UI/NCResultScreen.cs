using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과 화면: 승리·패배·무승부(각자 기준), 종료 사유, 1P·2P 통계, 남은 시간.
/// 호스트는 [대기실로], 참가자는 호스트를 기다린다. 둘 다 [나가기] 가능.
/// </summary>
public class NCResultScreen
{
    private const int RowCount = 6;
    private const float TableWidth = 660f;
    private const float RowHeight = 46f;
    private static readonly string[] RowLabels = { "지배율", "처치", "사망", "유령 막타", "점령지 체류", "명중률" };

    private readonly GameObject _root;
    private readonly Image _band;
    private readonly Image _frame;
    private readonly TextMeshProUGUI _title;
    private readonly TextMeshProUGUI _subtitle;
    private readonly TextMeshProUGUI[] _headers = new TextMeshProUGUI[2];
    private readonly TextMeshProUGUI[,] _cells = new TextMeshProUGUI[2, RowCount];
    private readonly Image[] _columns = new Image[2];
    private readonly TextMeshProUGUI _time;
    private readonly Button _retry;
    private readonly TextMeshProUGUI _waiting;
    private string _shownKey;

    public NCResultScreen(RectTransform canvas)
    {
        RectTransform root = NCUiKit.NewRect("ResultScreen", canvas);
        NCUiKit.Stretch(root);
        _root = root.gameObject;

        Image dim = NCUiKit.NewImage("Dim", root, null, NCStyle.Dim);
        NCUiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        RectTransform panel = NCUiKit.NewPanel("Panel", root, deep: true);
        NCUiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(740f, 700f));
        _frame = panel.Find("Frame").GetComponent<Image>();

        _band = NCUiKit.NewImage("Band", panel, NCUiKit.RoundSprite, NCStyle.Success);
        NCUiKit.Place(_band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(560f, 8f));

        _title = NCUiKit.NewText("Title", panel, "", 60f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(580f, 76f));

        _subtitle = NCUiKit.NewText("Subtitle", panel, "", NCStyle.FontBody, NCStyle.TextDim);
        NCUiKit.Place(_subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(580f, 32f));

        RectTransform table = NCUiKit.NewPanel("Table", panel);
        NCUiKit.Place(table, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(TableWidth, RowHeight * (RowCount + 1) + 24f));

        for (int slot = 0; slot < 2; slot++)
        {
            float x = ColumnX(slot);
            _columns[slot] = NCUiKit.NewImage("Column" + slot, table, NCUiKit.RoundSprite, Color.clear);
            NCUiKit.Place(_columns[slot].rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(170f, RowHeight * (RowCount + 1) + 8f));
            _headers[slot] = Cell(table, "Header" + slot, x, 0, NCStyle.SlotColor(slot));

            for (int row = 0; row < RowCount; row++)
            {
                _cells[slot, row] = Cell(table, $"Cell{slot}_{row}", x, row + 1, NCStyle.Text);
            }
        }

        for (int row = 0; row < RowCount; row++)
        {
            TextMeshProUGUI label = NCUiKit.NewText("Label" + row, table, "◆  " + RowLabels[row], 24f, NCStyle.TextDim, TextAlignmentOptions.Left);
            NCUiKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(26f, RowY(row + 1)), new Vector2(240f, RowHeight));
        }

        _time = NCUiKit.NewText("Time", panel, "", 24f, NCStyle.TextDim);
        NCUiKit.Place(_time.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 122f), new Vector2(TableWidth, 36f));

        _retry = NCUiKit.NewButton("Retry", panel, "대기실로", true, () => NCSession.Instance?.RequestRetry());
        NCUiKit.Place((RectTransform)_retry.transform, new Vector2(0.5f, 0f), new Vector2(-140f, 40f), new Vector2(250f, 68f));

        Button leave = NCUiKit.NewButton("Leave", panel, "나가기", false, () => NCLauncher.Instance?.Leave());
        NCUiKit.Place((RectTransform)leave.transform, new Vector2(0.5f, 0f), new Vector2(140f, 40f), new Vector2(250f, 68f));

        _waiting = NCUiKit.NewText("Waiting", panel, "호스트가 대기실로 돌아가기를 기다리는 중", 20f, NCStyle.TextDim);
        _waiting.textWrappingMode = TextWrappingModes.Normal;
        NCUiKit.Place(_waiting.rectTransform, new Vector2(0.5f, 0f), new Vector2(-140f, 44f), new Vector2(250f, 60f));
    }

    private static float ColumnX(int slot) => 284f + slot * 184f;

    private static float RowY(int row) => -12f - row * RowHeight;

    private static TextMeshProUGUI Cell(RectTransform table, string name, float x, int row, Color color)
    {
        TextMeshProUGUI text = NCUiKit.NewText(name, table, "", 28f, color, style: FontStyles.Bold);
        NCUiKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(x, RowY(row)), new Vector2(170f, RowHeight));
        return text;
    }

    public void SetVisible(bool visible)
    {
        if (_root.activeSelf != visible)
        {
            _root.SetActive(visible);
            _shownKey = null;
        }
    }

    public void Tick(NCSession session, NCLauncher launcher)
    {
        if (_shownKey != null)
        {
            return; // 결과는 한 번 확정되면 바뀌지 않는다.
        }

        _shownKey = "shown";
        NCPlayer me = NCPlayer.Local;
        bool draw = session.WinnerSlot < 0;
        bool win = draw == false && me != null && me.Slot == session.WinnerSlot;
        Color tone = draw ? NCStyle.Brass : (win ? NCStyle.Success : NCStyle.Danger);

        _band.color = tone;
        _frame.color = tone;
        _title.text = draw ? "무승부" : (win ? "승리" : "패배");
        _title.color = tone;

        switch (session.Cause)
        {
            case NCEndCause.Dominance: _subtitle.text = "지배율 100% 달성"; break;
            case NCEndCause.TimeUp: _subtitle.text = "시간 종료 · 지배율 우세"; break;
            case NCEndCause.Draw: _subtitle.text = "시간 종료 · 지배율 동일"; break;
            case NCEndCause.OpponentLeft: _subtitle.text = "상대 이탈"; break;
            default: _subtitle.text = ""; break;
        }

        // 시간 종료는 소수 차이로도 승부가 나므로 자세히 보여 준다.
        bool precise = session.Cause == NCEndCause.TimeUp || session.Cause == NCEndCause.Draw;
        for (int slot = 0; slot < 2; slot++)
        {
            bool mine = me != null && me.Slot == slot;
            _headers[slot].text = $"{slot + 1}P" + (mine ? " (나)" : "");
            Color back = NCStyle.SlotColor(slot);
            back.a = mine ? 0.16f : 0f;
            _columns[slot].color = back;

            _cells[slot, 0].text = DominanceLabel(session, slot, precise);
            _cells[slot, 1].text = session.Kills[slot].ToString();
            _cells[slot, 2].text = session.Deaths[slot].ToString();
            _cells[slot, 3].text = session.LastHits[slot].ToString();
            _cells[slot, 4].text = NCMath.FormatClock(Mathf.Floor(session.ZoneSeconds[slot]));
            _cells[slot, 5].text = session.Shots[slot] > 0
                ? $"{Mathf.FloorToInt(100f * session.Hits[slot] / session.Shots[slot])}%"
                : "-";
        }

        _time.text = $"남은 시간  {NCMath.FormatClock(session.RemainingSeconds)}";

        bool isHost = session.Runner != null && session.Runner.IsServer;
        _retry.gameObject.SetActive(isHost);
        _waiting.gameObject.SetActive(isHost == false);
        NCUi.PlaySound(draw ? NCSound.Tick : (win ? NCSound.Win : NCSound.Lose));
    }

    private static string DominanceLabel(NCSession session, int slot, bool precise)
    {
        return precise
            ? $"{Mathf.Floor(session.Dominance[slot] * 100f) / 100f:0.00}%"
            : $"{session.DominancePercent(slot)}%";
    }
}
