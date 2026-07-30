using UnityEngine;
using UnityEngine.AI;
using cowsins;

/// <summary>
/// Một entry trong loot table. Mỗi entry roll độc lập theo
/// <see cref="dropChance"/>; nếu trúng thì rơi <see cref="minQuantity"/>..
/// <see cref="maxQuantity"/> bản sao của <see cref="prefab"/> (mỗi bản sao
/// pop hướng riêng). Có thể có nhiều entry trùng prefab.
/// </summary>
[System.Serializable]
public struct LootDropEntry
{
    [Tooltip("Prefab loot sẽ rơi (Coin, Experience, Healthpack, ...).")]
    public GameObject prefab;

    [Range(0, 100)]
    [Tooltip("Xác suất rơi entry này (0-100). Mỗi entry roll độc lập.")]
    public float dropChance;

    [Tooltip("Số bản sao tối thiểu nếu entry trúng (mặc định 1).")]
    public int minQuantity;

    [Tooltip("Số bản sao tối đa nếu entry trúng (mặc định 1).")]
    public int maxQuantity;
}

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class ZombieAI : MonoBehaviour, IDamageable, ICrookEnemy, IEnemyHealthReadout
{
    public float GetMaxHealth()
    {
        return maxHealth;
    }

    // ---- Health observation (read-only; for UI such as EnemyHealthBar) ----
    // Combat logic is unchanged; these only expose state already tracked internally.

    /// <summary>Current hit points (never negative for display).</summary>
    public int CurrentHealth
    {
        get { return Mathf.Max(0, currentHealth); }
    }

    /// <summary>Normalized health in [0,1]. 1 = full, 0 = dead/empty.</summary>
    public float HealthFraction
    {
        get { return maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f; }
    }

    /// <summary>True once this zombie has died (before it is pooled back to inactive).</summary>
    public bool IsDead
    {
        get { return isDead; }
    }

    /// <summary>Identifies if the enemy is Normal, Special, or Boss.</summary>
    public EnemyType EnemyType
    {
        get { return EnemyType.Normal; }
    }

    /// <summary>
    /// True while the zombie is actively chasing the player (detected).
    /// </summary>
    public bool IsChasing
    {
        get { return !isDead && hasDetectedPlayer; }
    }

    /// <summary>
    /// Raised whenever health changes (damage or death). Argument is the new
    /// <see cref="HealthFraction"/> in [0,1]. Subscribers must null-check the
    /// zombie's <see cref="IsDead"/> state to decide show/hide.
    /// </summary>
    public event System.Action<float> OnHealthChanged;

    [Header("Target")]
    public Transform target;

    [Header("Movement")]
    public float walkSpeed = 1.5f;
    public float runSpeed = 3.5f;
    public float detectDistance = 20f;
    public float attackDistance = 1.2f;

    [Header("Wander")]
    public float wanderRadius = 15f;
    public float wanderInterval = 5f;

    [Header("Erratic / Unpredictable Behavior")]
    [Tooltip("Khoảng cách mất dấu player. Lon hon detectDistance de chong flip-flop (hysteresis).")]
    public float loseSightDistance = 28f;
    [System.Obsolete("No longer used in simplified chase logic.")]
    [Tooltip("Sau khi mat dau, zombie van duoi theo alertMemoryDuration giay truoc khi quay ve Wander.")]
    public float alertMemoryDuration = 3f;
    [Tooltip("Kha nang trigger mot dot lunge (sprint dot ngot) moi giay khi dang duoi o mid-range (0-1).")]
    [Range(0f, 1f)]
    public float lungeChancePerSecond = 0.25f;
    [Tooltip("Toc do lunge = runSpeed * lungeSpeedMultiplier.")]
    public float lungeSpeedMultiplier = 1.8f;
    [Tooltip("Thoi luong moi dot lunge (giay).")]
    public float lungeDuration = 0.7f;
    [Tooltip("Kha nang trigger mot dot feint pause (dung dot ngot) moi giay khi dang duoi (0-1).")]
    [Range(0f, 1f)]
    public float feintPauseChancePerSecond = 0.12f;
    [Tooltip("Thoi luong moi dot feint pause (giay).")]
    public float feintPauseDuration = 0.4f;
    [Tooltip("Bien do jitter (zigzag) them vao path duoi player (met). 0 = tat.")]
    public float chaseJitterRadius = 2.5f;

    [Header("Line of Sight")]
    [Tooltip("If true, zombies require an unobstructed line of sight to the player before initial detection. Once detected, hysteresis and alert memory work as normal (no LOS needed).")]
    public bool requireLineOfSight = true;
    [Tooltip("Layer mask for objects that block the zombie's sight (walls, floors, furniture). Defaults to everything; the player and the zombie's own layer are automatically excluded from the check.")]
    public LayerMask sightObstructionMask = ~0;
    [Tooltip("Eye height offset from the zombie's pivot for the LOS raycast.")]
    public float sightEyeHeight = 1.5f;

    [Header("Stuck Recovery")]
    [Tooltip("How long (seconds) the zombie must be nearly stationary while chasing before it is considered stuck. Higher = more patient (less likely to re-path abruptly).")]
    public float stuckTimeThreshold = 1.2f;
    [Tooltip("If the zombie moves less than this distance (meters) over stuckTimeThreshold, it is considered stuck.")]
    public float stuckMoveThreshold = 1f;
    [Tooltip("How far to search for an intermediate re-path position when stuck (no teleport — just re-pathing).")]
    public float stuckRepathRadius = 5f;

    [Header("Chase Re-path (Responsiveness)")]
    [Tooltip("Khoảng cách tối thiểu (m) player phải di chuyển so với destination cuối cùng trước khi zombie re-path ngay lập tức. Nhỏ hơn = truy cập vị trí mới nhanh hơn nhưng tốn CPU pathfinding hơn.")]
    public float playerMovedRepathThreshold = 1f;
    [Tooltip("Interval tối đa (giây) giữa các lần re-path khi player đứng yên. Re-path ngay khi player di chuyển quá playerMovedRepathThreshold.")]
    public float maxRepathInterval = 0.1f;



    [Header("Attack")]
    public float attackCooldown = 1.5f;
    public int attackDamage = 20;

    [Header("Health")]
    public int maxHealth = 100;

    [Header("Audio")]
    public AudioClip growlClip;
    public AudioClip attackClip;
    public AudioClip hitClip;
    public AudioClip deathClip;
    public AudioClip footstepClip;

    [Header("Loot")]
    [Tooltip("Loot table mới: mỗi entry roll độc lập, có thể rơi 0..N loại cùng lúc. Để trống nếu muốn dùng dropPrefab/dropChance cũ bên dưới.")]
    public LootDropEntry[] lootTable;

    [Tooltip("Fallback khi lootTable trống: loot đơn lẻ theo dropChance.")]
    public GameObject dropPrefab;
    [Range(0, 100)]
    public float dropChance = 50f;
    [Tooltip("Khoảng cách nâng loot lên so với vị trí zombie khi rớt xuống.")]
    public float dropHeightOffset = 1.5f;
    [Tooltip("Bật hiệu ứng loot nhảy ra khỏi zombie khi chết.")]
    public bool popLootOnDeath = true;
    [Tooltip("Vận tốc đứng (lên) khi loot bị bắn ra (m/s).")]
    public float lootPopUpwardSpeed = 4.5f;
    [Tooltip("Vận tốc ngang tối đa khi loot bị bắn ra (m/s).")]
    public float lootPopHorizontalSpeed = 2.5f;

    [Header("Loot Trail Effect")]
    [Tooltip("Cấu hình vệt trail + glow particle khi loot bay. Chỉnh trực tiếp trên zombie.")]
    public LootTrailSettings lootTrailSettings = new LootTrailSettings();

    [Header("GiftBox (Endless Mode)")]
    public GameObject giftBoxPrefab;
    [Range(0, 100)]
    public float giftBoxDropChance = 10f;

    [Header("Rewards")]
    public float experienceReward = 10f;
    public float headshotBonusExperience = 5f;
    public int coinReward = 5;
    public int headshotBonusCoins = 5;

    private static Transform _cachedPlayer;
    private static IDamageable _cachedPlayerDamageable;
    private static PlayerStats _cachedPlayerStats;

    private Animator animator;
    private NavMeshAgent agent;
    private AudioSource audioSource;
    private Rigidbody rb;
    private Collider _collider;
    private IDamageable _targetDamageable;
    private PlayerStats _targetStats;
    private EnemyLocomotion locomotion;

    // Caching original values for scaling
    private Vector3 _originalScale;
    private float _originalAgentHeight;
    private float _originalAgentRadius;
    private float _originalSightEyeHeight;

    private float _originalDropHeightOffset;
    private float _originalWalkSpeed;
    private float _originalRunSpeed;
    private int _originalMaxHealth;

    private int currentHealth;

    private float attackTimer;
    private float wanderTimer;

    private bool isDead;
    private bool isAttacking;
    private bool hasDetectedPlayer;
    [Header("Debug/Testing")]
    [SerializeField] private bool forceDetectPlayer = true;
    private bool lastHitWasHeadshot;

    // --- Erratic behavior runtime state ---
    private float lungeTimer;           // >0 while a lunge burst is active
    private float feintPauseTimer;      // >0 while a feint pause is active
    private float erraticRollTimer;     // accumulates dt to roll lunge/feint once per second
    private Vector3 chaseJitterOffset;  // current jitter offset applied to chase destination
    private float chaseJitterTimer;     // when to pick a new jitter offset

    // --- Stuck recovery runtime state ---
    private bool _wasInAttackRange;     // tracks isStopped transition (attack→chase)



    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int AttackHash =
        Animator.StringToHash("Attack");

    private static readonly int HitHash =
        Animator.StringToHash("Hit");

    private static readonly int DeathHash =
        Animator.StringToHash("Death");

    private static readonly int AttackIndexHash =
        Animator.StringToHash("AttackIndex");

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        locomotion = GetComponent<EnemyLocomotion>();
        if (locomotion == null)
            locomotion = gameObject.AddComponent<EnemyLocomotion>();

        // Cache original values before scaling
        _originalScale = transform.localScale;
        if (agent != null)
        {
            _originalAgentHeight = agent.height;
            _originalAgentRadius = agent.radius;
        }
        _originalSightEyeHeight = sightEyeHeight;

        _originalDropHeightOffset = dropHeightOffset;
        _originalWalkSpeed = walkSpeed;
        _originalRunSpeed = runSpeed;
        _originalMaxHealth = maxHealth;

        // Zombies cast shadows (so they are visible on the ground during daytime)
        // but do not receive shadows (GPU saving at high counts).
        var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < smrs.Length; i++)
        {
            smrs[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            smrs[i].receiveShadows = false;
        }

        // Performance: don't evaluate the animator for zombies no camera renders.
        // NOTE: off-screen zombies won't play attack animations until visible (acceptable for a horde shooter; revert to CullUpdateTransforms if undesired).
        if (animator != null)
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    }

    void OnEnable()
    {
        if (_chasePath == null)
            _chasePath = new NavMeshPath();

        // Dynamically randomize height between 1.5m and 2.0m
        float defaultHeight = 1.8f;
        if (_collider is CapsuleCollider capCol)
        {
            defaultHeight = capCol.height;
        }
        float targetHeight = Random.Range(1.5f, 2.0f);
        float scaleFactor = targetHeight / defaultHeight;

        // Apply uniform scale to visual mesh and collider
        transform.localScale = _originalScale * scaleFactor;

        // Scale NavMeshAgent bounds
        if (agent != null)
        {
            agent.height = _originalAgentHeight * scaleFactor;
            agent.radius = _originalAgentRadius * scaleFactor;
        }

        // Scale height-based locomotion variables
        sightEyeHeight = _originalSightEyeHeight * scaleFactor;

        dropHeightOffset = _originalDropHeightOffset * scaleFactor;

        // Calculate health multiplier based on height
        // 1.5m -> 100% (1.0x), 2.0m -> 200% (2.0x)
        float healthMultiplier = 1.0f + (targetHeight - 1.5f) * 2.0f;
        maxHealth = Mathf.RoundToInt(_originalMaxHealth * healthMultiplier);

        // Dynamic speed based on Player's actual speeds
        float playerWalkSpeed = 3f;
        float playerRunSpeed = 6f;

        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
        }

        // Safety net: if the tagged Player is a static parent, resolve to the
        // moving child that has PlayerMovement + Rigidbody.
        if (target != null && target.GetComponent<cowsins.PlayerMovement>() == null)
        {
            var pm = target.GetComponentInChildren<cowsins.PlayerMovement>();
            if (pm != null)
                target = pm.transform;
        }

        Debug.Log($"[ZombieAI] {name} OnEnable target set: target={(target!=null?target.name:"null")} pos={(target!=null?target.position.ToString():"N/A")}");

        if (target != null)
        {
            var pm = target.GetComponent<cowsins.PlayerMovement>();
            if (pm != null)
            {
                playerWalkSpeed = pm.WalkSpeed;
                playerRunSpeed = pm.RunSpeed;
            }
        }

        walkSpeed = Random.Range(0.75f * playerWalkSpeed, 1.0f * playerWalkSpeed);
        runSpeed = Random.Range(1.0f * playerWalkSpeed, 1.5f * playerWalkSpeed);

        isDead = false;
        isAttacking = false;
        hasDetectedPlayer = false;
        lastHitWasHeadshot = false;
        currentHealth = maxHealth;
        attackTimer = 0f;
        wanderTimer = wanderInterval;

        // Reset erratic behavior state on (re)spawn.
        lungeTimer = 0f;
        feintPauseTimer = 0f;
        erraticRollTimer = 0f;
        chaseJitterOffset = Vector3.zero;
        chaseJitterTimer = 0f;

        // Reset stuck recovery state on (re)spawn.
        _wasInAttackRange = false;

        if (locomotion != null)
        {
            locomotion.target = target;
            locomotion.requireLineOfSight = requireLineOfSight;
            locomotion.sightObstructionMask = sightObstructionMask;
            locomotion.sightEyeHeight = sightEyeHeight;
            locomotion.stuckTimeThreshold = stuckTimeThreshold;
            locomotion.stuckMoveThreshold = stuckMoveThreshold;
            locomotion.stuckRepathRadius = stuckRepathRadius;
            locomotion.playerMovedRepathThreshold = playerMovedRepathThreshold;
            locomotion.maxRepathInterval = maxRepathInterval;


            locomotion.Initialize();
        }

        if (target == null)
        {
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                target = player.transform;
        }

        // Cache the player's IDamageable once per spawn so AttackHit doesn't
        // call GetComponent every hit (50+ zombies * 1.5s cooldown = ~33 hits/s).
        if (target != null)
        {
            _targetDamageable = target.GetComponent<IDamageable>();
            _targetStats = target.GetComponent<PlayerStats>();
        }
        else
        {
            _targetDamageable = null;
            _targetStats = null;
        }

        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
                agent.isStopped = false;
            agent.speed = walkSpeed;
            agent.stoppingDistance = attackDistance * 0.5f;
            // Let the agent control rotation during Wander; ChasePlayer overrides
            // via FaceTarget() which is fine since both point toward the player/path.
            agent.updateRotation = true;
            // Performance: HighQuality avoidance is ~quadratic with agent count; use cheap avoidance + varied priority.
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(30, 70);

            if (locomotion != null)
            {
                locomotion.WarpIfUnderground();
            }

            // If the agent still isn't on the NavMesh (spawn position off-mesh),
            // try to find a valid surface nearby to prevent silent pathing failures.
            if (!agent.isOnNavMesh)
            {
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(transform.position, out navHit, 5f, NavMesh.AllAreas))
                {
                    agent.Warp(navHit.position);
                    transform.position = navHit.position;
                }
            }
        }

        // Keep the Rigidbody KINEMATIC while alive so the NavMeshAgent fully
        // controls movement. A non-kinematic Rigidbody with gravity fights the
        // agent on stairs/slopes: gravity pulls the zombie down, the capsule
        // collider catches on step edges, and the two systems jitter — the
        // zombie gets stuck at the foot of stairs. Kinematic mode lets the
        // agent drive position smoothly while still sending collision events
        // (for hit detection, triggers, etc.). Die() already sets kinematic
        // for the death animation, so this just makes it consistent.
        if (rb != null)
        {
            rb.isKinematic = true;
            // Keep gravity on so that if the agent is disabled (e.g. on death)
            // and kinematic is later turned off, the body can fall naturally.
            rb.useGravity = true;
        }

        Collider col = _collider;
        if (col != null)
            col.enabled = true;

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        if (AIDirector.Instance != null)
            AIDirector.Instance.RegisterZombie(this);
    }

    void OnDisable()
    {
        CancelInvoke();
        if (AIDirector.Instance != null)
            AIDirector.Instance.UnregisterZombie(this);
        lootTrailSettings = new LootTrailSettings();
    }


    private float cachedDistance = 100f;

    void Update()
    {
        if (isDead)
            return;

        if (target == null)
        {
            if (_cachedPlayer == null || _cachedPlayer.gameObject == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    _cachedPlayer = playerObj.transform;
                    _cachedPlayerDamageable = playerObj.GetComponent<IDamageable>();
                    _cachedPlayerStats = playerObj.GetComponent<PlayerStats>();
                }
            }
            target = _cachedPlayer;
            _targetDamageable = _cachedPlayerDamageable;
            _targetStats = _cachedPlayerStats;
        }
        if (target == null)
            return;

        if (locomotion != null)
        {
            locomotion.target = target;
        }

        if (agent != null && !agent.isOnNavMesh)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 10f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
                transform.position = navHit.position;
            }
            return;
        }

        attackTimer += Time.deltaTime;
        wanderTimer += Time.deltaTime;

        // Compute distance every frame — Vector3.Distance is extremely cheap
        // (a few arithmetic ops) and using a stale 0.2s cache causes the zombie
        // to react late when the player moves in/out of detect/attack range.
        cachedDistance = Vector3.Distance(transform.position, target.position);

        // First-time detection requires both distance AND line of sight (if
        // requireLineOfSight is enabled).
        bool hasLOSCurrently = false;
        if (!hasDetectedPlayer && cachedDistance <= detectDistance)
        {
            if (!requireLineOfSight)
            {
                hasLOSCurrently = true;
            }
            else
            {
                hasLOSCurrently = locomotion != null && locomotion.HasLineOfSight();
            }
        }

        // Target (Player) Dead Check
        bool isTargetDead = false;
        if (target != null)
        {
            var targetStats = _targetStats;
            if (targetStats == null)
            {
                targetStats = target.GetComponent<PlayerStats>();
                _targetStats = targetStats;
            }
            if (targetStats != null && targetStats.IsDead)
            {
                isTargetDead = true;
            }
        }

        if (isTargetDead)
        {
            if (hasDetectedPlayer)
            {
                wanderTimer = wanderInterval; // Force immediate destination selection to clear isStopped
            }
            Wander();
            hasDetectedPlayer = false;
        }
        else if (forceDetectPlayer)
        {
            // Test Mode: Always chase and detect player
            ChasePlayer(cachedDistance);
            hasDetectedPlayer = true;
        }
        else if (hasDetectedPlayer && cachedDistance <= loseSightDistance)
        {
            // Keep chasing Player's real position as long as detected and in range
            ChasePlayer(cachedDistance);
        }
        else if (!hasDetectedPlayer && hasLOSCurrently)
        {
            // Initial detection: player enters detection range and is visible
            ChasePlayer(cachedDistance);
        }
        else
        {
            if (hasDetectedPlayer)
            {
                wanderTimer = wanderInterval; // Force immediate destination selection
            }
            Wander();
            hasDetectedPlayer = false;
        }

        float targetAnimSpeed = agent.velocity.magnitude / runSpeed;

        if (animator != null)
            animator.SetFloat(SpeedHash, targetAnimSpeed, 0.15f, Time.deltaTime);
    }

    private float pathTimer = 0f;
    private Vector3 _lastSetDestination = Vector3.zero;
    private NavMeshPath _chasePath;

    void ChasePlayer(float distance)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            Debug.LogWarning($"[ZombieAI] {name} ChasePlayer skipped: agent={(agent!=null?"ok":"null")} isOnNavMesh={(agent!=null?agent.isOnNavMesh.ToString():"N/A")}");
            return;
        }

        if (target == null)
        {
            Debug.LogWarning($"[ZombieAI] {name} ChasePlayer skipped: target is null");
            return;
        }

        Debug.Log($"[ZombieAI] {name} ChasePlayer: dist={distance:F1} target.name={target.name} target.pos={target.position}");

        if (!hasDetectedPlayer)
        {
            PlaySound(growlClip);
            hasDetectedPlayer = true;
        }



        // Take over rotation from the agent so FaceTarget() and the agent
        // don't fight over transform.rotation (causes visual sliding when
        // the zombie faces the player while the path moves it sideways).
        if (agent.updateRotation)
            agent.updateRotation = false;

        // --- Roll lunge / feint once per second for unpredictability ---
        erraticRollTimer += Time.deltaTime;
        if (erraticRollTimer >= 1f)
        {
            erraticRollTimer = 0f;

            // Only roll lunge when at mid-range (not too close, not too far) and not already lunging.
            if (lungeTimer <= 0f &&
                distance > attackDistance + 1.5f &&
                distance < detectDistance * 0.8f &&
                Random.value < lungeChancePerSecond)
            {
                lungeTimer = lungeDuration;
            }

            // Only roll feint when actively moving toward player and not already pausing/lunging.
            if (feintPauseTimer <= 0f &&
                lungeTimer <= 0f &&
                distance > attackDistance + 2f &&
                Random.value < feintPauseChancePerSecond)
            {
                feintPauseTimer = feintPauseDuration;
            }
        }

        // Tick down active burst/pause timers.
        if (lungeTimer > 0f)
            lungeTimer -= Time.deltaTime;
        if (feintPauseTimer > 0f)
            feintPauseTimer -= Time.deltaTime;

        // --- Feint pause: stop abruptly, face the player, then resume ---
        if (feintPauseTimer > 0f)
        {
            if (!agent.isStopped)
                Debug.Log($"[ZombieAI] {name} entered feint pause. Stopping agent.");
            agent.isStopped = true;
            FaceTarget();
            return;
        }

        // Face movement direction while chasing (not attacking) so the zombie
        // looks where it's going, not at the player. FaceTarget is only used
        // when in attack range (needs to face player to hit them).
        if (distance <= attackDistance)
        {
            if (!agent.isStopped)
                Debug.Log($"[ZombieAI] {name} entered attack range (distance={distance:F2}). Stopping agent.");
            agent.isStopped = true;
            _wasInAttackRange = true;
            FaceTarget(); // face player to attack

            if (!isAttacking &&
                attackTimer >= attackCooldown)
            {
                Debug.Log($"[ZombieAI] {name} triggers Attack.");
                Attack();
            }
        }
        else
        {
            // Transitioning from attack range to chase: the agent's previous
            // path was completed while isStopped=true, so hasPath is false and
            // SetDestination alone may silently fail to create a new path
            // (a known Unity NavMeshAgent issue). Reset the path state and
            // force an immediate re-path on this frame.
            if (_wasInAttackRange)
            {
                Debug.Log($"[ZombieAI] {name} exited attack range (distance={distance:F2}). Resetting path.");
                _wasInAttackRange = false;
                agent.ResetPath();
                agent.isStopped = false;
                pathTimer = 0.25f; // force SetDestination this frame
                if (locomotion != null)
                    locomotion.ForceRecalculateLOS(); // force LOS recheck
            }
            else
            {
                if (agent.isStopped)
                    Debug.Log($"[ZombieAI] {name} resuming chase movement. Setting isStopped=false.");
                agent.isStopped = false;
            }

            // Lunge burst: sudden sprint faster than normal run.
            float speed = runSpeed;
            if (lungeTimer > 0f)
                speed = runSpeed * lungeSpeedMultiplier;
            agent.speed = speed;

            // --- Jitter the chase destination so the zombie doesn't run in a straight line ---
            // Only apply jitter when far enough from the player. Near attack range,
            // jitter causes the destination to shift by up to chaseJitterRadius,
            // which combined with stoppingDistance creates a flip-flop: the agent
            // stops at stoppingDistance from the jittered dest (which may be
            // further from the player than attackDistance), then the code sees
            // distance > attackDistance and chases again -> micro-sliding.
            chaseJitterTimer -= Time.deltaTime;
            if (chaseJitterTimer <= 0f)
            {
                // Pick a new lateral jitter offset around the player.
                Vector3 rand = Random.insideUnitSphere * chaseJitterRadius;
                rand.y = 0f;
                chaseJitterOffset = rand;
                chaseJitterTimer = Random.Range(0.4f, 0.9f);
            }

            pathTimer += Time.deltaTime;

            // Don't re-path while a path is already being calculated (pathPending).
            bool canRepath = !agent.pathPending && (locomotion == null || !locomotion.IsRecoveringFromStuck || !agent.hasPath);

            // Slower repathing when player is out of sight (no LOS) to save CPU
            bool hasLOS = locomotion != null && locomotion.HasLineOfSight();
            float dynamicInterval = Mathf.Lerp(0.15f, 0.4f, Mathf.Clamp01((distance - 5f) / 15f));
            float dynamicThreshold = Mathf.Lerp(1.0f, 2.0f, Mathf.Clamp01((distance - 5f) / 15f));
            if (!hasLOS)
            {
                dynamicInterval *= 2.0f;
                dynamicThreshold *= 1.5f;
            }

            float distToLastDest = Vector3.Distance(target.position, _lastSetDestination);

            if (canRepath && (pathTimer >= dynamicInterval || distToLastDest > dynamicThreshold))
            {
                // Disable jitter when close to attack range to prevent flip-flop.
                Vector3 dest = target.position;
                // Sample to the ground-level NavMesh near the player so
                // zombies path to reachable terrain even when the player is
                // standing on a disconnected raised surface (car roof, etc.).
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(new Vector3(dest.x, 0f, dest.z), out navHit, 2f, NavMesh.AllAreas))
                    dest = navHit.position;
                // If the destination is still unreachable (partial path),
                // use the nearest reachable NavMesh corner so the zombie
                // at least advances toward the player's vicinity instead
                // of cycling pathPending deadlocks.
                _chasePath.ClearCorners();
                if (NavMesh.CalculatePath(agent.transform.position, dest, NavMesh.AllAreas, _chasePath)
                    && _chasePath.status == NavMeshPathStatus.PathPartial
                    && _chasePath.corners.Length > 1)
                {
                    dest = _chasePath.corners[_chasePath.corners.Length - 1];
                }
                if (distance > attackDistance + chaseJitterRadius + 1f)
                    dest += chaseJitterOffset;

                SetDestinationRobust(dest);
                _lastSetDestination = dest;
                pathTimer = 0f;
            }

            // --- Stuck detection: if the zombie isn't making progress toward
            // the player while it should be chasing, try to recover by finding
            // a nearby valid NavMesh position and re-pathing. Handles cases
            // where the zombie is wedged against a wall, furniture, or door.
            HandleStuckDetection(distance);

            // Face movement direction (NavMesh path velocity) while chasing.
            FaceMovementDirection();
        }
    }

    void Wander()
    {
        if (locomotion != null)
        {
            locomotion.ExitDirectSteering();
        }

        agent.speed = walkSpeed;

        // Let the agent handle rotation during wander (ChasePlayer disabled it).
        if (!agent.updateRotation)
            agent.updateRotation = true;

        if (wanderTimer < wanderInterval)
            return;

        Vector3 destination =
            RandomNavSphere(
                transform.position,
                wanderRadius);

        SetDestinationRobust(destination);

        wanderTimer = 0f;
    }

    void Attack()
    {
        isAttacking = true;
        attackTimer = 0;

        animator.SetInteger(
            AttackIndexHash,
            Random.Range(0, 2));

        animator.SetTrigger(AttackHash);

        PlaySound(attackClip);

        Invoke(nameof(ResetAttack),
            attackCooldown);
    }

    void ResetAttack()
    {
        isAttacking = false;
    }

    public void AttackHit()
    {
        if (target == null)
            return;

        float distance =
            Vector3.Distance(
                transform.position,
                target.position);

        if (distance > attackDistance + 0.5f)
            return;

        // Use the cached IDamageable from OnEnable. Re-resolve only as a fallback
        // (e.g. if the player was swapped at runtime).
        IDamageable damageable = _targetDamageable;
        if (damageable == null && target != null)
        {
            damageable = target.GetComponent<IDamageable>();
            _targetDamageable = damageable;
        }

        if (damageable != null)
        {
            damageable.Damage(
                attackDamage,
                false);

            // Feed the directional damage indicator (engine Damage carries no direction).
            if (DamageDirectionHUD.Instance != null)
                DamageDirectionHUD.Instance.ShowDamageFrom(transform.position);
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;

        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterDamageDealt(damage);

        animator.SetTrigger(HitHash);

        PlaySound(hitClip);

        if (currentHealth <= 0)
        {
            Die();
        }

        // Notify observers (e.g. EnemyHealthBar). Die() above runs synchronously
        // and sets isDead, so a killing blow reports IsDead=true here.
        if (OnHealthChanged != null)
            OnHealthChanged(HealthFraction);
    }

    public void Damage(float damage,
        bool isHeadshot)
    {
        // Capture headshot/critical flag so Die() can award the bonus.
        // Damage -> TakeDamage -> Die run synchronously, so this reflects the killing blow.
        lastHitWasHeadshot = isHeadshot;

        if (CombatFeedbackHUD.Instance != null)
            CombatFeedbackHUD.Instance.ShowHit(transform.position, damage, isHeadshot);

        TakeDamage(
            Mathf.RoundToInt(Mathf.Abs(damage)));
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (CombatFeedbackHUD.Instance != null)
            CombatFeedbackHUD.Instance.ShowKill("Zombie");

        if (AIDirector.Instance != null)
            AIDirector.Instance.RegisterKill();

        if (WaveManager.Instance != null)
            WaveManager.Instance.RegisterZombieKill();

        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterZombieKill();

        // Achievement tracking: check wall-run state before the kill is fully processed.
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.NotifyZombieKill();
            // Check if the player is currently wall-running. Use the cached
            // target's PlayerMovement instead of FindAnyObjectByType (which
            // scans the whole hierarchy on every zombie death).
            if (target != null)
            {
                var pm = target.GetComponent<PlayerMovement>();
                if (pm != null && pm.IsWallRunning)
                    AchievementManager.Instance.NotifyZombieKillWhileWallRunning();
            }
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddKill();
            if (lastHitWasHeadshot)
                ScoreManager.Instance.AddCrit();
        }

        // Player progression via the real Cowsins ExperienceManager (XP -> level -> skill points -> upgrades).
        if (ExperienceManager.Instance != null && ExperienceManager.Instance.useExperience)
        {
            float xp = experienceReward;
            if (lastHitWasHeadshot)
                xp += headshotBonusExperience;
            ExperienceManager.Instance.AddExperience(xp);
        }

        if (CoinManager.Instance != null && CoinManager.Instance.useCoins)
        {
            int coins = coinReward;
            if (lastHitWasHeadshot)
                coins += headshotBonusCoins;
            CoinManager.Instance.AddCoins(coins);
        }

        PlaySound(deathClip);

        // Rigidbody is already kinematic from OnEnable (so the NavMeshAgent
        // can drive movement on stairs without physics fighting it). Keep it
        // kinematic here so the death animation plays in place after the
        // agent is disabled.
        if (rb != null)
            rb.isKinematic = true;

        agent.isStopped = true;
        agent.enabled = false;

        animator.SetTrigger(DeathHash);

        Collider col = _collider;

        if (col != null)
            col.enabled = false;

        TryDropLoot();

        Invoke(nameof(DeactivateZombie), 6f);
    }

    void DeactivateZombie()
    {
        gameObject.SetActive(false);
    }

    void TryDropLoot()
    {
        LootDropHelper.TryDropLoot(
            lootTable,
            dropPrefab,
            dropChance,
            transform.position,
            dropHeightOffset,
            popLootOnDeath,
            lootPopUpwardSpeed,
            lootPopHorizontalSpeed,
            lootTrailSettings);

        LootDropHelper.TryDropGiftBox(
            transform.position,
            dropHeightOffset,
            giftBoxPrefab,
            giftBoxDropChance,
            popLootOnDeath,
            lootPopUpwardSpeed,
            lootPopHorizontalSpeed,
            lootTrailSettings);
    }

    //==================================================
    // STUCK DETECTION & RECOVERY
    //==================================================

    private void HandleStuckDetection(float distanceToPlayer)
    {
        if (locomotion != null)
            locomotion.HandleStuckDetection(distanceToPlayer, attackDistance * 0.5f);
    }

    private void SetDestinationRobust(Vector3 destination)
    {
        if (locomotion != null)
            locomotion.SetDestinationRobust(destination);
    }

    //==================================================
    // LINE OF SIGHT
    //==================================================

    private bool HasLineOfSight()
    {
        if (locomotion != null)
            return locomotion.HasLineOfSight();
        return false;
    }

    void FaceTarget()
    {
        if (locomotion != null)
            locomotion.FaceTarget(8f);
    }

    void FaceMovementDirection()
    {
        if (locomotion != null)
            locomotion.FaceMovementDirection(8f);
    }

    public void PlayFootstep()
    {
        PlaySound(footstepClip);
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    public static Vector3 RandomNavSphere(Vector3 origin, float distance)
    {
        return EnemyLocomotion.RandomNavSphere(origin, distance);
    }


}