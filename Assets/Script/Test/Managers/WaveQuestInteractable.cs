using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using cowsins;

/// <summary>
/// Variant of QuestInteractable that spawns enemies in sequential waves with
/// increasing difficulty. The quest only completes after every wave is cleared.
///
/// Used for Chapter 3 Quest 7 (generator): the player starts the generator,
/// then must survive 3 waves of zombies (wave 3 includes a Boomer boss) before
/// the quest completes and the chapter advances.
///
/// Wave clearance is detected via ScoreManager.kills delta — when the kill count
/// increases by the wave's required kills, the wave is considered cleared and
/// the next wave spawns after a short breather.
///
/// The GameObject MUST be on the "Interactable" layer (layer 9) and have a
/// trigger Collider so InteractManager can detect it.
/// </summary>
public class WaveQuestInteractable : Interactable
{
    [System.Serializable]
    public class Wave
    {
        [Tooltip("Prefabs to spawn for this wave (e.g. 5 zombie prefabs = 5 enemies).")]
        public GameObject[] prefabs;

        [Tooltip("Number of kills required to clear this wave. Should match the total prefab count (unless some enemies are killed by other means).")]
        public int killsRequired;

        [Tooltip("Short banner title shown when this wave starts (e.g. 'WAVE 1').")]
        public string waveTitle;

        [Tooltip("Banner subtitle shown when this wave starts.")]
        public string waveSubtitle;

        [Tooltip("Delay (seconds) after this wave is cleared before the next wave spawns. Gives the player a breather.")]
        public float breatherDelay = 3f;

        [Tooltip("If true, this wave only counts as cleared when a boss (ISpecialEnemy) " +
                 "spawned in this wave is dead — the kill count is ignored. " +
                 "Use for boss waves where many regular zombies spawn alongside the boss, " +
                 "so the player only needs to kill the boss, not all the trash mobs.")]
        public bool requireBossKill = false;
    }

    [Header("Quest")]
    [Tooltip("QuestTrigger to complete when all waves are cleared.")]
    public QuestTrigger questTrigger;

    [Header("Waves")]
    [Tooltip("Waves in order. Wave 1 spawns first, then 2, etc. Quest completes after the last wave is cleared.")]
    public Wave[] waves;

    [Header("Spawn Settings")]
    [Tooltip("Offset from this transform's position where enemies spawn.")]
    public Vector3 spawnOffset = Vector3.zero;

    [Tooltip("Random spread radius for spawned enemies (0 = exact position).")]
    public float spawnSpread = 4f;

    [Tooltip("Delay after interaction before wave 1 spawns (lets the cutscene finish).")]
    public float initialDelay = 1f;

    [Header("Optional Cutscene")]
    [Tooltip("Cutscene played when the player interacts (before wave 1). Optional.")]
    public CutscenePlayer introCutscene;

    [Tooltip("Banner shown between waves. If null, a temporary one is created at runtime.")]
    public CutscenePlayer waveBanner;

    [Header("Boundary Lock")]
    [Tooltip("Optional ChapterBoundary to lock while waves are active, preventing the player from leaving the chapter area. Unlocked when all waves are cleared.")]
    public ChapterBoundary lockBoundary;

    [Tooltip("If true, also disable the chapter's continuous spawners while waves are active so only the wave-spawned enemies count toward the kill goal.")]
    public bool suppressChapterSpawners = true;

    [Header("Second Interaction (Post-Wave Activation)")]
    [Tooltip("If true, after all waves are cleared the quest does NOT auto-complete. " +
             "Instead the collider is re-enabled and the player must interact a second time " +
             "to complete the quest (e.g. activate the bomb after defeating the Tank boss).")]
    public bool requireSecondInteraction = false;

    [Tooltip("Interaction text shown for the second interaction (after waves are cleared).")]
    public string secondInteractText = "Kích hoạt quả bom";

    [Tooltip("Optional cutscene played when the player performs the second interaction (quest completion).")]
    public CutscenePlayer completionCutscene;

    [Header("Cleanup")]
    [Tooltip("If true, disable the collider after use so the prompt disappears.")]
    public bool disableColliderAfterUse = true;

    private bool _used;
    private bool _wavesCleared; // True after all waves are cleared (phase 2 ready).
    private Coroutine _waveRoutine;
    private string _originalInteractText;

    // All enemies (regular zombies + bosses) spawned by this WaveQuestInteractable.
    // Tracked so we can destroy them when the player dies mid-wave — otherwise
    // wave-spawned bosses (e.g. the Tank in Q11) are not registered with either
    // AIDirector or SpecialEnemyDirector and survive the respawn, causing a
    // duplicate boss to appear when the player re-triggers the quest.
    private readonly List<GameObject> _spawnedEnemies = new List<GameObject>();

    /// <summary>
    /// Called by InteractManager when the player presses the interact key.
    /// Starts the wave sequence. The quest completes after all waves are cleared.
    ///
    /// Gated: if questTrigger.targetQuest is set, the interaction is refused
    /// unless that quest is the StoryManager's active quest. This enforces
    /// linear quest progression — the player cannot trigger a future quest's
    /// wave fight (and get locked into the chapter boundary) before completing
    /// the prior quest.
    /// </summary>
    public override void Interact(Transform player)
    {
        if (_used) return;

        if (!IsTargetQuestActive())
        {
            Debug.Log($"[WaveQuestInteractable] {name}: target quest not active — interaction blocked (linear progression).");
            return;
        }

        // Phase 2: waves already cleared — second interaction completes the quest.
        if (_wavesCleared)
        {
            _used = true;
            base.Interact(player);

            if (disableColliderAfterUse)
            {
                var col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }

            CompleteQuest();
            return;
        }

        // Phase 1: start the wave sequence.
        _used = true;
        if (string.IsNullOrEmpty(_originalInteractText))
            _originalInteractText = interactText;

        base.Interact(player);

        if (disableColliderAfterUse)
        {
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        _waveRoutine = StartCoroutine(RunWaveSequence());
    }

    private void OnEnable()
    {
        // Cache the original interaction text so we can restore it after phase 2
        // or after a death reset.
        if (string.IsNullOrEmpty(_originalInteractText))
            _originalInteractText = interactText;

        // Subscribe to player death so we can abort the wave sequence and reset.
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.OnPlayerDied += HandlePlayerDeath;

        // Defensive: if the chapter advances during waves (debug skip, etc.),
        // abort the wave sequence and reset.
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
    }

    private void OnDisable()
    {
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.OnPlayerDied -= HandlePlayerDeath;

        if (StoryManager.Instance != null)
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
    }

    private void HandleChapterChanged(int oldChapter, int newChapter)
    {
        // If we're mid-wave and the chapter changes (edge case), abort cleanly.
        if (_used || _wavesCleared || _waveRoutine != null)
        {
            HandlePlayerDeath();
        }
    }

    /// <summary>
    /// Called when the player dies during the wave sequence. Stops the sequence,
    /// unlocks the boundary, and resets so the player can try again after respawning.
    /// </summary>
    private void HandlePlayerDeath()
    {
        if (!_used && !_wavesCleared) return;

        Debug.Log("[WaveQuestInteractable] Player died during waves — aborting and resetting.");

        if (_waveRoutine != null)
        {
            StopCoroutine(_waveRoutine);
            _waveRoutine = null;
        }

        // Destroy every enemy this quest spawned so they don't persist after
        // respawn (most importantly wave-spawned bosses like the Tank, which
        // are not registered with AIDirector or SpecialEnemyDirector and would
        // otherwise survive the respawn — causing a duplicate boss when the
        // player re-triggers the quest).
        CleanupSpawnedEnemies();

        // Unlock the boundary so the player can move freely after respawning.
        if (lockBoundary != null) lockBoundary.UnlockExternal();

        // Re-enable the chapter spawners if they were suppressed.
        if (lockBoundary != null && suppressChapterSpawners)
            lockBoundary.SetSpawnersActive(true);

        // Re-enable the collider so the player can interact again.
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        // Restore the original interaction text in case it was changed for phase 2.
        interactText = _originalInteractText;

        _used = false;
        _wavesCleared = false;
    }

    /// <summary>
    /// Destroys every enemy spawned by this WaveQuestInteractable and clears
    /// the tracking list. Called on player death (mid-wave) to prevent
    /// wave-spawned bosses — which are not registered with AIDirector or
    /// SpecialEnemyDirector — from surviving the respawn and duplicating when
    /// the quest is re-triggered.
    /// </summary>
    private void CleanupSpawnedEnemies()
    {
        if (_spawnedEnemies.Count == 0) return;

        Debug.Log($"[WaveQuestInteractable] Cleaning up {_spawnedEnemies.Count} spawned enemy(ies).");
        for (int i = 0; i < _spawnedEnemies.Count; i++)
        {
            var go = _spawnedEnemies[i];
            if (go != null) Destroy(go);
        }
        _spawnedEnemies.Clear();
    }

    private IEnumerator RunWaveSequence()
    {
        // Lock the player inside the chapter area for the duration of the waves.
        if (lockBoundary != null)
        {
            lockBoundary.LockExternal();
            if (suppressChapterSpawners) lockBoundary.SetSpawnersActive(false);
            Debug.Log("[WaveQuestInteractable] Boundary locked — player cannot leave until waves are cleared.");
        }

        // Optional intro cutscene before wave 1.
        if (introCutscene != null)
        {
            bool done = false;
            introCutscene.Play(() => done = true);
            while (!done) yield return null;
        }

        if (initialDelay > 0f)
            yield return new WaitForSecondsRealtime(initialDelay);

        for (int i = 0; i < waves.Length; i++)
        {
            var wave = waves[i];

            // Show wave banner. Use WaitForSecondsRealtime so the wait is not
            // affected by the banner's Time.timeScale = 0.
            ShowBanner(wave.waveTitle, wave.waveSubtitle);
            yield return new WaitForSecondsRealtime(1f); // Let the banner fade in.

            // Spawn all prefabs for this wave. If the wave requires a boss
            // kill, SpawnWave returns the tracked boss (ISpecialEnemy) so we
            // can gate wave completion on its death, not on the kill count —
            // otherwise the player would have to kill all 40+ trash mobs.
            cowsins.ISpecialEnemy trackedBoss = null;
            SpawnWave(wave, wave.requireBossKill, out trackedBoss);

            // Wait until the wave is cleared. If requireBossKill is true,
            // we only need the boss to be dead (kill count is ignored).
            // Otherwise, wait for the required kill count.
            int startKills = ScoreManager.Instance != null ? ScoreManager.Instance.kills : 0;
            int target = startKills + wave.killsRequired;
            while (true)
            {
                if (wave.requireBossKill)
                {
                    // Boss-kill mode: only the boss needs to die.
                    if (trackedBoss == null || trackedBoss.IsDead) break;
                }
                else
                {
                    // Kill-count mode: reach the required kills.
                    int current = ScoreManager.Instance != null ? ScoreManager.Instance.kills : 0;
                    if (current >= target) break;
                }
                yield return null;
            }

            Debug.Log($"[WaveQuestInteractable] Wave {i + 1} cleared" +
                      (wave.requireBossKill ? " (boss dead)" : $" ({wave.killsRequired} kills)") + ".");

            // Purge null entries from the spawned-enemies list (enemies that
            // were killed during the wave and whose references became stale).
            _spawnedEnemies.RemoveAll(go => go == null);

            // Breather between waves (not after the last wave).
            if (i < waves.Length - 1 && wave.breatherDelay > 0f)
            {
                ShowBanner("WAVE CLEARED", $"Get ready for wave {i + 2}...");
                yield return new WaitForSecondsRealtime(wave.breatherDelay);
            }
        }

        // All waves cleared — release the boundary lock so the player can move freely.
        if (lockBoundary != null)
        {
            lockBoundary.UnlockExternal();
            Debug.Log("[WaveQuestInteractable] Boundary unlocked — player can leave.");
        }

        _waveRoutine = null;

        // All waves cleared successfully — destroy any remaining spawned
        // enemies (in requireBossKill mode, regular zombies may still be alive)
        // before dropping references.
        CleanupSpawnedEnemies();

        if (requireSecondInteraction)
        {
            // Phase 2: re-enable the collider and wait for the player to interact
            // again to complete the quest (e.g. activate the bomb after defeating Tank).
            _wavesCleared = true;
            _used = false; // Allow the second interaction.

            // Restore chapter spawners so the area isn't dead quiet during phase 2.
            if (lockBoundary != null && suppressChapterSpawners)
                lockBoundary.SetSpawnersActive(true);

            // Show a banner telling the player to activate the bomb.
            ShowBanner("TANK ĐÃ BỊ TIÊU DIỆT", "Kích hoạt lại quả bom để hoàn tất!");

            // Swap the interaction text and re-enable the collider.
            interactText = secondInteractText;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            Debug.Log("[WaveQuestInteractable] Waves cleared — waiting for second interaction to complete quest.");
        }
        else
        {
            // No second interaction required — complete the quest immediately.
            CompleteQuest();
        }
    }

    /// <summary>
    /// Completes the quest via the QuestTrigger (handles cutscene + advancement),
    /// or falls back to StoryManager.CompleteActiveQuest if no trigger is assigned.
    /// </summary>
    private void CompleteQuest()
    {
        Debug.Log("[WaveQuestInteractable] Completing quest.");

        if (completionCutscene != null)
        {
            StartCoroutine(PlayCompletionCutsceneThenComplete());
        }
        else if (questTrigger != null)
        {
            questTrigger.Complete();
        }
        else
        {
            StoryManager.Instance?.CompleteActiveQuest();
        }
    }

    private IEnumerator PlayCompletionCutsceneThenComplete()
    {
        bool done = false;
        completionCutscene.Play(() => done = true);
        while (!done) yield return null;

        if (questTrigger != null)
            questTrigger.Complete();
        else
            StoryManager.Instance?.CompleteActiveQuest();
    }

    /// <summary>
    /// Spawns all prefabs for a wave. If <paramref name="trackBoss"/> is true,
    /// the first spawned ISpecialEnemy (boss) is returned via
    /// <paramref name="trackedBoss"/> so the caller can gate wave completion on
    /// the boss actually dying.
    /// </summary>
    private void SpawnWave(Wave wave, bool trackBoss, out cowsins.ISpecialEnemy trackedBoss)
    {
        trackedBoss = null;
        if (wave.prefabs == null) return;
        var container = GetRuntimeContainer();
        for (int i = 0; i < wave.prefabs.Length; i++)
        {
            var prefab = wave.prefabs[i];
            if (prefab == null) continue;

            Vector3 spread = spawnSpread > 0f
                ? new Vector3(
                    Random.Range(-spawnSpread, spawnSpread),
                    0f,
                    Random.Range(-spawnSpread, spawnSpread))
                : Vector3.zero;
            Vector3 pos = transform.position + spawnOffset + spread;

            // Validate the spawn position on the NavMesh. Without this, enemies
            // (especially the Tank boss) can spawn inside walls or furniture and
            // get permanently stuck. Try a few nearby positions if the exact
            // spot is not on the NavMesh.
            pos = FindValidNavMeshPosition(pos, 8f);

            var go = Instantiate(prefab, pos, Quaternion.identity, container);
            go.SetActive(true);
            _spawnedEnemies.Add(go);
            Debug.Log($"[WaveQuestInteractable] Spawned {go.name} at {pos}.");

            // Track the first boss (ISpecialEnemy) spawned in this wave if
            // the wave requires a boss kill to clear.
            if (trackBoss && trackedBoss == null)
            {
                trackedBoss = go.GetComponent<cowsins.ISpecialEnemy>();
                if (trackedBoss != null)
                {
                    Debug.Log($"[WaveQuestInteractable] Tracking boss {go.name} for wave completion (requireBossKill).");

                    // Companion skip hook: if the player accepted the companion's
                    // stage-2 dialogue, the boss spawns with reduced HP.
                    if (CompanionBossSkipHook.PendingReducedBossHP > 0)
                    {
                        var tankAI = go.GetComponent<TankBossAI>();
                        if (tankAI != null)
                        {
                            int newHP = CompanionBossSkipHook.PendingReducedBossHP;
                            tankAI.maxHealth = newHP;
                            tankAI.currentHealth = newHP; // TankBossAI.Start() also sets this = maxHealth.
                            Debug.Log($"[WaveQuestInteractable] Companion skip: Tank boss HP reduced to {newHP}.");
                        }
                        CompanionBossSkipHook.PendingReducedBossHP = 0; // Consume.
                    }
                }
            }
        }
    }

    /// <summary>
    /// Finds a valid NavMesh position near the requested spawn point. Tries
    /// the exact position first, then samples outward in a spiral. Falls back
    /// to the original position if no NavMesh point is found (the enemy's
    /// own stuck-recovery logic will handle it at runtime).
    /// </summary>
    private Vector3 FindValidNavMeshPosition(Vector3 requested, float searchRadius)
    {
        // Try the exact position first with a small tolerance.
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(requested, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;

        // Spiral outward: try several positions at increasing distances.
        for (float r = 2f; r <= searchRadius; r += 2f)
        {
            for (int a = 0; a < 8; a++)
            {
                float angle = a * (Mathf.PI * 2f / 8f);
                Vector3 candidate = requested + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                    return hit.position;
            }
        }

        // Fallback: return the original position. The enemy's own stuck-recovery
        // logic (if any) will handle it at runtime.
        Debug.LogWarning($"[WaveQuestInteractable] No NavMesh position found near {requested} — using fallback.");
        return requested;
    }

    private void ShowBanner(string title, string subtitle)
    {
        if (waveBanner != null)
        {
            waveBanner.title = title;
            waveBanner.body = subtitle;
            waveBanner.Play(null);
            return;
        }

        // Fallback: log if no banner assigned.
        Debug.Log($"[WaveQuestInteractable] {title}: {subtitle}");
    }

    private static Transform _runtimeContainer;
    private Transform GetRuntimeContainer()
    {
        if (_runtimeContainer == null)
        {
            var go = new GameObject("WaveQuestSpawns");
            _runtimeContainer = go.transform;
        }
        return _runtimeContainer;
    }

    /// <summary>
    /// Returns true if the target quest is the active quest (or if no specific
    /// quest is assigned, in which case there is no gate). Used to enforce
    /// linear quest progression.
    /// </summary>
    private bool IsTargetQuestActive()
    {
        var sm = StoryManager.Instance;
        if (sm == null) return false;
        if (questTrigger == null || questTrigger.targetQuest == null) return true;
        return sm.ActiveQuest == questTrigger.targetQuest;
    }
}
