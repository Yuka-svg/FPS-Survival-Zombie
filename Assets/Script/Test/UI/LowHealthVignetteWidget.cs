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
            _vignette.SetBackgroundImageSafe(vignetteTexture);
    }

    private void OnEnable()
    {
        StartCoroutine(Bind());
    }

    private System.Collections.IEnumerator Bind()
    {
        float timeout = 12f;
        while (CowsinsHUDAdapter.Instance == null && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        _adapter = CowsinsHUDAdapter.Instance;
        if (_adapter == null) yield break;

        _adapter.OnHealthChanged -= OnHealthChanged;
        _adapter.OnHealthChanged += OnHealthChanged;

        float maxHp = _adapter.MaxHealth > 0f ? _adapter.MaxHealth : 1f;
        Apply(_adapter.Health / maxHp);
    }

    private void OnDisable()
    {
        if (_adapter != null)
        {
            _adapter.OnHealthChanged -= OnHealthChanged;
            _adapter = null;
        }
        StopAllCoroutines();
    }

    private void OnHealthChanged(float health, float maxHealth, bool tookDamage)
    {
        float maxHp = maxHealth > 0f ? maxHealth : 1f;
        Apply(health / maxHp);
    }

    private void Apply(float ratio)
    {
        _vignette.EnableInClassList("critical", ratio <= criticalThreshold);
        _vignette.EnableInClassList("low", ratio <= threshold && ratio > criticalThreshold);
    }
}
