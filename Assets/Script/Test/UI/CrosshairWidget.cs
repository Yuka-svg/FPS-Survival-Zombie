using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class CrosshairWidget : MonoBehaviour
{
    [Header("Geometry (reference px @1920x1080)")]
    public float lineLength = 16f;
    public float lineThickness = 3.2f;
    public float enemyThickness = 5.5f;

    [Header("Spread per state")]
    public float defaultSpread = 20f;
    public float walkSpread = 45f;
    public float runSpread = 100f;
    public float crouchSpread = 20f;
    public float jumpSpread = 160f;
    public float resizeSpeed = 15f;

    [Header("Aim Transition")]
    [Range(0.10f, 0.50f), Tooltip("Crosshair reticle fade duration in seconds (aiming/unaiming). Default 0.20s = exactly 200ms visible transition.")]
    public float aimFadeDuration = 0.20f;

    [Header("Behavior")]
    public bool showOuterRadialTicks = false;
    public bool removeCrosshairOnAiming = true;
    public bool hideCrosshairOnInspecting = true;

    [Header("Colors")]
    public Color defaultColor = new Color(0f, 255f / 255f, 204f / 255f, 0.95f);
    public Color enemySpottedColor = new Color(255f / 255f, 42f / 255f, 42f / 255f, 0.95f);
    public Color hitmarkerColor = new Color(255f / 255f, 255f / 255f, 255f / 255f, 0.95f);
    public Color headshotHitmarkerColor = new Color(255f / 255f, 35f / 255f, 45f / 255f, 0.95f);
    public Color shadowColor = new Color(6f / 255f, 12f / 255f, 18f / 255f, 0.9f);

    private VisualElement _container;
    private CowsinsHUDAdapter _adapter;
    private float _spread;
    private float _thickness;

    // Reticle transition & repaint state
    private float _reticleOpacity = 1f;
    private float _lastRepaintedOpacity = -1f;
    private float _lastRepaintedHitmarkerAlpha = -1f;
    private float _lastRepaintedSpread = -1f;
    private float _lastRepaintedThickness = -1f;
    private bool _lastEnemySpotted = false;

    // Hitmarker feedback state
    private float _hitmarkerAlpha;
    private float _hitmarkerScale = 1f;
    private bool _isHeadshot;

    private bool _initialized;

    private void Awake()
    {
        _spread = defaultSpread;
        _thickness = lineThickness;
    }

    private void OnEnable()
    {
        if (!_initialized)
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null) { enabled = false; return; }
            _container = doc.rootVisualElement.Q("Crosshair");
            if (_container == null) { enabled = false; return; }

            // Clear any old child VisualElements (bars) if present in UXML to ensure 0 DOM child nodes
            _container.Clear();

            _container.generateVisualContent += OnGenerateCrosshairOverlay;
            _initialized = true;
        }

        if (_container != null)
        {
            _container.style.opacity = StyleKeyword.Null;
        }
        _reticleOpacity = 1f;
        _lastRepaintedOpacity = -1f;

        StartCoroutine(Bind());
    }

    private IEnumerator Bind()
    {
        float timeout = 12f;
        while (CowsinsHUDAdapter.Instance == null && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        _adapter = CowsinsHUDAdapter.Instance;
        if (_adapter == null) yield break;
        _adapter.OnFired += HandleFired;
        _adapter.OnEnemyHit += HandleEnemyHit;
    }

    private void OnDisable()
    {
        if (_adapter != null)
        {
            _adapter.OnFired -= HandleFired;
            _adapter.OnEnemyHit -= HandleEnemyHit;
        }
        if (_container != null) _container.generateVisualContent -= OnGenerateCrosshairOverlay;
        _initialized = false;
        StopAllCoroutines();
    }

    private void HandleFired()
    {
        if (_adapter == null) return;
        float kick = _adapter.WeaponCrosshairResize * 10f;
        _spread = Mathf.Lerp(_spread, kick, 0.25f);
    }

    private void HandleEnemyHit(bool isHeadshot, float damage)
    {
        _isHeadshot = isHeadshot;
        _hitmarkerAlpha = 1f;
        _hitmarkerScale = isHeadshot ? 1.45f : 1.2f;
    }

    private void Update()
    {
        if (_container == null) return;
        float dt = Time.unscaledDeltaTime;
        var a = _adapter;

        // 1. Independent Hitmarker Decay animation
        bool hitmarkerActive = _hitmarkerAlpha > 0.0001f;
        if (hitmarkerActive)
        {
            float decayRate = _isHeadshot ? 4.5f : 6f;
            _hitmarkerAlpha = Mathf.MoveTowards(_hitmarkerAlpha, 0f, decayRate * dt);
            _hitmarkerScale = Mathf.MoveTowards(_hitmarkerScale, 1f, 8f * dt);
        }

        // 2. Spread & Thickness Calculation
        float target = defaultSpread;
        if (a != null)
        {
            if (a.MoveGrounded)
            {
                float cs = a.MoveCurrentSpeed, run = a.MoveRunSpeed, walk = a.MoveWalkSpeed, crouch = a.MoveCrouchSpeed;
                if (run > 0f && Mathf.Approximately(cs, run) && !a.MoveIsIdle) target = runSpread;
                else if (walk > 0f && Mathf.Approximately(cs, walk)) target = a.MoveIsIdle ? defaultSpread : walkSpread;
                else if (crouch > 0f && Mathf.Approximately(cs, crouch)) target = crouchSpread;
                else target = defaultSpread;
            }
            else target = jumpSpread;
        }
        _spread = Mathf.Lerp(_spread, target, resizeSpeed * dt);
        _thickness = Mathf.Lerp(_thickness, a != null && a.EnemySpotted ? enemyThickness : lineThickness, resizeSpeed * dt);

        // 3. Gameplay Aiming State & Linear 0.20s Opacity Lerp
        bool hidden = a != null && (a.IsDead || (a.IsAiming && removeCrosshairOnAiming) || (hideCrosshairOnInspecting && a.IsInspecting));
        float targetOpacity = hidden ? 0f : 1f;
        float speed = 1.0f / Mathf.Max(0.01f, aimFadeDuration);
        _reticleOpacity = Mathf.MoveTowards(_reticleOpacity, targetOpacity, speed * dt);

        // 4. Optimized Canvas Dirty Repaint Check
        bool reticleVisible = _reticleOpacity > 0.0001f;
        bool opacityChanging = Mathf.Abs(_reticleOpacity - _lastRepaintedOpacity) > 0.0001f;
        bool hitmarkerChanging = Mathf.Abs(_hitmarkerAlpha - _lastRepaintedHitmarkerAlpha) > 0.0001f;
        bool spreadChanging = reticleVisible && Mathf.Abs(_spread - _lastRepaintedSpread) > 0.1f;
        bool thicknessChanging = reticleVisible && Mathf.Abs(_thickness - _lastRepaintedThickness) > 0.01f;
        bool currentEnemySpotted = a != null && a.EnemySpotted;
        bool enemySpottedChanging = currentEnemySpotted != _lastEnemySpotted;

        if (opacityChanging || hitmarkerActive || hitmarkerChanging || spreadChanging || thicknessChanging || enemySpottedChanging)
        {
            _lastRepaintedOpacity = _reticleOpacity;
            _lastRepaintedHitmarkerAlpha = _hitmarkerAlpha;
            _lastRepaintedSpread = _spread;
            _lastRepaintedThickness = _thickness;
            _lastEnemySpotted = currentEnemySpotted;
            _container.MarkDirtyRepaint();
        }
    }

    private void OnGenerateCrosshairOverlay(MeshGenerationContext ctx)
    {
        var painter = ctx.painter2D;
        float width = _container.resolvedStyle.width;
        float height = _container.resolvedStyle.height;
        if (width <= 0 || height <= 0) return;

        Vector2 center = new Vector2(width / 2f, height / 2f);
        bool spotted = _adapter != null && _adapter.EnemySpotted;

        float reticleAlpha = _reticleOpacity;
        Color basePrimaryColor = spotted ? enemySpottedColor : defaultColor;
        Color primaryColor = new Color(basePrimaryColor.r, basePrimaryColor.g, basePrimaryColor.b, basePrimaryColor.a * reticleAlpha);
        Color reticleShadowColor = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * reticleAlpha);

        float spread = _spread;
        float halfGap = spread / 2f;
        float L = lineLength;
        float t = _thickness;

        bool noWeapon = _adapter == null || !_adapter.HasWeapon;

        // Helper Dual-Pass Stroke Line
        void DrawLine(Vector2 p1, Vector2 p2, float thickness)
        {
            // Shadow pass
            painter.strokeColor = reticleShadowColor;
            painter.lineWidth = thickness + 2.4f;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(p1);
            painter.LineTo(p2);
            painter.Stroke();

            // Primary Neon pass
            painter.strokeColor = primaryColor;
            painter.lineWidth = thickness;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(p1);
            painter.LineTo(p2);
            painter.Stroke();
        }

        // Helper Dual-Pass Continuous L-Bracket Path (triet tieu 100% corner overlap blobs)
        void DrawLBracket(Vector2 pStart, Vector2 pCorner, Vector2 pEnd, float thickness)
        {
            // Shadow pass
            painter.strokeColor = reticleShadowColor;
            painter.lineWidth = thickness + 2.4f;
            painter.lineCap = LineCap.Butt;
            painter.lineJoin = LineJoin.Miter;
            painter.BeginPath();
            painter.MoveTo(pStart);
            painter.LineTo(pCorner);
            painter.LineTo(pEnd);
            painter.Stroke();

            // Primary Neon pass
            painter.strokeColor = primaryColor;
            painter.lineWidth = thickness;
            painter.lineCap = LineCap.Butt;
            painter.lineJoin = LineJoin.Miter;
            painter.BeginPath();
            painter.MoveTo(pStart);
            painter.LineTo(pCorner);
            painter.LineTo(pEnd);
            painter.Stroke();
        }

        // 1. Main 4 Crosshair Bars
        if (noWeapon || _adapter.CHTop)   DrawLine(center + new Vector2(0f, -halfGap), center + new Vector2(0f, -halfGap - L), t);
        if (noWeapon || _adapter.CHDown)  DrawLine(center + new Vector2(0f, halfGap),  center + new Vector2(0f, halfGap + L),  t);
        if (noWeapon || _adapter.CHLeft)  DrawLine(center + new Vector2(-halfGap, 0f), center + new Vector2(-halfGap - L, 0f), t);
        if (noWeapon || _adapter.CHRight) DrawLine(center + new Vector2(halfGap, 0f),  center + new Vector2(halfGap + L, 0f),  t);

        // 2. Continuous 4 L-Brackets (Corner Reticles)
        float s = spread;
        float bLen = L * 0.85f;
        if (!noWeapon && _adapter.CHTopLeft)
        {
            Vector2 corner = center + new Vector2(-s, -s);
            DrawLBracket(corner + new Vector2(0f, bLen), corner, corner + new Vector2(bLen, 0f), t);
        }
        if (!noWeapon && _adapter.CHTopRight)
        {
            Vector2 corner = center + new Vector2(s, -s);
            DrawLBracket(corner + new Vector2(0f, bLen), corner, corner + new Vector2(-bLen, 0f), t);
        }
        if (!noWeapon && _adapter.CHBottomLeft)
        {
            Vector2 corner = center + new Vector2(-s, s);
            DrawLBracket(corner + new Vector2(0f, -bLen), corner, corner + new Vector2(bLen, 0f), t);
        }
        if (!noWeapon && _adapter.CHBottomRight)
        {
            Vector2 corner = center + new Vector2(s, s);
            DrawLBracket(corner + new Vector2(0f, -bLen), corner, corner + new Vector2(-bLen, 0f), t);
        }

        // 3. Center Reticle (Dot / Diamond)
        if (noWeapon || _adapter.CHCenter)
        {
            float dSize = 5.5f;
            // Shadow Pass
            painter.fillColor = reticleShadowColor;
            painter.BeginPath();
            painter.MoveTo(center + new Vector2(0, -dSize - 1.5f));
            painter.LineTo(center + new Vector2(dSize + 1.5f, 0));
            painter.LineTo(center + new Vector2(0, dSize + 1.5f));
            painter.LineTo(center + new Vector2(-dSize - 1.5f, 0));
            painter.ClosePath();
            painter.Fill();

            // Primary Neon Pass
            painter.fillColor = primaryColor;
            painter.BeginPath();
            painter.MoveTo(center + new Vector2(0, -dSize));
            painter.LineTo(center + new Vector2(dSize, 0));
            painter.LineTo(center + new Vector2(0, dSize));
            painter.LineTo(center + new Vector2(-dSize, 0));
            painter.ClosePath();
            painter.Fill();
        }

        // 4. Outer Range Finder Radial Ticks (at 45°, 135°, 225°, 315°) - Optional
        if (showOuterRadialTicks)
        {
            float outerRadius = spread + 20f;
            float tickLen = 11f;

            for (int i = 0; i < 4; i++)
            {
                float angleDeg = 45f + i * 90f;
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Vector2 p1 = center + dir * outerRadius;
                Vector2 p2 = center + dir * (outerRadius + tickLen);

                DrawLine(p1, p2, 2.4f);
            }
        }

        // 5. Dynamic Hitmarker (X-mark) when hitting an enemy
        if (_hitmarkerAlpha > 0.005f)
        {
            float hitGap = (_isHeadshot ? 7f : 5f) * _hitmarkerScale;
            float hitLen = (_isHeadshot ? 16f : 11f) * _hitmarkerScale;
            float strokeWidth = _isHeadshot ? 3.6f : 2.6f;

            Color activeHitColor = _isHeadshot ? headshotHitmarkerColor : hitmarkerColor;
            Color curHitColor = new Color(activeHitColor.r, activeHitColor.g, activeHitColor.b, activeHitColor.a * _hitmarkerAlpha);
            Color curHitShadow = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * _hitmarkerAlpha);

            for (int i = 0; i < 4; i++)
            {
                float angleDeg = 45f + i * 90f;
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Vector2 p1 = center + dir * hitGap;
                Vector2 p2 = center + dir * (hitGap + hitLen);

                // Shadow stroke
                painter.strokeColor = curHitShadow;
                painter.lineWidth = strokeWidth + 2.2f;
                painter.BeginPath();
                painter.MoveTo(p1);
                painter.LineTo(p2);
                painter.Stroke();

                // Neon Hitmarker stroke
                painter.strokeColor = curHitColor;
                painter.lineWidth = strokeWidth;
                painter.BeginPath();
                painter.MoveTo(p1);
                painter.LineTo(p2);
                painter.Stroke();
            }
        }
    }
}
