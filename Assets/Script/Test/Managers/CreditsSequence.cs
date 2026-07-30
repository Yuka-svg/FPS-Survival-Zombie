using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class CreditsSequence : MonoBehaviour
{
    [Serializable]
    public class CreditLine
    {
        public string text = "";
    }

    [Serializable]
    public class CreditSection
    {
        public string header = "";
        [Tooltip("Lines under this header. Leave entries blank for now — fill in later.")]
        public List<CreditLine> lines = new List<CreditLine>();
    }

    [Header("School Logo (optional — assign later)")]
    public Sprite schoolLogo;

    [Header("Sections (fill in names/details later — headers are pre-set)")]
    public List<CreditSection> sections = new List<CreditSection>
    {
        new CreditSection { header = "NHÓM PHÁT TRIỂN", lines = new List<CreditLine> { new CreditLine() } },
        new CreditSection { header = "TRƯỜNG", lines = new List<CreditLine> { new CreditLine() } },
        new CreditSection { header = "THÀNH VIÊN", lines = new List<CreditLine> {
            new CreditLine(), new CreditLine(), new CreditLine(), new CreditLine()
        } },
        new CreditSection { header = "NGUỒN TÀI NGUYÊN", lines = new List<CreditLine> {
            new CreditLine(), new CreditLine(), new CreditLine()
        } },
        new CreditSection { header = "ENGINE", lines = new List<CreditLine> { new CreditLine { text = "Unity Engine" } } },
        new CreditSection { header = "LỜI CẢM ƠN", lines = new List<CreditLine> { new CreditLine() } },
        new CreditSection { header = "CẢM ƠN BẠN ĐÃ CHƠI", lines = new List<CreditLine> {
            new CreditLine { text = "Cảm ơn bạn đã dành thời gian trải nghiệm hành trình này." }
        } },
    };

    [Header("Timing")]
    [Tooltip("Fade-in duration at the start of the credits.")]
    public float fadeIn = 1f;
    [Tooltip("Scroll speed in pixels/second (unscaled).")]
    public float scrollSpeed = 60f;
    [Tooltip("Extra hold time (seconds) after the scroll finishes, before returning to the main menu.")]
    public float holdAtEnd = 2f;
    [Tooltip("Fade-out duration before loading the main menu.")]
    public float fadeOut = 1f;

    [Header("Scene")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Visuals")]
    public Color backgroundColor = Color.black;

    private bool _played;
    private VisualElement _root;
    private VisualElement _scrollContent;
    private GameObject _docGO;
    private bool _skipRequested;
    private float _contentHeight;
    private const float SectionGapTop = 70f;
    private const float HeaderHeight = 56f;
    private const float LineHeight = 40f;
    private const float LogoHeight = 220f;

    public void Play(Action onComplete = null)
    {
        if (_played) { onComplete?.Invoke(); return; }
        _played = true;

        // Switch to the credits music.
        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayAfterCreditMusic();

        StartCoroutine(PlayRoutine(onComplete));
    }

    public void Skip() => _skipRequested = true;

    private IEnumerator PlayRoutine(Action onComplete)
    {
        Build();

        float prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        global::UnityEngine.Cursor.lockState = CursorLockMode.None;
        global::UnityEngine.Cursor.visible = true;

        _root.style.opacity = 1f;
        yield return new WaitForSecondsRealtime(fadeIn);

        float viewportHeight = Screen.height;
        float totalTravel = _contentHeight + viewportHeight;
        float traveled = 0f;
        _scrollContent.style.translate = new Translate(0, viewportHeight);

        while (traveled < totalTravel && !_skipRequested)
        {
            float delta = scrollSpeed * Time.unscaledDeltaTime;
            traveled += delta;
            _scrollContent.style.translate = new Translate(0, viewportHeight - traveled);
            yield return null;
        }

        if (!_skipRequested)
            yield return new WaitForSecondsRealtime(holdAtEnd);

        _root.style.opacity = 0f;
        yield return new WaitForSecondsRealtime(fadeOut);

        Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
        AudioListener.pause = false;
        Destroy(_docGO);

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void Build()
    {
        _docGO = new GameObject("CreditsSequence_Doc", typeof(UIDocument));
        _docGO.transform.SetParent(transform, false);
        var doc = _docGO.GetComponent<UIDocument>();
        doc.sortingOrder = 1800;

        // Copy panelSettings from an existing screen-space UIDocument so the panel actually renders.
        // Without this, the UIDocument has no panel and nothing is visible on screen.
        // Must filter out WorldSpacePanelSettings — see UIPanelSettingsUtil for details.
        var ssDoc = UIPanelSettingsUtil.FindScreenSpaceUIDocument(doc);
        if (ssDoc != null)
        {
            doc.panelSettings = ssDoc.panelSettings;
        }
        if (doc.panelSettings == null)
        {
            // Fallback: search all loaded PanelSettings assets directly.
            doc.panelSettings = UIPanelSettingsUtil.FindScreenSpacePanelSettingsAsset();
        }

        var asset = Resources.Load<VisualTreeAsset>("CreditsSequence");
        if (asset == null) return;
        asset.CloneTree(doc.rootVisualElement);

        _root = doc.rootVisualElement.Q("CreditsRoot");
        _scrollContent = doc.rootVisualElement.Q("ScrollContent");
        if (_root == null || _scrollContent == null) return;

        _root.style.opacity = 0f;
        _root.pickingMode = PickingMode.Ignore;
        var bg = _root.Q("Background");
        if (bg != null) bg.style.backgroundColor = backgroundColor;

        float y = 0f;

        if (schoolLogo != null)
        {
            var logo = new VisualElement();
            logo.name = "SchoolLogo";
            logo.style.width = LogoHeight;
            logo.style.height = LogoHeight;
            logo.style.marginBottom = SectionGapTop;
            logo.style.backgroundImage = new StyleBackground(schoolLogo);
            logo.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            _scrollContent.Add(logo);
            y += LogoHeight + SectionGapTop;
        }

        foreach (var section in sections)
        {
            var headerLabel = new Label(section.header);
            headerLabel.name = "Header_" + section.header;
            headerLabel.AddToClassList("credits-header");
            headerLabel.style.height = HeaderHeight;
            _scrollContent.Add(headerLabel);
            y += HeaderHeight;

            foreach (var line in section.lines)
            {
                var lineLabel = new Label(line.text);
                lineLabel.AddToClassList("credits-line");
                lineLabel.style.height = LineHeight;
                _scrollContent.Add(lineLabel);
                y += LineHeight;
            }

            y += SectionGapTop;
        }

        _contentHeight = y;
        _scrollContent.style.minHeight = y;

        doc.rootVisualElement.Add(_root);
    }
}
