using UnityEngine;
using UnityEngine.UIElements;

public class WeaponIndicatorWidget : MonoBehaviour
{
    public float fadeSpeed = 6f;

    private Label _weaponName;
    private VisualElement _weaponIcon;
    private VisualElement _root;
    private float _currentOpacity = 1f;

    private void Awake()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) { enabled = false; return; }
        var root = doc.rootVisualElement;
        _root = root.Q("WeaponIndicator");
        if (_root == null) { enabled = false; return; }
        _weaponName = _root.Q<Label>("WeaponName");
        _weaponIcon = _root.Q("WeaponIcon");
    }

    private void OnEnable()
    {
        var a = CowsinsHUDAdapter.Instance;
        if (a == null) return;
        a.OnWeaponChanged += OnWeapon;
        OnWeapon(a.WeaponName, a.WeaponIcon);
    }

    private void OnDisable()
    {
        var a = CowsinsHUDAdapter.Instance;
        if (a != null) a.OnWeaponChanged -= OnWeapon;
    }

    private float _slideOffset = 0f;

    private void OnWeapon(string n, Sprite icon)
    {
        if (_weaponName != null) _weaponName.text = string.IsNullOrEmpty(n) ? string.Empty : n.ToUpperInvariant();
        if (_weaponIcon != null)
        {
            if (icon != null) _weaponIcon.style.backgroundImage = new StyleBackground(icon);
            else _weaponIcon.style.backgroundImage = null;
        }

        _currentOpacity = 0.1f;
        _slideOffset = -15f;
        if (_root != null)
        {
            _root.style.opacity = _currentOpacity;
            _root.style.translate = new Translate(_slideOffset, 0f);
        }
    }

    private void Update()
    {
        if (_root == null) return;
        if (_currentOpacity < 1f || Mathf.Abs(_slideOffset) > 0.05f)
        {
            float dt = Time.unscaledDeltaTime;
            _currentOpacity = Mathf.MoveTowards(_currentOpacity, 1f, fadeSpeed * dt);
            _slideOffset = Mathf.Lerp(_slideOffset, 0f, 1f - Mathf.Exp(-12f * dt));
            _root.style.opacity = _currentOpacity;
            _root.style.translate = new Translate(_slideOffset, 0f);
        }
    }
}
