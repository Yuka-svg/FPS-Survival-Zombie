using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public class EnemyHealthBar : MonoBehaviour
{
    [Tooltip("Local Y offset above the zombie root.")]
    public float heightOffset = 2.1f;
    [Tooltip("Bar size in canvas pixels before world scale.")]
    public Vector2 barSize = new Vector2(160f, 12f);
    [Tooltip("World-space canvas scale (pixels -> meters).")]
    public float worldScale = 0.0075f;

    [Tooltip("Detected or assigned enemy type.")]
    public EnemyType enemyType = EnemyType.Normal;

    private IEnemyHealthReadout _zombie;
    private GameObject _barGO;
    private VisualElement _fill;
    private Transform _cam;
    
    // Neon color transitions
    private Color _full = new Color(0f, 0.94f, 1f, 1f); // Neon Cyan (Normal)
    private Color _low = new Color(1f, 0.18f, 0.39f, 1f); // Pink (Normal)

    private float _hideTimer = 0f;
    private float _currentOpacity = 0f;
    private float _targetOpacity = 0f;
    private float _healthFraction = 1f;
    private bool _stretched = false;
    private const float ShowDuration = 3.0f; // Show for 3 seconds after change

    private void Awake()
    {
        _zombie = GetComponent<IEnemyHealthReadout>();
        if (_zombie != null)
        {
            enemyType = _zombie.EnemyType;
        }
        ApplyEnemyTypeConfiguration();
        Build();
    }

    private void ApplyEnemyTypeConfiguration()
    {
        // 1. Configure Boss
        if (enemyType == EnemyType.Boss)
        {
            barSize = new Vector2(240f, 22f); // Heroic larger bar
            _full = new Color(1f, 0.18f, 0.39f, 1f); // Crimson Red
            _low = new Color(0.44f, 0f, 1f, 1f); // Deep Violet
        }
        // 2. Configure Special
        else if (enemyType == EnemyType.Special)
        {
            barSize = new Vector2(180f, 16f); // Thicker bar
            _full = new Color(1f, 0.67f, 0f, 1f); // Gold/Orange
            _low = new Color(1f, 0.07f, 0.2f, 1f); // Red
        }
        // 3. Configure Normal
        else
        {
            barSize = new Vector2(160f, 12f);
            _full = new Color(0f, 0.94f, 1f, 1f); // Neon Cyan
            _low = new Color(1f, 0.18f, 0.39f, 1f); // Pink
        }
    }

    private void Build()
    {
        // Create GameObject and set as child of zombie to keep Hierarchy clean.
        // We create it without UIDocument first, and disable it before adding the component.
        // This forces Unity to initialize UIDocument correctly in World Space on first activation.
        _barGO = new GameObject("HealthBarPanel");
        _barGO.transform.SetParent(transform, false);
        _barGO.transform.localPosition = new Vector3(0f, heightOffset, 0f);
        _barGO.transform.localRotation = Quaternion.identity;
        _barGO.transform.localScale = Vector3.one;
        _barGO.SetActive(false);

        var doc = _barGO.AddComponent<UIDocument>();
        doc.sortingOrder = 100;
        doc.worldSpaceSize = new Vector2(barSize.x * worldScale, barSize.y * worldScale);

        // Load the World Space PanelSettings asset from Resources
        var settings = Resources.Load<PanelSettings>("WorldSpacePanelSettings");
        if (settings != null)
        {
            doc.panelSettings = settings;
        }
        else
        {
            Debug.LogWarning("[EnemyHealthBar] WorldSpacePanelSettings asset not found in Resources!");
        }

        // Destroy auto-generated collider immediately to prevent it from blocking player bullets/raycasts
        var col = _barGO.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }

        var asset = Resources.Load<VisualTreeAsset>("EnemyHealthBar");
        if (asset == null)
        {
            Debug.LogError("[EnemyHealthBar] VisualTreeAsset 'EnemyHealthBar' not found in Resources!");
            return;
        }
        doc.visualTreeAsset = asset;

        _stretched = false;
    }

    private void OnEnable()
    {
        if (_zombie != null) _zombie.OnHealthChanged += HandleHealth;
        _currentOpacity = 0f;
        _targetOpacity = 0f;
        _healthFraction = 1f;
        _stretched = false;
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
    }

    private void OnDisable()
    {
        if (_zombie != null) _zombie.OnHealthChanged -= HandleHealth;
        _currentOpacity = 0f;
        _targetOpacity = 0f;
        _stretched = false;

        if (_barGO != null && _barGO)
        {
            _barGO.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_barGO != null && _barGO)
        {
            var doc = _barGO.GetComponent<UIDocument>();
            if (doc != null && doc)
            {
                if (doc.rootVisualElement != null)
                {
                    doc.rootVisualElement.Clear();
                }
                doc.visualTreeAsset = null;
                doc.panelSettings = null;
            }
            Destroy(_barGO);
        }
        _fill = null;
        _zombie = null;
        _cam = null;
    }

    private void HandleHealth(float normalizedHealth)
    {
        if (_zombie == null) return;

        _healthFraction = normalizedHealth;

        if (_zombie.IsDead || normalizedHealth >= 1f)
        {
            _targetOpacity = 0f; // Fade out immediately on death or full health
            return;
        }

        _targetOpacity = 1f;
        _hideTimer = ShowDuration;

        if (_barGO != null && _barGO && !_barGO.activeSelf)
        {
            _barGO.SetActive(true);
        }
    }

    private void LateUpdate()
    {
        // Update opacity transition
        _currentOpacity = Mathf.MoveTowards(_currentOpacity, _targetOpacity, Time.deltaTime * 3.0f); // 0.33s fade

        if (_barGO == null || !_barGO) return;
        var doc = _barGO.GetComponent<UIDocument>();
        if (doc == null || !doc || doc.rootVisualElement == null) return;

        var root = doc.rootVisualElement.Q("HealthBarRoot");
        if (root == null) return;

        // Lazy-bind visual tree elements once they are initialized on active GameObject
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

                // Bind styling classes based on type
                root.ClearClassList();
                root.AddToClassList("healthbar-root");

                var nameLabel = doc.rootVisualElement.Q<Label>("NameLabel");
                var icon = doc.rootVisualElement.Q("Icon");

                if (enemyType == EnemyType.Boss)
                {
                    root.AddToClassList("healthbar-boss");
                    if (nameLabel != null)
                    {
                        nameLabel.text = "BOSS";
                        nameLabel.style.display = DisplayStyle.Flex;
                    }
                    if (icon != null) icon.style.display = DisplayStyle.Flex;
                }
                else if (enemyType == EnemyType.Special)
                {
                    root.AddToClassList("healthbar-special");
                    if (nameLabel != null)
                    {
                        string n = gameObject.name.ToLower();
                        if (n.Contains("bigguy") || GetComponent("BigGuyAI") != null) nameLabel.text = "BIG GUY";
                        else if (n.Contains("boomer") || GetComponent("BoomerAI") != null) nameLabel.text = "BOOMER";
                        else if (n.Contains("tank") || GetComponent("TankAI") != null) nameLabel.text = "TANK";
                        else if (n.Contains("witch") || GetComponent("WitchAI") != null) nameLabel.text = "WITCH";
                        else nameLabel.text = "SPECIAL";
                        
                        nameLabel.style.display = DisplayStyle.Flex;
                    }
                    if (icon != null) icon.style.display = DisplayStyle.Flex;
                }

                _fill = doc.rootVisualElement.Q("Fill");
                if (_fill != null)
                {
                    _fill.usageHints = UsageHints.DynamicTransform;
                }
                
                _stretched = true;
            }
        }

        root.style.opacity = _currentOpacity;
        if (_currentOpacity <= 0f)
        {
            _barGO.SetActive(false);
            _stretched = false; // Reset to allow re-binding cloned UXML on next activation
            return;
        }

        _barGO.SetActive(true);
        root.style.display = DisplayStyle.Flex;

        // Apply health fill changes
        if (_fill != null)
        {
            _fill.style.width = Length.Percent(Mathf.Clamp01(_healthFraction) * 100f);
            _fill.style.backgroundColor = Color.Lerp(_low, _full, _healthFraction);
        }

        // Count down display timer
        if (_targetOpacity > 0f)
        {
            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f)
            {
                _targetOpacity = 0f;
            }
        }

        if (_cam == null)
        {
            if (Camera.main != null) _cam = Camera.main.transform;
            else return;
        }

        // 1. Billboard: Align the 3D quad with the camera orientation
        _barGO.transform.rotation = _cam.rotation;

        // 2. Distance scaling: Adjust worldSpaceSize based on camera distance
        float dist = Vector3.Distance(_cam.position, _barGO.transform.position);
        float scale = Mathf.Clamp(12f / Mathf.Max(2f, dist), 0.4f, 1.1f);
        doc.worldSpaceSize = new Vector2(barSize.x * worldScale * scale, barSize.y * worldScale * scale);
    }
}
