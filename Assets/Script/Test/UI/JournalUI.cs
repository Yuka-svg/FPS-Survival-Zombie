using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;
using Cursor = UnityEngine.Cursor;

public class JournalUI : MonoBehaviour
{
    public static JournalUI Instance;

    [Header("Audio SFX")]
    public AudioClip hoverSFX;

    [Header("Ruled Lines Tuning")]
    [Range(0f, 100f)] public float lineStartOffset = 60f;
    [Range(10f, 100f)] public float lineSpacing = 41f;
    [Range(-50f, 100f)] public float contentTopOffset = 20f;

#if UNITY_EDITOR
    private void Reset()
    {
        if (hoverSFX == null) hoverSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Engine/Cowsins/SFX/UI/UIHover_SFX.wav");
    }
#endif

    private UIDocument _doc;
    private VisualElement _panel;
    private Label _title;
    private Label _content;
    private VisualElement _illustration;
    private VisualElement _crease;
    private ScrollView _scroll;
    private Button _closeButton;
    private AudioSource _audioSource;
    private PlayerControl _playerControl;
    private Coroutine _typewriterCoroutine;
    private Coroutine _closeCoroutine;
    private Coroutine _deferCoroutine;
    private JournalData _pendingJournal;
    private bool _open;

    public bool IsOpen => _open;
    private float _transitionEndTime = 0f;
    public bool IsTransitioning => Time.realtimeSinceStartup < _transitionEndTime;
    public bool IsOpenOrTransitioning => _open || IsTransitioning;

    void Awake()
    {
        Instance = this;
        _doc = GetComponent<UIDocument>();
        _audioSource = GetComponent<AudioSource>();

        var root = _doc.rootVisualElement;
        _panel = root.Q("JournalUI");
        _title = root.Q<Label>("JournalTitle");
        _content = root.Q<Label>("JournalContent");
        _illustration = root.Q("Illustration");
        _crease = root.Q("BinderCrease");
        _scroll = root.Q<ScrollView>("JournalScroll");
        _closeButton = root.Q<Button>("CloseButton");

        if (_scroll != null && _scroll.contentContainer != null)
        {
            _scroll.contentContainer.generateVisualContent += OnGenerateRuledLines;
        }

        if (_closeButton != null)
            _closeButton.RegisterCallback<ClickEvent>(_ => Close());
        if (_panel != null)
            _panel.style.display = DisplayStyle.None;
    }

    private void OnGenerateRuledLines(MeshGenerationContext mgc)
    {
        var painter = mgc.painter2D;
        painter.strokeColor = new Color(180f/255f, 160f/255f, 140f/255f, 0.25f); // Soft sepia lines
        painter.lineWidth = 1f;
        float width = mgc.visualElement.layout.width;
        if (float.IsNaN(width) || width <= 0f) width = 840f;

        for (int i = 0; i < 70; i++) // Draw plenty of lines to cover scroll range
        {
            float y = lineStartOffset + i * lineSpacing; // Use public tuning variables
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, y));
            painter.LineTo(new Vector2(width, y));
            painter.Stroke();
        }
    }

    private void OnDisable()
    {
        if (_open) Close();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_open)
        {
            bool cutsceneActive = CutscenePlayer.IsAnyPlaying;
            bool dialogueActive = false;
            var companions = FindObjectsByType<DialogueBubble>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var d in companions)
                if (d != null && d.IsVisible) { dialogueActive = true; break; }
            if (!cutsceneActive && !dialogueActive)
                Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerControl = player.GetComponentInChildren<PlayerControl>();
    }

    public void Show(JournalData journal)
    {
        // If a cutscene is currently playing, defer showing the journal until
        // the cutscene finishes. This prevents the journal UI and cutscene
        // from fighting over Time.timeScale — the cutscene sets timeScale=0,
        // and when the player closes the journal, PanelManager would restore
        // timeScale=1 while the cutscene is still running.
        if (CutscenePlayer.IsAnyPlaying)
        {
            _pendingJournal = journal;
            if (_deferCoroutine != null) StopCoroutine(_deferCoroutine);
            _deferCoroutine = StartCoroutine(ShowAfterCutscene());
            return;
        }

        ShowInternal(journal);
    }

    private System.Collections.IEnumerator ShowAfterCutscene()
    {
        // Wait until no cutscene is playing. CutscenePlayer uses
        // WaitForSecondsRealtime so it runs even when timeScale=0.
        while (CutscenePlayer.IsAnyPlaying)
            yield return null;

        var journal = _pendingJournal;
        _pendingJournal = null;
        _deferCoroutine = null;

        if (journal != null)
            ShowInternal(journal);
    }

    private void ShowInternal(JournalData journal)
    {
        if (PanelManager.Instance != null)
        {
            if (!PanelManager.Instance.CanOpenPanel("Journal")) return;
            PanelManager.Instance.OpenPanel("Journal", _panel, null, Close);
        }
        else
        {
            bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
            bool pauseActive = PauseManager.Instance != null && PauseManager.Instance.IsOpenOrTransitioning;
            bool skillTreeActive = false;
            var skillTree = FindAnyObjectByType<SkillTreeWidget>();
            if (skillTree != null) skillTreeActive = skillTree.IsOpenOrTransitioning;
            if (gameOver || pauseActive || skillTreeActive || IsTransitioning) return;
        }

        _open = true;
        _transitionEndTime = Time.realtimeSinceStartup + PanelManager.PanelTransitionDuration;

        if (_closeButton != null)
        {
            _closeButton.RemoveFromClassList("btn-visible");
            _closeButton.pickingMode = PickingMode.Ignore;
        }

        if (_content != null)
        {
            _content.style.marginTop = contentTopOffset;
        }

        if (journal.image != null)
        {
            if (_illustration != null)
            {
                _illustration.style.display = DisplayStyle.Flex;
                _illustration.style.backgroundImage = new StyleBackground(journal.image);
            }
            if (_crease != null) _crease.style.display = DisplayStyle.Flex;
            if (_scroll != null)
            {
                _scroll.style.left = 520f;
                _scroll.style.right = 40f;
            }
        }
        else
        {
            if (_illustration != null) _illustration.style.display = DisplayStyle.None;
            if (_crease != null) _crease.style.display = DisplayStyle.None;
            if (_scroll != null)
            {
                _scroll.style.left = 60f;
                _scroll.style.right = 60f;
            }
        }

        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
        }
        _typewriterCoroutine = StartCoroutine(TypeText(journal.title, journal.content, journal.voiceLog));
    }

    public void Close()
    {
        if (!_open || IsTransitioning) return;
        _open = false;
        _transitionEndTime = Time.realtimeSinceStartup + PanelManager.PanelTransitionDuration;

        if (_closeCoroutine != null)
        {
            StopCoroutine(_closeCoroutine);
        }

        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ClosePanel("Journal", _panel, null, ResumeGameplay);
        }
        else
        {
            ResumeGameplay();
        }

        _audioSource.Stop();
    }

    private IEnumerator TypeText(string titleText, string fullContent, AudioClip voiceLog)
    {
        // 1. Clear text initially
        _title.text = "";
        _content.text = "";

        // 2. Wait for the 1.5-second transition to complete
        yield return new WaitForSecondsRealtime(1.5f);

        // 3. Start voice log if available
        if (voiceLog != null)
        {
            _audioSource.Stop();
            _audioSource.clip = voiceLog;
            _audioSource.Play();
        }

        float charDelay = 0.015f; // fast, satisfying typewriter speed
        int sfxInterval = 2;      // play sfx every 2 chars to avoid spam

        // 4. Typewrite the title first
        string currentTitle = "";
        for (int i = 0; i < titleText.Length; i++)
        {
            currentTitle += titleText[i];
            _title.text = currentTitle;

            if (i % sfxInterval == 0 && hoverSFX != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(hoverSFX, 0f, 0f, false);
            }
            yield return new WaitForSecondsRealtime(charDelay);
        }

        // Slight pause between title and content typing
        yield return new WaitForSecondsRealtime(0.2f);

        // 5. Typewrite the content text
        string currentContent = "";
        for (int i = 0; i < fullContent.Length; i++)
        {
            currentContent += fullContent[i];
            _content.text = currentContent;

            // Play keyclick sound using Cowsins SoundManager
            if (i % sfxInterval == 0 && hoverSFX != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(hoverSFX, 0f, 0f, false);
            }

            yield return new WaitForSecondsRealtime(charDelay);
        }

        if (_closeButton != null)
        {
            _closeButton.AddToClassList("btn-visible");
            _closeButton.pickingMode = PickingMode.Position;
        }
        _typewriterCoroutine = null;
    }


    private void ResumeGameplay()
    {
        // Clear UI Toolkit focus
        if (_panel != null) _panel.Blur();
        if (_closeButton != null) _closeButton.Blur();

        // Clear EventSystem selection
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_scroll != null && _scroll.contentContainer != null)
        {
            _scroll.contentContainer.MarkDirtyRepaint();
        }
        if (_content != null)
        {
            _content.style.marginTop = contentTopOffset;
        }
    }
#endif
}
