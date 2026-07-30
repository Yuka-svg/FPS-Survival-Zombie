using UnityEngine;
using UnityEngine.AI;

public class Spawm : MonoBehaviour
{
    [Header("Zombies")]
    public GameObject[] zombiePrefabs;

    [Header("Spawn Settings")]
    public int maxZombie = 30;
    public float spawnInterval = 3f;

    [Header("Director Override")]
    [Tooltip("If true, this spawner ignores the AIDirector's dynamic spawnInterval and always uses the value set above. Use for chapter spawners where you want tight control over pacing.")]
    public bool ignoreDirectorInterval = false;

    [Tooltip("Multiplier applied to the AIDirector's spawnInterval when ignoreDirectorInterval is false. <1 = faster spawns, >1 = slower. 1 = use director value as-is.")]
    public float directorIntervalMultiplier = 1f;

    [Header("Per-Spawner Cap")]
    [Tooltip("If true, maxZombie counts only zombies spawned by THIS spawner (tracked locally), not the global AIDirector count. Prevents one chapter's spawner from blocking another's.")]
    public bool useLocalCap = true;

    [Header("Roaming")]
    [Tooltip("If true, spawned zombies get a random wander destination within spawnAreaSize so they patrol the area instead of standing still.")]
    public bool enableRoaming = true;

    [Tooltip("How often (seconds) to assign a new wander destination to each spawned zombie.")]
    public float wanderInterval = 5f;

    [Tooltip("Range from the spawner center within which zombies wander.")]
    public float wanderRadius = 30f;

    [Header("Spawn Area")]
    public Vector3 spawnAreaSize =
        new Vector3(50f, 0f, 50f);

    [Header("Player")]
    public Transform player;

    [Header("Spawn Safety")]
    [Tooltip("Zombies will not spawn closer than this XZ distance to the player (prevents spawn-on-top kills).")]
    public float minDistanceFromPlayer = 8f;
    [Tooltip("How far to search the NavMesh for a valid spawn position if the random point is off the NavMesh. 0 = skip NavMesh validation (legacy behavior).")]
    public float navMeshSpawnSearchRadius = 8f;

    [Header("Object Pool")]
    public int poolSize = 60;
    private System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.List<GameObject>> poolDictionary = new System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.List<GameObject>>();

    // All pooled zombies are parented under this single container so the runtime
    // hierarchy stays tidy instead of flooding the scene root.
    private Transform zombieContainer;

    private float timer;

    // Locally-tracked zombies spawned by THIS spawner (for per-spawner cap).
    private readonly System.Collections.Generic.List<ZombieAI> _localActiveZombies =
        new System.Collections.Generic.List<ZombieAI>();

    // Roaming: maps each zombie to its next wander time.
    private readonly System.Collections.Generic.Dictionary<ZombieAI, float> _wanderTimers =
        new System.Collections.Generic.Dictionary<ZombieAI, float>();

    private NavMeshPath _cachedPath;

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // Safety net: if the tagged Player is static (e.g. root of the
        // Cowsins player hierarchy), try to find the moving child instead.
        if (player != null)
        {
            var pm = player.GetComponentInChildren<cowsins.PlayerMovement>();
            if (pm != null)
                player = pm.transform;
        }

        if (zombiePrefabs == null || zombiePrefabs.Length == 0)
            return;

        _cachedPath = new NavMeshPath();

        if (zombieContainer == null)
            zombieContainer = new GameObject("Zombies").transform;

        int perPrefabSize = Mathf.Max(1, poolSize / zombiePrefabs.Length);

        foreach (GameObject prefab in zombiePrefabs)
        {
            if (prefab == null) continue;

            poolDictionary[prefab] = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < perPrefabSize; i++)
            {
                GameObject zombie = Instantiate(prefab, transform.position, Quaternion.identity, zombieContainer);
                zombie.SetActive(false);
                poolDictionary[prefab].Add(zombie);
            }
        }
    }

    private GameObject GetPooledZombie(GameObject prefab)
    {
        if (poolDictionary.TryGetValue(prefab, out var poolList))
        {
            foreach (GameObject zombie in poolList)
            {
                if (!zombie.activeInHierarchy)
                    return zombie;
            }

            GameObject newZombie = Instantiate(prefab, transform.position, Quaternion.identity, zombieContainer);
            newZombie.SetActive(false);
            poolList.Add(newZombie);
            return newZombie;
        }
        return null;
    }

    /// <summary>
    /// Called when the spawner is disabled (e.g. player enters a save room
    /// or leaves the chapter area). Only pauses spawning — existing zombies
    /// stay in the world so the area doesn't feel empty when the player
    /// returns.
    /// </summary>
    private void OnDisable()
    {
    }

    /// <summary>
    /// Clean up all spawned zombies when the spawner GameObject is destroyed
    /// (scene unload, dynamic removal) to prevent orphaned zombies.
    /// </summary>
    private void OnDestroy()
    {
        DespawnAllLocalZombies();
    }

    /// <summary>
    /// Returns every zombie spawned by this spawner to the pool and clears
    /// local tracking. Safe to call multiple times. Deactivates both tracked
    /// active zombies AND all pooled instances (in case tracking was lost).
    /// </summary>
    private void DespawnAllLocalZombies()
    {
        // Despawn tracked active zombies.
        foreach (var ai in _localActiveZombies)
        {
            if (ai != null && ai.gameObject != null && ai.gameObject.activeInHierarchy)
            {
                ai.gameObject.SetActive(false);
            }
        }
        _localActiveZombies.Clear();
        _wanderTimers.Clear();
        timer = 0f;

        // Also sweep the entire pool and deactivate any active instances.
        // This catches zombies that were somehow lost from local tracking
        // (e.g. re-parented, re-activated by another system, etc.).
        foreach (var kvp in poolDictionary)
        {
            if (kvp.Value == null) continue;
            foreach (var zombie in kvp.Value)
            {
                if (zombie != null && zombie.activeInHierarchy)
                {
                    zombie.SetActive(false);
                }
            }
        }
    }

    private void Update()
    {
        UpdateDirectorSettings();

        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            timer = 0f;

            SpawnWave();
        }

        CheckCamperPunishment();
    }

    private void UpdateDirectorSettings()
    {
        if (AIDirector.Instance == null)
            return;

        // If the designer wants full control, don't touch spawnInterval.
        if (ignoreDirectorInterval)
            return;

        float baseInterval;
        switch (AIDirector.Instance.currentState)
        {
            case AIDirector.DirectorState.Calm:
                baseInterval = 5f;
                break;

            case AIDirector.DirectorState.BuildUp:
                baseInterval = 3f;
                break;

            case AIDirector.DirectorState.Attack:
                baseInterval = 1f;
                break;

            case AIDirector.DirectorState.Recovery:
                baseInterval = 8f;
                break;

            default:
                baseInterval = spawnInterval;
                break;
        }

        spawnInterval = baseInterval * directorIntervalMultiplier;
    }

    private void SpawnWave()
    {
        if (zombiePrefabs == null ||
            zombiePrefabs.Length == 0)
        {
            Debug.LogWarning(
                "Chưa gán Zombie Prefab!"
            );

            return;
        }

        // Clean up destroyed/null entries from our local tracking list.
        CleanupLocalList();

        int currentZombie = useLocalCap
            ? _localActiveZombies.Count
            : (AIDirector.Instance != null ? AIDirector.Instance.GetZombieCount() : 0);

        int cap = EffectiveMaxZombie();

        if (currentZombie >= cap)
            return;

        int spawnAmount = 1;

        if (!useLocalCap && AIDirector.Instance != null)
        {
            spawnAmount =
                AIDirector.Instance
                .GetRecommendedSpawnCount();
        }
        else if (useLocalCap)
        {
            // Spawn a small batch each tick to fill the area quickly.
            spawnAmount = Mathf.Min(3, cap - currentZombie);
        }

        for (int i = 0; i < spawnAmount; i++)
        {
            currentZombie = useLocalCap
                ? _localActiveZombies.Count
                : (AIDirector.Instance != null ? AIDirector.Instance.GetZombieCount() : currentZombie);

            if (currentZombie >= cap)
                break;

            SpawnZombie();
        }
    }

    private void SpawnZombie()
    {
        if (zombiePrefabs == null || zombiePrefabs.Length == 0)
            return;

        Vector3 randomPos =
            transform.position +
            new Vector3(
                Random.Range(
                    -spawnAreaSize.x / 2f,
                    spawnAreaSize.x / 2f
                ),
                0f,
                Random.Range(
                    -spawnAreaSize.z / 2f,
                    spawnAreaSize.z / 2f
                )
            );

        // Prevent zombies from materialising on top of the player.
        if (player != null && minDistanceFromPlayer > 0f)
        {
            Vector3 offset = randomPos - player.position;
            offset.y = 0f;
            if (offset.sqrMagnitude < minDistanceFromPlayer * minDistanceFromPlayer)
            {
                if (offset.sqrMagnitude < 0.0001f)
                    offset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                randomPos = player.position + offset.normalized * minDistanceFromPlayer;
                randomPos.y = transform.position.y;
            }
        }

        // Validate the spawn position on the NavMesh so zombies don't spawn
        // inside walls or off-mesh and get stuck immediately.
        randomPos = FindValidNavMeshPosition(randomPos, navMeshSpawnSearchRadius);

        int randomIndex = Random.Range(0, zombiePrefabs.Length);
        GameObject selectedPrefab = zombiePrefabs[randomIndex];

        if (selectedPrefab == null) return;

        GameObject zombie = GetPooledZombie(selectedPrefab);

        if (zombie != null)
        {
            zombie.transform.position = randomPos;
            zombie.transform.rotation = Quaternion.identity;
            zombie.SetActive(true);

            // Track locally for per-spawner cap.
            var ai = zombie.GetComponent<ZombieAI>();
            if (ai != null)
            {
                _localActiveZombies.Add(ai);
                if (enableRoaming)
                    _wanderTimers[ai] = Time.time + Random.Range(0f, wanderInterval);
            }
        }
    }

    /// <summary>
    /// Removes destroyed/null entries from the local zombie list so the
    /// per-spawner cap stays accurate.
    /// </summary>
    private void CleanupLocalList()
    {
        for (int i = _localActiveZombies.Count - 1; i >= 0; i--)
        {
            var z = _localActiveZombies[i];
            if (z == null || z.gameObject == null || !z.gameObject.activeInHierarchy)
            {
                _localActiveZombies.RemoveAt(i);
                _wanderTimers.Remove(z);
            }
        }
    }

    // NOTE: Roaming is handled by ZombieAI.Wander() with NavMesh-validated
    // positions. The legacy roaming fields (enableRoaming, wanderInterval,
    // wanderRadius) are kept for inspector backward compatibility but are
    // no longer used here.

    private void CheckCamperPunishment()
    {
        if (AIDirector.Instance == null)
            return;

        if (!AIDirector.Instance.ShouldPunishCamper())
            return;

        if (player == null)
            return;

        int currentCount = useLocalCap
            ? _localActiveZombies.Count
            : AIDirector.Instance.GetZombieCount();

        if (currentCount >= EffectiveMaxZombie())
            return;

        SpawnBehindPlayer();
    }

    private void SpawnBehindPlayer()
    {
        if (zombiePrefabs == null ||
            zombiePrefabs.Length == 0)
            return;

        Vector3 spawnPos =
            player.position -
            player.forward * 8f;

        spawnPos +=
            new Vector3(
                Random.Range(-3f, 3f),
                0f,
                Random.Range(-3f, 3f)
            );

        // Validate the spawn position on the NavMesh so the camper-punishment
        // zombie doesn't spawn inside a wall behind the player.
        spawnPos = FindValidNavMeshPosition(spawnPos, navMeshSpawnSearchRadius);

        int randomIndex = Random.Range(0, zombiePrefabs.Length);
        GameObject selectedPrefab = zombiePrefabs[randomIndex];

        if (selectedPrefab == null) return;

        GameObject zombie = GetPooledZombie(selectedPrefab);

        if (zombie != null)
        {
            zombie.transform.position = spawnPos;
            zombie.transform.rotation = Quaternion.identity;
            zombie.SetActive(true);

            // Track locally for per-spawner cap.
            var ai = zombie.GetComponent<ZombieAI>();
            if (ai != null)
            {
                _localActiveZombies.Add(ai);
                if (enableRoaming)
                    _wanderTimers[ai] = Time.time + Random.Range(0f, wanderInterval);
            }

            if (AIDirector.Instance != null)
            {
                AIDirector.Instance.camperTime = 0f;
            }

            Debug.Log(
                "[DIRECTOR] Camper Punishment Spawn!"
            );
        }
    }

    /// <summary>
    /// Finds a valid NavMesh position near the requested spawn point. Tries
    /// the exact position first, then samples outward in a spiral. Falls back
    /// to the original position if no NavMesh point is found (the zombie's own
    /// stuck-recovery logic will handle it at runtime). If searchRadius is 0,
    /// validation is skipped (legacy behavior).
    ///
    /// Y-HEIGHT FILTER: NavMesh.SamplePosition searches in a 3D sphere, so it
    /// can snap to underground NavMesh surfaces (sewers, basements, beach rocks
    /// at y=-2 to -1385). 31% of the NavMesh triangles are underground and
    /// disconnected from the main surface. Zombies spawning there can never
    /// reach the player and slide around on a disconnected island. We reject
    /// any NavMesh position more than 2m below the requested position's Y.
    /// </summary>
    private Vector3 FindValidNavMeshPosition(Vector3 requested, float searchRadius)
    {
        if (searchRadius <= 0f)
            return requested;

        // Player position for path-connectivity validation. If the player
        // isn't available yet, fall back to NavMesh-only validation.
        Vector3 playerNavMeshPos;
        bool hasPlayerNavMesh = false;
        if (player != null)
        {
            NavMeshHit playerHit;
            hasPlayerNavMesh = NavMesh.SamplePosition(player.position, out playerHit, 10f, NavMesh.AllAreas);
            playerNavMeshPos = hasPlayerNavMesh ? playerHit.position : Vector3.zero;
        }
        else
        {
            playerNavMeshPos = Vector3.zero;
        }

        // Maximum allowed Y difference — reject underground NavMesh positions.
        const float maxBelowY = 2f;
        float requestY = requested.y;

        // Try the exact position first with a small tolerance.
        NavMeshHit hit;
        if (NavMesh.SamplePosition(requested, out hit, 2f, NavMesh.AllAreas))
        {
            if (hit.position.y >= requestY - maxBelowY &&
                (!hasPlayerNavMesh || HasPathToPlayer(hit.position, playerNavMeshPos)))
                return hit.position;
        }

        // Spiral outward: try several positions at increasing distances.
        for (float r = 2f; r <= searchRadius; r += 2f)
        {
            for (int a = 0; a < 8; a++)
            {
                float angle = a * (Mathf.PI * 2f / 8f);
                Vector3 candidate = requested + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                if (NavMesh.SamplePosition(candidate, out hit, 2f, NavMesh.AllAreas))
                {
                    if (hit.position.y >= requestY - maxBelowY &&
                        (!hasPlayerNavMesh || HasPathToPlayer(hit.position, playerNavMeshPos)))
                        return hit.position;
                }
            }
        }

        // Fallback: return the original position. The zombie's own stuck-recovery
        // logic will handle it at runtime.
        Debug.LogWarning($"[Spawm] No NavMesh position with path to player found near {requested} — using fallback.");
        return requested;
    }

    /// <summary>
    /// Checks whether a complete NavMesh path exists from the given spawn
    /// position to the player's NavMesh position. Returns false if the path
    /// is partial (disconnected NavMesh island) or invalid.
    /// </summary>
    private bool HasPathToPlayer(Vector3 fromPos, Vector3 playerNavMeshPos)
    {
        if (_cachedPath == null) _cachedPath = new NavMeshPath();
        NavMesh.CalculatePath(fromPos, playerNavMeshPos, NavMesh.AllAreas, _cachedPath);
        return _cachedPath.status == NavMeshPathStatus.PathComplete;
    }

    public void FlushSpawner()
    {
        foreach (var z in _localActiveZombies)
        {
            if (z != null && z.gameObject != null)
            {
                z.gameObject.SetActive(false);
            }
        }
        _localActiveZombies.Clear();
        _wanderTimers.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        Gizmos.DrawWireCube(
            transform.position,
            spawnAreaSize
        );
    }

    // The active-zombie ceiling, ramped per wave. WaveManager-driven GetWaveLimit()
    // grows the horde as waves progress (baseZombieCount + wave*5), while maxZombie
    // stays the absolute hard cap (also the perf ceiling). Early waves stay
    // survivable instead of instantly piling to the hard cap.
    private int EffectiveMaxZombie()
    {
        return Mathf.Min(maxZombie, GetWaveLimit());
    }

    private int GetWaveLimit()
    {
        if (WaveManager.Instance == null)
            return maxZombie;

        int perWaveStep = GameModeManager.CurrentMode == GameMode.Endless
            ? WaveManager.Instance.endlessZombiesPerWave
            : 5;

        return
            WaveManager.Instance.baseZombieCount +
            ((WaveManager.Instance.currentWave - 1) * perWaveStep);
    }

}