using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;

/// <summary>
/// Help / tutorial overlay toggleable with [H]. Shows movement, combat and
/// NPC-communication instructions, the list of quests already completed
/// ("NHIỆM VỤ ĐÃ LÀM") and the instructions of the currently active quest.
/// Follows the PanelManager open/close pattern used by
/// SkillTreeWidget / StatsPanelUI so timeScale, mouse lock and HUD hiding
/// are handled automatically.
/// </summary>
public class TutorialOverlayWidget : MonoBehaviour
{
    public static TutorialOverlayWidget Instance;

    public KeyCode toggleKey = KeyCode.H;

    [Header("Content")]
    [Tooltip("Movement instructions (hướng dẫn di chuyển).")]
    [TextArea(5, 12)]
    public string movementText =
        "WASD — Di chuyển\n" +
        "Space — Nhảy (nhấn 2 lần để nhảy đúp)\n" +
        "Left Shift — Chạy nhanh\n" +
        "Left Ctrl — Ngồi / Cúi\n" +
        "Middle Mouse — Dash (lướt nhanh)\n" +
        "C — Grapple Hook (móc di chuyển)\n" +
        "M — Bật/tắt bản đồ nhỏ / danh sách nhiệm vụ\n" +
        "B — Di chuyển nhanh (khi đứng trong nhà an toàn)";

    [Tooltip("Combat instructions.")]
    [TextArea(5, 12)]
    public string combatText =
        "Chuột trái — Bắn\n" +
        "Chuột phải — Ngắm (ADS)\n" +
        "R — Nạp đạn\n" +
        "F — Đánh cận chiến\n" +
        "G — Vứt vũ khí hiện tại\n" +
        "T — Bật/tắt đèn pin\n" +
        "1-4 / Con lăn — Đổi vũ khí\n" +
        "Tab — Mở cây kỹ năng";

    [Tooltip("NPC interaction instructions.")]
    [TextArea(5, 12)]
    public string npcText =
        "E — Trò chuyện / Tương tác với NPC\n" +
        "Y — Đồng ý   |   N — Từ chối (trả lời câu hỏi)\n" +
        "Giữ E — Cứu đồng đội bị ngã\n" +
        "E — Lấy nhu yếu phẩm (nhiệm vụ companion)";

    [Header("Placeholder text when no quest is active.")]
    [TextArea(2, 4)]
    public string noQuestText = "Chưa có nhiệm vụ. Hoàn thành chương để mở khóa nhiệm vụ phụ.";

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _card;
    private Label _questBody;
    private Label _historyBody;
    private bool _open;
    private bool _initialized;
    private float _transitionEndTime = 0f;

    public bool IsOpen => _open;
    public bool IsTransitioning => Time.realtimeSinceStartup < _transitionEndTime;
    public bool IsOpenOrTransitioning => _open || IsTransitioning;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }
    }

    private void OnEnable()
    {
        if (!_initialized) Initialize();
        SubscribeToManagers();
    }

    private void OnDisable()
    {
        if (_open) Close();
        _initialized = false;
        UnsubscribeFromManagers();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Initialize()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null) return;
        _root = _doc.rootVisualElement.Q("TutorialPanel");
        if (_root == null) return;
        _root.style.display = DisplayStyle.None;

        _card = _root.Q("TutorialCard");
        _questBody = _root.Q<Label>("TutorialQuestBody");
        _historyBody = _root.Q<Label>("TutorialHistoryBody");

        var movementBody = _root.Q<Label>("TutorialMovementBody");
        if (movementBody != null) movementBody.text = movementText;
        var combatBody = _root.Q<Label>("TutorialCombatBody");
        if (combatBody != null) combatBody.text = combatText;
        var npcBody = _root.Q<Label>("TutorialNpcBody");
        if (npcBody != null) npcBody.text = npcText;

        _initialized = true;
    }

    private void Start()
    {
        if (!_initialized) Initialize();
        SubscribeToManagers();
        RefreshQuestSection();
    }

    private void SubscribeToManagers()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
            StoryManager.Instance.OnActiveQuestChanged += HandleQuestChanged;
            StoryManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
            StoryManager.Instance.OnQuestCompleted += HandleQuestCompleted;
        }
        if (SideQuestManager.Instance != null)
        {
            SideQuestManager.Instance.OnSideQuestActivated -= HandleSideQuestChanged;
            SideQuestManager.Instance.OnSideQuestActivated += HandleSideQuestChanged;
            SideQuestManager.Instance.OnSideQuestCompleted -= HandleSideQuestChanged;
            SideQuestManager.Instance.OnSideQuestCompleted += HandleSideQuestChanged;
        }
    }

    private void UnsubscribeFromManagers()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
            StoryManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
        }
        if (SideQuestManager.Instance != null)
        {
            SideQuestManager.Instance.OnSideQuestActivated -= HandleSideQuestChanged;
            SideQuestManager.Instance.OnSideQuestCompleted -= HandleSideQuestChanged;
        }
    }

    private void HandleQuestChanged(QuestData oldQuest, QuestData newQuest) => RefreshQuestSection();
    private void HandleQuestCompleted(QuestData quest) => RefreshQuestSection();
    private void HandleSideQuestChanged(QuestData quest) => RefreshQuestSection();

    private void Update()
    {
        if (IsTransitioning) return;

        bool hPressed = false;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.hKey.wasPressedThisFrame) hPressed = true;
        if (!hPressed)
        {
            try { if (Input.GetKeyDown(toggleKey)) hPressed = true; } catch { }
        }

        if (!hPressed) return;

        if (_open)
        {
            Close();
        }
        else
        {
            if (PanelManager.Instance != null)
            {
                if (PanelManager.Instance.CanOpenPanel("Tutorial"))
                {
                    Open();
                }
            }
            else
            {
                bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
                bool pauseActive = PauseManager.Instance != null && PauseManager.Instance.IsOpenOrTransitioning;
                bool journalActive = JournalUI.Instance != null && JournalUI.Instance.IsOpenOrTransitioning;
                bool dialogueActive = false;
                var bubbles = FindObjectsByType<DialogueBubble>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var d in bubbles)
                    if (d != null && d.IsVisible) { dialogueActive = true; break; }
                if (!gameOver && !pauseActive && !journalActive && !dialogueActive && !CutscenePlayer.IsAnyPlaying)
                    Open();
            }
        }
    }

    private void Open()
    {
        if (!_initialized || IsTransitioning) return;
        RefreshQuestSection();
        _open = true;
        _transitionEndTime = Time.realtimeSinceStartup + PanelManager.PanelTransitionDuration;

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.OpenPanel("Tutorial", _root, _card, Close);
        }
    }

    public void Close()
    {
        if (!_open || IsTransitioning) return;
        _open = false;
        _transitionEndTime = Time.realtimeSinceStartup + PanelManager.PanelTransitionDuration;

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ClosePanel("Tutorial", _root, _card, ResumeGameplay);
        }
        else
        {
            ResumeGameplay();
        }
    }

    private void ResumeGameplay()
    {
        if (_root != null) _root.Blur();
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>
    /// Refreshes the "NHIỆM VỤ HIỆN TẠI" section with the currently active
    /// main quest (or first active side quest) including its instructions,
    /// and the "NHIỆM VỤ ĐÃ LÀM" section with every quest completed so far.
    /// </summary>
    private void RefreshQuestSection()
    {
        RefreshHistorySection();
        if (_questBody == null) return;

        string text = "";
        var sm = StoryManager.Instance;
        if (sm != null && sm.ActiveQuest != null)
        {
            var q = sm.ActiveQuest;
            text = q.title + "\n" +
                   (string.IsNullOrEmpty(q.objective) ? "" : q.objective + "\n") +
                   (string.IsNullOrEmpty(q.instructions) ? "" : "\n" + q.instructions);
        }
        else
        {
            var sqm = SideQuestManager.Instance;
            if (sqm != null && sqm.ActiveQuests.Count > 0)
            {
                var q = sqm.ActiveQuests[0];
                text = "Nhiệm vụ phụ: " + q.title + "\n" +
                       (string.IsNullOrEmpty(q.objective) ? "" : q.objective + "\n") +
                       (string.IsNullOrEmpty(q.instructions) ? "" : "\n" + q.instructions);
            }
            else
            {
                text = noQuestText;
            }
        }

        _questBody.text = text;
    }

    /// <summary>
    /// Refreshes the "NHIỆM VỤ ĐÃ LÀM" section with the list of completed
    /// main story quests (in order) followed by completed side quests.
    /// </summary>
    private void RefreshHistorySection()
    {
        if (_historyBody == null) return;

        var lines = new List<string>();
        var sm = StoryManager.Instance;
        if (sm != null)
        {
            foreach (var q in sm.CompletedQuests)
                lines.Add("• Chương " + q.chapter + ": " + q.title);
        }
        var sqm = SideQuestManager.Instance;
        if (sqm != null)
        {
            foreach (var q in sqm.CompletedQuests)
                lines.Add("• Nhiệm vụ phụ (Chương " + q.chapter + "): " + q.title);
        }

        _historyBody.text = lines.Count > 0 ? string.Join("\n", lines) : "Chưa hoàn thành nhiệm vụ nào.";
    }
}
