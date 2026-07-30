using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class CombatFeedbackHUD : MonoBehaviour
{
    public static CombatFeedbackHUD Instance;

    public Camera worldCamera;

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

    private class Kill { public VisualElement entry; public Label label; public float life; public bool active; }
    private readonly List<Kill> _kills = new List<Kill>();
    private readonly List<Kill> _killPool = new List<Kill>();
    private float _killLife = 4f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        _root = doc.rootVisualElement.Q("CombatFeedbackHUD");
        if (_root == null) return;
        _hitmarker = _root.Q("Hitmarker");
        _dmgContainer = _root.Q("DamageNumbers");
        _killContainer = _root.Q("Killfeed");

        BuildHitmarkerBars();
        BuildDamagePool();
    }

    private void BuildHitmarkerBars()
    {
        if (_hitmarker.childCount > 0) return;
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
        if (_dmgContainer.childCount > 0) return;
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

    public void ShowHit(Vector3 worldPos, float damage, bool headshot)
    {
        bool crit = _critPending && Time.frameCount == _critFrame;
        _critPending = false;

        _hitTimer = _hitDuration;
        _hitmarker.EnableInClassList("hitmarker--visible", true);
        float scale = crit ? 1.6f : headshot ? 1.4f : 1f;
        _hitmarker.style.scale = new Scale(Vector2.one * scale);
        foreach (var bar in _hitBars)
        {
            bar.EnableInClassList("hitmarker-bar--kill", headshot);
            bar.EnableInClassList("hitmarker-bar--crit", crit);
        }

        var d = GetDmg();
        if (d == null) return;

        // Initialize 3D physics parameters
        d.worldPos = worldPos + Vector3.up * 1.5f; // Start at chest/head level
        
        // Random 3D launch velocity
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float planarSpeed = Random.Range(1.5f, 3.5f); // horizontal spread
        float upwardSpeed = Random.Range(4.5f, 7.0f);  // snappy vertical bounce
        d.worldVel = new Vector3(Mathf.Cos(angle) * planarSpeed, upwardSpeed, Mathf.Sin(angle) * planarSpeed);

        d.scaleMultiplier = (crit || headshot) ? 1.5f : 1.0f;
        d.life = _dmgLife;

        int dmgInt = Mathf.Max(1, Mathf.RoundToInt(damage));
        d.label.text = GetDamageString(dmgInt, crit);
        
        // Apply styling classes to both parent VE and Label
        d.ve.EnableInClassList("dmg-number--kill", headshot);
        d.ve.EnableInClassList("dmg-number--crit", crit);
        d.label.EnableInClassList("dmg-number--kill", headshot);
        d.label.EnableInClassList("dmg-number--crit", crit);

        // Pre-position on the very first frame to avoid any 1-frame (0,0) flickering
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
                    float distanceScale = 12f / dist; // 12m reference distance
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
        Kill k = null;
        for (int i = 0; i < _killPool.Count; i++)
        {
            if (!_killPool[i].active)
            {
                k = _killPool[i];
                break;
            }
        }

        if (k == null)
        {
            var entry = new VisualElement();
            entry.AddToClassList("killfeed-entry");
            entry.usageHints = UsageHints.DynamicTransform;
            var label = new Label();
            label.AddToClassList("killfeed-entry-label");
            entry.Add(label);
            k = new Kill { entry = entry, label = label, active = true };
            _killPool.Add(k);
        }
        else
        {
            k.active = true;
        }

        k.entry.RemoveFromClassList("killfeed-entry--hidden");
        k.entry.RemoveFromHierarchy();
        _killContainer.Insert(0, k.entry);
        k.label.text = "Killed  " + (string.IsNullOrEmpty(name) ? "Zombie" : name);
        k.life = _killLife;
        _kills.Add(k);

        while (_kills.Count > 6)
        {
            var oldest = _kills[0];
            _kills.RemoveAt(0);
            oldest.active = false;
            oldest.entry.AddToClassList("killfeed-entry--hidden");
        }
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
            if (_hitTimer <= 0f) _hitmarker.EnableInClassList("hitmarker--visible", false);
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

            // Apply 3D arcade gravity (15 m/s^2)
            d.worldVel += Vector3.down * 15f * dt;
            d.worldPos += d.worldVel * dt;

            // Project 3D position to 2D Screen and convert to UITK Panel Space
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

            // Calculate scale based on distance to World Camera
            float dist = Vector3.Distance(cam.transform.position, d.worldPos);
            dist = Mathf.Max(2f, dist);
            float distanceScale = 12f / dist; // 12m reference distance
            distanceScale = Mathf.Clamp(distanceScale, 0.4f, 2.5f);
            float finalScale = d.scaleMultiplier * distanceScale;

            // Fade out smoothly over life
            float alpha = Mathf.Clamp01(d.life / _dmgLife);

            d.ve.style.scale = new Scale(Vector2.one * finalScale);
            d.ve.style.opacity = alpha;
        }

        for (int i = _kills.Count - 1; i >= 0; i--)
        {
            var k = _kills[i];
            k.life -= dt;
            if (k.life <= 0f)
            {
                _kills.RemoveAt(i);
                k.active = false;
                k.entry.AddToClassList("killfeed-entry--hidden");
            }
        }
    }
}
