using UnityEngine;
using cowsins;

/// <summary>
/// Singleton that manages the companion NPC lifecycle across chapters.
///
/// Responsibilities:
///   - Spawn the companion after Chapter 2 is completed (at the specified world position).
///   - Track whether the player accepted/refused at each dialogue stage.
///   - Teleport the companion near the player on chapter changes (so it doesn't
///     get blocked by ChapterBoundary walls).
///   - Trigger dialogue stage 2 when the player reaches Chapter 4's save room.
///   - Apply the "skip" logic when the player accepts stage 2 (skip Q9-Q10,
///     spawn Tank boss with reduced HP).
///
/// The companion prefab is loaded from Resources. If not found, the manager
/// logs a warning and does nothing (the game proceeds without a companion).
/// </summary>
public class CompanionManager : MonoBehaviour
{
    public static CompanionManager Instance { get; private set; }

    [Header("Companion Prefab")]
    [Tooltip("Prefab name in Resources (without extension). Must have CompanionAI + DialogueBubble + CompanionDialogueTrigger + CompanionHealthBar.")]
    public string companionPrefabResource = "Companion/BikerSurvivor";

    [Header("Spawn Position (after Chapter 2)")]
    public Vector3 spawnPosition = new Vector3(116.11f, 0f, -25.22f);

    [Header("Walk-Away Position (if refused at stage 1)")]
    public Vector3 deadEndPoint = new Vector3(60.62f, 0f, -21.49f);

    [Header("Ammo Cost (stage 1 accept)")]
    [Tooltip("Number of reserve bullets (totalBullets) the player must give the companion " +
             "for it to follow. If the current weapon's reserve ammo is below this, " +
             "the companion refuses to follow and the player can retry later.")]
    public int requiredAmmoToFollow = 40;

    [Header("Boss Skip")]
    [Tooltip("Tank boss max HP when the player accepts the stage 4 skip path (≥3 honest answers).")]
    public int skippedTankMaxHealth = 250;

    [Header("Stage 4 — 5 Questions (after Chapter 3)")]
    [Tooltip("5 yes/no questions asked by the follower after Chapter 3. The player answers each with Y/N. If the player answers YES to at least 'honestThreshold' questions, the follower trusts the player and helps skip Chapter 4 (finds the detonator). Otherwise the follower stays but does not help skip — the player must complete all Ch4 quests normally.")]
    [TextArea(2, 4)]
    public string[] stage4Questions = new string[]
    {
        "Anh đến thành phố này để tìm người thân, đúng không?",
        "Anh đang mang theo một thứ gì đó quan trọng, phải không?",
        "Anh từng là quân nhân, đúng không?",
        "Anh biết gì đó về vụ bùng phát dịch bệnh, đúng không?",
        "Anh đang tìm cách phá hủy nguồn gốc của dịch bệnh, đúng không?"
    };

    [Tooltip("Minimum number of YES answers required for the follower to trust the player and help skip Chapter 4.")]
    public int honestThreshold = 3;

    [Tooltip("Root transform of the Ch4_Residential zone, used by the skip path to find and collect all Ch4 collectibles. Assign in the inspector.")]
    public Transform ch4ResidentialRoot;

    [Tooltip("Intro line shown as a speech bubble before the 5 questions begin, so the player understands the context.")]
    [TextArea(2, 4)]
    public string stage4IntroLine = "Tôi muốn biết rõ anh hơn trước khi đi cùng. Hãy trả lời thật lòng 5 câu hỏi của tôi.";

    [Tooltip("How long (seconds) the intro line stays on screen before the first question appears.")]
    public float stage4IntroHold = 3f;

    [Header("Skip Cutscene (played between Ch4 and Ch5 skip)")]
    [Tooltip("Title of the cutscene shown when the player accepts the skip path.")]
    public string skipCutsceneTitle = "Tìm thấy kíp nổ";
    [Tooltip("Body text of the skip cutscene.")]
    [TextArea(3, 8)]
    public string skipCutsceneBody = "Sau nhiều giờ tìm kiếm, cuối cùng người chơi cũng đã tìm thấy kíp nổ... Đã đến lúc kết thúc mọi thứ.";
    [Tooltip("How long (seconds) the skip cutscene holds after typing completes.")]
    public float skipCutsceneHold = 3f;

    [Header("Skip Teleport")]
    [Tooltip("World position the player + companion are teleported to after the skip cutscene. " +
             "Defaults to near Q11_BombObjective so the player lands right at the Tank boss fight.")]
    public Vector3 skipTeleportPosition = new Vector3(1.71f, 0f, 14.99f);

    // ---- Runtime state ----
    public GameObject CompanionInstance { get; private set; }
    public CompanionAI CompanionAI { get; private set; }
    public CompanionDialogueTrigger DialogueTrigger { get; private set; }

    public bool AcceptedStage1 { get; private set; }
    public bool AcceptedStage2 { get; private set; } // Stage 2 = "giúp vào tiệm"
    public bool AcceptedStage3 { get; private set; } // Stage 3 = "đưa nhu yếu phẩm"
    public bool AcceptedStage4 { get; private set; } // Stage 4 = skip Ch4 (cũ stage 2)
    public bool Spawned => _spawnedFlag && CompanionInstance != null;
    private bool _spawnedFlag;

    // Follower recruitment arc state (Ch3).
    private bool _stage4Armed;   // Stage 4 dialogue armed (on Ch4 entry)
    private bool _subscribed;

    // Stage 4 interrogation (5 yes/no questions after Ch3).
    private bool _stage4InProgress;
    private int _stage4QuestionIndex;
    private int _stage4YesCount;

    // Shop supplies collection (Stage 2 → siege).
    private int _shopSuppliesCollected; // 0..2 (2 shops to loot)
    private int _shopSuppliesRequired = 2;
    private bool _siegeStarted;
    private bool _siegeCompleted;
    private bool _siegeRestartPending;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        Subscribe();
        if (GameOverManager.Instance != null)
        {
            GameOverManager.Instance.OnPlayerDied -= HandlePlayerDeath;
            GameOverManager.Instance.OnPlayerDied += HandlePlayerDeath;
        }
    }

    private void Start()
    {
        // Fallback: if OnEnable ran before StoryManager.Awake.
        Subscribe();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        }
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.OnPlayerDied -= HandlePlayerDeath;
        AbortStage4();
        _subscribed = false;
    }

    private void HandlePlayerDeath()
    {
        AbortStage4();
        // NOTE: _shopSuppliesCollected is intentionally NOT reset here.
        // If the counter were reset, collecting 1/2 shop supplies then
        // dying would cause a softlock: the collected lootable is consumed
        // but the counter is 0, so the player can never reach the required
        // count. Keeping the counter persistent across death fixes this.
        if (_siegeStarted && !_siegeCompleted)
        {
            _siegeStarted = false;
            _siegeRestartPending = true;
            var siege = FindAnyObjectByType<CompanionZombieSiege>();
            if (siege != null && siege.IsSiegeActive) siege.Abort();
            StartCoroutine(RestartSiegeAfterDelay(3f));
        }
    }

    private System.Collections.IEnumerator RestartSiegeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_siegeRestartPending) yield break;
        _siegeRestartPending = false;
        var siege = FindAnyObjectByType<CompanionZombieSiege>();
        if (siege != null)
        {
            siege.StartSiege(OnZombieSiegeCompleted);
            Debug.Log("[CompanionManager] Siege restarted after death.");
        }
    }

    /// <summary>Resets Stage 4 interrogation state so the player can retry.</summary>
    private void AbortStage4()
    {
        if (!_stage4InProgress) return;
        _stage4InProgress = false;
        _stage4QuestionIndex = 0;
        _stage4YesCount = 0;
        // Re-enable the dialogue trigger so the player can re-trigger stage 4.
        if (DialogueTrigger != null)
            DialogueTrigger.ResetForStage(4);
        Debug.Log("[CompanionManager] Stage 4 interrogation aborted — state reset.");
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        if (StoryManager.Instance == null) return;
        StoryManager.Instance.OnQuestCompleted += HandleQuestCompleted;
        StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
        _subscribed = true;
    }

    private void Update()
    {
        // Polling fallback: ensure we're subscribed and spawn if past Ch2.
        if (!_subscribed) Subscribe();

        var sm = StoryManager.Instance;
        if (sm == null) return;

        // If we're at Chapter 3+ and haven't spawned yet, spawn now.
        // This catches cases where the event was missed or an exception
        // in another handler broke the delegate chain.
        if (!Spawned && sm.CurrentChapter >= 3)
        {
            Debug.Log("[CompanionManager] Update fallback: CurrentChapter >= 3, spawning companion.");
            SpawnCompanion();
        }

        // Fallback: arm stage 4 dialogue when already at Chapter 4 with stage 3
        // accepted (follower joined) but stage 4 not armed. HandleChapterChanged
        // only fires on chapter transitions, so if the game starts directly at
        // Ch4 (e.g. for testing), the event is missed and stage 4 never arms.
        if (Spawned && AcceptedStage3 && sm.CurrentChapter >= 4 && !_stage4Armed && DialogueTrigger != null)
        {
            _stage4Armed = true;
            DialogueTrigger.ResetForStage(4);
            Debug.Log("[CompanionManager] Update fallback: armed stage 4 dialogue (already at Chapter 4).");
        }
    }

    // ---- Quest / Chapter hooks ----

    private void HandleQuestCompleted(QuestData quest)
    {
        try
        {
            var sm = StoryManager.Instance;
            if (sm == null) return;

            if (quest != null && quest.chapter == 2 && !Spawned)
            {
                // Use questId comparison instead of reference equality,
                // since the quest instance might differ.
                var ch2 = sm.chapter2Quests;
                if (ch2 != null && ch2.Length > 0)
                {
                    var lastQuest = ch2[ch2.Length - 1];
                    if (lastQuest != null && quest.questId == lastQuest.questId)
                    {
                        Debug.Log("[CompanionManager] Last Ch2 quest completed. Spawning companion.");
                        SpawnCompanion();
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CompanionManager] HandleQuestCompleted exception: {e}");
        }
    }

    private void HandleChapterChanged(int oldChapter, int newChapter)
    {
        try
        {
            if (newChapter <= 0) return; // -1 = story complete

            // Fallback: if we just entered Chapter 3 (or later) and the companion
            // hasn't spawned yet, spawn now.
            if (newChapter >= 3 && !Spawned)
            {
                Debug.Log("[CompanionManager] Chapter changed to " + newChapter + ", spawning companion (fallback).");
                SpawnCompanion();
            }

            // Teleport companion near the player on every chapter change.
            // Only if the follower has joined (Stage 3 accepted).
            if (Spawned && CompanionAI != null && AcceptedStage3)
            {
                CompanionAI.TeleportNearPlayer();
            }

            // Arm stage 4 dialogue when entering Chapter 4 (skip-Ch4 offer).
            if (newChapter == 4 && Spawned && AcceptedStage3 && !_stage4Armed)
            {
                _stage4Armed = true;
                if (DialogueTrigger != null)
                    DialogueTrigger.ResetForStage(4);
                Debug.Log("[CompanionManager] Stage 4 dialogue armed (Chapter 4).");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CompanionManager] HandleChapterChanged exception: {e}");
        }
    }

    // ---- Spawn ----

    private void SpawnCompanion()
    {
        if (Spawned) return;
        // Fresh companion instance — reset any stale Stage 4 state that may
        // have persisted across scene reloads (CompanionManager is DDOL).
        _stage4InProgress = false;
        _stage4QuestionIndex = 0;
        _stage4YesCount = 0;
        var prefab = Resources.Load<GameObject>(companionPrefabResource);
        if (prefab == null)
        {
            Debug.LogWarning($"[CompanionManager] Companion prefab '{companionPrefabResource}' not found in Resources. Companion will not spawn.");
            return;
        }

        CompanionInstance = Instantiate(prefab, spawnPosition, Quaternion.identity);
        CompanionInstance.name = "BikerSurvivor_Companion";
        _spawnedFlag = true;

        CompanionAI = CompanionInstance.GetComponent<CompanionAI>();
        DialogueTrigger = CompanionInstance.GetComponent<CompanionDialogueTrigger>();

        if (DialogueTrigger != null)
        {
            if (AcceptedStage3)
            {
                DialogueTrigger.DisableInteraction();
            }
            else if (_siegeCompleted)
            {
                DialogueTrigger.ResetForStage(3);
            }
            else if (AcceptedStage1)
            {
                DialogueTrigger.ResetForStage(2);
            }
            else
            {
                DialogueTrigger.ActiveStage = 1;
                DialogueTrigger.interactable = true;
            }
        }

        Debug.Log($"[CompanionManager] Companion spawned at {spawnPosition}. Waiting for player interaction (stage 1).");
    }

    // ---- Dialogue choice handler (called by CompanionDialogueTrigger) ----

    public void HandleDialogueChoice(int stage, bool accepted)
    {
        if (stage == 1)
        {
            if (accepted)
            {
                // Check if the player has enough reserve ammo to "pay" the
                // companion. If not, the companion stays put and the player
                // can retry after finding more ammo.
                if (TryConsumeAmmo(requiredAmmoToFollow))
                {
                    AcceptedStage1 = true;
                    // Do NOT StartFollowing yet — Stage 2 (shop supplies) must
                    // be completed first. Arm Stage 2 dialogue.

                    if (DialogueTrigger != null) DialogueTrigger.ResetForStage(2);
                    SimpleNotification.Show("Đã đưa đạn. Follower nhờ bạn vào tiệm lấy nhu yếu phẩm.");
                    Debug.Log($"[CompanionManager] Player ACCEPTED stage 1 and paid {requiredAmmoToFollow} bullets. Armed stage 2 (shop supplies).");
                }
                else
                {
                    // Not enough ammo — re-arm the trigger so the player can
                    // come back later with more ammo and try again.
                    if (DialogueTrigger != null) DialogueTrigger.ResetForStage(1);
                    SimpleNotification.Show($"Bạn không đủ {requiredAmmoToFollow} viên đạn dự trữ. Follower không thể đi theo.");
                    Debug.Log($"[CompanionManager] Player ACCEPTED stage 1 but lacks {requiredAmmoToFollow} reserve bullets. Companion stays. Trigger reset for retry.");
                }
            }
            else
            {
                AcceptedStage1 = false;
                if (CompanionAI != null) CompanionAI.WalkAway(deadEndPoint);
                Debug.Log("[CompanionManager] Player REFUSED stage 1. Companion walks away to " + deadEndPoint);
            }
        }
        else if (stage == 2)
        {
            // Stage 2: "Giúp tôi vào 2 tiệm lấy nhu yếu phẩm"
            AcceptedStage2 = accepted;
            if (accepted)
            {
                // Activate the two shop triggers so the player can loot them.
                EnableShopTriggers(true);
                SimpleNotification.Show("Vào 2 tiệm và nhấn [E] để lấy nhu yếu phẩm.");
                Debug.Log("[CompanionManager] Player ACCEPTED stage 2. Shop triggers activated.");
            }
            else
            {
                // Refused — show a confirmation warning before walking away
                // permanently. The player might have pressed N by accident.
                if (CompanionAI != null)
                {
                    var bubble = CompanionAI.GetComponent<DialogueBubble>();
                    if (bubble != null && !bubble.IsChoiceActive)
                    {
                        bubble.ShowChoice(
                            "Bạn có chắc không giúp? Follower sẽ bỏ đi vĩnh viễn.",
                            confirmed =>
                            {
                                if (confirmed)
                                {
                                    // Player confirmed the refusal — walk away.
                                    CompanionAI.WalkAway(deadEndPoint);
                                    Debug.Log("[CompanionManager] Player CONFIRMED refusal of stage 2. Companion walks away permanently.");
                                }
                                else
                                {
                                    // Player changed their mind — treat as accept.
                                    AcceptedStage2 = true;
                                    EnableShopTriggers(true);
                                    SimpleNotification.Show("Cảm ơn đã đổi ý! Vào 2 tiệm và nhấn [E] để lấy nhu yếu phẩm.");
                                    Debug.Log("[CompanionManager] Player changed mind on stage 2 refusal. Shop triggers activated.");
                                }
                            });
                        return;
                    }
                }
                // Fallback if bubble/CompanionAI missing — walk away directly.
                if (CompanionAI != null) CompanionAI.WalkAway(deadEndPoint);
                Debug.Log("[CompanionManager] Player REFUSED stage 2 (no confirmation UI). Companion walks away permanently.");
            }
        }
        else if (stage == 3)
        {
            // Stage 3: "Đưa nhu yếu phẩm cho tôi" (after siege completed)
            AcceptedStage3 = accepted;
            if (accepted)
            {
                if (CompanionAI != null) CompanionAI.StartFollowing();
                SimpleNotification.Show("Follower đã đồng hành cùng bạn!");
                Debug.Log("[CompanionManager] Player ACCEPTED stage 3. Companion now follows.");
            }
            else
            {
                // Refused — companion walks away permanently.
                if (CompanionAI != null) CompanionAI.WalkAway(deadEndPoint);
                Debug.Log("[CompanionManager] Player REFUSED stage 3. Companion walks away permanently.");
            }
        }
        else if (stage == 4)
        {
            // Stage 4 is now handled by StartStage4Interrogation (5 questions).
            // This branch should not be reached via HandleDialogueChoice anymore,
            // but kept as a safety fallback: treat 'accepted' as a single yes.
            Debug.LogWarning("[CompanionManager] HandleDialogueChoice(4) called directly — use StartStage4Interrogation instead. Falling back to single-question behavior.");
            AcceptedStage4 = accepted;
            if (accepted)
            {
                Debug.Log("[CompanionManager] Player ACCEPTED stage 4 (fallback). Applying skip logic.");
                StartCoroutine(SkipToQuest11WithReducedBoss());
            }
            else
            {
                Debug.Log("[CompanionManager] Player REFUSED stage 4 (fallback). Companion keeps following; player must complete all quests normally.");
            }
        }
    }

    // ---- Stage 4: 5-question interrogation ----

    /// <summary>
    /// Starts the 5-question interrogation sequence. Called by
    /// CompanionDialogueTrigger when the player interacts with the companion
    /// while Stage 4 is armed. Each question is shown via DialogueBubble as a
    /// Y/N choice; after 5 answers, the result is evaluated:
    ///   ≥ honestThreshold YES → follower trusts player → SkipToQuest11
    ///   &lt; honestThreshold YES → follower stays but does not help skip
    /// </summary>
    public void StartStage4Interrogation()
    {
        if (_stage4InProgress) return;
        if (stage4Questions == null || stage4Questions.Length == 0)
        {
            Debug.LogWarning("[CompanionManager] stage4Questions is empty — skipping interrogation, applying skip directly.");
            AcceptedStage4 = true;
            StartCoroutine(SkipToQuest11WithReducedBoss());
            return;
        }

        _stage4InProgress = true;
        _stage4QuestionIndex = 0;
        _stage4YesCount = 0;

        // Show an intro speech line first so the player understands the context
        // before the Y/N questions begin. After the hold duration, the first
        // question is shown.
        if (CompanionAI != null)
        {
            var bubble = CompanionAI.GetComponent<DialogueBubble>();
            if (bubble != null && !string.IsNullOrEmpty(stage4IntroLine))
            {
                bubble.ShowSpeech(stage4IntroLine, stage4IntroHold);
                Debug.Log("[CompanionManager] Stage 4 intro line shown.");
                if (CompanionAI != null)
                {
                    CompanionAI.StartCoroutine(DelayFirstQuestion(stage4IntroHold));
                }
                return;
            }
        }

        // Fallback: no intro, ask first question immediately.
        AskNextStage4Question();
    }

    private System.Collections.IEnumerator DelayFirstQuestion(float delay)
    {
        yield return new WaitForSecondsRealtime(delay + 0.3f);
        AskNextStage4Question();
    }

    private void AskNextStage4Question()
    {
        if (CompanionAI == null)
        {
            Debug.LogWarning("[CompanionManager] CompanionAI null during interrogation — aborting.");
            _stage4InProgress = false;
            return;
        }
        var bubble = CompanionAI.GetComponent<DialogueBubble>();
        if (bubble == null)
        {
            Debug.LogWarning("[CompanionManager] DialogueBubble null during interrogation — aborting.");
            _stage4InProgress = false;
            return;
        }

        string question = stage4Questions[_stage4QuestionIndex];
        Debug.Log($"[CompanionManager] Stage 4 interrogation: Q{_stage4QuestionIndex + 1}/{stage4Questions.Length}: \"{question}\"");
        bubble.ShowChoice($"({_stage4QuestionIndex + 1}/{stage4Questions.Length}) {question}", OnStage4Answer);
    }

    private void OnStage4Answer(bool yes)
    {
        if (yes) _stage4YesCount++;
        Debug.Log($"[CompanionManager] Stage 4 Q{_stage4QuestionIndex + 1}: {(yes ? "YES" : "NO")} (running yes count: {_stage4YesCount})");

        _stage4QuestionIndex++;
        if (_stage4QuestionIndex < stage4Questions.Length)
        {
            // Small delay before next question so the bubble can fade out/in.
            CompanionAI.StartCoroutine(DelayNextQuestion(0.4f));
        }
        else
        {
            EvaluateStage4Result();
        }
    }

    private System.Collections.IEnumerator DelayNextQuestion(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        AskNextStage4Question();
    }

    private void EvaluateStage4Result()
    {
        _stage4InProgress = false;
        bool trusted = _stage4YesCount >= honestThreshold;
        Debug.Log($"[CompanionManager] Stage 4 interrogation complete: {_stage4YesCount}/{stage4Questions.Length} YES → trusted={trusted}");

        if (trusted)
        {
            AcceptedStage4 = true;
            SimpleNotification.Show($"Anh ấy tin bạn ({_stage4YesCount}/{stage4Questions.Length} câu thật lòng). Anh ấy sẽ giúp bạn tìm kíp nổ!");
            Debug.Log("[CompanionManager] Player trusted by follower. Applying skip logic.");
            StartCoroutine(SkipToQuest11WithReducedBoss());
        }
        else
        {
            AcceptedStage4 = false;
            SimpleNotification.Show($"Anh ấy không tin bạn ({_stage4YesCount}/{stage4Questions.Length} câu thật lòng). Bạn phải tự hoàn thành Chapter 4.");
            Debug.Log("[CompanionManager] Player NOT trusted by follower. Companion keeps following; player must complete all quests normally.");
        }
    }

    /// <summary>
    /// Attempts to consume <paramref name="amount"/> reserve bullets from the
    /// player's currently-equipped weapon. Only <c>totalBullets</c> (reserve
    /// ammo) is considered — bullets already in the magazine are NOT touched.
    /// Returns true and deducts the ammo if the current weapon has enough
    /// reserve bullets; returns false and leaves ammo unchanged otherwise.
    /// </summary>
    private bool TryConsumeAmmo(int amount)
    {
        if (amount <= 0) return true;
        var wc = FindAnyObjectByType<cowsins.WeaponController>();
        if (wc == null || wc.Id == null)
        {
            Debug.LogWarning("[CompanionManager] WeaponController not found; cannot consume ammo.");
            return false;
        }
        int reserve = wc.Id.totalBullets;
        if (reserve < amount)
        {
            Debug.Log($"[CompanionManager] Not enough reserve ammo: have {reserve}, need {amount}.");
            return false;
        }
        wc.Id.totalBullets = reserve - amount;
        Debug.Log($"[CompanionManager] Consumed {amount} reserve bullets. Remaining: {wc.Id.totalBullets}.");
        return true;
    }

    // ---- Stage 2: Shop supplies collection ----

    /// <summary>
    /// Enables/disables all CompanionShopTrigger colliders in the scene. Called
    /// when the player accepts Stage 2 (enable) so they can loot the shops.
    /// Also toggles the follower-stage QuestBeacons on the shops so the player
    /// can see where to go.
    /// </summary>
    private void EnableShopTriggers(bool enable)
    {
        var shops = FindObjectsByType<CompanionShopTrigger>(FindObjectsSortMode.None);
        foreach (var shop in shops)
        {
            shop.SetAvailable(enable);
        }
        Debug.Log($"[CompanionManager] {(enable ? "Enabled" : "Disabled")} {shops.Length} CompanionShopTrigger(s).");

        // Toggle shop beacons (showOnFollowerStage == 2).
        var beacons = FindObjectsByType<QuestBeacon>(FindObjectsSortMode.None);
        int toggled = 0;
        foreach (var b in beacons)
        {
            if (b.showOnFollowerStage == 2)
            {
                b.SetFollowerActive(enable);
                toggled++;
            }
        }
        Debug.Log($"[CompanionManager] Toggled {toggled} shop beacon(s) ({(enable ? "on" : "off")}).");
    }

    /// <summary>
    /// Called by CompanionShopTrigger when the player presses E inside a shop
    /// and collects the supplies. When all required shops are looted, starts
    /// the zombie siege event.
    /// </summary>
    public void OnShopSuppliesCollected()
    {
        if (_siegeRestartPending) return;
        _shopSuppliesCollected++;
        Debug.Log($"[CompanionManager] Shop supplies collected: {_shopSuppliesCollected}/{_shopSuppliesRequired}.");

        if (_shopSuppliesCollected >= _shopSuppliesRequired && !_siegeStarted)
        {
            _siegeStarted = true;
            // Turn off all shop beacons (Stage 2) — both shops looted.
            SetFollowerBeacons(2, false);
            // Turn on the siege beacon (Stage 3) so the player knows where the
            // siege is happening (if a siege beacon is placed).
            SetFollowerBeacons(3, true);
            SimpleNotification.Show("Zombie đang bao vây! Tiêu diệt 10 con để sống sót!");
            var siege = FindAnyObjectByType<CompanionZombieSiege>();
            if (siege != null)
            {
                siege.StartSiege(OnZombieSiegeCompleted);
                Debug.Log("[CompanionManager] Zombie siege started.");
            }
            else
            {
                Debug.LogWarning("[CompanionManager] CompanionZombieSiege not found in scene — skipping siege, arming stage 3 directly.");
                OnZombieSiegeCompleted();
            }
        }
        else
        {
            SimpleNotification.Show($"Đã lấy nhu yếu phẩm {_shopSuppliesCollected}/{_shopSuppliesRequired}. Còn {_shopSuppliesRequired - _shopSuppliesCollected} tiệm nữa.");
        }
    }

    /// <summary>
    /// Called by CompanionZombieSiege when the player has killed the required
    /// number of zombies. Arms Stage 3 dialogue so the player can hand over
    /// the supplies and recruit the follower.
    /// </summary>
    private void OnZombieSiegeCompleted()
    {
        _siegeCompleted = true;

        // Turn off the siege beacon (Stage 3).
        SetFollowerBeacons(3, false);
        if (DialogueTrigger != null) DialogueTrigger.ResetForStage(3);
        SimpleNotification.Show("Đã tiêu diệt hết zombie! Quay lại nói chuyện với follower.");
        Debug.Log("[CompanionManager] Zombie siege completed. Stage 3 dialogue armed.");
    }

    /// <summary>
    /// Toggles all QuestBeacons with showOnFollowerStage == stage on/off.
    /// Used to guide the player to the shops (stage 2) and the siege (stage 3).
    /// </summary>
    private void SetFollowerBeacons(int stage, bool active)
    {
        var beacons = FindObjectsByType<QuestBeacon>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var b in beacons)
        {
            if (b.showOnFollowerStage == stage)
            {
                b.SetFollowerActive(active);
                count++;
            }
        }
        Debug.Log($"[CompanionManager] SetFollowerBeacons(stage={stage}, active={active}): toggled {count} beacon(s).");
    }

    // ---- Skip logic ----

    private System.Collections.IEnumerator SkipToQuest11WithReducedBoss()
    {
        var sm = StoryManager.Instance;
        if (sm == null) yield break;

        // 1) Skip remaining Chapter 4 quests (Quest 9) silently.
        int safety = 0;
        while (sm.CurrentChapter == 4 && sm.ActiveQuest != null && safety < 50)
        {
            sm.CompleteActiveQuest();
            yield return null;
            safety++;
        }
        if (safety >= 50)
            Debug.LogError("[CompanionManager] Safety limit hit skipping Ch4 quests! Possible infinite loop.");

        // 2) Side quests Ch4: unlock + auto-complete (grant rewards).
        //    The skip path assumes the player "already explored" Ch4, so all
        //    side quests are considered done with their rewards granted.
        AutoCompleteChapter4SideQuests();

        // 3) Collect (and hide) all Ch4 collectibles (journals).
        //    This both removes them from the world and registers them in
        //    CollectibleManager so the journal gallery reflects them.
        CollectAllChapter4Collectibles();

        // 4) Mark SaveRoom_Ch4 cutscene as played (so re-entering doesn't
        //    replay the "CHƯƠNG 4" banner). The checkpoint itself is set in
        //    step 7 to the teleport position (near Q11) so a death respawns
        //    the player near the boss fight, not back in Ch4.
        MarkSaveRoomCh4CutscenePlayed();

        // 5) Play the skip cutscene as a narrative bridge between Ch4 and Ch5.
        //    This explains how the player found the detonator without collecting
        //    all the journals, so the skip path doesn't feel abrupt.
        yield return PlaySkipCutscene();

        // 6) After Ch4 completes, the chapter advances to 5 with PendingChapterEntry.
        //    Force-activate the pending chapter quest so we can skip Ch5 quests.
        if (sm.PendingChapterEntry)
        {
            sm.ActivatePendingChapterQuest();
            yield return null;
        }

        // 7) Skip Chapter 5 quests until we reach index 0 (Q10).
        //    then force-set to index 1 (Q11 boss fight) to guarantee
        //    the player lands at the Tank boss regardless of trigger
        //    overlaps at the teleport position.
        safety = 0;
        while (sm.CurrentChapter == 5 && sm.CurrentQuestIndex < 1 && sm.ActiveQuest != null && safety < 50)
        {
            sm.CompleteActiveQuest();
            yield return null;
            safety++;
        }
        if (safety >= 50)
            Debug.LogError("[CompanionManager] Safety limit hit skipping Ch5 quests! Possible infinite loop.");
        sm.ForceSetQuest(5, 1);

        // 8) Set the reduced boss HP so the next Tank spawned by
        //    WaveQuestInteractable has reduced health (consumed on spawn).
        CompanionBossSkipHook.PendingReducedBossHP = skippedTankMaxHealth;

        // 9) Teleport the player + companion to the skip teleport position
        //    (near Q11_BombObjective). The save checkpoint is set to SaveRoom_Ch5
        //    (not the teleport position) so a death during the boss fight respawns
        //    the player at the Chapter 5 save zone — the player can heal and
        //    re-prepare before retrying the Tank boss, instead of respawning
        //    right on top of the fight.
        //    TeleportPlayerAndCompanion also restores timeScale if it's 0.

        // Temporarily disable chapter-transition cutscenes so SaveRoom_Ch5
        // doesn't play a "CHƯƠNG 5" banner (the skip cutscene already covers
        // the narrative bridge) AND so it doesn't capture prevTimeScale=0 and
        // leave the game paused after the transition cutscene ends.
        bool prevChapterCutsceneFlag = sm.playChapterTransitionCutscene;
        sm.playChapterTransitionCutscene = false;

        // Resolve SaveRoom_Ch5 position FIRST so we teleport the player
        // directly into the Chapter 5 boundary zone (not a static position
        // that may sit outside the boundary, which would leave spawners
        // disabled and the chapter system desynced).
        var ch5Checkpoint = ResolveChapter5SaveRoomCheckpoint();
        TeleportPlayerAndCompanion(ch5Checkpoint);

        // Set checkpoint to SaveRoom_Ch5's position so death respawns the
        // player at the save zone (heal + re-prepare) instead of at the
        // boss-fight teleport spot.
        SaveRoom.LastCheckpoint = ch5Checkpoint;
        SaveRoom.LastCheckpointRotation = Quaternion.identity;

        // Mark SaveRoom_Ch5's cutscene as played so re-entering the save
        // room doesn't replay the "CHƯƠNG 5" banner.
        var ch5SaveRoom = ResolveChapter5SaveRoom();
        if (ch5SaveRoom != null) ch5SaveRoom.MarkCutscenePlayed();

        // 10) Force re-evaluate all chapter boundaries + save rooms based
        //     on the new player position (same pattern as GameOverManager.
        //     RespawnWarp). This ensures spawners and chapter state are
        //     correctly synced after teleport, even if OnTriggerEnter didn't
        //     fire or the teleport position doesn't overlap any collider.
        var boundaries = FindObjectsByType<ChapterBoundary>(FindObjectsSortMode.None);
        foreach (var b in boundaries)
        {
            if (b != null) b.ReevaluateState(ch5Checkpoint);
        }
        var saverooms = FindObjectsByType<SaveRoom>(FindObjectsSortMode.None);
        foreach (var sr in saverooms)
        {
            if (sr != null) sr.ReevaluateState(ch5Checkpoint);
        }

        // Safety net: wait one frame for any remaining triggers to settle,
        // then force-restore timeScale.
        yield return null;
        if (!(PauseManager.Instance != null && PauseManager.Instance.IsPaused) &&
            !(GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver))
            Time.timeScale = 1f;

        // Re-enable chapter-transition cutscenes for future chapters.
        sm.playChapterTransitionCutscene = prevChapterCutsceneFlag;

        // 11) Disable the dialogue trigger so the player cannot re-interact
        //     with the follower and trigger another skip.
        if (DialogueTrigger != null) DialogueTrigger.DisableInteraction();

        Debug.Log($"[CompanionManager] Skip logic complete. Player teleported to SaveRoom_Ch5 ({ch5Checkpoint}), checkpoint set to same, should now fight Tank boss with reduced HP ({skippedTankMaxHealth}).");
    }

    /// <summary>
    /// Unlocks and auto-completes every Chapter 4 side quest, granting their
    /// rewards (EXP + journal). Used by the skip path so the player doesn't
    /// miss out on side quest rewards when skipping Ch4.
    /// </summary>
    private void AutoCompleteChapter4SideQuests()
    {
        var sqm = SideQuestManager.Instance;
        if (sqm == null)
        {
            Debug.LogWarning("[CompanionManager] SideQuestManager not found; cannot auto-complete Ch4 side quests.");
            return;
        }
        var quests = sqm.GetChapterSideQuests(4);
        if (quests == null) return;
        foreach (var q in quests)
        {
            if (q == null) continue;
            if (sqm.IsCompleted(q)) continue;
            // Unlock first (so CompleteSideQuest sees it as active), then complete.
            if (!sqm.IsActive(q)) sqm.UnlockChapterPublic(4);
            sqm.CompleteSideQuest(q);
        }
        Debug.Log($"[CompanionManager] Auto-completed {quests.Length} Chapter 4 side quests (rewards granted).");
    }

    /// <summary>
    /// Collects and hides every Collectible under Ch4_Residential so the skip
    /// path doesn't leave uncollected journals visible in the world. Each
    /// collectible is registered in CollectibleManager (so the journal gallery
    /// reflects it) and its GameObject is deactivated.
    /// </summary>
    private void CollectAllChapter4Collectibles()
    {
        var root = ch4ResidentialRoot;
        if (root == null)
        {
            var go = GameObject.Find("=== WORLD ===/StoryZones/Ch4_Residential");
            if (go != null) root = go.transform;
        }
        if (root == null)
        {
            Debug.LogWarning("[CompanionManager] ch4ResidentialRoot not assigned; cannot collect Ch4 collectibles.");
            return;
        }
        int count = 0;
        var allCollectibles = root.GetComponentsInChildren<Collectible>(false);
        foreach (var col in allCollectibles)
        {
            if (col != null && !col.IsPicked)
            {
                col.Collect();
                count++;
            }
        }
        Debug.Log($"[CompanionManager] Collected {count} Ch4 collectibles (journals registered + hidden).");
    }

    /// <summary>
    /// Marks the SaveRoom_Ch4 chapter-transition cutscene as already played so
    /// re-entering the save room doesn't replay the "CHƯƠNG 4" banner. The
    /// checkpoint is NOT set here — it is set later to SaveRoom_Ch5's position.
    /// </summary>
    private void MarkSaveRoomCh4CutscenePlayed()
    {
        var saveRoomGO = GameObject.Find("=== WORLD ===/StoryZones/Ch4_Residential/SaveRoom_Ch4");
        if (saveRoomGO == null) return;
        var sr = saveRoomGO.GetComponent<SaveRoom>();
        if (sr == null) return;
        sr.MarkCutscenePlayed();
        Debug.Log("[CompanionManager] Marked SaveRoom_Ch4 cutscene as played (no re-trigger on re-enter).");
    }

    /// <summary>
    /// Resolves the Chapter 5 SaveRoom's checkpoint position — the position
    /// the player should respawn at after dying during the Ch5 boss fight.
    /// Used by the skip flow so death respawns the player at the save zone
    /// (where they can heal + re-prepare) instead of at the boss-fight
    /// teleport spot.
    ///
    /// Falls back to skipTeleportPosition if SaveRoom_Ch5 is not found, so
    /// the checkpoint is always set to a sane value.
    /// </summary>
    private Vector3 ResolveChapter5SaveRoomCheckpoint()
    {
        var saveRoomGO = GameObject.Find("=== WORLD ===/StoryZones/Ch5_ApartmentBridge/SaveRoom_Ch5");
        if (saveRoomGO != null)
        {
            var sr = saveRoomGO.GetComponent<SaveRoom>();
            if (sr != null)
            {
                // SaveRoom.CheckpointPosition returns _checkpointPos, which is
                // set in Start() to respawnPoint.position (or transform.position
                // if respawnPoint is null). At runtime (after Start), this is
                // the correct respawn position.
                var cp = sr.CheckpointPosition;
                if (cp != Vector3.zero)
                {
                    Debug.Log($"[CompanionManager] Skip checkpoint set to SaveRoom_Ch5 at {cp}.");
                    return cp;
                }
            }
        }
        // Fallback: search for any SaveRoom assigned to Chapter 5.
        Debug.LogWarning("[CompanionManager] SaveRoom_Ch5 not found; searching for any chapter-5 SaveRoom.");
        var allRooms = FindObjectsByType<SaveRoom>(FindObjectsSortMode.None);
        foreach (var r in allRooms)
        {
            if (r == null) continue;
            var cp = r.CheckpointPosition;
            if (cp != Vector3.zero)
            {
                Debug.Log($"[CompanionManager] Found alternative Ch5 checkpoint at {cp} (via {r.name}).");
                return cp;
            }
        }
        // Final fallback: use the teleport position if no Ch5 SaveRoom is found.
        Debug.LogWarning("[CompanionManager] No Chapter 5 SaveRoom found; falling back to skipTeleportPosition for checkpoint.");
        return skipTeleportPosition;
    }

    /// <summary>
    /// Resolves the Chapter 5 SaveRoom component (not just the position).
    /// Returns the SaveRoom, or null if not found.
    /// </summary>
    private SaveRoom ResolveChapter5SaveRoom()
    {
        var saveRoomGO = GameObject.Find("=== WORLD ===/StoryZones/Ch5_ApartmentBridge/SaveRoom_Ch5");
        if (saveRoomGO != null)
        {
            var sr = saveRoomGO.GetComponent<SaveRoom>();
            if (sr != null) return sr;
        }
        // Fallback: search for any SaveRoom assigned to Chapter 5.
        var allRooms = FindObjectsByType<SaveRoom>(FindObjectsSortMode.None);
        foreach (var r in allRooms)
        {
            if (r == null) continue;
            if (r.chapter == 5) return r;
        }
        return null;
    }

    /// <summary>
    /// Teleports the player and companion to the given world position. The
    /// player is warped via a combination of PlayerStats.Respawn (heals + fires
    /// OnRespawn event), direct Rigidbody positioning, and Transform sync.
    /// The companion is warped via CompanionAI.TeleportNearPlayer.
    ///
    /// NOTE: Time.timeScale MUST be > 0 for the Rigidbody→Transform sync to
    /// happen on the next physics step. This method temporarily restores
    /// timeScale if it's 0 (e.g. right after a cutscene).
    /// </summary>
    private void TeleportPlayerAndCompanion(Vector3 position)
    {
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null)
        {
            Debug.LogWarning("[CompanionManager] Player not found; cannot teleport.");
            return;
        }

        // Restore timeScale if it's 0 — the Rigidbody→Transform sync needs a
        // physics step, which only runs when timeScale > 0.
        if (Time.timeScale == 0f)
        {
            bool pauseOpen = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
            bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
            if (!pauseOpen && !gameOver) Time.timeScale = 1f;
        }

        // 1) Try PlayerStats.Respawn (fires OnRespawn event → PlayerMovement.TeleportPlayer).
        var ps = playerGO.GetComponentInParent<PlayerStats>();
        if (ps == null) ps = playerGO.GetComponentInChildren<PlayerStats>();
        if (ps != null)
        {
            ps.Respawn(position);
            Debug.Log($"[CompanionManager] Player teleported to {position} via PlayerStats.Respawn.");
        }

        // 2) Direct warp on the Rigidbody's GameObject. The Rigidbody is
        //    typically on the tagged "Player" child (not the root), so we set
        //    both rb.position and the transform of the rb's GameObject.
        var rb = playerGO.GetComponentInParent<Rigidbody>();
        if (rb == null) rb = playerGO.GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
            // Also set the transform of the GameObject that owns the Rigidbody
            // so it syncs immediately (without waiting for a physics step).
            rb.transform.position = position;
        }
        else
        {
            // No Rigidbody — just set the transform directly.
            playerGO.transform.position = position;
        }

        // 3) Sync transforms so the hierarchy reflects the new positions
        //    immediately (critical when timeScale was just restored from 0).
        Physics.SyncTransforms();

        Debug.Log($"[CompanionManager] Player direct-warp to {position} (rb+transform+sync).");

        // 4) Teleport ALL companions near the player (spawned + scene-placed).
        var allCompanions = FindObjectsByType<CompanionAI>(FindObjectsSortMode.None);
        foreach (var c in allCompanions)
        {
            if (c != null) c.TeleportNearPlayer(2.5f);
        }
    }

    /// <summary>
    /// Plays the skip-path cutscene ("Tìm thấy kíp nổ") as a narrative bridge
    /// between Chapter 4 and Chapter 5. Creates a transient CutscenePlayer,
    /// plays it, and yields until it completes.
    /// </summary>
    private System.Collections.IEnumerator PlaySkipCutscene()
    {
        var go = new GameObject("CompanionSkipCutscene");
        // Parent under the manager so it doesn't get destroyed prematurely.
        go.transform.SetParent(transform, false);
        var cutscene = go.AddComponent<CutscenePlayer>();
        cutscene.title = skipCutsceneTitle;
        cutscene.body = skipCutsceneBody;
        cutscene.hold = skipCutsceneHold;
        cutscene.fadeIn = 0.6f;
        cutscene.fadeOut = 1.0f;

        // Ensure timeScale is 1 before the cutscene starts so CutscenePlayer
        // captures prevTimeScale=1 and restores it to 1 (not 0) when done.
        // The dialogue bubble / ResolveChoice path may have left it at 0.
        if (Time.timeScale == 0f)
        {
            bool pauseOpen = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
            bool gameOver = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOver;
            if (!pauseOpen && !gameOver) Time.timeScale = 1f;
        }

        bool done = false;
        cutscene.Play(() => done = true);

        // Wait until the cutscene finishes (it pauses Time.timeScale internally).
        while (!done) yield return null;

        // Clean up the transient cutscene GameObject.
        Destroy(go);
    }

    /// <summary>Resets all companion progression flags for a new game session.</summary>
    public void ResetForNewGame()
    {
        AcceptedStage1 = false;
        AcceptedStage2 = false;
        AcceptedStage3 = false;
        AcceptedStage4 = false;
        _stage4Armed = false;
        _stage4InProgress = false;
        _stage4QuestionIndex = 0;
        _stage4YesCount = 0;
        _shopSuppliesCollected = 0;
        _siegeStarted = false;
        _siegeCompleted = false;
        _siegeRestartPending = false;
        _spawnedFlag = false;
        CompanionInstance = null;
        CompanionAI = null;
        DialogueTrigger = null;
        _subscribed = false;

        CompanionBossSkipHook.ResetStaticState();
    }
}
