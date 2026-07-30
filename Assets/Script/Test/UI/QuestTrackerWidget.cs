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

    private VisualElement _minimapContainer;
    private VisualElement _minimapImage;
    private VisualElement _minimapPlayerArrow;
    private bool _isMinimapMode = false;

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

        _minimapContainer = root.Q("MinimapContainer");
        _minimapImage = root.Q("MinimapImage");
        _minimapPlayerArrow = root.Q("MinimapPlayerArrow");

        bool isEndless = (StoryManager.Instance == null) && (GameModeManager.CurrentMode == GameMode.Endless);
        if (isEndless)
        {
            _isMinimapMode = true;
        }

        if (_chapter == null || _title == null || _objective == null || _sideLinesContainer == null)
            enabled = false;
    }

    private void OnEnable()
    {
        if (_container != null)
        {
            _container.generateVisualContent += OnGenerateCardBackground;
            _container.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        if (_minimapPlayerArrow != null)
        {
            _minimapPlayerArrow.generateVisualContent += OnDrawPlayerArrow;
        }

        bool isEndless = (StoryManager.Instance == null) && (GameModeManager.CurrentMode == GameMode.Endless);
        if (isEndless)
        {
            _isMinimapMode = true;
        }

        SubscribeToManagers();
        UpdateDisplay();
        StartCoroutine(PollRoutine());
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        _container?.MarkDirtyRepaint();
    }

    private void Start()
    {
        // Ensure MinimapController component exists in scene
        if (MinimapController.Instance == null)
        {
            var mc = FindAnyObjectByType<MinimapController>();
            if (mc == null)
            {
                var mcObj = new GameObject("MinimapController");
                mcObj.AddComponent<MinimapController>();
            }
        }

        if (GameModeManager.CurrentMode == GameMode.Endless)
        {
            _isMinimapMode = true;
        }

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
        {
            _container.generateVisualContent -= OnGenerateCardBackground;
            _container.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        if (_minimapPlayerArrow != null)
        {
            _minimapPlayerArrow.generateVisualContent -= OnDrawPlayerArrow;
        }

        if (MinimapController.Instance != null)
        {
            MinimapController.Instance.SetCameraActive(false);
        }

        if (_minimapImage != null)
        {
            _minimapImage.style.backgroundImage = StyleKeyword.Null;
        }

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

    private void Update()
    {
        // Update Player Arrow rotation matching FPS Camera Yaw
        if (_isMinimapMode && _minimapPlayerArrow != null && MinimapController.Instance != null)
        {
            float rot = MinimapController.Instance.CameraYawRotation;
            _minimapPlayerArrow.style.rotate = new Rotate(Angle.Degrees(rot));
        }

        // Story Mode - Key M Toggle
        bool isEndless = (StoryManager.Instance == null) && (GameModeManager.CurrentMode == GameMode.Endless);
        if (!isEndless)
        {
            bool mPressed = false;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame)
            {
                mPressed = true;
            }
            if (!mPressed)
            {
                try { if (Input.GetKeyDown(KeyCode.M)) mPressed = true; } catch {}
            }

            if (mPressed)
            {
                var focusEl = _container?.panel?.focusController?.focusedElement;
                if (focusEl is TextField || focusEl is TextInputBaseField<string>)
                    return;

                bool pauseActive = cowsins.PauseMenu.isPaused || (PauseManager.Instance != null && PauseManager.Instance.IsOpenOrTransitioning);
                bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
                bool panelActive = PanelManager.Instance != null && PanelManager.Instance.IsAnyPanelActive();
                bool journalActive = JournalUI.Instance != null && JournalUI.Instance.IsOpenOrTransitioning;
                var skillTree = FindAnyObjectByType<SkillTreeWidget>();
                bool skillTreeActive = skillTree != null && skillTree.IsOpenOrTransitioning;

                if (!pauseActive && !gameOver && !panelActive && !journalActive && !skillTreeActive)
                {
                    _isMinimapMode = !_isMinimapMode;
                    Debug.Log($"[QuestTrackerWidget] M Key Pressed! Toggle _isMinimapMode = {_isMinimapMode}");
                    UpdateDisplay();
                }
            }
        }
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
        if (_isMinimapMode)
        {
            UpdateDisplay();
            return;
        }

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

        // Endless mode check
        bool isEndless = (StoryManager.Instance == null) && (GameModeManager.CurrentMode == GameMode.Endless);
        if (isEndless)
        {
            _isMinimapMode = true;
        }

        // Camera lifecycle management
        if (MinimapController.Instance != null)
        {
            MinimapController.Instance.SetCameraActive(_isMinimapMode && enabled);
        }

        // If Minimap mode is active
        if (_isMinimapMode)
        {
            if (_minimapContainer != null) _minimapContainer.style.display = DisplayStyle.Flex;
            if (_mainPanel != null) _mainPanel.style.display = DisplayStyle.None;
            if (_sidePanel != null) _sidePanel.style.display = DisplayStyle.None;
            if (_divider != null) _divider.style.display = DisplayStyle.None;

            if (_minimapImage != null && MinimapController.Instance?.MinimapTexture != null)
            {
                _minimapImage.style.backgroundImage = Background.FromRenderTexture(MinimapController.Instance.MinimapTexture);
            }

            if (_container != null) _container.MarkDirtyRepaint();
            return;
        }

        // Story mode Quest View
        if (_minimapContainer != null) _minimapContainer.style.display = DisplayStyle.None;
        if (_mainPanel != null) _mainPanel.style.display = DisplayStyle.Flex;

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

    private void OnDrawPlayerArrow(MeshGenerationContext mgc)
    {
        var painter = mgc.painter2D;
        var rect = mgc.visualElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        float w = rect.width;
        float h = rect.height;

        // Shadow / Outer Glow
        painter.fillColor = new Color(0f, 0f, 0f, 0.7f);
        painter.BeginPath();
        painter.MoveTo(new Vector2(w * 0.5f, 1f));
        painter.LineTo(new Vector2(w - 1f, h - 2f));
        painter.LineTo(new Vector2(w * 0.5f, h - 6f));
        painter.LineTo(new Vector2(1f, h - 2f));
        painter.ClosePath();
        painter.Fill();

        // Metallic Gold Body
        painter.fillColor = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.95f);
        painter.BeginPath();
        painter.MoveTo(new Vector2(w * 0.5f, 0f));
        painter.LineTo(new Vector2(w - 2f, h - 3f));
        painter.LineTo(new Vector2(w * 0.5f, h - 7f));
        painter.LineTo(new Vector2(2f, h - 3f));
        painter.ClosePath();
        painter.Fill();

        // Bright Accent Spine
        painter.strokeColor = new Color(255f / 255f, 245f / 255f, 180f / 255f, 0.9f);
        painter.lineWidth = 1.2f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(w * 0.5f, 2f));
        painter.LineTo(new Vector2(w * 0.5f, h - 7f));
        painter.Stroke();
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
