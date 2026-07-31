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
    private Label _instructions;
    private Label _collectibles;
    private VisualElement _divider;
    private VisualElement _sidePanel;
    private Label _sideHeader;
    private VisualElement _sideLinesContainer;
    private readonly List<Label> _sideLines = new();

    private VisualElement _minimapContainer;
    private VisualElement _minimapImage;
    private VisualElement _minimapPlayerArrow;
    private VisualElement _minimapMarkerLayer;
    private VisualElement _minimapQuestMarker;
    private VisualElement _minimapEdgeArrow;
    private bool _isMinimapMode = false;

    // Fixed pre-instantiated VisualElement Pools for zero-GC blips rendering
    private readonly List<VisualElement> _zombieBlips = new List<VisualElement>();
    private readonly List<VisualElement> _journalBlips = new List<VisualElement>();
    private readonly List<VisualElement> _sideQuestBlips = new List<VisualElement>();
    private float _markerUpdateTimer = 0f;

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
        _instructions = root.Q<Label>("Instructions");
        _collectibles = root.Q<Label>("Collectibles");
        _divider = root.Q("QuestDivider");
        _sidePanel = root.Q("SidePanel");
        _sideHeader = root.Q<Label>("SideHeader");
        _sideLinesContainer = root.Q("SideLines");

        _minimapContainer = root.Q("MinimapContainer");
        _minimapImage = root.Q("MinimapImage");
        _minimapPlayerArrow = root.Q("MinimapPlayerArrow");
        _minimapMarkerLayer = root.Q("MinimapMarkerLayer");
        _minimapQuestMarker = root.Q("MinimapQuestMarker");
        _minimapEdgeArrow = root.Q("MinimapEdgeArrow");

        bool isEndless = (StoryManager.Instance == null) && (GameModeManager.CurrentMode == GameMode.Endless);
        if (isEndless)
        {
            _isMinimapMode = true;
        }

        if (_chapter == null || _title == null || _objective == null || _sideLinesContainer == null)
            enabled = false;
    }

    private void BuildVisualElementPools()
    {
        if (_minimapMarkerLayer == null) return;

        _minimapMarkerLayer.Clear();
        _zombieBlips.Clear();
        _journalBlips.Clear();
        _sideQuestBlips.Clear();

        // 1. Zombie Blips Pool (20 elements)
        for (int i = 0; i < 20; i++)
        {
            var blip = new VisualElement();
            blip.AddToClassList("minimap-blip-zombie");
            blip.style.position = Position.Absolute;
            blip.style.display = DisplayStyle.None;
            _minimapMarkerLayer.Add(blip);
            _zombieBlips.Add(blip);
        }

        // 2. Journal Blips Pool (10 elements)
        for (int i = 0; i < 10; i++)
        {
            var blip = new VisualElement();
            blip.AddToClassList("minimap-blip-journal");
            blip.style.position = Position.Absolute;
            blip.style.display = DisplayStyle.None;
            _minimapMarkerLayer.Add(blip);
            _journalBlips.Add(blip);
        }

        // 3. Side Quest Blips Pool (5 elements)
        for (int i = 0; i < 5; i++)
        {
            var blip = new VisualElement();
            blip.AddToClassList("minimap-blip-sidequest");
            blip.style.position = Position.Absolute;
            blip.style.display = DisplayStyle.None;
            _minimapMarkerLayer.Add(blip);
            _sideQuestBlips.Add(blip);
        }
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

        if (_minimapEdgeArrow != null)
        {
            _minimapEdgeArrow.generateVisualContent += OnDrawEdgeArrow;
        }

        BuildVisualElementPools();

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

        if (_minimapEdgeArrow != null)
        {
            _minimapEdgeArrow.generateVisualContent -= OnDrawEdgeArrow;
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
        if (_isMinimapMode)
        {
            // 1. Dual-Rate System: 60+ FPS smooth frame update for Player Arrow rotation and Main Quest Edge Arrow/Marker
            if (_minimapPlayerArrow != null && MinimapController.Instance != null)
            {
                float rot = MinimapController.Instance.CameraYawRotation;
                _minimapPlayerArrow.style.rotate = new Rotate(Angle.Degrees(rot));
            }

            if (_minimapQuestMarker != null && _minimapEdgeArrow != null)
            {
                UpdateMainQuestMarkerAndArrow();
            }

            // 2. Dual-Rate System: 10 FPS timer tick for Zombie, Journal, and Side Quest Blips
            _markerUpdateTimer += Time.deltaTime;
            if (_markerUpdateTimer >= 0.1f)
            {
                _markerUpdateTimer = 0f;
                UpdateMinimapBlips();
            }
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
                bool skillTreeActive = false;
                var skillTree = FindAnyObjectByType<SkillTreeWidget>();
                if (skillTree != null) skillTreeActive = skillTree.IsOpenOrTransitioning;

                if (!pauseActive && !gameOver && !panelActive && !journalActive && !skillTreeActive)
                {
                    _isMinimapMode = !_isMinimapMode;
                    Debug.Log($"[QuestTrackerWidget] M Key Pressed! Toggle _isMinimapMode = {_isMinimapMode}");
                    UpdateDisplay();
                }
            }
        }
    }

    private Vector3? GetMainQuestObjectivePosition()
    {
        var sm = StoryManager.Instance;
        var activeQuest = sm != null ? sm.ActiveQuest : null;

        // Priority 1: Active QuestBeacon matching activeQuest OR current chapter save room
        if (activeQuest != null)
        {
            for (int i = QuestBeacon.ActiveBeacons.Count - 1; i >= 0; i--)
            {
                var b = QuestBeacon.ActiveBeacons[i];
                if (b != null && b && b.gameObject.activeInHierarchy && b.showOnQuest == activeQuest)
                    return b.transform.position;
            }
        }
        else if (sm != null && sm.CurrentChapter > 0)
        {
            for (int i = QuestBeacon.ActiveBeacons.Count - 1; i >= 0; i--)
            {
                var b = QuestBeacon.ActiveBeacons[i];
                if (b != null && b && b.gameObject.activeInHierarchy && b.showOnChapter == sm.CurrentChapter && b.showOnQuest == null && b.showOnSideQuest == null)
                    return b.transform.position;
            }
        }

        // Priority 2: CollectibleQuestObjective (uncollected journal)
        if (activeQuest != null)
        {
            for (int i = CollectibleQuestObjective.ActiveObjectives.Count - 1; i >= 0; i--)
            {
                var cqo = CollectibleQuestObjective.ActiveObjectives[i];
                if (cqo != null && cqo && cqo.gameObject.activeInHierarchy && cqo.targetQuest == activeQuest && cqo.requiredCollectibles != null)
                {
                    for (int j = 0; j < cqo.requiredCollectibles.Length; j++)
                    {
                        var c = cqo.requiredCollectibles[j];
                        if (c != null && c && c.gameObject.activeInHierarchy && !c.IsPicked)
                            return c.transform.position;
                    }
                }
            }
        }

        // Priority 3: QuestTrigger / QuestInteractable / KillCountObjective matching activeQuest
        if (activeQuest != null)
        {
            for (int i = QuestTrigger.ActiveTriggers.Count - 1; i >= 0; i--)
            {
                var qt = QuestTrigger.ActiveTriggers[i];
                if (qt != null && qt && qt.gameObject.activeInHierarchy && qt.targetQuest == activeQuest)
                    return qt.transform.position;
            }
            for (int i = QuestInteractable.ActiveInteractables.Count - 1; i >= 0; i--)
            {
                var qi = QuestInteractable.ActiveInteractables[i];
                if (qi != null && qi && qi.gameObject.activeInHierarchy && qi.questTrigger != null && qi.questTrigger.targetQuest == activeQuest)
                    return qi.transform.position;
            }
            for (int i = KillCountObjective.ActiveObjectives.Count - 1; i >= 0; i--)
            {
                var kco = KillCountObjective.ActiveObjectives[i];
                if (kco != null && kco && kco.gameObject.activeInHierarchy && kco.targetQuest == activeQuest)
                    return kco.transform.position;
            }
        }

        // Priority 4: Strict Main Story Fallback (active QuestBeacon not assigned to side quest)
        for (int i = QuestBeacon.ActiveBeacons.Count - 1; i >= 0; i--)
        {
            var b = QuestBeacon.ActiveBeacons[i];
            if (b != null && b && b.gameObject.activeInHierarchy && b.IsActive && b.showOnSideQuest == null)
                return b.transform.position;
        }

        return null;
    }

    private void UpdateMainQuestMarkerAndArrow()
    {
        Vector3? targetPos = GetMainQuestObjectivePosition();
        if (!targetPos.HasValue || Camera.main == null)
        {
            _minimapQuestMarker.style.display = DisplayStyle.None;
            _minimapEdgeArrow.style.display = DisplayStyle.None;
            return;
        }

        Vector3 playerPos = Camera.main.transform.position;
        Vector3 worldPos = targetPos.Value;

        float orthoSize = MinimapController.Instance != null ? MinimapController.Instance.OrthographicSize : 22f;
        if (orthoSize < 0.1f) orthoSize = 22f;

        float scale = 146f / orthoSize;
        float uiDX = (worldPos.x - playerPos.x) * scale;
        float uiDY = -(worldPos.z - playerPos.z) * scale;

        float distUI = Mathf.Sqrt(uiDX * uiDX + uiDY * uiDY);
        float safeDist = Mathf.Max(distUI, 0.001f);

        bool isOutside = distUI > 130f;

        if (!isOutside)
        {
            // Inside Minimap bounds: show Main Quest Marker
            _minimapQuestMarker.style.display = DisplayStyle.Flex;
            _minimapEdgeArrow.style.display = DisplayStyle.None;

            _minimapQuestMarker.style.left = 146f + uiDX - 8f;
            _minimapQuestMarker.style.top = 146f + uiDY - 8f;
        }
        else
        {
            // Outside Minimap bounds: show Edge Arrow smoothly clamped at 128px radius
            _minimapQuestMarker.style.display = DisplayStyle.None;
            _minimapEdgeArrow.style.display = DisplayStyle.Flex;

            float clampDX = (uiDX / safeDist) * 128f;
            float clampDY = (uiDY / safeDist) * 128f;

            _minimapEdgeArrow.style.left = 146f + clampDX - 8f;
            _minimapEdgeArrow.style.top = 146f + clampDY - 8f;

            float angle = Mathf.Atan2(uiDX, -uiDY) * Mathf.Rad2Deg;
            _minimapEdgeArrow.style.rotate = new Rotate(Angle.Degrees(angle));
        }
    }

    private void UpdateMinimapBlips()
    {
        if (!_isMinimapMode || Camera.main == null || _minimapMarkerLayer == null) return;

        Vector3 playerPos = Camera.main.transform.position;
        float orthoSize = MinimapController.Instance != null ? MinimapController.Instance.OrthographicSize : 22f;
        if (orthoSize < 0.1f) orthoSize = 22f;
        float scale = 146f / orthoSize;

        // 1. Zombie Blips (up to 20, excluding companion allies)
        int zCount = 0;
        for (int i = EnemyRegistry.ActiveEnemies.Count - 1; i >= 0; i--)
        {
            if (zCount >= _zombieBlips.Count) break;
            var enemy = EnemyRegistry.ActiveEnemies[i];
            var mb = enemy as MonoBehaviour;
            if (mb == null || !mb || mb.gameObject == null || !mb.gameObject.activeInHierarchy || enemy.IsDead) continue;

            Vector3 pos = mb.transform.position;
            if (Mathf.Abs(pos.y - playerPos.y) > 12f) continue;

            float dx = (pos.x - playerPos.x) * scale;
            float dz = -(pos.z - playerPos.z) * scale;
            float distUI = Mathf.Sqrt(dx * dx + dz * dz);

            if (distUI <= 128f)
            {
                var blip = _zombieBlips[zCount];
                blip.style.left = 146f + dx - 3f;
                blip.style.top = 146f + dz - 3f;
                blip.style.display = DisplayStyle.Flex;
                zCount++;
            }
        }
        for (int i = zCount; i < _zombieBlips.Count; i++)
            _zombieBlips[i].style.display = DisplayStyle.None;

        // 2. Journal Blips (up to 10)
        int jCount = 0;
        for (int i = Collectible.ActiveCollectibles.Count - 1; i >= 0; i--)
        {
            if (jCount >= _journalBlips.Count) break;
            var c = Collectible.ActiveCollectibles[i];
            if (c == null || !c || c.gameObject == null || !c.gameObject.activeInHierarchy || c.IsPicked) continue;

            Vector3 pos = c.transform.position;
            if (Mathf.Abs(pos.y - playerPos.y) > 12f) continue;

            float dx = (pos.x - playerPos.x) * scale;
            float dz = -(pos.z - playerPos.z) * scale;
            float distUI = Mathf.Sqrt(dx * dx + dz * dz);

            if (distUI <= 128f)
            {
                var blip = _journalBlips[jCount];
                blip.style.left = 146f + dx - 4f;
                blip.style.top = 146f + dz - 4f;
                blip.style.display = DisplayStyle.Flex;
                jCount++;
            }
        }
        for (int i = jCount; i < _journalBlips.Count; i++)
            _journalBlips[i].style.display = DisplayStyle.None;

        // 3. Side Quest Green Blips (up to 5)
        int sCount = 0;
        var sqm = SideQuestManager.Instance;
        if (sqm != null && sqm.ActiveQuests != null)
        {
            for (int i = QuestBeacon.ActiveBeacons.Count - 1; i >= 0; i--)
            {
                if (sCount >= _sideQuestBlips.Count) break;
                var b = QuestBeacon.ActiveBeacons[i];
                if (b == null || !b || b.gameObject == null || !b.gameObject.activeInHierarchy || !b.IsActive || b.showOnSideQuest == null) continue;

                Vector3 pos = b.transform.position;
                float dx = (pos.x - playerPos.x) * scale;
                float dz = -(pos.z - playerPos.z) * scale;
                float distUI = Mathf.Sqrt(dx * dx + dz * dz);

                if (distUI <= 128f)
                {
                    var blip = _sideQuestBlips[sCount];
                    blip.style.left = 146f + dx - 5f;
                    blip.style.top = 146f + dz - 5f;
                    blip.style.display = DisplayStyle.Flex;
                    sCount++;
                }
            }
        }
        for (int i = sCount; i < _sideQuestBlips.Count; i++)
            _sideQuestBlips[i].style.display = DisplayStyle.None;
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

        bool isEndless = (StoryManager.Instance == null) && (GameModeManager.CurrentMode == GameMode.Endless);
        if (isEndless)
        {
            _isMinimapMode = true;
        }

        if (MinimapController.Instance != null)
        {
            MinimapController.Instance.SetCameraActive(_isMinimapMode);
        }

        if (_minimapContainer != null)
        {
            _minimapContainer.style.display = _isMinimapMode ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_isMinimapMode)
        {
            if (_mainPanel != null) _mainPanel.style.display = DisplayStyle.None;
            if (_sidePanel != null) _sidePanel.style.display = DisplayStyle.None;
            if (_divider != null) _divider.style.display = DisplayStyle.None;

            if (_minimapImage != null && MinimapController.Instance != null)
            {
                var tex = MinimapController.Instance.MinimapTexture;
                if (tex != null)
                {
                    _minimapImage.style.backgroundImage = Background.FromRenderTexture(tex);
                }
            }
        }
        else
        {
            if (_mainPanel != null) _mainPanel.style.display = DisplayStyle.Flex;

            UpdateStoryContent();
            UpdateSideContent();
        }

        _container?.MarkDirtyRepaint();
    }

    private void UpdateStoryContent()
    {
        var sm = StoryManager.Instance;
        if (sm == null)
        {
            _chapter.text = "CHAPTER 1";
            _title.text = "SURVIVAL";
            _objective.text = "Survive the outbreak.";
            if (_instructions != null) _instructions.text = "";
            UpdateCollectibleDisplay();
            return;
        }

        if (sm.StoryComplete)
        {
            _chapter.text = "STORY COMPLETE";
            _title.text = "";
            _objective.text = "";
            if (_instructions != null) _instructions.text = "";
            UpdateCollectibleDisplay();
            return;
        }

        _chapter.text = $"CHAPTER {sm.CurrentChapter}";
        var quest = sm.ActiveQuest;
        if (quest != null)
        {
            _title.text = (quest.title ?? "OBJECTIVE").ToUpper();
            _objective.text = !string.IsNullOrEmpty(quest.objective) ? quest.objective : (quest.description ?? "");
            if (_instructions != null)
            {
                _instructions.text = string.IsNullOrEmpty(quest.instructions) ? "" : quest.instructions;
            }
        }
        else
        {
            var sqm = SideQuestManager.Instance;
            _title.text = (sqm != null && sqm.ActiveQuests != null && sqm.ActiveQuests.Count > 0)
                ? "— DISCOVER SIDE QUESTS —"
                : "—";
            _objective.text = "";
            if (_instructions != null) _instructions.text = "";
        }

        UpdateCollectibleDisplay();
    }

    private void UpdateSideContent()
    {
        var sqm = SideQuestManager.Instance;
        var activeQuests = sqm != null ? sqm.ActiveQuests : null;
        if (activeQuests == null || activeQuests.Count == 0 || _sideLinesContainer == null)
        {
            if (_sidePanel != null) _sidePanel.style.display = DisplayStyle.None;
            if (_divider != null) _divider.style.display = DisplayStyle.None;
            return;
        }

        if (_sidePanel != null) _sidePanel.style.display = DisplayStyle.Flex;
        if (_divider != null) _divider.style.display = DisplayStyle.Flex;

        _sideLinesContainer.Clear();
        _sideLines.Clear();

        int count = Mathf.Min(activeQuests.Count, maxSideQuestLines);
        for (int i = 0; i < count; i++)
        {
            var q = activeQuests[i];
            if (q == null) continue;

            var label = new Label
            {
                text = $"• {q.title}"
            };
            label.AddToClassList("side-line");
            _sideLinesContainer.Add(label);
            _sideLines.Add(label);
        }
    }

    private void OnDrawEdgeArrow(MeshGenerationContext mgc)
    {
        var painter = mgc.painter2D;
        // Cyan fill with white border for Main Quest edge arrow
        painter.fillColor = new Color(0f / 255f, 229f / 255f, 255f / 255f, 1f);
        painter.strokeColor = Color.white;
        painter.lineWidth = 1.2f;

        painter.BeginPath();
        painter.MoveTo(new Vector2(8f, 1f));   // Top tip
        painter.LineTo(new Vector2(15f, 15f)); // Right base
        painter.LineTo(new Vector2(8f, 11f));  // Inner notch
        painter.LineTo(new Vector2(1f, 15f));  // Left base
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();
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
        painter.LineTo(new Vector2(w * 0.5f, h - 6f));
        painter.Stroke();

        // Screw rivet at arrow center
        painter.fillColor = new Color(175f / 255f, 150f / 255f, 90f / 255f, 1f);
        painter.BeginPath();
        painter.Arc(new Vector2(w * 0.5f, h * 0.55f), 1.8f, 0f, 360f);
        painter.Fill();
    }

    private void OnGenerateCardBackground(MeshGenerationContext mgc)
    {
        var targetElement = mgc.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;
        float chamferSize = 10f;

        // 1. Draw solid dark blue-gray translucent background shape
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

        // 2. Draw yellow-black diagonal warning stripes
        float badgeW = 40f;
        float badgeH = 5f;
        float startX = rect.width - badgeW - 16f;
        float startY = 3f;

        painter.lineWidth = 1.0f;
        for (float offset = 0; offset < badgeW; offset += 5f)
        {
            painter.strokeColor = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.8f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset, startY));
            painter.LineTo(new Vector2(startX + offset - 3f, startY + badgeH));
            painter.Stroke();

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

            painter.fillColor = new Color(175f / 255f, 150f / 255f, 90f / 255f, 1.0f);
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
