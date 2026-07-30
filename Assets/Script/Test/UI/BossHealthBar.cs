using UnityEngine;
using UnityEngine.UIElements;

public class BossHealthBar : MonoBehaviour
{
    public string bossLabel = "BOSS";

    private TankBossAI _boss;
    private VisualElement _root;
    private VisualElement _fill;
    private Label _label;
    private Color _full = new Color(0.95f, 0.32f, 0.27f, 1f);
    private Color _low = new Color(0.66f, 0.09f, 0.13f, 1f);
    private float _reacquireTimer;

    private void OnEnable()
    {
        var uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null) return;
        var root = uiDoc.rootVisualElement;
        if (root == null) return;

        _root = root.Q<VisualElement>("BossHealthBar");
        _fill = _root?.Q<VisualElement>("BossFill");
        if (_fill != null) _fill.usageHints = UsageHints.DynamicTransform;
        _label = _root?.Q<Label>("BossLabel");

        if (_label != null) _label.text = bossLabel;
        Show(false);
    }

    private void OnDisable()
    {
        _root = null;
        _fill = null;
        _label = null;
    }

    private void Update()
    {
        if (_boss == null)
        {
            _reacquireTimer -= Time.unscaledDeltaTime;
            if (_reacquireTimer <= 0f)
            {
                _reacquireTimer = 0.5f;
                _boss = FindAnyObjectByType<TankBossAI>();
            }
        }
        Refresh();
    }

    public void SetBoss(TankBossAI boss) { _boss = boss; Refresh(); }

    public void Refresh()
    {
        if (_boss == null || !_boss.gameObject.activeInHierarchy || _boss.maxHealth <= 0 || _boss.currentHealth <= 0)
        {
            Show(false);
            return;
        }
        float f = Mathf.Clamp01((float)_boss.currentHealth / _boss.maxHealth);
        Show(true);
        if (_fill != null)
        {
            _fill.style.width = Length.Percent(f * 100);
            _fill.style.backgroundColor = Color.Lerp(_low, _full, f);
        }
    }

    private void Show(bool show)
    {
        if (_root != null)
            _root.style.display = DisplayStyle.None; // Force display to None always to remove screen-space overlay
    }
}
