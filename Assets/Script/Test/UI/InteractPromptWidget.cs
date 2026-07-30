using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class InteractPromptWidget : MonoBehaviour
{
    private CowsinsHUDAdapter _adapter;
    private VisualElement _root;
    private VisualElement _panel;
    private Label _label;
    private VisualElement _progressBar;
    private VisualElement _progressFill;
    private float _forbidTimer;

    private static readonly Color PanelColor = new(0.137f, 0.165f, 0.2f, 0.94f);
    private static readonly Color ForbiddenColor = new(0.66f, 0.09f, 0.13f, 0.95f);

    private bool _isForbidden;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        _root = doc.rootVisualElement.Q("InteractPrompt");
        if (_root == null) return;

        Build();
        if (_panel != null)
        {
            _panel.generateVisualContent += OnGeneratePanelBackground;
        }
        StartCoroutine(BindDeferred());
    }

    private void Build()
    {
        if (_root.childCount > 0)
        {
            _panel = _root.Q("interact-panel");
            _label = _root.Q<Label>("interact-label");
            _progressBar = _root.Q("interact-progress");
            _progressFill = _root.Q("interact-progress-fill");
            return;
        }

        _panel = new VisualElement();
        _panel.name = "interact-panel";
        _panel.AddToClassList("interact-panel");
        _root.Add(_panel);

        _label = new Label();
        _label.name = "interact-label";
        _label.AddToClassList("interact-label");
        _panel.Add(_label);

        _progressBar = new VisualElement();
        _progressBar.name = "interact-progress";
        _progressBar.AddToClassList("interact-progress");
        _panel.Add(_progressBar);

        _progressFill = new VisualElement();
        _progressFill.name = "interact-progress-fill";
        _progressFill.AddToClassList("interact-progress-fill");
        _progressFill.usageHints = UsageHints.DynamicTransform;
        _progressBar.Add(_progressFill);
    }

    private IEnumerator BindDeferred()
    {
        while (CowsinsHUDAdapter.Instance == null) yield return null;
        _adapter = CowsinsHUDAdapter.Instance;
        _adapter.OnInteractPrompt += HandlePrompt;
        _adapter.OnInteractForbidden += HandleForbidden;
        _adapter.OnInteractProgress += HandleProgress;
    }

    private void OnDisable()
    {
        if (_adapter != null)
        {
            _adapter.OnInteractPrompt -= HandlePrompt;
            _adapter.OnInteractForbidden -= HandleForbidden;
            _adapter.OnInteractProgress -= HandleProgress;
        }
        if (_panel != null)
        {
            _panel.generateVisualContent -= OnGeneratePanelBackground;
        }
        _adapter = null;
        StopAllCoroutines();
    }

    private bool IsReady()
    {
        return _root != null && _label != null && _panel != null && _progressBar != null && _progressFill != null;
    }

    private void HandlePrompt(bool visible, string text)
    {
        if (!IsReady()) return;
        if (visible)
        {
            _isForbidden = false;
            string formattedText = text;
            if (!formattedText.Contains("[E]") && !formattedText.Contains("E "))
            {
                formattedText = "[E]  " + formattedText.ToUpper();
            }
            _label.text = formattedText;
            _label.RemoveFromClassList("interact-label--forbidden");
            _panel.style.backgroundColor = Color.clear;
            SetProgress(0f);
            _root.style.display = DisplayStyle.Flex;
            _panel.MarkDirtyRepaint();
        }
        else if (_forbidTimer <= 0f)
        {
            _root.style.display = DisplayStyle.None;
        }
    }

    private void HandleForbidden()
    {
        if (!IsReady()) return;
        _isForbidden = true;
        _label.text = "CANNOT INTERACT";
        _label.AddToClassList("interact-label--forbidden");
        _panel.style.backgroundColor = Color.clear;
        SetProgress(0f);
        _root.style.display = DisplayStyle.Flex;
        _forbidTimer = 0.6f;
        _panel.MarkDirtyRepaint();
    }

    private void HandleProgress(float v)
    {
        if (!IsReady()) return;
        if (v > 0f && _root.style.display == DisplayStyle.None)
            _root.style.display = DisplayStyle.Flex;
        SetProgress(v);
    }

    private void SetProgress(float v)
    {
        float pct = Mathf.Clamp01(v);
        _progressFill.style.width = Length.Percent(pct * 100f);
        _progressBar.style.display = pct > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void Update()
    {
        if (_forbidTimer > 0f)
        {
            _forbidTimer -= Time.unscaledDeltaTime;
            if (_forbidTimer <= 0f && IsReady())
            {
                _label.RemoveFromClassList("interact-label--forbidden");
                _panel.style.backgroundColor = Color.clear;
                _root.style.display = DisplayStyle.None;
            }
        }
    }

    private void OnGeneratePanelBackground(MeshGenerationContext ctx)
    {
        var targetElement = ctx.visualElement;
        if (targetElement == null) return;
        var rect = targetElement.layout;
        if (rect.width <= 0 || rect.height <= 0) return;

        var painter = ctx.painter2D;
        float chamferSize = 10f;

        // 1. Draw solid dark background shape (translucent dark red if forbidden, dark blue-gray if normal)
        Color fillCol = _isForbidden ? new Color(65f / 255f, 15f / 255f, 15f / 255f, 0.9f)
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

        // 2. Draw hazard warning stripes at the left edge
        float stripeW = 20f;
        float stripeH = rect.height - chamferSize;
        painter.lineWidth = 1.0f;
        for (float offset = 0; offset < stripeW; offset += 5f)
        {
            float startY = offset < chamferSize ? (chamferSize - offset) : 0f;
            painter.strokeColor = _isForbidden ? new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.85f)
                                               : new Color(217f / 255f, 199f / 255f, 115f / 255f, 0.8f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(offset, startY));
            painter.LineTo(new Vector2(offset + 4f, rect.height));
            painter.Stroke();

            float startYBlack = (offset + 2f) < chamferSize ? (chamferSize - (offset + 2f)) : 0f;
            painter.strokeColor = new Color(16f / 255f, 14f / 255f, 14f / 255f, 0.9f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(offset + 2f, startYBlack));
            painter.LineTo(new Vector2(offset + 6f, rect.height));
            painter.Stroke();
        }

        // 3. Draw outer border (gold if normal, red if forbidden)
        Color strokeCol = _isForbidden ? new Color(229f / 255f, 72f / 255f, 60f / 255f, 0.65f)
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

        // 4. Draw 4 3D metallic rivets
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
        };

        float rOffset = 6f;
        drawRivet(new Vector2(rect.width - rOffset, rOffset));
        drawRivet(new Vector2(rect.width - rOffset - chamferSize, rect.height - rOffset));
        drawRivet(new Vector2(rOffset + 20f, rect.height - rOffset));
        drawRivet(new Vector2(rOffset + 20f, rOffset));
    }
}
