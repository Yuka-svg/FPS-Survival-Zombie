using UnityEngine;
using UnityEngine.UIElements;

public class ReloadIndicatorWidget : MonoBehaviour
{
    public float fallbackTime = 1.5f;
    public float fadeSpeed = 10f;

    private UIDocument _doc;
    private VisualElement _root;
    private VisualElement _ring;
    private Label _percentLabel;
    private Label _textLabel;
    private bool _isReloading;
    private float _fillAmount;
    private float _elapsed;
    private float _burstTime;

    private static readonly Vector2 Center = new Vector2(48f, 48f);
    private const float Radius = 38f;
    private const float StrokeWidth = 5.5f;
    private static readonly Color TrackColor = new Color(14f / 255f, 18f / 255f, 24f / 255f, 0.65f);
    private static readonly Color GoldColor = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.95f);
    private static readonly Color LeadGlowColor = new Color(255f / 255f, 245f / 255f, 200f / 255f, 1f);

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null) return;
        _root = _doc.rootVisualElement.Q("ReloadIndicator");
        _ring = _root?.Q("ReloadRing");
        _percentLabel = _root?.Q<Label>("ReloadPercent");
        _textLabel = _root?.Q<Label>("ReloadText");
        if (_root == null || _ring == null) return;

        _root.style.opacity = 0f;
        _root.style.scale = new Scale(Vector3.one);
        _fillAmount = 0f;
        _isReloading = false;

        _ring.generateVisualContent += OnGenerateRing;

        var a = CowsinsHUDAdapter.Instance;
        if (a != null)
        {
            a.OnReloadChanged += OnReload;
            if (a.IsReloading) OnReload(true);
        }
    }

    private void OnDisable()
    {
        var a = CowsinsHUDAdapter.Instance;
        if (a != null) a.OnReloadChanged -= OnReload;
        if (_ring != null) _ring.generateVisualContent -= OnGenerateRing;
    }

    private void OnReload(bool active)
    {
        if (active)
        {
            _isReloading = true;
            _elapsed = 0f;
            _fillAmount = 0f;
            _burstTime = 0f;
            if (_root != null) _root.style.scale = new Scale(Vector3.one);
        }
        else
        {
            _isReloading = false;
            _fillAmount = 1f;
        }
    }

    private void Update()
    {
        if (_root == null) return;

        float dt = Time.unscaledDeltaTime;

        if (_isReloading)
        {
            var a = CowsinsHUDAdapter.Instance;
            float dur = (a != null && a.ReloadTime > 0.01f) ? a.ReloadTime : fallbackTime;
            _elapsed += dt;
            _fillAmount = Mathf.Clamp01(_elapsed / dur);
            _root.style.opacity = Mathf.MoveTowards(_root.style.opacity.value, 1f, fadeSpeed * dt);

            if (_percentLabel != null)
                _percentLabel.text = $"{Mathf.FloorToInt(_fillAmount * 100f)}%";

            _ring?.MarkDirtyRepaint();

            if (_fillAmount >= 1f)
            {
                _isReloading = false;
                _burstTime = 0.2f;
            }
        }

        if (!_isReloading)
        {
            if (_burstTime > 0f)
            {
                _burstTime -= dt;
                float burstScale = 1.0f + (_burstTime / 0.2f) * 0.22f;
                _root.style.scale = new Scale(new Vector3(burstScale, burstScale, 1f));
                _root.style.opacity = 1f;
            }
            else
            {
                _root.style.scale = new Scale(Vector3.one);
                float current = _root.style.opacity.value;
                if (current > 0.001f)
                {
                    _root.style.opacity = Mathf.MoveTowards(current, 0f, fadeSpeed * dt);
                }
                else
                {
                    _root.style.opacity = 0f;
                    if (_fillAmount > 0f)
                    {
                        _fillAmount = 0f;
                        _ring?.MarkDirtyRepaint();
                    }
                }
            }
        }
    }

    private void OnGenerateRing(MeshGenerationContext ctx)
    {
        var painter = ctx.painter2D;

        // 1. Dark translucent background track ring
        painter.BeginPath();
        painter.lineWidth = StrokeWidth;
        painter.strokeColor = TrackColor;
        painter.Arc(Center, Radius, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
        painter.Stroke();

        // 2. Corner notch ticks (at 0°, 90°, 180°, 270°)
        painter.lineWidth = 2.0f;
        painter.strokeColor = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.4f);
        float tickLen = 4f;
        for (int angleDeg = 0; angleDeg < 360; angleDeg += 90)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 p1 = Center + dir * (Radius - tickLen);
            Vector2 p2 = Center + dir * (Radius + tickLen);
            painter.BeginPath();
            painter.MoveTo(p1);
            painter.LineTo(p2);
            painter.Stroke();
        }

        // 3. Gold progress arc sweep
        if (_fillAmount > 0.001f)
        {
            float startDeg = 90f;
            float endDeg = startDeg - 360f * _fillAmount;

            painter.BeginPath();
            painter.lineWidth = StrokeWidth;
            painter.strokeColor = GoldColor;
            painter.lineCap = LineCap.Round;
            painter.Arc(Center, Radius, Angle.Degrees(startDeg), Angle.Degrees(endDeg), ArcDirection.Clockwise);
            painter.Stroke();

            // 4. Glowing lead head dot
            float headRad = (90f - 360f * _fillAmount) * Mathf.Deg2Rad;
            Vector2 headPos = Center + new Vector2(Mathf.Cos(headRad), -Mathf.Sin(headRad)) * Radius;

            painter.fillColor = LeadGlowColor;
            painter.BeginPath();
            painter.Arc(headPos, StrokeWidth * 0.75f, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
        }
    }
}
