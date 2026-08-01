using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// AAA Screen-space dialogue overlay for NPC interactions.
/// Features:
///   - Speaker Header Badge (Name + Avatar icon)
///   - Dark Glassmorphism scrim panel with glowing gold border & Theme tokens
///   - Typewriter effect with real-time coroutines (unscaled time safe)
///   - Interactive UI Toolkit Choice Buttons ([Y] Agree / [N] Decline) with mouse hover/click + keyboard shortcuts
///   - Cyan Accent Hint Box
///   - Dynamic PickingMode & Cursor management integrated cleanly with PanelManager
/// </summary>
[RequireComponent(typeof(CompanionAI))]
public class DialogueBubble : MonoBehaviour
{
    [Header("Timing")]
    public float fadeIn = 0.3f;
    public float speechHoldDuration = 4f;
    public float fadeOut = 0.8f;
    public float typewriterSpeed = 0.025f;

    [Header("Speaker Identity")]
    public string defaultSpeakerName = "NPC";
    public Sprite defaultSpeakerAvatar;

    [Header("Visuals")]
    public Color textColor = new Color(0.96f, 0.96f, 0.96f, 1f);
    public Color choiceColor = new Color(0.851f, 0.78f, 0.451f, 1f);
    public Color scrimColor = new Color(0.055f, 0.067f, 0.082f, 0.92f);
    public float lineFontSize = 20f;
    public float choiceFontSize = 16f;
    public Color hintColor = new Color(0.306f, 0.804f, 0.769f, 1f);
    public float hintFontSize = 14f;

    private GameObject _panelGO;
    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _scrim;
    private VisualElement _headerBadge;
    private VisualElement _avatarImage;
    private Label _speakerLabel;
    private Label _lineLabel;
    private VisualElement _choiceContainer;
    private Button _btnYes;
    private Label _keyBadgeYes;
    private Label _btnTextYes;
    private Button _btnNo;
    private Label _keyBadgeNo;
    private Label _btnTextNo;
    private VisualElement _hintBox;
    private Label _hintLabel;

    private Coroutine _routine;
    private Coroutine _typewriterRoutine;
    private bool _isTyping;
    private string _fullText = "";
    private float _lastSkipTime;
    private float _lastSoundTime;

    private bool _choiceActive;
    private System.Action<bool> _choiceCallback;
    private float _prevTimeScale = 1f;
    private bool _didPause; // True when this bubble froze the game (choice mode only).
    private bool _themed; // True when the DialogueBubble.uss stylesheet was applied.

    public bool IsVisible => _panelGO != null && _root != null && _root.resolvedStyle.opacity > 0f;
    public bool IsChoiceActive => _choiceActive;

    private void Awake()
    {
        Build();
    }

    private void OnDisable()
    {
        CleanupState();
    }

    private void CleanupState()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }
        _isTyping = false;
        _choiceActive = false;
        _choiceCallback = null;

        // Unregister safely from PanelManager on disable
        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.RegisterPanelActive("DialogueBubble", false);
        }

        RestoreGameplayState();
    }

    private void Build()
    {
        _panelGO = new GameObject("DialogueBubblePanel", typeof(UIDocument));
        _panelGO.transform.SetParent(transform, false);
        _doc = _panelGO.GetComponent<UIDocument>();
        _doc.sortingOrder = 450; // Below SimpleNotification (500), above HUD

        var hudDoc = UIPanelSettingsUtil.FindScreenSpaceUIDocument(_doc);
        if (hudDoc != null)
            _doc.panelSettings = hudDoc.panelSettings;
        else
            Debug.LogWarning("[DialogueBubble] No screen-space UIDocument found to borrow panel settings.");

        // Root container
        _root = new VisualElement();
        _root.name = "DialogueBubbleRoot";
        _root.style.position = Position.Absolute;
        _root.style.left = 0f;
        _root.style.top = 0f;
        _root.style.right = 0f;
        _root.style.bottom = 0f;
        _root.style.display = DisplayStyle.None;
        _root.style.alignItems = Align.Center;
        _root.style.justifyContent = Justify.Center;
        _root.style.opacity = 0f;
        _root.pickingMode = PickingMode.Ignore;

        var sheet = Resources.Load<StyleSheet>("DialogueBubble");
        _themed = sheet != null;
        if (sheet != null)
            _root.styleSheets.Add(sheet);

        // Scrim background panel
        _scrim = new VisualElement();
        _scrim.name = "DialogueScrim";
        _scrim.AddToClassList("dialogue-scrim");
        _scrim.AddToClassList("dialogue-hidden");
        if (!_themed)
        {
            _scrim.style.backgroundColor = scrimColor;
            _scrim.style.borderTopLeftRadius = 16f;
            _scrim.style.borderTopRightRadius = 16f;
            _scrim.style.borderBottomLeftRadius = 16f;
            _scrim.style.borderBottomRightRadius = 16f;
            _scrim.style.paddingTop = 20f;
            _scrim.style.paddingBottom = 20f;
            _scrim.style.paddingLeft = 28f;
            _scrim.style.paddingRight = 28f;
        }
        _root.Add(_scrim);

        // Allow clicking scrim to skip typewriter
        _scrim.RegisterCallback<ClickEvent>(evt =>
        {
            if (_isTyping)
            {
                SkipTypewriter();
                evt.StopPropagation();
            }
        });

        // Speaker Header Badge
        _headerBadge = new VisualElement();
        _headerBadge.name = "SpeakerHeaderBadge";
        _headerBadge.AddToClassList("dialogue-header-badge");

        _avatarImage = new VisualElement();
        _avatarImage.name = "SpeakerAvatar";
        _avatarImage.AddToClassList("dialogue-speaker-avatar");
        _avatarImage.style.display = DisplayStyle.None;
        _headerBadge.Add(_avatarImage);

        _speakerLabel = new Label();
        _speakerLabel.name = "SpeakerName";
        _speakerLabel.AddToClassList("dialogue-speaker-name");
        _speakerLabel.text = defaultSpeakerName.ToUpper();
        _headerBadge.Add(_speakerLabel);
        _scrim.Add(_headerBadge);

        // Main dialogue line label
        _lineLabel = new Label();
        _lineLabel.name = "DialogueLine";
        _lineLabel.AddToClassList("dialogue-line");
        if (!_themed) _lineLabel.style.color = textColor;
        if (!_themed) _lineLabel.style.fontSize = lineFontSize;
        _lineLabel.style.whiteSpace = WhiteSpace.Normal;
        _lineLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _scrim.Add(_lineLabel);

        // Interactive Choice Buttons Container
        _choiceContainer = new VisualElement();
        _choiceContainer.name = "ChoiceContainer";
        _choiceContainer.AddToClassList("dialogue-choice-container");
        _choiceContainer.style.display = DisplayStyle.None;
        _scrim.Add(_choiceContainer);

        // [Y] Agree Button
        _btnYes = new Button(() => OnChoiceButtonClicked(true));
        _btnYes.name = "BtnYes";
        _btnYes.AddToClassList("dialogue-choice-btn");

        _keyBadgeYes = new Label("[ Y ]");
        _keyBadgeYes.AddToClassList("dialogue-key-badge");
        _btnYes.Add(_keyBadgeYes);

        _btnTextYes = new Label("ĐỒNG Ý");
        _btnTextYes.AddToClassList("dialogue-btn-text");
        _btnYes.Add(_btnTextYes);

        _choiceContainer.Add(_btnYes);

        // [N] Decline Button
        _btnNo = new Button(() => OnChoiceButtonClicked(false));
        _btnNo.name = "BtnNo";
        _btnNo.AddToClassList("dialogue-choice-btn");

        _keyBadgeNo = new Label("[ N ]");
        _keyBadgeNo.AddToClassList("dialogue-key-badge");
        _btnNo.Add(_keyBadgeNo);

        _btnTextNo = new Label("TỪ CHỐI");
        _btnTextNo.AddToClassList("dialogue-btn-text");
        _btnNo.Add(_btnTextNo);

        _choiceContainer.Add(_btnNo);

        // Hint Box Container
        _hintBox = new VisualElement();
        _hintBox.name = "HintBox";
        _hintBox.AddToClassList("dialogue-hint-box");
        _hintBox.style.display = DisplayStyle.None;

        _hintLabel = new Label();
        _hintLabel.name = "DialogueHint";
        _hintLabel.AddToClassList("dialogue-hint");
        if (!_themed) _hintLabel.style.color = hintColor;
        if (!_themed) _hintLabel.style.fontSize = hintFontSize;
        _hintLabel.style.whiteSpace = WhiteSpace.Normal;
        _hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _hintBox.Add(_hintLabel);
        _scrim.Add(_hintBox);

        _doc.rootVisualElement.Add(_root);
    }

    /// <summary>Sets the speaker name and optional avatar icon.</summary>
    public void SetSpeaker(string speakerName, Sprite avatar = null)
    {
        if (_speakerLabel != null)
        {
            _speakerLabel.text = string.IsNullOrEmpty(speakerName) ? defaultSpeakerName.ToUpper() : speakerName.ToUpper();
        }
        if (_avatarImage != null)
        {
            if (avatar != null)
            {
                _avatarImage.style.backgroundImage = new StyleBackground(avatar);
                _avatarImage.style.display = DisplayStyle.Flex;
            }
            else
            {
                _avatarImage.style.backgroundImage = StyleKeyword.Null;
                _avatarImage.style.display = DisplayStyle.None;
            }
        }
    }

    private void Update()
    {
        if (!_choiceActive) return;

        bool yPressed = false, nPressed = false, skipPressed = false;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            yPressed = kb.yKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
            nPressed = kb.nKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame;
            skipPressed = yPressed || nPressed || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame;
        }
        else
        {
            yPressed = Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.Return);
            nPressed = Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Escape);
            skipPressed = yPressed || nPressed || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E);
        }

        // If typewriter is active, any keypress completes text (skip typewriter)
        if (_isTyping)
        {
            if (skipPressed)
            {
                SkipTypewriter();
            }
            return;
        }

        // Only allow choice selection if typewriter finished AND debounce duration passed
        if (Time.unscaledTime - _lastSkipTime < 0.15f) return;

        if (yPressed)
        {
            PlayClickSound();
            ResolveChoice(true);
        }
        else if (nPressed)
        {
            PlayClickSound();
            ResolveChoice(false);
        }
    }

    private void OnChoiceButtonClicked(bool accepted)
    {
        if (_isTyping)
        {
            SkipTypewriter();
            return;
        }

        if (Time.unscaledTime - _lastSkipTime < 0.15f) return;

        PlayClickSound();
        ResolveChoice(accepted);
    }

    // ---- Speech mode ----

    public void ShowSpeech(string line)
    {
        ShowSpeech(line, speechHoldDuration);
    }

    public void ShowSpeech(string line, float holdDuration)
    {
        ShowSpeech(defaultSpeakerName, line, holdDuration);
    }

    public void ShowSpeech(string speakerName, string line, float holdDuration)
    {
        SetSpeaker(speakerName, defaultSpeakerAvatar);
        if (_choiceContainer != null) _choiceContainer.style.display = DisplayStyle.None;
        if (_hintBox != null) _hintBox.style.display = DisplayStyle.None;

        StartTypewriter(line);
        Show(pauseGame: false);

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(HideAfter(fadeIn + holdDuration + (line.Length * typewriterSpeed)));
    }

    // ---- Choice mode ----

    public void ShowChoice(string line, System.Action<bool> onChoice)
    {
        ShowChoice(defaultSpeakerName, line, null, onChoice);
    }

    public void ShowChoice(string line, string hint, System.Action<bool> onChoice)
    {
        ShowChoice(defaultSpeakerName, line, hint, onChoice);
    }

    public void ShowChoice(string speakerName, string line, string hint, System.Action<bool> onChoice)
    {
        SetSpeaker(speakerName, defaultSpeakerAvatar);

        if (_choiceContainer != null)
        {
            _choiceContainer.style.display = DisplayStyle.Flex;
        }

        if (_hintBox != null)
        {
            if (!string.IsNullOrEmpty(hint))
            {
                _hintLabel.text = hint;
                _hintBox.style.display = DisplayStyle.Flex;
            }
            else
            {
                _hintLabel.text = "";
                _hintBox.style.display = DisplayStyle.None;
            }
        }

        _choiceCallback = onChoice;
        _choiceActive = true;

        StartTypewriter(line);
        Show(pauseGame: true);

        if (_routine != null) StopCoroutine(_routine);
    }

    // ---- Typewriter Control ----

    private void StartTypewriter(string fullText)
    {
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }
        _fullText = fullText ?? "";
        _typewriterRoutine = StartCoroutine(TypewriterCoroutine(_fullText));
    }

    private IEnumerator TypewriterCoroutine(string targetText)
    {
        _isTyping = true;
        if (_lineLabel != null) _lineLabel.text = "";

        for (int i = 0; i < targetText.Length; i++)
        {
            if (_lineLabel != null)
            {
                _lineLabel.text = targetText.Substring(0, i + 1);
            }
            PlayTypeSound();
            yield return new WaitForSecondsRealtime(typewriterSpeed);
        }

        _isTyping = false;
        _typewriterRoutine = null;
    }

    private void SkipTypewriter()
    {
        if (!_isTyping) return;
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }
        _isTyping = false;
        if (_lineLabel != null) _lineLabel.text = _fullText;
        _lastSkipTime = Time.unscaledTime;
    }

    private void ResolveChoice(bool accepted)
    {
        _choiceActive = false;
        var cb = _choiceCallback;
        _choiceCallback = null;
        Hide();
        cb?.Invoke(accepted);
    }

    // ---- Show / Hide ----

    private void Show(bool pauseGame)
    {
        if (_root != null)
        {
            _root.style.display = DisplayStyle.Flex;
            _root.style.opacity = 1f;
        }

        if (_scrim != null)
        {
            _scrim.RemoveFromClassList("dialogue-hidden");
            _scrim.AddToClassList("dialogue-visible");
        }

        bool pauseOpen = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
        bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;

        if (pauseGame)
        {
            // Choice Mode: capture cursor and pause game
            _root.pickingMode = PickingMode.Position;

            if (PanelManager.Instance != null)
            {
                PanelManager.Instance.RegisterPanelActive("DialogueBubble", true, () => ResolveChoice(false));
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (cowsins.PlayerControl.instance != null)
            {
                cowsins.PlayerControl.instance.LoseControl();
            }

            if (!pauseOpen && !gameOver && Time.timeScale > 0f)
            {
                if (!_didPause)
                {
                    _prevTimeScale = Time.timeScale;
                    _didPause = true;
                }
                Time.timeScale = 0f;
            }
        }
        else
        {
            // Speech Mode: non-intrusive
            _root.pickingMode = PickingMode.Ignore;
        }
    }

    public void ForceHide()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        if (_typewriterRoutine != null)
        {
            StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = null;
        }
        _isTyping = false;
        _choiceActive = false;
        _choiceCallback = null;
        Hide();
    }

    private void Hide()
    {
        if (_scrim != null)
        {
            _scrim.RemoveFromClassList("dialogue-visible");
            _scrim.AddToClassList("dialogue-hidden");
        }

        if (_root != null)
        {
            _root.style.opacity = 0f;
            _root.style.display = DisplayStyle.None;
            _root.pickingMode = PickingMode.Ignore;
        }

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.RegisterPanelActive("DialogueBubble", false);
        }

        RestoreGameplayState();
    }

    private void RestoreGameplayState()
    {
        bool pauseOpen = false;
        bool gameOver = false;
        try
        {
            pauseOpen = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
            gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
        }
        catch (System.NullReferenceException) { }

        if (_didPause && !pauseOpen && !gameOver)
        {
            _didPause = false;
            Time.timeScale = _prevTimeScale > 0f ? _prevTimeScale : 1f;

            if (cowsins.UIController.Instance != null)
            {
                cowsins.UIController.Instance.LockMouse();
            }
            if (cowsins.PlayerControl.instance != null)
            {
                cowsins.PlayerControl.instance.GrantControl();
            }
        }
    }

    private IEnumerator HideAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Hide();
        _routine = null;
    }

    private void PlayTypeSound()
    {
        if (Time.unscaledTime - _lastSoundTime < 0.05f) return;
        _lastSoundTime = Time.unscaledTime;
        if (UISoundManager.Instance != null)
        {
            UISoundManager.Instance.PlayTick();
        }
    }

    private void PlayClickSound()
    {
        if (UISoundManager.Instance != null)
        {
            UISoundManager.Instance.PlayButtonClick();
        }
    }

    private void OnDestroy()
    {
        CleanupState();
        if (_panelGO != null && _panelGO)
        {
            if (_doc != null && _doc.rootVisualElement != null)
                _doc.rootVisualElement.Clear();
            Destroy(_panelGO);
        }
    }
}

