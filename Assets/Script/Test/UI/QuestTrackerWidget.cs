using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class QuestTrackerWidget : MonoBehaviour
{
    [Header("Layout")]
    public int maxSideQuestLines = 6;

    private VisualElement _container;
    private VisualElement _mainPanel;
    private Label _chapter;
    private Label _title;
    private Label _objective;
    private Label _collectibles;
    private VisualElement _divider;
    private VisualElement _sidePanel;
    private Label _sideHeader;
    private VisualElement _sideLinesContainer;
    private readonly List<Label> _sideLines = new();

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) { enabled = false; return; }
        var root = doc.rootVisualElement;
        
        _container = root.Q("QuestTracker");
        _mainPanel = root.Q("MainPanel");
        _chapter = root.Q<Label>("Chapter");
        _title = root.Q<Label>("Title");
        _objective = root.Q<Label>("Objective");
        _collectibles = root.Q<Label>("Collectibles");
        _divider = root.Q("QuestDivider");
        _sidePanel = root.Q("SidePanel");
        _sideHeader = root.Q<Label>("SideHeader");
        _sideLinesContainer = root.Q("SideLines");

        if (_chapter == null || _title == null || _objective == null || _sideLinesContainer == null)
            enabled = false;
    }

    private void OnEnable()
    {
        if (_container != null)
            _container.generateVisualContent += OnGenerateCardBackground;

        SubscribeToManagers();
        UpdateDisplay();
        StartCoroutine(PollRoutine());
    }

    private void Start()
    {
        // Retry subscription in case StoryManager/SideQuestManager awakened
        // after this widget's OnEnable (script execution order race).
        SubscribeToManagers();
        UpdateDisplay();
    }

    private void SubscribeToManagers()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
            StoryManager.Instance.OnActiveQuestChanged += HandleQuestChanged;
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
            StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
        }
        if (SideQuestManager.Instance != null)
        {
            SideQuestManager.Instance.OnSideQuestCompleted -= HandleSideQuestChanged;
            SideQuestManager.Instance.OnSideQuestCompleted += HandleSideQuestChanged;
            SideQuestManager.Instance.OnSideQuestActivated -= HandleSideQuestChanged;
            SideQuestManager.Instance.OnSideQuestActivated += HandleSideQuestChanged;
        }
    }

    private void OnDisable()
    {
        if (_container != null)
            _container.generateVisualContent -= OnGenerateCardBackground;

        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        }
        if (SideQuestManager.Instance != null)
        {
            SideQuestManager.Instance.OnSideQuestCompleted -= HandleSideQuestChanged;
            SideQuestManager.Instance.OnSideQuestActivated -= HandleSideQuestChanged;
        }
        StopAllCoroutines();
    }

    private IEnumerator PollRoutine()
    {
        var wait = new WaitForSeconds(0.5f);
        while (true)
        {
            yield return wait;
            var cm = CollectibleManager.Instance;
            if (cm != null && cm.Count != _lastCollectibleCount)
            {
                _lastCollectibleCount = cm.Count;
                UpdateCollectibleDisplay();
            }
            var sqm = SideQuestManager.Instance;
            int sqCount = sqm != null ? sqm.ActiveQuests.Count : 0;
            if (sqCount != _lastSideQuestCount)
            {
                _lastSideQuestCount = sqCount;
                TriggerUpdateAnimation();
            }

            // Fallback: detect active quest / chapter changes even if the
            // OnActiveQuestChanged subscription was missed (race condition).
            var sm = StoryManager.Instance;
            if (sm != null)
            {
                string curTitle = sm.ActiveQuest?.title;
                int curCh = sm.CurrentChapter;
                if (curTitle != _lastActiveQuestTitle || curCh != _lastActiveChapter)
                {
                    _lastActiveQuestTitle = curTitle;
                    _lastActiveChapter = curCh;
                    TriggerUpdateAnimation();
                }
            }
        }
    }

    private int _lastCollectibleCount = -1;
    private int _lastSideQuestCount = -1;
    private string _lastActiveQuestTitle = "__init__";
    private int _lastActiveChapter = -1;

    private void HandleQuestChanged(QuestData oldQuest, QuestData newQuest) => TriggerUpdateAnimation();
    private void HandleChapterChanged(int oldCh, int newCh) => TriggerUpdateAnimation();
    private void HandleSideQuestChanged(QuestData quest) => TriggerUpdateAnimation();

    private void TriggerUpdateAnimation()
    {
        if (_container == null)
        {
            UpdateDisplay();
            return;
        }

        _container.AddToClassList("quest-updating");

        // Wait 40ms for transition animation, then swap text and fade in smoothly
        _container.schedule.Execute(() =>
        {
            UpdateDisplay();
            _container.RemoveFromClassList("quest-updating");
        }).ExecuteLater(40);
    }

    private void UpdateCollectibleDisplay()
    {
        var cm = CollectibleManager.Instance;
        if (cm == null || _collectibles == null) return;
        _collectibles.text = $"Journals: {cm.Count}/{cm.Total}";
    }

    private void UpdateDisplay()
    {
        if (_chapter == null || _title == null || _objective == null) return;
        var sm = StoryManager.Instance;
        if (sm == null)
        {
            _chapter.text = "";
            _title.text = "";
            _objective.text = "";
            _collectibles.text = "";
            RebuildSideBlock();
            if (_container != null) _container.MarkDirtyRepaint();
            return;
        }

        if (sm.StoryComplete)
        {
            _chapter.text = "STORY COMPLETE";
            _title.text = "";
            _objective.text = "";
        }
        else
        {
            _chapter.text = "CHAPTER " + sm.CurrentChapter;
            var q = sm.ActiveQuest;
            if (q != null)
            {
                _title.text = q.title;
                _objective.text = q.objective;
            }
            else
            {
                var sqm = SideQuestManager.Instance;
                _title.text = (sqm != null && sqm.ActiveQuests.Count > 0)
                    ? "— Discover side quests —"
                    : "—";
                _objective.text = "";
            }
        }
        UpdateCollectibleDisplay();
        RebuildSideBlock();
        if (_container != null) _container.MarkDirtyRepaint();
    }

    private void RebuildSideBlock()
    {
        if (_sideLinesContainer == null) return;
        _sideLinesContainer.Clear();
        _sideLines.Clear();

        var sqm = SideQuestManager.Instance;
        bool hasSide = sqm != null && sqm.ActiveQuests.Count > 0;

        if (_divider != null)
        {
            _divider.style.display = hasSide ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (!hasSide)
        {
            _sidePanel.style.display = DisplayStyle.None;
            return;
        }

        _sidePanel.style.display = DisplayStyle.Flex;

        int count = Mathf.Min(sqm.ActiveQuests.Count, maxSideQuestLines);
        for (int i = 0; i < count; i++)
        {
            var quest = sqm.ActiveQuests[i];
            var line = new Label($"• {quest.title}");
            line.AddToClassList("side-line");
            _sideLinesContainer.Add(line);
            _sideLines.Add(line);
        }
        if (_container != null) _container.MarkDirtyRepaint();
    }

    private void OnGenerateCardBackground(MeshGenerationContext mgc)
    {
        var targetElement = mgc.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;
        float chamferSize = 10f;

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
        float badgeW = 40f;
        float badgeH = 5f;
        float startX = rect.width - badgeW - 16f;
        float startY = 3f;

        painter.lineWidth = 1.0f;
        for (float offset = 0; offset < badgeW; offset += 5f)
        {
            // Gold stripe
            painter.strokeColor = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.8f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset, startY));
            painter.LineTo(new Vector2(startX + offset - 3f, startY + badgeH));
            painter.Stroke();

            // Black stripe
            painter.strokeColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.9f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset + 2f, startY));
            painter.LineTo(new Vector2(startX + offset - 1f, startY + badgeH));
            painter.Stroke();
        }

        // 3. Draw outer border with gold breathing glow
        float pulse = 0.35f + Mathf.PingPong(Time.realtimeSinceStartup * 1.5f, 0.45f);
        Color strokeCol = new Color(217f / 255f, 199f / 255f, 115f / 255f, pulse * 0.5f);
        float lineWidth = 1.2f;

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

        // 4. Draw inner offset border
        float d = 3f;
        if (rect.width > d * 2 && rect.height > d * 2)
        {
            Color innerCol = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.1f);
            painter.strokeColor = innerCol;
            painter.lineWidth = 0.8f;
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
            painter.Arc(center + new Vector2(0.5f, 0.5f), 2.5f, 0f, 360f);
            painter.Fill();

            painter.fillColor = new Color(175f / 255f, 150f / 255f, 90f / 255f, 1.0f); // Gold screw head
            painter.BeginPath();
            painter.Arc(center, 2.0f, 0f, 360f);
            painter.Fill();

            painter.fillColor = Color.white;
            painter.BeginPath();
            painter.Arc(center - new Vector2(0.6f, 0.6f), 0.4f, 0f, 360f);
            painter.Fill();
        };

        float rOffset = 8f;
        drawRivet(new Vector2(rOffset, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rect.height - rOffset));
        drawRivet(new Vector2(rOffset, rect.height - rOffset));
    }
}
