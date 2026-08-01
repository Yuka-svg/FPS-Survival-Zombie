using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Chapter Summary panel (B4): shows a stat recap for the chapter the player
/// just completed when StoryManager advances to the next chapter. Pauses the
/// game via PanelManager while visible; the player presses "TIẾP TỤC" to
/// resume. Stats are deltas measured against a snapshot taken when the
/// chapter started (or when the widget first subscribed).
///
/// Runtime-built UI like SimpleNotification: self-instantiates, borrows a
/// screen-space PanelSettings, and loads Assets/UI/Resources/ChapterSummary.uss.
/// </summary>
public class ChapterSummaryWidget : MonoBehaviour
{
    private static ChapterSummaryWidget _instance;

    [Header("Timing")]
    public float fadeDuration = 0.25f;

    [Header("Content")]
    public string continueButtonText = "TIẾP TỤC";
    public string finalTitle = "HOÀN THÀNH CHƯƠNG {0}";
    public string finalSubtitle = "Đây là những gì bạn đã làm được trong chương này:";

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _card;
    private bool _built;
    private bool _shown;

    // ---- Chapter stat snapshot (deltas vs. chapter start) ----
    private float _snapPlayTime;
    private int _snapKills;
    private int _snapJournals;
    private float _snapDamage;
    private int _snapScore;
    private bool _snapshotTaken;

    public static ChapterSummaryWidget Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ChapterSummaryWidget");
                _instance = go.AddComponent<ChapterSummaryWidget>();
                Object.DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        _instance = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        Build();
    }

    private void OnEnable()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
            StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
        }
        TakeSnapshot();
    }

    private void Start()
    {
        // PlayerStatsTracker.Awake sets Instance — by Start it always exists,
        // so this guarantees the chapter-1 baseline is captured even if
        // OnEnable ran before the tracker initialized.
        TakeSnapshot();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
    }

    private void OnDestroy()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        if (_instance == this) _instance = null;
    }

    private void Build()
    {
        if (_built) return;

        var go = new GameObject("ChapterSummaryPanel", typeof(UIDocument));
        go.transform.SetParent(transform, false);
        _doc = go.GetComponent<UIDocument>();
        _doc.sortingOrder = 300;

        var hudDoc = UIPanelSettingsUtil.FindScreenSpaceUIDocument(_doc);
        if (hudDoc != null) _doc.panelSettings = hudDoc.panelSettings;

        _root = new VisualElement();
        _root.name = "ChapterSummaryPanel";
        _root.AddToClassList("chapter-summary-panel");
        _root.style.display = DisplayStyle.None;

        _card = new VisualElement();
        _card.name = "ChapterSummaryCard";
        _card.AddToClassList("chapter-summary-card");

        var title = new Label();
        title.name = "ChapterSummaryTitle";
        title.AddToClassList("chapter-summary-title");
        _card.Add(title);

        var subtitle = new Label();
        subtitle.name = "ChapterSummarySubtitle";
        subtitle.AddToClassList("chapter-summary-subtitle");
        _card.Add(subtitle);

        var divider = new VisualElement();
        divider.name = "ChapterSummaryDivider";
        divider.AddToClassList("chapter-summary-divider");
        _card.Add(divider);

        var stats = new VisualElement();
        stats.name = "ChapterSummaryStats";
        stats.AddToClassList("chapter-summary-stats");
        _card.Add(stats);

        for (int i = 0; i < 4; i++)
        {
            var row = new VisualElement();
            row.name = $"ChapterSummaryRow{i}";
            row.AddToClassList("chapter-summary-row");

            var icon = new Label();
            icon.AddToClassList("chapter-summary-icon");
            row.Add(icon);

            var label = new Label();
            label.name = $"ChapterSummaryLabel{i}";
            label.AddToClassList("chapter-summary-label");
            row.Add(label);

            var value = new Label();
            value.name = $"ChapterSummaryValue{i}";
            value.AddToClassList("chapter-summary-value");
            row.Add(value);

            stats.Add(row);
        }

        var hint = new Label();
        hint.name = "ChapterSummaryHint";
        hint.AddToClassList("chapter-summary-hint");
        _card.Add(hint);

        var button = new Button(Close);
        button.name = "ChapterSummaryContinue";
        button.AddToClassList("chapter-summary-continue");
        button.text = continueButtonText;
        _card.Add(button);

        _root.Add(_card);

        var sheet = Resources.Load<StyleSheet>("ChapterSummary");
        if (sheet != null)
            _root.styleSheets.Add(sheet);

        _doc.rootVisualElement.Add(_root);
        _built = true;
    }

    /// <summary>Records the current stats as the chapter-start baseline.</summary>
    private void TakeSnapshot()
    {
        var st = PlayerStatsTracker.Instance;
        if (st == null) return;
        _snapPlayTime = st.playTime;
        _snapKills = st.TotalKills;
        _snapJournals = st.journalsCollected;
        _snapDamage = st.totalDamageDealt;
        _snapScore = st.score;
        _snapshotTaken = true;
    }

    private void HandleChapterChanged(int oldChapter, int newChapter)
    {
        // Show summary for the chapter just completed (2->3, 3->4, ...).
        // newChapter == -1 means story complete — the ending sequence owns that.
        if (newChapter <= 0) return;
        if (newChapter <= oldChapter) return;

        var st = PlayerStatsTracker.Instance;
        if (st == null || !_snapshotTaken)
        {
            TakeSnapshot();
            return;
        }

        float time = Mathf.Max(0f, st.playTime - _snapPlayTime);
        int kills = Mathf.Max(0, st.TotalKills - _snapKills);
        int journals = Mathf.Max(0, st.journalsCollected - _snapJournals);
        float damage = Mathf.Max(0f, st.totalDamageDealt - _snapDamage);
        int score = Mathf.Max(0, st.score - _snapScore);

        // Snapshot now belongs to the new chapter.
        TakeSnapshot();

        Show(oldChapter, time, kills, journals, damage, score);
    }

    private void Show(int chapter, float time, int kills, int journals, float damage, int score)
    {
        if (!_built) Build();
        if (_shown || _root == null) return;
        if (PanelManager.Instance == null) return;
        if (!PanelManager.Instance.CanOpenPanel("ChapterSummary")) return;

        _root.Q<Label>("ChapterSummaryTitle").text = string.Format(finalTitle, chapter);
        _root.Q<Label>("ChapterSummarySubtitle").text = finalSubtitle;
        _root.Q<Label>("ChapterSummaryHint").text = $"Chương {chapter} hoàn thành — chạm {continueButtonText} để tiếp tục";

        SetStat(0, "THỜI GIAN", PlayerStatsTracker.FormatTime(time));
        SetStat(1, "KẺ THÙ TIÊU DIỆT", kills.ToString("N0"));
        SetStat(2, "NHẬT KÝ", $"{journals}/{st_JournalTotal()}");
        SetStat(3, "SÁT THƯƠNG", PlayerStatsTracker.FormatDamage(damage));

        _shown = true;
        PanelManager.Instance.OpenPanel("ChapterSummary", _root, _card, Close);
    }

    private int st_JournalTotal()
    {
        return CollectibleManager.Instance != null ? CollectibleManager.Instance.Total : 0;
    }

    private void SetStat(int index, string label, string value)
    {
        var l = _root.Q<Label>($"ChapterSummaryLabel{index}");
        if (l != null) l.text = label;
        var v = _root.Q<Label>($"ChapterSummaryValue{index}");
        if (v != null) v.text = value;
    }

    public void Close()
    {
        if (!_shown) return;
        _shown = false;
        if (PanelManager.Instance != null)
            PanelManager.Instance.ClosePanel("ChapterSummary", _root, _card, null);
        else if (_root != null)
            _root.style.display = DisplayStyle.None;
    }
}
