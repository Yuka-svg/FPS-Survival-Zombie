using UnityEngine;
using cowsins;

/// <summary>
/// Save Room: a safe zone where the player can rest, heal, and resume from if
/// they die. While the player is inside the trigger volume:
/// - All zombie spawners in `spawnersToSuppress` are disabled (no spawns).
/// - The player is slowly healed up to full health.
/// - The respawn checkpoint is updated so PlayerStats.Respawn uses this position.
///
/// Place one SaveRoom per chapter (the Level Design doc maps 5 Save Rooms to the
/// 5 chapters). The heal is gradual so it feels like a rest, not an instant full
/// heal, but a `healRate` of 0 makes it instant on enter.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SaveRoom : MonoBehaviour
{
    [Header("Heal")]
    [Tooltip("Health restored per second while inside. 0 = instant full heal on enter.")]
    public float healRate = 25f;

    [Tooltip("If true, also restores shield to max.")]
    public bool restoreShield = true;

    [Header("Spawner Suppression")]
    [Tooltip("Zombie spawners to disable while the player is resting here.")]
    public MonoBehaviour[] spawnersToSuppress;

    [Header("Checkpoint")]
    [Tooltip("Transform the player respawns at if they die after reaching this save room. If null, uses this transform.")]
    public Transform respawnPoint;

    [Header("Visuals")]
    [Tooltip("Optional glow/vignette object toggled on while inside.")]
    public GameObject restIndicator;

    [Header("Chapter Transition")]
    [Tooltip("If > 0, plays the chapter transition cutscene for this chapter number " +
             "when the player enters this save room for the first time. " +
             "Set to 2 on SaveRoom_Ch2, 3 on SaveRoom_Ch3, etc. 0 = no cutscene.")]
    public int chapterTransitionOnEnter = 0;

    [Header("Chapter")]
    [Tooltip("Chapter number this save room belongs to (1-5). Used to determine " +
             "whether spawners should be re-enabled when the player leaves: if the " +
             "chapter is already completed, spawners stay off permanently.")]
    public int chapter = 0;

    private PlayerStats _playerStats;
    private bool _inside;
    private bool _cutscenePlayed;
    private Vector3 _checkpointPos;
    private float _checkTimer = 0f;

    /// <summary>
    /// Marks the chapter-transition cutscene as already played so re-entering
    /// this save room doesn't replay the banner. Called by CompanionManager
    /// during the skip path.
    /// </summary>
    public void MarkCutscenePlayed()
    {
        _cutscenePlayed = true;
    }

    /// <summary>
    /// Last save room checkpoint the player entered, scene-persistent (static).
    /// Used by GameOverManager to respawn the player at the save room instead
    /// of reloading the whole scene (which would lose quest/zombie progress).
    /// </summary>
    public static Vector3? LastCheckpoint { get; set; }
    public static Quaternion LastCheckpointRotation { get; set; }

    /// <summary>
    /// The save room the player is currently standing inside, or null.
    /// Used by FastTravelWidget to gate the fast travel menu ([T]).
    /// </summary>
    public static SaveRoom CurrentRoom { get; set; }

    /// <summary>Reset checkpoint static state for a new game session.</summary>
    public static void ResetStaticState()
    {
        LastCheckpoint = null;
        LastCheckpointRotation = Quaternion.identity;
        CurrentRoom = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnDomainReload()
    {
        LastCheckpoint = null;
        LastCheckpointRotation = Quaternion.identity;
        CurrentRoom = null;
    }

    private void SaveCheckpoint()
    {
        LastCheckpoint = _checkpointPos;
        LastCheckpointRotation = respawnPoint != null ? respawnPoint.rotation : transform.rotation;
        Debug.Log($"[SaveRoom] Checkpoint updated to {_checkpointPos}.");
    }

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void Start()
    {
        _checkpointPos = respawnPoint != null ? respawnPoint.position : transform.position;
        if (restIndicator != null) restIndicator.SetActive(false);
    }

    private void HandlePlayerEnter(GameObject playerObj)
    {
        _inside = true;
        CurrentRoom = this;
        _playerStats = playerObj.GetComponentInParent<PlayerStats>();
        if (_playerStats == null) _playerStats = playerObj.GetComponentInChildren<PlayerStats>();

        SetSpawners(false);
        if (restIndicator != null) restIndicator.SetActive(true);

        // Update the global checkpoint so GameOverManager can respawn here.
        SaveCheckpoint();

        if (healRate <= 0f && _playerStats != null)
        {
            _playerStats.HealFull();
        }

        // Play chapter transition cutscene the first time the player enters this
        // save room (e.g. SaveRoom_Ch2 triggers the "CHƯƠNG 2" banner).
        if (chapterTransitionOnEnter > 0 && !_cutscenePlayed)
        {
            _cutscenePlayed = true;
            if (StoryManager.Instance != null)
                StoryManager.Instance.PlayChapterTransitionCutscene(chapterTransitionOnEnter);
        }

        Debug.Log($"[SaveRoom] Player entered save room at {_checkpointPos}.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        HandlePlayerEnter(other.gameObject);
    }

    private void Update()
    {
        _checkTimer += Time.unscaledDeltaTime;
        if (_checkTimer >= 1f)
        {
            _checkTimer = 0f;
            var player = GameObject.FindGameObjectWithTag("Player");
            var trigger = GetComponent<Collider>();
            if (player != null && trigger != null)
            {
                bool isInside = trigger.bounds.Contains(player.transform.position);
                if (isInside != _inside)
                {
                    if (isInside)
                    {
                        HandlePlayerEnter(player);
                    }
                    else
                    {
                        _inside = false;
                        if (CurrentRoom == this) CurrentRoom = null;
                        SetSpawners(true);
                        if (restIndicator != null) restIndicator.SetActive(false);
                        Debug.Log($"[SaveRoom] Player left save room.");
                    }
                }
            }
        }

        if (_inside && _playerStats != null)
        {
            if (healRate > 0f && !_playerStats.IsFullyHealed())
            {
                _playerStats.HealHealthOnly(healRate * Time.deltaTime);
            }
        }
    }

    private void SetSpawners(bool active)
    {
        if (spawnersToSuppress == null) return;
        // Never re-enable spawners for a completed chapter. Once the player
        // has advanced past this chapter, the area should stay zombie-free
        // even if they re-enter the save room and leave again.
        if (active && IsChapterCompleted())
        {
            Debug.Log($"[SaveRoom] Ch{chapter} is completed — keeping spawners OFF.");
            active = false;
        }
        foreach (var s in spawnersToSuppress)
        {
            if (s != null) s.enabled = active;
        }
    }

    /// <summary>True if this save room's chapter has been completed (player advanced past it).</summary>
    private bool IsChapterCompleted()
    {
        if (chapter <= 0) return false;
        var sm = StoryManager.Instance;
        if (sm == null) return false;
        return sm.CurrentChapter > chapter;
    }

    /// <summary>Checkpoint position for external respawn systems.</summary>
    public Vector3 CheckpointPosition => _checkpointPos;

    /// <summary>
    /// The effective respawn position, accessible even before Start() runs.
    /// Uses respawnPoint if assigned, otherwise this transform's position.
    /// </summary>
    public Vector3 EffectiveRespawnPosition =>
        respawnPoint != null ? respawnPoint.position : transform.position;

    public void ReevaluateState(Vector3 playerPosition)
    {
        var trigger = GetComponent<Collider>();
        if (trigger != null && trigger.bounds.Contains(playerPosition))
        {
            _inside = true;
            CurrentRoom = this;
            _playerStats = FindAnyObjectByType<PlayerStats>();
            SetSpawners(false);
            if (restIndicator != null) restIndicator.SetActive(true);
            Debug.Log($"[SaveRoom] ReevaluateState: Player is inside save room. Suppressed spawners.");
        }
        else
        {
            _inside = false;
            if (restIndicator != null) restIndicator.SetActive(false);
            Debug.Log($"[SaveRoom] ReevaluateState: Player is outside save room.");
        }
    }
}
