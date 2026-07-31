using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using cowsins;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance;

    [Header("UIDocument")]
    public UIDocument uiDocument;

    [Header("Main Menu")]
    public string mainMenuSceneName = "MainMenu";

    public bool IsPaused { get; private set; }
    private float _transitionEndTime = 0f;
    public bool IsTransitioning => Time.realtimeSinceStartup < _transitionEndTime;
    public bool IsOpenOrTransitioning => IsPaused || IsTransitioning;

    private PlayerControl playerControl;
    private Transform _canvasRoot;

    [Header("Audio")]
    public AudioClip hoverSFX;
    public AudioClip clickSFX;

    private VisualElement _pausePanel;
    private VisualElement _pauseCard;
    private Button _resumeButton;
    private Button _mainMenuButton;
    private Button _quitButton;

    private bool _uiReady;
    private bool _slidersRegistered;
    private cowsins.InputManager _cowsinsInputManager;
    private int _lastEscapeFrame;

    private Coroutine _resumeCoroutine;

    private Button _settingsButton;
    private VisualElement _settingsCard;
    private Button _settingsBackButton;
    private Button _settingsResetButton;

    private Slider _volumeSlider;
    private Slider _musicVolumeSlider;
    private Slider _sfxVolumeSlider;
    private Slider _mouseSensXSlider;
    private Slider _mouseSensYSlider;
    private Slider _controllerSensXSlider;
    private Slider _controllerSensYSlider;
    private Toggle _fullscreenToggle;
    private Toggle _vsyncToggle;

    private Label _volumeValueLabel;
    private Label _musicVolumeValueLabel;
    private Label _sfxVolumeValueLabel;
    private Label _mouseSensXValueLabel;
    private Label _mouseSensYValueLabel;
    private Label _controllerSensXValueLabel;
    private Label _controllerSensYValueLabel;

#if UNITY_EDITOR
    private void Reset()
    {
        if (hoverSFX == null) hoverSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Engine/Cowsins/SFX/UI/UIHover_SFX.wav");
        if (clickSFX == null) clickSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Engine/Cowsins/SFX/UI/clickSFX.wav");
    }
#endif

    private void Awake()
    {
        Instance = this;

        if (PanelManager.Instance == null)
        {
            var pm = FindAnyObjectByType<PanelManager>();
            if (pm == null)
            {
                gameObject.AddComponent<PanelManager>();
                Debug.Log("[PauseManager] Added PanelManager component automatically to ensure centralized UI state.");
            }
        }

        if (FindAnyObjectByType<StatsPanelUI>() == null)
        {
            gameObject.AddComponent<StatsPanelUI>();
            Debug.Log("[PauseManager] Added StatsPanelUI component automatically to host Statistics UI.");
        }

        if (FindAnyObjectByType<PlayerStatsTracker>() == null)
        {
            gameObject.AddComponent<PlayerStatsTracker>();
            Debug.Log("[PauseManager] Added PlayerStatsTracker component automatically to track player runtime statistics.");
        }
    }

    private void OnEnable()
    {
        SetupUI();
    }

    private void Start()
    {
        _canvasRoot = GameObject.Find("GameUICanvas")?.transform;

        // Find and disable all active and inactive scene instances of Cowsins' built-in PauseMenu to prevent input/cursor conflicts
        var allPauseMenus = Resources.FindObjectsOfTypeAll<cowsins.PauseMenu>();
        foreach (var pm in allPauseMenus)
        {
            if (pm.gameObject.scene.name != null)
            {
                Debug.Log("[PauseManager Debug] Disabling cowsins.PauseMenu on scene GameObject: " + pm.gameObject.name);
                pm.enabled = false;
            }
        }

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
            playerControl = p.GetComponentInChildren<PlayerControl>();

        if (!IsPaused && (GameOverManager.Instance == null || !GameOverManager.Instance.IsGameOver))
        {
            if (PanelManager.Instance != null) PanelManager.Instance.ForceLockMouse();
        }

        SubscribeToInputManager();
    }

    private void OnDisable()
    {
        if (_resumeButton != null) _resumeButton.clicked -= Resume;
        if (_settingsButton != null) _settingsButton.clicked -= OpenSettings;
        if (_mainMenuButton != null) _mainMenuButton.clicked -= GoToMainMenu;
        if (_quitButton != null) _quitButton.clicked -= QuitGame;
        if (_settingsBackButton != null) _settingsBackButton.clicked -= CloseSettings;
        if (_settingsResetButton != null) _settingsResetButton.clicked -= ResetSettingsToDefault;

        if (_pauseCard != null)
        {
            _pauseCard.generateVisualContent -= OnGenerateCardBackground;
        }
        if (_settingsCard != null)
        {
            _settingsCard.generateVisualContent -= OnGenerateCardBackground;
        }
        UnsubscribeFromInputManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromInputManager();
    }

    private void SubscribeToInputManager()
    {
        if (_cowsinsInputManager == null)
        {
            _cowsinsInputManager = FindAnyObjectByType<cowsins.InputManager>();
            if (_cowsinsInputManager != null)
            {
                _cowsinsInputManager.OnTogglePause += HandleEscapeInput;
                Debug.Log("[PauseManager Debug] Successfully subscribed to cowsins.InputManager.OnTogglePause");
            }
        }
    }

    private void UnsubscribeFromInputManager()
    {
        if (_cowsinsInputManager != null)
        {
            _cowsinsInputManager.OnTogglePause -= HandleEscapeInput;
            _cowsinsInputManager = null;
            Debug.Log("[PauseManager Debug] Unsubscribed from cowsins.InputManager.OnTogglePause");
        }
    }

    private void SetupUI()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            var canvasGo = GameObject.Find("GameUICanvas");
            if (canvasGo != null) uiDocument = canvasGo.GetComponent<UIDocument>();
        }
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[PauseManager] No UIDocument found! PausePanel will not function.");
            return;
        }

        var root = uiDocument.rootVisualElement;
        _pausePanel = root.Q("PausePanel");
        if (_pausePanel == null)
        {
            Debug.LogError("[PauseManager] PausePanel not found in UXML!");
            return;
        }

        _pauseCard = _pausePanel.Q("PauseCard");
        _resumeButton = _pausePanel.Q<Button>("ResumeButton");
        _settingsButton = _pausePanel.Q<Button>("SettingsButton");
        _mainMenuButton = _pausePanel.Q<Button>("PauseMainMenuButton");
        _quitButton = _pausePanel.Q<Button>("PauseQuitButton");

        // Settings Elements
        _settingsCard = _pausePanel.Q("SettingsCard");
        _settingsBackButton = _pausePanel.Q<Button>("SettingsBackButton");
        _settingsResetButton = _pausePanel.Q<Button>("SettingsResetButton");
        _volumeSlider = _pausePanel.Q<Slider>("VolumeSlider");
        _musicVolumeSlider = _pausePanel.Q<Slider>("MusicVolumeSlider");
        _sfxVolumeSlider = _pausePanel.Q<Slider>("SFXVolumeSlider");
        _mouseSensXSlider = _pausePanel.Q<Slider>("MouseSensXSlider");
        _mouseSensYSlider = _pausePanel.Q<Slider>("MouseSensYSlider");
        _controllerSensXSlider = _pausePanel.Q<Slider>("ControllerSensXSlider");
        _controllerSensYSlider = _pausePanel.Q<Slider>("ControllerSensYSlider");
        _fullscreenToggle = _pausePanel.Q<Toggle>("FullscreenToggle");
        _vsyncToggle = _pausePanel.Q<Toggle>("VsyncToggle");

        _volumeValueLabel = _pausePanel.Q<Label>("VolumeValueLabel");
        _musicVolumeValueLabel = _pausePanel.Q<Label>("MusicVolumeValueLabel");
        _sfxVolumeValueLabel = _pausePanel.Q<Label>("SFXVolumeValueLabel");
        _mouseSensXValueLabel = _pausePanel.Q<Label>("MouseSensXValueLabel");
        _mouseSensYValueLabel = _pausePanel.Q<Label>("MouseSensYValueLabel");
        _controllerSensXValueLabel = _pausePanel.Q<Label>("ControllerSensXValueLabel");
        _controllerSensYValueLabel = _pausePanel.Q<Label>("ControllerSensYValueLabel");

        // Force enable progress fill and update value labels at runtime.
        // Only register once to avoid callback accumulation on re-enable.
        if (!_slidersRegistered)
        {
            if (_volumeSlider != null)
            {
                _volumeSlider.fill = true;
                _volumeSlider.RegisterValueChangedCallback(evt => {
                    if (_volumeValueLabel != null) _volumeValueLabel.text = $"{(evt.newValue * 100f):F0}%";
                });
            }
            if (_musicVolumeSlider != null)
            {
                _musicVolumeSlider.fill = true;
                _musicVolumeSlider.RegisterValueChangedCallback(evt => {
                    if (_musicVolumeValueLabel != null) _musicVolumeValueLabel.text = $"{(evt.newValue * 100f):F0}%";
                });
            }
            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.fill = true;
                _sfxVolumeSlider.RegisterValueChangedCallback(evt => {
                    if (_sfxVolumeValueLabel != null) _sfxVolumeValueLabel.text = $"{(evt.newValue * 100f):F0}%";
                });
            }
            if (_mouseSensXSlider != null)
            {
                _mouseSensXSlider.fill = true;
                _mouseSensXSlider.RegisterValueChangedCallback(evt => {
                    if (_mouseSensXValueLabel != null) _mouseSensXValueLabel.text = $"{evt.newValue:F1}";
                });
            }
            if (_mouseSensYSlider != null)
            {
                _mouseSensYSlider.fill = true;
                _mouseSensYSlider.RegisterValueChangedCallback(evt => {
                    if (_mouseSensYValueLabel != null) _mouseSensYValueLabel.text = $"{evt.newValue:F1}";
                });
            }
            if (_controllerSensXSlider != null)
            {
                _controllerSensXSlider.fill = true;
                _controllerSensXSlider.RegisterValueChangedCallback(evt => {
                    if (_controllerSensXValueLabel != null) _controllerSensXValueLabel.text = $"{evt.newValue:F0}";
                });
            }
            if (_controllerSensYSlider != null)
            {
                _controllerSensYSlider.fill = true;
                _controllerSensYSlider.RegisterValueChangedCallback(evt => {
                    if (_controllerSensYValueLabel != null) _controllerSensYValueLabel.text = $"{evt.newValue:F0}";
                });
            }
            _slidersRegistered = true;
        }

        _pausePanel.style.display = DisplayStyle.None;

        if (_pauseCard != null)
        {
            _pauseCard.generateVisualContent += OnGenerateCardBackground;
        }

        if (_settingsCard != null)
        {
            _settingsCard.generateVisualContent += OnGenerateCardBackground;
        }

        if (_resumeButton != null)
            _resumeButton.clicked += Resume;
        if (_settingsButton != null)
            _settingsButton.clicked += OpenSettings;
        if (_mainMenuButton != null)
            _mainMenuButton.clicked += GoToMainMenu;
        if (_quitButton != null)
            _quitButton.clicked += QuitGame;
        if (_settingsBackButton != null)
            _settingsBackButton.clicked += CloseSettings;
        if (_settingsResetButton != null)
            _settingsResetButton.clicked += ResetSettingsToDefault;

        // Hook up hover and click audio feedback
        HookUpAudio(_resumeButton);
        HookUpAudio(_settingsButton);
        HookUpAudio(_mainMenuButton);
        HookUpAudio(_quitButton);
        HookUpAudio(_settingsBackButton);
        HookUpAudio(_settingsResetButton);
        HookUpAudio(_volumeSlider);
        HookUpAudio(_musicVolumeSlider);
        HookUpAudio(_sfxVolumeSlider);
        HookUpAudio(_mouseSensXSlider);
        HookUpAudio(_mouseSensYSlider);
        HookUpAudio(_controllerSensXSlider);
        HookUpAudio(_controllerSensYSlider);
        HookUpAudio(_fullscreenToggle);
        HookUpAudio(_vsyncToggle);

        _uiReady = true;
    }

    private void Update()
    {
        SubscribeToInputManager();

        if (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver)
            return;

        if (IsAnyCutscenePlaying())
            return;

        // Only poll escape as fallback when InputManager subscription is not active.
        if (_cowsinsInputManager == null)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                HandleEscapeInput();
            }
        }
    }

    private bool IsAnyCutscenePlaying()
    {
        var players = FindObjectsByType<CutscenePlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != null && p.IsPlaying) return true;
        }
        return false;
    }

    private void HandleEscapeInput()
    {
        if (Time.frameCount == _lastEscapeFrame) return;
        _lastEscapeFrame = Time.frameCount;

        if (PanelManager.Instance != null)
        {
            if (PanelManager.Instance.IsPanelActive("GameOver")) return;
            if (PanelManager.Instance.IsAnyPanelTransitioning()) return;

            // Try to close active panel generically (e.g. SkillTree, Journal, Stats)
            if (PanelManager.Instance.CloseActivePanel())
            {
                return;
            }
        }
        else
        {
            if (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver)
                return;
            if (IsTransitioning) return;

            var skillTree = FindAnyObjectByType<SkillTreeWidget>();
            bool skillTreeActive = skillTree != null && skillTree.IsOpenOrTransitioning;
            bool journalActive = JournalUI.Instance != null && JournalUI.Instance.IsOpenOrTransitioning;

            var statsPanel = FindAnyObjectByType<StatsPanelUI>();
            bool statsActive = statsPanel != null && statsPanel.IsOpen;

            if (skillTreeActive)
            {
                if (skillTree.IsOpen && !skillTree.IsTransitioning)
                {
                    skillTree.Close();
                }
                return;
            }
            if (journalActive)
            {
                if (JournalUI.Instance.IsOpen && !JournalUI.Instance.IsTransitioning)
                {
                    JournalUI.Instance.Close();
                }
                return;
            }
            if (statsActive)
            {
                statsPanel.Toggle();
                return;
            }
        }

        // Check if Settings Card is open
        if (IsPaused && _settingsCard != null && _settingsCard.ClassListContains("active"))
        {
            CloseSettings();
            return;
        }

        Toggle();
    }

    public void Toggle()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (!_uiReady || IsTransitioning) return;
        IsPaused = true;
        _transitionEndTime = Time.realtimeSinceStartup + PanelManager.PanelTransitionDuration;

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.OpenPanel("Pause", _pausePanel, _pauseCard);
        }

        // Find and disable all active and inactive scene instances of Cowsins' built-in PauseMenu to prevent input/cursor conflicts
        var allPauseMenus = Resources.FindObjectsOfTypeAll<cowsins.PauseMenu>();
        foreach (var pm in allPauseMenus)
        {
            if (pm.gameObject.scene.name != null)
            {
                pm.enabled = false;
            }
        }
    }

    public void Resume()
    {
        if (!_uiReady || IsTransitioning) return;
        IsPaused = false;
        _transitionEndTime = Time.realtimeSinceStartup + PanelManager.PanelTransitionDuration;

        // Find and disable all active and inactive scene instances of Cowsins' built-in PauseMenu to prevent input/cursor conflicts
        var allPauseMenus2 = Resources.FindObjectsOfTypeAll<cowsins.PauseMenu>();
        foreach (var pm in allPauseMenus2)
        {
            if (pm.gameObject.scene.name != null)
            {
                pm.enabled = false;
            }
        }

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ClosePanel("Pause", _pausePanel, _pauseCard, ResumeGameplay);
        }
        else
        {
            ResumeGameplay();
        }
    }

    private void ResumeGameplay()
    {
        // Clean up settings classes so it resets for next pause
        if (_pauseCard != null) _pauseCard.RemoveFromClassList("slide-down");
        if (_settingsCard != null) _settingsCard.RemoveFromClassList("active");

        // Clear UI Toolkit focus
        if (_resumeButton != null) _resumeButton.Blur();
        if (_settingsButton != null) _settingsButton.Blur();
        if (_mainMenuButton != null) _mainMenuButton.Blur();
        if (_quitButton != null) _quitButton.Blur();
        if (_pausePanel != null) _pausePanel.Blur();

        // Clear EventSystem selection
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void OpenSettings()
    {
        if (_pauseCard != null) _pauseCard.AddToClassList("slide-down");
        if (_settingsCard != null) _settingsCard.AddToClassList("active");

        var gsm = cowsins.GameSettingsManager.Instance;
        if (gsm != null)
        {
            if (_volumeSlider != null) _volumeSlider.value = gsm.masterVolume;
            if (_mouseSensXSlider != null) _mouseSensXSlider.value = gsm.playerSensX;
            if (_mouseSensYSlider != null) _mouseSensYSlider.value = gsm.playerSensY;
            if (_controllerSensXSlider != null) _controllerSensXSlider.value = gsm.playerControllerSensX;
            if (_controllerSensYSlider != null) _controllerSensYSlider.value = gsm.playerControllerSensY;
            if (_fullscreenToggle != null) _fullscreenToggle.value = gsm.fullScreen == 1;
            if (_vsyncToggle != null) _vsyncToggle.value = gsm.vsync == 1;
        }
        else
        {
            if (_volumeSlider != null) _volumeSlider.value = PlayerPrefs.GetFloat("masterVolume", 1f);
            if (_mouseSensXSlider != null) _mouseSensXSlider.value = PlayerPrefs.GetFloat("playerSensX", 4f);
            if (_mouseSensYSlider != null) _mouseSensYSlider.value = PlayerPrefs.GetFloat("playerSensY", 4f);
            if (_controllerSensXSlider != null) _controllerSensXSlider.value = PlayerPrefs.GetFloat("playerControllerSensX", 35f);
            if (_controllerSensYSlider != null) _controllerSensYSlider.value = PlayerPrefs.GetFloat("playerControllerSensY", 35f);
            if (_fullscreenToggle != null) _fullscreenToggle.value = PlayerPrefs.GetInt("fullScreen", 1) == 1;
            if (_vsyncToggle != null) _vsyncToggle.value = PlayerPrefs.GetInt("vsync", 0) == 1;
        }

        if (_musicVolumeSlider != null) _musicVolumeSlider.value = PlayerPrefs.GetFloat("musicVolume", 1f);
        if (_sfxVolumeSlider != null) _sfxVolumeSlider.value = PlayerPrefs.GetFloat("sfxVolume", 1f);

        UpdateValueLabels();
    }

    private void UpdateValueLabels()
    {
        if (_volumeSlider != null && _volumeValueLabel != null) _volumeValueLabel.text = $"{(_volumeSlider.value * 100f):F0}%";
        if (_musicVolumeSlider != null && _musicVolumeValueLabel != null) _musicVolumeValueLabel.text = $"{(_musicVolumeSlider.value * 100f):F0}%";
        if (_sfxVolumeSlider != null && _sfxVolumeValueLabel != null) _sfxVolumeValueLabel.text = $"{(_sfxVolumeSlider.value * 100f):F0}%";
        if (_mouseSensXSlider != null && _mouseSensXValueLabel != null) _mouseSensXValueLabel.text = $"{_mouseSensXSlider.value:F1}";
        if (_mouseSensYSlider != null && _mouseSensYValueLabel != null) _mouseSensYValueLabel.text = $"{_mouseSensYSlider.value:F1}";
        if (_controllerSensXSlider != null && _controllerSensXValueLabel != null) _controllerSensXValueLabel.text = $"{_controllerSensXSlider.value:F0}";
        if (_controllerSensYSlider != null && _controllerSensYValueLabel != null) _controllerSensYValueLabel.text = $"{_controllerSensYSlider.value:F0}";
    }

    private void ResetSettingsToDefault()
    {
        if (_volumeSlider != null) _volumeSlider.value = 1f;
        if (_musicVolumeSlider != null) _musicVolumeSlider.value = 1f;
        if (_sfxVolumeSlider != null) _sfxVolumeSlider.value = 1f;
        if (_mouseSensXSlider != null) _mouseSensXSlider.value = 4f;
        if (_mouseSensYSlider != null) _mouseSensYSlider.value = 4f;
        if (_controllerSensXSlider != null) _controllerSensXSlider.value = 35f;
        if (_controllerSensYSlider != null) _controllerSensYSlider.value = 35f;
        if (_fullscreenToggle != null) _fullscreenToggle.value = true;
        if (_vsyncToggle != null) _vsyncToggle.value = false;

        UpdateValueLabels();
    }

    private void CloseSettings()
    {
        if (_pauseCard != null) _pauseCard.RemoveFromClassList("slide-down");
        if (_settingsCard != null) _settingsCard.RemoveFromClassList("active");

        float vol = _volumeSlider != null ? _volumeSlider.value : 1f;
        float musicVol = _musicVolumeSlider != null ? _musicVolumeSlider.value : 1f;
        float sfxVol = _sfxVolumeSlider != null ? _sfxVolumeSlider.value : 1f;
        float sensX = _mouseSensXSlider != null ? _mouseSensXSlider.value : 4f;
        float sensY = _mouseSensYSlider != null ? _mouseSensYSlider.value : 4f;
        float ctrlX = _controllerSensXSlider != null ? _controllerSensXSlider.value : 35f;
        float ctrlY = _controllerSensYSlider != null ? _controllerSensYSlider.value : 35f;
        bool fullScreen = _fullscreenToggle != null && _fullscreenToggle.value;
        bool vsync = _vsyncToggle != null && _vsyncToggle.value;

        // Save Music & SFX volumes locally first
        PlayerPrefs.SetFloat("musicVolume", musicVol);
        PlayerPrefs.SetFloat("sfxVolume", sfxVol);
        PlayerPrefs.Save();

        // Notify the MusicManager to re-read volume settings immediately.
        if (MusicManager.Instance != null)
            MusicManager.Instance.RefreshVolume();

        var gsm = cowsins.GameSettingsManager.Instance;
        if (gsm != null)
        {
            gsm.masterVolume = vol;
            gsm.playerSensX = sensX;
            gsm.playerSensY = sensY;
            gsm.playerControllerSensX = ctrlX;
            gsm.playerControllerSensY = ctrlY;
            gsm.fullScreen = fullScreen ? 1 : 0;
            gsm.vsync = vsync ? 1 : 0;

            gsm.SaveSettings();
            gsm.LoadSettings();

            // Set music/sfx direct parameters on mixer if present
            if (gsm.masterMixer != null)
            {
                gsm.masterMixer.SetFloat("MusicVolume", Mathf.Log10(musicVol) * 20);
                gsm.masterMixer.SetFloat("Music", Mathf.Log10(musicVol) * 20);
                gsm.masterMixer.SetFloat("SFXVolume", Mathf.Log10(sfxVol) * 20);
                gsm.masterMixer.SetFloat("SFX", Mathf.Log10(sfxVol) * 20);
            }
        }
        else
        {
            PlayerPrefs.SetFloat("masterVolume", vol);
            PlayerPrefs.SetFloat("playerSensX", sensX);
            PlayerPrefs.SetFloat("playerSensY", sensY);
            PlayerPrefs.SetFloat("playerControllerSensX", ctrlX);
            PlayerPrefs.SetFloat("playerControllerSensY", ctrlY);
            PlayerPrefs.SetInt("fullScreen", fullScreen ? 1 : 0);
            PlayerPrefs.SetInt("vsync", vsync ? 1 : 0);
            PlayerPrefs.Save();

            Resolution[] availableResolutions = Screen.resolutions;
            int savedResIndex = PlayerPrefs.GetInt("res", availableResolutions.Length - 1);
            if (savedResIndex >= 0 && savedResIndex < availableResolutions.Length)
            {
                Resolution selectedResolution = availableResolutions[savedResIndex];
                Screen.SetResolution(selectedResolution.width, selectedResolution.height, fullScreen);
            }
            QualitySettings.vSyncCount = vsync ? 1 : 0;
        }
    }

    private void LateUpdate()
    {
        if (_uiReady && !IsPaused && !IsTransitioning && (GameOverManager.Instance == null || !GameOverManager.Instance.IsGameOver))
        {
            bool anyPanelActive = PanelManager.Instance != null && PanelManager.Instance.IsAnyPanelActive();
            if (!anyPanelActive)
            {
                if (UnityEngine.Cursor.lockState != CursorLockMode.Locked || UnityEngine.Cursor.visible)
                {
                    cowsins.PauseMenu.isPaused = false;
                    if (cowsins.UIController.Instance != null)
                    {
                        cowsins.UIController.Instance.LockMouse();
                    }
                    else
                    {
                        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                        UnityEngine.Cursor.visible = false;
                    }
                }
            }
        }
    }

    public static void SetHUDVisible(Transform canvasRoot, bool visible)
    {
        PanelManager.SetHUDVisible(canvasRoot, visible);
    }

    public void GoToMainMenu()
    {
        StartCoroutine(TransitionAndLoadScene(mainMenuSceneName));
    }

    public void QuitGame()
    {
        StartCoroutine(TransitionAndQuit());
    }

    private System.Collections.IEnumerator TransitionAndLoadScene(string sceneName)
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
#if UNITY_EDITOR
        PauseManager.EditorReallowCursorLock();
#endif

        if (_pausePanel != null)
        {
            _pausePanel.RemoveFromClassList("visible");
        }
        if (_pauseCard != null)
        {
            _pauseCard.RemoveFromClassList("visible");
        }

        var root = uiDocument.rootVisualElement;
        var overlay = root?.Q("BlackOverlay");
        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Position; // Block clicks
            overlay.RemoveFromClassList("fade-out"); // Starts 3s fade to black in USS!
        }

        yield return new WaitForSecondsRealtime(PanelManager.BlackOverlayDuration);

        if (_pausePanel != null)
        {
            _pausePanel.style.display = DisplayStyle.None;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private System.Collections.IEnumerator TransitionAndQuit()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
#if UNITY_EDITOR
        PauseManager.EditorReallowCursorLock();
#endif

        if (_pausePanel != null)
        {
            _pausePanel.RemoveFromClassList("visible");
        }
        if (_pauseCard != null)
        {
            _pauseCard.RemoveFromClassList("visible");
        }

        var root = uiDocument.rootVisualElement;
        var overlay = root?.Q("BlackOverlay");
        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Position; // Block clicks
            overlay.RemoveFromClassList("fade-out"); // Starts 3s fade to black in USS!
        }

        yield return new WaitForSecondsRealtime(PanelManager.BlackOverlayDuration);

        if (_pausePanel != null)
        {
            _pausePanel.style.display = DisplayStyle.None;
        }

        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnGenerateCardBackground(MeshGenerationContext mgc)
    {
        var targetElement = mgc.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;
        float chamferSize = 32f;

        // 1. Draw solid dark blue-gray translucent background shape to match HUD modules (0.85 alpha as requested)
        Color fillCol = new Color(9f / 255f, 13f / 255f, 19f / 255f, 0.85f);
        painter.fillColor = fillCol;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Fill();

        // 2. Draw yellow-black diagonal warning stripes at the top edge (adapted to Gold)
        float badgeW = 60f;
        float badgeH = 7f;
        float startX = rect.width - badgeW - 24f;
        float startY = 4f;

        painter.lineWidth = 1.0f;
        for (float offset = 0; offset < badgeW; offset += 6f)
        {
            // Gold stripe
            painter.strokeColor = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.8f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset, startY));
            painter.LineTo(new Vector2(startX + offset - 4f, startY + badgeH));
            painter.Stroke();

            // Black stripe
            painter.strokeColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.9f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset + 3f, startY));
            painter.LineTo(new Vector2(startX + offset - 1f, startY + badgeH));
            painter.Stroke();
        }

        // 3. Draw outer border with gold breathing glow
        float pulse = 0.35f + Mathf.PingPong(Time.realtimeSinceStartup * 1.5f, 0.45f);
        Color strokeCol = new Color(217f / 255f, 199f / 255f, 115f / 255f, pulse);
        float lineWidth = 1.5f;

        painter.strokeColor = strokeCol;
        painter.lineWidth = lineWidth;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Stroke();

        // 4. Draw inner offset double-line border
        float d = 3.5f;
        if (rect.width > d * 2 && rect.height > d * 2)
        {
            Color innerCol = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.15f);
            painter.strokeColor = innerCol;
            painter.lineWidth = 1.0f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chamferSize, d));
            painter.LineTo(new Vector2(rect.width - d, d));
            painter.LineTo(new Vector2(rect.width - d, rect.height - chamferSize));
            painter.LineTo(new Vector2(rect.width - chamferSize, rect.height - d));
            painter.LineTo(new Vector2(d, rect.height - d));
            painter.LineTo(new Vector2(d, chamferSize));
            painter.ClosePath();
            painter.Stroke();
        }

        // 5. Draw 4 3D metallic gold corner rivets (screws)
        System.Action<Vector2> drawRivet = center =>
        {
            painter.fillColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.6f);
            painter.BeginPath();
            painter.Arc(center + new Vector2(0.5f, 0.5f), 3.5f, 0f, 360f);
            painter.Fill();

            painter.fillColor = new Color(175f / 255f, 150f / 255f, 90f / 255f, 1.0f); // Gold screw head
            painter.BeginPath();
            painter.Arc(center, 3.0f, 0f, 360f);
            painter.Fill();

            painter.fillColor = Color.white;
            painter.BeginPath();
            painter.Arc(center - new Vector2(0.8f, 0.8f), 0.6f, 0f, 360f);
            painter.Fill();
        };

        float rOffset = 10f;
        drawRivet(new Vector2(rOffset, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rect.height - rOffset));
        drawRivet(new Vector2(rOffset, rect.height - rOffset));
    }

#if UNITY_EDITOR
    public static void EditorReallowCursorLock()
    {
        try
        {
            System.Type gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null)
            {
                var gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
                var method = gameViewType.GetMethod("AllowCursorLockAndHide", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    foreach (var gv in gameViews)
                    {
                        if (gv != null)
                        {
                            method.Invoke(gv, new object[] { true });
                        }
                    }
                }
            }
        }
        catch (System.Exception)
        {
            // Fail silently in Editor without console spam
        }
    }
#endif

    private void PlayHoverSound()
    {
        if (hoverSFX != null && cowsins.SoundManager.Instance != null)
        {
            cowsins.SoundManager.Instance.PlaySound(hoverSFX, 0f, 0f, false);
        }
    }

    private void PlayClickSound()
    {
        if (clickSFX != null && cowsins.SoundManager.Instance != null)
        {
            cowsins.SoundManager.Instance.PlaySound(clickSFX, 0f, 0f, false);
        }
    }

    private void HookUpAudio(VisualElement element)
    {
        if (element == null) return;

        // Register hover sound on pointer enter
        element.RegisterCallback<PointerEnterEvent>(evt => PlayHoverSound());

        // Register click sound if it is a Button
        if (element is Button btn)
        {
            btn.clicked += PlayClickSound;
        }
    }
}
