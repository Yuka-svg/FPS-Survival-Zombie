using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class DamageDirectionHUD : MonoBehaviour
{
    public static DamageDirectionHUD Instance { get; private set; }

    public float radius = 160f;
    public float arcWidth = 100f, arcHeight = 6f;
    public float fadeTime = 1.0f;
    public int poolSize = 8;

    private readonly List<VisualElement> _pivots = new List<VisualElement>();
    private VisualElement _root;

    private static readonly Color ArcColor = new Color(0.898f, 0.282f, 0.235f, 1f);

    private void OnEnable()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
        var doc = GetComponent<UIDocument>();
        if (doc == null) { enabled = false; return; }
        _root = doc.rootVisualElement.Q("DamageDirection");
        if (_root == null) { enabled = false; return; }
        Build();
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        _root.Clear();
        _pivots.Clear();
        for (int i = 0; i < poolSize; i++)
        {
            var pivot = new VisualElement();
            pivot.name = "Dir";
            pivot.AddToClassList("dir-pivot");
            _root.Add(pivot);
            _pivots.Add(pivot);

            var arc = new VisualElement();
            arc.name = "Arc";
            arc.AddToClassList("dir-arc");
            arc.usageHints = UsageHints.DynamicTransform;
            arc.style.width = arcWidth;
            arc.style.height = arcHeight;
            arc.style.translate = new Translate(0, -radius);
            pivot.Add(arc);
        }
    }

    public void ShowDamageFrom(Vector3 worldAttackerPos)
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 dir = worldAttackerPos - cam.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Vector3 fwd = cam.transform.forward; fwd.y = 0f;
        float bearing = Vector3.SignedAngle(fwd, dir, Vector3.up);

        int idx = 0; float min = float.MaxValue;
        for (int i = 0; i < _pivots.Count; i++)
        {
            float a = _pivots[i].style.opacity.value;
            if (a < min) { min = a; idx = i; }
        }
        _pivots[idx].style.rotate = new Rotate(Angle.Degrees(-bearing));
        _pivots[idx].style.opacity = 1f;
    }

    private void Update()
    {
        float drop = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);
        for (int i = 0; i < _pivots.Count; i++)
        {
            float a = _pivots[i].style.opacity.value;
            if (a > 0f)
                _pivots[i].style.opacity = Mathf.Max(0f, a - drop);
        }
    }
}
