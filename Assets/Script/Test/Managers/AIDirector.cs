using System;
using System.Collections.Generic;
using UnityEngine;
using cowsins;

public class AIDirector : MonoBehaviour
{
    public static AIDirector Instance;

    [Header("Player")]
    public PlayerStats player;

    [Header("Threat")]
    [Range(0, 100)]
    public float threatLevel;

    [Header("Difficulty")]
    public int minSpawnCount = 2;
    public int maxSpawnCount = 20;

    [Header("Player Tracking")]
    public float survivalTime;
    public float playerHealthPercent;

    [Header("Accuracy")]
    public int shotsFired;
    public int shotsHit;

    [Header("Kills")]
    public int zombiesKilled;

    [Header("Movement")]
    public float distanceMoved;
    public float camperTime;

    public event Action OnThreatChanged;

    [Header("Debug")]
    public DirectorState currentState;

    private Vector3 lastPlayerPosition;

    private readonly HashSet<ZombieAI> activeZombies =
        new HashSet<ZombieAI>();

    public enum DirectorState
    {
        Calm,
        BuildUp,
        Attack,
        Recovery
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerStats>();

        if (player != null)
            lastPlayerPosition = player.transform.position;
    }

    private float updateTimer = 0f;
    private float updateInterval = 0.5f;

    private void Update()
    {
        if (player == null)
            return;

        survivalTime += Time.deltaTime;
        updateTimer += Time.deltaTime;

        if (updateTimer >= updateInterval)
        {
            TrackMovement(updateTimer);
            UpdatePlayerHealth();
            CalculateThreat();
            UpdateState();
            OnThreatChanged?.Invoke();
            updateTimer = 0f;
        }
    }

    #region Tracking

    private void TrackMovement(float dt)
    {
        float moved =
            Vector3.Distance(
                player.transform.position,
                lastPlayerPosition
            );

        distanceMoved += moved;

        if (moved < 3f * dt)
            camperTime += dt;
        else
            camperTime = 0f;

        lastPlayerPosition =
            player.transform.position;
    }

    private void UpdatePlayerHealth()
    {
        playerHealthPercent =
            player.health /
            player.maxHealth;
    }

    #endregion

    #region Threat

    private void CalculateThreat()
    {
        float threat = 0f;

        threat += activeZombies.Count * 4f;

        threat += GetKillRate() * 2f;

        threat += GetAccuracy() * 30f;

        threat -= playerHealthPercent * 40f;

        if (camperTime > 10f)
            threat += 20f;

        threat += Mathf.Clamp(
            survivalTime / 60f,
            0f,
            25f
        );

        threatLevel =
            Mathf.Clamp(
                threat,
                0f,
                100f
            );
    }

    private void UpdateState()
    {
        if (threatLevel < 25)
        {
            currentState =
                DirectorState.Calm;
        }
        else if (threatLevel < 50)
        {
            currentState =
                DirectorState.BuildUp;
        }
        else if (threatLevel < 80)
        {
            currentState =
                DirectorState.Attack;
        }
        else
        {
            currentState =
                DirectorState.Recovery;
        }
    }

    #endregion

    #region Public Metrics

    public float GetAccuracy()
    {
        if (shotsFired == 0)
            return 0;

        return (float)shotsHit /
               shotsFired;
    }

    public float GetKillRate()
    {
        if (survivalTime <= 0)
            return 0;

        return zombiesKilled /
               (survivalTime / 60f);
    }

    #endregion

    #region Zombie Registry

    public void RegisterZombie(
        ZombieAI zombie)
    {
        activeZombies.Add(zombie);
    }

    public void UnregisterZombie(
        ZombieAI zombie)
    {
        activeZombies.Remove(zombie);
    }

    public void FlushActiveZombies()
    {
        var tempActive = new List<ZombieAI>(activeZombies);
        foreach (var zombie in tempActive)
        {
            if (zombie != null && zombie.gameObject != null)
            {
                zombie.gameObject.SetActive(false);
            }
        }
        activeZombies.Clear();
    }

    public void RegisterKill()
    {
        zombiesKilled++;
    }


    public int GetZombieCount()
    {
        return activeZombies.Count;
    }

    #endregion

    #region Combat Events

    public void RegisterShot()
    {
        shotsFired++;
        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterShot();
    }

    public void RegisterHit()
    {
        shotsHit++;
        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterHit();
    }

    #endregion

    #region Spawning

        public int GetRecommendedSpawnCount()
    {
        switch (currentState)
        {
            case DirectorState.Calm:
                return UnityEngine.Random.Range(2, 4);

            case DirectorState.BuildUp:
                return UnityEngine.Random.Range(4, 8);

            case DirectorState.Attack:
                return UnityEngine.Random.Range(8, 15);

            case DirectorState.Recovery:
                return UnityEngine.Random.Range(0, 2);
        }

        return minSpawnCount;
    }

    public bool ShouldSpawnRunner()
    {
        return
            currentState ==
            DirectorState.Attack;
    }

    public bool ShouldPunishCamper()
    {
        return camperTime > 10f;
    }

    #endregion
}