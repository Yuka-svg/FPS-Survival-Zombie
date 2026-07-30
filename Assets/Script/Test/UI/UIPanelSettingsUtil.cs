using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Helper for borrowing PanelSettings from existing UIDocuments at runtime.
///
/// Several runtime-created UIDocuments (CutscenePlayer, DialogueBubble,
/// SimpleNotification, EpilogueSlide, CreditsSequence, BombExplosionCutscene)
/// don't have a PanelSettings asset assigned in the inspector — they borrow
/// one from an existing UIDocument via FindObjectsByType.
///
/// The project has TWO PanelSettings assets:
///   - GameUIPanelSettings      (screen-space overlay — what cutscenes/HUD want)
///   - WorldSpacePanelSettings  (world-space — used by CompanionHealthBar /
///                               CompanionRescueUI, rendered at a world position)
///
/// If a runtime UIDocument accidentally borrows WorldSpacePanelSettings, it
/// renders in WORLD SPACE at its GameObject's transform position instead of
/// on the camera screen — e.g. a cutscene appears "outside the map" at the
/// quest trigger's coordinates.
///
/// This utility filters out world-space panels so borrowers always get a
/// screen-space panel.
/// </summary>
public static class UIPanelSettingsUtil
{
    /// <summary>
    /// Returns true if the given PanelSettings is configured for world-space
    /// rendering. Detected by name since PanelSettings doesn't expose a public
    /// "worldSpace" flag. The project's world-space panel is named
    /// "WorldSpacePanelSettings" (see CompanionRescueUI / CompanionHealthBar).
    /// </summary>
    public static bool IsWorldSpace(PanelSettings ps)
    {
        if (ps == null) return false;
        return ps.name.IndexOf("WorldSpace", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Finds the first UIDocument whose panelSettings is screen-space (i.e.
    /// NOT world-space). Returns null if none found. Pass in the caller's own
    /// UIDocument to skip it.
    /// </summary>
    public static UIDocument FindScreenSpaceUIDocument(UIDocument exclude)
    {
        var allDocs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var d in allDocs)
        {
            if (d == exclude || d.panelSettings == null) continue;
            if (IsWorldSpace(d.panelSettings)) continue;
            return d;
        }
        return null;
    }

    /// <summary>
    /// Finds the first screen-space PanelSettings asset loaded in the project
    /// (searches all loaded assets, not just scene UIDocuments). Used as a
    /// fallback when no screen-space UIDocument exists in the scene yet.
    /// </summary>
    public static PanelSettings FindScreenSpacePanelSettingsAsset()
    {
        var allPS = Resources.FindObjectsOfTypeAll(typeof(PanelSettings));
        foreach (var ps in allPS)
        {
            var settings = ps as PanelSettings;
            if (settings != null && !IsWorldSpace(settings))
                return settings;
        }
        return null;
    }
}
