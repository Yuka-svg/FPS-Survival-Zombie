using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class DashWidget : MonoBehaviour
{
    public float pipSize = 20f;

    private UIDocument _doc;
    private VisualElement _root;
    private readonly List<VisualElement> _pips = new List<VisualElement>();
    private readonly List<VisualElement> _regen = new List<VisualElement>();
    private readonly List<Action<MeshGenerationContext>> _regenCallbacks = new List<Action<MeshGenerationContext>>();
    private int _current, _max;
    private float _regenStart;
    private CowsinsHUDAdapter _adapter;

    private static readonly Color RegenColor = new Color(0f, 0.86f, 0.7f, 0.45f);

    private void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        if (_doc == null) return;
        _root = _doc.rootVisualElement.Q("DashWidget");
        if (_root == null) return;

        _adapter = CowsinsHUDAdapter.Instance;
        if (_adapter != null)
        {
            _adapter.OnDashChanged += HandleDash;
            HandleDash(_adapter.CurrentDashes, _adapter.MaxDashes);
        }
    }

    private void OnDisable()
    {
        if (_adapter != null) _adapter.OnDashChanged -= HandleDash;
        for (int i = 0; i < _regen.Count; i++)
        {
            if (i < _regenCallbacks.Count)
                _regen[i].generateVisualContent -= _regenCallbacks[i];
        }
        _regenCallbacks.Clear();
    }

    private void HandleDash(int current, int max)
    {
        _current = current; _max = max;
        if (max <= 0)
        {
            EnsurePips(0);
            return;
        }
        EnsurePips(max);
        for (int i = 0; i < _pips.Count; i++)
            _pips[i].EnableInClassList("dash-pip--on", i < current);
        _regenStart = current < max ? Time.unscaledTime : 0f;
    }

    private void Update()
    {
        float cd = _adapter != null ? _adapter.DashCooldown : 0f;
        for (int i = 0; i < _regen.Count; i++)
        {
            bool show = i == _current && _current < _max && cd > 0f && _regenStart > 0f;
            _regen[i].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) _regen[i].MarkDirtyRepaint();
        }
    }

    private void EnsurePips(int count)
    {
        while (_pips.Count < count)
        {
            var pip = new VisualElement();
            pip.AddToClassList("dash-pip");
            pip.style.width = pipSize;
            pip.style.height = pipSize;
            _root.Add(pip);
            _pips.Add(pip);

            var regen = new VisualElement();
            regen.AddToClassList("dash-pip__regen");
            regen.style.display = DisplayStyle.None;
            var captured = regen;
            Action<MeshGenerationContext> cb = (ctx) => OnGenerateRegen(ctx, captured);
            regen.generateVisualContent += cb;
            _regenCallbacks.Add(cb);
            pip.Add(regen);
            _regen.Add(regen);
        }

        if (count == 0)
        {
            _root.style.display = DisplayStyle.None;
        }
        else
        {
            _root.style.display = DisplayStyle.Flex;
            for (int i = 0; i < _pips.Count; i++)
                _pips[i].style.display = i < count ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void OnGenerateRegen(MeshGenerationContext ctx, VisualElement element)
    {
        int idx = _regen.IndexOf(element);
        if (idx < 0) return;

        if (idx != _current || _current >= _max) return;

        float cd = _adapter != null ? _adapter.DashCooldown : 1f;
        float fillAmount = cd > 0f ? Mathf.Clamp01((Time.unscaledTime - _regenStart) / cd) : 1f;
        if (fillAmount <= 0f) return;

        float w = element.resolvedStyle.width;
        float h = element.resolvedStyle.height;
        if (w < 1f || h < 1f) return;

        var center = new Vector2(w * 0.5f, h * 0.5f);
        float radius = Mathf.Min(w, h) * 0.45f;

        var painter = ctx.painter2D;
        painter.BeginPath();
        painter.MoveTo(center);
        painter.fillColor = RegenColor;
        painter.Arc(center, radius, Angle.Degrees(90f), Angle.Degrees(90f - 360f * fillAmount), ArcDirection.Clockwise);
        painter.ClosePath();
        painter.Fill();
    }
}
