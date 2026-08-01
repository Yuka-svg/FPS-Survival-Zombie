using UnityEngine;
using UnityEngine.UIElements;
using cowsins;
using System.Collections.Generic;

public class WeaponSelectionWidget : MonoBehaviour
{
    [Header("Weapons")]
    public Weapon_SO[] availableWeapons;

    private UIDocument _doc;
    private VisualElement _panel;
    private VisualElement _card;
    private VisualElement _grid;

    private void Start()
    {
        StartCoroutine(DelayedInit());
    }

    private System.Collections.IEnumerator DelayedInit()
    {
        yield return null;

        _doc = GetComponent<UIDocument>();
        if (_doc == null)
        {
            GameObject go = GameObject.Find("GameUICanvas");
            if (go != null) _doc = go.GetComponent<UIDocument>();
        }
        if (_doc == null || _doc.rootVisualElement == null) yield break;

        _panel = _doc.rootVisualElement.Q("WeaponSelectionPanel");
        if (_panel == null) yield break;
        _card = _panel.Q("WeaponSelCard");
        _grid = _panel.Q("WeaponSelGrid");

        _panel.style.display = DisplayStyle.Flex;
        PopulateGrid();

        PauseGame();
        if (PanelManager.Instance != null)
            PanelManager.Instance.OpenPanel("WeaponSelection", _panel, _card, null);
    }

    private void PauseGame()
    {
        Time.timeScale = 0f;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var pc = player.GetComponentInChildren<PlayerControl>();
            if (pc != null) pc.LoseControl();
        }
        PauseMenu.isPaused = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var pc = player.GetComponentInChildren<PlayerControl>();
            if (pc != null) pc.GrantControl();
        }
        PauseMenu.isPaused = false;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
    }

    private void PopulateGrid()
    {
        if (_grid == null || availableWeapons == null) return;

        _grid.Clear();

        foreach (var weapon in availableWeapons)
        {
            if (weapon == null) continue;

            var card = new VisualElement();
            card.AddToClassList("weapon-card");

            var icon = new VisualElement();
            icon.AddToClassList("weapon-card-icon");
            icon.SetBackgroundImageSafe(weapon.icon);
            card.Add(icon);

            var label = new Label(weapon._name);
            label.AddToClassList("weapon-card-name");
            card.Add(label);

            Weapon_SO captured = weapon;
            card.RegisterCallback<PointerDownEvent>(evt => {
                evt.StopPropagation();
                OnWeaponSelected(captured);
            });

            _grid.Add(card);
        }
    }

    private void OnWeaponSelected(Weapon_SO selected)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            WeaponController wc = player.GetComponent<WeaponController>();
            if (wc != null)
            {
                var inv = wc.Inventory;
                int slots = inv != null ? inv.Length : 2;
                for (int i = 0; i < slots; i++)
                {
                    if (inv != null && i < inv.Length && inv[i] != null)
                        wc.ReleaseWeapon(i);
                }
                wc.InstantiateWeapon(selected, 0);
            }
        }

        if (PanelManager.Instance != null)
        {
            PanelManager.Instance.ClosePanel("WeaponSelection", _panel, _card, () =>
            {
                StartGame();
            });
        }
        else
        {
            _panel.style.display = DisplayStyle.None;
            StartGame();
        }
    }

    private void StartGame()
    {
        ResumeGame();
        if (WaveManager.Instance != null)
            WaveManager.Instance.StartWave();

        Destroy(this);
    }
}
