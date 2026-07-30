using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class ProgressionHUDWidget : MonoBehaviour
{
    [Header("Coins")]
    [SerializeField] private string _coinsPrefix = "";

    [Header("XP / Level")]
    [SerializeField] private string _levelPrefix = "LV ";

    [Header("Tuning")]
    [SerializeField] private float _xpDamping = 6f;
    [SerializeField] private float _xpGhostDamping = 2.5f;

    private VisualElement _root;
    private Label _coinsLabel;
    private Label _levelValue;
    private VisualElement _xpFill;
    private VisualElement _xpGhost;
    private CowsinsHUDAdapter _adapter;
    private float _xpTarget;
    private float _currentFillPct;
    private float _currentGhostPct;

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) { enabled = false; return; }
        var root = doc.rootVisualElement;
        _root = root.Q<VisualElement>("ProgressionCluster");
        _coinsLabel = _root?.Q<Label>("CoinsLabel");
        _levelValue = root.Q<Label>("LevelValue");
        _xpFill = _root?.Q<VisualElement>("XpFill");
        _xpGhost = _root?.Q<VisualElement>("XpGhost");
        if (_xpFill != null) _xpFill.usageHints = UsageHints.DynamicTransform;
        if (_xpGhost != null) _xpGhost.usageHints = UsageHints.DynamicTransform;
        if (_root == null) { enabled = false; }
    }

    private void OnEnable() { StartCoroutine(Bind()); }

    private IEnumerator Bind()
    {
        while (CowsinsHUDAdapter.Instance == null) yield return null;
        _adapter = CowsinsHUDAdapter.Instance;
        _adapter.OnCoinsChanged += HandleCoins;
        _adapter.OnXpChanged += HandleXp;
        HandleCoins(_adapter.Coins);
        HandleXp(_adapter.PlayerLevel, _adapter.XpFill);
    }

    private void OnDisable()
    {
        if (_adapter != null)
        {
            _adapter.OnCoinsChanged -= HandleCoins;
            _adapter.OnXpChanged -= HandleXp;
        }
        StopAllCoroutines();
    }

    private void HandleCoins(int coins)
    {
        if (_coinsLabel != null) _coinsLabel.text = _coinsPrefix + coins.ToString();
    }

    private void HandleXp(int level, float fill)
    {
        if (_levelValue != null) _levelValue.text = _levelPrefix + level.ToString();
        _xpTarget = Mathf.Clamp01(fill);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (_xpFill != null)
        {
            float k = 1f - Mathf.Exp(-_xpDamping * dt);
            _currentFillPct = Mathf.Lerp(_currentFillPct, _xpTarget, k);
            _xpFill.style.width = Length.Percent(_currentFillPct * 100f);
        }
        if (_xpGhost != null)
        {
            if (_currentGhostPct < _xpTarget) _currentGhostPct = _xpTarget;
            else _currentGhostPct = Mathf.Lerp(_currentGhostPct, _xpTarget, 1f - Mathf.Exp(-_xpGhostDamping * dt));
            _xpGhost.style.width = Length.Percent(_currentGhostPct * 100f);
        }
    }
}
