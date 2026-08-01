using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using cowsins;
using System.Collections;

/// <summary>
/// Companion (ally) NPC that follows the player and fights zombies with a
/// shotgun (raycast spread). Implements IDamageable + IEnemyHealthReadout so
/// the existing EnemyHealthBar world-space UI can render its health bar.
///
/// The companion never truly dies: when HP reaches 1 it enters a Downed state
/// for <see cref="downedDuration"/> seconds, then revives at 50% HP.
///
/// State machine:
///   Waiting     — idle at spawn, waiting for player to interact (E) and decide.
///   Following   — follows the player via NavMeshAgent, keeps followDistance.
///   Shooting    — sub-state of Following: fires shotgun at nearest enemy in range.
///   Downed      — incapacitated, revives after downedDuration.
///   WalkingAway — paths to deadEndPoint, then self-destructs (player refused).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class CompanionAI : MonoBehaviour, IDamageable, IEnemyHealthReadout
{
    public static readonly List<CompanionAI> ActiveCompanions = new List<CompanionAI>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitOnLoad()
    {
        ActiveCompanions.Clear();
    }

    public enum State { Waiting, Following, Downed, WalkingAway }
    public enum FireMode { Shotgun, Burst }

    [Header("Identity")]
    [Tooltip("Tag of hostile targets the companion will shoot.")]
    public string enemyTag = "Enemy";

    [Header("Health")]
    public int maxHealth = 150;
    public int currentHealth { get; private set; }
    public bool IsDead => false; // Companion never truly dies.

    [Header("Following")]
    public float followSpeed = 3.5f;
    public float followDistance = 4f;
    public float repathInterval = 0.25f;
    [Tooltip("How long the companion stops to shoot before resuming movement (L4D2 style).")]
    public float shootStopDuration = 0.35f;
    [Tooltip("Smooth time for Speed animator parameter.")]
    public float speedSmoothTime = 0.15f;
    [Tooltip("If player is farther than this, companion abandons combat to catch up (L4D2 style).")]
    public float abandonCombatDistance = 12f;
    [Tooltip("If distance to player exceeds this, the companion teleports behind the player instead of walking.")]
    public float teleportDistance = 30f;
    [Tooltip("How far behind the player the companion teleports to.")]
    public float teleportBehindOffset = 3f;

    [Header("Fire Mode")]
    [Tooltip("Shotgun = all pellets at once. Burst = fire pellets one by one with burstInterval.")]
    public FireMode fireMode = FireMode.Shotgun;
    [Tooltip("Time (seconds) between individual bullets during a burst.")]
    public float burstInterval = 0.08f;

    [Header("Combat — Shotgun")]
    [Tooltip("Range at which the companion can detect and shoot enemies.")]
    public float detectRange = 20f;
    public float shootCooldown = 1.5f;
    public int shotgunPellets = 8;
    public float shotgunSpreadDeg = 8f;
    public float shotgunRange = 20f;
    public int shotgunDamagePerRay = 18;
    [Tooltip("Muzzle flash effect prefab (spawned at gun barrel, once per shot).")]
    public GameObject muzzleFlashPrefab;
    [Tooltip("Impact effect prefab (spawned at each hit point). Optional — runtime sparks used as fallback.")]
    public GameObject impactPrefab;
    [Tooltip("Audio clip played when firing.")]
    public AudioClip shootClip;

    [Header("Downed / Revive")]
    public float downedDuration = 30f;
    public float reviveHealthFraction = 0.5f;

    [Header("Rescue by Player (hold E)")]
    [Tooltip("How long the player must hold E near the downed companion to revive it.")]
    public float rescueHoldDuration = 3f;
    [Tooltip("Maximum distance from the downed companion within which the player can rescue.")]
    public float rescueMaxDistance = 3f;
    [Tooltip("Key the player must hold to rescue the downed companion.")]
    public KeyCode rescueKey = KeyCode.E;
    [Tooltip("Health fraction restored when the player rescues the companion (1 = full HP).")]
    public float rescueHealthFraction = 1f;
    [Tooltip("Dialogue line spoken by the companion after being rescued.")]
    [TextArea(1, 3)]
    public string rescuedThankLine = "Cảm ơn vì đã cứu tôi.";
    [Tooltip("How long (seconds) the thank-you dialogue stays visible before fading.")]
    public float rescuedThankHoldDuration = 2f;

    [Header("Walking Away (refused)")]
    [Tooltip("Destination the companion walks to when the player refuses.")]
    public Vector3 deadEndPoint = new Vector3(60.62f, 0f, -21.49f);
    public float destroyDistance = 1.5f;
    public float fadeOutDuration = 0.5f;

    [Header("Damage From Enemies")]
    [Tooltip("How often (seconds) nearby enemies deal damage to the companion.")]
    public float enemyDamageTickInterval = 3f;
    [Tooltip("Khoảng thời gian tối thiểu (giây) giữa 2 lần NPC nhận sát thương.")]
    public float damageCooldown = 3.0f;
    [Tooltip("Damage per tick per nearby enemy within enemyDamageRadius.")]
    public int enemyDamagePerTick = 10;
    public float enemyDamageRadius = 2f;

    [Header("Weapon IK")]
    [Tooltip("Name of the gun child under the right hand bone (e.g. CompanionShotgun, CompanionSMG).")]
    public string gunChildName = "CompanionShotgun";
    [Tooltip("Local position of the left-hand grip point on the gun (forestock / foregrip).")]
    public Vector3 leftHandGripPosition = new Vector3(0f, 0f, 0.37f);

    [Header("Animator")]
    public Animator animator;

    [Header("Muzzle")]
    [Tooltip("Forward offset from muzzle transform (or gun pivot) to barrel tip. 0 = use exact child position.")]
    public float muzzleOffset = 0.4f;

    [Header("Reload")]
    [Tooltip("Number of shots before the companion reloads.")]
    public int magazineSize = 8;
    [Tooltip("Reload duration in seconds (must match Reload animation length).")]
    public float reloadDuration = 3.3f;

    public EnemyType EnemyType => EnemyType.Special;
    public float HealthFraction => maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
    public event System.Action<float> OnHealthChanged;
    public event System.Action<State> OnStateChanged;
    /// <summary>Raised with normalized 0..1 progress while the player holds E to rescue. 0 = stopped, 1 = complete.</summary>
    public event System.Action<float> OnRescueProgressChanged;

    public void CancelRescue()
    {
        _rescueProgress = 0f;
        isBeingRescued = false;
        OnRescueProgressChanged?.Invoke(0f);
    }

    public void NotifyRescueProgress(float progress)
    {
        isBeingRescued = progress > 0f;
        _rescueProgress = progress * rescueHoldDuration;
        OnRescueProgressChanged?.Invoke(Mathf.Clamp01(progress));
    }
    /// <summary>Raised the first time the player rescues the companion by holding E. Lets dialogue triggers switch to "thanks" small talk.</summary>
    public event System.Action OnRescuedByPlayer;

    /// <summary>True once the player has rescued this companion at least once (hold E while Downed).</summary>
    public bool RescuedByPlayer { get; private set; }

    public State CurrentState
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            OnStateChanged?.Invoke(_state);
        }
    }

    private State _state = State.Waiting;
    private NavMeshAgent _agent;
    private AudioSource _audio;
    private Transform _player;
    private float _shootTimer;
    private float _repathTimer;
    private float _enemyDamageTimer;
    private float _lastHitTime = -999f;
    private float _downedTimer;
    private float _shootStopTimer; // Stops movement while shooting (L4D2 style)
    private int _ammoRemaining; // Shots left before reload
    private float _reloadTimer; // Counts down during reload; companion can't shoot while > 0
    private float _rescueProgress;
    private bool isBeingRescued = false; // 0..rescueHoldDuration — accumulated while player holds E near downed companion
    private DialogueBubble _bubble; // Cached for rescue thank-you dialogue
    private CompanionDialogueTrigger _dialogueTrigger;
    private Collider _interactCollider; // Trigger collider on layer Interactable — disabled while Downed so InteractManager ignores the companion
    private cowsins.InputManager _playerInput; // Cached player InputManager (Input System) for reading the Interacting action
    private float _speedVelocity; // SmoothDamp velocity for Speed parameter
    private float _currentAnimSpeed; // Current smoothed Speed value
    private float _ikWeight; // Smoothed IK weight for left hand grip
    private Transform _rootBone; // Skeleton root bone (for fixing sink issue)
    private Transform _leftHandGrip; // Grip target on the gun (for IK)
    private Transform _muzzleTransform; // Cached muzzle (silencer/barrel child) for raycast origin
    private Coroutine _burstCoroutine; // Active burst fire coroutine
    private Collider[] _enemyBuffer = new Collider[32];
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AimHash = Animator.StringToHash("Aim");
    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int DownedHash = Animator.StringToHash("Downed");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int ReviveHash = Animator.StringToHash("Revive");
    private static readonly int ReloadHash = Animator.StringToHash("Reload");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int DashBackHash = Animator.StringToHash("DashBack");

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _audio = GetComponent<AudioSource>();
        _bubble = GetComponent<DialogueBubble>();
        _dialogueTrigger = GetComponent<CompanionDialogueTrigger>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _ammoRemaining = magazineSize;

        // Cache the trigger collider on the Interactable layer. This is what
        // InteractManager raycasts against to show the "Nói chuyện" prompt.
        // We disable it while Downed so InteractManager completely ignores the
        // companion (no prompt, no E consumption) — leaving E free for rescue.
        _interactCollider = GetComponent<Collider>();
        // CRITICAL: Disable root motion — NavMeshAgent controls position.
        // Root motion from Mixamo animations causes the model to sink into the ground.
        if (animator != null) animator.applyRootMotion = false;
        // Cache the skeleton root bone so we can fix its Y in LateUpdate.
        // Mixamo animations drive the Root bone to y=-0.96, causing the model
        // to sink into the ground. We override this every frame after animation.
        _rootBone = animator != null ? animator.transform.Find("Root") : null;

        // Find the left-hand grip target on the gun.
        // Uses gunChildName (configurable) and leftHandGripPosition.
        if (animator != null)
        {
            var rh = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rh != null)
            {
                var gun = rh.Find(gunChildName);
                if (gun != null)
                {
                    var gripGO = new GameObject("LeftHandGrip");
                    gripGO.transform.SetParent(gun, false);
                    gripGO.transform.localPosition = leftHandGripPosition;
                    _leftHandGrip = gripGO.transform;
                }
            }
        }
    }

    private void LateUpdate()
    {
        // Fix model sinking: Mixamo animations push the Root bone down to y=-0.96.
        // After the Animator applies pose, reset the Root bone Y so feet stay on ground.
        // When Downed (idle crouching), the animation root is at y=0.526 instead of
        // 0.955 — we need to offset so the character doesn't float or sink.
        if (_rootBone != null)
        {
            Vector3 pos = _rootBone.localPosition;
            // Use animator bool (more reliable than _state for LateUpdate timing).
            bool isDowned = animator != null && animator.GetBool(DownedHash);
            if (isDowned)
            {
                // idle crouching RootT.y=0.526 vs idle aiming RootT.y=0.955
                // Offset = -(0.955 - 0.526) = -0.429, plus extra -0.03 to plant feet
                pos.y = -0.46f;
            }
            else
            {
                pos.y = 0f;
            }
            _rootBone.localPosition = pos;
        }
    }

    private void OnEnable()
    {
        if (!ActiveCompanions.Contains(this)) ActiveCompanions.Add(this);
        currentHealth = maxHealth;
        _lastHitTime = Time.time;
        _agent.speed = followSpeed;
        _agent.acceleration = 8f; // Smooth acceleration (L4D2 style)
        _agent.angularSpeed = 360f; // Turn quickly to face enemies
        _agent.stoppingDistance = followDistance * 0.5f;
        // Disable agent auto-rotation — we handle facing manually (like ZombieAI)
        // so the companion faces its movement direction while chasing (not the
        // player), preventing spine twisting when the player runs in circles.
        _agent.updateRotation = false;
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        _agent.avoidancePriority = 50;
    }

    private void OnDisable()
    {
        ActiveCompanions.Remove(this);
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        if (_player != null) return;
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;
        ResolvePlayerInput();
    }

    /// <summary>
    /// Resolves the player's InputManager (Input System). The InputManager is a
    /// sibling of the tagged "Player" object under the same parent (e.g.
    /// "Player/InputManager" vs "Player/Player"), so we search the root parent's
    /// children. Safe to call repeatedly — returns early once found.
    /// </summary>
    private void ResolvePlayerInput()
    {
        if (_playerInput != null) return;
        if (_player == null) return;
        var p = _player.gameObject;
        _playerInput = p.GetComponentInParent<cowsins.InputManager>();
        if (_playerInput == null && p.transform.parent != null)
            _playerInput = p.transform.parent.GetComponentInChildren<cowsins.InputManager>();
        if (_playerInput == null)
            _playerInput = p.GetComponentInChildren<cowsins.InputManager>();
    }

    private void Update()
    {
        FindPlayer();
        if (_player == null) return;

        switch (_state)
        {
            case State.Waiting:
                SetAgentStopped(true);
                SetAnimSpeed(0f);
                break;
            case State.Following:
                UpdateFollowing();
                break;
            case State.Downed:
                UpdateDowned();
                break;
            case State.WalkingAway:
                UpdateWalkingAway();
                break;
        }
    }

    // ---- Following + Combat ----

    private void UpdateFollowing()
    {
        // Tick enemy damage (nearby zombies hurt the companion).
        _enemyDamageTimer -= Time.deltaTime;
        if (_enemyDamageTimer <= 0f)
        {
            _enemyDamageTimer = enemyDamageTickInterval;
            TakeDamageFromNearbyEnemies();
        }

        // L4D2 style: if player is too far, abandon combat and catch up.
        float distToPlayer = Vector3.Distance(transform.position, _player.position);
        bool playerTooFar = distToPlayer > abandonCombatDistance;

        // Teleport if the companion falls too far behind.
        if (distToPlayer > teleportDistance)
        {
            Vector3 behindPlayer = _player.position - _player.forward * teleportBehindOffset;
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(behindPlayer, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
                _agent.Warp(hit.position);
            else
                _agent.Warp(behindPlayer);
            distToPlayer = Vector3.Distance(transform.position, _player.position);
            playerTooFar = distToPlayer > abandonCombatDistance;
        }

        // Adjust agent speed based on distance: walk when close, run when far.
        // This creates smooth idle->walk->run blend transitions (like Zombie).
        if (distToPlayer > followDistance * 2f)
            _agent.speed = followSpeed;           // Run (Speed ≈ 1.0)
        else
            _agent.speed = followSpeed * 0.5f;    // Walk (Speed ≈ 0.5)

        if (playerTooFar)
        {
            // Don't interrupt reload or shoot-in-progress — finish the action
            // first, THEN follow.  Prevents "running while reloading" glitch.
            if (_reloadTimer > 0f || _shootStopTimer > 0f)
            {
                if (_reloadTimer > 0f) _reloadTimer -= Time.deltaTime;
                if (_shootStopTimer > 0f) _shootStopTimer -= Time.deltaTime;
                SetAgentStopped(true);
                if (animator != null) animator.SetBool(AimHash, true);
                var target = FindNearestEnemy();
                if (target != null) FacePosition(target.position, 10f);
            }
            else
            {
                // No combat action — follow player immediately.
                if (animator != null) animator.SetBool(AimHash, false);
                SetAgentStopped(false);
                _repathTimer -= Time.deltaTime;
                if (_repathTimer <= 0f)
                {
                    _repathTimer = repathInterval;
                    _agent.SetDestination(_player.position);
                }
                FaceMovementDirection(8f);
            }
        }
        else
        {
            // Tick down reload timer — companion can't shoot while reloading.
            if (_reloadTimer > 0f)
            {
                _reloadTimer -= Time.deltaTime;
                SetAgentStopped(true);
            }

            // Player is close enough — can afford to stop and shoot.
            _shootTimer -= Time.deltaTime;
            if (_shootTimer <= 0f && _reloadTimer <= 0f)
            {
                var target = FindNearestEnemy();
                if (target != null)
                {
                    if (_ammoRemaining <= 0)
                    {
                        // Out of ammo — reload.
                        _shootTimer = reloadDuration;
                        _reloadTimer = reloadDuration;
                        _shootStopTimer = reloadDuration;
                        PlayReload();
                    }
                    else
                    {
                        _shootTimer = shootCooldown;
                        // Only stop if not already stopped — prevents burst
                        // mode (fast cooldown) from permanently locking the
                        // companion in place (each shot renews the timer).
                        if (_shootStopTimer <= 0f)
                        {
                            _shootStopTimer = fireMode == FireMode.Burst
                                ? Mathf.Max(shootStopDuration, burstInterval * shotgunPellets + 0.1f)
                                : shootStopDuration;
                        }
                        ShootAt(target);
                    }
                }
            }

            // L4D2 style: stop moving while shooting, face the enemy.
            if (_shootStopTimer > 0f)
            {
                _shootStopTimer -= Time.deltaTime;
                SetAgentStopped(true);
                if (animator != null) animator.SetBool(AimHash, true);

                // Face the nearest enemy while shooting (like ZombieAI.FaceTarget).
                var target = FindNearestEnemy();
                if (target != null)
                {
                    FacePosition(target.position, 10f);
                }
            }
            else
            {
                // Resume following player.
                if (animator != null) animator.SetBool(AimHash, false);
                SetAgentStopped(false);
                _repathTimer -= Time.deltaTime;
                if (_repathTimer <= 0f)
                {
                    _repathTimer = repathInterval;
                    _agent.SetDestination(_player.position);
                }

                // Face movement direction (like ZombieAI.FaceMovementDirection)
                // so the companion looks where it's going, not at the player.
                // This prevents spine twisting when the player runs in circles.
                FaceMovementDirection(8f);
            }
        }

        // Set Speed parameter directly from agent velocity for snappy response.
        // SmoothDamp was too slow — agent reaches full speed in ~0.1s but the
        // smoothed Speed parameter lagged behind by several seconds.
        float targetSpeed = (_agent != null && _agent.isOnNavMesh && _agent.isStopped) ? 0f : Mathf.Clamp01(_agent.velocity.magnitude / followSpeed);
        SetAnimSpeed(targetSpeed);
    }

    private Transform FindNearestEnemy()
    {
        // Use Enemy layer mask (layer 7) so we only detect enemies, not environment.
        // Using ~0 (all layers) fills the buffer with environment colliders and
        // the zombie may not fit in the 32-slot buffer.
        int enemyLayerMask = 1 << LayerMask.NameToLayer("Enemy");
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, detectRange, _enemyBuffer, enemyLayerMask, QueryTriggerInteraction.Ignore);
        int shootMask = GetShootMask();
        Vector3 origin = GetMuzzleOrigin();
        Transform best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var col = _enemyBuffer[i];
            if (col == null) continue;
            if (!col.CompareTag(enemyTag)) continue;
            // Skip dead enemies.
            var dmg = col.GetComponent<IDamageable>();
            if (dmg is IEnemyHealthReadout readout && readout.IsDead) continue;
            // Line-of-sight check: only consider enemies the companion can
            // actually hit. Without this the companion wastes ammo firing at
            // enemies behind walls.
            Vector3 losDir = (col.bounds.center - origin).normalized;
            bool clearLos;
            if (Physics.Raycast(origin, losDir, out var losHit, shotgunRange, shootMask, QueryTriggerInteraction.Ignore))
            {
                clearLos = losHit.collider == col ||
                           losHit.collider.transform.IsChildOf(col.transform) ||
                           col.transform.IsChildOf(losHit.collider.transform);
            }
            else
            {
                // Raycast missed entirely — either nothing solid is between
                // the muzzle and the target, or the muzzle is inside the
                // enemy's collider at point-blank range. Both count as clear
                // line of sight.
                clearLos = col.bounds.Contains(origin);
            }
            if (!clearLos) continue;
            float sqr = (col.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = col.transform; }
        }
        return best;
    }

    private void ShootAt(Transform target)
    {
        if (animator != null) animator.CrossFade("Shoot", 0.1f, 0, 0f);
        if (shootClip != null && _audio != null) _audio.PlayOneShot(shootClip);
        if (_ammoRemaining > 0) _ammoRemaining--;

        if (fireMode == FireMode.Burst)
        {
            if (_burstCoroutine != null) StopCoroutine(_burstCoroutine);
            _burstCoroutine = StartCoroutine(FireBurst(target));
            return;
        }

        // Shotgun mode: fire all pellets at once.
        FirePellets(target);
    }

    /// <summary>Fire shotgunPellets at the target (single burst of all pellets).</summary>
    private void FirePellets(Transform target)
    {
        Vector3 origin = GetMuzzleOrigin();
        Vector3 baseDir = GetBaseDir(target, origin);
        int shootMask = GetShootMask();
        var targetCollider = target.GetComponent<Collider>();

        for (int i = 0; i < shotgunPellets; i++)
        {
            Vector3 dir = ApplySpread(baseDir, shotgunSpreadDeg);
            FireOneRay(origin, dir, shootMask, targetCollider, out _, out _);
        }
        SpawnMuzzleFlash(origin, baseDir);
    }

    /// <summary>Burst fire coroutine: fires shotgunPellets bullets one by one with burstInterval.</summary>
    private System.Collections.IEnumerator FireBurst(Transform target)
    {
        Vector3 origin = GetMuzzleOrigin();
        Vector3 baseDir = GetBaseDir(target, origin);
        int shootMask = GetShootMask();
        var targetCollider = target.GetComponent<Collider>();

        for (int i = 0; i < shotgunPellets; i++)
        {
            Vector3 dir = ApplySpread(baseDir, shotgunSpreadDeg);
            FireOneRay(origin, dir, shootMask, targetCollider, out bool hit, out Vector3 hitPoint);
            if (i == 0) SpawnMuzzleFlash(origin, baseDir);
            yield return new WaitForSeconds(burstInterval);
        }

        _burstCoroutine = null;
    }

    /// <summary>Fire a single ray and apply damage. Resolves target via collider.Raycast for guaranteed hit.</summary>
    private void FireOneRay(Vector3 origin, Vector3 dir, int shootMask, Collider targetCollider, out bool hasHit, out Vector3 hitPoint)
    {
        hasHit = false;
        hitPoint = origin + dir * shotgunRange;

        // Line-of-sight check toward the target's center using the full shoot
        // mask (includes walls/environment). If something solid sits between
        // the muzzle and the target, the shot is blocked — even though the
        // spread-compensating collider.Raycast below would otherwise hit it.
        // This prevents the companion from shooting through walls.
        bool lineOfSight = false;
        if (targetCollider != null)
        {
            Vector3 losDir = (targetCollider.bounds.center - origin).normalized;
            if (Physics.Raycast(origin, losDir, out var losHit, shotgunRange, shootMask, QueryTriggerInteraction.Ignore))
            {
                lineOfSight = losHit.collider == targetCollider ||
                              losHit.collider.transform.IsChildOf(targetCollider.transform) ||
                              targetCollider.transform.IsChildOf(losHit.collider.transform);
            }
            else
            {
                // Raycast missed entirely — nothing solid between the muzzle
                // and the target (e.g. muzzle is inside the target's collider
                // at point-blank range). Treat as clear line of sight.
                lineOfSight = true;
            }
        }

        if (lineOfSight && targetCollider != null && targetCollider.Raycast(new Ray(origin, dir), out var targetHit, shotgunRange))
        {
            hasHit = true;
            hitPoint = targetHit.point;
            var dmg = targetHit.collider.GetComponent<IDamageable>();
            if (dmg != null)
            {
                bool headshot = targetHit.collider.CompareTag("Critical");
                dmg.Damage(shotgunDamagePerRay, headshot);
            }
        }
        else if (Physics.Raycast(origin, dir, out var hit, shotgunRange, shootMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform))
            {
                hasHit = true;
                hitPoint = hit.point;
                var dmg = hit.collider.GetComponent<IDamageable>();
                if (dmg != null)
                {
                    bool headshot = hit.collider.CompareTag("Critical");
                    dmg.Damage(shotgunDamagePerRay, headshot);
                }
            }
        }

        SpawnTracer(origin, hitPoint);
        if (hasHit) SpawnImpact(hitPoint);
    }

    private Transform GetMuzzleTransform()
    {
        if (_muzzleTransform != null) return _muzzleTransform;
        if (animator != null)
        {
            var rh = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rh != null)
            {
                var gun = rh.Find(gunChildName);
                if (gun != null)
                {
                    // Try common muzzle child names
                    foreach (Transform child in gun)
                    {
                        if (child.name.Contains("Silencer") || child.name.Contains("Muzzle") || child.name.Contains("Barrel"))
                        {
                            _muzzleTransform = child;
                            return _muzzleTransform;
                        }
                    }
                    // Fallback: farthest child along gun's forward
                    _muzzleTransform = gun;
                }
            }
        }
        return null;
    }

    private Vector3 GetMuzzleOrigin()
    {
        var muzzle = GetMuzzleTransform();
        if (muzzle != null) return muzzle.position + muzzle.forward * muzzleOffset;
        return transform.position + Vector3.up * 1.5f;
    }

    private Vector3 GetBaseDir(Transform target, Vector3 origin)
    {
        var targetCollider = target.GetComponent<Collider>();
        Vector3 aimPoint;
        if (targetCollider != null)
            aimPoint = targetCollider.bounds.center;
        else
            aimPoint = target.position + Vector3.up * 1f;
        return (aimPoint - origin).normalized;
    }

    private int GetShootMask()
    {
        int mask = ~0;
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) mask &= ~(1 << playerLayer);
        int companionLayer = gameObject.layer;
        mask &= ~(1 << companionLayer);
        return mask;
    }

    private void SpawnMuzzleFlash(Vector3 origin, Vector3 baseDir)
    {
        if (muzzleFlashPrefab != null)
        {
            var fx = Instantiate(muzzleFlashPrefab, origin, Quaternion.LookRotation(baseDir));
            Destroy(fx, 1f);
        }
    }

    private void SpawnTracer(Vector3 from, Vector3 to)
    {
        var go = new GameObject("Tracer");
        var lr = go.AddComponent<LineRenderer>();
        lr.startWidth = 0.04f;
        lr.endWidth = 0.01f;
        lr.positionCount = 2;
        lr.SetPositions(new Vector3[] { from, to });
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(1f, 0.9f, 0.4f, 0.8f);
        lr.endColor = new Color(1f, 0.6f, 0f, 0f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        Destroy(go, 0.15f);
    }

    private void SpawnImpact(Vector3 point)
    {
        if (impactPrefab != null)
        {
            var impact = Instantiate(impactPrefab, point, Quaternion.identity);
            Destroy(impact, 1.5f);
            return;
        }

        // Runtime spark effect (fallback).
        var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.transform.position = point;
        spark.transform.localScale = Vector3.one * 0.08f;
        Destroy(spark.GetComponent<SphereCollider>());
        var mr = spark.GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Sprites/Default"));
        mr.material.color = new Color(1f, 0.8f, 0.2f, 1f);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Destroy(spark, 0.3f);
    }

    /// <summary>Plays the Reload animation and refills the magazine when complete.</summary>
    private void PlayReload()
    {
        if (animator != null) animator.CrossFade("Reload", 0.15f, 0, 0f);
        // Refill ammo immediately (the reload timer gates the next shot).
        _ammoRemaining = magazineSize;
    }

    private static Vector3 ApplySpread(Vector3 dir, float spreadDeg)
    {
        float spreadRad = spreadDeg * Mathf.Deg2Rad;
        float yaw = Random.Range(-spreadRad, spreadRad);
        float pitch = Random.Range(-spreadRad, spreadRad);
        var rot = Quaternion.Euler(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0f);
        return rot * dir;
    }

    private void TakeDamageFromNearbyEnemies()
    {
        // Use Enemy layer mask (layer 7) to only detect enemies.
        int enemyLayerMask = 1 << LayerMask.NameToLayer("Enemy");
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, enemyDamageRadius, _enemyBuffer, enemyLayerMask, QueryTriggerInteraction.Ignore);
        int attackers = 0;
        for (int i = 0; i < count; i++)
        {
            var col = _enemyBuffer[i];
            if (col == null) continue;
            if (!col.CompareTag(enemyTag)) continue;
            var readout = col.GetComponent<IEnemyHealthReadout>();
            if (readout != null && readout.IsDead) continue;
            attackers++;
        }
        if (attackers > 0)
        {
            int dmg = enemyDamagePerTick;
            Damage(dmg, false);
        }
    }

    // ---- Downed / Revive ----

    private void UpdateDowned()
    {
        SetAgentStopped(true);
        SetAnimSpeed(0f);

        // ---- Player rescue (hold E) ----
        // Runs in parallel with the auto-revive timer. If the player holds E
        // within rescueMaxDistance for rescueHoldDuration seconds, the companion
        // is revived at rescueHealthFraction (default full HP). If the timer
        // expires first, auto-revive kicks in at reviveHealthFraction (50%).
        bool rescuing = false;
        if (_player != null)
        {
            // Re-resolve the InputManager if it wasn't found yet (e.g. it was
            // not ready during Start).
            ResolvePlayerInput();
            float dist = Vector3.Distance(transform.position, _player.position);
            // Read the Interacting action from the player's InputManager (Input
            // System). Fallback to Input.GetKey for Input Manager mode.
            bool eHeld = _playerInput != null
                ? _playerInput.Interacting
                : Input.GetKey(rescueKey);
            if (dist <= rescueMaxDistance && eHeld)
            {
                rescuing = true;
                _rescueProgress += Time.deltaTime;
                float normalized = Mathf.Clamp01(_rescueProgress / rescueHoldDuration);
                OnRescueProgressChanged?.Invoke(normalized);
                if (_rescueProgress >= rescueHoldDuration)
                {
                    OnRescueProgressChanged?.Invoke(0f); // Reset UI before revive
                    Revive(rescueHealthFraction, byPlayer: true);
                    return;
                }
            }
        }
        if (!rescuing && _rescueProgress > 0f)
        {
            // Player released E or walked away — reset progress.
            _rescueProgress = 0f;
            OnRescueProgressChanged?.Invoke(0f);
        }

        // ---- Auto-revive timer ----
        _downedTimer -= Time.deltaTime;
        if (_downedTimer <= 0f)
        {
            OnRescueProgressChanged?.Invoke(0f);
            Revive(reviveHealthFraction, byPlayer: false);
        }
    }

    /// <summary>
    /// Revives the companion from the Downed state.
    /// </summary>
    /// <param name="healthFraction">Fraction of maxHealth to restore (0.5 = 50%, 1 = full).</param>
    /// <param name="byPlayer">If true, the companion thanks the player via DialogueBubble.</param>
    public void Revive(float healthFraction, bool byPlayer = false)
    {
        if (CurrentState != State.Downed) return;
        _lastHitTime = Time.time;
        _rescueProgress = 0f;
        isBeingRescued = false;
        OnRescueProgressChanged?.Invoke(0f);

        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.radius = 0.4f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
        }
        if (byPlayer)
        {
            bool firstRescue = !RescuedByPlayer;
            RescuedByPlayer = true;
            if (firstRescue) OnRescuedByPlayer?.Invoke();
        }
        currentHealth = Mathf.RoundToInt(maxHealth * Mathf.Clamp01(healthFraction));
        OnHealthChanged?.Invoke(HealthFraction);
        CurrentState = State.Following;
        // Re-enable the interaction collider so the player can talk to the
        // companion again (dialogue prompt reappears).
        if (_interactCollider != null) _interactCollider.enabled = true;
        // Restore interactable (set to false by CompanionDialogueTrigger.Update
        // while Downed) so the cowsins InteractManager can detect the NPC again.
        if (_dialogueTrigger != null) _dialogueTrigger.interactable = true;
        if (animator != null)
        {
            // Reset Downed bool FIRST so AnyState transition stops firing.
            animator.SetBool(DownedHash, false);
            // Then trigger Revive to play revive animation.
            animator.SetTrigger(ReviveHash);
        }
        if (byPlayer && _bubble != null && !string.IsNullOrEmpty(rescuedThankLine))
        {
            // Show thank-you dialogue, auto-hide after rescuedThankHoldDuration.
            // Speech bubbles do NOT freeze time (see DialogueBubble.Show), so
            // the player can keep moving/fighting while it fades away.
            _bubble.ShowSpeech(rescuedThankLine, rescuedThankHoldDuration);
            Debug.Log("[CompanionAI] Rescued by player. Revived at " + (healthFraction * 100f) + "% HP.");
        }
        else
        {
            Debug.Log("[CompanionAI] Auto-revived at " + (healthFraction * 100f) + "% HP.");
        }
    }

    // ---- Walking Away (refused) ----

    private void UpdateWalkingAway()
    {
        SetAgentStopped(false);
        _agent.SetDestination(deadEndPoint);
        SetAnimSpeed(Mathf.Clamp01(_agent.velocity.magnitude / followSpeed));

        float dist = Vector3.Distance(transform.position, deadEndPoint);
        if (dist <= destroyDistance)
        {
            StartCoroutine(FadeAndDestroy());
        }
    }

    private IEnumerator FadeAndDestroy()
    {
        var smrs = GetComponentsInChildren<SkinnedMeshRenderer>();
        var mrs = GetComponentsInChildren<MeshRenderer>();
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float a = 1f - (t / fadeOutDuration);
            foreach (var smr in smrs)
            {
                if (smr != null) { var c = smr.material.color; c.a = a; smr.material.color = c; }
            }
            foreach (var mr in mrs)
            {
                if (mr != null) { var c = mr.material.color; c.a = a; mr.material.color = c; }
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    // ---- IDamageable ----

    public void Damage(float damage, bool isHeadshot)
    {
        if (_state == State.Downed || _state == State.WalkingAway) return;
        if (Time.time - _lastHitTime < damageCooldown) return;
        _lastHitTime = Time.time;
        currentHealth -= Mathf.RoundToInt(damage);
        OnHealthChanged?.Invoke(HealthFraction);

        if (currentHealth <= 1)
        {
            currentHealth = 1;
            EnterDowned();
        }
        else if (animator != null && _state == State.Following)
        {
            animator.CrossFade("Hit", 0.1f, 0, 0f);
        }
    }

    private void EnterDowned()
    {
        CurrentState = State.Downed;
        // Stop burst coroutine so the companion doesn't keep firing while downed.
        if (_burstCoroutine != null)
        {
            StopCoroutine(_burstCoroutine);
            _burstCoroutine = null;
        }
        _downedTimer = downedDuration;
        _shootStopTimer = 0f;
        _rescueProgress = 0f;
        OnRescueProgressChanged?.Invoke(0f);
        // Disable the interaction collider so InteractManager stops detecting
        // the companion (no "Nói chuyện" prompt, no E consumption). This frees
        // the E key for the rescue hold in UpdateDowned.
        if (_interactCollider != null) _interactCollider.enabled = false;
        // Reset dialogue triggers so the player can retry questions after revive.
        var staffTrigger = GetComponent<CleaningStaffDialogueTrigger>();
        if (staffTrigger != null) staffTrigger.ResetQuiz();
        var compTrigger = GetComponent<CompanionDialogueTrigger>();
        if (compTrigger != null) compTrigger.ResetConsumed();
        if (animator != null)
        {
            // Reset ALL triggers to prevent stale transitions.
            animator.ResetTrigger(HitHash);
            animator.ResetTrigger(ShootHash);
            animator.ResetTrigger(ReloadHash);
            animator.ResetTrigger(DeathHash);
            animator.ResetTrigger(DashBackHash);
            animator.ResetTrigger(ReviveHash);
            // Set Downed bool LAST so AnyState transition fires correctly.
            animator.SetBool(DownedHash, true);
        }
        Debug.Log("[CompanionAI] Downed! Will revive in " + downedDuration + "s.");
    }

    // ---- Public API (called by CompanionManager / DialogueTrigger) ----

    public void StartFollowing()
    {
        _lastHitTime = Time.time;
        CurrentState = State.Following;
    }

    public void WalkAway(Vector3 destination)
    {
        deadEndPoint = destination;
        CurrentState = State.WalkingAway;
    }

    /// <summary>Teleport the companion near the player (used on chapter changes).</summary>
    public void TeleportNearPlayer(float offset = 2.5f)
    {
        if (_player == null) return;
        _agent.enabled = false;
        // Try multiple positions around the player to find valid NavMesh.
        Vector3[] candidates = {
            _player.position - _player.forward * offset,
            _player.position + _player.forward * offset,
            _player.position - _player.right * offset,
            _player.position + _player.right * offset,
            _player.position
        };
        bool found = false;
        foreach (var pos in candidates)
        {
            if (NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                found = true;
                break;
            }
        }
        if (!found)
        {
            // Last resort: sample anywhere within 10m of the player.
            if (NavMesh.SamplePosition(_player.position, out var fallback, 10f, NavMesh.AllAreas))
                transform.position = fallback.position;
            else
                transform.position = _player.position;
        }
        _agent.enabled = true;
        SetAgentStopped(true);
        _repathTimer = 0.5f;
    }

    /// <summary>
    /// Safely sets _agent.isStopped — only when the agent is active and on a
    /// NavMesh. Setting isStopped on an agent not placed on a NavMesh throws
    /// "Stop can only be called on an active agent that has been placed on a
    /// NavMesh" (e.g. right after TeleportNearPlayer re-enables the agent but
    /// before it has sampled a position on the NavMesh).
    /// </summary>
    private void SetAgentStopped(bool stopped)
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = stopped;
    }

    private void SetAnimSpeed(float speed)
    {
        if (animator == null) return;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        _currentAnimSpeed = Mathf.SmoothDamp(_currentAnimSpeed, speed, ref _speedVelocity, speedSmoothTime, float.MaxValue, dt);
        animator.SetFloat(SpeedHash, _currentAnimSpeed);
    }

    /// <summary>
    /// Smoothly rotates the companion to face the given world position.
    /// Mirrors EnemyLocomotion.FaceTarget — used to face enemies while shooting.
    /// </summary>
    private void FacePosition(Vector3 worldPos, float rotSpeed)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * rotSpeed);
    }

    /// <summary>
    /// Smoothly rotates the companion to face its movement direction (NavMeshAgent
    /// velocity or steering target). Mirrors EnemyLocomotion.FaceMovementDirection —
    /// used while chasing so the companion looks where it's going, not at the
    /// player. Prevents spine twisting when the player runs in circles.
    /// </summary>
    private void FaceMovementDirection(float rotSpeed)
    {
        Vector3 moveDir = Vector3.zero;
        if (_agent.velocity.sqrMagnitude > 0.5f)
            moveDir = _agent.velocity;
        else if (_agent.hasPath)
            moveDir = _agent.steeringTarget - transform.position;

        moveDir.y = 0f;
        if (moveDir.sqrMagnitude < 0.01f)
        {
            // Not moving — face the player instead (idle pose).
            if (_player != null) FacePosition(_player.position, rotSpeed);
            return;
        }

        Quaternion rot = Quaternion.LookRotation(moveDir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * rotSpeed);
    }

    /// <summary>
    /// Pins the left hand to the shotgun forestock via IK so the companion
    /// always grips the rifle with both hands, regardless of animation pose.
    /// </summary>
    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || _leftHandGrip == null) return;

        float targetWeight = (_state == State.Downed) ? 0f : 1f;
        _ikWeight = Mathf.MoveTowards(_ikWeight, targetWeight, Time.deltaTime * 8f);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _ikWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _ikWeight);
        if (_ikWeight > 0.01f)
        {
            animator.SetIKPosition(AvatarIKGoal.LeftHand, _leftHandGrip.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, _leftHandGrip.rotation);
        }
    }
}
