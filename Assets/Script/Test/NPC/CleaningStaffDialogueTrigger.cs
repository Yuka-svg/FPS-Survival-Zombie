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
    }

    [Header("3 Questions about the Hospital")]
    public QuestionData[] questions = new QuestionData[3]
    {
        new QuestionData { question = "Khu cách ly của bệnh viện nằm ở tầng 2, đúng không?", correctAnswer = true },
        new QuestionData { question = "Bệnh nhân số 0 được đưa vào lúc nửa đêm?", correctAnswer = false },
        new QuestionData { question = "Anh có thấy một bác sĩ mặc áo choàng trắng chạy về phía cầu thang thoát hiểm không?", correctAnswer = true },
    };

    [Header("Interact Text")]
    public string defaultInteractText = "Hỏi chuyện";

    [Header("Proximity Fallback")]
    public float proximityDistance = 2.5f;
    public KeyCode proximityKey = KeyCode.E;

    private DialogueBubble _bubble;
    private CompanionAI _ai;
    private bool _consumed;
    private Transform _player;
    private cowsins.InputManager _playerInput;
    private int _currentQuestionIndex;
    private bool _quizComplete;

    private void Awake()
    {
        _bubble = GetComponent<DialogueBubble>();
        _ai = GetComponent<CompanionAI>();
        interactable = true;
        var field = typeof(Interactable).GetField("instantInteraction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(this, true);
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (interactText != defaultInteractText) interactText = defaultInteractText;

        if (_consumed || _bubble == null || _bubble.IsChoiceActive) return;
        if (_quizComplete || _ai.CurrentState == CompanionAI.State.Downed) return;

        if (_player == null) FindPlayer();
        if (_player == null) return;

        ResolvePlayerInput();
        bool ePressed = _playerInput != null
            ? _playerInput.StartInteraction
            : Input.GetKeyDown(proximityKey);

        float dist = Vector3.Distance(transform.position, _player.position);
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
        if (_bubble == null || _bubble.IsChoiceActive) return;
        if (_quizComplete || _ai.CurrentState == CompanionAI.State.Downed) return;
        TriggerQuiz();
    }

    private void TriggerQuiz()
    {
        if (_consumed || _bubble == null || _bubble.IsChoiceActive) return;
        if (_quizComplete) return;
        _currentQuestionIndex = 0;
        _consumed = true;
        AskNextQuestion();
    }

    private void AskNextQuestion()
    {
        if (_ai == null) return;
        if (_currentQuestionIndex >= questions.Length)
        {
            _quizComplete = true;
            _ai.StartFollowing();
            SimpleNotification.Show("Nhân viên vệ sinh đã đồng hành cùng bạn!");
            interactable = false;
            Debug.Log("[CleaningStaff] All 3 questions answered correctly. Companion now follows.");
            return;
        }

        var q = questions[_currentQuestionIndex];
        _bubble.ShowChoice($"({_currentQuestionIndex + 1}/{questions.Length}) {q.question}", OnAnswer);
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
        if (_ai != null && _ai.CurrentState == CompanionAI.State.Downed) return true;
        return base.IsForbiddenInteraction(weaponController);
    }
}
