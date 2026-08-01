using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using cowsins;

public class CombatFeedbackHUD : MonoBehaviour
{
    public static CombatFeedbackHUD Instance;

    public Camera worldCamera;

    [Header("SVG Vector Icons")]
    [SerializeField] private VectorImage iconZombie;
    [SerializeField] private VectorImage iconHeadshot;
    [SerializeField] private VectorImage iconWeaponRifle;
    [SerializeField] private VectorImage iconWeaponPistol;
    [SerializeField] private VectorImage iconWeaponShotgun;
    [SerializeField] private VectorImage iconWeaponMelee;
    [SerializeField] private VectorImage iconSkull;

    [Header("Sprite Fallbacks")]
    [SerializeField] private Sprite spriteZombie;
    [SerializeField] private Sprite spriteHeadshot;
    [SerializeField] private Sprite spriteWeaponRifle;
    [SerializeField] private Sprite spriteWeaponPistol;
    [SerializeField] private Sprite spriteWeaponShotgun;
    [SerializeField] private Sprite spriteWeaponMelee;
    [SerializeField] private Sprite spriteSkull;

    private static readonly string[] _numberStrings = new string[1000];
    private static string GetDamageString(int dmg, bool crit)
    {
        if (dmg >= 0 && dmg < 1000)
        {
            if (_numberStrings[dmg] == null)
                _numberStrings[dmg] = dmg.ToString();
            return crit ? (_numberStrings[dmg] + "!") : _numberStrings[dmg];
        }
        return crit ? (dmg + "!") : dmg.ToString();
    }

    private VisualElement _root, _hitmarker, _dmgContainer, _killContainer;
    private readonly List<VisualElement> _hitBars = new List<VisualElement>();
    private float _hitTimer, _hitDuration = 0.18f;

    private bool _critPending;
    private int _critFrame;
    public void FlagCriticalHit() { _critPending = true; _critFrame = Time.frameCount; }

    private class Dmg { public VisualElement ve; public Label label; public float life; public Vector3 worldPos; public Vector3 worldVel; public float scaleMultiplier; public bool active; }
    private readonly List<Dmg> _pool = new List<Dmg>();
    private const int PoolSize = 28;
    private float _dmgLife = 1.0f;

    public enum KillState { Pooled, Active, Exiting }
    private class Kill
    {
        public VisualElement entry;
        public Label killerLabel;
        public VisualElement killerIcon;
        public VisualElement weaponIcon;
        public VisualElement headshotBadge;
        public Label victimLabel;
        public VisualElement zombieIcon;
        public KillState state = KillState.Pooled;
        public int generation;
        public float spawnTime;
        public IVisualElementScheduledItem entranceSchedule;
        public IVisualElementScheduledItem exitSchedule;
        public EventCallback<TransitionEndEvent> transEndCb;
    }

    private readonly List<Kill> _activeKills = new List<Kill>();
    private readonly List<Kill> _exitingKills = new List<Kill>();
    private readonly List<Kill> _killPool = new List<Kill>();
    private readonly Queue<KillReport> _pendingKillQueue = new Queue<KillReport>();
    private IVisualElementScheduledItem _queuePumpSchedule;
    private float _lastKillTime;

    private void Awake()
    {
        Instance = this;
        LoadIconResourcesFallback();
    }

    private void LoadIconResourcesFallback()
    {
        if (iconZombie == null) iconZombie = Resources.Load<VectorImage>("Icons/icon_zombie");
        if (iconHeadshot == null) iconHeadshot = Resources.Load<VectorImage>("Icons/icon_headshot");
        if (iconWeaponRifle == null) iconWeaponRifle = Resources.Load<VectorImage>("Icons/icon_weapon_rifle");
        if (iconWeaponPistol == null) iconWeaponPistol = Resources.Load<VectorImage>("Icons/icon_weapon_pistol");
        if (iconWeaponShotgun == null) iconWeaponShotgun = Resources.Load<VectorImage>("Icons/icon_weapon_shotgun");
        if (iconWeaponMelee == null) iconWeaponMelee = Resources.Load<VectorImage>("Icons/icon_weapon_melee");
        if (iconSkull == null) iconSkull = Resources.Load<VectorImage>("Icons/icon_skull");

        if (spriteZombie == null) spriteZombie = Resources.Load<Sprite>("Icons/icon_zombie");
        if (spriteHeadshot == null) spriteHeadshot = Resources.Load<Sprite>("Icons/icon_headshot");
        if (spriteWeaponRifle == null) spriteWeaponRifle = Resources.Load<Sprite>("Icons/icon_weapon_rifle");
        if (spriteWeaponPistol == null) spriteWeaponPistol = Resources.Load<Sprite>("Icons/icon_weapon_pistol");
        if (spriteWeaponShotgun == null) spriteWeaponShotgun = Resources.Load<Sprite>("Icons/icon_weapon_shotgun");
        if (spriteWeaponMelee == null) spriteWeaponMelee = Resources.Load<Sprite>("Icons/icon_weapon_melee");
        if (spriteSkull == null) spriteSkull = Resources.Load<Sprite>("Icons/icon_skull");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UIEvents.onEnemyKilledDetailed -= OnEnemyKilledDetailed;
        _queuePumpSchedule?.Pause();
        _queuePumpSchedule = null;
    }

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        _root = doc.rootVisualElement != null ? doc.rootVisualElement.Q("CombatFeedbackHUD") : null;
        if (_root == null) return;
        _hitmarker = _root.Q("Hitmarker");
        _dmgContainer = _root.Q("DamageNumbers");
        _killContainer = doc.rootVisualElement != null ? doc.rootVisualElement.Q("Killfeed") : null;

        BuildHitmarkerBars();
        BuildDamagePool();

        UIEvents.onEnemyKilledDetailed += OnEnemyKilledDetailed;
        _queuePumpSchedule = _root.schedule.Execute(ProcessPendingKillQueue).Every(30);
    }

    private void OnDisable()
    {
        UIEvents.onEnemyKilledDetailed -= OnEnemyKilledDetailed;
        _queuePumpSchedule?.Pause();
        _queuePumpSchedule = null;
        PurgeAllKillsToPool();
    }

    private void PurgeAllKillsToPool()
    {
        for (int i = _activeKills.Count - 1; i >= 0; i--)
            ReturnToPool(_activeKills[i]);
        _activeKills.Clear();

        for (int i = _exitingKills.Count - 1; i >= 0; i--)
            ReturnToPool(_exitingKills[i]);
        _exitingKills.Clear();

        _pendingKillQueue.Clear();
    }

    private void BuildHitmarkerBars()
    {
        if (_hitmarker == null || _hitmarker.childCount > 0) return;
        string[] classes = { "hm-tl", "hm-tr", "hm-bl", "hm-br" };
        for (int i = 0; i < 4; i++)
        {
            var bar = new VisualElement();
            bar.AddToClassList("hitmarker-bar");
            bar.AddToClassList(classes[i]);
            bar.usageHints = UsageHints.DynamicColor;
            _hitmarker.Add(bar);
            _hitBars.Add(bar);
        }
    }

    private void BuildDamagePool()
    {
        if (_dmgContainer == null || _dmgContainer.childCount > 0) return;
        for (int i = 0; i < PoolSize; i++)
        {
            var ve = new VisualElement();
            ve.AddToClassList("dmg-number");
            ve.usageHints = UsageHints.DynamicTransform | UsageHints.DynamicColor;
            ve.AddToClassList("dmg-number--hidden");
            var label = new Label();
            ve.Add(label);
            _dmgContainer.Add(ve);
            _pool.Add(new Dmg { ve = ve, label = label, active = false });
        }
    }

    private void OnEnemyKilledDetailed(KillReport report)
    {
        _pendingKillQueue.Enqueue(report);
    }

    private void ProcessPendingKillQueue()
    {
        if (_pendingKillQueue.Count == 0) return;
        float interval = _pendingKillQueue.Count > 5 ? 0.08f : 0.18f;
        if (Time.unscaledTime - _lastKillTime < interval) return;

        var report = _pendingKillQueue.Dequeue();
        _lastKillTime = Time.unscaledTime;
        SpawnKillEntry(report);
    }

    private void SpawnKillEntry(KillReport report)
    {
        if (_killContainer == null)
        {
            var doc = GetComponent<UIDocument>();
            if (doc != null && doc.rootVisualElement != null)
                _killContainer = doc.rootVisualElement.Q("Killfeed");
        }
        if (_killContainer == null) return;

        Kill k = GetPooledKill();

        k.state = KillState.Active;
        k.spawnTime = Time.unscaledTime;
        int currentGen = ++k.generation;

        string kName = string.IsNullOrEmpty(report.killerName) ? "Player" : report.killerName;
        string vName = string.IsNullOrEmpty(report.victimName) ? "Zombie" : report.victimName;
        string wName = string.IsNullOrEmpty(report.weaponName) ? GetActivePlayerWeaponName() : report.weaponName;

        k.killerLabel.text = kName;
        k.victimLabel.text = vName;

        var (weaponSvg, weaponSprite) = GetWeaponVectorImage(wName);
        SetIcon(k.weaponIcon, weaponSvg, weaponSprite);
        SetIcon(k.zombieIcon, iconZombie, spriteZombie);

        if (report.isHeadshot)
        {
            k.headshotBadge.style.display = DisplayStyle.Flex;
            SetIcon(k.headshotBadge, iconHeadshot, spriteHeadshot);
        }
        else
        {
            k.headshotBadge.style.display = DisplayStyle.None;
        }

        k.entry.style.display = DisplayStyle.Flex;
        k.entry.RemoveFromClassList("killfeed-entry--exiting");
        k.entry.AddToClassList("killfeed-entry--entering");
        k.entry.RemoveFromHierarchy();

        _killContainer.Insert(0, k.entry);
        _activeKills.Add(k);

        k.entranceSchedule = k.entry.schedule.Execute(() =>
        {
            if (k.generation == currentGen && k.state == KillState.Active)
            {
                k.entry.RemoveFromClassList("killfeed-entry--entering");
            }
        }).StartingIn(16);

        CheckEvictionLimits();
    }

    private string GetActivePlayerWeaponName()
    {
        var wc = Object.FindFirstObjectByType<WeaponController>();
        if (wc != null && wc.Weapon != null && !string.IsNullOrEmpty(wc.Weapon._name))
            return wc.Weapon._name;
        return "Rifle";
    }

    private void CheckEvictionLimits()
    {
        while (_activeKills.Count >= 5)
        {
            var oldest = _activeKills[0];
            _activeKills.RemoveAt(0);
            TransitionToExiting(oldest);
        }

        while (_exitingKills.Count >= 3)
        {
            var oldestExit = _exitingKills[0];
            _exitingKills.RemoveAt(0);
            ReturnToPool(oldestExit);
        }
    }

    private void TransitionToExiting(Kill k)
    {
        if (k.state != KillState.Active) return;
        k.state = KillState.Exiting;
        _exitingKills.Add(k);
        int currentGen = k.generation;

        k.entry.AddToClassList("killfeed-entry--exiting");

        k.transEndCb = evt =>
        {
            if (k.generation == currentGen && k.state == KillState.Exiting && evt.target == k.entry && evt.stylePropertyNames.Contains("height"))
            {
                _exitingKills.Remove(k);
                ReturnToPool(k);
            }
        };
        k.entry.RegisterCallback(k.transEndCb);

        k.exitSchedule = k.entry.schedule.Execute(() =>
        {
            if (k.generation == currentGen && k.state == KillState.Exiting)
            {
                _exitingKills.Remove(k);
                ReturnToPool(k);
            }
        }).StartingIn(400);
    }

    private void ReturnToPool(Kill k)
    {
        k.entranceSchedule?.Pause();
        k.entranceSchedule = null;

        k.exitSchedule?.Pause();
        k.exitSchedule = null;

        if (k.transEndCb != null)
        {
            k.entry.UnregisterCallback(k.transEndCb);
            k.transEndCb = null;
        }

        k.entry.style.display = DisplayStyle.None;
        k.entry.style.translate = new StyleTranslate(StyleKeyword.Null);
        k.entry.style.scale = new StyleScale(StyleKeyword.Null);
        k.entry.style.height = StyleKeyword.Null;
        k.entry.style.marginBottom = StyleKeyword.Null;
        k.entry.style.opacity = StyleKeyword.Null;
        k.entry.style.transitionDuration = StyleKeyword.Null;
        k.entry.style.backgroundImage = StyleKeyword.Null;

        k.entry.RemoveFromClassList("killfeed-entry--entering");
        k.entry.RemoveFromClassList("killfeed-entry--exiting");
        k.entry.RemoveFromHierarchy();

        k.state = KillState.Pooled;
        k.generation++;
    }

    private Kill GetPooledKill()
    {
        for (int i = 0; i < _killPool.Count; i++)
        {
            if (_killPool[i].state == KillState.Pooled)
                return _killPool[i];
        }

        var entry = new VisualElement();
        entry.AddToClassList("killfeed-entry");
        entry.usageHints = UsageHints.DynamicTransform;

        var killerLabel = new Label();
        killerLabel.AddToClassList("killfeed-label");

        var killerIcon = new VisualElement();
        killerIcon.AddToClassList("killfeed-icon--avatar");

        var weaponIcon = new VisualElement();
        weaponIcon.AddToClassList("killfeed-icon--weapon");

        var headshotBadge = new VisualElement();
        headshotBadge.AddToClassList("killfeed-icon--badge");

        var victimLabel = new Label();
        victimLabel.AddToClassList("killfeed-label");

        var zombieIcon = new VisualElement();
        zombieIcon.AddToClassList("killfeed-icon--victim");

        entry.Add(killerLabel);
        entry.Add(killerIcon);
        entry.Add(weaponIcon);
        entry.Add(headshotBadge);
        entry.Add(victimLabel);
        entry.Add(zombieIcon);

        var k = new Kill
        {
            entry = entry,
            killerLabel = killerLabel,
            killerIcon = killerIcon,
            weaponIcon = weaponIcon,
            headshotBadge = headshotBadge,
            victimLabel = victimLabel,
            zombieIcon = zombieIcon,
            state = KillState.Pooled
        };
        _killPool.Add(k);
        return k;
    }

    private (VectorImage svg, Sprite sprite) GetWeaponVectorImage(string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return (iconSkull, spriteSkull);
        string lower = weaponName.ToLowerInvariant();

        if (lower.Contains("rifle") || lower.Contains("ak") || lower.Contains("m4") || lower.Contains("assault") || lower.Contains("smg"))
            return (iconWeaponRifle, spriteWeaponRifle);
        if (lower.Contains("pistol") || lower.Contains("glock") || lower.Contains("revolver") || lower.Contains("handgun"))
            return (iconWeaponPistol, spriteWeaponPistol);
        if (lower.Contains("shotgun") || lower.Contains("pump") || lower.Contains("gauge"))
            return (iconWeaponShotgun, spriteWeaponShotgun);
        if (lower.Contains("knife") || lower.Contains("melee") || lower.Contains("blade") || lower.Contains("sword"))
            return (iconWeaponMelee, spriteWeaponMelee);

        return (iconSkull, spriteSkull);
    }

    private void SetIcon(VisualElement element, VectorImage vectorSvg, Sprite spriteImg)
    {
        if (element == null) return;
        if (vectorSvg != null)
        {
            element.style.display = DisplayStyle.Flex;
            element.style.backgroundImage = Background.FromVectorImage(vectorSvg);
        }
        else if (spriteImg != null)
        {
            element.style.display = DisplayStyle.Flex;
            element.style.backgroundImage = Background.FromSprite(spriteImg);
        }
        else
        {
            element.style.backgroundImage = StyleKeyword.Null;
            element.style.display = DisplayStyle.None;
        }
    }

    public void ShowHit(Vector3 worldPos, float damage, bool headshot)
    {
        bool crit = _critPending && Time.frameCount == _critFrame;
        _critPending = false;

        _hitTimer = _hitDuration;
        if (_hitmarker != null)
        {
            _hitmarker.EnableInClassList("hitmarker--visible", true);
            float scale = crit ? 1.6f : headshot ? 1.4f : 1f;
            _hitmarker.style.scale = new Scale(Vector2.one * scale);
            foreach (var bar in _hitBars)
            {
                bar.EnableInClassList("hitmarker-bar--kill", headshot);
                bar.EnableInClassList("hitmarker-bar--crit", crit);
            }
        }

        var d = GetDmg();
        if (d == null) return;

        d.worldPos = worldPos + Vector3.up * 1.5f;
        
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float planarSpeed = Random.Range(1.5f, 3.5f);
        float upwardSpeed = Random.Range(4.5f, 7.0f);
        d.worldVel = new Vector3(Mathf.Cos(angle) * planarSpeed, upwardSpeed, Mathf.Sin(angle) * planarSpeed);

        d.scaleMultiplier = (crit || headshot) ? 1.5f : 1.0f;
        d.life = _dmgLife;

        int dmgInt = Mathf.Max(1, Mathf.RoundToInt(damage));
        d.label.text = GetDamageString(dmgInt, crit);
        
        d.ve.EnableInClassList("dmg-number--kill", headshot);
        d.ve.EnableInClassList("dmg-number--crit", crit);
        d.label.EnableInClassList("dmg-number--kill", headshot);
        d.label.EnableInClassList("dmg-number--crit", crit);

        var cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(d.worldPos);
            if (sp.z < 0f) { d.active = false; d.ve.AddToClassList("dmg-number--hidden"); return; }
            var doc = GetComponent<UIDocument>();
            if (doc != null && doc.rootVisualElement != null)
            {
                var panel = doc.rootVisualElement.panel;
                if (panel != null)
                {
                    var panelPos = RuntimePanelUtils.ScreenToPanel(panel, sp);
                    d.ve.style.left = panelPos.x;
                    d.ve.style.top = panelPos.y;

                    float dist = Vector3.Distance(cam.transform.position, d.worldPos);
                    dist = Mathf.Max(2f, dist);
                    float distanceScale = 12f / dist;
                    distanceScale = Mathf.Clamp(distanceScale, 0.4f, 2.5f);
                    float finalScale = d.scaleMultiplier * distanceScale;

                    d.ve.style.scale = new Scale(Vector2.one * finalScale);
                    d.ve.style.opacity = 1f;
                }
            }
        }

        d.ve.RemoveFromClassList("dmg-number--hidden");
        d.active = true;
    }

    public void ShowKill(string name)
    {
        UIEvents.onEnemyKilledDetailed?.Invoke(new KillReport
        {
            enemyInstanceID = 0,
            killerName = "Player",
            victimName = name,
            weaponName = GetActivePlayerWeaponName(),
            isHeadshot = false
        });
    }

    private Dmg GetDmg()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].active) return _pool[i];
        Dmg oldest = null;
        float min = float.MaxValue;
        foreach (var d in _pool)
            if (d.life < min) { min = d.life; oldest = d; }
        return oldest;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (_critPending && Time.frameCount != _critFrame)
            _critPending = false;

        if (_hitTimer > 0f)
        {
            _hitTimer -= dt;
            if (_hitTimer <= 0f && _hitmarker != null) _hitmarker.EnableInClassList("hitmarker--visible", false);
        }

        var cam = worldCamera != null ? worldCamera : Camera.main;
        var doc = GetComponent<UIDocument>();
        var panel = doc != null && doc.rootVisualElement != null ? doc.rootVisualElement.panel : null;

        for (int i = 0; i < _pool.Count; i++)
        {
            var d = _pool[i];
            if (!d.active) continue;
            d.life -= dt;
            if (d.life <= 0f || cam == null || panel == null)
            {
                d.active = false;
                d.ve.AddToClassList("dmg-number--hidden");
                continue;
            }

            d.worldVel += Vector3.down * 15f * dt;
            d.worldPos += d.worldVel * dt;

            Vector3 sp = cam.WorldToScreenPoint(d.worldPos);
            if (sp.z < 0f)
            {
                d.active = false;
                d.ve.AddToClassList("dmg-number--hidden");
                continue;
            }

            var panelPos = RuntimePanelUtils.ScreenToPanel(panel, sp);
            d.ve.style.left = panelPos.x;
            d.ve.style.top = panelPos.y;

            float dist = Vector3.Distance(cam.transform.position, d.worldPos);
            dist = Mathf.Max(2f, dist);
            float distanceScale = 12f / dist;
            distanceScale = Mathf.Clamp(distanceScale, 0.4f, 2.5f);
            float finalScale = d.scaleMultiplier * distanceScale;

            float alpha = Mathf.Clamp01(d.life / _dmgLife);

            d.ve.style.scale = new Scale(Vector2.one * finalScale);
            d.ve.style.opacity = alpha;
        }

        for (int i = _activeKills.Count - 1; i >= 0; i--)
        {
            var k = _activeKills[i];
            if (Time.unscaledTime - k.spawnTime >= 1.5f)
            {
                _activeKills.RemoveAt(i);
                TransitionToExiting(k);
            }
        }
    }
}
