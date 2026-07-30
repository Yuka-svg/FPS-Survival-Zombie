using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zombie siege event for the Chapter 3 follower recruitment arc.
///
/// After the player loots both shops (Stage 2), CompanionManager calls
/// StartSiege(callback). This component:
///   - Spawns `zombieCount` zombies at the configured spawn points (random
///     prefab from `zombiePrefabs`).
///   - Tracks kills via ScoreManager.kills delta.
///   - When the required kills are reached, stops spawning, cleans up any
///     remaining spawned zombies, and invokes the completion callback so
///     CompanionManager can arm Stage 3.
///
/// This is a self-contained event — it does NOT lock the ChapterBoundary
/// (the siege is a follower side event, not a main quest) and does NOT
/// complete any QuestTrigger.
/// </summary>
public class CompanionZombieSiege : MonoBehaviour
{
    [Header("Zombies")]
    [Tooltip("Number of zombies to spawn for the siege.")]
    public int zombieCount = 10;

    [Tooltip("Random zombie prefabs to spawn. One is picked at random per spawn.")]
    public GameObject[] zombiePrefabs;

    [Header("Spawn")]
    [Tooltip("Spawn points for the zombies. A random one is picked per spawn.")]
    public Transform[] spawnPoints;

    [Tooltip("Random spread radius for spawned enemies (0 = exact spawn point).")]
    public float spawnSpread = 4f;

    [Tooltip("Delay between each spawn (seconds). 0 = spawn all at once.")]
    public float spawnInterval = 0.5f;

    [Tooltip("Delay before the first spawn after StartSiege is called.")]
    public float initialDelay = 1f;

    [Header("Optional Banner")]
    [Tooltip("Optional CutscenePlayer to show a banner when the siege starts.")]
    public CutscenePlayer bannerCutscene;

    [Tooltip("Banner title shown when the siege starts.")]
    public string bannerTitle = "ZOMBIE BAO VÂY";

    [Tooltip("Banner subtitle shown when the siege starts.")]
    public string bannerSubtitle = "Tiêu diệt 10 con zombie!";

    [Header("Boundary Lock")]
    [Tooltip("Optional ChapterBoundary to lock while the siege is active, preventing the player from leaving the chapter area. Unlocked when the siege is cleared or aborted. Leave null if you want to use lockZone instead (a custom trigger zone like 'Follower zone').")]
    public ChapterBoundary lockBoundary;

    [Tooltip("If true, also disable the chapter's continuous spawners while the siege is active so only the siege-spawned zombies count toward the kill goal.")]
    public bool suppressChapterSpawners = true;

    [Header("Zone Lock (alternative to lockBoundary)")]
    [Tooltip("Optional trigger collider that defines the area the player is locked inside during the siege. " +
             "When the player exits this trigger, they are teleported back to their last position inside the zone. " +
             "Use this when the siege area is a sub-region of the chapter (e.g. 'Follower zone' around the market). " +
             "If set, this takes precedence over lockBoundary.")]
    public Collider lockZone;

    [Tooltip("How far above the lockZone center to teleport the player back when they exit (prevents clipping into the ground).")]
    public float lockZoneTeleportYOffset = 1f;

    private readonly List<GameObject> _spawnedZombies = new List<GameObject>();
    private readonly List<GameObject> _zoneWalls = new List<GameObject>();
    private bool _siegeActive;
    private int _startKills;
    private int _targetKills;
    private System.Action _onCompleted;
    private Coroutine _siegeRoutine;

    // Zone lock state (for lockZone mode).
    private Transform _player;
    private Rigidbody _playerRb;
    private Vector3 _lastInsidePos;
    private bool _playerInsideZone = true;

    /// <summary>True while the siege is in progress (spawning + waiting for kills).</summary>
    public bool IsSiegeActive => _siegeActive;

    private void FindPlayer()
    {
        if (_player != null) return;
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            _player = playerGO.transform;
            _playerRb = playerGO.GetComponentInParent<Rigidbody>();
            if (_playerRb == null) _playerRb = playerGO.GetComponentInChildren<Rigidbody>();
        }
    }

    private void Update()
    {
        if (!_siegeActive || lockZone == null) return;
        if (_player == null) { FindPlayer(); return; }

        // Check if the player is inside the lock zone. Expand Y bounds so jumping
        // up does not trigger false boundary containment violation & teleport back.
        var bounds = lockZone.bounds;
        bounds.Expand(new Vector3(0f, 500f, 0f));
        bool inside = bounds.Contains(_player.position);

        if (inside)
        {
            // Track the last known inside position (slightly above ground).
            _lastInsidePos = _player.position;
            _playerInsideZone = true;
        }
        else if (_playerInsideZone)
        {
            // Player just exited the zone — teleport back to last inside position.
            Debug.Log($"[CompanionZombieSiege] Player exited lock zone — teleporting back to {_lastInsidePos}");
            TeleportPlayerBack();
            _playerInsideZone = false;
        }
        else
        {
            // Still outside — keep teleporting back each frame until inside.
            TeleportPlayerBack();
        }
    }

    private void TeleportPlayerBack()
    {
        if (_player == null) return;
        Debug.Log($"[SiegeDiag] Frame={Time.frameCount} TELEPORT BACK EXECUTED! Pos={_player.position}, LastInside={_lastInsidePos}, Bounds={lockZone.bounds}");
        Vector3 back = _lastInsidePos;
        back.y += lockZoneTeleportYOffset;
        if (_playerRb != null)
        {
            _playerRb.position = back;
            _playerRb.linearVelocity = Vector3.zero;
        }
        _player.position = back;
        UnityEngine.Physics.SyncTransforms();
    }

    private void BuildZoneWalls()
    {
        if (lockZone == null) return;
        DestroyZoneWalls();

        float wallHeight = 150f;
        const float thickness = 1f;

        if (lockZone is BoxCollider box)
        {
            Vector3 center = box.center;
            Vector3 size = box.size;

            var centers = new Vector3[] {
                new Vector3(center.x - size.x / 2f, 0f, center.z),
                new Vector3(center.x + size.x / 2f, 0f, center.z),
                new Vector3(center.x, 0f, center.z - size.z / 2f),
                new Vector3(center.x, 0f, center.z + size.z / 2f),
            };
            var wallSizes = new Vector3[] {
                new Vector3(thickness, wallHeight, size.z + thickness),
                new Vector3(thickness, wallHeight, size.z + thickness),
                new Vector3(size.x + thickness, wallHeight, thickness),
                new Vector3(size.x + thickness, wallHeight, thickness),
            };

            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"SiegeWall_{i}");
                go.transform.SetParent(lockZone.transform, false);
                go.transform.localPosition = centers[i];
                go.layer = lockZone.gameObject.layer;

                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = false;
                col.size = wallSizes[i];
                col.center = Vector3.zero;

                _zoneWalls.Add(go);
            }
        }
        else
        {
            // Non-box collider: surround with a simple box-based cage.
            var bounds = lockZone.bounds;
            Vector3 c = bounds.center;
            Vector3 s = bounds.size;

            var centers = new Vector3[] {
                new Vector3(c.x - s.x / 2f, c.y, c.z),
                new Vector3(c.x + s.x / 2f, c.y, c.z),
                new Vector3(c.x, c.y, c.z - s.z / 2f),
                new Vector3(c.x, c.y, c.z + s.z / 2f),
            };
            var wallSizes = new Vector3[] {
                new Vector3(thickness, wallHeight, s.z + thickness * 2),
                new Vector3(thickness, wallHeight, s.z + thickness * 2),
                new Vector3(s.x + thickness * 2, wallHeight, thickness),
                new Vector3(s.x + thickness * 2, wallHeight, thickness),
            };

            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"SiegeWall_{i}");
                go.transform.position = centers[i];
                go.layer = lockZone.gameObject.layer;

                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = false;
                col.size = wallSizes[i];
                col.center = Vector3.zero;

                _zoneWalls.Add(go);
            }
        }
    }

    private void DestroyZoneWalls()
    {
        foreach (var w in _zoneWalls)
        {
            if (w != null) Destroy(w);
        }
        _zoneWalls.Clear();
    }

    /// <summary>
    /// Starts the siege. Spawns zombies and waits for the player to kill the
    /// required number. When complete, invokes <paramref name="onCompleted"/>
    /// and cleans up.
    /// </summary>
    public void StartSiege(System.Action onCompleted)
    {
        if (_siegeActive) return;
        _siegeActive = true;
        _onCompleted = onCompleted;

        // Lock the player inside the siege area. Two modes:
        //   1) lockZone (preferred): a custom trigger collider (e.g. "Follower zone")
        //      that defines a sub-region of the chapter. The player is teleported
        //      back when they exit.
        //   2) lockBoundary (fallback): the whole ChapterBoundary.
        if (lockZone != null)
        {
            // Build physical walls around the zone so the player can't climb over.
            BuildZoneWalls();
            FindPlayer();
            if (_player != null)
            {
                var bounds = lockZone.bounds;
                if (bounds.Contains(_player.position))
                {
                    _lastInsidePos = _player.position;
                    _playerInsideZone = true;
                }
                else
                {
                    // Player is outside the zone when the siege starts — teleport
                    // them to the zone center so they start inside.
                    _lastInsidePos = bounds.center;
                    _lastInsidePos.y = lockZone.transform.position.y + lockZoneTeleportYOffset;
                    _playerInsideZone = true;
                    TeleportPlayerBack();
                    Debug.Log($"[CompanionZombieSiege] Player was outside lock zone — teleported to zone center {_lastInsidePos}");
                }
                Debug.Log($"[CompanionZombieSiege] Zone lock active — player locked inside {lockZone.name}. Last inside pos: {_lastInsidePos}");
            }
        }
        else if (lockBoundary != null)
        {
            lockBoundary.LockExternal();
            if (suppressChapterSpawners) lockBoundary.SetSpawnersActive(false);
            Debug.Log("[CompanionZombieSiege] Boundary locked — player cannot leave until siege is cleared.");
        }

        _siegeRoutine = StartCoroutine(SiegeRoutine());
    }

    private IEnumerator SiegeRoutine()
    {
        // Optional banner.
        if (bannerCutscene != null)
        {
            bannerCutscene.title = bannerTitle;
            bannerCutscene.body = bannerSubtitle;
            bool bannerDone = false;
            bannerCutscene.Play(() => bannerDone = true);
            while (!bannerDone) yield return null;
        }

        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        // Record the starting kill count so we can detect the delta.
        _startKills = ScoreManager.Instance != null ? ScoreManager.Instance.kills : 0;
        _targetKills = _startKills + zombieCount;

        // Spawn zombies.
        for (int i = 0; i < zombieCount; i++)
        {
            SpawnOneZombie();
            if (spawnInterval > 0f)
                yield return new WaitForSeconds(spawnInterval);
        }

        Debug.Log($"[CompanionZombieSiege] Spawned {zombieCount} zombies. Waiting for {_targetKills - _startKills} kills.");

        // Wait until the player kills the required number.
        while (true)
        {
            int current = ScoreManager.Instance != null ? ScoreManager.Instance.kills : 0;
            if (current >= _targetKills) break;
            yield return null;
        }

        Debug.Log("[CompanionZombieSiege] Siege cleared — all required kills reached.");

        // Clean up any remaining spawned zombies (in case some were killed by
        // other means or are still alive).
        CleanupSpawnedZombies();

        // Unlock the boundary so the player can move freely after the siege.
        UnlockBoundary();

        _siegeActive = false;
        _siegeRoutine = null;
        _onCompleted?.Invoke();
    }

    private void SpawnOneZombie()
    {
        if (zombiePrefabs == null || zombiePrefabs.Length == 0) return;

        var prefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Length)];
        if (prefab == null) return;

        Vector3 basePos = transform.position;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (sp != null) basePos = sp.position;
        }

        Vector3 spread = spawnSpread > 0f
            ? new Vector3(
                Random.Range(-spawnSpread, spawnSpread),
                0f,
                Random.Range(-spawnSpread, spawnSpread))
            : Vector3.zero;
        Vector3 pos = basePos + spread;

        // Validate the spawn position on the NavMesh.
        pos = FindValidNavMeshPosition(pos, 8f);

        var container = GetRuntimeContainer();
        var go = Instantiate(prefab, pos, Quaternion.identity, container);
        go.SetActive(true);
        _spawnedZombies.Add(go);
        Debug.Log($"[CompanionZombieSiege] Spawned {go.name} at {pos}.");
    }

    private Vector3 FindValidNavMeshPosition(Vector3 requested, float searchRadius)
    {
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(requested, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;

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

        Debug.LogWarning($"[CompanionZombieSiege] No NavMesh position found near {requested} — using fallback.");
        return requested;
    }

    private Transform GetRuntimeContainer()
    {
        var container = GameObject.Find("=== RUNTIME ===");
        if (container == null)
        {
            container = new GameObject("=== RUNTIME ===");
        }
        return container.transform;
    }

    /// <summary>
    /// Destroys all zombies spawned by this siege. Called when the siege is
    /// completed or when the player dies mid-siege (to prevent zombies from
    /// persisting after respawn).
    /// </summary>
    private void CleanupSpawnedZombies()
    {
        if (_spawnedZombies.Count == 0) return;
        Debug.Log($"[CompanionZombieSiege] Cleaning up {_spawnedZombies.Count} spawned zombie(s).");
        for (int i = 0; i < _spawnedZombies.Count; i++)
        {
            var go = _spawnedZombies[i];
            if (go != null) Destroy(go);
        }
        _spawnedZombies.Clear();
    }

    /// <summary>
    /// Aborts the siege (e.g. if the player dies). Stops the coroutine and
    /// cleans up spawned zombies. Does NOT invoke the completion callback.
    /// </summary>
    public void Abort()
    {
        if (_siegeRoutine != null)
        {
            StopCoroutine(_siegeRoutine);
            _siegeRoutine = null;
        }
        _siegeActive = false;
        CleanupSpawnedZombies();
        UnlockBoundary();
    }

    /// <summary>Releases the boundary/zone lock and restores chapter spawners.</summary>
    private void UnlockBoundary()
    {
        DestroyZoneWalls();
        if (lockZone != null)
        {
            _playerInsideZone = true;
            Debug.Log("[CompanionZombieSiege] Zone lock released — player can leave.");
        }
        if (lockBoundary != null)
        {
            lockBoundary.UnlockExternal();
            if (suppressChapterSpawners) lockBoundary.SetSpawnersActive(true);
            Debug.Log("[CompanionZombieSiege] Boundary unlocked — player can leave.");
        }
    }

    private void OnEnable()
    {
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.OnPlayerDied += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.OnPlayerDied -= HandlePlayerDeath;
    }

    private void HandlePlayerDeath()
    {
        if (_siegeActive)
        {
            Debug.Log("[CompanionZombieSiege] Player died during siege — aborting.");
            Abort();
        }
    }

    private void OnDestroy()
    {
        Abort();
    }
}
