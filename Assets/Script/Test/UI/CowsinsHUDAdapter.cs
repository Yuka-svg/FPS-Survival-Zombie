using System;
using System.Collections;
using UnityEngine;
using cowsins;

/// <summary>
/// The SINGLE bridge between the Cowsins FPS engine and the custom AAA HUD.
/// This is the ONLY custom script that references cowsins.*. It reads
/// health/shield/ammo/weapon/reload/death from Cowsins (via engine events plus
/// authoritative property reads) and re-broadcasts engine-free C# events that
/// the HUD widgets consume. Widgets read the public properties on enable for
/// the current state, then subscribe to the events for live updates.
///
/// Design note: health/ammo handlers intentionally IGNORE the engine event's
/// numeric arguments and read the authoritative PlayerStats / WeaponIdentification
/// values directly, so we are immune to any argument-order assumptions.
/// </summary>
[DefaultExecutionOrder(-50)]
public class CowsinsHUDAdapter : MonoBehaviour
{
    public static CowsinsHUDAdapter Instance { get; private set; }

    // ---- Health / Shield ----
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public float Shield { get; private set; }
    public float MaxShield { get; private set; }
    /// <summary>(currentHealth, maxHealth, tookDamageThisChange)</summary>
    public event Action<float, float, bool> OnHealthChanged;
    /// <summary>(currentShield, maxShield)</summary>
    public event Action<float, float> OnShieldChanged;

    // ---- Ammo ----
    public int Ammo { get; private set; }
    public int MagazineSize { get; private set; }
    public int Reserve { get; private set; }
    public bool LimitedReserve { get; private set; }
    public float Heat { get; private set; }
    /// <summary>(ammoInMagazine, reserveBullets)</summary>
    public event Action<int, int> OnAmmoChanged;
    public event Action<float> OnHeatChanged;
    public event Action OnFired;

    // ---- Weapon ----
    public string WeaponName { get; private set; } = string.Empty;
    public Sprite WeaponIcon { get; private set; }
    public bool HasWeapon { get; private set; }
    /// <summary>(weaponName, weaponIcon)</summary>
    public event Action<string, Sprite> OnWeaponChanged;

    // ---- Reload ----
    public bool IsReloading { get; private set; }
    public float ReloadTime { get; private set; }
    public event Action<bool> OnReloadChanged;

    // ---- Death ----
    public bool IsDead { get; private set; }
    public event Action OnDied;

    // ---- Progression (Coins / XP) ----
    public int Coins { get; private set; }
    public int PlayerLevel { get; private set; }
    public float XpFill { get; private set; }
    public event Action<int> OnCoinsChanged;
    /// <summary>(displayLevel, xpFill01)</summary>
    public event Action<int, float> OnXpChanged;

    // ---- Dash ----
    public int CurrentDashes { get; private set; }
    public int MaxDashes { get; private set; }
    public bool InfiniteDashes { get; private set; }
    /// <summary>Seconds to regenerate one dash charge (PlayerMovementSettings.dashCooldown).</summary>
    public float DashCooldown => _pm != null ? _pm.playerSettings.dashCooldown : 0f;
    /// <summary>(currentDashes, maxDashes)</summary>
    public event Action<int, int> OnDashChanged;

    // ---- Interact ----
    public string InteractText { get; private set; } = string.Empty;
    /// <summary>(visible, displayText)</summary>
    public event Action<bool, string> OnInteractPrompt;
    public event Action OnInteractForbidden;
    /// <summary>interaction hold progress 0..1</summary>
    public event Action<float> OnInteractProgress;

    // ---- Weapon inventory ----
    public struct SlotInfo { public bool occupied; public string name; public Sprite icon; }
    public int InventorySize { get; private set; }
    public int SelectedWeaponIndex { get; private set; }
    public SlotInfo[] WeaponSlots { get; private set; } = new SlotInfo[0];
    public event Action OnInventoryStructureChanged;
    public event Action<int> OnWeaponSelected;

    // ---- Stamina ----
    // The engine (StaminaBehaviour) has no continuous stamina value event; its
    // currentStamina field is private. We read it via reflection every poll tick
    // and re-broadcast engine-free via OnStaminaChanged.
    public float Stamina { get; private set; }
    public float MaxStamina { get; private set; }
    public bool UsesStamina { get; private set; }
    /// <summary>(currentStamina, maxStamina)</summary>
    public event Action<float, float> OnStaminaChanged;

    // ---- Crosshair-supporting state ----
    // Live read-through of the movement/weapon providers so the custom CrosshairWidget
    // can reproduce the Cowsins dynamic crosshair (spread by movement, hide on ADS,
    // enemy-spotted colour) without touching cowsins.* itself. EnemySpotted is the only
    // stateful one (driven by the weapon's OnEnemySpotted event).
    public bool MoveGrounded => _moveState != null && _moveState.Grounded;
    public float MoveCurrentSpeed => _moveState != null ? _moveState.CurrentSpeed : 0f;
    public float MoveRunSpeed => _moveState != null ? _moveState.RunSpeed : 0f;
    public float MoveWalkSpeed => _moveState != null ? _moveState.WalkSpeed : 0f;
    public float MoveCrouchSpeed => _moveState != null ? _moveState.CrouchSpeed : 0f;
    public bool MoveIsIdle => _moveState != null && _moveState.IsIdle;
    public bool IsAiming => _weaponBehaviourProv != null && _weaponBehaviourProv.IsAiming;
    public bool EnemySpotted { get; private set; }
    public float WeaponCrosshairResize => (_weapon != null && _weapon.Weapon != null) ? _weapon.Weapon.crosshairResize : 0f;
    public event Action<bool> OnEnemySpottedChanged;

    // Equipped weapon's crosshair shape (engine-free mirror of Weapon_SO.crosshairParts).
    public bool CHTop { get; private set; } = true;
    public bool CHDown { get; private set; } = true;
    public bool CHLeft { get; private set; } = true;
    public bool CHRight { get; private set; } = true;
    public bool CHCenter { get; private set; }
    public bool CHTopLeft { get; private set; }
    public bool CHTopRight { get; private set; }
    public bool CHBottomLeft { get; private set; }
    public bool CHBottomRight { get; private set; }

    private bool _staticBound;

    private PlayerStats _stats;
    private PlayerStatsEvents _statsEvents;
    private WeaponController _weapon;
    private PlayerDependencies _deps;
    private PlayerMovement _pm;
    private System.Reflection.FieldInfo _staminaField;
    private float _lastStaminaPolled = -1f;
    private IPlayerMovementStateProvider _moveState;
    private IWeaponBehaviourProvider _weaponBehaviourProv;
    private PlayerMovementEvents _moveEvents;
    private InteractManagerEvents _interactEvents;
    private float _lastHealth = -1f;
    private bool _bound;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        StartCoroutine(AcquireAndBind());
    }

    private void OnDisable()
    {
        Unbind();
        UnsubscribeStatic();
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        Unbind();
        if (Instance == this) Instance = null;
    }

    private IEnumerator AcquireAndBind()
    {
        // Cowsins sets up PlayerStats / WeaponController in Start(); wait until present.
        float timeout = 12f;
        while (timeout > 0f && (_stats == null || _weapon == null))
        {
            if (_stats == null) _stats = FindAnyObjectByType<PlayerStats>();
            if (_weapon == null) _weapon = FindAnyObjectByType<WeaponController>();
            if (_deps == null) _deps = FindAnyObjectByType<PlayerDependencies>();
            if (_pm == null) _pm = FindAnyObjectByType<PlayerMovement>();
            // Cache stamina field reflection handle (one-time).
            if (_pm != null && _staminaField == null) CacheStaminaField();
            if (_stats != null && _weapon != null) break;
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (_stats == null || _weapon == null)
        {
            Debug.LogWarning("[CowsinsHUDAdapter] PlayerStats/WeaponController not found; HUD will stay idle.");
            yield break;
        }

        Bind();

        // Fallback: if the loop broke on stats/weapon before PlayerMovement appeared.
        if (_pm == null) _pm = FindAnyObjectByType<PlayerMovement>();
        if (_pm != null && _staminaField == null) CacheStaminaField();

        // Crosshair-supporting providers (populated in PlayerDependencies.Awake).
        // NOTE: This adapter has [DefaultExecutionOrder(-50)], so our OnEnable/AcquireAndBind
        // can run BEFORE PlayerDependencies.Awake() has populated these interface properties.
        // We therefore defer the cache into a wait-until-ready coroutine (same pattern as
        // BindDashWhenReady / BindInteractWhenReady) instead of reading them eagerly here.
        if (_deps == null) _deps = FindAnyObjectByType<PlayerDependencies>();
        StartCoroutine(BindCrosshairProvidersWhenReady());

        // Pull authoritative current values (init events may have fired in Start before we bound).
        PullHealth(false);
        PullWeapon();
        PullAmmo();
        BuildInventorySnapshot();

        SubscribeStatic();
        PullCoins();
        PullXp();

        StartCoroutine(PollHeat());
        StartCoroutine(BindDashWhenReady());
        StartCoroutine(BindInteractWhenReady());
    }

    private IEnumerator BindInteractWhenReady()
    {
        float timeout = 12f;
        while (timeout > 0f && (_deps == null || _deps.InteractEvents == null || _deps.InteractEvents.Events == null))
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (_deps == null || _deps.InteractEvents == null) yield break;
        _interactEvents = _deps.InteractEvents.Events;
        if (_interactEvents == null) yield break;
        _interactEvents.OnAllowedInteraction.AddListener(HandleAllowed);
        _interactEvents.OnForbiddenInteraction.AddListener(HandleForbidden);
        _interactEvents.OnDisableInteraction.AddListener(HandleInteractHide);
        _interactEvents.OnFinishInteraction.AddListener(HandleInteractHide);
        _interactEvents.OnInteractionProgressChanged.AddListener(HandleInteractProgress);
    }

    // Crosshair-supporting providers (PlayerMovementState, WeaponBehaviour) are assigned in
    // PlayerDependencies.Awake(). Since this adapter runs at DefaultExecutionOrder(-50),
    // Awake on PlayerDependencies may not have run yet when AcquireAndBind reaches the
    // provider-cache section. This coroutine waits until those interfaces are non-null,
    // then caches them so the CrosshairWidget can read IsAiming / movement state live.
    private IEnumerator BindCrosshairProvidersWhenReady()
    {
        float timeout = 12f;
        while (timeout > 0f && (_deps == null || _deps.PlayerMovementState == null || _deps.WeaponBehaviour == null))
        {
            if (_deps == null) _deps = FindAnyObjectByType<PlayerDependencies>();
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (_deps == null) yield break;
        _moveState = _deps.PlayerMovementState;
        _weaponBehaviourProv = _deps.WeaponBehaviour;
    }

    private void HandleAllowed(string text) { InteractText = text; OnInteractPrompt?.Invoke(true, text); }
    private void HandleForbidden() { OnInteractForbidden?.Invoke(); OnInteractPrompt?.Invoke(false, string.Empty); }
    private void HandleInteractHide() { OnInteractPrompt?.Invoke(false, string.Empty); OnInteractProgress?.Invoke(0f); }
    private void HandleInteractProgress(float v) { OnInteractProgress?.Invoke(v); }

    // Provider events (PlayerDependencies.PlayerMovementEvents) are populated in the
    // player's Awake, which may run after our Bind(); wait for them, then subscribe dash.
    private IEnumerator BindDashWhenReady()
    {
        float timeout = 12f;
        while (timeout > 0f && (_deps == null || _deps.PlayerMovementEvents == null || _deps.PlayerMovementEvents.Events == null))
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (_deps == null || _deps.PlayerMovementEvents == null) yield break;
        _moveEvents = _deps.PlayerMovementEvents.Events;
        if (_moveEvents == null) yield break;
        _moveEvents.OnInitializeDash.AddListener(HandleInitDash);
        _moveEvents.OnDashUsed.AddListener(HandleDashUsed);
        _moveEvents.OnDashGained.AddListener(HandleDashGained);
    }

    private void Bind()
    {
        if (_bound) return;

        _statsEvents = (_stats as IPlayerStatsEventsProvider)?.Events;
        if (_statsEvents != null)
        {
            _statsEvents.OnHealthChanged.AddListener(HandleHealthChanged);
            _statsEvents.OnInitializeHealth.AddListener(HandleInitializeHealth);
        }
        _stats.AddOnDieListener(HandleDied);

        // Listen for respawn to reset the death state (restores crosshair).
        if (_moveEvents != null)
            _moveEvents.OnRespawn.AddListener(HandleRespawn);

        var e = _weapon.Events;
        if (e != null)
        {
            e.OnAmmoChanged.AddListener(HandleAmmoChanged);
            e.OnStartReload.AddListener(HandleStartReload);
            e.OnFinishReload.AddListener(HandleFinishReload);
            e.OnSelectWeapon.AddListener(HandleWeaponChanged);
            e.OnUnholster.AddListener(HandleUnholster);
            e.OnShoot.AddListener(HandleShoot);
            e.OnInitializeWeaponSystem.AddListener(HandleInitInventory);
            e.OnWeaponInventoryChanged.AddListener(HandleSlotChanged);
            e.OnEnemySpotted.AddListener(HandleEnemySpotted);
        }

        _bound = true;
    }

    private void Unbind()
    {
        if (!_bound) return;

        if (_statsEvents != null)
        {
            _statsEvents.OnHealthChanged.RemoveListener(HandleHealthChanged);
            _statsEvents.OnInitializeHealth.RemoveListener(HandleInitializeHealth);
        }
        if (_stats != null) _stats.RemoveOnDieListener(HandleDied);

        if (_moveEvents != null)
            _moveEvents.OnRespawn.RemoveListener(HandleRespawn);

        if (_weapon != null && _weapon.Events != null)
        {
            var e = _weapon.Events;
            e.OnAmmoChanged.RemoveListener(HandleAmmoChanged);
            e.OnStartReload.RemoveListener(HandleStartReload);
            e.OnFinishReload.RemoveListener(HandleFinishReload);
            e.OnSelectWeapon.RemoveListener(HandleWeaponChanged);
            e.OnUnholster.RemoveListener(HandleUnholster);
            e.OnShoot.RemoveListener(HandleShoot);
            e.OnInitializeWeaponSystem.RemoveListener(HandleInitInventory);
            e.OnWeaponInventoryChanged.RemoveListener(HandleSlotChanged);
            e.OnEnemySpotted.RemoveListener(HandleEnemySpotted);
        }

        if (_moveEvents != null)
        {
            _moveEvents.OnInitializeDash.RemoveListener(HandleInitDash);
            _moveEvents.OnDashUsed.RemoveListener(HandleDashUsed);
            _moveEvents.OnDashGained.RemoveListener(HandleDashGained);
        }

        if (_interactEvents != null)
        {
            _interactEvents.OnAllowedInteraction.RemoveListener(HandleAllowed);
            _interactEvents.OnForbiddenInteraction.RemoveListener(HandleForbidden);
            _interactEvents.OnDisableInteraction.RemoveListener(HandleInteractHide);
            _interactEvents.OnFinishInteraction.RemoveListener(HandleInteractHide);
            _interactEvents.OnInteractionProgressChanged.RemoveListener(HandleInteractProgress);
        }

        _bound = false;
    }

    // ---- Health (read authoritative props; ignore event float args) ----
    private void HandleInitializeHealth(float a, float b, float c, float d) => PullHealth(false);
    private void HandleHealthChanged(float a, float b, bool damaged) => PullHealth(true);

    private void PullHealth(bool fromChange)
    {
        if (_stats == null) return;
        Health = _stats.Health;
        MaxHealth = _stats.MaxHealth;
        Shield = _stats.Shield;
        MaxShield = _stats.MaxShield;
        bool tookDamage = fromChange && _lastHealth >= 0f && Health < _lastHealth - 0.001f;
        _lastHealth = Health;
        OnHealthChanged?.Invoke(Health, MaxHealth, tookDamage);
        OnShieldChanged?.Invoke(Shield, MaxShield);
    }

    private void HandleDied()
    {
        IsDead = true;
        OnDied?.Invoke();
    }

    private void HandleRespawn(Vector3 pos, Quaternion rot, bool resetStamina, bool resetDashes)
    {
        IsDead = false;
    }

    // ---- Ammo ----
    private void HandleAmmoChanged(bool auto) => PullAmmo();

    private void PullAmmo()
    {
        if (_weapon == null) return;
        var id = _weapon.Id;
        var w = _weapon.Weapon;
        if (id != null)
        {
            Ammo = id.bulletsLeftInMagazine;
            MagazineSize = id.magazineSize;
            Reserve = id.totalBullets;
            Heat = id.heatRatio;
        }
        LimitedReserve = w != null && w.limitedMagazines;
        OnAmmoChanged?.Invoke(Ammo, Reserve);
    }

    private IEnumerator PollHeat()
    {
        var wait = new WaitForSecondsRealtime(0.05f);
        while (true)
        {
            if (_weapon != null && _weapon.Id != null)
            {
                float h = _weapon.Id.heatRatio;
                if (!Mathf.Approximately(h, Heat))
                {
                    Heat = h;
                    OnHeatChanged?.Invoke(Heat);
                }
            }
            if (CoinManager.Instance != null && CoinManager.Instance.coins != Coins) PullCoins();
            var xpm = ExperienceManager.Instance;
            if (xpm != null && xpm.GetPlayerLevel() != PlayerLevel) PullXp();
            PollStamina();
            yield return wait;
        }
    }

    // ---- Weapon ----
    private void HandleWeaponChanged() { PullWeapon(); var wref = _weapon as IWeaponReferenceProvider; if (wref != null) { SelectedWeaponIndex = wref.CurrentWeaponIndex; OnWeaponSelected?.Invoke(SelectedWeaponIndex); } }
    private void HandleUnholster(bool a, bool b) => PullWeapon();

    public string CaliberText { get; private set; } = "9MM NATO";
    public string FireModeText { get; private set; } = "AUTO";

    private void PullWeapon()
    {
        if (_weapon == null) return;
        var w = _weapon.Weapon;
        if (w != null)
        {
            HasWeapon = true;
            WeaponName = w._name;
            WeaponIcon = w.icon;
            ReloadTime = w.reloadTime;

            string n = (w._name ?? string.Empty).ToUpperInvariant();
            if (n.Contains("PISTOL") || n.Contains("REVOLVER") || n.Contains("HANDGUN"))
            {
                CaliberText = "9MM NATO";
                FireModeText = "SEMI";
            }
            else if (n.Contains("SHOTGUN"))
            {
                CaliberText = "12 GAUGE";
                FireModeText = "PUMP";
            }
            else if (n.Contains("SNIPER"))
            {
                CaliberText = ".50 BMG";
                FireModeText = "BOLT";
            }
            else if (n.Contains("SMG"))
            {
                CaliberText = "9MM";
                FireModeText = "BURST";
            }
            else
            {
                CaliberText = "5.56MM";
                FireModeText = "AUTO";
            }
        }
        else
        {
            HasWeapon = false;
            WeaponName = string.Empty;
            WeaponIcon = null;
            ReloadTime = 0f;
            CaliberText = string.Empty;
            FireModeText = string.Empty;
        }

        var cp = w != null ? w.crosshairParts : null;
        CHTop = cp != null && cp.topPart;
        CHDown = cp != null && cp.downPart;
        CHLeft = cp != null && cp.leftPart;
        CHRight = cp != null && cp.rightPart;
        CHCenter = cp != null && cp.center;
        CHTopLeft = cp != null && cp.topLeftBracket;
        CHTopRight = cp != null && cp.topRightBracket;
        CHBottomLeft = cp != null && cp.bottomLeftBracket;
        CHBottomRight = cp != null && cp.bottomRightBracket;
        OnWeaponChanged?.Invoke(WeaponName, WeaponIcon);
        PullAmmo();
    }

    // ---- Reload ----
    private void HandleStartReload()
    {
        IsReloading = true;
        OnReloadChanged?.Invoke(true);
    }

    private void HandleFinishReload()
    {
        IsReloading = false;
        OnReloadChanged?.Invoke(false);
        PullAmmo();
    }

    // ---- Shoot (for fire-punch animation; ammo number handled by OnAmmoChanged) ----
    private void HandleShoot() => OnFired?.Invoke();

    // ---- Enemy-spotted (crosshair colour) ----
    private void HandleEnemySpotted(bool spotted)
    {
        if (EnemySpotted == spotted) return;
        EnemySpotted = spotted;
        OnEnemySpottedChanged?.Invoke(spotted);
    }

    // ---- Weapon inventory ----
    private void EnsureSlots(int size)
    {
        if (size < 0) size = 0;
        if (WeaponSlots.Length != size)
        {
            var n = new SlotInfo[size];
            for (int i = 0; i < Mathf.Min(size, WeaponSlots.Length); i++) n[i] = WeaponSlots[i];
            WeaponSlots = n;
        }
        InventorySize = size;
    }
    private void HandleInitInventory(int size) { EnsureSlots(size); OnInventoryStructureChanged?.Invoke(); }
    private void HandleSlotChanged(int index, Weapon_SO w)
    {
        if (index < 0) return;
        if (index >= WeaponSlots.Length) EnsureSlots(index + 1);
        WeaponSlots[index] = new SlotInfo { occupied = w != null, name = w != null ? w._name : string.Empty, icon = w != null ? w.icon : null };
        OnInventoryStructureChanged?.Invoke();
    }
    private void BuildInventorySnapshot()
    {
        var wref = _weapon as IWeaponReferenceProvider;
        if (wref == null || wref.Inventory == null) return;
        EnsureSlots(wref.Inventory.Length);
        for (int i = 0; i < wref.Inventory.Length; i++)
        {
            var id = wref.Inventory[i];
            var w = id != null ? id.weapon : null;
            WeaponSlots[i] = new SlotInfo { occupied = w != null, name = w != null ? w._name : string.Empty, icon = w != null ? w.icon : null };
        }
        SelectedWeaponIndex = wref.CurrentWeaponIndex;
        OnInventoryStructureChanged?.Invoke();
        OnWeaponSelected?.Invoke(SelectedWeaponIndex);
    }

    // ---- Dash ----
    private void HandleInitDash(int max) { MaxDashes = max; CurrentDashes = max; InfiniteDashes = false; OnDashChanged?.Invoke(CurrentDashes, MaxDashes); }
    private void HandleDashUsed(int cur) { CurrentDashes = cur; if (cur > MaxDashes) MaxDashes = cur; OnDashChanged?.Invoke(CurrentDashes, MaxDashes); }
    private void HandleDashGained(int cur) { CurrentDashes = cur; if (cur > MaxDashes) MaxDashes = cur; OnDashChanged?.Invoke(CurrentDashes, MaxDashes); }

    private void CacheStaminaField()
    {
        if (_pm == null || _staminaField != null) return;
        if (_pm.staminaBehaviour == null) return;
        _staminaField = typeof(StaminaBehaviour).GetField("currentStamina",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (_staminaField == null) return;
        UsesStamina = _pm.playerSettings.usesStamina;
        float max = _pm.playerSettings.maxStamina;
        MaxStamina = max;
        Stamina = (float)_staminaField.GetValue(_pm.staminaBehaviour);
        _lastStaminaPolled = Stamina;
        OnStaminaChanged?.Invoke(Stamina, MaxStamina);
    }

    private void PollStamina()
    {
        if (_pm == null) return;
        if (_staminaField == null) { CacheStaminaField(); if (_staminaField == null) return; }
        float current = (float)_staminaField.GetValue(_pm.staminaBehaviour);
        float mx = _pm.playerSettings.maxStamina;
        UsesStamina = _pm.playerSettings.usesStamina;
        if (!Mathf.Approximately(current, _lastStaminaPolled) || !Mathf.Approximately(mx, MaxStamina))
        {
            Stamina = current;
            MaxStamina = mx;
            _lastStaminaPolled = current;
            OnStaminaChanged?.Invoke(Stamina, MaxStamina);
        }
    }

    // ---- Progression: Coins / XP (Cowsins static UIEvents) ----
    private void SubscribeStatic()
    {
        if (_staticBound) return;
        UIEvents.onCoinsChange += HandleCoinsChanged;
        UIEvents.onExperienceCollected += HandleXpChanged;
        _staticBound = true;
    }

    private void UnsubscribeStatic()
    {
        if (!_staticBound) return;
        UIEvents.onCoinsChange -= HandleCoinsChanged;
        UIEvents.onExperienceCollected -= HandleXpChanged;
        _staticBound = false;
    }

    private void HandleCoinsChanged(int amount, bool updatePanel) => PullCoins();
    private void HandleXpChanged(bool updatePanel) => PullXp();

    private void PullCoins()
    {
        Coins = CoinManager.Instance != null ? CoinManager.Instance.coins : Coins;
        OnCoinsChanged?.Invoke(Coins);
    }

    private void PullXp()
    {
        var xp = ExperienceManager.Instance;
        if (xp == null) { OnXpChanged?.Invoke(PlayerLevel, XpFill); return; }
        PlayerLevel = xp.GetPlayerLevel();
        float fill = 0f;
        float req = xp.GetRequirement(xp.playerLevel);
        if (req > 0)
            fill = Mathf.Clamp01(xp.GetCurrentExperience() / req);
        XpFill = fill;
        OnXpChanged?.Invoke(PlayerLevel, XpFill);
    }
}
