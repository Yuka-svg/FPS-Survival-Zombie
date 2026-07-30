using UnityEngine;
using UnityEngine.AI;
using cowsins;

[RequireComponent(typeof(AudioSource))]
public class TankBossAI : MonoBehaviour, IDamageable, ISpecialEnemy, IEnemyHealthReadout
{
    public event System.Action<float> OnHealthChanged;
    public float HealthFraction => Mathf.Clamp01((float)currentHealth / maxHealth);
    public EnemyType EnemyType => EnemyType.Boss;

    [Header("Player")]
    public Transform target;

    [Header("Health")]
    public int maxHealth = 500;
    public int currentHealth;

    [Header("Detection")]
    public float detectRange = 20f;
    [Tooltip("Khoảng cách mất dấu player. Lớn hơn detectRange để chống flip-flop.")]
    public float loseSightDistance = 60f;
    [System.Obsolete("No longer used in simplified chase logic.")]
    [Tooltip("Sau khi mất dấu, Tank vẫn đuổi theo alertMemoryDuration giây trước khi từ bỏ.")]
    public float alertMemoryDuration = 3f;

    [Header("Movement")]
    public float runSpeed = 3.5f;
    public float rotationSpeed = 10f;

    [Header("Attack")]
    public float meleeRange = 3f;
    public float jumpAttackRange = 10f;

    public float attackCooldown = 2f;
    public float jumpAttackCooldown = 10f;

    [Header("Damage")]
    public float punchDamage = 30f;
    public float swipeDamage = 40f;
    public float jumpDamage = 60f;
    public float jumpRadius = 5f;
    public bool shakeCameraOnLand = true;

    [Header("Scream")]
    public float screamDuration = 2.5f;

    [Header("Chase Re-path (Responsiveness)")]
    [Tooltip("Khoảng cách tối thiểu (m) player phải di chuyển so với destination cuối cùng trước khi Tank re-path ngay lập tức.")]
    public float playerMovedRepathThreshold = 1f;
    [Tooltip("Interval tối đa (giây) giữa các lần re-path khi player đứng yên.")]
    public float maxRepathInterval = 0.1f;


    [Tooltip("Layer mask cho vật cản tầm nhìn (tường,家具). Mặc định tất cả.")]
    public LayerMask sightObstructionMask = ~0;
    [Tooltip("Chiều cao mắt từ pivot cho LOS raycast.")]
    public float sightEyeHeight = 2f;


    [Header("Audio")]
    [Tooltip("Sound khi tank phát hiện player và bắt đầu scream.")]
    public AudioClip screamClip;
    [Tooltip("Sound khi tank thực hiện đòn đánh cận chiến (swipe/punch).")]
    public AudioClip meleeAttackClip;
    [Tooltip("Sound khi tank đáp xuống đất sau jump attack.")]
    public AudioClip jumpLandClip;
    [Tooltip("Sound khi tank chết.")]
    public AudioClip deathClip;

    [Header("Loot")]
    [Tooltip("Loot table: mỗi entry roll độc lập, có thể rơi 0..N loại cùng lúc.")]
    public LootDropEntry[] lootTable;
    [Tooltip("Fallback khi lootTable trống: loot đơn lẻ theo dropChance.")]
    public GameObject dropPrefab;
    [Range(0, 100)]
    public float dropChance = 100f;
    [Tooltip("Khoảng cách nâng loot lên so với vị trí boss khi rớt xuống.")]
    public float dropHeightOffset = 2f;
    [Tooltip("Bật hiệu ứng loot nhảy ra khỏi boss khi chết.")]
    public bool popLootOnDeath = true;
    [Tooltip("Vận tốc đứng (lên) khi loot bị bắn ra (m/s).")]
    public float lootPopUpwardSpeed = 6f;
    [Tooltip("Vận tốc ngang tối đa khi loot bị bắn ra (m/s).")]
    public float lootPopHorizontalSpeed = 4f;

    [Header("Loot Trail Effect")]
    [Tooltip("Cấu hình vệt trail + glow particle khi loot bay. Chỉnh trực tiếp trên tank boss.")]
    public LootTrailSettings lootTrailSettings = new LootTrailSettings();

    [Header("GiftBox (Endless Mode)")]
    public GameObject giftBoxPrefab;
    [Range(0, 100)]
    public float giftBoxDropChance = 10f;

    private Animator animator;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private Collider col;
    private AudioSource audioSource;

    private float attackTimer;
    private float jumpTimer;
    private float _pathTimer;
    private float _cachedDistance;
    private Vector3 _lastSetDestination = Vector3.zero;

    private EnemyLocomotion locomotion;
    private PlayerStats _targetStats;

    private bool isDead;
    public bool IsDead { get { return isDead; } }
    private bool isHit;
    private bool isAttacking;
    private bool isJumpAttacking;

    private bool hasScreamed;
    private bool isScreaming;
    private bool hasDetectedPlayer;
    [Header("Debug/Testing")]
    [SerializeField] private bool forceDetectPlayer = true;

    private int attackIndex;

    // --- Reusable buffer for OverlapSphere (avoid per-call allocation) ---
    private static readonly Collider[] _overlapBuffer = new Collider[64];

    // --- Cached animator parameter hashes (avoid per-call string hashing) ---
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int ScreamHash = Animator.StringToHash("Scream");
    private static readonly int IsPlayerNearHash = Animator.StringToHash("isPlayerNear");
    private static readonly int IsDeathHash = Animator.StringToHash("isDeath");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int JumpAttackStateHash = Animator.StringToHash("Mutant Jump Attack");

    private void Start()
    {
        if (GetComponent<EnemyHealthBar>() == null)
        {
            gameObject.AddComponent<EnemyHealthBar>();
        }

        currentHealth = maxHealth;

        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();
        locomotion = GetComponent<EnemyLocomotion>();
        if (locomotion == null)
            locomotion = gameObject.AddComponent<EnemyLocomotion>();

        FindPlayer();

        if (agent != null)
        {
            agent.speed = runSpeed;
            agent.acceleration = 20f;
            agent.angularSpeed = 300f;
            // Use half meleeRange as stopping distance — same fix as ZombieAI:
            // stoppingDistance == meleeRange causes a chase-stop-chase flip-flop
            // when the agent stops exactly at meleeRange (distance == meleeRange
            // triggers attack, but the agent stops slightly outside due to
            // stopping distance, so it never enters attack range).
            agent.stoppingDistance = meleeRange * 0.5f;
            agent.updateRotation = false;
            // Performance: HighQuality avoidance is ~quadratic with agent count;
            // use cheap avoidance so the Tank doesn't overload the avoidance
            // system when many regular zombies are also active (Ch5 boss wave).
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = 10; // Boss gets high priority so regular zombies yield.
        }

        // Keep the Rigidbody KINEMATIC while alive so the NavMeshAgent fully
        // controls movement. A non-kinematic Rigidbody with gravity fights the
        // agent on stairs/slopes/bridges (Ch5 = ApartmentBridge): gravity pulls
        // the Tank down, the capsule collider catches on step edges, and the two
        // systems jitter — the Tank slides in one direction. Kinematic mode lets
        // the agent drive position smoothly while still sending collision events.
        // Die() sets kinematic again for the death animation, so this is consistent.
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = true; // kept on so the body can fall if the agent is disabled later.
        }

        animator.speed =
            Random.Range(0.95f, 1.05f);

        if (locomotion != null)
        {
            locomotion.target = target;
            locomotion.requireLineOfSight = true; // Tank always requires LOS initially
            locomotion.sightObstructionMask = sightObstructionMask;
            locomotion.sightEyeHeight = sightEyeHeight;
            locomotion.stuckTimeThreshold = 1.2f; // default stuck threshold
            locomotion.stuckMoveThreshold = 1f;
            locomotion.stuckRepathRadius = 5f;
            locomotion.playerMovedRepathThreshold = playerMovedRepathThreshold;
            locomotion.maxRepathInterval = maxRepathInterval;


            locomotion.Initialize();
        }

        hasDetectedPlayer = false;
        _lastSetDestination = transform.position;
        _pathTimer = maxRepathInterval; // force immediate first re-path
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
        {
            locomotion.target = target;
        }

        attackTimer += Time.deltaTime;
        jumpTimer += Time.deltaTime;

        // Compute distance every frame — Vector3.Distance is extremely cheap
        // and a stale 0.2s cache causes the Tank to react late when the player
        // moves in/out of detect/attack range.
        _cachedDistance = Vector3.Distance(transform.position, target.position);

        float distance = _cachedDistance;

        HandleJumpState();

        if (isHit)
            return;

        //------------------------------------------------
        // FIRST SCREAM
        //------------------------------------------------

        if (!hasScreamed &&
            distance <= detectRange)
        {
            StartScream();
            return;
        }

        if (isScreaming)
            return;

        if (distance <= meleeRange)
        {
            FaceTarget();
        }
        else
        {
            FaceMovementDirection();
        }

        //------------------------------------------------
        // DETECTION: check if we can see the player
        //------------------------------------------------
        bool hasLOSCurrently = false;
        if (!hasDetectedPlayer && !isScreaming && distance <= detectRange)
        {
            hasLOSCurrently = HasLineOfSight();
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
            hasDetectedPlayer = false;
            agent.isStopped = true;
            return;
        }

        bool shouldChase = false;
        if (forceDetectPlayer)
        {
            hasDetectedPlayer = true;
            shouldChase = true;
            if (!hasScreamed)
            {
                StartScream();
                return;
            }
        }
        else if (hasDetectedPlayer && distance <= loseSightDistance)
        {
            shouldChase = true;
        }
        else if (!hasDetectedPlayer && hasLOSCurrently)
        {
            hasDetectedPlayer = true;
            shouldChase = true;
            if (!hasScreamed)
            {
                StartScream();
                return;
            }
        }

        if (isScreaming)
            return;

        if (distance <= meleeRange)
        {
            FaceTarget();
        }
        else
        {
            FaceMovementDirection();
        }

        //------------------------------------------------
        // CHASE
        //------------------------------------------------
        if (shouldChase)
        {
            if (!isAttacking && !isJumpAttacking)
            {
                agent.isStopped = false;

                _pathTimer += Time.deltaTime;
                float distToLastDest = Vector3.Distance(target.position, _lastSetDestination);
                bool canRepath = agent != null && !agent.pathPending && (locomotion == null || !locomotion.IsRecoveringFromStuck || !agent.hasPath);
                
                // Slower repathing when player is out of sight (no LOS) to save CPU
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
                {
                    locomotion.HandleStuckDetection(distance, meleeRange * 0.5f);
                }
            }
        }
        else
        {
            hasDetectedPlayer = false;
            agent.isStopped = true;
        }

        //------------------------------------------------
        // JUMP ATTACK
        //------------------------------------------------

        if (!isJumpAttacking &&
            !isAttacking &&
            distance > meleeRange &&
            distance <= jumpAttackRange &&
            jumpTimer >= jumpAttackCooldown)
        {
            StartJumpAttack();
            return;
        }

        //------------------------------------------------
        // MELEE ATTACK
        //------------------------------------------------

        if (!isAttacking &&
            distance <= meleeRange &&
            attackTimer >= attackCooldown)
        {
            StartMeleeAttack();
        }

        // Set animator speed using normalized values and handle direct steering case
        float targetAnimSpeed = agent != null && agent.isOnNavMesh ? agent.velocity.magnitude / runSpeed : 0f;

        if (animator != null)
        {
            animator.SetFloat(SpeedHash, targetAnimSpeed, 0.15f, Time.deltaTime);
        }
    }

    private float findPlayerTimer;
    private Transform cachedPlayerTransform;
    private IDamageable cachedPlayerDamageable;
    private CameraEffects cachedCameraEffects;

    private void FindPlayer()
    {
        if (cachedPlayerTransform != null)
        {
            target = cachedPlayerTransform;
            cachedPlayerDamageable = target.GetComponent<IDamageable>();
            cachedCameraEffects = target.GetComponent<CameraEffects>();
            _targetStats = target.GetComponent<PlayerStats>();
            return;
        }

        GameObject player =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (player != null)
        {
            cachedPlayerTransform = player.transform;
            target = player.transform;
            cachedPlayerDamageable = target.GetComponent<IDamageable>();
            cachedCameraEffects = target.GetComponent<CameraEffects>();
            _targetStats = target.GetComponent<PlayerStats>();
        }
    }

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

    //==================================================
    // SCREAM
    //==================================================

    private void StartScream()
    {
        hasScreamed = true;
        isScreaming = true;

        agent.isStopped = true;

        if (animator != null)
        {
            animator.SetBool(IsPlayerNearHash, true);
            animator.SetTrigger(ScreamHash);
        }

        PlaySound(screamClip);

        Invoke(
            nameof(EndScream),
            screamDuration
        );
    }

    private void EndScream()
    {
        isScreaming = false;
    }

    //==================================================
    // MELEE ATTACK
    //==================================================

    private void StartMeleeAttack()
    {
        isAttacking = true;

        attackTimer = 0;

        attackIndex = Random.Range(0, 2);

        if (animator != null)
        {
            animator.SetInteger(AttackIndexHash, attackIndex);
            animator.SetTrigger(AttackHash);
        }

        PlaySound(meleeAttackClip);

        Invoke(
            nameof(ResetAttack),
            attackCooldown
        );
    }

    private void ResetAttack()
    {
        isAttacking = false;
    }

    //==================================================
    // JUMP ATTACK
    //==================================================

    private void StartJumpAttack()
    {
        isJumpAttacking = true;
        isAttacking = true;

        jumpTimer = 0;

        agent.isStopped = true;

        // The Rigidbody is already kinematic from Start(), so there is no need
        // to toggle gravity/velocity here — the agent is stopped and the
        // animation drives the visual. Previously this disabled gravity on a
        // non-kinematic body, which caused the Tank to slide after the jump
        // ended (gravity was re-enabled but the agent was still stopped, so
        // physics pulled it down a slope/stairs).
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (animator != null) animator.Play(JumpAttackStateHash);
    }

    private void HandleJumpState()
    {
        if (animator == null) return;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        if (
            isJumpAttacking &&
            !state.IsName("Mutant Jump Attack")
        )
        {
            isJumpAttacking = false;
            isAttacking = false;

            // Nothing to restore — the Rigidbody stayed kinematic throughout
            // the jump. The agent will resume driving movement next Update.
        }
    }

    //==================================================
    // ANIMATION EVENTS
    //==================================================

    public void AttackHit()
    {
        DamagePlayer(punchDamage);
    }

    public void SwipeHit()
    {
        DamagePlayer(swipeDamage);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    public void JumpLand()
    {
        PlaySound(jumpLandClip);

        // Use NonAlloc variant with a static reusable buffer to avoid
        // per-explosion GC allocation (OverlapSphere allocates an array).
        int numHits = Physics.OverlapSphereNonAlloc(
                transform.position,
                jumpRadius,
                _overlapBuffer
            );

        for (int i = 0; i < numHits; i++)
        {
            Collider hit = _overlapBuffer[i];
            if (hit.transform == transform)
                continue;

            if (target != null && hit.transform.root == target.root)
            {
                if (cachedPlayerDamageable != null)
                {
                    cachedPlayerDamageable.Damage(
                        jumpDamage,
                        false
                    );
                }

                if (cachedCameraEffects != null)
                {
                    cachedCameraEffects.ExplosionShake(
                        Vector3.Distance(
                            cachedCameraEffects.transform.position,
                            transform.position
                        ) * 0.5f
                    );
                }
            }
            else
            {
                // Damage other enemies or entities
                IDamageable damageable =
                    hit.GetComponent<IDamageable>();

                if (damageable != null)
                {
                    damageable.Damage(
                        jumpDamage,
                        false
                    );
                }
            }
        }
    }

    private void DamagePlayer(float damage)
    {
        if (target == null)
            return;

        float distance =
            Vector3.Distance(
                transform.position,
                target.position
            );

        if (distance > meleeRange + 1f)
            return;

        if (cachedPlayerDamageable != null)
        {
            cachedPlayerDamageable.Damage(
                damage,
                false
            );
        }
    }

    //==================================================
    // DAMAGE
    //==================================================

    public void Damage(
        float damage,
        bool isHeadshot
    )
    {
        TakeDamage(
            Mathf.RoundToInt(Mathf.Abs(damage))
        );
    }

    public void TakeDamage(int damage)
    {

        if (isDead)
            return;

        currentHealth -= damage;
        OnHealthChanged?.Invoke(HealthFraction);

        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterDamageDealt(damage);

        if (animator != null) animator.SetTrigger(HitHash);

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

        CancelInvoke();

        if (CombatFeedbackHUD.Instance != null)
            CombatFeedbackHUD.Instance.ShowKill("Tank");

        if (AIDirector.Instance != null)
            AIDirector.Instance.RegisterKill();

        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterTankKill();

        if (AchievementManager.Instance != null)
            AchievementManager.Instance.NotifyTankKill();

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddKill(500);

        // Return to the current chapter music after the final boss dies.
        if (MusicManager.Instance != null)
            MusicManager.Instance.StopFinalBossMusic();

        agent.enabled = false;

        if (animator != null)
        {
            animator.SetBool(IsDeathHash, true);
            animator.SetTrigger(DeathHash);
        }

        PlaySound(deathClip);

        if (col != null)
        {
            col.enabled = false;
        }

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

        Destroy(
            gameObject,
            6f
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            detectRange
        );

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            meleeRange
        );

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(
            transform.position,
            jumpAttackRange
        );
    }

    //==================================================
    // DIRECT STEERING
    //==================================================

    /// <summary>
    /// Syncs the NavMeshAgent's internal position to the transform's current
    /// position. Call this BEFORE setting updatePosition = true when
    /// transitioning from direct steering back to NavMesh. Without this, the
    /// agent's position is stale (frozen at the spot where direct steering
    /// started), causing the Tank to "snap back" / retreat to that old
    /// position when the agent resumes position control.
    /// </summary>
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

}