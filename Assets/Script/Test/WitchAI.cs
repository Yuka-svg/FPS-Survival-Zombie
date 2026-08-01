using UnityEngine;
using UnityEngine.AI;
using cowsins;

/// <summary>
/// The Witch — a mini-boss inspired by the L4D2 Witch. A mother who lost
/// her child, normally sits alone crying (Cry state) at a fixed point.
/// When disturbed (player approaches within provokeRadius, or gets shot),
/// she screams briefly then charges at the player at high speed
/// (Scream → Chase). Once provoked, she NEVER returns to Cry — she chases
/// the player relentlessly until she or the player is dead. Her looping
/// cry audio fades with distance so it is only audible within a small
/// area around her resting point (cryMaxDistance).
///
/// Mini-boss stats: health 60, speed 6.5 m/s, damage 30, scream 1.2s.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class WitchAI : MonoBehaviour, IDamageable, ISpecialEnemy, IEnemyHealthReadout
{
    // ---- IEnemyHealthReadout (read-only; for EnemyHealthBar) ----
    public float HealthFraction
    {
        get { return maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f; }
    }
    public bool IsDead { get { return isDead; } }
    public EnemyType EnemyType { get { return EnemyType.Special; } }
    public event System.Action<float> OnHealthChanged;

    protected bool lastHitWasHeadshot;

    private void OnEnable()
    {
        EnemyRegistry.Register(this);
        lastHitWasHeadshot = false;
    }

    private void OnDestroy()
    {
        EnemyRegistry.Unregister(this);
    }

    [Header("Target")]
    public Transform target;

    [Header("Health")]
    public int maxHealth = 60;
    public int currentHealth;

    [Header("Movement")]
    public float runSpeed = 6.5f;
    public float rotationSpeed = 10f;

    [Header("Detection")]
    [Tooltip("Distance the player must approach to provoke the Witch (transitions from Cry to Scream).")]
    public float provokeRadius = 8f;
    [Tooltip("DEPRECATED: Witch no longer loses sight of the player once provoked. Kept for scene serialization compatibility.")]
    public float loseSightDistance = 20f;

    [Header("Line of Sight")]
    [Tooltip("If true, the Witch requires clear LOS to the player before proximity-based provoke.")]
    public bool requireLineOfSight = true;
    [Tooltip("Layer mask for sight obstruction objects (walls, furniture). Defaults to everything.")]
    public LayerMask sightObstructionMask = ~0;
    [Tooltip("Eye height from pivot for the LOS raycast.")]
    public float sightEyeHeight = 1.5f;

    [Header("Attack")]
    public float attackRange = 2.0f;
    public float attackCooldown = 2.5f;
    public float attackDamage = 30f;
    [Tooltip("Delay after triggering Attack before applying damage. Now driven by AnimationEvent 'WitchAttackHit' on the Punching clip; this is a fallback if the event is missing.")]
    public float damageApplyDelay = 1.0f;

    [Header("Scream")]
    [Tooltip("Duration of the scream before starting to charge at the player (seconds). Includes StandUp animation time + Scream time.")]
    public float screamDuration = 3.0f;

    [Header("Stuck Recovery")]
    public float stuckTimeThreshold = 1.2f;
    public float stuckMoveThreshold = 1f;
    public float stuckRepathRadius = 5f;

    [Header("Chase Re-path (Responsiveness)")]
    [Tooltip("Minimum distance (m) the player must move relative to the last destination before the Witch immediately re-paths.")]
    public float playerMovedRepathThreshold = 1f;
    [Tooltip("Maximum interval (seconds) between re-paths when the player is standing still.")]
    public float maxRepathInterval = 0.1f;

    [Header("Animation Smoothing")]
    [Tooltip("Damping time for the Speed animator parameter (lower = snappier, higher = smoother).")]
    public float animSpeedDamping = 0.14f;
    [Tooltip("Scales Run animation playback speed by actual agent speed. Requires Speed parameter active on Run state.")]
    public bool scaleAnimBySpeed = true;
    [Tooltip("Multiplier applied to animator.speed when chasing (gives a more frenetic, L4D2-like motion).")]
    public float chaseAnimSpeedMultiplier = 1.05f;
    [Tooltip("Grace period (s) after entering Cry/ReturnToCrying before the agent fully stops, for smoother deceleration.")]
    public float stopDecelTime = 0.35f;

    [Header("Cry Audio (Distance Falloff)")]
    [Tooltip("Looping crying sound while in Cry state (loop). Volume is scaled by distance to the player using cryMinDistance..cryMaxDistance.")]
    public AudioClip cryClip;
    [Tooltip("Distance (m) at which the cry audio is at full volume (player inside this radius hears it loudly).")]
    public float cryMinDistance = 3f;
    [Tooltip("Distance (m) at which the cry audio fades to silence. The cry is only audible within this radius around the Witch.")]
    public float cryMaxDistance = 12f;
    [Tooltip("Base volume of the cry loop before distance falloff is applied.")]
    [Range(0f, 1f)]
    public float cryBaseVolume = 1f;

    [Header("Audio (One-Shot)")]
    [Tooltip("Screaming sound when provoked.")]
    public AudioClip screamClip;
    [Tooltip("Sound when attacking.")]
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
    [Tooltip("Height offset to raise loot relative to the Witch's position when dropped.")]
    public float dropHeightOffset = 1.5f;
    [Tooltip("Enable loot pop effect bouncing away from the Witch on death.")]
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

    [Header("Quest")]
    [Tooltip("QuestTrigger (Manual mode) will be completed when the Witch dies. Leave null if not using quests.")]
    public QuestTrigger questTriggerOnDeath;

    [Header("Rewards")]
    public float experienceReward = 100f;
    public int coinReward = 40;

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
    private enum WitchState { Crying, StandingUp, Screaming, Chasing, Attacking, Dead }
    private WitchState state = WitchState.Crying;
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

    // --- Cry audio loop ---
    private bool cryAudioPlaying;

    // --- Smooth stop (gradual deceleration instead of agent.isStopped = true) ---
    private bool _wantsToStop;
    private float _stopTimer;
    private float _baseAnimSpeed;

    // --- Cached animator parameter hashes ---
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int ScreamHash = Animator.StringToHash("Scream");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int IsChasingHash = Animator.StringToHash("isChasing");
    private static readonly int IsCryingHash = Animator.StringToHash("isCrying");
    private static readonly int IsDeathHash = Animator.StringToHash("isDeath");
    private static readonly int StandUpHash = Animator.StringToHash("StandUp");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int HitHash = Animator.StringToHash("Hit");
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
            agent.speed = runSpeed;
            agent.acceleration = 22f; // tuned for smooth charge without jerky startups
            agent.angularSpeed = 360f;
            agent.stoppingDistance = attackRange * 0.8f;
            agent.updateRotation = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = 8; // Mini-boss — high priority, regular zombies yield.
        }

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = true;
        }

        if (animator != null)
        {
            animator.SetBool(IsCryingHash, true);
            animator.SetBool(IsChasingHash, false);
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

        state = WitchState.Crying;
        hasScreamed = false;
        _lastSetDestination = transform.position;
        _pathTimer = maxRepathInterval;

        // Witch starts crying at a fixed point — stop the agent immediately
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

        // Check if player is dead — Witch stops and stands still (does NOT return to crying)
        if (_targetStats != null && _targetStats.IsDead)
        {
            if (state == WitchState.Chasing || state == WitchState.Attacking)
            {
                RequestStop();
                state = WitchState.Crying; // reuse Crying as the "idle" state but do not restart cry audio
                isAttacking = false;
                attackDamageTimerActive = false;
                attackResetTimerActive = false;
                if (animator != null)
                {
                    animator.SetBool(IsChasingHash, false);
                    animator.SetBool(IsCryingHash, false);
                }
                // Stop any looping cry audio so she stands silent over the corpse
                if (cryAudioPlaying)
                {
                    audioSource.Stop();
                    audioSource.clip = null;
                    audioSource.loop = false;
                    cryAudioPlaying = false;
                }
            }
            return;
        }

        // Tick timers (replacing Invoke for reliability)
        TickTimers();

        switch (state)
        {
            case WitchState.Crying:
                UpdateCrying(distance);
                break;
            case WitchState.StandingUp:
                // StandingUp: Witch is getting up from crying pose.
                // AnimatorController handles Cry→StandUp→Scream transition.
                // Face the player while standing up.
                FaceTarget();
                break;
            case WitchState.Screaming:
                // Screaming state is handled by screamTimer in TickTimers.
                FaceTarget();
                break;
            case WitchState.Chasing:
                UpdateChasing(distance);
                break;
            case WitchState.Attacking:
                // Attacking is handled by attackDamageTimer/attackResetTimer in TickTimers.
                FaceTarget();
                break;
        }

        // Smooth stop: gradually decelerate instead of snapping agent.isStopped
        HandleSmoothStop();

        // Update animator speed parameter (smoother damping for natural motion)
        if (animator != null)
        {
            float targetAnimSpeed = agent != null && agent.isOnNavMesh ? agent.velocity.magnitude / runSpeed : 0f;

            // When holding position inside attack range (waiting for cooldown),
            // the agent is stopped so velocity=0 which would drive the Speed
            // parameter to 0. The Run state uses Speed as its playback speed
            // (speedParameter="Speed"), so Speed=0 freezes the Run animation
            // on a single frame. Keep a small minimum so the Witch still
            // shifts in place instead of locking up.
            if (state == WitchState.Chasing && !isAttacking && targetAnimSpeed < 0.2f)
                targetAnimSpeed = 0.2f;

            animator.SetFloat(SpeedHash, targetAnimSpeed, animSpeedDamping, Time.deltaTime);

            // Frenetic chase animation: scale playback speed slightly faster when chasing
            if (scaleAnimBySpeed && state == WitchState.Chasing && !isAttacking)
            {
                float speedFraction = Mathf.Clamp01(targetAnimSpeed);
                animator.speed = _baseAnimSpeed * Mathf.Lerp(1f, chaseAnimSpeedMultiplier, speedFraction);
            }
            else if (state != WitchState.Dead)
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
            if (attackResetTimer >= attackCooldown)
            {
                attackResetTimerActive = false;
                ResetAttack();
            }
        }
    }

    //==================================================
    // CRYING STATE
    //==================================================

    private void UpdateCrying(float distance)
    {
        // Play cry audio as a looping clip so audioSource.Stop() can cut it.
        // Volume is scaled by distance to the player so the cry is only
        // audible within cryMaxDistance around the Witch's resting point.
        // NOTE: distance falloff MUST be applied BEFORE Play() so the cry
        // does not blip at full volume for one frame when the game starts
        // with the player far away.
        float t = Mathf.InverseLerp(cryMaxDistance, cryMinDistance, distance);
        float targetVolume = cryBaseVolume * Mathf.Clamp01(t);

        if (!cryAudioPlaying && cryClip != null)
        {
            audioSource.clip = cryClip;
            audioSource.loop = true;
            audioSource.volume = targetVolume;
            audioSource.Play();
            cryAudioPlaying = true;
        }

        // Distance-based volume falloff: full at cryMinDistance, silent at cryMaxDistance
        if (cryAudioPlaying)
        {
            audioSource.volume = targetVolume;
        }

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
        // Witch NEVER returns to Crying once provoked — she chases until
        // she or the player is dead. No loseSightDistance check here.

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
                float dynamicInterval = Mathf.Lerp(0.15f, 0.4f, Mathf.Clamp01((distance - 5f) / 15f));
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
        state = WitchState.StandingUp;

        // Stop looping cry audio so it doesn't overlap with the scream.
        // Also reset volume to full since the cry falloff may have lowered it.
        if (cryAudioPlaying)
        {
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
            audioSource.volume = 1f;
            cryAudioPlaying = false;
        }
        else
        {
            audioSource.volume = 1f;
        }

        if (agent != null)
            RequestStop();

        // Trigger StandUp animation first — Witch stands up from crying pose
        // before screaming. The AnimatorController transitions:
        //   Cry → StandUp (trigger) → Scream (exit time) → Run (isChasing)
        if (animator != null)
        {
            animator.SetBool(IsCryingHash, false);
            animator.SetTrigger(StandUpHash);
            // Pre-set the Scream trigger so when StandUp→Scream transition fires
            // (via exit time), the Scream state plays. Then EndScream() will set
            // isChasing=true to transition Scream→Run.
            animator.SetTrigger(ScreamHash);
        }

        if (screamClip != null)
            audioSource.PlayOneShot(screamClip);

        // Start scream timer — will count during StandUp + Scream
        screamTimer = 0f;
        screamTimerActive = true;
    }

    private void EndScream()
    {
        state = WitchState.Chasing;

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
        state = WitchState.Attacking;
        attackTimer = 0;

        RequestStop();

        if (animator != null)
            animator.SetTrigger(AttackHash);

        if (attackClip != null)
            audioSource.PlayOneShot(attackClip);

        // Damage is applied via AnimationEvent "WitchAttackHit" on the
        // Punching clip (synced to the actual hit frame). The fallback
        // timer runs in parallel in case the event is missing.
        attackDamageTimer = 0f;
        attackDamageTimerActive = true;
        attackResetTimer = 0f;
        attackResetTimerActive = true;
    }

    /// <summary>
    /// Called by an AnimationEvent on the Punching clip at the hit frame.
    /// This ensures damage is applied exactly when the animation connects,
    /// not at a fixed delay after the attack starts.
    /// </summary>
    public void WitchAttackHit()
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
        state = WitchState.Chasing;

        // Trigger animator transition Attack → Run immediately
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
        lastHitWasHeadshot = isHeadshot;

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

        if (animator != null)
            animator.SetTrigger(HitHash);

        if (OnHealthChanged != null)
            OnHealthChanged(HealthFraction);

        // Provoke on hit: if still crying, scream immediately and chase
        if (state == WitchState.Crying && !hasScreamed)
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
        state = WitchState.Dead;

        // Cancel all timers (replacing CancelInvoke)
        screamTimerActive = false;
        attackDamageTimerActive = false;
        attackResetTimerActive = false;

        var report = new KillReport
        {
            enemyInstanceID = gameObject.GetInstanceID(),
            killerName = "Player",
            victimName = "Witch",
            weaponName = string.Empty,
            isHeadshot = lastHitWasHeadshot
        };
        UIEvents.onEnemyKilledDetailed?.Invoke(report);
        UIEvents.onEnemyKilled?.Invoke("Witch");

        if (AIDirector.Instance != null)
            AIDirector.Instance.RegisterKill();

        // Witch is a mini-boss, not a Tank — use generic kill registration.
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
            // Force-clear other state bools so no Cry/Run transition can
            // override the Death transition (the AnyState→Death trigger can
            // lose to an in-progress Scream→Run exit-time transition).
            animator.SetBool(IsCryingHash, false);
            animator.SetBool(IsChasingHash, false);
            animator.SetBool(IsDeathHash, true);
            animator.SetTrigger(DeathHash);
            // Force-play Death state immediately, bypassing any pending
            // transitions that might fire first.
            animator.Play("Death", 0, 0f);
        }

        // Stop any ongoing cry/scream before playing death sound
        if (cryAudioPlaying)
        {
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
            cryAudioPlaying = false;
        }
        else
        {
            audioSource.Stop();
        }
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

        // Complete the quest (if a QuestTrigger is assigned) — Manual mode.
        if (questTriggerOnDeath != null)
        {
            questTriggerOnDeath.Complete();
        }

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
        EnemyRegistry.Unregister(this);
        lootTrailSettings = new LootTrailSettings();
    }

    //==================================================
    // GIZMOS
    //==================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, provokeRadius);

        // Cry audio falloff radius (only audible inside this)
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, cryMaxDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
