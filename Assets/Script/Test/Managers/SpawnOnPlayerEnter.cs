using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Spawns a prefab (e.g. a Boomer boss) the first time the player enters the
/// trigger volume. Used for story-driven enemy appearances like "when the player
/// enters Building 2, a Boomer bursts out".
///
/// Simpler than SpawnOnQuestEvent: it is purely proximity-driven and does not
/// depend on the quest state. The spawned object is parented under a runtime
/// container to keep the hierarchy clean.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SpawnOnPlayerEnter : MonoBehaviour
{
    [Header("Spawn")]
    [Tooltip("Prefab to spawn when the player enters (e.g. Boomer).")]
    public GameObject prefab;

    [Tooltip("Offset from this transform's position where the prefab spawns.")]
    public Vector3 spawnOffset = Vector3.zero;

    [Tooltip("If true, only spawns once. Usually true.")]
    public bool oneShot = true;

    [Tooltip("Optional cutscene to play before spawning. The prefab spawns after the cutscene ends.")]
    public CutscenePlayer cutscene;

    [Tooltip("Delay (seconds) after cutscene ends before the prefab spawns. 0 = instant.")]
    public float delayAfterCutscene = 0.5f;

    [Header("Chapter Guard")]
    [Tooltip("Chapter number this spawner belongs to. If the player has already " +
             "completed this chapter (CurrentChapter > chapter), the spawner will " +
             "NOT fire even if the player enters the trigger. 0 = no guard (always fires).")]
    public int chapter = 0;

    private bool _fired;
    private Transform _runtimeContainer;

    private void OnDisable()
    {
        _fired = false;
    }

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_fired && oneShot) return;

        // Don't spawn if this chapter has already been completed (player is
        // re-exploring a finished area — no new enemies should appear).
        if (chapter > 0 && StoryManager.Instance != null
            && StoryManager.Instance.CurrentChapter > chapter)
        {
            Debug.Log($"[SpawnOnPlayerEnter] {name}: Ch{chapter} already completed — skipping spawn.");
            return;
        }

        _fired = true;

        if (cutscene != null)
        {
            cutscene.Play(() =>
            {
                if (delayAfterCutscene > 0f)
                    StartCoroutine(DelayedSpawn(delayAfterCutscene));
                else
                    Spawn();
            });
        }
        else
        {
            Spawn();
        }
    }

    private System.Collections.IEnumerator DelayedSpawn(float delay)
    {
        yield return new WaitForSeconds(delay);
        Spawn();
    }

    private void Spawn()
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[SpawnOnPlayerEnter] {name}: no prefab assigned.");
            return;
        }

        Vector3 pos = transform.position + spawnOffset;
        var go = Instantiate(prefab, pos, Quaternion.identity, GetContainer());
        go.SetActive(true);
        // Validate NavMesh for enemy spawns — sample nearest valid position.
        var agent = go.GetComponent<NavMeshAgent>();
        if (agent != null && NavMesh.SamplePosition(pos, out var navHit, 5f, NavMesh.AllAreas))
        {
            agent.Warp(navHit.position);
            if (!agent.isOnNavMesh)
                agent.enabled = false;
        }
        Debug.Log($"[SpawnOnPlayerEnter] Spawned {go.name} at {pos}.");
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
