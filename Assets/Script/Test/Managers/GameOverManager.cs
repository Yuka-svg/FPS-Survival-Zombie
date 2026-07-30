using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using cowsins;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance;

    [Header("Player")]
    [Tooltip("Cowsins PlayerStats to listen to. Auto-found if left empty.")]
    public PlayerStats playerStats;

    [Header("UIDocument")]
    [Tooltip("UIDocument containing GameOverPanel. Auto-found if left empty.")]
    public UIDocument uiDocument;

    [Header("Main Menu")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Behaviour")]
    public float showDelay = PanelManager.PanelTransitionDuration;
    public bool freezeTimeOnGameOver = true;

    public bool IsGameOver => isGameOver;

    public event System.Action OnPlayerDied;

    private VisualElement _gameOverPanel;
    private VisualElement _card;
    private Label _finalScoreText;
    private Label _waveReachedText;
    private Label _killsText;
    private Label _bestScoreText;
    private Button _restartButton;
    private Button _mainMenuButton;
    private Button _quitButton;
    private VisualElement _statIcon0;
    private VisualElement _statIcon1;
    private VisualElement _statIcon2;
    private VisualElement _statIcon3;

    private bool isGameOver;
    private bool subscribed;
    private bool _uiReady;

    [Header("Audio")]
    public AudioClip hoverSFX;
    public AudioClip clickSFX;

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
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (_restartButton != null) _restartButton.clicked -= RestartGame;
        if (_quitButton != null) _quitButton.clicked -= QuitGame;
        if (_mainMenuButton != null) _mainMenuButton.clicked -= GoToMainMenu;

        if (_card != null)
        {
            _card.generateVisualContent -= OnGenerateCardBackground;
        }
    }

    private void Start()
    {
        TrySubscribe();
        SetupUI();
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
            Debug.LogError("[GameOverManager] No UIDocument found! GameOverPanel will not function.");
            return;
        }

        var root = uiDocument.rootVisualElement;
        _gameOverPanel = root.Q("GameOverPanel");
        if (_gameOverPanel == null)
        {
            Debug.LogError("[GameOverManager] GameOverPanel not found in UXML!");
            return;
        }
        _card = _gameOverPanel.Q("Card");
        _finalScoreText = _gameOverPanel.Q<Label>("FinalScoreText");
        _waveReachedText = _gameOverPanel.Q<Label>("WaveReachedText");
        _killsText = _gameOverPanel.Q<Label>("KillsText");
        _bestScoreText = _gameOverPanel.Q<Label>("BestScoreText");
        _restartButton = _gameOverPanel.Q<Button>("RestartButton");
        _mainMenuButton = _gameOverPanel.Q<Button>("MainMenuButton");
        _quitButton = _gameOverPanel.Q<Button>("QuitButton");
        _statIcon0 = _gameOverPanel.Q<VisualElement>("GameOverStatIcon0");
        _statIcon1 = _gameOverPanel.Q<VisualElement>("GameOverStatIcon1");
        _statIcon2 = _gameOverPanel.Q<VisualElement>("GameOverStatIcon2");
        _statIcon3 = _gameOverPanel.Q<VisualElement>("GameOverStatIcon3");

        if (_gameOverPanel != null)
            _gameOverPanel.style.display = DisplayStyle.None;

        if (_card != null)
        {
            _card.generateVisualContent += OnGenerateCardBackground;
        }

        if (_restartButton != null)
            _restartButton.clicked += RestartGame;
        if (_quitButton != null)
            _quitButton.clicked += QuitGame;
        if (_mainMenuButton != null)
            _mainMenuButton.clicked += GoToMainMenu;

        // Hook up hover and click audio feedback
        HookUpAudio(_restartButton);
        HookUpAudio(_mainMenuButton);
        HookUpAudio(_quitButton);

        _uiReady = true;
    }

    private void Unsubscribe()
    {
        if (subscribed && playerStats != null)
        {
            playerStats.RemoveOnDieListener(OnPlayerDeath);
            subscribed = false;
        }
    }

    private void TrySubscribe()
    {
        if (subscribed)
            return;
        if (playerStats == null)
            playerStats = FindAnyObjectByType<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.AddOnDieListener(OnPlayerDeath);
            subscribed = true;
        }
    }

    public void OnPlayerDeath()
    {
        if (isGameOver)
            return;
        isGameOver = true;
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.RegisterPanelActive("GameOver", true);
        }
        OnPlayerDied?.Invoke();
        StartCoroutine(ShowAfterDelay());
    }

    private System.Collections.IEnumerator ShowAfterDelay()
    {
        if (showDelay > 0f)
            yield return new WaitForSecondsRealtime(showDelay);
        ShowGameOver();
    }

    private void ShowGameOver()
    {
        if (!_uiReady)
        {
            Debug.LogWarning("[GameOverManager] UI not ready, cannot show GameOverPanel.");
            return;
        }

        var sm = StoryManager.Instance;
        bool isStoryMode = sm != null;
        bool isEndless = GameModeManager.CurrentMode == GameMode.Endless;
        bool isWaveMode = !isStoryMode && !isEndless;

        int finalScore = ScoreManager.Instance != null ? ScoreManager.Instance.GetFinalScore() : 0;
        int wave = WaveManager.Instance != null ? WaveManager.Instance.currentWave : 0;
        int kills = ScoreManager.Instance != null ? ScoreManager.Instance.kills : 0;

        if (isStoryMode)
        {
            int chapter = sm.CurrentChapter;
            int questsDone = sm.TotalQuestsCompleted;
            int journals = CollectibleManager.Instance != null ? CollectibleManager.Instance.Count : 0;
            int journalsTotal = CollectibleManager.Instance != null ? CollectibleManager.Instance.Total : 0;

            if (_finalScoreText != null)
                _finalScoreText.text = "Chapter: " + chapter;
            if (_waveReachedText != null)
                _waveReachedText.text = "Quests: " + questsDone;
            if (_killsText != null)
                _killsText.text = "Journals: " + journals + (journalsTotal > 0 ? " / " + journalsTotal : "");
            if (_bestScoreText != null)
                _bestScoreText.text = sm.StoryComplete ? "Story Complete" : "Progress: Chapter " + chapter;
        }
        else if (isEndless)
        {
            int bestScore = PlayerPrefs.GetInt("BestEndlessScore", 0);
            int bestWave = PlayerPrefs.GetInt("BestEndlessWave", 0);
            if (finalScore > bestScore) { bestScore = finalScore; PlayerPrefs.SetInt("BestEndlessScore", bestScore); }
            if (wave > bestWave) { bestWave = wave; PlayerPrefs.SetInt("BestEndlessWave", bestWave); }
            PlayerPrefs.Save();

            if (PlayFabManager.Instance != null && PlayFabManager.Instance.IsLoggedIn)
                PlayFabManager.Instance.SaveAllToCloud();

            if (_finalScoreText != null)
                _finalScoreText.text = "Score : " + finalScore;
            if (_waveReachedText != null)
                _waveReachedText.text = "Wave : " + wave;
            if (_killsText != null)
                _killsText.text = "Kills : " + kills;
            if (_bestScoreText != null)
                _bestScoreText.text = "Best : " + bestScore + "  (Wave " + bestWave + ")";
        }
        else
        {
            int bestScore = PlayerPrefs.GetInt("BestScore", 0);
            int bestWave = PlayerPrefs.GetInt("BestWave", 0);
            if (finalScore > bestScore) { bestScore = finalScore; PlayerPrefs.SetInt("BestScore", bestScore); }
            if (wave > bestWave) { bestWave = wave; PlayerPrefs.SetInt("BestWave", bestWave); }
            PlayerPrefs.Save();

            if (PlayFabManager.Instance != null && PlayFabManager.Instance.IsLoggedIn)
                PlayFabManager.Instance.SaveAllToCloud();

            if (_finalScoreText != null)
                _finalScoreText.text = "Score : " + finalScore;
            if (_waveReachedText != null)
                _waveReachedText.text = "Wave : " + wave;
            if (_killsText != null)
                _killsText.text = "Kills : " + kills;
            if (_bestScoreText != null)
                _bestScoreText.text = "Best : " + bestScore + "  (Wave " + bestWave + ")";
        }

        UpdateStatIcons(isStoryMode);

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.OpenPanel("GameOver", _gameOverPanel, _card);
        }
        else
        {
            if (uiDocument != null)
            {
                PauseManager.SetHUDVisible(uiDocument.transform, false);
            }

            if (_gameOverPanel != null)
            {
                _gameOverPanel.style.display = DisplayStyle.Flex;
                _gameOverPanel.AddToClassList("visible");
            }

            if (_card != null)
            {
                _card.AddToClassList("visible");
            }

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            if (freezeTimeOnGameOver)
                Time.timeScale = 0f;
        }
    }

    private void UpdateStatIcons(bool isStoryMode)
    {
        if (isStoryMode)
        {
            UpdateStatIcon(_statIcon0, "gameover-icon-chapter");
            UpdateStatIcon(_statIcon1, "gameover-icon-quests");
            UpdateStatIcon(_statIcon2, "gameover-icon-journals");
            UpdateStatIcon(_statIcon3, "gameover-icon-progress");
        }
        else
        {
            UpdateStatIcon(_statIcon0, "gameover-icon-score");
            UpdateStatIcon(_statIcon1, "gameover-icon-wave");
            UpdateStatIcon(_statIcon2, "gameover-icon-kills");
            UpdateStatIcon(_statIcon3, "gameover-icon-best");
        }
    }

    private void UpdateStatIcon(VisualElement icon, string newClass)
    {
        if (icon == null) return;
        icon.RemoveFromClassList("gameover-icon-chapter");
        icon.RemoveFromClassList("gameover-icon-quests");
        icon.RemoveFromClassList("gameover-icon-journals");
        icon.RemoveFromClassList("gameover-icon-progress");
        icon.RemoveFromClassList("gameover-icon-score");
        icon.RemoveFromClassList("gameover-icon-wave");
        icon.RemoveFromClassList("gameover-icon-kills");
        icon.RemoveFromClassList("gameover-icon-best");

        icon.AddToClassList(newClass);
    }

    public void RestartGame()
    {
        StartCoroutine(TransitionAndRestart());
    }

    public void GoToMainMenu()
    {
        SaveRoom.ResetStaticState();
        if (CompanionManager.Instance != null) CompanionManager.Instance.ResetForNewGame();
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

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ClosePanel("GameOver", _gameOverPanel, _card);
        }
        else
        {
            if (_gameOverPanel != null)
            {
                _gameOverPanel.RemoveFromClassList("visible");
            }
            if (_card != null)
            {
                _card.RemoveFromClassList("visible");
            }
        }

        var root = uiDocument.rootVisualElement;
        var overlay = root?.Q("BlackOverlay");
        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Position; // Block clicks
            overlay.RemoveFromClassList("fade-out"); // Starts 3s fade to black in USS!
        }

        yield return new WaitForSecondsRealtime(PanelManager.BlackOverlayDuration);

        if (PanelManager.Instance == null)
        {
            if (_gameOverPanel != null)
            {
                _gameOverPanel.style.display = DisplayStyle.None;
            }
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private System.Collections.IEnumerator TransitionAndRestart()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
#if UNITY_EDITOR
        PauseManager.EditorReallowCursorLock();
#endif

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.forceFreezeTimeScale = true; // Lock PanelManager timescale unfreezing
            PanelManager.Instance.ClosePanel("GameOver", _gameOverPanel, _card);
        }
        else
        {
            if (_gameOverPanel != null)
            {
                _gameOverPanel.RemoveFromClassList("visible");
            }
            if (_card != null)
            {
                _card.RemoveFromClassList("visible");
            }
        }

        var root = uiDocument.rootVisualElement;
        var overlay = root?.Q("BlackOverlay");
        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Position; // Block clicks
            overlay.RemoveFromClassList("fade-out"); // Starts 3s fade to black in USS!
        }

        yield return new WaitForSecondsRealtime(PanelManager.BlackOverlayDuration);

        if (AchievementManager.Instance != null)
            AchievementManager.Instance.ResetProgress();

        var sm = StoryManager.Instance;
        if (sm != null && SaveRoom.LastCheckpoint.HasValue)
        {
            isGameOver = false;

            // Warp and respawn player immediately while screen is black!
            var ps = FindAnyObjectByType<PlayerStats>();
            if (ps != null)
            {
                ps.Respawn(SaveRoom.LastCheckpoint.Value);
                
                // Immediately re-evaluate ChapterBoundary and SaveRoom states based on new position
                var checkpointPos = SaveRoom.LastCheckpoint.Value;
                var boundaries = FindObjectsByType<ChapterBoundary>(FindObjectsSortMode.None);
                foreach (var b in boundaries)
                {
                    if (b != null) b.ReevaluateState(checkpointPos);
                }

                var saverooms = FindObjectsByType<SaveRoom>(FindObjectsSortMode.None);
                foreach (var sr in saverooms)
                {
                    if (sr != null) sr.ReevaluateState(checkpointPos);
                }

                var playerGO = GameObject.FindGameObjectWithTag("Player");
                if (playerGO != null)
                {
                    var control = playerGO.GetComponentInChildren<PlayerControl>();
                    if (control != null) control.LoseControl(); // Lock control initially
                }
                Debug.Log($"[GameOverManager] Warped and respawned player at checkpoint {checkpointPos} and re-evaluated trigger states.");
            }
            else
            {
                if (PanelManager.Instance != null)
                    PanelManager.Instance.forceFreezeTimeScale = false;
                Time.timeScale = 1f;
                SaveRoom.ResetStaticState();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                yield break;
            }

            if (PanelManager.Instance == null)
            {
                if (_gameOverPanel != null)
                    _gameOverPanel.style.display = DisplayStyle.None;
                if (uiDocument != null)
                {
                    PauseManager.SetHUDVisible(uiDocument.transform, true);
                }
            }

            // Fade back out when respawning using pure USS transition
            if (overlay != null)
            {
                overlay.AddToClassList("fade-out"); // Starts 3s fade-out in USS!
                yield return new WaitForSecondsRealtime(PanelManager.BlackOverlayDuration);
                overlay.pickingMode = PickingMode.Ignore; // Allow clicks to pass through
            }

            // Wait another 1.5 seconds for HUD slide-in transition to fully complete!
            yield return new WaitForSecondsRealtime(1.5f);

            // Unlock PanelManager timescale and resume gameplay!
            if (PanelManager.Instance != null)
            {
                PanelManager.Instance.forceFreezeTimeScale = false;
            }
            Time.timeScale = 1f;

            var playerGO2 = GameObject.FindGameObjectWithTag("Player");
            if (playerGO2 != null)
            {
                var control = playerGO2.GetComponentInChildren<PlayerControl>();
                if (control != null) control.GrantControl();
            }
            yield break;
        }

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.forceFreezeTimeScale = false;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private System.Collections.IEnumerator TransitionAndQuit()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
#if UNITY_EDITOR
        PauseManager.EditorReallowCursorLock();
#endif

        if (_gameOverPanel != null)
        {
            _gameOverPanel.RemoveFromClassList("visible");
        }
        if (_card != null)
        {
            _card.RemoveFromClassList("visible");
        }

        var root = uiDocument.rootVisualElement;
        var overlay = root?.Q("BlackOverlay");
        if (overlay != null)
        {
            overlay.pickingMode = PickingMode.Position; // Block clicks
            overlay.RemoveFromClassList("fade-out"); // Starts 3s fade to black in USS!
        }

        yield return new WaitForSecondsRealtime(PanelManager.BlackOverlayDuration);

        if (_gameOverPanel != null)
        {
            _gameOverPanel.style.display = DisplayStyle.None;
        }

        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

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

        // 1b. Draw Dot Matrix Grid Pattern (Lưới điểm công nghệ)
        painter.fillColor = new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.04f);
        float dotSpacing = 24f;
        for (float x = dotSpacing; x < rect.width; x += dotSpacing)
        {
            for (float y = dotSpacing; y < rect.height; y += dotSpacing)
            {
                // Skip if near the corners or outside the chamfered region
                if (x < chamferSize && y < chamferSize - x) continue;
                if (x > rect.width - chamferSize && y < x - (rect.width - chamferSize)) continue;
                if (x < chamferSize && y > rect.height - chamferSize + x) continue;
                if (x > rect.width - chamferSize && y > rect.height - chamferSize + (rect.width - x)) continue;

                painter.BeginPath();
                painter.Arc(new Vector2(x, y), 1.0f, 0f, 360f);
                painter.Fill();
            }
        }

        // 2. Draw red-black diagonal warning stripes at the top edge (adapted to Crimson)
        float badgeW = 60f;
        float badgeH = 7f;
        float startX = rect.width - badgeW - 24f;
        float startY = 4f;

        painter.lineWidth = 1.0f;
        for (float offset = 0; offset < badgeW; offset += 6f)
        {
            // Red stripe
            painter.strokeColor = new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.8f);
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

        // 2b. Draw decorative tech status badge block at top-left edge
        float tagW = 64f;
        float tagH = 14f;
        float tagX = 48f;
        // Draw background for tag
        painter.fillColor = new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.15f);
        painter.BeginPath();
        painter.MoveTo(new Vector2(tagX, 0));
        painter.LineTo(new Vector2(tagX + tagW, 0));
        painter.LineTo(new Vector2(tagX + tagW, tagH));
        painter.LineTo(new Vector2(tagX, tagH));
        painter.ClosePath();
        painter.Fill();
        // Left stripe of the tag
        painter.fillColor = new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.8f);
        painter.BeginPath();
        painter.MoveTo(new Vector2(tagX, 0));
        painter.LineTo(new Vector2(tagX + 3f, 0));
        painter.LineTo(new Vector2(tagX + 3f, tagH));
        painter.LineTo(new Vector2(tagX, tagH));
        painter.ClosePath();
        painter.Fill();

        // 2c. Draw Side Ventilation Slits / Joints
        Color darkSlotCol = new Color(16f / 255f, 22f / 255f, 30f / 255f, 0.95f);
        Color lightSlotHighlight = new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.35f);
        float ventWidth = 5f;
        float ventHeight = 3f;
        float ventSpacing = 8f;
        int ventCount = 6;
        float startYLeft = rect.height * 0.5f - (ventCount * ventSpacing) * 0.5f;
        for (int i = 0; i < ventCount; i++)
        {
            float yPos = startYLeft + i * ventSpacing;
            // Left side vents
            painter.fillColor = darkSlotCol;
            painter.BeginPath();
            painter.MoveTo(new Vector2(6f, yPos));
            painter.LineTo(new Vector2(6f + ventWidth, yPos));
            painter.LineTo(new Vector2(6f + ventWidth, yPos + ventHeight));
            painter.LineTo(new Vector2(6f, yPos + ventHeight));
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = lightSlotHighlight;
            painter.lineWidth = 0.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(6f, yPos + ventHeight));
            painter.LineTo(new Vector2(6f + ventWidth, yPos + ventHeight));
            painter.Stroke();

            // Right side vents
            painter.fillColor = darkSlotCol;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.width - 6f - ventWidth, yPos));
            painter.LineTo(new Vector2(rect.width - 6f, yPos));
            painter.LineTo(new Vector2(rect.width - 6f, yPos + ventHeight));
            painter.LineTo(new Vector2(rect.width - 6f - ventWidth, yPos + ventHeight));
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = lightSlotHighlight;
            painter.lineWidth = 0.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.width - 6f - ventWidth, yPos + ventHeight));
            painter.LineTo(new Vector2(rect.width - 6f, yPos + ventHeight));
            painter.Stroke();
        }

        // 3. Draw outer border with red breathing glow
        float pulse = 0.35f + Mathf.PingPong(Time.realtimeSinceStartup * 1.5f, 0.45f);
        Color strokeCol = new Color(229f / 255f, 72f / 255f, 60f / 255f, pulse);
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

        // 3b. Draw Corner Targeting Brackets
        painter.strokeColor = new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.65f);
        painter.lineWidth = 1.5f;
        float bLength = 15f;
        float bOffset = 40f;
        // Top-left
        painter.BeginPath();
        painter.MoveTo(new Vector2(bOffset + bLength, bOffset));
        painter.LineTo(new Vector2(bOffset, bOffset));
        painter.LineTo(new Vector2(bOffset, bOffset + bLength));
        painter.Stroke();
        // Top-right
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width - bOffset - bLength, bOffset));
        painter.LineTo(new Vector2(rect.width - bOffset, bOffset));
        painter.LineTo(new Vector2(rect.width - bOffset, bOffset + bLength));
        painter.Stroke();
        // Bottom-left
        painter.BeginPath();
        painter.MoveTo(new Vector2(bOffset + bLength, rect.height - bOffset));
        painter.LineTo(new Vector2(bOffset, rect.height - bOffset));
        painter.LineTo(new Vector2(bOffset, rect.height - bOffset - bLength));
        painter.Stroke();
        // Bottom-right
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width - bOffset - bLength, rect.height - bOffset));
        painter.LineTo(new Vector2(rect.width - bOffset, rect.height - bOffset));
        painter.LineTo(new Vector2(rect.width - bOffset, rect.height - bOffset - bLength));
        painter.Stroke();

        // 4. Draw inner offset double-line border
        float d = 3.5f;
        if (rect.width > d * 2 && rect.height > d * 2)
        {
            Color innerCol = new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.15f);
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

        // 5. Draw 4 3D metallic silver corner rivets (screws) with washers and slots
        System.Action<Vector2, float> drawRivet = (center, angle) =>
        {
            // Washer
            painter.fillColor = new Color(30f / 255f, 35f / 255f, 45f / 255f, 0.8f);
            painter.BeginPath();
            painter.Arc(center, 6.0f, 0f, 360f);
            painter.Fill();

            // Screw head shadow
            painter.fillColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.6f);
            painter.BeginPath();
            painter.Arc(center + new Vector2(0.5f, 0.5f), 3.5f, 0f, 360f);
            painter.Fill();

            // Silver screw head
            painter.fillColor = new Color(180f / 255f, 185f / 255f, 195f / 255f, 1.0f);
            painter.BeginPath();
            painter.Arc(center, 3.5f, 0f, 360f);
            painter.Fill();

            // Slotted groove
            painter.strokeColor = new Color(60f / 255f, 65f / 255f, 75f / 255f, 1.0f);
            painter.lineWidth = 0.8f;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 2.2f;
            painter.BeginPath();
            painter.MoveTo(center - dir);
            painter.LineTo(center + dir);
            painter.Stroke();

            // Reflection/highlight
            painter.fillColor = Color.white;
            painter.BeginPath();
            painter.Arc(center - new Vector2(0.8f, 0.8f), 0.6f, 0f, 360f);
            painter.Fill();
        };

        float rOffset = 14f;
        drawRivet(new Vector2(rOffset, rOffset), 45f);
        drawRivet(new Vector2(rect.width - rOffset, rOffset), 120f);
        drawRivet(new Vector2(rect.width - rOffset, rect.height - rOffset), 30f);
        drawRivet(new Vector2(rOffset, rect.height - rOffset), 280f);
    }
}
