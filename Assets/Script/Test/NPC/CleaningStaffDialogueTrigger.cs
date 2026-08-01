using UnityEngine;
using cowsins;

[RequireComponent(typeof(CompanionAI))]
[RequireComponent(typeof(DialogueBubble))]
public class CleaningStaffDialogueTrigger : Interactable
{
    [System.Serializable]
    public struct QuestionData
    {
        [TextArea(2, 4)]
        public string question;

        public bool correctAnswer;

        [Tooltip("Gợi ý hiển thị bên dưới câu hỏi để người chơi biết cách trả lời đúng. Để trống nếu không muốn gợi ý.")]
        [TextArea(1, 3)]
        public string hint;
    }

    [Header("3 Questions about the Hospital")]
    public QuestionData[] questions = new QuestionData[3]
    {
        new QuestionData { question = "Khu cách ly của bệnh viện nằm ở tầng 2, đúng không?", correctAnswer = true, hint = "Gợi ý: Tôi thấy ghi chú 'KHOA CÁCH LY - TẦNG 2' trên tường hành lang." },
        new QuestionData { question = "Bệnh nhân số 0 được đưa vào lúc nửa đêm?", correctAnswer = false, hint = "Gợi ý: Biên bản nhập viện ghi rõ thời điểm lúc trời vừa sáng." },
        new QuestionData { question = "Anh có thấy một bác sĩ mặc áo choàng trắng chạy về phía cầu thang thoát hiểm không?", correctAnswer = true, hint = "Gợi ý: Bạn có nhớ đã nhìn thấy ai đó chạy về phía cầu thang lúc mới vào không?" },
    };

    [Header("Interact Text")]
    public string defaultInteractText = "Hỏi chuyện";

    [Header("Small Talk (after joining)")]
    [Tooltip("Casual dialogue lines spoken when the quiz is done and the staff member is following. One is picked randomly each interaction.")]
    [TextArea(2, 4)]
    public string[] smallTalkLines = new string[]
    {
        "Anh có bị thương ở đâu không? Tôi có thể băng bó cho anh.",
        "Hôm nay còn sống là một ngày tốt rồi, đúng không?",
        "Tôi vẫn nhớ mùi cồn trong bệnh viện... Giờ chỉ còn mùi khói.",
        "Đừng lo, tôi sẽ chú ý phía sau lưng anh."
    };

    [Tooltip("Thank-you + check-up line spoken the first time the player talks to the staff member after rescuing them, then falls back to smallTalkLines.")]
    [TextArea(2, 4)]
    public string[] rescuedThankLines = new string[]
    {
        "Cảm ơn anh đã cứu tôi lúc nãy. Anh có sao không?",
        "Nhờ anh kéo tôi dậy kịp lúc. Cảm ơn anh nhiều lắm."
    };

    public string smallTalkInteractText = "Hỏi chuyện";

    [Header("Proximity Fallback")]
    public float proximityDistance = 2.5f;
    public KeyCode proximityKey = KeyCode.E;

    private DialogueBubble _bubble;
    private CompanionAI _ai;
    private bool _consumed;
    private bool _smallTalkEnabled;  // True once the quiz is done — casual chat mode.
    private bool _thanksPending;     // Set when the player rescues the staff; consumed by next small talk.
    private string _lastSmallTalkLine; // Avoid repeating the same line twice in a row.
    private Transform _player;
    private cowsins.InputManager _playerInput;
    private int _currentQuestionIndex;
    private bool _quizComplete;
    private bool _proximityHintShown;

    private void OnEnable()
    {
        if (_ai == null) _ai = GetComponent<CompanionAI>();
        if (_ai != null) _ai.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (_ai != null) _ai.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(CompanionAI.State newState)
    {
        if (newState == CompanionAI.State.Downed)
        {
            interactText = "Giải cứu đồng đội";
        }
        else
        {
            interactText = defaultInteractText;
        }
    }

    public override float GetHoldDuration(float defaultDuration)
    {
        if (_ai != null && _ai.CurrentState == CompanionAI.State.Downed) return _ai.rescueHoldDuration;
        return 0f;
    }

    public override bool InstantInteraction => (_ai != null && _ai.CurrentState == CompanionAI.State.Downed) ? false : instantInteraction;

    public override void OnHoldProgressUpdate(float progress)
    {
        if (_ai != null) _ai.NotifyRescueProgress(progress);
    }

    public override void OnHoldCancel()
    {
        if (_ai != null) _ai.CancelRescue();
    }

    private void Awake()
    {
        _bubble = GetComponent<DialogueBubble>();
        if (_bubble != null) _bubble.defaultSpeakerName = "Nhân Viên Vệ Sinh";
        _ai = GetComponent<CompanionAI>();
        interactable = true;
        var field = typeof(Interactable).GetField("instantInteraction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(this, true);
        if (_ai != null) _ai.OnRescuedByPlayer += HandleRescuedByPlayer;
    }

    private void OnDestroy()
    {
        if (_ai != null) _ai.OnRescuedByPlayer -= HandleRescuedByPlayer;
    }

    private void HandleRescuedByPlayer()
    {
        _thanksPending = true;
        Debug.Log("[CleaningStaff] Rescued by player — next small talk will thank the player.");
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (_ai != null && _ai.CurrentState == CompanionAI.State.Downed)
        {
            if (interactText != "Giải cứu đồng đội") interactText = "Giải cứu đồng đội";
            return;
        }
        string target = _smallTalkEnabled ? smallTalkInteractText : defaultInteractText;
        if (interactText != target) interactText = target;

        if (_consumed || _bubble == null || _bubble.IsChoiceActive) return;
        if (_ai.CurrentState == CompanionAI.State.Downed) return;
        if (_quizComplete && !_smallTalkEnabled) return;

        if (_player == null) FindPlayer();
        if (_player == null) return;

        ResolvePlayerInput();
        bool ePressed = _playerInput != null
            ? _playerInput.StartInteraction
            : Input.GetKeyDown(proximityKey);

        float dist = Vector3.Distance(transform.position, _player.position);
        if (!_proximityHintShown && dist <= proximityDistance * 1.4f)
        {
            _proximityHintShown = true;
            SimpleNotification.Show("Trò chuyện với nhân vật bằng phím [E] để nhận thông tin.");
        }

        if (dist <= proximityDistance && ePressed)
        {
            // Facing check: only trigger if the player is roughly looking
            // toward the NPC (prevents pressing E near one NPC from
            // accidentally triggering dialogue on another nearby NPC).
            var dirToNpc = (transform.position - _player.position).normalized;
            var cam = Camera.main != null ? Camera.main.transform : _player;
            float facingDot = Vector3.Dot(cam.forward, dirToNpc);
            if (facingDot >= 0.4f)
                TriggerQuiz();
        }
    }

    private void FindPlayer()
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _player = playerGO.transform;
        ResolvePlayerInput();
    }

    private void ResolvePlayerInput()
    {
        if (_playerInput != null) return;
        if (_player == null) return;
        var p = _player.gameObject;
        _playerInput = p.GetComponentInParent<cowsins.InputManager>();
        if (_playerInput == null && p.transform.parent != null)
            _playerInput = p.transform.parent.GetComponentInChildren<cowsins.InputManager>();
        if (_playerInput == null)
            _playerInput = p.GetComponentInChildren<cowsins.InputManager>();
    }

    public override void Interact(Transform player)
    {
        base.Interact(player);
        if (_consumed) return;
        if (_ai != null && _ai.CurrentState == CompanionAI.State.Downed)
        {
            _ai.Revive(1f, byPlayer: true);
            interactText = defaultInteractText;
            return;
        }
        if (_bubble == null || _bubble.IsChoiceActive) return;
        if (_quizComplete && !_smallTalkEnabled) return;
        TriggerQuiz();
    }

    private void TriggerQuiz()
    {
        if (_consumed || _bubble == null || _bubble.IsChoiceActive) return;
        if (_quizComplete)
        {
            if (_smallTalkEnabled) ShowSmallTalk();
            return;
        }
        _currentQuestionIndex = 0;
        _consumed = true;
        AskNextQuestion();
    }

    /// <summary>
    /// Shows a random casual dialogue line (small talk). If the player just
    /// rescued the staff member, a thank-you + check-up line is shown first.
    /// Small talk never consumes the interaction.
    /// </summary>
    private void ShowSmallTalk()
    {
        if (_bubble == null) return;

        if (_thanksPending)
        {
            _thanksPending = false;
            if (rescuedThankLines != null && rescuedThankLines.Length > 0)
            {
                string thanks = rescuedThankLines[Random.Range(0, rescuedThankLines.Length)];
                _lastSmallTalkLine = thanks;
                _bubble.ShowSpeech(thanks);
                return;
            }
        }

        if (smallTalkLines == null || smallTalkLines.Length == 0)
        {
            _bubble.ShowSpeech(defaultInteractText);
            return;
        }

        string line = smallTalkLines[Random.Range(0, smallTalkLines.Length)];
        if (smallTalkLines.Length > 1 && line == _lastSmallTalkLine)
        {
            line = smallTalkLines[Random.Range(0, smallTalkLines.Length)];
        }
        _lastSmallTalkLine = line;
        _bubble.ShowSpeech(line);
    }

    private void AskNextQuestion()
    {
        if (_ai == null) return;
        if (_currentQuestionIndex >= questions.Length)
        {
            _quizComplete = true;
            _ai.StartFollowing();
            SimpleNotification.Show("Nhân viên vệ sinh đã đồng hành cùng bạn!");
            // Switch to small-talk mode so the staff member stays talkable.
            _smallTalkEnabled = true;
            _consumed = false;
            interactable = true;
            Debug.Log("[CleaningStaff] All 3 questions answered correctly. Companion now follows (small talk enabled).");
            return;
        }

        var q = questions[_currentQuestionIndex];
        _bubble.ShowChoice($"({_currentQuestionIndex + 1}/{questions.Length}) {q.question}", q.hint, OnAnswer);
    }

    private void OnAnswer(bool playerAnsweredYes)
    {
        var q = questions[_currentQuestionIndex];
        bool correct = playerAnsweredYes == q.correctAnswer;

        if (correct)
        {
            _currentQuestionIndex++;
            SimpleNotification.Show($"Đúng! ({_currentQuestionIndex}/{questions.Length})");
            AskNextQuestion();
        }
        else
        {
            SimpleNotification.Show("Sai rồi! Hãy suy nghĩ lại.");
            _currentQuestionIndex = 0;
            _consumed = false;
        }
    }

    /// <summary>
    /// Resets the consumed flag so the player can interact again after revive.
    /// Quiz progress (_currentQuestionIndex, _quizComplete) is preserved.
    /// Called by CompanionAI.EnterDowned().
    /// </summary>
    public void ResetQuiz()
    {
        _consumed = false;
        if (_bubble != null) _bubble.ForceHide();
    }

    public override bool IsForbiddenInteraction(IWeaponReferenceProvider weaponController)
    {
        if (_ai != null && (_ai.CurrentState == CompanionAI.State.Dead || _ai.CurrentState == CompanionAI.State.WalkingAway)) return true;
        return base.IsForbiddenInteraction(weaponController);
    }
}
