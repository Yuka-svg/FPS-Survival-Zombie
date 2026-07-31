using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Screen-space dialogue bubble for the companion NPC.
/// Uses the same approach as SimpleNotification — a screen-space UIDocument
/// overlay that shows the dialogue line + Y/N prompt in the center of the screen.
///
/// Two modes:
///   Speech — shows a single line, auto-hides after duration.
///   Choice — shows a line + "Nhấn Y để đồng ý / N để từ chối", waits for Y/N input,
///            fires OnChoiceMade(bool accepted).
/// </summary>
[RequireComponent(typeof(CompanionAI))]
public class DialogueBubble : MonoBehaviour
{
    [Header("Timing")]
    public float fadeIn = 0.3f;
    public float speechHoldDuration = 4f;
    public float fadeOut = 0.8f;

    [Header("Visuals")]
    public Color textColor = new Color(0.95f, 0.92f, 0.8f, 1f);
    public Color choiceColor = new Color(1f, 0.85f, 0.3f, 1f);
    public Color scrimColor = new Color(0f, 0f, 0f, 0.6f);
    public float lineFontSize = 26f;
    public float choiceFontSize = 22f;
    public Color hintColor = new Color(0.5f, 0.85f, 0.6f, 1f);
    public float hintFontSize = 20f;

    private GameObject _panelGO;
    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _scrim;
    private Label _lineLabel;
    private Label _choiceLabel;
    private Label _hintLabel;
    private Coroutine _routine;
    private bool _choiceActive;
    private System.Action<bool> _choiceCallback;
    private float _prevTimeScale = 1f;

    public bool IsVisible => _panelGO != null && _root != null && _root.resolvedStyle.opacity > 0f;
    public bool IsChoiceActive => _choiceActive;

    private void Awake()
    {
        Build();
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        _choiceActive = false;
        _choiceCallback = null;
    }

    private void Build()
    {
        _panelGO = new GameObject("DialogueBubblePanel", typeof(UIDocument));
        _panelGO.transform.SetParent(transform, false);
        _doc = _panelGO.GetComponent<UIDocument>();
        _doc.sortingOrder = 450; // Below SimpleNotification (500), above HUD

        // Borrow panel settings from an existing screen-space UIDocument
        // (same approach as SimpleNotification). Must filter out
        // WorldSpacePanelSettings — see UIPanelSettingsUtil for details.
        var hudDoc = UIPanelSettingsUtil.FindScreenSpaceUIDocument(_doc);
        if (hudDoc != null)
            _doc.panelSettings = hudDoc.panelSettings;
        else
            Debug.LogWarning("[DialogueBubble] No screen-space UIDocument found to borrow panel settings.");

        // Build visual tree programmatically.
        _root = new VisualElement();
        _root.name = "DialogueBubbleRoot";
        _root.style.position = Position.Absolute;
        _root.style.left = 0f;
        _root.style.top = 0f;
        _root.style.right = 0f;
        _root.style.bottom = 0f;
        _root.style.display = DisplayStyle.Flex;
        _root.style.alignItems = Align.Center;
        _root.style.justifyContent = Justify.Center;
        _root.style.opacity = 0f; // Hidden by default
        // The root is a full-screen overlay. It must NOT block input to other
        // UI (e.g. GameOver panel, Pause menu) when hidden or visible — the
        // dialogue only displays text, the player interacts via keyboard (Y/N),
        // not by clicking on the bubble. Without Ignore, an opacity:0 root
        // still picks input and blocks clicks on lower-sortingOrder UIs.
        _root.pickingMode = PickingMode.Ignore;

        // Semi-transparent scrim behind the text.
        _scrim = new VisualElement();
        _scrim.name = "DialogueScrim";
        _scrim.style.backgroundColor = scrimColor;
        _scrim.style.borderTopLeftRadius = 12f;
        _scrim.style.borderTopRightRadius = 12f;
        _scrim.style.borderBottomLeftRadius = 12f;
        _scrim.style.borderBottomRightRadius = 12f;
        _scrim.style.paddingTop = 20f;
        _scrim.style.paddingBottom = 20f;
        _scrim.style.paddingLeft = 30f;
        _scrim.style.paddingRight = 30f;
        _scrim.style.marginLeft = 200f;
        _scrim.style.marginRight = 200f;
        _root.Add(_scrim);

        // Main dialogue line.
        _lineLabel = new Label();
        _lineLabel.name = "DialogueLine";
        _lineLabel.style.color = textColor;
        _lineLabel.style.fontSize = lineFontSize;
        _lineLabel.style.whiteSpace = WhiteSpace.Normal;
        _lineLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _lineLabel.style.marginBottom = 12f;
        _scrim.Add(_lineLabel);

        // Y/N choice prompt.
        _choiceLabel = new Label();
        _choiceLabel.name = "DialogueChoice";
        _choiceLabel.style.color = choiceColor;
        _choiceLabel.style.fontSize = choiceFontSize;
        _choiceLabel.style.whiteSpace = WhiteSpace.Normal;
        _choiceLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _choiceLabel.style.display = DisplayStyle.None;
        _scrim.Add(_choiceLabel);

        // Optional hint shown under the choice (helps answer NPC questions).
        _hintLabel = new Label();
        _hintLabel.name = "DialogueHint";
        _hintLabel.style.color = hintColor;
        _hintLabel.style.fontSize = hintFontSize;
        _hintLabel.style.whiteSpace = WhiteSpace.Normal;
        _hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _hintLabel.style.marginTop = 14f;
        _hintLabel.style.display = DisplayStyle.None;
        _scrim.Add(_hintLabel);

        _doc.rootVisualElement.Add(_root);
    }

    private void Update()
    {
        if (!_choiceActive) return;

        // Read Y/N via the Input System (Keyboard.current) when available,
        // falling back to the legacy Input Manager. The project uses "Input
        // System Package Only" mode, so Input.GetKeyDown does NOT work.
        bool yPressed, nPressed;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            yPressed = kb.yKey.wasPressedThisFrame;
            nPressed = kb.nKey.wasPressedThisFrame;
        }
        else
        {
            yPressed = Input.GetKeyDown(KeyCode.Y);
            nPressed = Input.GetKeyDown(KeyCode.N);
        }

        if (yPressed)
        {
            ResolveChoice(true);
        }
        else if (nPressed)
        {
            ResolveChoice(false);
        }
    }

    // ---- Speech mode ----

    public void ShowSpeech(string line)
    {
        ShowSpeech(line, speechHoldDuration);
    }

    /// <summary>Show a single speech line that auto-hides after <paramref name="holdDuration"/> seconds (fade-in not included).</summary>
    public void ShowSpeech(string line, float holdDuration)
    {
        if (_lineLabel != null) _lineLabel.text = line;
        if (_choiceLabel != null) _choiceLabel.style.display = DisplayStyle.None;
        if (_hintLabel != null) _hintLabel.style.display = DisplayStyle.None;
        Show();
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(HideAfter(fadeIn + holdDuration));
    }

    // ---- Choice mode ----

    public void ShowChoice(string line, System.Action<bool> onChoice)
    {
        ShowChoice(line, null, onChoice);
    }

    /// <summary>
    /// Show a Y/N choice with an optional hint line under it. The hint helps the
    /// player answer NPC questions correctly (e.g. "Gợi ý: ...").
    /// </summary>
    public void ShowChoice(string line, string hint, System.Action<bool> onChoice)
    {
        if (_lineLabel != null) _lineLabel.text = line;
        if (_choiceLabel != null)
        {
            _choiceLabel.text = "Nhấn [Y] để đồng ý   |   Nhấn [N] để từ chối";
            _choiceLabel.style.display = DisplayStyle.Flex;
        }
        if (_hintLabel != null)
        {
            if (!string.IsNullOrEmpty(hint))
            {
                _hintLabel.text = hint;
                _hintLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _hintLabel.text = "";
                _hintLabel.style.display = DisplayStyle.None;
            }
        }
        _choiceCallback = onChoice;
        _choiceActive = true;
        Show();
        if (_routine != null) StopCoroutine(_routine);
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

    private void Show()
    {
        if (_root != null)
        {
            _root.style.display = DisplayStyle.Flex;
            _root.style.opacity = 1f;
        }
        // Pause the game while the dialogue is visible so zombies don't
        // attack the player while they're reading / choosing. Skip if the
        // pause menu or game-over screen is already open (they manage
        // timeScale themselves).
        bool pauseOpen = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
        bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
        if (!pauseOpen && !gameOver && Time.timeScale > 0f)
        {
            // Only capture prevTimeScale once — nested Show() calls must not
            // overwrite the original value, otherwise ForceHide → Hide would
            // restore 0 instead of the correct pre-dialogue timeScale.
            if (_prevTimeScale <= 0f) _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    /// <summary>Public wrapper so dialogue triggers can close the bubble on downed/revive.</summary>
    public void ForceHide()
    {
        // Cancel any pending auto-hide coroutine.
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        _choiceActive = false;
        _choiceCallback = null;
        Hide();
    }

    private void Hide()
    {
        if (_root != null)
        {
            _root.style.opacity = 0f;
            // Also hide display so the full-screen overlay doesn't intercept
            // layout/input even after fading out. Combined with pickingMode =
            // Ignore this guarantees the bubble never blocks other UI.
            _root.style.display = DisplayStyle.None;
        }
        // Restore timeScale — but only if we were the one who paused it,
        // and the pause menu / game-over screen isn't currently open.
        bool pauseOpen = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
        bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
        if (!pauseOpen && !gameOver && Time.timeScale == 0f)
        {
            Time.timeScale = _prevTimeScale > 0f ? _prevTimeScale : 1f;
        }
    }

    private IEnumerator HideAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Hide();
        _routine = null;
    }

    private void OnDestroy()
    {
        // Safety: if the dialogue is destroyed while still visible (e.g. scene
        // unload), restore timeScale so the game doesn't stay frozen.
        // Must guard against Managers destroyed in the same frame (scene unload).
        if (Time.timeScale == 0f)
        {
            bool pauseOpen = false;
            bool gameOver = false;
            try
            {
                pauseOpen = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
                gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
            }
            catch (System.NullReferenceException) { }
            if (!pauseOpen && !gameOver)
                Time.timeScale = _prevTimeScale > 0f ? _prevTimeScale : 1f;
        }
        if (_panelGO != null && _panelGO)
        {
            if (_doc != null && _doc.rootVisualElement != null)
                _doc.rootVisualElement.Clear();
            Destroy(_panelGO);
        }
    }
}
