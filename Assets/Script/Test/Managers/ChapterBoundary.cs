using UnityEngine;

/// <summary>
/// Marks a chapter's playable area and gates the player's progress using a
/// trigger volume plus a hard physical wall for FUTURE chapters only. The
/// boundary:
/// - Activates the chapter's zombie spawners when the player enters, and
///   deactivates them when the player leaves.
/// - Activates the pending chapter's first quest when the player enters (see
///   StoryManager.PendingChapterEntry).
/// - Enforces LINEAR progression: future chapters have their wall enabled from
///   the start. The player physically cannot enter a chapter they haven't
///   reached yet. When the chapter becomes current, the wall is disabled.
/// - COMPLETED chapters are NOT locked: the player can freely re-explore
///   them. Spawners stay OFF (no new zombies), quests don't re-trigger
///   (QuestTrigger.oneShot), but the player can walk around freely.
///
/// Place one ChapterBoundary per chapter, sized to cover that chapter's area.
/// The boundary's primary collider should be a trigger (BoxCollider) covering
/// the full chapter zone. Four thin, tall non-trigger BoxColliders (the
/// "walls") are created at runtime at the boundary edges and enabled only for
/// future (unreached) chapters.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ChapterBoundary : MonoBehaviour
{
    [Header("Chapter")]
    [Tooltip("Chapter number this boundary represents (1-5).")]
    public int chapter = 1;

    [Header("Spawners")]
    [Tooltip("Zombie spawners (Spawm components) that should be active while the player is inside this chapter.")]
    public MonoBehaviour[] spawners;

    [Header("Debug")]
    public Color gizmoColor = new Color(0.2f, 0.8f, 0.3f, 0.25f);

    /// <summary>True if this chapter has not been reached yet (wall is up).</summary>
    private bool _isFutureChapter;
    private Collider _triggerCol;
    /// <summary>Runtime-created physical (non-trigger) wall colliders — 4 thin
    /// tall walls at the boundary edges. Enabled only for future (unreached)
    /// chapters to prevent the player from skipping ahead. Completed chapters
    /// have no walls — the player can freely re-explore them.</summary>
    private BoxCollider[] _wallCols;
    private GameObject[] _wallGOs;
    /// <summary>True while an external system (e.g. WaveQuestInteractable) has locked the player in. Prevents OnTriggerExit from re-locking.</summary>
    private bool _externallyLocked;
    /// <summary>Last known position of the player while inside this boundary (for teleport-back on external lock).</summary>
    private Vector3 _lastInsidePos;
    /// <summary>Last known forward direction of the player while inside (for teleport-back orientation).</summary>
    private Quaternion _lastInsideRot;

    private float _checkTimer = 0f;
    private bool _isPlayerInside = false;

    private GameObject _cachedPlayer;
    private Rigidbody _cachedPlayerRb;

    private GameObject GetPlayer()
    {
        if (_cachedPlayer == null)
        {
            _cachedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (_cachedPlayer != null)
            {
                _cachedPlayerRb = _cachedPlayer.GetComponent<Rigidbody>();
            }
        }
        return _cachedPlayer;
    }

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void Awake()
    {
        _triggerCol = GetComponent<Collider>();
        SetupWallCollider();
    }

    /// <summary>
    /// Creates 4 thin, tall non-trigger BoxColliders at the boundary edges
    /// (X-, X+, Z-, Z+). These are the "walls" that hard-block entry when the
    /// chapter is locked or is a future chapter. Disabled by default.
    ///
    /// Using 4 edge walls instead of a single solid box prevents the player
    /// from wall-running on a nearby building, wall-jumping + double-jumping
    /// over the boundary height, and landing on an invisible "ceiling" above
    /// the chapter. The walls are tall (80+ units) so they cannot be cleared
    /// by any combination of jump / wall-jump / double-jump, and thin (1 unit)
    /// so they don't create a walkable surface above the chapter.
    /// </summary>
    private void SetupWallCollider()
    {
        Vector3 size, center;
        if (_triggerCol is BoxCollider triggerBox)
        {
            size = triggerBox.size;
            center = triggerBox.center;
        }
        else
        {
            // Fallback for non-box triggers: use a 60x20x60 default.
            size = new Vector3(60f, 20f, 60f);
            center = Vector3.zero;
        }

        float wallHeight = Mathf.Max(size.y + 100f, 150f);
        const float wallThickness = 1f;

        // 4 walls: X-, X+, Z-, Z+
        Vector3[] wallCenters = {
            new Vector3(center.x - size.x / 2f, center.y, center.z),
            new Vector3(center.x + size.x / 2f, center.y, center.z),
            new Vector3(center.x, center.y, center.z - size.z / 2f),
            new Vector3(center.x, center.y, center.z + size.z / 2f),
        };
        Vector3[] wallSizes = {
            new Vector3(wallThickness, wallHeight, size.z + wallThickness),
            new Vector3(wallThickness, wallHeight, size.z + wallThickness),
            new Vector3(size.x + wallThickness, wallHeight, wallThickness),
            new Vector3(size.x + wallThickness, wallHeight, wallThickness),
        };
        string[] wallNames = { "Wall_X-", "Wall_X+", "Wall_Z-", "Wall_Z+" };

        _wallCols = new BoxCollider[4];
        _wallGOs = new GameObject[4];

        for (int i = 0; i < 4; i++)
        {
            var wallGO = new GameObject($"Ch{chapter}_Boundary_{wallNames[i]}");
            wallGO.transform.SetParent(transform, false);
            wallGO.transform.localPosition = wallCenters[i];
            wallGO.tag = "Boundary";
            var col = wallGO.AddComponent<BoxCollider>();
            col.isTrigger = false;
            col.size = wallSizes[i];
            col.center = Vector3.zero;
            col.enabled = false;
            // Forward collision events to this ChapterBoundary so it can show
            // notifications when the player hits a wall.
            var forwarder = wallGO.AddComponent<WallCollisionForwarder>();
            forwarder.owner = this;
            _wallGOs[i] = wallGO;
            _wallCols[i] = col;
        }
    }

    /// <summary>Enable/disable all 4 edge walls at once.</summary>
    private void SetWallsEnabled(bool enabled)
    {
        if (_wallCols == null) return;
        foreach (var col in _wallCols)
        {
            if (col != null) col.enabled = enabled;
        }
    }

    private void Start()
    {
        // Fallback: re-subscribe in case OnEnable ran before StoryManager.Awake.
        Subscribe();

        SetSpawnersActive(false);

        var sm = StoryManager.Instance;
        var player = GetPlayer();

        // If this is a future chapter (not yet reached), enable the wall from
        // the start — but only if the player is NOT inside (safety: don't trap).
        _isFutureChapter = sm != null && chapter > sm.CurrentChapter;
        if (_isFutureChapter)
        {
            if (player == null || _triggerCol == null || !_triggerCol.bounds.Contains(player.transform.position))
            {
                SetWallsEnabled(true);
            }
        }

        // If the player starts inside this chapter (e.g., Ch1 at game start),
        // activate spawners and pending quest.
        if (player != null && _triggerCol != null && _triggerCol.bounds.Contains(player.transform.position))
        {
            _isPlayerInside = true;
            SetSpawnersActive(true);
            if (sm != null && sm.CurrentChapter == chapter && sm.PendingChapterEntry)
            {
                sm.ActivatePendingChapterQuest();
            }
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        }
    }

    /// <summary>
    /// Subscribe to StoryManager events. Called from OnEnable and again from
    /// Start as a fallback — if OnEnable ran before StoryManager.Awake (which
    /// sets Instance), the subscription was silently skipped. Start is
    /// guaranteed to run after all Awake calls, so Instance is always set.
    /// </summary>
    private void Subscribe()
    {
        if (StoryManager.Instance == null) return;
        // Avoid double-subscription.
        StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
    }

    private void Update()
    {
        // Safety: if externally locked and the player is somehow outside the
        // boundary (e.g. respawned outside after dying during waves), teleport
        // them back inside. This catches edge cases that OnTriggerExit might miss.
        if (_externallyLocked && _triggerCol != null)
        {
            var player = GetPlayer();
            if (player != null)
            {
                if (!_triggerCol.bounds.Contains(player.transform.position))
                {
                    Debug.Log($"[ChapterBoundary] Ch{chapter} player outside during external lock — teleporting back.");
                    if (_cachedPlayerRb != null)
                    {
                        _cachedPlayerRb.linearVelocity = Vector3.zero;
                        _cachedPlayerRb.angularVelocity = Vector3.zero;
                    }
                    player.transform.position = _lastInsidePos != Vector3.zero ? _lastInsidePos : transform.position;
                }
            }
        }

        // Periodically verify the player's presence in the trigger volume to heal desync bugs
        _checkTimer += Time.unscaledDeltaTime;
        if (_checkTimer >= 1f)
        {
            _checkTimer = 0f;
            var player = GetPlayer();
            if (player != null && _triggerCol != null)
            {
                bool isInside = _triggerCol.bounds.Contains(player.transform.position);
                if (isInside != _isPlayerInside)
                {
                    _isPlayerInside = isInside;
                    if (_isPlayerInside)
                    {
                        // Enable spawners only if this is the current chapter
                        // (not a future chapter and not a completed chapter).
                        var sm = StoryManager.Instance;
                        if (sm == null || sm.CurrentChapter == chapter)
                        {
                            SetSpawnersActive(true);
                            if (sm != null && sm.CurrentChapter == chapter && sm.PendingChapterEntry)
                            {
                                sm.ActivatePendingChapterQuest();
                            }
                        }
                    }
                    else
                    {
                        // Disable spawners when player leaves. Completed
                        // chapters are NOT locked — the player can re-enter
                        // freely, but spawners stay off (no new zombies).
                        SetSpawnersActive(false);
                    }
                }
            }
        }
    }

    private void HandleChapterChanged(int oldChapter, int newChapter)
    {
        // When this chapter becomes current, disable the wall so the player
        // can enter. Mark as no longer future.
        if (newChapter == chapter)
        {
            SetWallsEnabled(false);
            _isFutureChapter = false;
        }
        // If the player just LEFT this chapter (oldChapter == ours), force
        // disable spawners even if they are still physically inside our zone.
        // The periodic check in Update() only reacts to toggles of the bool,
        // so if the player was already inside and never moved it would keep
        // spawners alive. Reset the flag to force re-evaluation on next tick.
        //
        // NOTE: existing zombies already in the area are NOT flushed here —
        // the player may walk back to loot/explore and the area should still
        // feel alive with whatever zombies were already there. Only new spawns
        // are prevented.
        if (oldChapter == chapter)
        {
            SetSpawnersActive(false);
            _isPlayerInside = false;

            // Release any external lock (e.g. from CompanionZombieSiege) so
            // the player isn't teleported back into this chapter after advancing.
            if (_externallyLocked)
                UnlockExternal();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var sm = StoryManager.Instance;

        // If the player somehow enters a future chapter (the wall should
        // prevent this, but just in case), don't activate anything.
        if (sm != null && chapter > sm.CurrentChapter)
        {
            return;
        }

        // Only enable spawners for the CURRENT chapter. Completed chapters
        // (chapter < CurrentChapter) stay quiet — no new zombies.
        if (sm == null || sm.CurrentChapter == chapter)
        {
            SetSpawnersActive(true);
        }

        // Activate pending quest if this is the current chapter.
        if (sm != null && sm.CurrentChapter == chapter && sm.PendingChapterEntry)
        {
            sm.ActivatePendingChapterQuest();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        // Track the player's last known position inside the boundary so we can
        // teleport them back if they try to leave while externally locked.
        _lastInsidePos = other.transform.position;
        _lastInsideRot = other.transform.rotation;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // If an external system (e.g. WaveQuestInteractable) has locked the
        // player in, teleport them back inside.
        if (_externallyLocked)
        {
            TeleportPlayerBack(other);
            return;
        }

        // Disable spawners when the player leaves. Completed chapters are
        // NOT locked — the player can re-enter freely. Spawners stay off.
        SetSpawnersActive(false);
    }

    /// <summary>
    /// Called by <see cref="WallCollisionForwarder"/> when the player hits one
    /// of the 4 edge wall colliders. These walls only exist for future
    /// (unreached) chapters, so we always show the "finish current area first"
    /// notification.
    /// </summary>
    public void OnWallHit(Collision collision)
    {
        if (!collision.collider.CompareTag("Player")) return;

        // Wall only exists for future chapters — tell the player to finish
        // the current area first.
        SimpleNotification.Show("Tôi cần khám phá xong khu vực trước.");
        Debug.Log($"[ChapterBoundary] Ch{chapter} player hit future-chapter wall — showed 'finish current area first' notification.");
    }

    /// <summary>
    /// Teleports the player back to the last known position inside the boundary.
    /// Used when the player tries to leave during an external lock (e.g. Q7 waves).
    /// Falls back to the boundary center if no inside position was recorded.
    /// </summary>
    private void TeleportPlayerBack(Collider playerCol)
    {
        Vector3 target = _lastInsidePos != Vector3.zero ? _lastInsidePos : transform.position;
        var rb = playerCol.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        playerCol.transform.position = target;
        Debug.Log($"[ChapterBoundary] Ch{chapter} player teleported back inside to {target} (external lock).");
    }

    /// <summary>
    /// Externally lock the player inside the chapter area. Used by wave-based
    /// quests (e.g. Q7 generator) to prevent the player from leaving until all
    /// waves are cleared.
    ///
    /// IMPORTANT: This does NOT enable the full-area wall collider — that would
    /// trap the player inside a solid box (the collider covers the entire chapter
    /// area). Instead, it relies on OnTriggerExit to teleport the player back
    /// inside if they try to leave.
    /// </summary>
    public void LockExternal()
    {
        _externallyLocked = true;
        // Record the player's current position as the teleport-back point.
        var player = GetPlayer();
        if (player != null)
        {
            _lastInsidePos = player.transform.position;
            _lastInsideRot = player.transform.rotation;
        }
        Debug.Log($"[ChapterBoundary] Ch{chapter} externally locked — player cannot leave (teleport-back on exit).");
    }

    /// <summary>
    /// Release an external lock. After calling this, normal boundary behavior
    /// resumes and the player can leave the chapter area freely.
    /// </summary>
    public void UnlockExternal()
    {
        _externallyLocked = false;
        Debug.Log($"[ChapterBoundary] Ch{chapter} external lock released — player can leave.");
    }

    /// <summary>True if this chapter's quests are all done.</summary>
    private bool IsChapterComplete(StoryManager sm)
    {
        if (sm == null) return false;
        if (sm.CurrentChapter != chapter) return false;
        return sm.GetCurrentQuest() == null && sm.QuestsCompletedThisChapter > 0;
    }

    public void SetSpawnersActive(bool active)
    {
        if (spawners == null) return;
        foreach (var s in spawners)
        {
            if (s != null) s.enabled = active;
        }
    }

    public void ReevaluateState(Vector3 playerPosition)
    {
        var sm = StoryManager.Instance;
        // Future chapter — wall is up, player shouldn't be here.
        if (sm != null && chapter > sm.CurrentChapter)
        {
            _isPlayerInside = false;
            SetSpawnersActive(false);
            return;
        }

        if (_triggerCol != null && _triggerCol.bounds.Contains(playerPosition))
        {
            _isPlayerInside = true;
            // Only enable spawners for the current chapter (not completed ones).
            if (sm == null || sm.CurrentChapter == chapter)
            {
                SetSpawnersActive(true);
                if (sm != null && sm.CurrentChapter == chapter && sm.PendingChapterEntry)
                {
                    sm.ActivatePendingChapterQuest();
                }
            }
            Debug.Log($"[ChapterBoundary] Ch{chapter} ReevaluateState: Player is inside. Activated spawners.");
        }
        else
        {
            _isPlayerInside = false;
            SetSpawnersActive(false);
            Debug.Log($"[ChapterBoundary] Ch{chapter} ReevaluateState: Player is outside. Deactivated spawners.");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        var c = GetComponent<Collider>();
        if (c is BoxCollider bc)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(bc.center, bc.size);
        }
        else if (c is SphereCollider sc)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawSphere(sc.center, sc.radius);
        }
    }
}

/// <summary>
/// Tiny component placed on each edge wall child GameObject to forward
/// collision events to the parent <see cref="ChapterBoundary"/>. Needed
/// because the walls are on separate child GameObjects (Unity does not allow
/// two BoxColliders on the same GameObject).
/// </summary>
public class WallCollisionForwarder : MonoBehaviour
{
    public ChapterBoundary owner;

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.OnWallHit(collision);
    }
}
