using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// World-space health bar for the companion NPC. Reuses the same
/// WorldSpacePanelSettings + EnemyHealthBar VisualTreeAsset as enemies,
/// but reads from the companion's IEnemyHealthReadout and uses ally colors
/// (green) instead of enemy colors.
/// </summary>
[RequireComponent(typeof(CompanionAI))]
public class CompanionHealthBar : MonoBehaviour
{
    public float heightOffset = 2.0f;
    public Vector2 barSize = new Vector2(160f, 12f);
    public float worldScale = 0.0075f;

    private CompanionAI _companion;
    private GameObject _barGO;
    private UIDocument _doc;
    private VisualElement _fill;
    private Transform _cam;

    private float _currentOpacity = 0f;
    private float _targetOpacity = 0f;
    private float _healthFraction = 1f;
    private bool _stretched;

    private static readonly Color FullColor = new Color(0.2f, 0.95f, 0.3f, 1f); // Green
    private static readonly Color LowColor = new Color(0.95f, 0.85f, 0.1f, 1f); // Yellow
    private static readonly Color DownedColor = new Color(0.6f, 0.6f, 0.6f, 1f); // Gray

    private void Awake()
    {
        _companion = GetComponent<CompanionAI>();
        Build();
    }

    private void Build()
    {
        _barGO = new GameObject("CompanionHealthBarPanel");
        _barGO.transform.SetParent(transform, false);
        _barGO.transform.localPosition = new Vector3(0f, heightOffset, 0f);
        _barGO.transform.localRotation = Quaternion.identity;
        _barGO.transform.localScale = Vector3.one;

        _doc = _barGO.AddComponent<UIDocument>();
        _doc.sortingOrder = 150;
        _doc.worldSpaceSize = new Vector2(barSize.x * worldScale, barSize.y * worldScale);

        var settings = Resources.Load<PanelSettings>("WorldSpacePanelSettings");
        if (settings != null)
            _doc.panelSettings = settings;
        else
            Debug.LogWarning("[CompanionHealthBar] WorldSpacePanelSettings not found in Resources!");

        var col = _barGO.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var asset = Resources.Load<VisualTreeAsset>("EnemyHealthBar");
        if (asset != null)
            _doc.visualTreeAsset = asset;
        else
            Debug.LogWarning("[CompanionHealthBar] EnemyHealthBar VisualTreeAsset not found in Resources!");

        _stretched = false;

        // Keep _barGO active in hierarchy to guarantee UIDocument.OnDisable() is called by Unity Engine on teardown
        _barGO.SetActive(true);
    }

    private void OnEnable()
    {
        if (_companion != null) _companion.OnHealthChanged += HandleHealth;
        _currentOpacity = 0f;
        _targetOpacity = 0f;
        _healthFraction = 1f;
        _stretched = false;
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
    }

    private void OnDisable()
    {
        if (_companion != null) _companion.OnHealthChanged -= HandleHealth;
        _currentOpacity = 0f;
        _targetOpacity = 0f;
        _stretched = false;

        // Prevent 1-frame ghost pop when GameObject is re-enabled from pooling/spawner
        if (_barGO != null && _barGO)
        {
            var doc = _barGO.GetComponent<UIDocument>();
            if (doc != null && doc.rootVisualElement != null)
            {
                var root = doc.rootVisualElement.Q("HealthBarRoot");
                if (root != null) root.style.display = DisplayStyle.None;
            }
        }
    }

    private void OnDestroy()
    {
        if (_barGO != null && _barGO)
        {
            Destroy(_barGO);
            _barGO = null;
        }
        _fill = null;
        _companion = null;
        _cam = null;
    }

    private void HandleHealth(float normalizedHealth)
    {
        _healthFraction = normalizedHealth;
        if (normalizedHealth >= 1f)
        {
            _targetOpacity = 0f;
            return;
        }
        _targetOpacity = 1f;
    }

    private void LateUpdate()
    {
        _currentOpacity = Mathf.MoveTowards(_currentOpacity, _targetOpacity, Time.deltaTime * 3f);
        if (_barGO == null || !_barGO) return;
        if (_doc == null || !_doc || _doc.rootVisualElement == null) return;

        var root = _doc.rootVisualElement.Q("HealthBarRoot");
        if (root == null) return;

        // 1. Resilient Live-Reload VisualElement Binding Guard (runs BEFORE early return so elements are bound on frame 1)
        if (!_stretched || _fill == null || _fill.panel == null)
        {
            var container = root.parent;
            if (container != null)
            {
                container.style.position = Position.Absolute;
                container.style.left = 0f;
                container.style.top = 0f;
                container.style.width = barSize.x;
                container.style.height = barSize.y;
                root.style.width = barSize.x;
                root.style.height = barSize.y;
                root.ClearClassList();
                root.AddToClassList("healthbar-root");
                root.AddToClassList("healthbar-special"); // Reuse special styling.

                // Hide the name label and icon for the companion.
                var nameLabel = _doc.rootVisualElement.Q<Label>("NameLabel");
                if (nameLabel != null) nameLabel.style.display = DisplayStyle.None;
                var icon = _doc.rootVisualElement.Q("Icon");
                if (icon != null) icon.style.display = DisplayStyle.None;

                _fill = root.Q("Fill") ?? root.Q<VisualElement>("Fill");
                if (_fill != null)
                {
                    _fill.usageHints = UsageHints.DynamicTransform;
                    _stretched = true;
                }
            }
        }

        // 2. ALWAYS update Billboard rotation FIRST so 3D orientation is aligned before fade-in
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
        if (_cam != null) _barGO.transform.rotation = _cam.rotation;

        // 3. Opacity & UI Toolkit Visibility Control
        root.style.opacity = _currentOpacity;
        if (_currentOpacity <= 0f)
        {
            root.style.display = DisplayStyle.None;
            return;
        }

        root.style.display = DisplayStyle.Flex;

        // 4. Update fill width and color dynamically
        if (_fill != null)
        {
            _fill.style.width = Length.Percent(Mathf.Clamp01(_healthFraction) * 100f);
            Color c = _healthFraction > 0.5f
                ? FullColor
                : (_healthFraction > 0.15f ? LowColor : DownedColor);
            _fill.style.backgroundColor = c;
        }

        // 5. Distance scaling: Adjust worldSpaceSize based on camera distance
        if (_cam != null)
        {
            float dist = Vector3.Distance(_cam.position, _barGO.transform.position);
            float scale = Mathf.Clamp(12f / Mathf.Max(2f, dist), 0.4f, 1.1f);
            _doc.worldSpaceSize = new Vector2(barSize.x * worldScale * scale, barSize.y * worldScale * scale);
        }
    }
}
