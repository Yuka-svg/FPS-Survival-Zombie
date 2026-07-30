using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using cowsins;

/// <summary>
/// Spawns "special" enemies (Boomer and Tank boss) as a bonus threat layered on
/// top of the regular zombie waves. They are wave-gated (only appear from a
/// configurable wave onward), rate-limited, and capped in concurrent count so
/// they spice up encounters without overwhelming the swarm pacing driven by
/// <see cref="Spawm"/> / <see cref="AIDirector"/>.
///
/// Boomers are instantiated fresh (not pooled) on the NavMesh at a safe distance
/// from the player. Tanks use the same approach but with a later wave gate and
/// per-wave stat scaling (health / damage / speed) so later waves produce
/// tougher bosses. Both self-clean on death, so this director only needs to
/// prune dead references to honour the alive caps.
/// </summary>
public class SpecialEnemyDirector : MonoBehaviour
{
    public static SpecialEnemyDirector Instance;

    [Header("Boomer")]
    [Tooltip("The Boomer prefab to spawn (SM_Chr_ZombieBoss_Slobber_01).")]
    public GameObject boomerPrefab;

    [Header("Player")]
    public Transform player;

    [Header("Boomer Gating")]
    [Tooltip("Boomers only start appearing once the wave reaches this number.")]
    public int firstWave = 3;
    [Tooltip("Seconds between Boomer spawn attempts once gating allows them.")]
    public float spawnInterval = 28f;
    [Tooltip("Maximum Boomers alive at once.")]
    public int maxAlive = 2;

    [Header("Boomer Placement")]
    [Tooltip("Closest a Boomer may spawn to the player (XZ).")]
    public float minDistanceFromPlayer = 16f;
    [Tooltip("Farthest a Boomer may spawn to the player (XZ).")]
    public float maxDistanceFromPlayer = 36f;

    [Header("Tank")]
    [Tooltip("The Tank boss prefab to spawn.")]
    public GameObject tankPrefab;

    [Header("Tank Gating")]
    [Tooltip("Tanks only start appearing once the wave reaches this number. Should be >= Boomer firstWave.")]
    public int tankFirstWave = 5;
    [Tooltip("Seconds between Tank spawn attempts once gating allows them.")]
    public float tankSpawnInterval = 90f;
    [Tooltip("Maximum Tanks alive at once. Bosses are usually capped at 1.")]
    public int tankMaxAlive = 1;
    [Tooltip("On boss waves, force-spawn the Tank immediately regardless of timer.")]
    public bool forceTankOnBossWave = true;

    [Header("Tank Placement")]
    [Tooltip("Closest a Tank may spawn to the player (XZ).")]
    public float tankMinDistanceFromPlayer = 20f;
    [Tooltip("Farthest a Tank may spawn from the player (XZ).")]
    public float tankMaxDistanceFromPlayer = 40f;

    [Header("Tank Spawn Warning")]
    [Tooltip("Tiếng gầm cảnh báo phát ra khi Tank spawn (2D, nghe toàn map).")]
    public AudioClip tankSpawnWarningClip;
    [Tooltip("Âm lượng tiếng gầm cảnh báo (0-1).")]
    [Range(0f, 1f)]
    public float tankSpawnWarningVolume = 0.8f;

    [Header("Tank Scaling Per Wave")]
    [Tooltip("Wave used as the baseline (no scaling) for Tank stats. At this wave the Tank uses its prefab defaults.")]
    public int tankBaseWave = 5;
    [Tooltip("Extra max health added to the Tank for each wave above tankBaseWave.")]
    public int tankHealthPerWave = 150;
    [Tooltip("Extra damage added to all Tank attacks for each wave above tankBaseWave.")]
    public float tankDamagePerWave = 8f;
    [Tooltip("Extra run speed added to the Tank for each wave above tankBaseWave. Clamped to NavMeshAgent limits.")]
    public float tankSpeedPerWave = 0.1f;
    [Tooltip("Optional cap on total speed bonus to keep Tanks from outrunning the player.")]
    public float tankMaxSpeedBonus = 1.5f;

    [Header("Placement (shared)")]
    [Tooltip("How far to search for a valid NavMesh point near the chosen spot.")]
    public float navSampleRadius = 6f;

    private readonly List<GameObject> _alive = new List<GameObject>();
    private float _timer;

    private readonly List<GameObject> _aliveTanks = new List<GameObject>();
    private float _tankTimer;
    private AudioSource _audioSource;
    private NavMeshPath _pathValidation;

    private void Awake()
    {
        Instance = this;
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2D — nghe toàn map
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // Stagger the first spawn so a Boomer doesn't appear the instant the gate opens.
        _timer = spawnInterval * 0.5f;
        _tankTimer = tankSpawnInterval * 0.5f;
    }

    private void Update()
    {
        if (player == null)
            return;

        // Skip spawning ticks if player is dead
        var stats = player.GetComponent<PlayerStats>();
        if (stats != null && stats.IsDead)
            return;

        int wave = WaveManager.Instance != null ? WaveManager.Instance.currentWave : 1;

        //------------------------------------------------
        // BOOMER
        //------------------------------------------------
        if (boomerPrefab != null && wave >= firstWave)
        {
            PruneDeadBoomers();

            _timer += Time.deltaTime;

            if (_timer >= spawnInterval)
            {
                _timer = 0f;

                if (_alive.Count < maxAlive)
                    TrySpawnBoomer();
            }
        }

        //------------------------------------------------
        // TANK
        //------------------------------------------------
        bool isBossWave = WaveManager.Instance != null && WaveManager.Instance.IsBossWave();

        if (tankPrefab != null && wave >= tankFirstWave)
        {
            PruneDeadTanks();

            // On boss waves, force-spawn the Tank immediately (if under the cap).
            if (isBossWave && forceTankOnBossWave && _aliveTanks.Count < tankMaxAlive)
            {
                _tankTimer = 0f;
                TrySpawnTank(wave);
            }

            _tankTimer += Time.deltaTime;

            if (_tankTimer >= tankSpawnInterval)
            {
                _tankTimer = 0f;

                if (_aliveTanks.Count < tankMaxAlive)
                    TrySpawnTank(wave);
            }
        }
    }

    private void PruneDeadBoomers()
    {
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            GameObject go = _alive[i];
            if (go == null || !go.activeInHierarchy)
            {
                _alive.RemoveAt(i);
                continue;
            }

            BoomerAI b = go.GetComponent<BoomerAI>();
            if (b != null && b.IsDead)
                _alive.RemoveAt(i);
        }
    }

    private void PruneDeadTanks()
    {
        for (int i = _aliveTanks.Count - 1; i >= 0; i--)
        {
            GameObject go = _aliveTanks[i];
            if (go == null || !go.activeInHierarchy)
            {
                _aliveTanks.RemoveAt(i);
                continue;
            }

            TankBossAI t = go.GetComponent<TankBossAI>();
            if (t != null && t.IsDead)
                _aliveTanks.RemoveAt(i);
        }
    }

    private void TrySpawnBoomer()
    {
        Vector3 spawnPos;
        if (!TryGetSpawnPosition(out spawnPos, minDistanceFromPlayer, maxDistanceFromPlayer))
            return;

        GameObject boomer = Instantiate(
            boomerPrefab,
            spawnPos,
            Quaternion.identity);

        _alive.Add(boomer);
    }

    private void TrySpawnTank(int wave)
    {
        Vector3 spawnPos;
        if (!TryGetSpawnPosition(out spawnPos, tankMinDistanceFromPlayer, tankMaxDistanceFromPlayer))
            return;

        // Phát tiếng gầm cảnh báo khi Tank spawn (2D, nghe toàn map).
        if (tankSpawnWarningClip != null && _audioSource != null)
            _audioSource.PlayOneShot(tankSpawnWarningClip, tankSpawnWarningVolume);

        // Switch to the final-boss fight music.
        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayFinalBossMusic();

        GameObject tank = Instantiate(
            tankPrefab,
            spawnPos,
            Quaternion.identity);

        ApplyTankScaling(tank, wave);

        _aliveTanks.Add(tank);
    }

    /// <summary>
    /// Scales the Tank's health, damage and speed based on how far the current
    /// wave is above <see cref="tankBaseWave"/>. At tankBaseWave the Tank uses
    /// its prefab defaults; each wave beyond that adds the configured per-wave
    /// bonuses. Waves below tankBaseWave apply no downscaling (prefab defaults win).
    /// </summary>
    private void ApplyTankScaling(GameObject tank, int wave)
    {
        TankBossAI ai = tank.GetComponent<TankBossAI>();
        if (ai == null)
            return;

        int wavesAboveBase = Mathf.Max(0, wave - tankBaseWave);
        if (wavesAboveBase == 0)
            return;

        int healthBonus = tankHealthPerWave * wavesAboveBase;
        float damageBonus = tankDamagePerWave * wavesAboveBase;
        float speedBonus = Mathf.Min(tankSpeedPerWave * wavesAboveBase, tankMaxSpeedBonus);

        ai.maxHealth += healthBonus;
        ai.currentHealth = ai.maxHealth;

        ai.punchDamage += damageBonus;
        ai.swipeDamage += damageBonus;
        ai.jumpDamage += damageBonus;

        ai.runSpeed += speedBonus;
    }

    private bool TryGetSpawnPosition(out Vector3 result, float minDist, float maxDist)
    {
        result = player.position;

        // Find player NavMesh position first
        NavMeshHit playerHit;
        if (!NavMesh.SamplePosition(player.position, out playerHit, 10f, NavMesh.AllAreas))
        {
            return false;
        }
        Vector3 playerNavPos = playerHit.position;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(minDist, maxDist);

            Vector3 candidate = player.position + new Vector3(
                Mathf.Cos(angle) * dist,
                0f,
                Mathf.Sin(angle) * dist);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(candidate, out hit, navSampleRadius, NavMesh.AllAreas))
            {
                // Reject underground NavMesh positions (disconnected islands
                // at y<-1 that cover 31% of the NavMesh). Special enemies
                // spawning there can never reach the player.
                if (hit.position.y < -1f)
                    continue;

                // Validate path connectivity (Main Thread, Cached Path)
                if (_pathValidation == null) _pathValidation = new NavMeshPath();
                NavMesh.CalculatePath(hit.position, playerNavPos, NavMesh.AllAreas, _pathValidation);
                if (_pathValidation.status != NavMeshPathStatus.PathComplete)
                    continue;

                result = hit.position;
                return true;
            }
        }

        return false;
    }

    public void FlushSpecialEnemies()
    {
        var tempBoomers = new List<GameObject>(_alive);
        foreach (var boomer in tempBoomers)
        {
            if (boomer != null) Destroy(boomer);
        }
        _alive.Clear();

        var tempTanks = new List<GameObject>(_aliveTanks);
        foreach (var tank in tempTanks)
        {
            if (tank != null) Destroy(tank);
        }
        _aliveTanks.Clear();
    }
}
