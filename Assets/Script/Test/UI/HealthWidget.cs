using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class HealthWidget : MonoBehaviour
{
    [Header("Tuning")]
    public Color healthFullColor = new Color(0.85f, 0.78f, 0.45f, 1f);
    public Color healthLowColor = new Color(0.72f, 0.12f, 0.12f, 1f);
    public Color shieldColor = new Color(0.45f, 0.78f, 0.95f, 1f);
    [Range(0f, 1f)] public float lowThreshold = 0.3f;
    public float ghostDamping = 3f;

    private VisualElement _root;
    private VisualElement _healthFill;
    private VisualElement _healthGhost;
    private Label _healthValue;
    private VisualElement _shieldFill;
    private VisualElement _shieldRoot;
    private VisualElement _healthNotchesOverlay;
    private VisualElement _xpNotchesOverlay;
    private float _target;
    private float _shieldTarget;
    private Color _colorCurrent;
    private float _currentGhostPct = 1f;
    private Coroutine _shake;

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) { enabled = false; return; }
        var root = doc.rootVisualElement;
        _root = root.Q<VisualElement>("HealthCluster");
        _healthFill = root.Q<VisualElement>("HealthFill");
        _healthGhost = root.Q<VisualElement>("HealthGhost");
        _healthValue = root.Q<Label>("HealthValue");
        _shieldFill = root.Q<VisualElement>("ShieldFill");
        _shieldRoot = root.Q<VisualElement>("ShieldRoot");
        _healthNotchesOverlay = root.Q<VisualElement>("HealthNotchesOverlay");
        _xpNotchesOverlay = root.Q<VisualElement>("XpNotchesOverlay");

        if (_root == null) { enabled = false; return; }
        _root.generateVisualContent += OnGenerateHealthBackground;
        if (_healthNotchesOverlay != null) _healthNotchesOverlay.generateVisualContent += OnGenerateTrackNotches;
        if (_xpNotchesOverlay != null) _xpNotchesOverlay.generateVisualContent += OnGenerateTrackNotches;
    }

    private void OnEnable() { StartCoroutine(Bind()); }

    private void OnDisable()
    {
        var a = CowsinsHUDAdapter.Instance;
        if (a != null) { a.OnHealthChanged -= OnHealth; a.OnShieldChanged -= OnShield; }
        if (_root != null) _root.generateVisualContent -= OnGenerateHealthBackground;
        if (_healthNotchesOverlay != null) _healthNotchesOverlay.generateVisualContent -= OnGenerateTrackNotches;
        if (_xpNotchesOverlay != null) _xpNotchesOverlay.generateVisualContent -= OnGenerateTrackNotches;
        StopAllCoroutines();
    }

    private IEnumerator Bind()
    {
        var th = UITheme.Active;
        if (th != null) { healthFullColor = th.healthFull; healthLowColor = th.healthLow; shieldColor = th.shield; }
        yield return new WaitUntil(() => CowsinsHUDAdapter.Instance != null);
        var a = CowsinsHUDAdapter.Instance;
        a.OnHealthChanged += OnHealth;
        a.OnShieldChanged += OnShield;
        OnHealth(a.Health, a.MaxHealth, false);
        OnShield(a.Shield, a.MaxShield);
        _colorCurrent = Color.Lerp(healthLowColor, healthFullColor, Mathf.InverseLerp(lowThreshold, 1f, _target));
    }

    private float _lastHp;
    private void OnHealth(float hp, float max, bool damaged)
    {
        _target = max > 0f ? Mathf.Clamp01(hp / max) : 0f;
        if (_healthValue != null) _healthValue.text = Mathf.CeilToInt(Mathf.Max(0f, hp)).ToString();
        Color tgt = Color.Lerp(healthLowColor, healthFullColor, Mathf.InverseLerp(lowThreshold, 1f, _target));
        _colorCurrent = tgt;
        _healthFill.style.width = Length.Percent(_target * 100f);
        _healthFill.style.backgroundColor = _colorCurrent;

        if (_root != null)
        {
            _root.EnableInClassList("low", _target <= lowThreshold);
            _root.MarkDirtyRepaint();
        }

        if (hp > _lastHp)
            _healthGhost.style.width = Length.Percent(_target * 100f);
        _lastHp = hp;

        if (damaged && _root != null)
        {
            if (_shake != null) StopCoroutine(_shake);
            _shake = StartCoroutine(Shake());
        }
    }

    private void OnShield(float sh, float max)
    {
        bool has = max > 0f;
        if (_shieldRoot != null) _shieldRoot.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
        _shieldTarget = has ? Mathf.Clamp01(sh / max) : 0f;
        _shieldFill.style.backgroundColor = shieldColor;
        _shieldFill.style.width = Length.Percent(_shieldTarget * 100f);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (_healthGhost != null)
        {
            if (_currentGhostPct < _target) _currentGhostPct = _target;
            else _currentGhostPct = Mathf.Lerp(_currentGhostPct, _target, 1f - Mathf.Exp(-ghostDamping * dt));
            _healthGhost.style.width = Length.Percent(_currentGhostPct * 100f);
        }
        if (_target <= lowThreshold && _root != null)
        {
            _root.MarkDirtyRepaint();
        }
    }

    private IEnumerator Shake()
    {
        float t = 0f, dur = 0.25f, mag = 7f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float damp = 1f - (t / dur);
            float x = Random.Range(-1f, 1f) * mag * damp;
            float y = Random.Range(-1f, 1f) * mag * damp;
            _root.style.translate = new Translate(x, y);
            yield return null;
        }
        _root.style.translate = new Translate(0f, 0f);
    }

    private void OnGenerateHealthBackground(MeshGenerationContext mgc)
    {
        var targetElement = mgc.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;
        float chamferSize = 12f;
        bool isLow = _target <= lowThreshold;

        // 1. Draw solid dark blue-gray translucent background shape (0.85 alpha, reddish if low HP)
        Color fillCol = isLow ? new Color(60f / 255f, 15f / 255f, 15f / 255f, 0.85f)
                              : new Color(9f / 255f, 13f / 255f, 19f / 255f, 0.85f);
        painter.fillColor = fillCol;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Fill();

        // 2. Draw gold/black diagonal hazard warning stripes at top-right edge
        float badgeW = 40f;
        float badgeH = 4f;
        float startX = rect.width - badgeW - 16f;
        float startY = 2f;
        painter.lineWidth = 1.0f;
        for (float offset = 0; offset < badgeW; offset += 5f)
        {
            painter.strokeColor = isLow ? new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.9f)
                                        : new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.8f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset, startY));
            painter.LineTo(new Vector2(startX + offset - 3f, startY + badgeH));
            painter.Stroke();

            painter.strokeColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.9f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + offset + 2f, startY));
            painter.LineTo(new Vector2(startX + offset - 1f, startY + badgeH));
            painter.Stroke();
        }



        // 4. Outer border (red pulsing if low HP, faint gold if normal)
        float pulse = 0.35f + Mathf.PingPong(Time.realtimeSinceStartup * 2.0f, 0.45f);
        Color strokeCol = isLow ? new Color(229f / 255f, 72f / 255f, 60f / 255f, pulse)
                                : new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.25f);
        painter.strokeColor = strokeCol;
        painter.lineWidth = 1.2f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Stroke();

        // 5. Inner offset border (3px inset) with enhanced 1.5px thickness and 0.45 alpha
        float d = 3f;
        if (rect.width > d * 2 && rect.height > d * 2)
        {
            Color innerCol = isLow ? new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.5f)
                                   : new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.45f);
            painter.strokeColor = innerCol;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chamferSize, d));
            painter.LineTo(new Vector2(rect.width - d, d));
            painter.LineTo(new Vector2(rect.width - d, rect.height - chamferSize));
            painter.LineTo(new Vector2(rect.width - chamferSize, rect.height - d));
            painter.LineTo(new Vector2(d, rect.height - d));
            painter.LineTo(new Vector2(d, chamferSize));
            painter.ClosePath();
            painter.Stroke();
        }

        // 6. L-shaped corner metal brackets (solid metallic gold/steel contrast plates)
        Color bracketColor = isLow ? new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.95f)
                                   : new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.9f);
        painter.strokeColor = bracketColor;
        painter.lineWidth = 3.5f;

        // Top-Left bracket
        painter.BeginPath();
        painter.MoveTo(new Vector2(0, chamferSize + 6f));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.LineTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(chamferSize + 6f, 0));
        painter.Stroke();

        // Top-Right bracket
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width - chamferSize - 6f, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, 6f));
        painter.Stroke();

        // Bottom-Right bracket
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width, rect.height - chamferSize - 6f));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(rect.width - chamferSize - 6f, rect.height));
        painter.Stroke();

        // Bottom-Left bracket
        painter.BeginPath();
        painter.MoveTo(new Vector2(6f, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, rect.height - 6f));
        painter.Stroke();

        // 7. Draw 4 3D metallic gold corner rivets (screws)
        System.Action<Vector2> drawRivet = center =>
        {
            painter.fillColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.6f);
            painter.BeginPath();
            painter.Arc(center + new Vector2(0.4f, 0.4f), 2.2f, 0f, 360f);
            painter.Fill();

            painter.fillColor = new Color(175f / 255f, 150f / 255f, 90f / 255f, 1.0f);
            painter.BeginPath();
            painter.Arc(center, 1.8f, 0f, 360f);
            painter.Fill();

            painter.fillColor = Color.white;
            painter.BeginPath();
            painter.Arc(center - new Vector2(0.4f, 0.4f), 0.4f, 0f, 360f);
            painter.Fill();
        };

        float rOffset = 7f;
        drawRivet(new Vector2(rOffset + 2f, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rect.height - rOffset));
        drawRivet(new Vector2(rOffset + 2f, rect.height - rOffset));
    }

    private void OnGenerateTrackNotches(MeshGenerationContext mgc)
    {
        var targetElement = mgc.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = mgc.painter2D;

        for (int i = 1; i < 10; i++)
        {
            float x = rect.width * i * 0.1f;

            // 1. Dark notch separator line (1.5px thick)
            painter.strokeColor = new Color(10f / 255f, 14f / 255f, 20f / 255f, 0.95f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, 0));
            painter.LineTo(new Vector2(x, rect.height));
            painter.Stroke();

            // 2. Subtle bright highlight edge (1.0px thick)
            painter.strokeColor = new Color(255f / 255f, 255f / 255f, 255f / 255f, 0.35f);
            painter.lineWidth = 1.0f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x + 1f, 0));
            painter.LineTo(new Vector2(x + 1f, rect.height));
            painter.Stroke();
        }
    }
}
