using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;

[DefaultExecutionOrder(100)]
public class QuestTrackerWidget : MonoBehaviour
{
    [Header("Layout")]
    public int maxSideQuestLines = 6;

    [Header("Audio SFX")]
    public AudioClip toggleSFX;

#if UNITY_EDITOR
    private void Reset() => AutoAssignSFX();
    private void OnValidate() => AutoAssignSFX();
    private void AutoAssignSFX()
    {
        if (toggleSFX == null)
            toggleSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Engine/Cowsins/SFX/UI/UIHover_SFX.wav");
    }
#endif

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

    private VisualElement _contentWrapper;
    private VisualElement _questGroup;
    private bool _isTransitioning = false;
    private Coroutine _transitionCoroutine;
    private float _cachedQuestHeight = 0f;
    private float _currentMinimapOpacity = 0f;
    private float _currentQuestOpacity = 1f;

    private Coroutine _mainQuestTransitionCoroutine;
    private IVisualElementScheduledItem _glowScheduledItem;
    private bool _isQuestUpdating = false;
    private bool _hasPendingQuestUpdate = false;
    private bool _hasPendingReentryGlow = false;
    private readonly List<Label> _exitingLabels = new();
    private readonly List<string> _lastSideQuestTitles = new();

    // Fixed pre-instantiated VisualElement Pools for zero-GC blips rendering across 6 categories
    private readonly List<VisualElement> _zombieBlips = new List<VisualElement>();
    private readonly List<VisualElement> _companionBlips = new List<VisualElement>();
    private readonly List<VisualElement> _specialBlips = new List<VisualElement>();
    private readonly List<VisualElement> _journalBlips = new List<VisualElement>();
    private readonly List<VisualElement> _sideQuestBlips = new List<VisualElement>();
    private readonly List<VisualElement> _bossBlips = new List<VisualElement>();
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

        if (_contentWrapper == null)
        {
            _contentWrapper = _container?.Q("ContentWrapper") ?? new VisualElement { name = "ContentWrapper" };
            _contentWrapper.AddToClassList("content-wrapper");
            if (_contentWrapper.parent == null && _container != null)
            {
                _container.Add(_contentWrapper);
            }
        }

        if (_questGroup == null)
        {
            _questGroup = new VisualElement { name = "QuestGroup" };
            _questGroup.AddToClassList("quest-group");

            if (_mainPanel != null && _mainPanel.parent != null) _mainPanel.RemoveFromHierarchy();
            if (_divider != null && _divider.parent != null) _divider.RemoveFromHierarchy();
            if (_sidePanel != null && _sidePanel.parent != null) _sidePanel.RemoveFromHierarchy();

            if (_mainPanel != null) _questGroup.Add(_mainPanel);
            if (_divider != null) _questGroup.Add(_divider);
            if (_sidePanel != null) _questGroup.Add(_sidePanel);

            if (_contentWrapper != null) _contentWrapper.Add(_questGroup);
        }

        if (_minimapContainer != null && _minimapContainer.parent != _contentWrapper && _contentWrapper != null)
        {
            _minimapContainer.RemoveFromHierarchy();
            _contentWrapper.Add(_minimapContainer);
        }

        bool isEndless = (StoryManager.Instance == null) && (GameModeManager.CurrentMode == GameMode.Endless);
        if (isEndless)
        {
            _isMinimapMode = true;
            _currentMinimapOpacity = 1f;
            _currentQuestOpacity = 0f;
        }

        if (_chapter == null || _title == null || _objective == null || _sideLinesContainer == null)
            enabled = false;
    }

    private void BuildVisualElementPools()
    {
        if (_minimapMarkerLayer == null) return;

        _minimapMarkerLayer.Clear();
        _zombieBlips.Clear();
        _companionBlips.Clear();
        _journalBlips.Clear();
        _sideQuestBlips.Clear();
        _specialBlips.Clear();
        _bossBlips.Clear();

        // 1. Zombie Blips Pool (40 elements - bottom DOM layer)
        for (int i = 0; i < 40; i++)
        {
            var blip = new VisualElement();
            blip.AddToClassList("minimap-blip-zombie");
            blip.style.position = Position.Absolute;
            blip.style.display = DisplayStyle.None;
            _minimapMarkerLayer.Add(blip);
            _zombieBlips.Add(blip);
        }

        // 2. Companion Blips Pool (5 elements)
        for (int i = 0; i < 5; i++)
        {
            var blip = new VisualElement();
            blip.AddToClassList("minimap-blip-companion");
            blip.style.position = Position.Absolute;
            blip.style.display = DisplayStyle.None;
            _minimapMarkerLayer.Add(blip);
            _companionBlips.Add(blip);
        }

        // 3. Journal Blips Pool (10 elements)
        for (int i = 0; i < 10; i++)
        {
            var blip = new VisualElement();
            blip.AddToClassList("minimap-blip-journal");
            blip.style.position = Position.Absolute;
            blip.style.display = DisplayStyle.None;
            _minimapMarkerLayer.Add(blip);
            _journalBlips.Add(blip);
        }

        // 4. Side Quest Blips Pool (5 elements)
        for (int i = 0; i < 5; i++)
        {
            var blip = new VisualElement();
            blip.AddToClassList("minimap-blip-sidequest");
            blip.style.position = Position.Absolute;
            blip.style.display = DisplayStyle.None;
            _minimapMarkerLayer.Add(blip);
            _sideQuestBlips.Add(blip);
        }

        // 5. Special Infected Blips Pool (8 elements)
        for (int i = 0; i < 8; i++)
        {
            var blip = new VisualElement();
            blip.AddToClassList("minimap-blip-special");
            blip.style.position = Position.Absolute;
            blip.style.display = DisplayStyle.None;
            _minimapMarkerLayer.Add(blip);
            _specialBlips.Add(blip);
        }

        // 6. Boss Blips Pool (5 elements - top DOM layer)
        for (int i = 0; i < 5; i++)
        {
            var blip = new VisualElement();
            blip.AddToClassList("minimap-blip-boss");
            blip.style.position = Position.Absolute;
            blip.style.display = DisplayStyle.None;
            _minimapMarkerLayer.Add(blip);
            _bossBlips.Add(blip);
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
        if (!_isTransitioning && !_isMinimapMode && _container != null && _container.layout.height > 0)
        {
            _cachedQuestHeight = _container.layout.height;
        }
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
            _currentMinimapOpacity = 1f;
            _currentQuestOpacity = 0f;
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
        CollectibleManager.OnJournalCollected -= HandleCollectibleCollected;
        CollectibleManager.OnJournalCollected += HandleCollectibleCollected;
    }

    private void ClearExitingLabels()
    {
        for (int i = _exitingLabels.Count - 1; i >= 0; i--)
        {
            var label = _exitingLabels[i];
            if (label != null && label.parent != null)
            {
                label.RemoveFromHierarchy();
            }
        }
        _exitingLabels.Clear();
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

        _glowScheduledItem?.Pause();
        _glowScheduledItem = null;

        if (_mainPanel != null)
        {
            _mainPanel.RemoveFromClassList("quest-title-glow");
            _mainPanel.style.opacity = StyleKeyword.Null;
        }

        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }

        if (_mainQuestTransitionCoroutine != null)
        {
            StopCoroutine(_mainQuestTransitionCoroutine);
            _mainQuestTransitionCoroutine = null;
        }

        _isTransitioning = false;
        _isQuestUpdating = false;

        ApplyFinalModeState(_isMinimapMode);
        ClearExitingLabels();

        if (_minimapImage != null)
        {
            _minimapImage.SetBackgroundImageSafe((RenderTexture)null);
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
        CollectibleManager.OnJournalCollected -= HandleCollectibleCollected;
        StopAllCoroutines();
    }

    private void Update()
    {
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
                    bool targetIsMinimap = !_isMinimapMode;
                    if (cowsins.SoundManager.Instance != null && toggleSFX != null)
                    {
                        try { cowsins.SoundManager.Instance.PlaySound(toggleSFX, 0f, 0f, false); } catch {}
                    }
                    StartTransition(targetIsMinimap);
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (_isMinimapMode || _isTransitioning)
        {
            Transform pTrans = MinimapController.Instance != null && MinimapController.Instance.PlayerTransform != null
                ? MinimapController.Instance.PlayerTransform
                : (Camera.main != null ? Camera.main.transform : null);

            if (pTrans == null)
            {
                HideAllBlipPools();
                return;
            }

            // 1. Dual-Rate System: Smooth frame update for Player Arrow rotation and Main Quest Edge Arrow/Marker
            if (_minimapPlayerArrow != null && MinimapController.Instance != null)
            {
                float rot = MinimapController.Instance.CameraYawRotation;
                _minimapPlayerArrow.style.rotate = new Rotate(Angle.Degrees(rot));
            }

            if (_minimapQuestMarker != null && _minimapEdgeArrow != null)
            {
                UpdateMainQuestMarkerAndArrow(pTrans);
            }

            // Tint adjustment to prevent daytime solar overexposure glare on minimap
            if (_minimapImage != null)
            {
                float dayWeight = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentDayWeight : 0f;
                float factor = Mathf.Lerp(1.0f, 0.72f, dayWeight);
                _minimapImage.style.unityBackgroundImageTintColor = new Color(factor, factor, factor, 1.0f);
            }

            // 2. Dual-Rate System: 10 FPS timer tick for secondary blips
            _markerUpdateTimer += Time.deltaTime;
            if (_markerUpdateTimer >= 0.1f)
            {
                _markerUpdateTimer = 0f;
                UpdateMinimapBlips(pTrans);
            }

            // 3. Smooth frame-rate pulse effect for all active minimap blips & markers
            ApplyMinimapPulseEffects();
        }
    }

    private void ApplyMinimapPulseEffects()
    {
        if (!_isMinimapMode && !_isTransitioning) return;

        float pulseAngle = Time.unscaledTime * 4f;
        float opacityPulse = 0.85f + 0.15f * Mathf.Sin(pulseAngle);
        float scalePulse = 1.125f + 0.075f * Mathf.Sin(pulseAngle); // Mathematically exact [1.05x, 1.20x] range

        var scaleStyle = new StyleScale(new Scale(new Vector2(scalePulse, scalePulse)));

        ApplyPoolPulse(_zombieBlips, opacityPulse, false, scaleStyle);
        ApplyPoolPulse(_companionBlips, opacityPulse, true, scaleStyle);
        ApplyPoolPulse(_specialBlips, opacityPulse, true, scaleStyle);
        ApplyPoolPulse(_bossBlips, opacityPulse, true, scaleStyle); // Boss blips pulse scale breathing!
        ApplyPoolPulse(_journalBlips, opacityPulse, true, scaleStyle);
        ApplyPoolPulse(_sideQuestBlips, opacityPulse, true, scaleStyle);

        if (_minimapPlayerArrow != null && _minimapPlayerArrow.style.display == DisplayStyle.Flex)
        {
            _minimapPlayerArrow.style.opacity = opacityPulse;
        }

        if (_minimapQuestMarker != null && _minimapQuestMarker.style.display == DisplayStyle.Flex)
        {
            _minimapQuestMarker.style.opacity = opacityPulse;
            _minimapQuestMarker.style.scale = scaleStyle;
        }

        if (_minimapEdgeArrow != null && _minimapEdgeArrow.style.display == DisplayStyle.Flex)
        {
            _minimapEdgeArrow.style.opacity = opacityPulse;
        }
    }

    private void ApplyPoolPulse(List<VisualElement> pool, float opacity, bool enableScalePulse, StyleScale scaleStyle)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var blip = pool[i];
            if (blip.style.display == DisplayStyle.Flex)
            {
                blip.style.opacity = opacity;
                if (enableScalePulse)
                {
                    blip.style.scale = scaleStyle;
                }
            }
        }
    }

    private void HideAllBlipPools()
    {
        for (int i = 0; i < _zombieBlips.Count; i++) _zombieBlips[i].style.display = DisplayStyle.None;
        for (int i = 0; i < _companionBlips.Count; i++) _companionBlips[i].style.display = DisplayStyle.None;
        for (int i = 0; i < _journalBlips.Count; i++) _journalBlips[i].style.display = DisplayStyle.None;
        for (int i = 0; i < _sideQuestBlips.Count; i++) _sideQuestBlips[i].style.display = DisplayStyle.None;
        for (int i = 0; i < _specialBlips.Count; i++) _specialBlips[i].style.display = DisplayStyle.None;
        for (int i = 0; i < _bossBlips.Count; i++) _bossBlips[i].style.display = DisplayStyle.None;
        if (_minimapQuestMarker != null) _minimapQuestMarker.style.display = DisplayStyle.None;
        if (_minimapEdgeArrow != null) _minimapEdgeArrow.style.display = DisplayStyle.None;
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

        // Priority 4: Fallback to active save room or chapter objective position
        var saveRoom = FindAnyObjectByType<CheckPointView>();
        if (saveRoom != null && saveRoom.gameObject.activeInHierarchy)
        {
            return saveRoom.transform.position;
        }

        return null;
    }

    private void UpdateMainQuestMarkerAndArrow(Transform pTrans)
    {
        Vector3? targetPos = GetMainQuestObjectivePosition();
        if (!targetPos.HasValue || pTrans == null)
        {
            if (_minimapQuestMarker != null) _minimapQuestMarker.style.display = DisplayStyle.None;
            if (_minimapEdgeArrow != null) _minimapEdgeArrow.style.display = DisplayStyle.None;
            return;
        }

        Vector3 playerPos = pTrans.position;
        Vector3 worldPos = targetPos.Value;

        float halfWidth = _minimapMarkerLayer != null && _minimapMarkerLayer.layout.width > 0 ? _minimapMarkerLayer.layout.width * 0.5f : 146f;
        float orthoSize = MinimapController.Instance != null ? MinimapController.Instance.OrthographicSize : 22f;
        if (orthoSize < 0.1f) orthoSize = 22f;

        float scale = halfWidth / orthoSize;
        float uiDX = (worldPos.x - playerPos.x) * scale;
        float uiDY = -(worldPos.z - playerPos.z) * scale;

        float markerElementHalf = 8.0f; // Main quest marker 16px element half-width
        float arrowElementHalf = 8.0f;  // Edge arrow 16px element half-width
        float edgeMargin = 11.5f;       // 11.5px safety margin for 1.20x quest marker scale & 45 deg edge arrow rotation

        float markerEdgeLimit = halfWidth - edgeMargin; // 134.5px
        float arrowEdgeLimit = markerEdgeLimit;         // 134.5px (0px jump, 0px clipping)
        bool isOutside = Mathf.Abs(uiDX) > markerEdgeLimit || Mathf.Abs(uiDY) > markerEdgeLimit;

        if (!isOutside)
        {
            // Inside Minimap bounds: show Main Quest Marker
            if (_minimapQuestMarker != null)
            {
                _minimapQuestMarker.style.display = DisplayStyle.Flex;
                _minimapQuestMarker.style.left = halfWidth + uiDX - markerElementHalf;
                _minimapQuestMarker.style.top = halfWidth + uiDY - markerElementHalf;
            }
            if (_minimapEdgeArrow != null) _minimapEdgeArrow.style.display = DisplayStyle.None;
        }
        else
        {
            // Outside Minimap bounds: show Edge Arrow smoothly clamped to square boundary
            if (_minimapQuestMarker != null) _minimapQuestMarker.style.display = DisplayStyle.None;
            if (_minimapEdgeArrow != null)
            {
                _minimapEdgeArrow.style.display = DisplayStyle.Flex;

                float maxCoord = Mathf.Max(Mathf.Abs(uiDX), Mathf.Abs(uiDY));
                float scaleFactor = arrowEdgeLimit / maxCoord;

                float clampDX = uiDX * scaleFactor;
                float clampDY = uiDY * scaleFactor;

                _minimapEdgeArrow.style.left = halfWidth + clampDX - arrowElementHalf;
                _minimapEdgeArrow.style.top = halfWidth + clampDY - arrowElementHalf;

                float angle = Mathf.Atan2(uiDX, -uiDY) * Mathf.Rad2Deg;
                _minimapEdgeArrow.style.rotate = new Rotate(Angle.Degrees(angle));
            }
        }
    }

    private struct EnemySortEntry
    {
        public MonoBehaviour EnemyMB;
        public EnemyType EnemyType;
        public float SqrDist;
    }

    private readonly List<EnemySortEntry> _normalCandidates = new List<EnemySortEntry>();
    private readonly List<EnemySortEntry> _specialCandidates = new List<EnemySortEntry>();
    private readonly List<EnemySortEntry> _bossCandidates = new List<EnemySortEntry>();

    private void UpdateMinimapBlips(Transform pTrans)
    {
        if (!_isMinimapMode || pTrans == null || _minimapMarkerLayer == null)
        {
            HideAllBlipPools();
            return;
        }

        Vector3 playerPos = pTrans.position;
        float halfWidth = _minimapMarkerLayer.layout.width > 0 ? _minimapMarkerLayer.layout.width * 0.5f : 146f;
        float orthoSize = MinimapController.Instance != null ? MinimapController.Instance.OrthographicSize : 22f;
        if (orthoSize < 0.1f) orthoSize = 22f;
        float scale = halfWidth / orthoSize;

        // 1. Companion Blips (up to 5 elements)
        int cCount = 0;
        float companionHalfSize = 5.0f; // 10px
        float companionEdgeLimit = halfWidth - (companionHalfSize * 1.20f);

        for (int i = CompanionAI.ActiveCompanions.Count - 1; i >= 0; i--)
        {
            if (cCount >= _companionBlips.Count) break;
            var companion = CompanionAI.ActiveCompanions[i];
            if (companion == null || !companion || companion.gameObject == null || !companion.gameObject.activeInHierarchy) continue;

            // Filter state: Waiting (in save room), Following (active), Downed (wounded)
            if (companion.CurrentState != CompanionAI.State.Waiting &&
                companion.CurrentState != CompanionAI.State.Following &&
                companion.CurrentState != CompanionAI.State.Downed) continue;

            Vector3 pos = companion.transform.position;
            if (Mathf.Abs(pos.y - playerPos.y) > 12f) continue;

            float dx = (pos.x - playerPos.x) * scale;
            float dz = -(pos.z - playerPos.z) * scale;

            if (Mathf.Abs(dx) <= companionEdgeLimit && Mathf.Abs(dz) <= companionEdgeLimit)
            {
                var blip = _companionBlips[cCount];
                blip.style.left = halfWidth + dx - companionHalfSize;
                blip.style.top = halfWidth + dz - companionHalfSize;
                blip.style.display = DisplayStyle.Flex;
                cCount++;
            }
        }
        for (int i = cCount; i < _companionBlips.Count; i++)
            _companionBlips[i].style.display = DisplayStyle.None;

        // 2. Enemy Blips (Two-stage selection and type routing)
        _normalCandidates.Clear();
        _specialCandidates.Clear();
        _bossCandidates.Clear();

        for (int i = EnemyRegistry.ActiveEnemies.Count - 1; i >= 0; i--)
        {
            var enemy = EnemyRegistry.ActiveEnemies[i];
            var mb = enemy as MonoBehaviour;
            if (mb == null || !mb || mb.gameObject == null || !mb.gameObject.activeInHierarchy || enemy.IsDead) continue;
            if (mb is CompanionAI) continue; // Exclude companions from enemy loops

            Vector3 pos = mb.transform.position;
            if (Mathf.Abs(pos.y - playerPos.y) > 12f) continue;

            float dx = (pos.x - playerPos.x) * scale;
            float dz = -(pos.z - playerPos.z) * scale;
            float maxOffset = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));

            float enemyHalfSize = enemy.EnemyType == EnemyType.Boss ? (9.0f * 1.20f) : (enemy.EnemyType == EnemyType.Special ? (5.0f * 1.20f) : 3.0f);
            if (maxOffset > halfWidth - enemyHalfSize) continue;

            float sqrDist = dx * dx + dz * dz;
            var entry = new EnemySortEntry { EnemyMB = mb, EnemyType = enemy.EnemyType, SqrDist = sqrDist };

            if (enemy.EnemyType == EnemyType.Boss) _bossCandidates.Add(entry);
            else if (enemy.EnemyType == EnemyType.Special) _specialCandidates.Add(entry);
            else _normalCandidates.Add(entry);
        }

        // Sort candidates by distance ascending (closest first)
        _normalCandidates.Sort((a, b) => a.SqrDist.CompareTo(b.SqrDist));
        _specialCandidates.Sort((a, b) => a.SqrDist.CompareTo(b.SqrDist));
        _bossCandidates.Sort((a, b) => a.SqrDist.CompareTo(b.SqrDist));

        // Render Normal Zombies (up to 40) - Reverse assignment so closest threat gets highest DOM index in pool
        int nTotal = Mathf.Min(_normalCandidates.Count, _zombieBlips.Count);
        float normalHalfSize = 3.0f; // 6px
        for (int i = 0; i < _zombieBlips.Count; i++)
        {
            if (i < nTotal)
            {
                int srcIdx = nTotal - 1 - i;
                var entry = _normalCandidates[srcIdx];
                Vector3 pos = entry.EnemyMB.transform.position;
                float dx = (pos.x - playerPos.x) * scale;
                float dz = -(pos.z - playerPos.z) * scale;

                var blip = _zombieBlips[i];
                blip.style.left = halfWidth + dx - normalHalfSize;
                blip.style.top = halfWidth + dz - normalHalfSize;
                blip.style.display = DisplayStyle.Flex;
            }
            else
            {
                _zombieBlips[i].style.display = DisplayStyle.None;
            }
        }

        // Render Special Infected (up to 8) - Reverse assignment
        int sTotal = Mathf.Min(_specialCandidates.Count, _specialBlips.Count);
        float specialHalfSize = 5.0f; // 10px
        for (int i = 0; i < _specialBlips.Count; i++)
        {
            if (i < sTotal)
            {
                int srcIdx = sTotal - 1 - i;
                var entry = _specialCandidates[srcIdx];
                Vector3 pos = entry.EnemyMB.transform.position;
                float dx = (pos.x - playerPos.x) * scale;
                float dz = -(pos.z - playerPos.z) * scale;

                var blip = _specialBlips[i];
                blip.style.left = halfWidth + dx - specialHalfSize;
                blip.style.top = halfWidth + dz - specialHalfSize;
                blip.style.display = DisplayStyle.Flex;
            }
            else
            {
                _specialBlips[i].style.display = DisplayStyle.None;
            }
        }

        // Render Bosses (up to 5) - Reverse assignment
        int bTotal = Mathf.Min(_bossCandidates.Count, _bossBlips.Count);
        float bossHalfSize = 9.0f; // 18px
        for (int i = 0; i < _bossBlips.Count; i++)
        {
            if (i < bTotal)
            {
                int srcIdx = bTotal - 1 - i;
                var entry = _bossCandidates[srcIdx];
                Vector3 pos = entry.EnemyMB.transform.position;
                float dx = (pos.x - playerPos.x) * scale;
                float dz = -(pos.z - playerPos.z) * scale;

                var blip = _bossBlips[i];
                blip.style.left = halfWidth + dx - bossHalfSize;
                blip.style.top = halfWidth + dz - bossHalfSize;
                blip.style.display = DisplayStyle.Flex;
            }
            else
            {
                _bossBlips[i].style.display = DisplayStyle.None;
            }
        }

        // 3. Journal Blips (up to 10)
        int jCount = 0;
        float journalHalfSize = 5.0f; // 10px
        float journalEdgeLimit = halfWidth - (journalHalfSize * 1.20f);

        for (int i = Collectible.ActiveCollectibles.Count - 1; i >= 0; i--)
        {
            if (jCount >= _journalBlips.Count) break;
            var c = Collectible.ActiveCollectibles[i];
            if (c == null || !c || c.gameObject == null || !c.gameObject.activeInHierarchy || c.IsPicked) continue;

            Vector3 pos = c.transform.position;
            if (Mathf.Abs(pos.y - playerPos.y) > 12f) continue;

            float dx = (pos.x - playerPos.x) * scale;
            float dz = -(pos.z - playerPos.z) * scale;

            if (Mathf.Abs(dx) <= journalEdgeLimit && Mathf.Abs(dz) <= journalEdgeLimit)
            {
                var blip = _journalBlips[jCount];
                blip.style.left = halfWidth + dx - journalHalfSize;
                blip.style.top = halfWidth + dz - journalHalfSize;
                blip.style.display = DisplayStyle.Flex;
                jCount++;
            }
        }
        for (int i = jCount; i < _journalBlips.Count; i++)
            _journalBlips[i].style.display = DisplayStyle.None;

        // 4. Side Quest Green Blips (up to 5)
        int sideCount = 0;
        float sideHalfSize = 6.0f; // 12px
        float sideEdgeLimit = halfWidth - (sideHalfSize * 1.20f);

        var sqm = SideQuestManager.Instance;
        if (sqm != null && sqm.ActiveQuests != null)
        {
            for (int i = QuestBeacon.ActiveBeacons.Count - 1; i >= 0; i--)
            {
                if (sideCount >= _sideQuestBlips.Count) break;
                var b = QuestBeacon.ActiveBeacons[i];
                if (b == null || !b || b.gameObject == null || !b.gameObject.activeInHierarchy || !b.IsActive || b.showOnSideQuest == null) continue;

                Vector3 pos = b.transform.position;
                float dx = (pos.x - playerPos.x) * scale;
                float dz = -(pos.z - playerPos.z) * scale;

                if (Mathf.Abs(dx) <= sideEdgeLimit && Mathf.Abs(dz) <= sideEdgeLimit)
                {
                    var blip = _sideQuestBlips[sideCount];
                    blip.style.left = halfWidth + dx - sideHalfSize;
                    blip.style.top = halfWidth + dz - sideHalfSize;
                    blip.style.display = DisplayStyle.Flex;
                    sideCount++;
                }
            }
        }
        for (int i = sideCount; i < _sideQuestBlips.Count; i++)
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
                else
                {
                    // A1: live progress counters — refresh text only when numbers change.
                    string curProgress = GetQuestProgressText();
                    if (curProgress != _lastQuestProgress)
                    {
                        _lastQuestProgress = curProgress;
                        RefreshObjectiveProgressText();
                    }
                }
            }
        }
    }

    private int _lastCollectibleCount = -1;
    private int _lastSideQuestCount = -1;
    private string _lastActiveQuestTitle = "__init__";
    private int _lastActiveChapter = -1;
    private string _lastQuestProgress = "__init__";

    private void SyncPollCache()
    {
        var sm = StoryManager.Instance;
        if (sm != null)
        {
            _lastActiveQuestTitle = sm.ActiveQuest?.title;
            _lastActiveChapter = sm.CurrentChapter;
        }
        _lastQuestProgress = "__init__";
        var sqm = SideQuestManager.Instance;
        if (sqm != null)
        {
            _lastSideQuestCount = sqm.ActiveQuests.Count;
        }
        var cm = CollectibleManager.Instance;
        if (cm != null)
        {
            _lastCollectibleCount = cm.Count;
        }
    }

    private void HandleQuestChanged(QuestData oldQuest, QuestData newQuest)
    {
        SyncPollCache();
        TriggerUpdateAnimation();
    }
    private void HandleChapterChanged(int oldCh, int newCh)
    {
        SyncPollCache();
        TriggerUpdateAnimation();
    }
    private void HandleSideQuestChanged(QuestData quest)
    {
        SyncPollCache();
        TriggerUpdateAnimation();
    }
    private void HandleCollectibleCollected(JournalData journal)
    {
        SyncPollCache();
        TriggerUpdateAnimation();
    }

    private void TriggerUpdateAnimation()
    {
        if (_isMinimapMode)
        {
            _hasPendingReentryGlow = true;
            UpdateDisplay();
            return;
        }

        if (_container == null || _mainPanel == null)
        {
            UpdateDisplay();
            return;
        }

        if (_isQuestUpdating)
        {
            _hasPendingQuestUpdate = true;
            return;
        }

        if (_mainQuestTransitionCoroutine != null)
        {
            StopCoroutine(_mainQuestTransitionCoroutine);
            _mainQuestTransitionCoroutine = null;
        }

        _mainQuestTransitionCoroutine = StartCoroutine(Run2PhaseQuestTransition());
    }

    private IEnumerator Run2PhaseQuestTransition()
    {
        _isQuestUpdating = true;

        // Phase 1 (120ms): Fade out _mainPanel
        if (_mainPanel != null)
        {
            _mainPanel.style.opacity = 0f;
        }
        yield return new WaitForSecondsRealtime(0.12f);

        // Midpoint: Swap text strings into labels while opacity = 0
        UpdateStoryContent();
        UpdateSideContent();

        // Phase 2 (200ms): Fade in _mainPanel + add gold title glow
        if (_mainPanel != null)
        {
            _mainPanel.style.opacity = 1f;
            _glowScheduledItem?.Pause();
            _mainPanel.RemoveFromClassList("quest-title-glow");
            _mainPanel.AddToClassList("quest-title-glow");
            _glowScheduledItem = _mainPanel.schedule.Execute(() =>
            {
                _mainPanel?.RemoveFromClassList("quest-title-glow");
            }).StartingIn(250);
        }
        yield return new WaitForSecondsRealtime(0.20f);

        _isQuestUpdating = false;
        _mainQuestTransitionCoroutine = null;

        if (_hasPendingQuestUpdate)
        {
            _hasPendingQuestUpdate = false;
            TriggerUpdateAnimation();
        }
    }

    private void UpdateCollectibleDisplay()
    {
        var cm = CollectibleManager.Instance;
        if (cm == null || _collectibles == null) return;
        _collectibles.text = $"Journals: {cm.Count}/{cm.Total}";
    }

    private void StartTransition(bool targetIsMinimap)
    {
        _isMinimapMode = targetIsMinimap;
        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }
        _transitionCoroutine = StartCoroutine(AnimateToggleMode(targetIsMinimap));
    }

    private IEnumerator AnimateToggleMode(bool targetIsMinimap)
    {
        _isTransitioning = true;
        if (targetIsMinimap)
        {
            MinimapController.Instance?.SetCameraActive(true);
        }

        if (_minimapContainer != null) _minimapContainer.style.display = DisplayStyle.Flex;
        if (_questGroup != null) _questGroup.style.display = DisplayStyle.Flex;
        if (_container != null) _container.style.overflow = Overflow.Hidden;

        if (_minimapContainer != null)
        {
            _minimapContainer.style.position = Position.Absolute;
            _minimapContainer.style.top = 0f;
            _minimapContainer.style.left = 0f;
            _minimapContainer.style.width = Length.Percent(100);
        }

        if (_questGroup != null)
        {
            _questGroup.style.position = Position.Absolute;
            _questGroup.style.top = 0f;
            _questGroup.style.left = 0f;
            _questGroup.style.width = Length.Percent(100);
        }

        if (_minimapImage != null)
        {
            var tex = MinimapController.Instance != null ? MinimapController.Instance.MinimapTexture : null;
            _minimapImage.SetBackgroundImageSafe(tex);
        }

        UpdateStoryContent();
        UpdateSideContent();

        float startMinimapOpacity = _currentMinimapOpacity;
        float startQuestOpacity = _currentQuestOpacity;

        float targetMinimapOpacity = targetIsMinimap ? 1f : 0f;
        float targetQuestOpacity = targetIsMinimap ? 0f : 1f;

        float H_quest_target = Mathf.Max(144f, _cachedQuestHeight > 0 ? _cachedQuestHeight : 180f);
        float startHeight = _container != null && _container.layout.height > 0 ? _container.layout.height : (targetIsMinimap ? H_quest_target : 316f);
        float targetHeight = targetIsMinimap ? 316f : H_quest_target;

        float elapsed = 0f;
        float duration = 0.20f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);

            _currentMinimapOpacity = Mathf.Lerp(startMinimapOpacity, targetMinimapOpacity, smoothT);
            _currentQuestOpacity = Mathf.Lerp(startQuestOpacity, targetQuestOpacity, smoothT);
            float curHeight = Mathf.Lerp(startHeight, targetHeight, smoothT);

            if (_minimapContainer != null)
            {
                _minimapContainer.style.opacity = _currentMinimapOpacity;
                _minimapContainer.style.scale = new StyleScale(new Scale(new Vector3(0.96f + 0.04f * _currentMinimapOpacity, 0.96f + 0.04f * _currentMinimapOpacity, 1f)));
            }

            if (_questGroup != null)
            {
                _questGroup.style.opacity = _currentQuestOpacity;
                _questGroup.style.scale = new StyleScale(new Scale(new Vector3(0.96f + 0.04f * _currentQuestOpacity, 0.96f + 0.04f * _currentQuestOpacity, 1f)));
            }

            if (_container != null)
            {
                _container.style.height = Length.Pixels(curHeight);
                _container.MarkDirtyRepaint();
            }

            yield return null;
        }

        _isTransitioning = false;
        _transitionCoroutine = null;
        ApplyFinalModeState(targetIsMinimap);
    }

    private void ApplyFinalModeState(bool isMinimap)
    {
        _currentMinimapOpacity = isMinimap ? 1f : 0f;
        _currentQuestOpacity = isMinimap ? 0f : 1f;

        if (isMinimap)
        {
            if (_questGroup != null) _questGroup.style.display = DisplayStyle.None;
            if (_minimapContainer != null) _minimapContainer.style.display = DisplayStyle.Flex;
        }
        else
        {
            if (_minimapContainer != null) _minimapContainer.style.display = DisplayStyle.None;
            if (_questGroup != null) _questGroup.style.display = DisplayStyle.Flex;
            HideAllBlipPools();
            MinimapController.Instance?.SetCameraActive(false);

            if (_hasPendingReentryGlow && _mainPanel != null)
            {
                _hasPendingReentryGlow = false;
                _glowScheduledItem?.Pause();
                _mainPanel.RemoveFromClassList("quest-title-glow");
                _mainPanel.AddToClassList("quest-title-glow");
                _glowScheduledItem = _mainPanel.schedule.Execute(() =>
                {
                    _mainPanel?.RemoveFromClassList("quest-title-glow");
                }).StartingIn(250);
            }
        }

        ResetTransitionInlineStyles();
        _container?.MarkDirtyRepaint();
    }

    private void ResetTransitionInlineStyles()
    {
        if (_minimapContainer != null)
        {
            _minimapContainer.style.position = StyleKeyword.Null;
            _minimapContainer.style.top = StyleKeyword.Null;
            _minimapContainer.style.left = StyleKeyword.Null;
            _minimapContainer.style.width = StyleKeyword.Null;
            _minimapContainer.style.scale = StyleKeyword.Null;
            _minimapContainer.style.opacity = StyleKeyword.Null;
        }

        if (_questGroup != null)
        {
            _questGroup.style.position = StyleKeyword.Null;
            _questGroup.style.top = StyleKeyword.Null;
            _questGroup.style.left = StyleKeyword.Null;
            _questGroup.style.width = StyleKeyword.Null;
            _questGroup.style.scale = StyleKeyword.Null;
            _questGroup.style.opacity = StyleKeyword.Null;
        }

        if (_mainPanel != null)
        {
            _mainPanel.style.opacity = StyleKeyword.Null;
        }

        if (_container != null)
        {
            _container.style.height = StyleKeyword.Null;
            _container.style.overflow = StyleKeyword.Null;
        }
    }

    private void UpdateDisplay()
    {
        if (_chapter == null || _title == null || _objective == null) return;

        bool isEndless = (StoryManager.Instance == null) && (GameModeManager.CurrentMode == GameMode.Endless);
        if (isEndless)
        {
            _isMinimapMode = true;
        }

        ApplyFinalModeState(_isMinimapMode);

        if (_isMinimapMode)
        {
            if (_minimapImage != null)
            {
                var tex = MinimapController.Instance != null ? MinimapController.Instance.MinimapTexture : null;
                _minimapImage.SetBackgroundImageSafe(tex);
            }
        }
        else
        {
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
            _baseObjectiveText = !string.IsNullOrEmpty(quest.objective) ? quest.objective : (quest.description ?? "");
            _objective.text = _baseObjectiveText + GetQuestProgressText();
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
            _baseObjectiveText = "";
            _objective.text = "";
            if (_instructions != null) _instructions.text = "";
        }

        UpdateCollectibleDisplay();
    }

    /// <summary>
    /// Returns " (cur/target)" for the active quest's kill/collectible objective
    /// (A1: live progress counters), or "" when none applies.
    /// </summary>
    private string GetQuestProgressText()
    {
        var sm = StoryManager.Instance;
        var quest = sm != null ? sm.ActiveQuest : null;
        if (quest == null) return "";

        for (int i = KillCountObjective.ActiveObjectives.Count - 1; i >= 0; i--)
        {
            var kco = KillCountObjective.ActiveObjectives[i];
            if (kco != null && kco.targetQuest == quest && kco.Target > 0)
                return $" ({Mathf.Min(kco.Progress, kco.Target)}/{kco.Target})";
        }

        for (int i = CollectibleQuestObjective.ActiveObjectives.Count - 1; i >= 0; i--)
        {
            var cqo = CollectibleQuestObjective.ActiveObjectives[i];
            if (cqo != null && cqo.targetQuest == quest && cqo.RequiredCount > 0)
                return $" ({Mathf.Min(cqo.PickedCount, cqo.RequiredCount)}/{cqo.RequiredCount})";
        }

        return "";
    }

    private void RefreshObjectiveProgressText()
    {
        if (_objective == null) return;
        _objective.text = _baseObjectiveText + GetQuestProgressText();
    }

    private string _baseObjectiveText = "";

    private void UpdateSideContent()
    {
        var sqm = SideQuestManager.Instance;
        var activeQuests = sqm != null ? sqm.ActiveQuests : null;
        int activeCount = activeQuests != null ? activeQuests.Count : 0;

        if ((activeCount == 0 && _exitingLabels.Count == 0) || _sideLinesContainer == null)
        {
            if (_sidePanel != null) _sidePanel.style.display = DisplayStyle.None;
            if (_divider != null) _divider.style.display = DisplayStyle.None;
            return;
        }

        if (_sidePanel != null) _sidePanel.style.display = DisplayStyle.Flex;
        if (_divider != null) _divider.style.display = DisplayStyle.Flex;

        var newTitles = new List<string>();
        if (activeQuests != null)
        {
            int c = Mathf.Min(activeQuests.Count, maxSideQuestLines);
            for (int i = 0; i < c; i++)
            {
                if (activeQuests[i] != null && !string.IsNullOrEmpty(activeQuests[i].title))
                    newTitles.Add($"• {activeQuests[i].title}");
            }
        }

        for (int i = _sideLines.Count - 1; i >= 0; i--)
        {
            var label = _sideLines[i];
            if (label != null && !newTitles.Contains(label.text))
            {
                AnimateSideQuestExit(label);
            }
        }

        var toRemove = new List<VisualElement>();
        for (int i = 0; i < _sideLinesContainer.childCount; i++)
        {
            var child = _sideLinesContainer[i];
            if (child is Label l && !_exitingLabels.Contains(l))
            {
                toRemove.Add(child);
            }
        }
        foreach (var el in toRemove)
        {
            el.RemoveFromHierarchy();
        }
        _sideLines.Clear();

        for (int i = 0; i < newTitles.Count; i++)
        {
            var label = new Label
            {
                text = newTitles[i]
            };
            label.AddToClassList("side-line");
            _sideLinesContainer.Add(label);
            _sideLines.Add(label);
        }

        _lastSideQuestTitles.Clear();
        _lastSideQuestTitles.AddRange(newTitles);
    }

    private void AnimateSideQuestExit(Label label)
    {
        if (label == null || _exitingLabels.Contains(label)) return;
        _exitingLabels.Add(label);

        float startH = label.resolvedStyle.height > 0 ? label.resolvedStyle.height : (label.layout.height > 0 ? label.layout.height : 24f);
        label.style.overflow = Overflow.Hidden;
        label.AddToClassList("side-line--exiting");

        StartCoroutine(RunSideQuestExitLerp(label, startH));
    }

    private IEnumerator RunSideQuestExitLerp(Label label, float startH)
    {
        float duration = 0.18f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curH = Mathf.Lerp(startH, 0f, t);
            float curOpacity = Mathf.Lerp(1f, 0f, t);

            if (label != null)
            {
                label.style.height = Length.Pixels(curH);
                label.style.opacity = curOpacity;
            }
            yield return null;
        }

        if (label != null)
        {
            label.RemoveFromHierarchy();
            _exitingLabels.Remove(label);
        }

        if ((SideQuestManager.Instance == null || SideQuestManager.Instance.ActiveQuests.Count == 0) && _exitingLabels.Count == 0)
        {
            if (_sidePanel != null) _sidePanel.style.display = DisplayStyle.None;
            if (_divider != null) _divider.style.display = DisplayStyle.None;
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

    private void DrawRivet(Painter2D painter, Vector2 center)
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
        float rOffset = 8f;
        DrawRivet(painter, new Vector2(rOffset, rOffset));
        DrawRivet(painter, new Vector2(rect.width - rOffset, rOffset));
        DrawRivet(painter, new Vector2(rect.width - rOffset, rect.height - rOffset));
        DrawRivet(painter, new Vector2(rOffset, rect.height - rOffset));
    }
}
