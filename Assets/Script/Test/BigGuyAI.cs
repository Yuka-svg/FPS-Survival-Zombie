using UnityEngine;
using UnityEngine.AI;
using cowsins;

/// <summary>
/// Big Guy — a mini-boss inspired by a loving father who wore a princess dress
/// to make his daughter happy before the infection. A hulking, slow, tanky
/// zombie that stands dazed (Dazed state) at a fixed point until provoked.
/// When disturbed (player approaches within provokeRadius, or gets shot),
/// he roars briefly then walks toward the player slowly but relentlessly.
/// Unlike the Witch, once provoked he NEVER returns to Dazed — he chases
/// until he or the player is dead.
///
/// Mini-boss stats: health 80, speed 3 m/s, damage 30, scream 2.27s.
/// Tuned for smooth, heavy motion: low acceleration, slow turning, and
/// a sub-1 chase animation multiplier for a deliberate, menacing walk.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class BigGuyAI : MonoBehaviour, IDamageable, ISpecialEnemy, IEnemyHealthReadout
{
    // ---- IEnemyHealthReadout (read-only; for EnemyHealthBar) ----
    public float HealthFraction
    {
        get { return maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f; }
    }
    public bool IsDead { get { return isDead; } }
    public EnemyType EnemyType { get { return EnemyType.Special; } }
    public event System.Action<float> OnHealthChanged;

    [Header("Target")]
    public Transform target;

    [Header("Health")]
    public int maxHealth = 80;
    public int currentHealth;

    [Header("Movement")]
    public float walkSpeed = 3.5f;
    public float rotationSpeed = 8f;

    [Header("Detection")]
    [Tooltip("Distance the player must approach to provoke the Big Guy (transitions from Dazed to Scream).")]
    public float provokeRadius = 10f;

    [Header("Line of Sight")]
    [Tooltip("If true, the Big Guy requires clear LOS to the player before proximity-based provoke.")]
    public bool requireLineOfSight = true;
    [Tooltip("Layer mask for sight obstruction objects (walls, furniture). Defaults to everything.")]
    public LayerMask sightObstructionMask = ~0;
    [Tooltip("Eye height from pivot for the LOS raycast.")]
    public float sightEyeHeight = 1.5f;

    [Header("Attack")]
    public float attackRange = 2.5f;
    public float attackCooldown = 2.5f;
    public float attackDamage = 30f;
    [Tooltip("Delay after triggering Attack before applying damage. Now driven by AnimationEvent 'BigGuyAttackHit' on the Mutant Punch clip; this is a fallback if the event is missing.")]
    public float damageApplyDelay = 1.0f;
    [Tooltip("How long the attack animation plays before returning to Walk (seconds). Should be slightly shorter than the Mutant Punch clip length (~1.1s) so the Attack→Walk transition fires before the clip freezes on its last frame. If this is shorter than attackCooldown, the Big Guy walks between attacks.")]
    public float attackAnimDuration = 1.0f;

    [Header("Scream")]
    [Tooltip("Duration of the roar before starting to walk toward the player (seconds). Matches the Zombie Scream clip length (2.27s).")]
    public float screamDuration = 2.27f;

    [Header("Stuck Recovery")]
    public float stuckTimeThreshold = 1.2f;
    public float stuckMoveThreshold = 1f;
    public float stuckRepathRadius = 5f;

    [Header("Chase Re-path (Responsiveness)")]
    [Tooltip("Minimum distance (m) the player must move relative to the last destination before the Big Guy immediately re-paths.")]
    public float playerMovedRepathThreshold = 1f;
    [Tooltip("Maximum interval (seconds) between re-paths when the player is standing still.")]
    public float maxRepathInterval = 0.1f;

    [Header("Animation Smoothing")]
    [Tooltip("Damping time for the Speed animator parameter (lower = snappier, higher = smoother).")]
    public float animSpeedDamping = 0.18f;
    [Tooltip("Scales Walk animation playback speed by actual agent speed. Requires Speed parameter active on Walk state.")]
    public bool scaleAnimBySpeed = true;
    [Tooltip("Multiplier applied to animator.speed when chasing (below 1 = heavy, sluggish walk; above 1 = frantic).")]
    public float chaseAnimSpeedMultiplier = 0.85f;
    [Tooltip("Grace period (s) after entering Dazed before the agent fully stops, for smoother deceleration.")]
    public float stopDecelTime = 0.45f;

    [Header("Audio")]
    [Tooltip("Roaring sound when provoked.")]
    public AudioClip screamClip;
    [Tooltip("Sound when attacking (punching).")]
    public AudioClip attackClip;
    [Tooltip("Sound when dying.")]
    public AudioClip deathClip;

    [Header("Loot")]
    [Tooltip("Loot table: each entry rolls independently, can drop 0..N types at once.")]
    public LootDropEntry[] lootTable;
    [Tooltip("Fallback when lootTable is empty: single loot based on dropChance.")]
    public GameObject dropPrefab;
    [Range(0, 100)]
    public float dropChance = 100f;
    [Tooltip("Height offset to raise loot relative to the Big Guy's position when dropped.")]
    public float dropHeightOffset = 1.5f;
    [Tooltip("Enable loot pop effect bouncing away from the Big Guy on death.")]
    public bool popLootOnDeath = true;
    [Tooltip("Upward velocity (up) when loot is popped out (m/s).")]
    public float lootPopUpwardSpeed = 5f;
    [Tooltip("Maximum horizontal velocity when loot is popped out (m/s).")]
    public float lootPopHorizontalSpeed = 3f;

    [Header("Loot Trail Effect")]
    [Tooltip("Configuration for trail + glow particle effect when loot flies.")]
    public LootTrailSettings lootTrailSettings = new LootTrailSettings();

    [Header("GiftBox (Endless Mode)")]
    public GameObject giftBoxPrefab;
    [Range(0, 100)]
    public float giftBoxDropChance = 10f;

    [Header("Rewards")]
    public float experienceReward = 120f;
    public int coinReward = 50;

    [Header("Cleanup")]
    [Tooltip("Time after death before destroying the GameObject (seconds).")]
    public float cleanupDelay = 6f;

    [Header("Debug/Testing")]
    [SerializeField] private bool forceDetectPlayer = false;

    private Animator animator;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private Collider col;
    private AudioSource audioSource;
    private EnemyLocomotion locomotion;
    private PlayerStats _targetStats;

    // --- State ---
    private enum BigGuyState { Dazed, Screaming, Chasing, Attacking, Dead }
    private BigGuyState state = BigGuyState.Dazed;
    private bool isDead;
    private bool hasScreamed;
    private bool isAttacking;
    private float attackTimer;
    private float _pathTimer;
    private Vector3 _lastSetDestination = Vector3.zero;

    // --- Timers (replacing Invoke for reliability) ---
    private float screamTimer;
    private bool screamTimerActive;
    private float attackDamageTimer;
    private bool attackDamageTimerActive;
    private float attackResetTimer;
    private bool attackResetTimerActive;

    // --- Smooth stop (gradual deceleration instead of agent.isStopped = true) ---
    private bool _wantsToStop;
    private float _stopTimer;
    private float _baseAnimSpeed;

    // --- Cached animator parameter hashes ---
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int ScreamHash = Animator.StringToHash("Scream");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int IsChasingHash = Animator.StringToHash("isChasing");
    private static readonly int IsDeathHash = Animator.StringToHash("isDeath");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int AttackResetHash = Animator.StringToHash("attackReset");

    // --- Cached player references ---
    private float findPlayerTimer;
    private Transform cachedPlayerTransform;
    private IDamageable cachedPlayerDamageable;

    private void Awake()
    {
        if (GetComponent<EnemyHealthBar>() == null)
        {
            gameObject.AddComponent<EnemyHealthBar>();
        }

        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();
        locomotion = GetComponent<EnemyLocomotion>();
        if (locomotion == null)
            locomotion = gameObject.AddComponent<EnemyLocomotion>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        FindPlayer();

        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.acceleration = 10f; // lower accel for a heavy, smooth start/stop
            agent.angularSpeed = 120f; // slower, smoother turning for a big guy
            agent.stoppingDistance = attackRange * 0.8f;
            agent.updateRotation = false;
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = 10; // Boss gets high priority so regular zombies yield
        }

        // Keep the Rigidbody KINEMATIC while alive so the NavMeshAgent fully
        // controls movement (same fix as Tank/Witch).
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = true;
        }

        if (animator != null)
        {
            _baseAnimSpeed = Random.Range(0.95f, 1.05f);
            animator.speed = _baseAnimSpeed;
        }

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

        state = BigGuyState.Dazed;
        hasScreamed = false;
        _lastSetDestination = transform.position;
        _pathTimer = maxRepathInterval;

        // Big Guy starts dazed at a fixed point — stop the agent immediately
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;
    }

    private void Update()
    {
        if (isDead)
            return;

        if (target == null)
        {
            findPlayerTimer += Time.deltaTime;
            if (findPlayerTimer >= 1f)
            {
                FindPlayer();
                findPlayerTimer = 0f;
            }
            return;
        }

        if (locomotion != null)
            locomotion.target = target;

        attackTimer += Time.deltaTime;

        float distance = Vector3.Distance(transform.position, target.position);

        // Check if player is dead — Big Guy stops chasing but does NOT return to Dazed
        if (_targetStats != null && _targetStats.IsDead)
        {
            if (state == BigGuyState.Chasing || state == BigGuyState.Attacking)
            {
                // Player is dead — stop and stand still
                RequestStop();
                state = BigGuyState.Dazed;
                if (animator != null)
                    animator.SetBool(IsChasingHash, false);
            }
            return;
        }

        // Tick timers (replacing Invoke for reliability)
        TickTimers();

        switch (state)
        {
            case BigGuyState.Dazed:
                UpdateDazed(distance);
                break;
            case BigGuyState.Screaming:
                // Screaming state is handled by screamTimer in TickTimers.
                FaceTarget();
                break;
            case BigGuyState.Chasing:
                UpdateChasing(distance);
                break;
            case BigGuyState.Attacking:
                // Attacking is handled by attackDamageTimer/attackResetTimer in TickTimers.
                FaceTarget();
                break;
        }

        // Smooth stop: gradually decelerate instead of snapping agent.isStopped
        HandleSmoothStop();

        // Update animator speed parameter (smoother damping for natural motion)
        if (animator != null)
        {
            float targetAnimSpeed = agent != null && agent.isOnNavMesh ? agent.velocity.magnitude / walkSpeed : 0f;

            // When holding position inside attack range (waiting for cooldown),
            // the agent is stopped so velocity=0 which would drive the Speed
            // parameter to 0. The Walk state uses Speed as its playback speed
            // (speedParameter="Speed"), so Speed=0 freezes the Walk animation
            // on a single frame. Keep a small minimum so Big Guy still shuffles
            // in place menacingly instead of locking up.
            if (state == BigGuyState.Chasing && !isAttacking && targetAnimSpeed < 0.2f)
                targetAnimSpeed = 0.2f;

            animator.SetFloat(SpeedHash, targetAnimSpeed, animSpeedDamping, Time.deltaTime);

            // Heavy chase animation: scale playback speed slightly slower when
            // chasing for a heavy, menacing walk (below 1 = sluggish/heavy).
            if (scaleAnimBySpeed && state == BigGuyState.Chasing && !isAttacking)
            {
                float speedFraction = Mathf.Clamp01(targetAnimSpeed);
                animator.speed = _baseAnimSpeed * Mathf.Lerp(1f, chaseAnimSpeedMultiplier, speedFraction);
            }
            else if (state != BigGuyState.Dead)
            {
                animator.speed = _baseAnimSpeed;
            }
        }
    }

    //==================================================
    // SMOOTH STOP (gradual deceleration)
    //==================================================

    private void HandleSmoothStop()
    {
        if (!_wantsToStop || agent == null || !agent.isOnNavMesh)
            return;

        _stopTimer += Time.deltaTime;
        if (_stopTimer >= stopDecelTime)
        {
            agent.isStopped = true;
            _wantsToStop = false;
        }
    }

    private void RequestStop()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;
        // Already stopped — nothing to do
        if (agent.isStopped)
            return;
        _wantsToStop = true;
        _stopTimer = 0f;
    }

    private void CancelStop()
    {
        _wantsToStop = false;
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = false;
    }

    //==================================================
    // TIMERS (replacing Invoke for reliability)
    //==================================================

    private void TickTimers()
    {
        if (screamTimerActive)
        {
            screamTimer += Time.deltaTime;
            if (screamTimer >= screamDuration)
            {
                screamTimerActive = false;
                EndScream();
            }
        }

        if (attackDamageTimerActive)
        {
            attackDamageTimer += Time.deltaTime;
            if (attackDamageTimer >= damageApplyDelay)
            {
                attackDamageTimerActive = false;
                ApplyAttackDamage();
            }
        }

        if (attackResetTimerActive)
        {
            attackResetTimer += Time.deltaTime;
            if (attackResetTimer >= attackAnimDuration)
            {
                attackResetTimerActive = false;
                ResetAttack();
            }
        }
    }

    //==================================================
    // DAZED STATE
    //==================================================

    private void UpdateDazed(float distance)
    {
        // Provoke by proximity
        if (distance <= provokeRadius)
        {
            bool hasLOS = !requireLineOfSight || HasLineOfSight();
            if (hasLOS)
            {
                StartScream();
                return;
            }
        }

        // Force detect for testing
        if (forceDetectPlayer)
        {
            StartScream();
            return;
        }
    }

    //==================================================
    // CHASING STATE
    //==================================================

    private void UpdateChasing(float distance)
    {
        // Big Guy NEVER returns to Dazed — he chases until dead.
        // No loseSightDistance check here.

        // In attack range and cooldown ready
        if (!isAttacking && distance <= attackRange && attackTimer >= attackCooldown)
        {
            StartAttack();
            return;
        }

        if (!isAttacking)
        {
            if (distance <= attackRange)
            {
                // Inside attack range but not ready to attack yet (cooldown):
                // hold position and face the player. Do NOT CancelStop or repath,
                // otherwise the agent shuffles toward the player between attacks
                // and physically pushes the player.
                RequestStop();
                FaceTarget();
            }
            else
            {
                // Player is outside attack range — resume chasing.
                CancelStop();

                _pathTimer += Time.deltaTime;
                float distToLastDest = Vector3.Distance(target.position, _lastSetDestination);
                bool canRepath = agent != null && !agent.pathPending &&
                    (locomotion == null || !locomotion.IsRecoveringFromStuck || !agent.hasPath);

                // Dynamic re-path interval based on distance and LOS
                bool hasLOS = HasLineOfSight();
                float dynamicInterval = Mathf.Lerp(0.2f, 0.4f, Mathf.Clamp01((distance - 5f) / 15f));
                float dynamicThreshold = Mathf.Lerp(1.0f, 2.0f, Mathf.Clamp01((distance - 5f) / 15f));
                if (!hasLOS)
                {
                    dynamicInterval *= 2.0f;
                    dynamicThreshold *= 1.5f;
                }

                if (canRepath && (_pathTimer >= dynamicInterval || distToLastDest > dynamicThreshold))
                {
                    SetDestinationRobust(target.position);
                    _lastSetDestination = target.position;
                    _pathTimer = 0f;
                }

                if (locomotion != null)
                    locomotion.HandleStuckDetection(distance, attackRange * 0.5f);

                FaceMovementDirection();
            }
        }
    }

    //==================================================
    // SCREAM
    //==================================================

    private void StartScream()
    {
        if (hasScreamed)
            return;

        hasScreamed = true;
        state = BigGuyState.Screaming;

        if (agent != null)
            RequestStop();

        if (animator != null)
        {
            animator.SetBool(IsChasingHash, false);
            animator.SetTrigger(ScreamHash);
        }

        if (screamClip != null)
            audioSource.PlayOneShot(screamClip);

        // Start scream timer
        screamTimer = 0f;
        screamTimerActive = true;
    }

    private void EndScream()
    {
        state = BigGuyState.Chasing;

        if (animator != null)
            animator.SetBool(IsChasingHash, true);

        CancelStop();

        _pathTimer = maxRepathInterval; // force immediate first re-path
    }

    //==================================================
    // ATTACK
    //==================================================

    private void StartAttack()
    {
        isAttacking = true;
        state = BigGuyState.Attacking;
        attackTimer = 0;

        RequestStop();

        if (animator != null)
            animator.SetTrigger(AttackHash);

        if (attackClip != null)
            audioSource.PlayOneShot(attackClip);

        // Damage is applied via AnimationEvent "BigGuyAttackHit" on the
        // Mutant Punch clip (synced to the actual hit frame). The fallback
        // timer runs in parallel in case the event is missing.
        attackDamageTimer = 0f;
        attackDamageTimerActive = true;
        attackResetTimer = 0f;
        attackResetTimerActive = true;
    }

    /// <summary>
    /// Called by an AnimationEvent on the Mutant Punch clip at the hit frame.
    /// This ensures damage is applied exactly when the animation connects,
    /// not at a fixed delay after the attack starts.
    /// </summary>
    public void BigGuyAttackHit()
    {
        // Cancel the fallback timer since the event fired
        attackDamageTimerActive = false;
        ApplyAttackDamage();
    }

    private void ApplyAttackDamage()
    {
        if (isDead || target == null)
            return;

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > attackRange + 1f)
            return;

        if (cachedPlayerDamageable != null)
            cachedPlayerDamageable.Damage(attackDamage, false);
    }

    private void ResetAttack()
    {
        isAttacking = false;
        state = BigGuyState.Chasing;

        // Trigger animator transition Attack → Walk immediately
        if (animator != null)
            animator.SetTrigger(AttackResetHash);

        CancelStop();

        _pathTimer = maxRepathInterval;
    }

    //==================================================
    // DAMAGE
    //==================================================

    public void Damage(float damage, bool isHeadshot)
    {
        if (CombatFeedbackHUD.Instance != null)
            CombatFeedbackHUD.Instance.ShowHit(transform.position, damage, isHeadshot);

        TakeDamage(Mathf.RoundToInt(Mathf.Abs(damage)));
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;

        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterDamageDealt(damage);

        if (OnHealthChanged != null)
            OnHealthChanged(HealthFraction);

        // Provoke on hit: if still dazed, scream immediately and chase
        if (state == BigGuyState.Dazed && !hasScreamed)
        {
            StartScream();
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    //==================================================
    // DEATH
    //==================================================

    public void Die()
    {
        if (isDead)
            return;

        isDead = true;
        state = BigGuyState.Dead;

        // Cancel all timers (replacing CancelInvoke)
        screamTimerActive = false;
        attackDamageTimerActive = false;
        attackResetTimerActive = false;

        if (CombatFeedbackHUD.Instance != null)
            CombatFeedbackHUD.Instance.ShowKill("Big Guy");

        if (AIDirector.Instance != null)
            AIDirector.Instance.RegisterKill();

        // Big Guy is a mini-boss — use generic kill registration.
        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterKill(500);

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddKill(500);

        if (ExperienceManager.Instance != null && ExperienceManager.Instance.useExperience)
            ExperienceManager.Instance.AddExperience(experienceReward);

        if (CoinManager.Instance != null && CoinManager.Instance.useCoins)
            CoinManager.Instance.AddCoins(coinReward);

        // Hard-stop the agent on death (no need for smooth decel here)
        _wantsToStop = false;
        if (agent != null)
            agent.enabled = false;

        if (animator != null)
        {
            // Force-clear other state bools so no Dazed/Walk transition can
            // override the Death transition.
            animator.SetBool(IsChasingHash, false);
            animator.SetBool(IsDeathHash, true);
            animator.SetTrigger(DeathHash);
            // Force-play Death state immediately, bypassing any pending
            // transitions that might fire first.
            animator.Play("Death", 0, 0f);
        }

        // Stop any ongoing audio before playing death sound
        audioSource.Stop();
        if (deathClip != null)
            audioSource.PlayOneShot(deathClip);

        if (col != null)
            col.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

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

        Destroy(gameObject, cleanupDelay);
    }

    //==================================================
    // PLAYER FINDING
    //==================================================

    private void FindPlayer()
    {
        if (cachedPlayerTransform != null)
        {
            target = cachedPlayerTransform;
            cachedPlayerDamageable = target.GetComponent<IDamageable>();
            _targetStats = target.GetComponent<PlayerStats>();
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            cachedPlayerTransform = player.transform;
            target = player.transform;
            cachedPlayerDamageable = target.GetComponent<IDamageable>();
            _targetStats = target.GetComponent<PlayerStats>();
        }
    }

    //==================================================
    // HELPERS
    //==================================================

    private void FaceTarget()
    {
        if (locomotion != null)
            locomotion.FaceTarget(rotationSpeed);
    }

    private void FaceMovementDirection()
    {
        if (locomotion != null)
            locomotion.FaceMovementDirection(rotationSpeed);
    }

    private void SetDestinationRobust(Vector3 destination)
    {
        if (locomotion != null)
            locomotion.SetDestinationRobust(destination);
    }

    private bool HasLineOfSight()
    {
        if (locomotion != null)
            return locomotion.HasLineOfSight();
        return false;
    }

    private void OnDisable()
    {
        lootTrailSettings = new LootTrailSettings();
    }

    //==================================================
    // GIZMOS
    //==================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, provokeRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
