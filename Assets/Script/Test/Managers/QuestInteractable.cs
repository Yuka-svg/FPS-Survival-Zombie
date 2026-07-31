using UnityEngine;
using cowsins;

/// <summary>
/// Custom Interactable that completes a StoryManager quest when the player
/// interacts with it (press E). Inherits from cowsins.Interactable so the
/// engine's InteractManager detects it via the "Interactable" layer and shows
/// the interaction prompt.
///
/// Used for interaction-driven quests like Chapter 3's loot crate (Q6) and
/// generator (Q7). Optional features:
/// - Cutscene played before quest completion (via QuestTrigger.cutscene).
/// - Prefab spawns (mini-wave) when interacted — e.g. zombies burst out when
///   the generator starts.
///
/// The GameObject MUST be on the "Interactable" layer (layer 9) and have a
/// trigger Collider so InteractManager can detect it.
/// </summary>
public class QuestInteractable : Interactable
{
    [Header("Quest")]
    [Tooltip("QuestTrigger to complete when interacted. If null, completes the active quest directly.")]
    public QuestTrigger questTrigger;

    [Header("Optional Prefab Spawns")]
    [Tooltip("Prefabs to spawn when interacted (e.g. mini-wave zombies). Leave empty for none.")]
    public GameObject[] spawnPrefabs;

    [Tooltip("Offset from this transform's position where prefabs spawn.")]
    public Vector3 spawnOffset = Vector3.zero;

    [Tooltip("Random spread radius for spawned prefabs (0 = exact position).")]
    public float spawnSpread = 2f;

    [Tooltip("Delay (seconds) after interaction before prefabs spawn. 0 = instant.")]
    public float spawnDelay = 0f;

    [Tooltip("If true, destroy this interactable after use (e.g. loot crate disappears).")]
    public bool destroyAfterUse = false;

    [Tooltip("If true, disable the collider after use so the prompt disappears.")]
    public bool disableColliderAfterUse = true;

    private bool _used;

    public static readonly System.Collections.Generic.List<QuestInteractable> ActiveInteractables = new System.Collections.Generic.List<QuestInteractable>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActiveInteractables.Clear();
    }

    private void OnEnable()
    {
        if (!ActiveInteractables.Contains(this))
            ActiveInteractables.Add(this);
    }

    private void OnDisable()
    {
        ActiveInteractables.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveInteractables.Remove(this);
    }

    /// <summary>
    /// Called by InteractManager when the player presses the interact key.
    /// Completes the quest, optionally spawns prefabs, and cleans up.
    /// 
    /// Gated: if questTrigger.targetQuest is set, the interaction is refused
    /// unless that quest is the StoryManager's active quest. This enforces
    /// linear quest progression — the player cannot interact with a future
    /// quest's objective before completing the prior quest.
    /// </summary>
    public override void Interact(Transform player)
    {
        if (_used) return;

        if (!IsTargetQuestActive())
        {
            Debug.Log($"[QuestInteractable] {name}: target quest not active — interaction blocked (linear progression).");
            return;
        }

        _used = true;

        // Fire base Interact (UnityEvents, alreadyInteracted flag, etc.)
        base.Interact(player);

        // Disable collider so the interaction prompt disappears immediately.
        if (disableColliderAfterUse)
        {
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }

        // Spawn prefabs (mini-wave) with optional delay.
        if (spawnPrefabs != null && spawnPrefabs.Length > 0)
        {
            if (spawnDelay > 0f)
                StartCoroutine(DelayedSpawn(spawnDelay));
            else
                SpawnAll();
        }

        // Complete the quest (handles cutscene + quest advancement).
        if (questTrigger != null)
        {
            questTrigger.Complete();
        }
        else
        {
            StoryManager.Instance?.CompleteActiveQuest();
        }

        // Optional: destroy the object after use. If there's a delayed spawn
        // pending, the DelayedSpawn coroutine handles destruction after spawning.
        if (destroyAfterUse && spawnDelay <= 0f)
        {
            Destroy(gameObject, 1f);
        }
    }

    private System.Collections.IEnumerator DelayedSpawn(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnAll();
        if (destroyAfterUse)
            Destroy(gameObject);
    }

    private void SpawnAll()
    {
        var container = GetRuntimeContainer();
        for (int i = 0; i < spawnPrefabs.Length; i++)
        {
            var prefab = spawnPrefabs[i];
            if (prefab == null) continue;

            Vector3 spread = spawnSpread > 0f
                ? new Vector3(
                    Random.Range(-spawnSpread, spawnSpread),
                    0f,
                    Random.Range(-spawnSpread, spawnSpread))
                : Vector3.zero;
            Vector3 pos = transform.position + spawnOffset + spread;
            var go = Instantiate(prefab, pos, Quaternion.identity, container);
            go.SetActive(true);
            Debug.Log($"[QuestInteractable] Spawned {go.name} at {pos}.");
        }
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

    private static Transform _runtimeContainer;
    private Transform GetRuntimeContainer()
    {
        if (_runtimeContainer == null)
        {
            var go = new GameObject("QuestInteractableSpawns");
            _runtimeContainer = go.transform;
        }
        return _runtimeContainer;
    }
}
