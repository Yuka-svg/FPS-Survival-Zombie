using UnityEngine;

/// <summary>
/// Central design-system source of truth for the custom HUD/UI.
/// Role-named tokens (colors, spacing scale, radii, fonts, motion) so widgets
/// stop baking magic values. Captured from the existing custom HUD widgets +
/// PremiumUITheme + scene UIGradient instances (see consolidation research).
///
/// Access at runtime via UITheme.Active (loads "UITheme" from a Resources folder,
/// or use a serialized reference). This asset is additive; widgets are migrated
/// to read from it in later consolidation steps. Font styling still flows through
/// PremiumUITheme, which will be pointed at these font fields.
/// </summary>
[CreateAssetMenu(fileName = "UITheme", menuName = "UI/UITheme", order = 0)]
public class UITheme : ScriptableObject
{
    // ---- Palette (role-named, not color-named) ----
    [Header("Status — Health / Shield / Ammo")]
    public Color healthFull = new Color(0.85f, 0.78f, 0.45f, 1f);
    public Color healthLow = new Color(0.72f, 0.12f, 0.12f, 1f);
    public Color shield = new Color(0.45f, 0.78f, 0.95f, 1f);
    public Color ammoNormal = new Color(0.92f, 0.88f, 0.78f, 1f);
    public Color ammoLow = new Color(0.85f, 0.35f, 0.15f, 1f);

    [Header("Semantic gradients (top -> bottom)")]
    public Color dangerTop = new Color(0.95f, 0.32f, 0.27f, 1f);
    public Color dangerBottom = new Color(0.66f, 0.09f, 0.13f, 1f);
    public Color successTop = new Color(0.31f, 0.878f, 0.541f, 1f);
    public Color successBottom = new Color(0.094f, 0.639f, 0.31f, 1f);

    [Header("Surfaces (top -> bottom)")]
    public Color surfaceTop = new Color(0.137f, 0.165f, 0.20f, 0.94f);
    public Color surfaceBottom = new Color(0.078f, 0.094f, 0.118f, 0.94f);
    public Color cardTop = new Color(0.122f, 0.149f, 0.18f, 1f);
    public Color cardBottom = new Color(0.075f, 0.09f, 0.11f, 1f);
    public Color scrimTop = new Color(0.04f, 0.05f, 0.07f, 0.90f);
    public Color scrimBottom = new Color(0f, 0f, 0f, 0.93f);

    [Header("Text / Accent")]
    public Color textPrimary = new Color(0.96f, 0.96f, 0.96f, 1f);
    public Color textMuted = new Color(0.62f, 0.66f, 0.72f, 1f);
    public Color accent = new Color(0.85f, 0.78f, 0.45f, 1f);

    // ---- Spacing scale (px @ 1920x1080 reference) ----
    [Header("Spacing scale")]
    public float spaceXS = 4f;
    public float spaceS = 8f;
    public float spaceM = 16f;
    public float spaceL = 24f;
    public float spaceXL = 40f;

    // ---- Corner radii (matches generated 9-slice sprites) ----
    [Header("Radii")]
    public float radiusS = 8f;
    public float radiusM = 16f;
    public float radiusL = 22f;

    // ---- Motion (house style; matches UIButtonMotion / UIPanelTransition) ----
    [Header("Motion")]
    public float hoverScale = 1.06f;
    public float pressScale = 0.95f;
    public float panelInDuration = 0.22f;
    [Range(0.5f, 1f)] public float panelStartScale = 0.9f;
    public float ammoPunchScale = 1.14f;

    // ---- Access ----
    private static UITheme _active;

    /// <summary>
    /// The active theme. Loads a "UITheme" asset from any Resources folder.
    /// Returns null if none exists (callers must null-check and fall back to
    /// their own serialized defaults during migration).
    /// </summary>
    public static UITheme Active
    {
        get
        {
            if (_active == null) _active = Resources.Load<UITheme>("UITheme");
            return _active;
        }
    }

    /// <summary>Allow an explicit assignment (e.g. from a bootstrap) without Resources.</summary>
    public static void SetActive(UITheme theme) { _active = theme; }
}
