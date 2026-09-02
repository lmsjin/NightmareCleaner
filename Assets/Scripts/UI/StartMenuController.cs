using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class StartMenuController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string inGameSceneName = "SampleScene";

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Manual Popup")]
    [SerializeField] private GameObject manualPopupPanel;

    [Header("Settings UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeValueText;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown frameRateDropdown;
    [SerializeField] private GameObject soundOptionsPanel;
    [SerializeField] private GameObject resolutionOptionsPanel;
    [SerializeField] private GameObject controlOptionsPanel;
    [SerializeField] private GameObject resetConfirmPanel;
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

    private const string VolumeKey = "Settings.MasterVolume";
    private const string FullscreenKey = "Settings.Fullscreen";
    private readonly List<Resolution> availableResolutions = new();

    private void Awake()
    {
        ApplyFinalUI();
        LoadSettings();
        BuildOptions();
        ShowMainMenu();

        if (manualPopupPanel != null)
            manualPopupPanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (manualPopupPanel != null && manualPopupPanel.activeSelf)
                HideManualPopup();
        }
    }

    // ───── Manual Popup ─────
    public void ShowManualPopup()
    {
        if (manualPopupPanel != null)
            manualPopupPanel.SetActive(true);
    }

    public void HideManualPopup()
    {
        if (manualPopupPanel != null)
            manualPopupPanel.SetActive(false);
    }

    private void ApplyFinalUI()
    {
        Transform mainRoot = mainPanel != null ? mainPanel.transform : transform;
        Transform optionRoot = settingsPanel != null ? settingsPanel.transform : transform;
        UIRealTheme.SkinButton(mainRoot, "PlayButton", "[UI]_MainLoby/Button_Gamestart");
        UIRealTheme.SkinButton(mainRoot, "OptionButton", "[UI]_MainLoby/Button_Option");
        UIRealTheme.SkinButton(mainRoot, "ExitButton", "[UI]_MainLoby/Button_Exit");
        UIRealTheme.ApplyOptionTheme(optionRoot);
        UIRealTheme.SkinToggle(fullscreenToggle);

        soundTabOn = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_SoundON");
        soundTabOff = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_SoundOFF");
        resolutionTabOn = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_GraghicON");
        resolutionTabOff = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_GraghicOFF");
        controlTabOn = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_ControlON");
        controlTabOff = UIRealTheme.LoadSprite("[UI]_Option/OptionTabButton_ControlOFF");
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(inGameSceneName);
    }

    public void ShowSettings()
    {
        SetPanelState(false, true);
        ShowSoundTab();
    }

    public void ShowMainMenu()
    {
        SetPanelState(true, false);
    }

    public void SetMasterVolume(float value)
    {
        float volume = Mathf.Clamp01(value);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(VolumeKey, volume);
        PlayerPrefs.Save();
        RefreshVolumeLabel(volume);
    }

    public void SetFullscreen(bool enabled)
    {
        Screen.fullScreen = enabled;
        PlayerPrefs.SetInt(FullscreenKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ShowSoundTab() => SetTab(soundOptionsPanel, soundTabImage);
    public void ShowResolutionTab() => SetTab(resolutionOptionsPanel, resolutionTabImage);
    public void ShowControlTab() => SetTab(controlOptionsPanel, controlTabImage);
    public void ShowResetConfirm() { if (resetConfirmPanel != null) resetConfirmPanel.SetActive(true); }
    public void HideResetConfirm() { if (resetConfirmPanel != null) resetConfirmPanel.SetActive(false); }

    public void ApplySettings()
    {
        if (resolutionDropdown != null && availableResolutions.Count > 0)
        {
            Resolution selected = availableResolutions[Mathf.Clamp(resolutionDropdown.value, 0, availableResolutions.Count - 1)];
            bool fullscreen = fullscreenToggle != null && fullscreenToggle.isOn;
            Screen.SetResolution(selected.width, selected.height, fullscreen);
            PlayerPrefs.SetInt("Settings.ResolutionWidth", selected.width);
            PlayerPrefs.SetInt("Settings.ResolutionHeight", selected.height);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
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

    public void ApplyAndClose() { ApplySettings(); ShowMainMenu(); }

    public void ResetSettings()
    {
        SetMasterVolume(1f);
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(1f);
        if (resolutionDropdown != null) resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, availableResolutions.Count - 1));
        if (frameRateDropdown != null) frameRateDropdown.SetValueWithoutNotify(1);
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(true);
        fullscreenToggle?.GetComponent<OptionToggleVisual>()?.Refresh(true);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.SetValueWithoutNotify(1f);
        SetMouseSensitivity(1f);
        ApplySettings();
        HideResetConfirm();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadSettings()
    {
        float volume = PlayerPrefs.GetFloat(VolumeKey, AudioListener.volume);
        bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

        AudioListener.volume = volume;
        Screen.fullScreen = fullscreen;

        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(volume);

        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);

        RefreshVolumeLabel(volume);
    }

    private void SetPanelState(bool showMain, bool showSettingsPanel)
    {
        if (mainPanel != null)
            mainPanel.SetActive(showMain);

        if (settingsPanel != null)
            settingsPanel.SetActive(showSettingsPanel);
    }

    private void RefreshVolumeLabel(float volume)
    {
        if (masterVolumeValueText != null)
            masterVolumeValueText.text = $"{Mathf.RoundToInt(volume * 100f)}%";
    }

    private void SetTab(GameObject selected, Image selectedTab)
    {
        if (soundOptionsPanel != null) soundOptionsPanel.SetActive(soundOptionsPanel == selected);
        if (resolutionOptionsPanel != null) resolutionOptionsPanel.SetActive(resolutionOptionsPanel == selected);
        if (controlOptionsPanel != null) controlOptionsPanel.SetActive(controlOptionsPanel == selected);
        if (soundTabImage != null) soundTabImage.sprite = soundTabImage == selectedTab ? soundTabOn : soundTabOff;
        if (resolutionTabImage != null) resolutionTabImage.sprite = resolutionTabImage == selectedTab ? resolutionTabOn : resolutionTabOff;
        if (controlTabImage != null) controlTabImage.sprite = controlTabImage == selectedTab ? controlTabOn : controlTabOff;
    }

    private void BuildOptions()
    {
        availableResolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
            if (!availableResolutions.Exists(r => r.width == resolution.width && r.height == resolution.height))
                availableResolutions.Add(resolution);
        if (availableResolutions.Count == 0) availableResolutions.Add(Screen.currentResolution);
        if (resolutionDropdown != null)
        {
            var labels = new List<string>();
            foreach (Resolution resolution in availableResolutions) labels.Add($"{resolution.width} x {resolution.height}");
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(labels);
            int width = PlayerPrefs.GetInt("Settings.ResolutionWidth", Screen.width);
            int height = PlayerPrefs.GetInt("Settings.ResolutionHeight", Screen.height);
            int index = availableResolutions.FindIndex(r => r.width == width && r.height == height);
            resolutionDropdown.SetValueWithoutNotify(index >= 0 ? index : availableResolutions.Count - 1);
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
        if (fullscreenToggle != null)
            fullscreenToggle.GetComponent<OptionToggleVisual>()?.Refresh(fullscreenToggle.isOn);
    }

    public void SetMouseSensitivity(float value)
    {
        float sensitivity = Mathf.Clamp(value, 0.2f, 2f);
        PlayerPrefs.SetFloat("Settings.MouseSensitivity", sensitivity);
        RefreshSensitivityLabel(sensitivity);
    }

    private void RefreshSensitivityLabel(float sensitivity)
    {
        if (mouseSensitivityValueText != null) mouseSensitivityValueText.text = $"{sensitivity:0.0}";
    }
}