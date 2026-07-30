using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;

public class SkillTreeWidget : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Audio SFX")]
    public AudioClip hoverSFX;
    public AudioClip purchaseSFX;
    public AudioClip lockedSFX;

#if UNITY_EDITOR
    private void Reset()
    {
        if (hoverSFX == null) hoverSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Engine/Cowsins/SFX/UI/UIHover_SFX.wav");
        if (purchaseSFX == null) purchaseSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Engine/Cowsins/SFX/Others/Loot_Success_SFX.wav");
        if (lockedSFX == null) lockedSFX = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Engine/Cowsins/SFX/Others/emptyMag_SFX.wav");
    }
#endif

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _card;
    private VisualElement _tooltip;
    private Label _tooltipName;
    private Label _tooltipDesc;
    private Label _tooltipStatus;
    private Label _sp;
    private readonly VisualElement[] _nodeContainer = new VisualElement[Trees];
    private readonly VisualElement[,] _nodes = new VisualElement[Trees, NodesPerTree];
    private readonly VisualElement[,] _lines = new VisualElement[Trees, NodesPerTree - 1];
    private readonly Label[] _cost = new Label[Trees];
    private readonly Label[] _next = new Label[Trees];
    private SkillTreeManager _mgr;
    private PlayerControl _playerControl;
    private Transform _canvasRoot;
    private bool _open;
    private bool _initialized;
    private int _lastSp = -1;
    private Coroutine _closeCoroutine;

    private static readonly string[] TreeNames = { "MOVEMENT", "AIM", "INTELLIGENCE" };
    private const int NodesPerTree = 5;
    private const int Trees = 3;

    public bool IsOpen => _open;
    private float _transitionEndTime = 0f;
    public bool IsTransitioning => Time.realtimeSinceStartup < _transitionEndTime;
    public bool IsOpenOrTransitioning => _open || IsTransitioning;

    private void OnEnable()
    {
        if (!_initialized) Initialize();
    }

    private void OnDisable()
    {
        if (_open) Close();
        _initialized = false;
    }

    private void OnDestroy()
    {
        if (JournalUI.Instance != null && JournalUI.Instance.IsOpen) return;
        if (_open)
        {
            // Don't restore timeScale if a cutscene or dialogue is active.
            bool cutsceneActive = CutscenePlayer.IsAnyPlaying;
            bool dialogueActive = false;
            var companions = FindObjectsByType<DialogueBubble>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var d in companions)
                if (d != null && d.IsVisible) { dialogueActive = true; break; }
            if (!cutsceneActive && !dialogueActive)
                Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }
    }

    private void Initialize()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null) return;
        _root = _doc.rootVisualElement.Q("SkillTreeWidget");
        if (_root == null) return;
        _root.style.display = DisplayStyle.None;

        _card = _root.Q("card");
        _sp = _root.Q<Label>("sp");

        if (_card != null)
        {
            _card.generateVisualContent += OnGenerateCardBackground;
        }

        // Dynamically instantiate the floating tooltip to avoid UXML desyncs
        _tooltip = new VisualElement();
        _tooltip.name = "tooltip";
        _tooltip.pickingMode = PickingMode.Ignore; // Mouse clicks pass straight through to node beneath
        _tooltip.style.display = DisplayStyle.None;
        _tooltip.style.opacity = 0f;

        _tooltipName = new Label();
        _tooltipName.name = "tooltip-name";
        _tooltipName.AddToClassList("tooltip-header");
        _tooltipName.pickingMode = PickingMode.Ignore;
        _tooltip.Add(_tooltipName);

        _tooltipDesc = new Label();
        _tooltipDesc.name = "tooltip-desc";
        _tooltipDesc.AddToClassList("tooltip-body");
        _tooltipDesc.pickingMode = PickingMode.Ignore;
        _tooltip.Add(_tooltipDesc);

        var divider = new VisualElement();
        divider.name = "tooltip-divider";
        divider.AddToClassList("tooltip-divider");
        divider.pickingMode = PickingMode.Ignore;
        _tooltip.Add(divider);

        _tooltipStatus = new Label();
        _tooltipStatus.name = "tooltip-status";
        _tooltipStatus.AddToClassList("tooltip-status");
        _tooltipStatus.pickingMode = PickingMode.Ignore;
        _tooltip.Add(_tooltipStatus);

        if (_card != null)
        {
            _card.Add(_tooltip);
        }

        for (int t = 0; t < Trees; t++)
        {
            _nodeContainer[t] = _root.Q("nodes" + t);
            _cost[t] = _root.Q<Label>("cost" + t);
            _next[t] = _root.Q<Label>("next" + t);
        }

        BuildNodes();
        _initialized = true;
    }

    private void BuildNodes()
    {
        for (int t = 0; t < Trees; t++)
        {
            for (int n = 0; n < NodesPerTree; n++)
            {
                // Calculate position using simple linear math (10px start, 90px center-to-center offset)
                float top = 10f + n * 90f;

                var node = new VisualElement();
                node.AddToClassList("skill-node");
                node.AddToClassList("locked");
                node.style.position = Position.Absolute;
                node.style.left = 0;
                node.style.top = top;
                node.style.width = 64;
                node.style.height = 64;
                _nodeContainer[t].Add(node);

                var icon = new VisualElement();
                icon.AddToClassList("node-icon");
                icon.AddToClassList($"node-icon-{t}-{n}");
                node.Add(icon);

                int ti = t;
                int ni = n;
                node.RegisterCallback<MouseEnterEvent>(_ => OnNodeHover(ti, ni));
                node.RegisterCallback<MouseLeaveEvent>(_ => OnNodeLeave(ti, ni));
                node.RegisterCallback<ClickEvent>(_ => TryUpgrade(ti, ni));

                if (n < NodesPerTree - 1)
                {
                    var line = new VisualElement();
                    line.AddToClassList("skill-line");
                    line.style.position = Position.Absolute;
                    line.style.left = 29; // Centered relative to 64px node width: (64 / 2) - (6 / 2) = 29
                    line.style.top = top + 64f;
                    line.style.width = 6;
                    line.style.height = 26;
                    _nodeContainer[t].Add(line);
                    _lines[t, n] = line;
                }

                _nodes[t, n] = node;
            }
        }
    }

    private void Start()
    {
        _canvasRoot = GameObject.Find("GameUICanvas")?.transform;
        if (_mgr == null) _mgr = FindAnyObjectByType<SkillTreeManager>();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _playerControl = player.GetComponentInChildren<PlayerControl>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (IsTransitioning) return;

            if (_open)
            {
                Close();
            }
            else
            {
                if (PanelManager.Instance != null)
                {
                    if (PanelManager.Instance.CanOpenPanel("SkillTree"))
                    {
                        Open();
                    }
                }
                else
                {
                    bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
                    bool pauseActive = PauseManager.Instance != null && PauseManager.Instance.IsOpenOrTransitioning;
                    bool journalActive = JournalUI.Instance != null && JournalUI.Instance.IsOpenOrTransitioning;
                    if (!gameOver && !pauseActive && !journalActive) Open();
                }
            }
        }
        if (_open)
        {
            RefreshIfDirty();
            if (_card != null) _card.MarkDirtyRepaint();

            // Dynamic border and background pulse for available nodes
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            Color borderColor = new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.4f + pulse * 0.6f);
            Color bgColor = new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.06f + pulse * 0.19f);
            for (int t = 0; t < Trees; t++)
            {
                for (int n = 0; n < NodesPerTree; n++)
                {
                    var node = _nodes[t, n];
                    if (node != null && node.ClassListContains("available"))
                    {
                        node.style.borderTopColor = borderColor;
                        node.style.borderRightColor = borderColor;
                        node.style.borderBottomColor = borderColor;
                        node.style.borderLeftColor = borderColor;
                        node.style.backgroundColor = bgColor;
                    }
                }
            }
        }
    }

    private void Open()
    {
        if (!_initialized || IsTransitioning) return;
        _open = true;
        _transitionEndTime = Time.realtimeSinceStartup + PanelManager.PanelTransitionDuration;

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.OpenPanel("SkillTree", _root, _card, Close);
        }
        Refresh();
    }

    public void Close()
    {
        if (!_open || IsTransitioning) return;
        _open = false;
        _transitionEndTime = Time.realtimeSinceStartup + PanelManager.PanelTransitionDuration;

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ClosePanel("SkillTree", _root, _card, ResumeGameplay);
        }
        else
        {
            ResumeGameplay();
        }
    }

    private void ResumeGameplay()
    {
        // Clear UI Toolkit focus
        if (_root != null) _root.Blur();

        // Clear EventSystem selection
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void OnNodeHover(int tree, int nodeIndex)
    {
        if (_mgr == null) return;
        int lvl = tree == 0 ? _mgr.MovementLevel : tree == 1 ? _mgr.AimLevel : _mgr.IntelligenceLevel;
        int cost = tree == 0 ? _mgr.NextMovementCost : tree == 1 ? _mgr.NextAimCost : _mgr.NextIntelligenceCost;
        bool maxed = lvl >= SkillTreeManager.MaxLevel;
        bool canAfford = !maxed && _mgr.CurrentSkillPoints >= cost;
        bool isNext = nodeIndex == lvl && !maxed;

        var node = _nodes[tree, nodeIndex];

        // Play hover sound for available nodes
        if (isNext && canAfford && hoverSFX != null && cowsins.SoundManager.Instance != null)
        {
            cowsins.SoundManager.Instance.PlaySound(hoverSFX, 0f, 0f, false);
        }

        // Show and configure the floating tooltip
        if (_tooltip != null && node != null && _card != null)
        {
            if (_tooltipName != null) _tooltipName.text = GetNodeName(tree, nodeIndex);
            if (_tooltipDesc != null) _tooltipDesc.text = SkillTreeManager.GetNodeDescription(tree, nodeIndex + 1);

            if (_tooltipStatus != null)
            {
                _tooltipStatus.ClearClassList();
                _tooltipStatus.AddToClassList("tooltip-status");
                bool unlocked = nodeIndex < lvl;
                if (unlocked)
                {
                    _tooltipStatus.text = "STATUS: ACQUIRED";
                    _tooltipStatus.AddToClassList("acquired");
                }
                else if (isNext && canAfford)
                {
                    _tooltipStatus.text = $"STATUS: AVAILABLE (Cost: {cost} SP)";
                    _tooltipStatus.AddToClassList("available");
                }
                else
                {
                    _tooltipStatus.text = isNext ? $"LOCKED: Need {cost} SP" : "LOCKED: Unlock Previous First";
                    _tooltipStatus.AddToClassList("locked");
                }
            }

            // Convert local node boundaries to card space for positioning
            Vector2 nodeLocalPos = node.parent.ChangeCoordinatesTo(_card, node.layout.position);
            _tooltip.style.left = nodeLocalPos.x + (node.layout.width / 2f);
            _tooltip.style.top = nodeLocalPos.y - 12f;

            _tooltip.style.display = DisplayStyle.Flex;
            _tooltip.style.opacity = 1f;
        }
    }

    private void OnNodeLeave(int tree, int nodeIndex)
    {
        if (_tooltip != null)
        {
            _tooltip.style.opacity = 0f;
            _tooltip.style.display = DisplayStyle.None;
        }
    }

    private string GetNodeName(int tree, int nodeIndex)
    {
        switch (tree)
        {
            case 0:
                switch (nodeIndex)
                {
                    case 0: return "Walk Speed Boost";
                    case 1: return "Sprint Thrusters";
                    case 2: return "Jet Dash Engine";
                    case 3: return "Wall Run System";
                    case 4: return "Cybernetic Grapple Hook";
                }
                break;
            case 1:
                switch (nodeIndex)
                {
                    case 0: return "Recoil Dampeners";
                    case 1: return "Targeting Array I";
                    case 2: return "Targeting Array II";
                    case 3: return "Hyper-Critical Lens";
                    case 4: return "One-Shot Decimator";
                }
                break;
            case 2:
                switch (nodeIndex)
                {
                    case 0: return "Bio-Magnetic XP Ring";
                    case 1: return "XP Multiplier Matrix I";
                    case 2: return "Bio-Magnetic XP Pulse";
                    case 3: return "XP Multiplier Matrix II";
                    case 4: return "Bio-Visor Highlight";
                }
                break;
        }
        return "Upgrade Node";
    }

    private void TryUpgrade(int tree, int nodeIndex)
    {
        if (_mgr == null) return;
        int lvl = tree == 0 ? _mgr.MovementLevel : tree == 1 ? _mgr.AimLevel : _mgr.IntelligenceLevel;
        int cost = tree == 0 ? _mgr.NextMovementCost : tree == 1 ? _mgr.NextAimCost : _mgr.NextIntelligenceCost;
        bool maxed = lvl >= SkillTreeManager.MaxLevel;
        bool canAfford = !maxed && _mgr.CurrentSkillPoints >= cost;
        bool isNext = nodeIndex == lvl && !maxed;

        var node = _nodes[tree, nodeIndex];

        if (isNext && canAfford)
        {
            bool ok = tree == 0 ? _mgr.UpgradeMovement() : tree == 1 ? _mgr.UpgradeAim() : _mgr.UpgradeIntelligence();
            if (ok)
            {
                if (purchaseSFX != null && cowsins.SoundManager.Instance != null)
                {
                    cowsins.SoundManager.Instance.PlaySound(purchaseSFX, 0f, 0f, false);
                }
                if (node != null)
                {
                    node.AddToClassList("pop");
                    StartCoroutine(RemoveAnimationClassAfterDelay(node, "pop", 0.15f));
                }
                Refresh();
                _lastSp = _mgr.CurrentSkillPoints;
            }
        }
        else
        {
            if (lockedSFX != null && cowsins.SoundManager.Instance != null)
            {
                cowsins.SoundManager.Instance.PlaySound(lockedSFX, 0f, 0f, false);
            }
            if (node != null)
            {
                StartCoroutine(ShakeNode(node));
            }
        }
    }

    private IEnumerator ShakeNode(VisualElement node)
    {
        if (node == null) yield break;
        node.AddToClassList("shake-left");
        yield return new WaitForSecondsRealtime(0.06f);
        node.RemoveFromClassList("shake-left");
        node.AddToClassList("shake-right");
        yield return new WaitForSecondsRealtime(0.06f);
        node.RemoveFromClassList("shake-right");
        node.AddToClassList("shake-left");
        yield return new WaitForSecondsRealtime(0.06f);
        node.RemoveFromClassList("shake-left");
    }

    private IEnumerator RemoveAnimationClassAfterDelay(VisualElement element, string className, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        element?.RemoveFromClassList(className);
    }

    private void RefreshIfDirty()
    {
        int sp = _mgr != null ? _mgr.CurrentSkillPoints : 0;
        if (sp == _lastSp) return;
        _lastSp = sp;
        Refresh();
    }

    private void Refresh()
    {
        int sp = _mgr != null ? _mgr.CurrentSkillPoints : 0;
        _sp.text = "SKILL POINTS : " + sp;

        for (int t = 0; t < Trees; t++)
        {
            int lvl = _mgr == null ? 0 : (t == 0 ? _mgr.MovementLevel : t == 1 ? _mgr.AimLevel : _mgr.IntelligenceLevel);
            int cost = _mgr == null ? 0 : (t == 0 ? _mgr.NextMovementCost : t == 1 ? _mgr.NextAimCost : _mgr.NextIntelligenceCost);
            bool maxed = lvl >= SkillTreeManager.MaxLevel;
            bool canAfford = !maxed && _mgr != null && sp >= cost;

            for (int n = 0; n < NodesPerTree; n++)
            {
                var node = _nodes[t, n];
                bool unlocked = n < lvl;
                bool isNext = n == lvl && !maxed;

                node.ClearClassList();
                node.AddToClassList("skill-node");
                if (unlocked)
                    node.AddToClassList("unlocked");
                else if (isNext && canAfford)
                    node.AddToClassList("available");
                else
                    node.AddToClassList("locked");

                node.SetEnabled(isNext && canAfford);
            }

            for (int n = 0; n < NodesPerTree - 1; n++)
            {
                var line = _lines[t, n];
                bool upperUnlocked = (n + 1) < lvl;
                bool upperIsNext = (n + 1) == lvl && !maxed;

                if (upperUnlocked)
                    line.style.backgroundColor = new StyleColor(new Color(230f / 255f, 80f / 255f, 40f / 255f, 1f));
                else if (upperIsNext && canAfford)
                    line.style.backgroundColor = new StyleColor(new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.6f));
                else if (upperIsNext)
                    line.style.backgroundColor = new StyleColor(new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.2f));
                else
                    line.style.backgroundColor = new StyleColor(new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.08f));
            }

            var costLabel = _cost[t];
            costLabel.text = maxed ? "MAXED" : ("NEXT  " + cost + " SP");
            costLabel.ClearClassList();
            costLabel.AddToClassList("cost-label");
            costLabel.AddToClassList(maxed ? "accent" : (canAfford ? "ready" : "muted"));

            _next[t].text = maxed ? "\u2014" : SkillTreeManager.GetNodeDescription(t, lvl + 1);
            _next[t].style.color = maxed
                ? new StyleColor(new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.25f))
                : (canAfford ? new StyleColor(new Color(230f / 255f, 80f / 255f, 40f / 255f, 1f)) : new StyleColor(new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.5f)));
        }
    }

    private void OnGenerateCardBackground(MeshGenerationContext mgc)
    {
        if (_card == null) return;
        var rect = _card.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;
        float chamferSize = 20f;

        // 1. Draw solid dark background shape
        Color fillCol = new Color(9f / 255f, 13f / 255f, 19f / 255f, 0.85f);
        painter.fillColor = fillCol;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width - chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, chamferSize));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height - chamferSize));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Fill();

        // 1.1 Draw horizontal sci-fi scanlines as filled rectangles using paths for maximum compatibility
        painter.fillColor = new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.05f);
        for (float y = 30f; y < rect.height - 30f; y += 14f)
        {
            painter.BeginPath();
            painter.MoveTo(new Vector2(15f, y));
            painter.LineTo(new Vector2(rect.width - 15f, y));
            painter.LineTo(new Vector2(rect.width - 15f, y + 1.5f));
            painter.LineTo(new Vector2(15f, y + 1.5f));
            painter.ClosePath();
            painter.Fill();
        }

        // 1.2 Draw vertical column dividers as filled rectangles
        painter.fillColor = new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.18f);
        
        // Left divider (between column 0 and 1)
        painter.BeginPath();
        painter.MoveTo(new Vector2(380f, 140f));
        painter.LineTo(new Vector2(381.5f, 140f));
        painter.LineTo(new Vector2(381.5f, rect.height - 120f));
        painter.LineTo(new Vector2(380f, rect.height - 120f));
        painter.ClosePath();
        painter.Fill();

        // Right divider (between column 1 and 2)
        painter.BeginPath();
        painter.MoveTo(new Vector2(700f, 140f));
        painter.LineTo(new Vector2(701.5f, 140f));
        painter.LineTo(new Vector2(701.5f, rect.height - 120f));
        painter.LineTo(new Vector2(700f, rect.height - 120f));
        painter.ClosePath();
        painter.Fill();

        // 2. Draw warning stripes at the top edge
        float badgeW = 100f;
        float badgeH = 8f;
        float startX = rect.width / 2f - badgeW / 2f;
        float startY = 4f;
        
        painter.lineWidth = 1.0f;
        for (float offset = 0; offset < badgeW; offset += 6f)
        {
            painter.strokeColor = new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.6f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset, startY));
            painter.LineTo(new Vector2(startX + offset - 4f, startY + badgeH));
            painter.Stroke();

            painter.strokeColor = new Color(9f / 255f, 13f / 255f, 19f / 255f, 0.9f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset + 3f, startY));
            painter.LineTo(new Vector2(startX + offset - 1f, startY + badgeH));
            painter.Stroke();
        }

        // 3. Draw pulsing outer border
        float pulse = 0.25f + Mathf.PingPong(Time.unscaledTime * 1.5f, 0.45f);
        painter.strokeColor = new Color(230f / 255f, 80f / 255f, 40f / 255f, pulse);
        painter.lineWidth = 2.0f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width - chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, chamferSize));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height - chamferSize));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Stroke();

        // 4. Draw crosshair tick marks at 4 corners
        float tickLen = 12f;
        painter.strokeColor = new Color(230f / 255f, 80f / 255f, 40f / 255f, 0.8f);
        painter.lineWidth = 1.5f;

        // Top-Left
        painter.BeginPath();
        painter.MoveTo(new Vector2(0, chamferSize + tickLen));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.LineTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(chamferSize + tickLen, 0));
        painter.Stroke();

        // Top-Right
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width - chamferSize - tickLen, 0));
        painter.LineTo(new Vector2(rect.width - chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, chamferSize));
        painter.LineTo(new Vector2(rect.width, chamferSize + tickLen));
        painter.Stroke();

        // Bottom-Left
        painter.BeginPath();
        painter.MoveTo(new Vector2(0, rect.height - chamferSize - tickLen));
        painter.LineTo(new Vector2(0, rect.height - chamferSize));
        painter.LineTo(new Vector2(chamferSize, rect.height));
        painter.LineTo(new Vector2(chamferSize + tickLen, rect.height));
        painter.Stroke();

        // Bottom-Right
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width - chamferSize - tickLen, rect.height));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize - tickLen));
        painter.Stroke();
    }
}
