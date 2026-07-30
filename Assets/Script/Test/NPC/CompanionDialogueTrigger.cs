using UnityEngine;
using cowsins;

/// <summary>
/// Interactable (E key) that triggers a dialogue choice on the companion.
/// Uses the Cowsins Interactable base class so InteractManager detects it
/// via the "Interactable" layer and shows the prompt text.
///
/// ALSO supports a proximity fallback: if the player is within proximityDistance
/// and presses E (KeyCode.E), the dialogue triggers even without aiming directly
/// at the NPC. This makes interaction much easier in tight spaces.
///
/// Four dialogue stages are supported (Ch3 follower recruitment arc):
///   Stage 1 (Ch3 spawn):       "Tôi cần 40 viên đạn"
///   Stage 2 (after Stage 1):   "Giúp tôi vào 2 tiệm lấy nhu yếu phẩm"
///   Stage 3 (after siege):     "Đưa nhu yếu phẩm cho tôi"
///   Stage 4 (Ch4 save room):   "Tôi có thể giúp anh tìm công thức thuốc..."
///
/// The active stage is set by CompanionManager based on story progress.
/// </summary>
[RequireComponent(typeof(CompanionAI))]
[RequireComponent(typeof(DialogueBubble))]
public class CompanionDialogueTrigger : Interactable
{
    [Header("Dialogue Lines")]
    [TextArea(2, 4)]
    public string stage1Line = "Này anh bạn, tôi cần ít đạn. Anh có dư 40 viên không?";
    [TextArea(2, 4)]
    public string stage2Line = "Cảm ơn đạn. Giờ tôi cần anh giúp — vào 2 tiệm kia tìm nhu yếu phẩm giúp tôi. Tôi bị thương không đi được.";
    [TextArea(2, 4)]
    public string stage3Line = "Anh lấy được nhu yếu phẩm rồi à? Đưa cho tôi, rồi tôi sẽ đi cùng anh.";
    [TextArea(2, 4)]
    public string stage4Line = "Tôi có thể giúp anh tìm công thức thuốc, nhưng với điều kiện là anh phải cho tôi đi cùng.";

    [Header("Interact Text")]
    public string stage1InteractText = "Nói chuyện";
    public string stage2InteractText = "Nói chuyện";
    public string stage3InteractText = "Nói chuyện";
    public string stage4InteractText = "Nói chuyện";

    [Header("Proximity Fallback")]
    [Tooltip("If the player is within this distance and presses E, the dialogue triggers even without aiming at the NPC.")]
    public float proximityDistance = 2.5f;

    [Tooltip("Key to press for proximity interaction.")]
    public KeyCode proximityKey = KeyCode.E;

    /// <summary>0 = no dialogue available, 1..4 = active stage.</summary>
    public int ActiveStage { get; set; } = 1;

    private DialogueBubble _bubble;
    private bool _consumed;
    private bool _permanentlyDisabled;
    private Transform _player;
    private cowsins.InputManager _playerInput;

    private void Awake()
    {
        _bubble = GetComponent<DialogueBubble>();
        var field = typeof(Interactable).GetField("instantInteraction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) field.SetValue(this, true);
    }

    private void OnDisable()
    {
        _player = null;
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        string target = GetInteractTextForStage(ActiveStage);
        if (interactText != target) interactText = target;

        var ai = GetComponent<CompanionAI>();
        if (ai != null && ai.CurrentState == CompanionAI.State.Downed)
        {
            if (interactable) interactable = false;
            return;
        }

        if (_permanentlyDisabled) return;

        // Proximity fallback: check E key press when near the NPC.
        if (_consumed || _bubble == null || _bubble.IsChoiceActive) return;
        if (ActiveStage <= 0) return;

        if (_player == null) FindPlayer();
        if (_player == null) return;

        // Read the Interacting action via the player's InputManager (Input
        // System). Fallback to Input.GetKeyDown for Input Manager mode.
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
                TriggerDialogue();
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
        if (_consumed || _permanentlyDisabled) return;
        if (_bubble == null || _bubble.IsChoiceActive) return;
        if (ActiveStage <= 0) return;
        // Disable dialogue while the companion is Downed — E is used for rescue.
        var ai = GetComponent<CompanionAI>();
        if (ai != null && ai.CurrentState == CompanionAI.State.Downed) return;
        TriggerDialogue();
    }

    /// <summary>
    /// While the companion is Downed, block the cowsins InteractManager from
    /// treating this as a normal interactable. This prevents the "Nói chuyện"
    /// prompt from appearing and stops InteractManager from consuming the E key
    /// (which must be free for the rescue hold in CompanionAI.UpdateDowned).
    /// </summary>
    public override bool IsForbiddenInteraction(IWeaponReferenceProvider weaponController)
    {
        if (_permanentlyDisabled) return true;
        var ai = GetComponent<CompanionAI>();
        if (ai != null && ai.CurrentState == CompanionAI.State.Downed) return true;
        return base.IsForbiddenInteraction(weaponController);
    }

    private void TriggerDialogue()
    {
        if (_consumed || _permanentlyDisabled || _bubble == null || _bubble.IsChoiceActive) return;
        if (ActiveStage <= 0) return;

        // Stage 4 is a 5-question interrogation handled by CompanionManager,
        // not a single ShowChoice call here.
        if (ActiveStage == 4)
        {
            if (CompanionManager.Instance != null)
            {
                CompanionManager.Instance.StartStage4Interrogation();
            }
            return;
        }

        string line = GetDialogueLineForStage(ActiveStage);
        _bubble.ShowChoice(line, OnChoiceMade);
    }

    private string GetDialogueLineForStage(int stage)
    {
        switch (stage)
        {
            case 2: return stage2Line;
            case 3: return stage3Line;
            case 4: return stage4Line;
            default: return stage1Line;
        }
    }

    private string GetInteractTextForStage(int stage)
    {
        switch (stage)
        {
            case 2: return stage2InteractText;
            case 3: return stage3InteractText;
            case 4: return stage4InteractText;
            default: return stage1InteractText;
        }
    }

    private void OnChoiceMade(bool accepted)
    {
        _consumed = true;
        // Remember the stage before HandleDialogueChoice may reset it.
        int stageBefore = ActiveStage;
        if (CompanionManager.Instance != null)
        {
            CompanionManager.Instance.HandleDialogueChoice(ActiveStage, accepted);
        }
        // Only disable further interactions if HandleDialogueChoice did NOT
        // reset the trigger (e.g. stage 1 accept with insufficient ammo calls
        // ResetForStage to allow retry — in that case interactable stays true).
        if (ActiveStage == stageBefore && !_consumedWasReset())
            interactable = false;
    }

    /// <summary>Returns true if ResetForStage was called during the last
    /// HandleDialogueChoice (i.e. _consumed was reset back to false).</summary>
    private bool _consumedWasReset()
    {
        // After ResetForStage, _consumed is false. If we set it to true at the
        // start of OnChoiceMade and it's now false, ResetForStage ran.
        return _consumed == false;
    }

    /// <summary>
    /// Resets the consumed flag and re-enables interaction so the player can
    /// interact again after revive. Stage progress is preserved.
    /// Called by CompanionAI.EnterDowned().
    /// </summary>
    public void ResetConsumed()
    {
        if (_permanentlyDisabled) return;
        _consumed = false;
        interactable = true;
        if (_bubble != null) _bubble.ForceHide();
    }

    /// <summary>Re-enables the trigger for a new stage (called by CompanionManager).</summary>
    public void ResetForStage(int stage)
    {
        if (_permanentlyDisabled) return;
        ActiveStage = stage;
        _consumed = false;
        interactable = true;
    }

    /// <summary>Permanently disables interaction (called after stage 4 skip).</summary>
    public void DisableInteraction()
    {
        _permanentlyDisabled = true;
        _consumed = true;
        interactable = false;
        if (_bubble != null) _bubble.ForceHide();
    }
}
