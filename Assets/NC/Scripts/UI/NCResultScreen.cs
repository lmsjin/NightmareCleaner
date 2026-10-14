using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과 화면(UI 기획서 14장). 같은 패널 구조에서 성공·실패의 색과 문구만 바꾼다.
///  - 소탕 완료 수 · 완료(성공)/경과(실패) 시간 · 호스트 PC 최고 기록(없으면 "기록 없음") · 팀 다운 횟수
///  - 호스트: [재도전] [나가기] / 참가자: 호스트 대기 안내 + [나가기]
/// </summary>
public class NCResultScreen
{
    private readonly GameObject _root;
    private readonly Image _band;
    private readonly TextMeshProUGUI _title;
    private readonly TextMeshProUGUI _subtitle;
    private readonly TextMeshProUGUI _cleared;
    private readonly TextMeshProUGUI _timeLabel;
    private readonly TextMeshProUGUI _time;
    private readonly TextMeshProUGUI _best;
    private readonly TextMeshProUGUI _downs;
    private readonly TextMeshProUGUI _note;
    private readonly GameObject _newRecord;
    private readonly Button _retry;
    private readonly TextMeshProUGUI _waiting;
    private readonly Image _frame;

    public NCResultScreen(RectTransform canvas)
    {
        RectTransform root = NCUiKit.NewRect("ResultScreen", canvas);
        NCUiKit.Stretch(root);
        _root = root.gameObject;

        Image dim = NCUiKit.NewImage("Dim", root, null, NCStyle.Dim);
        NCUiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        RectTransform panel = NCUiKit.NewPanel("Panel", root, deep: true);
        NCUiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640f, 620f));
        _frame = panel.Find("Frame").GetComponent<Image>();

        // 상단 색 띠 + 제목
        _band = NCUiKit.NewImage("Band", panel, NCUiKit.RoundSprite, NCStyle.Success);
        NCUiKit.Place(_band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(560f, 8f));

        _title = NCUiKit.NewText("Title", panel, "의뢰 완료", 60f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(580f, 76f));

        _subtitle = NCUiKit.NewText("Subtitle", panel, "", NCStyle.FontBody, NCStyle.TextDim);
        NCUiKit.Place(_subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(580f, 32f));

        // 기록 표
        RectTransform table = NCUiKit.NewPanel("Table", panel);
        NCUiKit.Place(table, new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(560f, 236f));

        _cleared = Row(table, 0, "소탕 완료", out _);
        _time = Row(table, 1, "완료 시간", out _timeLabel);
        _best = Row(table, 2, "호스트 PC 최고 기록", out _);
        _downs = Row(table, 3, "팀 다운 횟수", out _);

        RectTransform badge = NCUiKit.NewImage("NewRecord", table, NCUiKit.RoundSprite, NCStyle.P2).rectTransform;
        NCUiKit.Place(badge, new Vector2(1f, 1f), new Vector2(-12f, 14f), new Vector2(110f, 30f));
        TextMeshProUGUI badgeText = NCUiKit.NewText("Text", badge, "신기록!", 18f, NCStyle.PanelDeep, style: FontStyles.Bold);
        NCUiKit.Stretch(badgeText.rectTransform);
        _newRecord = badge.gameObject;

        _note = NCUiKit.NewText("Note", panel, "실패한 판은 기록에 등록되지 않습니다", 18f, NCStyle.TextDim);
        NCUiKit.Place(_note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -424f), new Vector2(560f, 26f));

        // 버튼
        _retry = NCUiKit.NewButton("Retry", panel, "재도전", true, () => NCSession.Instance?.RequestRetry());
        NCUiKit.Place((RectTransform)_retry.transform, new Vector2(0.5f, 0f), new Vector2(-140f, 40f), new Vector2(250f, 68f));

        Button leave = NCUiKit.NewButton("Leave", panel, "나가기", false, () => NCLauncher.Instance?.Leave());
        NCUiKit.Place((RectTransform)leave.transform, new Vector2(0.5f, 0f), new Vector2(140f, 40f), new Vector2(250f, 68f));

        _waiting = NCUiKit.NewText("Waiting", panel, "호스트가 재도전을 선택하기를 기다리는 중...", 20f, NCStyle.TextDim);
        NCUiKit.Place(_waiting.rectTransform, new Vector2(0.5f, 0f), new Vector2(-140f, 58f), new Vector2(300f, 30f));
        _waiting.textWrappingMode = TextWrappingModes.Normal;
        _waiting.rectTransform.sizeDelta = new Vector2(260f, 60f);
    }

    private static TextMeshProUGUI Row(RectTransform table, int index, string label, out TextMeshProUGUI labelText)
    {
        float y = -20f - index * 52f;

        labelText = NCUiKit.NewText("Label" + index, table, "◆  " + label, 24f, NCStyle.TextDim, TextAlignmentOptions.Left);
        NCUiKit.Place(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(24f, y), new Vector2(300f, 40f));

        TextMeshProUGUI value = NCUiKit.NewText("Value" + index, table, "", 32f, NCStyle.Text, TextAlignmentOptions.Right, FontStyles.Bold);
        NCUiKit.Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, y), new Vector2(240f, 40f));

        if (index < 3)
        {
            Image line = NCUiKit.NewImage("Line" + index, table, null, NCStyle.BrassDim);
            NCUiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y - 46f), new Vector2(512f, 1f));
        }

        return value;
    }

    public void SetVisible(bool visible)
    {
        if (_root.activeSelf != visible)
        {
            _root.SetActive(visible);
        }
    }

    // 결과 값이 바뀔 때만 다시 그린다(매 프레임 문자열 생성 방지).
    private long _shownKey = -1;

    public void Tick(NCSession session, NCLauncher launcher)
    {
        long key = (long)session.Phase
                   | (long)session.Cause << 4
                   | (long)session.ClearedRooms << 8
                   | (long)session.TotalRooms << 14
                   | (long)session.TeamDownCount << 20
                   | (session.IsNewRecord ? 1L : 0L) << 30
                   | (long)session.ElapsedCentisec << 31;
        key ^= (long)session.BestCentisec * 1000003L;

        if (key == _shownKey)
        {
            return;
        }

        _shownKey = key;
        bool victory = session.Phase == NCPhase.Victory;
        Color tone = victory ? NCStyle.Success : NCStyle.Danger;

        _band.color = tone;
        _frame.color = victory ? NCStyle.Brass : NCStyle.Danger;
        _title.text = victory ? "의뢰 완료" : "의뢰 실패";
        _title.color = victory ? NCStyle.Text : NCStyle.Hex("FFB4B8");

        _subtitle.text = victory
            ? "모든 전투방 소탕 완료"
            : (session.Cause == NCDefeatCause.TimeUp
                ? "<color=#E5484D><b>시간 초과</b></color> · 제한 시간 안에 소탕하지 못했습니다"
                : "<color=#E5484D><b>전원 다운</b></color> · 두 명 모두 쓰러졌습니다");

        _cleared.text = $"{session.ClearedRooms}/{session.TotalRooms}";
        _timeLabel.text = victory ? "◆  완료 시간" : "◆  경과 시간";
        _time.text = NCMath.FormatCentisec(session.ElapsedCentisec);
        _best.text = session.BestCentisec > 0 ? NCMath.FormatCentisec(session.BestCentisec) : "<color=#F4F1E799>기록 없음</color>";
        _downs.text = $"{session.TeamDownCount}회";
        _newRecord.SetActive(victory && session.IsNewRecord);
        _note.gameObject.SetActive(victory == false);

        bool isHost = session.Runner != null && session.Runner.IsServer;
        _retry.gameObject.SetActive(isHost);
        _waiting.gameObject.SetActive(isHost == false);
    }
}
