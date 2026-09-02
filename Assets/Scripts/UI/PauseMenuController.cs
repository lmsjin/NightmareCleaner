using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class PauseMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string startSceneName = "start";
    [SerializeField] private string inGameSceneName = "SampleScene";

    [Header("Canvas UI")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeValueText;
    [SerializeField] private GameObject soundOptionsPanel;
    [SerializeField] private GameObject resolutionOptionsPanel;
    [SerializeField] private GameObject controlOptionsPanel;
    [SerializeField] private GameObject resetConfirmPanel;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown frameRateDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TMP_Text mouseSensitivityValueText;
    [SerializeField] private Image soundTabImage;
    [SerializeField] private Image resolutionTabImage;
    [SerializeField] private Image controlTabImage;
    [SerializeField] private Sprite soundTabOn;
    [SerializeField] private Sprite soundTabOff;
    [SerializeField] private Sprite resolutionTabOn;
    [SerializeField] private Sprite resolutionTabOff;
    [SerializeField] private Sprite controlTabOn;
    [SerializeField] private Sprite controlTabOff;

    // ESC 일시정지 메뉴가 열려있는 동안 true.
    // 구출 미니게임 등 다른 스크립트가 "플레이어가 직접 멈춘 상태"인지 구분하기 위해 참조한다.
    public static bool IsPaused { get; private set; }

    private GameObject pauseCanvasRoot;
    private readonly List<Resolution> availableResolutions = new();

    private void Awake()
    {
        ResolveCanvasRoot();
        ApplyFinalUI();
        float volume = PlayerPrefs.GetFloat("Settings.MasterVolume", AudioListener.volume);
        AudioListener.volume = volume;
        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(volume);
        RefreshVolumeLabel(volume);
        BuildSettingsOptions();
        HideAll();
    }

    private void ApplyFinalUI()
    {
        Transform pauseRoot = pausePanel != null ? pausePanel.transform : transform;
        Transform optionRoot = settingsPanel != null ? settingsPanel.transform : transform;
        UIRealTheme.SkinButton(pauseRoot, "ResumeButton", "[UI]_Pause/Button_Continue");
        UIRealTheme.SkinButton(pauseRoot, "SettingsButton", "[UI]_Pause/Button_Option");
        UIRealTheme.SkinButton(pauseRoot, "ExitButton", "[UI]_Pause/Button_Mainmenu");
        UIRealTheme.ApplyOptionTheme(optionRoot);
        UIRealTheme.SkinToggle(fullscreenToggle);

        soundTabOn = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_SoundON");
        soundTabOff = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_SoundOFF");
        resolutionTabOn = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_GraghicON");
        resolutionTabOff = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_GraghicOFF");
        controlTabOn = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_ControlON");
        controlTabOff = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_ControlOFF");
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        if (settingsPanel != null && settingsPanel.activeSelf)
            ShowPauseMenu();
        else if (IsPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (pauseCanvasRoot != null) pauseCanvasRoot.SetActive(true);
        ShowPauseMenu();
    }

    public void ResumeGame()
    {
        IsPaused = false;
        // 구출 미니게임이 아직 진행 중이라면 월드는 계속 멈춰있어야 하므로
        // 무조건 1f로 복원하지 않고 현재 상태에 맞춰 결정한다.
        Time.timeScale = RescueMiniGameManager.IsActive ? 0f : 1f;
        HideAll();
    }

    public void RestartGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(inGameSceneName);
    }

    public void ExitToStart()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(startSceneName);
    }

    public void ShowPauseMenu()
    {
        if (pauseCanvasRoot != null) pauseCanvasRoot.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void ShowSettings()
    {
        if (pauseCanvasRoot != null) pauseCanvasRoot.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        ShowSoundTab();
    }

    public void ShowSoundTab() => SetOptionTab(soundOptionsPanel, soundTabImage);
    public void ShowResolutionTab() => SetOptionTab(resolutionOptionsPanel, resolutionTabImage);
    public void ShowControlTab() => SetOptionTab(controlOptionsPanel, controlTabImage);

    public void ShowResetConfirm()
    {
        if (resetConfirmPanel != null) resetConfirmPanel.SetActive(true);
    }

    public void HideResetConfirm()
    {
        if (resetConfirmPanel != null) resetConfirmPanel.SetActive(false);
    }

    public void ResetSettings()
    {
        SetMasterVolume(1f);
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(1f);
        if (resolutionDropdown != null) resolutionDropdown.SetValueWithoutNotify(FindCurrentResolutionIndex());
        if (frameRateDropdown != null) frameRateDropdown.SetValueWithoutNotify(1);
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(true);
        fullscreenToggle?.GetComponent<OptionToggleVisual>()?.Refresh(true);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.SetValueWithoutNotify(1f);
        RefreshSensitivityLabel(1f);
        ApplySettings();
        HideResetConfirm();
    }

    public void ApplySettings()
    {
        if (resolutionDropdown != null && availableResolutions.Count > 0)
        {
            int index = Mathf.Clamp(resolutionDropdown.value, 0, availableResolutions.Count - 1);
            Resolution selected = availableResolutions[index];
            bool fullscreen = fullscreenToggle != null && fullscreenToggle.isOn;
            Screen.SetResolution(selected.width, selected.height, fullscreen);
            PlayerPrefs.SetInt("Settings.ResolutionWidth", selected.width);
            PlayerPrefs.SetInt("Settings.ResolutionHeight", selected.height);
            PlayerPrefs.SetInt("Settings.Fullscreen", fullscreen ? 1 : 0);
        }

        if (frameRateDropdown != null)
        {
            int[] frameRates = { 30, 60, 120, -1 };
            int index = Mathf.Clamp(frameRateDropdown.value, 0, frameRates.Length - 1);
            Application.targetFrameRate = frameRates[index];
            PlayerPrefs.SetInt("Settings.FrameRateIndex", index);
        }

        if (mouseSensitivitySlider != null)
            SetMouseSensitivity(mouseSensitivitySlider.value);

        PlayerPrefs.Save();
    }

    public void ApplyAndClose()
    {
        ApplySettings();
        ShowPauseMenu();
    }

    public void SetMasterVolume(float value)
    {
        float volume = Mathf.Clamp01(value);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("Settings.MasterVolume", volume);
        PlayerPrefs.Save();
        RefreshVolumeLabel(volume);
    }

    public void SetMouseSensitivity(float value)
    {
        float sensitivity = Mathf.Clamp(value, 0.2f, 2f);
        PlayerPrefs.SetFloat("Settings.MouseSensitivity", sensitivity);
        RefreshSensitivityLabel(sensitivity);
    }

    private void HideAll()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void ResolveCanvasRoot()
    {
        Transform current = pausePanel != null ? pausePanel.transform : null;
        while (current != null)
        {
            if (current.GetComponent<Canvas>() != null)
            {
                pauseCanvasRoot = current.gameObject;
                pauseCanvasRoot.SetActive(true);
                return;
            }
            current = current.parent;
        }
    }

    private void SetOptionTab(GameObject selected, Image selectedTab)
    {
        if (soundOptionsPanel != null) soundOptionsPanel.SetActive(soundOptionsPanel == selected);
        if (resolutionOptionsPanel != null) resolutionOptionsPanel.SetActive(resolutionOptionsPanel == selected);
        if (controlOptionsPanel != null) controlOptionsPanel.SetActive(controlOptionsPanel == selected);
        if (soundTabImage != null) soundTabImage.sprite = soundTabImage == selectedTab ? soundTabOn : soundTabOff;
        if (resolutionTabImage != null) resolutionTabImage.sprite = resolutionTabImage == selectedTab ? resolutionTabOn : resolutionTabOff;
        if (controlTabImage != null) controlTabImage.sprite = controlTabImage == selectedTab ? controlTabOn : controlTabOff;
    }

    private void BuildSettingsOptions()
    {
        availableResolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            bool duplicate = availableResolutions.Exists(r => r.width == resolution.width && r.height == resolution.height);
            if (!duplicate) availableResolutions.Add(resolution);
        }

        if (availableResolutions.Count == 0)
            availableResolutions.Add(Screen.currentResolution);

        if (resolutionDropdown != null)
        {
            var labels = new List<string>();
            foreach (Resolution resolution in availableResolutions)
                labels.Add($"{resolution.width} x {resolution.height}");
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
            resolutionDropdown.SetValueWithoutNotify(FindCurrentResolutionIndex());
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt("Settings.Fullscreen", Screen.fullScreen ? 1 : 0) == 1);
            fullscreenToggle.GetComponent<OptionToggleVisual>()?.Refresh(fullscreenToggle.isOn);
        }

        if (frameRateDropdown != null)
        {
            frameRateDropdown.ClearOptions();
            frameRateDropdown.AddOptions(new List<string> { "30 FPS", "60 FPS", "120 FPS", "제한 없음" });
            int frameIndex = PlayerPrefs.GetInt("Settings.FrameRateIndex", 1);
            frameRateDropdown.SetValueWithoutNotify(frameIndex);
            int[] frameRates = { 30, 60, 120, -1 };
            Application.targetFrameRate = frameRates[Mathf.Clamp(frameIndex, 0, frameRates.Length - 1)];
        }

        float sensitivity = PlayerPrefs.GetFloat("Settings.MouseSensitivity", 1f);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.SetValueWithoutNotify(sensitivity);
        RefreshSensitivityLabel(sensitivity);
    }

    private int FindCurrentResolutionIndex()
    {
        int width = PlayerPrefs.GetInt("Settings.ResolutionWidth", Screen.width);
        int height = PlayerPrefs.GetInt("Settings.ResolutionHeight", Screen.height);
        int index = availableResolutions.FindIndex(r => r.width == width && r.height == height);
        return index >= 0 ? index : Mathf.Max(0, availableResolutions.Count - 1);
    }

    private void RefreshVolumeLabel(float volume)
    {
        if (masterVolumeValueText != null)
            masterVolumeValueText.text = $"{Mathf.RoundToInt(volume * 100f)}%";
    }

    private void RefreshSensitivityLabel(float sensitivity)
    {
        if (mouseSensitivityValueText != null)
            mouseSensitivityValueText.text = $"{sensitivity:0.0}";
    }

    private void OnDestroy()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }
}