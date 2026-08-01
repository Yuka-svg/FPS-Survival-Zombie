using UnityEngine;
using UnityEngine.AI;
using cowsins;

public class BoomerAI : MonoBehaviour, IDamageable, ISpecialEnemy, IEnemyHealthReadout
{
    [Header("Player")]
    public Transform target;

    protected bool lastHitWasHeadshot;

    public int currentHealth;
    public int maxHealth = 100;

    [Header("Detection")]
    public float detectRange = 20f;
    [Tooltip("Khoảng cách mất dấu player. Lớn hơn detectRange để chống flip-flop.")]
    public float loseSightDistance = 25f;
    [System.Obsolete("No longer used in simplified chase logic.")]
    [Tooltip("Sau khi mất dấu, Boomer vẫn đuổi theo alertMemoryDuration giây trước khi từ bỏ.")]
    public float alertMemoryDuration = 3f;

    [Header("Line of Sight")]
    [Tooltip("If true, the boomer requires an unobstructed line of sight to the player before detection. Once chasing, it keeps chasing while within detectRange regardless of LOS.")]
    public bool requireLineOfSight = true;
    [Tooltip("Layer mask for objects that block the boomer's sight (walls, floors, furniture). Defaults to everything; the player and the boomer's own layer are automatically excluded.")]
    public LayerMask sightObstructionMask = ~0;
    [Tooltip("Eye height offset from the boomer's pivot for the LOS raycast.")]
    public float sightEyeHeight = 1.5f;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 10f;

    [Header("Stuck Recovery")]
    [Tooltip("How long (seconds) the boomer must be nearly stationary while chasing before it is considered stuck. Higher = more patient.")]
    public float stuckTimeThreshold = 1.2f;
    [Tooltip("If the boomer moves less than this distance (meters) over stuckTimeThreshold, it is considered stuck.")]
    public float stuckMoveThreshold = 1f;
    [Tooltip("How far to search for an intermediate re-path position when stuck (no teleport — just re-pathing).")]
    public float stuckRepathRadius = 5f;

    [Header("Explosion")]
    public float explodeRange = 3f;
    [Tooltip("Thời gian cảnh báo (scream) trước khi nổ. Đặt rất nhỏ (vd 0.1s) để boomer gần như nổ ngay lập tức khi lại gần player.")]
    public float screamDuration = 0.1f;

    [Header("Chase Re-path (Responsiveness)")]
    [Tooltip("Khoảng cách tối thiểu (m) player phải di chuyển so với destination cuối cùng trước khi Boomer re-path ngay lập tức.")]
    public float playerMovedRepathThreshold = 1f;
    [Tooltip("Interval tối đa (giây) giữa các lần re-path khi player đứng yên.")]
    public float maxRepathInterval = 0.1f;



    [Header("Explosion Damage")]
    public float explosionDamage = 50f;
    public float explosionRadius = 5f;

    [Header("Effects")]
    public GameObject explosionPrefab;

    [Header("Acid Pools")]
    public GameObject acidPoolSelfExplodePrefab;
    public GameObject acidPoolDeathPrefab;

    [Header("Acid Pool Lifetime")]
    public float acidPoolLifetime = 10f;

    [Header("Prefab Reference for Pooling")]
    public GameObject prefab;

    [Header("Rewards (granted only when the player shoots it down)")]
    public float experienceReward = 60f;
    public int coinReward = 25;

    [Header("Loot (dropped only when the player shoots it down)")]
    [Tooltip("Loot table: mỗi entry roll độc lập, có thể rơi 0..N loại cùng lúc.")]
    public LootDropEntry[] lootTable;
    [Tooltip("Fallback khi lootTable trống: loot đơn lẻ theo dropChance.")]
    public GameObject dropPrefab;
    [Range(0, 100)]
    public float dropChance = 100f;
    [Tooltip("Khoảng cách nâng loot lên so với vị trí boomer khi rớt xuống.")]
    public float dropHeightOffset = 1.5f;
    [Tooltip("Bật hiệu ứng loot nhảy ra khỏi boomer khi chết.")]
    public bool popLootOnDeath = true;
    [Tooltip("Vận tốc đứng (lên) khi loot bị bắn ra (m/s).")]
    public float lootPopUpwardSpeed = 5f;
    [Tooltip("Vận tốc ngang tối đa khi loot bị bắn ra (m/s).")]
    public float lootPopHorizontalSpeed = 3.5f;

    [Header("Loot Trail Effect")]
    [Tooltip("Cấu hình vệt trail + glow particle khi loot bay. Chỉnh trực tiếp trên boomer.")]
    public LootTrailSettings lootTrailSettings = new LootTrailSettings();

    [Header("GiftBox (Endless Mode)")]
    public GameObject giftBoxPrefab;
    [Range(0, 100)]
    public float giftBoxDropChance = 10f;

    [Header("Self-Contained Timing (independent of animation events)")]
    [Tooltip("Delay after the Explode trigger before the blast actually fires.")]
    public float explodeFxDelay = 0.25f;
    [Tooltip("Delay after the Explode trigger before the corpse is cleaned up.")]
    public float cleanupDelay = 2.5f;

    private Collider[] overlapColliders = new Collider[500];

    // ---- IEnemyHealthReadout (read-only; for EnemyHealthBar) ----
    public float HealthFraction
    {
        get { return maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f; }
    }
    public bool IsDead { get { return isDead; } }
    public EnemyType EnemyType { get { return EnemyType.Special; } }
    public event System.Action<float> OnHealthChanged;

    private bool hasExploded;
    private bool hasDestroyed;
    private bool rewardsGranted;
    private bool isWaitingForExplosion;
    private float explosionWaitTimer;

    private Animator animator;
    private NavMeshAgent agent;
    private Rigidbody rb;
    private Collider col;

    private bool isDead;
    private bool isHit;
    private bool isScreaming;
    private bool hasStartedExplosion;
    private bool hasDetectedPlayer;
    [Header("Debug/Testing")]
    [SerializeField] private bool forceDetectPlayer = true;

    private EnemyLocomotion locomotion;
    private PlayerStats _targetStats;

    // Caching original values for scaling
    private Vector3 _originalScale;
    private float _originalAgentHeight;
    private float _originalAgentRadius;
    private float _originalSightEyeHeight;

    private float _originalDropHeightOffset;
    private float _originalMoveSpeed;

    // --- Chase re-path throttling ---
    private float _pathTimer;
    private Vector3 _lastSetDestination = Vector3.zero;



    // --- Cached animator parameter hashes (avoid per-call string hashing) ---
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int IsWarningHash = Animator.StringToHash("isWarning");
    private static readonly int ExplodeHash = Animator.StringToHash("Explode");

    private enum ExplosionType
    {
        SelfExplode,
        Killed
    }

    private ExplosionType explosionType;

    private void OnDisable()
    {
        EnemyRegistry.Unregister(this);
        CancelInvoke();
        lootTrailSettings = new LootTrailSettings();
    }

    private void OnDestroy()
    {
        EnemyRegistry.Unregister(this);
    }

    private void Awake()
    {
        _originalMoveSpeed = moveSpeed;

        if (GetComponent<EnemyHealthBar>() == null)
        {
            gameObject.AddComponent<EnemyHealthBar>();
        }

        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
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
        _originalMoveSpeed = moveSpeed;
    }

    private void OnEnable()
    {
        EnemyRegistry.Register(this);
        lastHitWasHeadshot = false;

        currentHealth = maxHealth;
        isDead = false;
        isHit = false;
        isScreaming = false;
        hasStartedExplosion = false;
        hasExploded = false;
        hasDestroyed = false;
        rewardsGranted = false;
        isWaitingForExplosion = false;
        explosionWaitTimer = 0f;

        hasDetectedPlayer = false;

        // Dynamically randomize height between 1.6m and 1.9m
        float defaultHeight = 1.8f;
        if (col is CapsuleCollider capCol)
        {
            defaultHeight = capCol.height;
        }
        float targetHeight = Random.Range(1.6f, 1.9f);
        float scaleFactor = targetHeight / defaultHeight;

        // Apply uniform scale to visual mesh and collider
        transform.localScale = _originalScale * scaleFactor;

        // Scale NavMeshAgent bounds
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
            agent.height = _originalAgentHeight * scaleFactor;
            agent.radius = _originalAgentRadius * scaleFactor;
        }

        // Scale height-based locomotion variables
        sightEyeHeight = _originalSightEyeHeight * scaleFactor;

        dropHeightOffset = _originalDropHeightOffset * scaleFactor;

        // Scale movement speed to match stride length
        moveSpeed = _originalMoveSpeed * scaleFactor;

        FindPlayer();

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.acceleration = 20f;
            agent.angularSpeed = 300f;
            agent.stoppingDistance = explodeRange;
            agent.updateRotation = false;
            // Performance: use cheap avoidance so Boomers don't overload the
            // avoidance system when many regular zombies are also active.
            agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = 15; // Special enemy — slightly higher priority than regular zombies.
        }

        // Keep the Rigidbody KINEMATIC while alive so the NavMeshAgent fully
        // controls movement. A non-kinematic Rigidbody with gravity fights the
        // agent on stairs/slopes/bridges (Ch5 = ApartmentBridge): gravity pulls
        // the Boomer down, the capsule collider catches on step edges, and the
        // two systems jitter — the Boomer slides in one direction. Kinematic
        // mode lets the agent drive position smoothly while still sending
        // collision events (for hit detection, triggers, etc.).
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = true; // kept on so the body can fall if the agent is disabled later.
        }

        if (animator != null)
        {
            animator.speed =
                Random.Range(0.95f, 1.05f);
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

        _lastSetDestination = transform.position;
        _pathTimer = maxRepathInterval; // force immediate first re-path
    }

    private void Update()
    {
        if (isDead)
            return;

        // Poll the death animation and fire the explosion when it is nearly
        // finished, so the blast syncs with the end of the animation instead
        // of firing too early (cutting the animation short).
        if (isWaitingForExplosion && !hasExploded)
        {
            TryFireExplosionFromAnimation();
        }

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
            agent.isStopped = true;
            if (animator != null) animator.SetFloat(SpeedHash, 0f, 0.2f, Time.deltaTime);
            return;
        }

        if (locomotion != null)
        {
            locomotion.target = target;
        }

        if (isHit)
            return;

        float distance =
            Vector3.Distance(
                transform.position,
                target.position
            );

        FaceTarget();

        //--------------------------------
        // DETECTION: check if we can see the player
        //--------------------------------
        bool hasLOSCurrently = false;
        if (!hasDetectedPlayer && !isScreaming && distance <= detectRange)
        {
            if (!requireLineOfSight)
                hasLOSCurrently = true;
            else
                hasLOSCurrently = HasLineOfSight();
        }

        bool shouldChase = false;
        if (forceDetectPlayer)
        {
            hasDetectedPlayer = true;
            shouldChase = true;
        }
        else if (hasDetectedPlayer && distance <= loseSightDistance)
        {
            shouldChase = true;
        }
        else if (!hasDetectedPlayer && hasLOSCurrently)
        {
            hasDetectedPlayer = true;
            shouldChase = true;
        }

        //--------------------------------
        // CHASE
        //--------------------------------
        if (shouldChase)
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

            HandleStuckDetection(distance);
        }
        else
        {
            hasDetectedPlayer = false;
            agent.isStopped = true;
        }

        //--------------------------------
        // SELF EXPLODE
        //--------------------------------

        if (!hasStartedExplosion &&
            distance <= explodeRange)
        {
            explosionType =
                ExplosionType.SelfExplode;

            StartExplosion();
        }

        //--------------------------------
        // ANIMATION SPEED
        //--------------------------------

        float speed =
            agent.velocity.magnitude /
            moveSpeed;

        if (animator != null) animator.SetFloat(SpeedHash, speed, 0.2f, Time.deltaTime);
    }

    private float findPlayerTimer;
    private Transform cachedPlayerTransform;

    private void FindPlayer()
    {
        if (cachedPlayerTransform != null)
        {
            target = cachedPlayerTransform;
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
            _targetStats = target.GetComponent<PlayerStats>();
        }
    }

    //==================================================
    // STUCK DETECTION & RECOVERY
    //==================================================

    private void HandleStuckDetection(float distanceToPlayer)
    {
        if (locomotion != null)
            locomotion.HandleStuckDetection(distanceToPlayer, explodeRange);
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

    private void FaceTarget()
    {
        if (locomotion != null)
            locomotion.FaceTarget(rotationSpeed);
    }

    //==================================
    // EXPLOSION SEQUENCE
    //==================================

    private void StartExplosion()
    {
        if (hasStartedExplosion)
            return;

        hasStartedExplosion = true;

        isScreaming = true;

        if (agent != null)
        {
            agent.isStopped = true;
        }

        if (animator != null) animator.SetBool(IsWarningHash, true);

        Invoke(
            nameof(PlayDeath),
            screamDuration
        );
    }

    private void PlayDeath()
    {
        if (animator != null)
        {
            animator.SetBool(IsWarningHash, false);
            animator.SetTrigger(ExplodeHash);
        }

        // The explosion is fired from Update() by polling the death animation
        // state so the blast syncs with the end of the animation instead of
        // firing at a fixed delay that may cut the animation short.
        isWaitingForExplosion = true;
        explosionWaitTimer = 0f;
        Invoke(nameof(DestroyEvent), cleanupDelay);
    }

    /// <summary>
    /// Polls the animator for the Death/Death 0 state and fires the explosion
    /// when the death animation is nearly finished (>= 90% normalized time).
    /// Falls back to a timed delay if the death state is never entered
    /// (e.g. animator missing or transition blocked).
    /// </summary>
    private void TryFireExplosionFromAnimation()
    {
        explosionWaitTimer += Time.deltaTime;

        if (animator == null)
        {
            ExplosionEvent();
            isWaitingForExplosion = false;
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        if (state.IsName("Death") || state.IsName("Death 0"))
        {
            if (state.normalizedTime >= 0.9f)
            {
                ExplosionEvent();
                isWaitingForExplosion = false;
            }
        }
        else if (explosionWaitTimer >= explodeFxDelay + 2f)
        {
            // Fallback: if we never entered the Death state within a reasonable
            // window, fire the explosion anyway so the boomer always explodes.
            ExplosionEvent();
            isWaitingForExplosion = false;
        }
    }

    //==================================
    // DAMAGE
    //==================================

    public void Damage(
        float damage,
        bool isHeadshot
    )
    {
        lastHitWasHeadshot = isHeadshot;

        if (CombatFeedbackHUD.Instance != null)
            CombatFeedbackHUD.Instance.ShowHit(transform.position, damage, isHeadshot);

        TakeDamage(
            Mathf.RoundToInt(Mathf.Abs(damage))
        );
    }

    public void TakeDamage(int damage)
    {
        // Once it has begun screaming/exploding, further hits no longer matter.
        if (isDead || isScreaming || hasStartedExplosion)
            return;

        currentHealth -= damage;

        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterDamageDealt(damage);

        if (animator != null) animator.SetTrigger(HitHash);

        if (OnHealthChanged != null)
            OnHealthChanged(HealthFraction);

        if (currentHealth <= 0)
        {
            explosionType =
                ExplosionType.Killed;

            GrantKillRewards();

            StartExplosion();
        }
    }

    // The player shot it down before it reached them: award bonus progression.
    // Self-detonation (reaching the player) grants nothing on purpose.
    private void GrantKillRewards()
    {
        if (rewardsGranted)
            return;

        rewardsGranted = true;

        var report = new KillReport
        {
            enemyInstanceID = gameObject.GetInstanceID(),
            killerName = "Player",
            victimName = "Boomer",
            weaponName = string.Empty,
            isHeadshot = lastHitWasHeadshot
        };
        UIEvents.onEnemyKilledDetailed?.Invoke(report);
        UIEvents.onEnemyKilled?.Invoke("Boomer");

        if (AIDirector.Instance != null)
            AIDirector.Instance.RegisterKill();

        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterBoomerKill();

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddKill();

        if (ExperienceManager.Instance != null && ExperienceManager.Instance.useExperience)
            ExperienceManager.Instance.AddExperience(experienceReward);

        if (CoinManager.Instance != null && CoinManager.Instance.useCoins)
            CoinManager.Instance.AddCoins(coinReward);
    }

    //==================================
    // ANIMATION EVENTS
    //==================================

    // Gọi tại frame nổ
    public void ExplosionEvent()
    {
        if (hasExploded)
            return;

        hasExploded = true;

        // Achievement tracking: check if the player was close to the explosion.
        if (AchievementManager.Instance != null)
            AchievementManager.Instance.NotifyBoomerCloseExplosion(transform.position);

        //--------------------------------
        // VFX
        //--------------------------------

        if (explosionPrefab != null)
        {
            cowsins.PoolManager.Instance.GetFromPool(
                explosionPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        //--------------------------------
        // ACID POOL
        // Raycast down from the boomer's position to find the ground so the
        // pool doesn't float in mid-air when the boomer explodes on top of a
        // car, roof, staircase, or any elevated surface.
        //--------------------------------

        Vector3 acidPos = transform.position;
        if (Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                Vector3.down,
                out RaycastHit acidHit,
                10f,
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            acidPos = acidHit.point;
        }

        GameObject acidPool = null;

        if (explosionType ==
            ExplosionType.Killed)
        {
            if (acidPoolDeathPrefab != null)
            {
                acidPool = cowsins.PoolManager.Instance.GetFromPool(
                    acidPoolDeathPrefab,
                    acidPos,
                    Quaternion.identity,
                    acidPoolLifetime
                );
            }
        }
        else
        {
            if (acidPoolSelfExplodePrefab != null)
            {
                acidPool = cowsins.PoolManager.Instance.GetFromPool(
                    acidPoolSelfExplodePrefab,
                    acidPos,
                    Quaternion.identity,
                    acidPoolLifetime
                );
            }
        }

        //--------------------------------
        // DAMAGE
        //--------------------------------

        int numHits = Physics.OverlapSphereNonAlloc(
                transform.position,
                explosionRadius,
                overlapColliders
            );

        for (int i = 0; i < numHits; i++)
        {
            Collider hit = overlapColliders[i];
            if (hit.transform == transform)
                continue;

            IDamageable damageable =
                hit.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageable.Damage(
                    explosionDamage,
                    false
                );

                // Feed the directional damage indicator when the blast hits the player.
                if (hit.CompareTag("Player") && DamageDirectionHUD.Instance != null)
                    DamageDirectionHUD.Instance.ShowDamageFrom(transform.position);
            }
        }
    }

    // Gọi ở frame cuối animation Death
    public void DestroyEvent()
    {
        if (hasDestroyed)
            return;

        hasDestroyed = true;

        isDead = true;

        if (agent != null)
        {
            agent.enabled = false;
        }

        // Chỉ rơi loot khi player bắn chết (Killed), không rơi khi tự nổ (SelfExplode).
        if (explosionType == ExplosionType.Killed)
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

        if (prefab != null)
        {
            cowsins.PoolManager.Instance.ReturnToPool(gameObject, prefab);
        }
        else
        {
            Destroy(gameObject);
        }
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
            explodeRange
        );

        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(
            transform.position,
            explosionRadius
        );
    }
}
