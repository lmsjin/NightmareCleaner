using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 메뉴(UI 기획서 15장). Esc로 열고 닫는다.
///  - 멀티 게임이라 시간을 멈추지 않는다(Time.timeScale 변경 없음). 메뉴를 연 사람의 게임 입력만 막는다.
///  - [계속하기] / [나가기] → 확인 팝업(기본 선택은 취소).
/// 설정(음량)은 이번 범위 밖.
/// </summary>
public class NCMenuScreen
{
    private readonly GameObject _root;
    private readonly NCConfirmPopup _confirm;

    public bool IsOpen => _root.activeSelf;

    public NCMenuScreen(RectTransform canvas, NCConfirmPopup confirm)
    {
        _confirm = confirm;

        RectTransform root = NCUiKit.NewRect("MenuScreen", canvas);
        NCUiKit.Stretch(root);
        _root = root.gameObject;

        Image dim = NCUiKit.NewImage("Dim", root, null, NCStyle.Dim);
        NCUiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        RectTransform panel = NCUiKit.NewPanel("Panel", root, deep: true);
        NCUiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 420f));
        panel.Find("Frame").GetComponent<Image>().color = NCStyle.Brass;

        TextMeshProUGUI title = NCUiKit.NewText("Title", panel, "게임 메뉴", 44f, NCStyle.Text, style: FontStyles.Bold);
        NCUiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(400f, 56f));

        TextMeshProUGUI warning = NCUiKit.NewText("Warning", panel, "●  온라인 게임은 계속 진행됩니다", 20f, NCStyle.P2);
        NCUiKit.Place(warning.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(400f, 30f));

        Image line = NCUiKit.NewImage("Line", panel, null, NCStyle.BrassDim);
        NCUiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(400f, 1f));

        Button resume = NCUiKit.NewButton("Resume", panel, "계속하기", true, Close);
        NCUiKit.Place((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0f, -168f), new Vector2(340f, 64f));

        Button leave = NCUiKit.NewButton("Leave", panel, "나가기", false, AskLeave);
        NCUiKit.Place((RectTransform)leave.transform, new Vector2(0.5f, 1f), new Vector2(0f, -248f), new Vector2(340f, 64f));

        RectTransform hint = NCUiKit.NewRect("Hint", panel);
        NCUiKit.Place(hint, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(200f, 34f));
        RectTransform key = NCUiKit.NewKeyCap("Esc", hint, "Esc", 30f);
        NCUiKit.Place(key, new Vector2(0f, 0.5f), Vector2.zero, key.sizeDelta);
        TextMeshProUGUI hintText = NCUiKit.NewText("Text", hint, "메뉴 닫기", 20f, NCStyle.TextDim, TextAlignmentOptions.Left);
        NCUiKit.Place(hintText.rectTransform, new Vector2(0f, 0.5f), new Vector2(key.sizeDelta.x + 10f, 0f), new Vector2(140f, 30f));

        _root.SetActive(false);
    }

    public void Open()
    {
        _root.SetActive(true);
    }

    public void Close()
    {
        _root.SetActive(false);
    }

    private void AskLeave()
    {
        _confirm.Show("나가면 현재 경기를 포기합니다.\n나가시겠습니까?", "나가기", () =>
        {
            Close();
            NCLauncher.Instance?.Leave();
        });
    }
}

/// <summary>
/// 공용 확인 팝업(UI 기획서 18장: 나가기 확인 · 기본 선택은 취소 · 아래 화면 클릭 차단).
/// 대기실 나가기와 게임 메뉴 나가기가 함께 쓴다.
/// </summary>
public class NCConfirmPopup
{
    private readonly GameObject _root;
    private readonly TextMeshProUGUI _message;
    private readonly TextMeshProUGUI _confirmLabel;
    private readonly Button _cancel;
    private Action _onConfirm;

    public bool IsOpen => _root.activeSelf;

    public NCConfirmPopup(RectTransform canvas)
    {
        RectTransform root = NCUiKit.NewRect("ConfirmPopup", canvas);
        NCUiKit.Stretch(root);
        _root = root.gameObject;

        Image dim = NCUiKit.NewImage("Dim", root, null, new Color(0f, 0f, 0f, 0.5f));
        NCUiKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true; // 아래 화면 클릭 차단

        RectTransform panel = NCUiKit.NewPanel("Panel", root, deep: true);
        NCUiKit.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 260f));
        panel.Find("Frame").GetComponent<Image>().color = NCStyle.Brass;

        _message = NCUiKit.NewText("Message", panel, "", NCStyle.FontGuide, NCStyle.Text);
        NCUiKit.Place(_message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(500f, 100f));
        _message.textWrappingMode = TextWrappingModes.Normal;

        _cancel = NCUiKit.NewButton("Cancel", panel, "취소", true, Hide);
        NCUiKit.Place((RectTransform)_cancel.transform, new Vector2(0.5f, 0f), new Vector2(-120f, 30f), new Vector2(210f, 62f));

        Button confirm = NCUiKit.NewButton("Confirm", panel, "확인", false, Confirm);
        NCUiKit.Place((RectTransform)confirm.transform, new Vector2(0.5f, 0f), new Vector2(120f, 30f), new Vector2(210f, 62f));
        _confirmLabel = confirm.transform.Find("Label").GetComponent<TextMeshProUGUI>();

        _root.SetActive(false);
    }

    public void Show(string message, string confirmLabel, Action onConfirm)
    {
        _message.text = message;
        _confirmLabel.text = confirmLabel;
        _onConfirm = onConfirm;
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        _cancel.Select(); // 기본 포커스는 취소
    }

    public void Hide()
    {
        _root.SetActive(false);
        _onConfirm = null;
    }

    private void Confirm()
    {
        Action action = _onConfirm;
        Hide();
        action?.Invoke();
    }
}
