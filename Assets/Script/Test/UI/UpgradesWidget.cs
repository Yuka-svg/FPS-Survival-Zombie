using UnityEngine;
using UnityEngine.UIElements;
using cowsins;

public class UpgradesWidget : MonoBehaviour
{
    public float fontSize = 20f;

    private Label _label;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null) { enabled = false; return; }
        _label = doc.rootVisualElement != null ? doc.rootVisualElement.Q<Label>("UpgradesWidget") : null;
        if (PlayerUpgradeManager.Instance != null)
            PlayerUpgradeManager.Instance.OnUpgradesChanged += Refresh;
    }

    private void OnDisable()
    {
        if (PlayerUpgradeManager.Instance != null)
            PlayerUpgradeManager.Instance.OnUpgradesChanged -= Refresh;
        _label = null;
    }

    private void Refresh()
    {
        var m = PlayerUpgradeManager.Instance;
        if (m == null) return;
        int h = m.bonusHealth;
        int s = m.bonusShield;
        float sta = m.bonusStamina;
        float dmg = m.bonusDamage;

        if (_label == null) return;

        if (h == 0 && s == 0 && sta <= 0f && dmg <= 0f) { _label.style.opacity = 0; _label.text = ""; return; }

        var parts = new System.Collections.Generic.List<string>(4);
        if (h > 0) parts.Add("+" + h + " HP");
        if (s > 0) parts.Add("+" + s + " ARMOR");
        if (sta > 0f) parts.Add("+" + sta.ToString("0.#") + " STA");
        if (dmg > 0f) parts.Add("+" + (dmg * 100f).ToString("0.#") + "% DMG");
        _label.text = "PERKS   " + string.Join("   ", parts);
        _label.style.opacity = 1;
    }
}
