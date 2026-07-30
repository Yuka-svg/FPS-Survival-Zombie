using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central achievement system.  Tracks unlock state for all achievements,
/// persists to PlayerPrefs, and fires events that the UI subscribes to.
///
/// Integration points (called from game code):
///  - <see cref="NotifyChapterComplete"/>  — from StoryManager when the story ends.
///  - <see cref="NotifyZombieKill"/>       — from ZombieAI.Die() / BoomerAI / TankAI.
///  - <see cref="NotifyZombieKillWhileWallRunning"/> — from ZombieAI.Die() if wall-running.
///  - <see cref="NotifyTankKill"/>         — from TankAI.Die() via PlayerStatsTracker.
///  - <see cref="NotifyBoomerCloseExplosion"/> — from BoomerAI.ExplosionEvent().
///
/// Placement: lives on the GeneralManagers GameObject alongside the other
/// singletons (ScoreManager, AIDirector, PlayerStatsTracker, ...).
/// </summary>
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    [Header("Achievement Definitions")]
    [Tooltip("All achievements in the game. Assign via the inspector or code.")]
    public AchievementData[] achievements;

    // ---- Known achievement IDs (used for typed notify methods) ----
    // These must match the `id` field on the corresponding AchievementData assets.
    public const string IdChapterComplete = "chapter_complete_under_11min";
    public const string IdHellSlayer = "hell_slayer_130_crooks";
    public const string IdWallRunKills = "wall_run_20_kills";
    public const string IdFirstTank = "first_tank_kill";
    public const string IdBoomerCloseExplosion = "boomer_close_explosion";

    [Header("Wall-Run Kill Tracking")]
    [Tooltip("Max distance between player and boomer to count as 'close' explosion.")]
    public float boomerCloseExplosionDistance = 5f;

    // ---- Runtime state ----
    private readonly HashSet<string> _unlocked = new HashSet<string>();
    private readonly Dictionary<string, int> _progress = new Dictionary<string, int>();
    private bool _wallRunning;

    // ---- Events ----
    /// <summary>Fired when an achievement is unlocked. (achievement)</summary>
    public event System.Action<AchievementData> OnAchievementUnlocked;

    /// <summary>Fired when a progression achievement's progress changes. (achievement, currentProgress, target)</summary>
    public event System.Action<AchievementData, int, int> OnProgressChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadState();
    }

    private void Start()
    {
        // Subscribe to StoryManager chapter changes for chapter-complete achievement.
        var sm = StoryManager.Instance;
        if (sm != null)
            sm.OnChapterChanged += HandleChapterChanged;
    }

    private void OnDisable()
    {
        var sm = StoryManager.Instance;
        if (sm != null)
            sm.OnChapterChanged -= HandleChapterChanged;
    }

    // ---- Persistence ----

    private void LoadState()
    {
        _unlocked.Clear();
        _progress.Clear();
        if (achievements == null) return;
        foreach (var ach in achievements)
        {
            if (ach == null) continue;
            if (PlayerPrefs.GetInt(ach.UnlockedKey, 0) == 1)
                _unlocked.Add(ach.id);
            // Progress is NOT loaded from PlayerPrefs — it is per-match only
            // and resets at the start of each playthrough (see ResetProgress).
            if (ach.isProgression)
                _progress[ach.id] = 0;
        }
    }

    /// <summary>
    /// Reset all progression achievements' progress to 0. Call this at the start
    /// of each match/playthrough so progress is only tracked within a single run.
    /// Unlock state is preserved (once unlocked, stays unlocked).
    /// </summary>
    public void ResetProgress()
    {
        _progress.Clear();
        if (achievements == null) return;
        foreach (var ach in achievements)
        {
            if (ach == null || !ach.isProgression) continue;
            if (!_unlocked.Contains(ach.id))
                _progress[ach.id] = 0;
        }
        // Notify UI subscribers so progress bars reset visually.
        if (achievements != null)
        {
            foreach (var ach in achievements)
            {
                if (ach == null || !ach.isProgression) continue;
                OnProgressChanged?.Invoke(ach, 0, ach.targetValue);
            }
        }
        Debug.Log("[AchievementManager] Progress reset for new playthrough.");
    }

    /// <summary>
    /// Reload unlock state and progress from PlayerPrefs. Call this after
    /// PlayerPrefs has been cleared/replaced (e.g. after switching PlayFab
    /// accounts) so the in-memory cache matches the new account's data.
    /// </summary>
    public void ReloadState()
    {
        LoadState();
    }

    private void SaveUnlocked(AchievementData ach)
    {
        PlayerPrefs.SetInt(ach.UnlockedKey, 1);
        PlayerPrefs.Save();
    }

    private void SaveProgress(AchievementData ach, int value)
    {
        // Progress is per-match only — intentionally not persisted to PlayerPrefs.
        // It lives in the in-memory _progress dictionary and resets each playthrough.
    }

    // ---- Public read API ----

    public bool IsUnlocked(string id) => _unlocked.Contains(id);

    public bool IsUnlocked(AchievementData ach) => ach != null && _unlocked.Contains(ach.id);

    public int GetProgress(AchievementData ach)
    {
        if (ach == null || !ach.isProgression) return 0;
        return _progress.TryGetValue(ach.id, out int v) ? v : 0;
    }

    // ---- Internal unlock helper ----

    private AchievementData FindById(string id)
    {
        if (achievements == null) return null;
        foreach (var ach in achievements)
            if (ach != null && ach.id == id) return ach;
        return null;
    }

    private void Unlock(AchievementData ach)
    {
        if (ach == null || _unlocked.Contains(ach.id)) return;
        _unlocked.Add(ach.id);
        SaveUnlocked(ach);
        Debug.Log($"[AchievementManager] UNLOCKED: {ach.title}");
        OnAchievementUnlocked?.Invoke(ach);
        // Upload to PlayFab cloud if logged in
        if (PlayFabManager.Instance != null && PlayFabManager.Instance.IsLoggedIn)
            PlayFabManager.Instance.SaveAllToCloud();
    }

    private void SetProgress(AchievementData ach, int value)
    {
        if (ach == null || !ach.isProgression || _unlocked.Contains(ach.id)) return;
        int clamped = Mathf.Min(value, ach.targetValue);
        int prev = GetProgress(ach);
        if (clamped == prev) return;
        _progress[ach.id] = clamped;
        SaveProgress(ach, clamped);
        OnProgressChanged?.Invoke(ach, clamped, ach.targetValue);
        if (clamped >= ach.targetValue)
            Unlock(ach);
    }

    private void AddProgress(AchievementData ach, int delta)
    {
        if (ach == null || !ach.isProgression || _unlocked.Contains(ach.id)) return;
        SetProgress(ach, GetProgress(ach) + delta);
    }

    // ---- Typed notify methods (called from game code) ----

    /// <summary>
    /// Called when the story is completed (all chapters done).
    /// Checks if the play time is within the target for the speed-run achievement.
    /// </summary>
    public void NotifyChapterComplete()
    {
        var ach = FindById(IdChapterComplete);
        if (ach == null || IsUnlocked(ach)) return;
        float playTime = PlayerStatsTracker.Instance != null
            ? PlayerStatsTracker.Instance.playTime
            : (ScoreManager.Instance != null ? ScoreManager.Instance.GetSurvivalTime() : 0f);
        // 11 minutes = 660 seconds
        if (playTime <= 660f)
        {
            Unlock(ach);
        }
    }

    /// <summary>
    /// Called when any Crook (standard zombie) is killed.
    /// Tracks total Crook kills for the "Hell Slayer" achievement.
    /// </summary>
    public void NotifyZombieKill()
    {
        AddProgress(FindById(IdHellSlayer), 1);
    }

    /// <summary>
    /// Called when a Crook is killed while the player is wall-running.
    /// Tracks wall-run kills for the "At Ease, Cooper" achievement.
    /// </summary>
    public void NotifyZombieKillWhileWallRunning()
    {
        AddProgress(FindById(IdWallRunKills), 1);
    }

    /// <summary>
    /// Called when a Tank is killed for the first time.
    /// </summary>
    public void NotifyTankKill()
    {
        Unlock(FindById(IdFirstTank));
    }

    /// <summary>
    /// Called when a Boomer explodes. Checks if the player was within close range.
    /// </summary>
    /// <param name="boomerPosition">World position of the exploding boomer.</param>
    public void NotifyBoomerCloseExplosion(Vector3 boomerPosition)
    {
        var ach = FindById(IdBoomerCloseExplosion);
        if (ach == null || IsUnlocked(ach)) return;
        var player = PlayerStatsTracker.Instance != null ? PlayerStatsTracker.Instance.player : null;
        if (player == null) return;
        float dist = Vector3.Distance(player.position, boomerPosition);
        if (dist <= boomerCloseExplosionDistance)
            Unlock(ach);
    }

    // ---- StoryManager hook ----

    private void HandleChapterChanged(int oldChapter, int newChapter)
    {
        // newChapter == -1 means story complete (see StoryManager.AdvanceChapter).
        if (newChapter == -1)
            NotifyChapterComplete();
    }

    // ---- Wall-run state tracking (called by ZombieAI before kill) ----

    /// <summary>Set by ZombieAI before registering a kill, to indicate wall-run state.</summary>
    public void SetWallRunning(bool running) => _wallRunning = running;

    /// <summary>Reads the current wall-run state. Used by ZombieAI to decide which notify to call.</summary>
    public bool IsWallRunning => _wallRunning;
}
