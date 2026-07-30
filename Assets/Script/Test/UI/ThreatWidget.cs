using UnityEngine;
using UnityEngine.UIElements;

public class ThreatWidget : MonoBehaviour
{
    private VisualElement _container;
    private VisualElement _fill;
    private Label _state;
    private Label _warn;
    private IVisualElementScheduledItem _warnSched;

    private Color _calm = new Color(0.31f, 0.878f, 0.541f, 1f);
    private Color _build = new Color(1f, 0.83f, 0.30f, 1f);
    private Color _attack = new Color(0.95f, 0.32f, 0.27f, 1f);
    private Color _recover = new Color(0.45f, 0.78f, 0.95f, 1f);

    private void OnEnable()
    {
        var uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null) return;
        var root = uiDoc.rootVisualElement;
        if (root == null) return;

        _container = root.Q("ThreatWidget");
        if (_container != null)
        {
            _container.generateVisualContent += OnGenerateBackground;
        }

        _fill = root.Q<VisualElement>("ThreatFill");
        _state = root.Q<Label>("ThreatState");
        _warn = root.Q<Label>("ThreatWarn");

        var d = AIDirector.Instance;
        if (d != null)
        {
            d.OnThreatChanged += Refresh;
            Refresh();
        }

        _warnSched = _warn?.schedule.Execute(AnimateWarn).Every(50);
    }

    private void OnDisable()
    {
        _warnSched?.Pause();
        var d = AIDirector.Instance;
        if (d != null) d.OnThreatChanged -= Refresh;
        
        if (_container != null)
        {
            _container.generateVisualContent -= OnGenerateBackground;
        }
        
        _container = null;
        _fill = null;
        _state = null;
        _warn = null;
    }

    private void Update()
    {
        // Animate the pulsing border/LED/warning repaint at all times
        _container?.MarkDirtyRepaint();
    }

    private void Refresh()
    {
        var d = AIDirector.Instance;
        if (d == null || _fill == null || _state == null || _warn == null) return;

        float target = Mathf.Clamp01(d.threatLevel / 100f);
        _fill.style.width = Length.Percent(target * 100f);

        Color c; string label;
        switch (d.currentState)
        {
            case AIDirector.DirectorState.Calm: c = _calm; label = "CALM"; break;
            case AIDirector.DirectorState.BuildUp: c = _build; label = "BUILD-UP"; break;
            case AIDirector.DirectorState.Attack: c = _attack; label = "ATTACK"; break;
            default: c = _recover; label = "RECOVERY"; break;
        }
        _fill.style.backgroundColor = c;
        if (_state.text != label) _state.text = label;
        _state.style.color = c;
        
        // Trigger background redraw immediately
        _container?.MarkDirtyRepaint();
    }

    private void AnimateWarn()
    {
        if (_warn == null || _state == null) return;
        bool flank = AIDirector.Instance != null && AIDirector.Instance.ShouldPunishCamper();
        _warn.style.opacity = flank ? (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 8f)) : 0f;
        
        // Hide state text when flanked to allow center-flashed warning text
        _state.style.opacity = flank ? 0f : 1f;
    }

    private void OnGenerateBackground(MeshGenerationContext mgc)
    {
        var targetElement = mgc.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width < 10f || rect.height < 10f) return;

        var painter = mgc.painter2D;
        float chamferSize = 6f;

        bool flank = AIDirector.Instance != null && AIDirector.Instance.ShouldPunishCamper();

        // 1. Draw solid dark background with 0.85 alpha
        painter.fillColor = new Color(9f / 255f, 13f / 255f, 19f / 255f, 0.85f);
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, 0));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Fill();

        // 2. Draw corner brackets (ôm sát viền vát)
        painter.strokeColor = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.2f);
        painter.lineWidth = 1.0f;
        // Top-Right Bracket
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width - 12f, 0f));
        painter.LineTo(new Vector2(rect.width, 0f));
        painter.LineTo(new Vector2(rect.width, 12f));
        painter.Stroke();
        // Bottom-Left Bracket
        painter.BeginPath();
        painter.MoveTo(new Vector2(12f, rect.height));
        painter.LineTo(new Vector2(0f, rect.height));
        painter.LineTo(new Vector2(0f, rect.height - 12f));
        painter.Stroke();

        // 3. Determine colors and pulsing states
        Color stateCol = _calm;
        Color borderCol = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.25f);
        float pulse = 1f;

        if (AIDirector.Instance != null)
        {
            var state = AIDirector.Instance.currentState;
            switch (state)
            {
                case AIDirector.DirectorState.Calm:
                    stateCol = _calm;
                    break;
                case AIDirector.DirectorState.BuildUp:
                    stateCol = _build;
                    pulse = 0.5f + 0.5f * Mathf.PingPong(Time.realtimeSinceStartup * 2f, 1f);
                    borderCol = new Color(1f, 0.83f, 0.30f, 0.25f + 0.3f * pulse);
                    break;
                case AIDirector.DirectorState.Attack:
                    stateCol = _attack;
                    pulse = 0.5f + 0.5f * Mathf.PingPong(Time.realtimeSinceStartup * 4f, 1f);
                    borderCol = new Color(0.95f, 0.32f, 0.27f, 0.35f + 0.45f * pulse);
                    break;
                default:
                    stateCol = _recover;
                    pulse = 0.5f + 0.5f * Mathf.PingPong(Time.realtimeSinceStartup * 1.5f, 1f);
                    borderCol = new Color(0.45f, 0.78f, 0.95f, 0.2f + 0.2f * pulse);
                    break;
            }
        }

        // Flanking state overrides border to red flash
        if (flank)
        {
            float fPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            borderCol = new Color(0.95f, 0.32f, 0.27f, 0.4f + 0.5f * fPulse);
        }

        // 4. Draw outer border
        painter.strokeColor = borderCol;
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

        // 5. Draw rivets (đinh vít)
        System.Action<Vector2> drawRivet = center =>
        {
            painter.fillColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.6f);
            painter.BeginPath();
            painter.Arc(center + new Vector2(0.4f, 0.4f), 1.8f, 0f, 360f);
            painter.Fill();

            painter.fillColor = new Color(175f / 255f, 150f / 255f, 90f / 255f, 1.0f);
            painter.BeginPath();
            painter.Arc(center, 1.4f, 0f, 360f);
            painter.Fill();
        };
        float rOffset = 5f;
        drawRivet(new Vector2(rOffset + chamferSize, rOffset));
        drawRivet(new Vector2(rect.width - rOffset, rOffset));
        drawRivet(new Vector2(rOffset, rect.height - rOffset));
        drawRivet(new Vector2(rect.width - rOffset - chamferSize, rect.height - rOffset));

        if (flank) return; // Skip LED & Ticks when flanked

        // 6. Draw LED indicator dot (chấm tròn)
        float ledPulse = 1f;
        if (AIDirector.Instance != null && AIDirector.Instance.currentState != AIDirector.DirectorState.Calm)
        {
            ledPulse = 0.4f + 0.6f * Mathf.PingPong(Time.realtimeSinceStartup * (AIDirector.Instance.currentState == AIDirector.DirectorState.Attack ? 4f : 2f), 1f);
        }
        Color ledColor = stateCol;
        ledColor.a = ledPulse;
        painter.fillColor = ledColor;
        painter.BeginPath();
        painter.Arc(new Vector2(18f, rect.height * 0.5f), 3f, 0f, 360f);
        painter.Fill();

        // 7. Draw 10 slanted progress notches (vạch chia nghiêng)
        float threatLevel = AIDirector.Instance != null ? AIDirector.Instance.threatLevel : 0f;
        int activeTicks = Mathf.RoundToInt(Mathf.Clamp01(threatLevel / 100f) * 10f);

        for (int i = 0; i < 10; i++)
        {
            float startX = 120f + i * 10f;
            bool isActive = i < activeTicks;
            
            Color tickCol;
            if (isActive)
            {
                tickCol = stateCol;
            }
            else
            {
                // Inactive tick is very dark faded gold
                tickCol = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.08f);
            }

            painter.fillColor = tickCol;
            painter.BeginPath();
            painter.MoveTo(new Vector2(startX + 2f, 14f));
            painter.LineTo(new Vector2(startX + 8f, 14f));
            painter.LineTo(new Vector2(startX + 6f, 22f));
            painter.LineTo(new Vector2(startX + 0f, 22f));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
