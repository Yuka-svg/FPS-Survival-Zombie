using UnityEngine;
using UnityEngine.UIElements;

public class LowHealthVignetteWidget : MonoBehaviour
{
    public float threshold = 0.3f;
    public float criticalThreshold = 0.15f;
    public Texture2D vignetteTexture;

    private VisualElement _vignette;
    private CowsinsHUDAdapter _adapter;

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) { enabled = false; return; }
        _vignette = doc.rootVisualElement.Q("LowHealthVignette");
        if (_vignette == null)
        {
            Debug.LogError("[LowHealthVignetteWidget] #LowHealthVignette not found");
            enabled = false;
            return;
        }
        if (vignetteTexture != null)
            _vignette.style.backgroundImage = vignetteTexture;
    }

    private void OnEnable()
    {
        _adapter = CowsinsHUDAdapter.Instance;
        if (_adapter != null)
        {
            _adapter.OnHealthChanged += OnHealthChanged;
            Apply(_adapter.Health / _adapter.MaxHealth);
        }
    }

    private void OnDisable()
    {
        if (_adapter != null)
            _adapter.OnHealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float health, float maxHealth, bool tookDamage)
    {
        Apply(health / maxHealth);
    }

    private void Apply(float ratio)
    {
        _vignette.EnableInClassList("critical", ratio <= criticalThreshold);
        _vignette.EnableInClassList("low", ratio <= threshold && ratio > criticalThreshold);
    }
}
