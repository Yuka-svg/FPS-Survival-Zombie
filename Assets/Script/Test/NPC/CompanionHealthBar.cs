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
        _barGO.SetActive(false);

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
        if (_barGO != null) _barGO.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_barGO != null)
        {
            if (_doc != null && _doc.rootVisualElement != null)
                _doc.rootVisualElement.Clear();
            Destroy(_barGO);
        }
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
        if (_barGO != null && !_barGO.activeSelf)
            _barGO.SetActive(true);
    }

    private void LateUpdate()
    {
        _currentOpacity = Mathf.MoveTowards(_currentOpacity, _targetOpacity, Time.deltaTime * 3f);
        if (_barGO == null || !_barGO) return;
        if (_doc == null || _doc.rootVisualElement == null) return;

        var root = _doc.rootVisualElement.Q("HealthBarRoot");
        if (root == null) return;

        if (!_stretched)
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
                _stretched = true;
            }
        }

        root.style.opacity = _currentOpacity;

        // Update fill width.
        if (_fill != null)
        {
            _fill.style.width = barSize.x * Mathf.Clamp01(_healthFraction);
            Color c = _healthFraction > 0.5f
                ? FullColor
                : (_healthFraction > 0.15f ? LowColor : DownedColor);
            _fill.style.backgroundColor = c;
        }
    }
}
