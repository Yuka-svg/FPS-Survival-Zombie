using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class WeaponInventoryWidget : MonoBehaviour
{
    public float slotSize = 64f;
    public float spacing = 8f;

    private CowsinsHUDAdapter _adapter;
    private VisualElement _row;
    private readonly List<VisualElement> _slots = new();
    private readonly List<VisualElement> _icons = new();
    private Color _slotBg = new(0.137f, 0.165f, 0.2f, 0.85f);
    private Color _selected = new(0.85f, 0.78f, 0.45f, 1f);

    private void Awake()
    {
        var th = UITheme.Active;
        if (th != null) { _slotBg = th.surfaceTop; _selected = th.accent; }
        var doc = GetComponent<UIDocument>();
        if (doc == null) { enabled = false; return; }
        var root = doc.rootVisualElement;
        _row = root.Q("WeaponInventoryCluster");
        if (_row == null) { enabled = false; return; }
    }

    private void OnEnable() { StartCoroutine(Bind()); }

    private void OnDisable()
    {
        if (_adapter != null)
        {
            _adapter.OnInventoryStructureChanged -= Rebuild;
            _adapter.OnWeaponSelected -= HandleSelected;
        }
        StopAllCoroutines();
    }

    private IEnumerator Bind()
    {
        float timeout = 12f;
        while (CowsinsHUDAdapter.Instance == null && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        _adapter = CowsinsHUDAdapter.Instance;
        if (_adapter == null) yield break;
        _adapter.OnInventoryStructureChanged += Rebuild;
        _adapter.OnWeaponSelected += HandleSelected;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_adapter == null) return;
        var slots = _adapter.WeaponSlots;
        int n = slots != null ? slots.Length : 0;
        EnsureSlots(n);
        for (int i = 0; i < _slots.Count; i++)
        {
            bool used = i < n;
            _slots[i].style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
            if (!used) continue;
            var info = slots[i];
            if (info.icon != null)
                _icons[i].style.backgroundImage = new StyleBackground(info.icon);
            else
                _icons[i].style.backgroundImage = null;
            _icons[i].style.opacity = info.occupied ? 1f : 0.12f;
        }
        HandleSelected(_adapter.SelectedWeaponIndex);
    }

    private void HandleSelected(int index)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].style.display == DisplayStyle.None) continue;
            bool sel = i == index;
            _slots[i].EnableInClassList("selected", sel);
        }
    }

    private void EnsureSlots(int count)
    {
        while (_slots.Count < count)
        {
            var idx = _slots.Count + 1;
            var slot = new VisualElement();
            slot.AddToClassList("weapon-inventory-slot");
            slot.style.backgroundColor = _slotBg;
            _row.Add(slot);
            _slots.Add(slot);

            var icon = new VisualElement();
            icon.AddToClassList("weapon-inventory-icon");
            slot.Add(icon);
            _icons.Add(icon);

            var label = new Label(idx.ToString());
            label.AddToClassList("weapon-inventory-index");
            slot.Add(label);
        }
    }
}
