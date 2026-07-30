using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class CompassWidget : MonoBehaviour
{
    private Transform _cam;
    private VisualElement _viewport;
    private VisualElement _strip;
    private VisualElement _blipLayer;
    private Label _heading;
    private int _lastAngle = -999;

    private readonly List<VisualElement> _blips = new List<VisualElement>();
    private readonly List<Label> _dirLabels = new List<Label>();
    private readonly string[] _dirNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
    private readonly float[] _dirAngles = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        var root = doc.rootVisualElement;

        _viewport = root.Q<VisualElement>("CompassViewport");
        _strip = root.Q<VisualElement>("CompassStrip");
        _blipLayer = root.Q<VisualElement>("CompassBlips");
        _heading = root.Q<Label>("CompassHeading");

        // Clear background image from CompassStrip to remove compas.png (and its dots)
        if (_strip != null)
        {
            _strip.style.backgroundImage = null;
        }

        if (_viewport != null)
        {
            _viewport.generateVisualContent += OnGenerateCompassBackground;
            InitializeDirections();
        }
    }

    private void OnDisable()
    {
        if (_viewport != null)
        {
            _viewport.generateVisualContent -= OnGenerateCompassBackground;
        }
        
        foreach (var lbl in _dirLabels)
        {
            lbl.RemoveFromHierarchy();
        }
        _dirLabels.Clear();
    }

    private void EnsureCam()
    {
        if (_cam != null) return;
        if (Camera.main != null) _cam = Camera.main.transform;
    }

    private void Update()
    {
        EnsureCam();
        if (_cam == null) return;

        float yaw = _cam.eulerAngles.y;

        int ai = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f));
        if (ai != _lastAngle)
        {
            _lastAngle = ai;
            if (_heading != null)
            {
                _heading.text = $"{ai}° {GetCardinal(ai)}";
            }
        }

        UpdateDirections(yaw);
        UpdateBlips();
    }

    private string GetCardinal(int angle)
    {
        float norm = Mathf.Repeat(angle, 360f);
        if (norm >= 337.5f || norm < 22.5f) return "N";
        if (norm >= 22.5f && norm < 67.5f) return "NE";
        if (norm >= 67.5f && norm < 112.5f) return "E";
        if (norm >= 112.5f && norm < 157.5f) return "SE";
        if (norm >= 157.5f && norm < 202.5f) return "S";
        if (norm >= 202.5f && norm < 247.5f) return "SW";
        if (norm >= 247.5f && norm < 292.5f) return "W";
        return "NW";
    }

    private void InitializeDirections()
    {
        if (_viewport == null) return;

        foreach (var lbl in _dirLabels)
        {
            lbl.RemoveFromHierarchy();
        }
        _dirLabels.Clear();

        FontDefinition fontDef = default;
        if (_heading != null)
        {
            fontDef = _heading.resolvedStyle.unityFontDefinition;
        }

        for (int i = 0; i < _dirNames.Length; i++)
        {
            var label = new Label(_dirNames[i]);
            label.style.position = Position.Absolute;
            
            bool isCardinal = _dirNames[i].Length == 1;
            label.style.fontSize = isCardinal ? 12 : 9;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            
            // Cardinal directions are gold; intercardinal are semi-transparent gray
            label.style.color = isCardinal 
                ? new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.9f) 
                : new Color(210f / 255f, 215f / 255f, 225f / 255f, 0.45f);
                
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.top = isCardinal ? 14f : 16f; // Positioned in lower half of 32px height
            
            if (fontDef.font != null || fontDef.fontAsset != null)
            {
                label.style.unityFontDefinition = fontDef;
            }
            
            _viewport.Add(label);
            _dirLabels.Add(label);
        }
    }

    private void UpdateDirections(float yaw)
    {
        float viewportWidth = _viewport != null ? _viewport.resolvedStyle.width : 360f;
        if (viewportWidth <= 0f) viewportWidth = 360f;

        for (int i = 0; i < _dirLabels.Count; i++)
        {
            var label = _dirLabels[i];
            
            // Attempt to resolve font definition dynamically if not set
            if (label.style.unityFontDefinition.value.font == null && label.style.unityFontDefinition.value.fontAsset == null && _heading != null)
            {
                var fontDef = _heading.resolvedStyle.unityFontDefinition;
                if (fontDef.font != null || fontDef.fontAsset != null)
                {
                    label.style.unityFontDefinition = fontDef;
                }
            }

            float angleDiff = Mathf.DeltaAngle(yaw, _dirAngles[i]);
            
            // Render only if within visible field of view (+/- 90 degrees)
            if (Mathf.Abs(angleDiff) > 90f)
            {
                label.style.display = DisplayStyle.None;
            }
            else
            {
                label.style.display = DisplayStyle.Flex;
                float labelWidth = _dirNames[i].Length == 1 ? 12f : 24f;
                float x = (viewportWidth / 2f) + angleDiff - (labelWidth / 2f);
                label.style.left = x;
                label.style.width = labelWidth;
                
                // Edge fade out effect
                float edgeFade = 1f - Mathf.Clamp01(Mathf.Abs(angleDiff) / 90f);
                label.style.opacity = edgeFade;
            }
        }
    }

    private void OnGenerateCompassBackground(MeshGenerationContext ctx)
    {
        var targetElement = ctx.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = ctx.painter2D;
        float chamferSize = 8f;

        // 1. Draw solid dark blue-gray translucent background shape (0.85 alpha)
        Color fillCol = new Color(9f / 255f, 13f / 255f, 19f / 255f, 0.85f);
        painter.fillColor = fillCol;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width - chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, chamferSize));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height - chamferSize));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Fill();

        // 2. Draw outer border (gold)
        Color strokeCol = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.3f);
        painter.strokeColor = strokeCol;
        painter.lineWidth = 1.2f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(rect.width - chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, chamferSize));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height - chamferSize));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.ClosePath();
        painter.Stroke();

        // 3. Draw 4 L-shaped metal brackets
        Color bracketColor = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.85f);
        painter.strokeColor = bracketColor;
        painter.lineWidth = 2.5f;

        // Top-Left
        painter.BeginPath();
        painter.MoveTo(new Vector2(0, chamferSize + 4f));
        painter.LineTo(new Vector2(0, chamferSize));
        painter.LineTo(new Vector2(chamferSize, 0));
        painter.LineTo(new Vector2(chamferSize + 4f, 0));
        painter.Stroke();

        // Top-Right
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width - chamferSize - 4f, 0));
        painter.LineTo(new Vector2(rect.width - chamferSize, 0));
        painter.LineTo(new Vector2(rect.width, chamferSize));
        painter.LineTo(new Vector2(rect.width, chamferSize + 4f));
        painter.Stroke();

        // Bottom-Right
        painter.BeginPath();
        painter.MoveTo(new Vector2(rect.width, rect.height - chamferSize - 4f));
        painter.LineTo(new Vector2(rect.width, rect.height - chamferSize));
        painter.LineTo(new Vector2(rect.width - chamferSize, rect.height));
        painter.LineTo(new Vector2(rect.width - chamferSize - 4f, rect.height));
        painter.Stroke();

        // Bottom-Left
        painter.BeginPath();
        painter.MoveTo(new Vector2(chamferSize + 4f, rect.height));
        painter.LineTo(new Vector2(chamferSize, rect.height));
        painter.LineTo(new Vector2(0, rect.height - chamferSize));
        painter.LineTo(new Vector2(0, rect.height - chamferSize - 4f));
        painter.Stroke();

        // 4. Center Pointer (Downward golden triangle)
        painter.fillColor = new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.95f);
        painter.BeginPath();
        float midX = rect.width / 2f;
        painter.MoveTo(new Vector2(midX - 5f, 0));
        painter.LineTo(new Vector2(midX + 5f, 0));
        painter.LineTo(new Vector2(midX, 6f));
        painter.ClosePath();
        painter.Fill();

        // 5. Draw 2 3D metallic rivets
        System.Action<Vector2> drawRivet = center =>
        {
            painter.fillColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.6f);
            painter.BeginPath();
            painter.Arc(center + new Vector2(0.3f, 0.3f), 1.5f, 0f, 360f);
            painter.Fill();

            painter.fillColor = new Color(175f / 255f, 150f / 255f, 90f / 255f, 1.0f);
            painter.BeginPath();
            painter.Arc(center, 1.2f, 0f, 360f);
            painter.Fill();
        };

        float rOffset = 5f;
        drawRivet(new Vector2(rOffset, rect.height / 2f));
        drawRivet(new Vector2(rect.width - rOffset, rect.height / 2f));
    }

    private void UpdateBlips()
    {
        var markers = CompassMarker.Active;
        EnsureBlips(markers.Count);
        float viewportWidth = _viewport.resolvedStyle.width;
        Vector2 playerPos = new Vector2(_cam.position.x, _cam.position.z);
        Vector2 fwd = new Vector2(_cam.forward.x, _cam.forward.z);
        for (int i = 0; i < _blips.Count; i++)
        {
            bool used = i < markers.Count && markers[i] != null;
            _blips[i].style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
            if (!used) continue;
            var m = markers[i];
            
            // Calculate distance and size/opacity
            float dist = Vector2.Distance(playerPos, m.PlanarPosition);
            float minDist = 10f;
            float maxDist = 120f;
            
            float minSize = 4f;   // Very small when far
            float maxSize = 14f;  // Large when near
            
            float minOpacity = 0.25f; // Faded when far
            float maxOpacity = 0.95f; // Bright when near
            
            float t = Mathf.Clamp01((dist - minDist) / (maxDist - minDist));
            float size = Mathf.Lerp(maxSize, minSize, t);
            float opacity = Mathf.Lerp(maxOpacity, minOpacity, t);

            float angle = Vector2.SignedAngle(m.PlanarPosition - playerPos, fwd);
            
            // Position blips dynamically near the top (top: 4px) to separate vertically from letters
            _blips[i].style.width = size;
            _blips[i].style.height = size;
            _blips[i].style.left = (angle / 360f * viewportWidth) + viewportWidth / 2f - size / 2f;
            _blips[i].style.top = 4f;
            _blips[i].style.opacity = opacity;

            if (m.icon != null)
            {
                _blips[i].style.backgroundImage = new StyleBackground(m.icon);
                _blips[i].style.unityBackgroundImageTintColor = new Color(255f / 255f, 42f / 255f, 42f / 255f, 0.95f);
            }
        }
    }

    private void EnsureBlips(int count)
    {
        while (_blips.Count < count)
        {
            var blip = new VisualElement();
            blip.AddToClassList("compass-blip");
            blip.style.position = Position.Absolute;
            blip.style.width = 10;
            blip.style.height = 10;
            blip.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            blip.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
            blip.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            blip.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            _blipLayer.Add(blip);
            _blips.Add(blip);
        }
    }
}
