using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Idle hint (C8): in Story mode, while a quest is active, if the player goes
/// idle (no movement / mouse look / firing / kill / quest progress) for
/// `idleSeconds`, a SimpleNotification reminds them of the active quest's
/// objective/instructions. Repeats every `cooldownSeconds` while still idle.
/// </summary>
public class IdleHintWidget : MonoBehaviour
{
    private static IdleHintWidget _instance;

    [Header("Timing")]
    [Tooltip("Seconds of inactivity before the hint appears.")]
    public float idleSeconds = 45f;

    [Tooltip("Minimum seconds between repeated hints while staying idle.")]
    public float cooldownSeconds = 60f;

    [Tooltip("If true, also hint when no story quest is active.")]
    public bool hintWithoutQuest = false;

    private float _lastActivityTime = 0f;
    private float _lastHintTime = float.NegativeInfinity;
    private float _pollTimer = 0f;
    private int _lastKills = -1;
    private Vector3 _lastPlayerPos;
    private bool _posInit;

    public static IdleHintWidget Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("IdleHintWidget");
                _instance = go.AddComponent<IdleHintWidget>();
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
    }

    private void OnEnable()
    {
        _lastActivityTime = Time.time;
        _lastHintTime = float.NegativeInfinity;
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
            StoryManager.Instance.OnActiveQuestChanged += HandleQuestChanged;
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
            StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
        }
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        }
    }

    private void OnDestroy()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        }
        if (_instance == this) _instance = null;
    }

    private void HandleQuestChanged(QuestData oldQuest, QuestData newQuest)
    {
        MarkActivity();
    }

    private void HandleChapterChanged(int oldCh, int newCh)
    {
        MarkActivity();
    }

    private void MarkActivity()
    {
        _lastActivityTime = Time.time;
    }

    private void Update()
    {
        if (GameModeManager.CurrentMode != GameMode.Story) return;

        _pollTimer -= Time.unscaledDeltaTime;
        if (_pollTimer > 0f) return;
        _pollTimer = 0.5f;

        if (BlockedByUI()) return;

        if (HasInputActivity())
        {
            MarkActivity();
            return;
        }

        float idle = Time.time - _lastActivityTime;
        if (idle < idleSeconds) return;

        if (Time.time - _lastHintTime < cooldownSeconds) return;

        var sm = StoryManager.Instance;
        if (sm == null) return;
        if (sm.StoryComplete) return;

        var quest = sm.ActiveQuest;
        if (quest == null && !hintWithoutQuest) return;

        _lastHintTime = Time.time;
        MarkActivity();

        string msg;
        if (quest != null)
        {
            string obj = !string.IsNullOrEmpty(quest.objective)
                ? quest.objective
                : quest.description;
            msg = $"NHIỆM VỤ: {quest.title}\n{obj}";
            if (!string.IsNullOrEmpty(quest.instructions))
                msg += $"\n{quest.instructions}";
        }
        else
        {
            msg = "Bạn đang ở đâu đó trong thị trấn. Hãy tiếp tục tìm kiếm nhiệm vụ và nhật ký.";
        }

        SimpleNotification.Show(msg);
    }

    /// <summary>True when a panel/cutscene/pause blocks the hint.</summary>
    private bool BlockedByUI()
    {
        if (CutscenePlayer.IsAnyPlaying) return true;
        if (cowsins.PauseMenu.isPaused) return true;
        if (PanelManager.Instance != null && PanelManager.Instance.IsAnyPanelActive()) return true;
        return false;
    }

    private bool HasInputActivity()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed) return true;
            if (kb.spaceKey.isPressed || kb.shiftKey.isPressed) return true;
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed) return true;
            var delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude > 0.1f) return true;
        }

        // Kill progress counts as activity (keeps hint away during active fights).
        int kills = ScoreManager.Instance != null ? ScoreManager.Instance.kills : 0;
        if (_lastKills >= 0 && kills != _lastKills)
        {
            _lastKills = kills;
            return true;
        }
        _lastKills = kills;

        // Player physically moving counts as activity.
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 pos = player.transform.position;
            if (_posInit)
            {
                if (Vector3.SqrMagnitude(pos - _lastPlayerPos) > 0.04f)
                {
                    _lastPlayerPos = pos;
                    return true;
                }
            }
            else
            {
                _lastPlayerPos = pos;
                _posInit = true;
            }
        }

        return false;
    }
}
