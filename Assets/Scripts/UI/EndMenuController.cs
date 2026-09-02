using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string inGameSceneName = "SampleScene";
    [SerializeField] private string startSceneName = "start";

    [Header("Canvas UI")]
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Color winTitleColor = new(0.35f, 0.9f, 0.45f);
    [SerializeField] private Color loseTitleColor = new(1f, 0.35f, 0.35f);

    [Header("Result Background")]
    [SerializeField] private GameObject clearBackgroundPanel;
    [SerializeField] private GameObject gameOverBackgroundPanel;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite clearBackgroundSprite;
    [SerializeField] private Sprite gameOverBackgroundSprite;

    [Header("Readability Background")]
    [SerializeField, Range(0f, 1f)] private float bgPanelAlpha = 0.65f;
    [SerializeField] private Image[] bgPanelImages;

    private void Awake()
    {
        UIRealTheme.SkinButton(transform, "RetryButton", "[UI]_Gameover/Button_Retry");
        UIRealTheme.SkinButton(transform, "StartButton", "[UI]_Gameover/Button_Mainmenu");

        ApplyResultBackground(GameResult.Win);
        ApplyBgPanelOpacity();
        BringResultTextToFront();

        if (resultTitleText != null)
        {
            resultTitleText.text = GameResult.Win ? "클리어!" : "GAME OVER";
            resultTitleText.color = GameResult.Win ? winTitleColor : loseTitleColor;
        }

        if (scoreText != null)
            scoreText.text = $"점수: {GameResult.Score}";
    }

    private void ApplyResultBackground(bool isWin)
    {
        if (clearBackgroundPanel != null)
        {
            clearBackgroundPanel.SetActive(isWin);
            SendBehindUi(clearBackgroundPanel);
        }
        if (gameOverBackgroundPanel != null)
        {
            gameOverBackgroundPanel.SetActive(!isWin);
            SendBehindUi(gameOverBackgroundPanel);
        }

        if (backgroundImage == null)
        {
            GameObject backgroundObject = GameObject.Find("Background");
            if (backgroundObject != null)
                backgroundImage = backgroundObject.GetComponent<Image>();
        }

        Sprite resultSprite = isWin ? clearBackgroundSprite : gameOverBackgroundSprite;
        if (backgroundImage != null && resultSprite != null)
            backgroundImage.sprite = resultSprite;

        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = false;
            SendBehindUi(backgroundImage.gameObject);
        }
    }

    private static void SendBehindUi(GameObject panel)
    {
        if (panel == null) return;
        panel.transform.SetAsFirstSibling();

        foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }

    private void ApplyBgPanelOpacity()
    {
        Image[] targets = bgPanelImages != null && bgPanelImages.Length > 0
            ? bgPanelImages
            : FindNamedBgImages();

        foreach (Image image in targets)
        {
            if (image == null || image == backgroundImage) continue;

            Color color = image.color;
            color.a = bgPanelAlpha;
            image.color = color;
            image.raycastTarget = false;
        }
    }

    private Image[] FindNamedBgImages()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        return System.Array.FindAll(images, image =>
            image != null &&
            image.gameObject != null &&
            image.gameObject.name.ToLowerInvariant().Contains("bg"));
    }

    private void BringResultTextToFront()
    {
        if (resultTitleText != null)
            resultTitleText.transform.SetAsLastSibling();
        if (scoreText != null)
            scoreText.transform.SetAsLastSibling();
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(inGameSceneName);
    }

    public void GoToStart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(startSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
