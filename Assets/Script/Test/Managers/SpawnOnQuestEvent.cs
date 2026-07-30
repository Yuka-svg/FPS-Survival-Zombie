using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns GameObjects (zombies, items, effects) when a specific quest is
/// completed or when the active quest completes. Used for story-driven spawns
/// like "after picking up the pistol, a zombie rushes out from behind the tent"
/// (Chapter 1) or "after activating the generator, waves begin" (Chapter 3).
///
/// The spawner can place objects at the host transform's position or at explicit
/// spawn points. Spawned zombies are parented under a runtime container to keep
/// the hierarchy clean.
/// </summary>
public class SpawnOnQuestEvent : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("Quest whose completion triggers the spawn. If null, fires on any quest completion.")]
    public QuestData onQuestComplete;

    [Tooltip("If true, also fires when this quest becomes active (useful for spawning at quest start).")]
    public bool fireOnQuestActive = false;

    [Header("Spawn")]
    [Tooltip("Prefabs to spawn (zombies, items, effects).")]
    public GameObject[] prefabs;

    [Tooltip("Explicit spawn points. If empty, uses this transform's position for all prefabs.")]
    public Transform[] spawnPoints;

    [Tooltip("If true, the spawned objects are children of this transform. Otherwise they go to a runtime container.")]
    public bool parentToThis = false;

    [Tooltip("Delay (seconds) after the quest event before spawning.")]
    public float delay = 0f;

    private Transform _runtimeContainer;

    private bool _subscribed;
    private bool _fired;

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        // Fallback: if OnEnable ran before StoryManager.Awake (which sets
        // Instance), the subscription was silently skipped. Start is
        // guaranteed to run after all Awake calls, so Instance is always set.
        Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed || StoryManager.Instance == null) return;
        StoryManager.Instance.OnQuestCompleted += HandleQuestCompleted;
        if (fireOnQuestActive)
            StoryManager.Instance.OnActiveQuestChanged += HandleQuestActive;
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
            if (fireOnQuestActive)
                StoryManager.Instance.OnActiveQuestChanged -= HandleQuestActive;
        }
        _subscribed = false;
        _fired = false;
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        if (onQuestComplete != null && quest != onQuestComplete)
        {
            Debug.Log($"[SpawnOnQuestEvent] {name}: quest '{quest?.title}' != '{onQuestComplete.title}'. Skipped.");
            return;
        }
        Debug.Log($"[SpawnOnQuestEvent] {name}: quest '{quest?.title}' matched. Spawning {prefabs?.Length ?? 0} prefab(s).");
        DoSpawn();
    }

    private void HandleQuestActive(QuestData oldQuest, QuestData newQuest)
    {
        if (onQuestComplete != null && newQuest != onQuestComplete) return;
        Debug.Log($"[SpawnOnQuestEvent] {name}: quest '{newQuest?.title}' became active. Spawning {prefabs?.Length ?? 0} prefab(s).");
        DoSpawn();
    }

    private void DoSpawn()
    {
        if (_fired) return;
        _fired = true;
        if (delay > 0f)
            StartCoroutine(DelayedSpawn(delay));
        else
            Spawn();
    }

    private System.Collections.IEnumerator DelayedSpawn(float d)
    {
        yield return new WaitForSeconds(d);
        Spawn();
    }

    private void Spawn()
    {
        if (prefabs == null || prefabs.Length == 0) return;

        Transform parent = parentToThis ? transform : GetContainer();

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null) continue;
            Vector3 pos = transform.position;
            if (spawnPoints != null && spawnPoints.Length > 0)
                pos = spawnPoints[i % spawnPoints.Length].position;

            var go = Instantiate(prefabs[i], pos, Quaternion.identity, parent);
            go.SetActive(true);
            // Validate NavMesh for enemy spawns.
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null && NavMesh.SamplePosition(pos, out var navHit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
                if (!agent.isOnNavMesh)
                    agent.enabled = false;
            }
            Debug.Log($"[SpawnOnQuestEvent] Spawned {go.name} at {pos}.");
        }
    }

    private Transform GetContainer()
    {
        if (_runtimeContainer == null)
        {
            var go = new GameObject("QuestSpawns");
            _runtimeContainer = go.transform;
        }
        return _runtimeContainer;
    }
}
