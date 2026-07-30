using UnityEngine;

/// <summary>
/// Central runtime tracker for ALL player stats. Owns every stat directly as
/// fields so the entire game state can be read from one script. Existing
/// managers (ScoreManager, AIDirector, CollectibleManager, CowsinsHUDAdapter)
/// forward their increments here via Register* methods / events, keeping a
/// single source of truth for the stats panel + game-over screen.
///
/// Placement: lives on the GeneralManagers GameObject alongside the other
/// singletons (ScoreManager, AIDirector, ...). Singleton via Instance.
/// </summary>
public class PlayerStatsTracker : MonoBehaviour
{
    public static PlayerStatsTracker Instance;

    [Header("Kills by type")]
    public int zombieKills;
    public int boomerKills;
    public int tankKills;

    [Header("Combat")]
    public float totalDamageDealt;
    public int shotsFired;
    public int shotsHit;
    public int crits;
    public int reloadCount;

    [Header("Score / Progression")]
    public int score;
    public int kills;
    public int coins;
    public int journalsCollected;

    [Header("Survival")]
    public float healthLost;
    public float healthHealed;
    public int deathCount;

    [Header("Exploration")]
    public float playTime;
    public float distanceMoved;

    [Header("Player Reference")]
    [Tooltip("Auto-found from the Player tag if left empty.")]
    public Transform player;

    private Vector3 _lastPlayerPos;
    private bool _playerPosInitialized;
    private float _lastHealth = -1f;
    private bool _eventsBound;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    private void OnEnable()
    {
        StartCoroutine(BindAdapterWhenReady());
    }

    private void OnDisable()
    {
        UnbindAdapter();
    }

    private System.Collections.IEnumerator BindAdapterWhenReady()
    {
        float timeout = 12f;
        // Wait until the adapter exists AND has pulled real health data. The adapter
        // sets Instance in Awake (before PullHealth runs), so Health is still 0 at
        // that point. Additionally, Cowsins' PlayerStats sets maxHealth in the
        // inspector but only sets health = maxHealth inside Start() — so before
        // Start() runs, MaxHealth is already 100 but Health is still 0. If we seed
        // _lastHealth from 0, the first real PullHealth (100) would be miscounted as
        // 100 "healed". Requiring BOTH Health > 0 AND MaxHealth > 0 guarantees
        // PlayerStats.Start() has run and health is the authoritative value.
        while (timeout > 0f && (CowsinsHUDAdapter.Instance == null
            || CowsinsHUDAdapter.Instance.MaxHealth <= 0f
            || CowsinsHUDAdapter.Instance.Health <= 0f))
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        var a = CowsinsHUDAdapter.Instance;
        if (a == null) yield break;
        a.OnHealthChanged += HandleHealthChanged;
        a.OnReloadChanged += HandleReloadChanged;
        a.OnDied += HandleDied;
        a.OnCoinsChanged += HandleCoinsChanged;
        // Seed the last-health baseline + coins from the adapter's current values.
        _lastHealth = a.Health;
        coins = a.Coins;
        _eventsBound = true;
    }

    private void UnbindAdapter()
    {
        if (!_eventsBound) return;
        var a = CowsinsHUDAdapter.Instance;
        if (a != null)
        {
            a.OnHealthChanged -= HandleHealthChanged;
            a.OnReloadChanged -= HandleReloadChanged;
            a.OnDied -= HandleDied;
            a.OnCoinsChanged -= HandleCoinsChanged;
        }
        _eventsBound = false;
    }

    private void HandleHealthChanged(float hp, float max, bool damaged)
    {
        if (_lastHealth < 0f)
        {
            _lastHealth = hp;
            return;
        }
        float delta = hp - _lastHealth;
        if (delta < -0.001f) healthLost += -delta;
        else if (delta > 0.001f) healthHealed += delta;
        _lastHealth = hp;
    }

    private void HandleReloadChanged(bool reloading)
    {
        if (reloading) reloadCount++;
    }

    private void HandleDied()
    {
        deathCount++;
    }

    private void HandleCoinsChanged(int total)
    {
        coins = total;
    }

    private void Update()
    {
        playTime += Time.deltaTime;
        TrackDistance();
    }

    private void TrackDistance()
    {
        if (player == null) return;

        Vector3 current = player.position;
        if (!_playerPosInitialized)
        {
            _lastPlayerPos = current;
            _playerPosInitialized = true;
            return;
        }

        float moved = Vector3.Distance(current, _lastPlayerPos);
        // Ignore tiny jitter (< 0.01m) to avoid counting physics noise.
        if (moved > 0.01f)
            distanceMoved += moved;

        _lastPlayerPos = current;
    }

    // ---- Kill registration (called by each enemy's death handler) ----

    public void RegisterZombieKill()
    {
        zombieKills++;
    }

    public void RegisterBoomerKill()
    {
        boomerKills++;
    }

    public void RegisterTankKill()
    {
        tankKills++;
    }

    // ---- Damage registration (called by each enemy's TakeDamage) ----

    public void RegisterDamageDealt(float damage)
    {
        if (damage <= 0f) return;
        totalDamageDealt += damage;
    }

    // ---- Shot registration (forwarded by AIDirector) ----

    public void RegisterShot()
    {
        shotsFired++;
    }

    public void RegisterHit()
    {
        shotsHit++;
    }

    // ---- Score / kill / crit registration (forwarded by ScoreManager) ----

    public void RegisterKill(int scoreAmount)
    {
        kills++;
        score += scoreAmount;
    }

    public void RegisterCrit(int scoreAmount)
    {
        crits++;
        score += scoreAmount;
    }

    public void AddScore(int amount)
    {
        score += amount;
    }

    // ---- Journal registration (forwarded by CollectibleManager) ----

    public void RegisterJournalCollected()
    {
        journalsCollected++;
    }

    // ---- Read API for UI ----

    /// <summary>Total kills across all enemy types.</summary>
    public int TotalKills => zombieKills + boomerKills + tankKills;

    public int SpecialKills => boomerKills + tankKills;

    /// <summary>Distance moved in metres (tracked independently from the player transform).</summary>
    public float GetDistanceMoved()
    {
        return distanceMoved;
    }

    /// <summary>Survival time in seconds. Falls back to ScoreManager if this tracker hasn't run.</summary>
    public float GetPlayTime()
    {
        if (ScoreManager.Instance != null && playTime <= 0f)
            return ScoreManager.Instance.GetSurvivalTime();
        return playTime;
    }

    public int GetCoins()
    {
        return coins;
    }

    public int GetWaveReached()
    {
        return WaveManager.Instance != null ? WaveManager.Instance.currentWave : 0;
    }

    /// <summary>Final score = base score + survival time bonus (mirrors ScoreManager).</summary>
    public int GetScore()
    {
        return score + Mathf.RoundToInt(playTime);
    }

    public int GetBaseScore()
    {
        return score;
    }

    public int GetBestScore()
    {
        return PlayerPrefs.GetInt("BestScore", 0);
    }

    public int GetBestWave()
    {
        return PlayerPrefs.GetInt("BestWave", 0);
    }

    public int GetCrits()
    {
        return crits;
    }

    public int GetShotsFired()
    {
        return shotsFired;
    }

    public int GetShotsHit()
    {
        return shotsHit;
    }

    public float GetAccuracy()
    {
        if (shotsFired <= 0) return 0f;
        return (float)shotsHit / shotsFired * 100f;
    }

    /// <summary>Journals collected so far.</summary>
    public int GetJournalsCollected()
    {
        return journalsCollected;
    }

    /// <summary>Total journals available in the game (read from CollectibleManager).</summary>
    public int GetJournalsTotal()
    {
        return CollectibleManager.Instance != null ? CollectibleManager.Instance.Total : 0;
    }

    public float GetHealthLost()
    {
        return healthLost;
    }

    public float GetHealthHealed()
    {
        return healthHealed;
    }

    public int GetDeathCount()
    {
        return deathCount;
    }

    public int GetReloadCount()
    {
        return reloadCount;
    }

    /// <summary>Formats a time in seconds as MM:SS or H:MM:SS for long sessions.</summary>
    public static string FormatTime(float seconds)
    {
        int totalSec = Mathf.FloorToInt(seconds);
        int h = totalSec / 3600;
        int m = (totalSec % 3600) / 60;
        int s = totalSec % 60;
        if (h > 0) return $"{h}:{m:00}:{s:00}";
        return $"{m:00}:{s:00}";
    }

    /// <summary>Formats a distance in metres, showing km if large.</summary>
    public static string FormatDistance(float metres)
    {
        if (metres >= 1000f) return (metres / 1000f).ToString("F2") + " km";
        return Mathf.RoundToInt(metres) + " m";
    }

    /// <summary>Formats damage as an integer with thousands separators.</summary>
    public static string FormatDamage(float damage)
    {
        return Mathf.RoundToInt(damage).ToString("N0");
    }

    /// <summary>Formats a health amount (rounded to integer with thousands separators).</summary>
    public static string FormatHealth(float amount)
    {
        return Mathf.RoundToInt(amount).ToString("N0");
    }
}
