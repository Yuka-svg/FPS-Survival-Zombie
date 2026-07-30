using UnityEngine;
using UnityEngine.UIElements;

public class StaminaWidget : MonoBehaviour
{
    [Tooltip("How fast the visible fill chases the target.")] public float fillSpeed = 6f;
    [Tooltip("Below this fraction the bar reads as 'low' and tints toward danger.")]
    [Range(0f, 1f)] public float lowThreshold = 0.25f;

    private VisualElement _cluster;
    private VisualElement _fill;
    private Color _full = new Color(0.31f, 0.878f, 0.541f, 1f);
    private Color _low = new Color(0.85f, 0.35f, 0.15f, 1f);

    private CowsinsHUDAdapter _adapter;
    private float _target = 1f;
    private bool _used = true;

    private void Awake()
    {
        var th = UITheme.Active;
        if (th != null) { _full = th.successTop; _low = th.ammoLow; }
    }

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) return;
        _cluster = doc.rootVisualElement.Q("StaminaCluster");
        if (_cluster == null) return;
        _fill = _cluster.Q("StaminaFill");

        StartCoroutine(Bind());
    }

    private System.Collections.IEnumerator Bind()
    {
        float timeout = 12f;
        while (CowsinsHUDAdapter.Instance == null && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        _adapter = CowsinsHUDAdapter.Instance;
        if (_adapter == null) yield break;
        _adapter.OnStaminaChanged += HandleStamina;
        HandleStamina(_adapter.Stamina, _adapter.MaxStamina);
    }

    private void OnDisable()
    {
        if (_adapter != null) _adapter.OnStaminaChanged -= HandleStamina;
        StopAllCoroutines();
    }

    private void HandleStamina(float current, float max)
    {
        _used = _adapter == null || _adapter.UsesStamina;
        _target = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        _fill.style.width = Length.Percent(_target * 100f);
        _fill.style.backgroundColor = Color.Lerp(_low, _full, Mathf.InverseLerp(lowThreshold, 1f, _target));
        bool full = _target > 0.999f;
        _cluster.style.opacity = (!_used) ? 0f : (full ? 0f : 1f);
    }
}
