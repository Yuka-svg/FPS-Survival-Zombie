using System.Collections.Generic;
using UnityEngine;

public class CollectibleManager : MonoBehaviour
{
    public static CollectibleManager Instance;

    [Header("Auto-detection")]
    [Tooltip("Subfolder under Resources/ that holds every JournalData asset. " +
             "Total count is auto-loaded from here so designers only need to drop " +
             "new journal assets into the folder. Leave empty to disable auto-load.")]
    public string resourcesFolder = "Journals";

    [Tooltip("Manual override for total journals in the level. " +
             "If <= 0 the manager uses the auto-detected count from the Resources folder.")]
    public int totalOverride = 0;

    private List<JournalData> collected = new();

    /// <summary>Number of journals collected so far.</summary>
    public int Count => collected.Count;

    /// <summary>Total journals available in the game (auto-detected or overridden).</summary>
    public int Total { get; private set; }

    /// <summary>True when every journal in the game has been collected (True Ending condition).</summary>
    public bool HasAll => Total > 0 && collected.Count >= Total;

    private void Awake()
    {
        Instance = this;
        Total = ResolveTotal();
    }

    private int ResolveTotal()
    {
        if (totalOverride > 0)
            return totalOverride;

        if (!string.IsNullOrEmpty(resourcesFolder))
        {
            var all = Resources.LoadAll<JournalData>(resourcesFolder);
            if (all != null && all.Length > 0)
                return all.Length;
        }

        Debug.LogWarning(
            "[CollectibleManager] No JournalData assets found in Resources/" +
            resourcesFolder + " and totalOverride <= 0. Total fallbacks to 0."
        );
        return 0;
    }

    public void Collect(JournalData journal)
    {
        if (collected.Contains(journal))
            return;

        collected.Add(journal);

        if (PlayerStatsTracker.Instance != null)
            PlayerStatsTracker.Instance.RegisterJournalCollected();

        if (JournalUI.Instance != null)
            JournalUI.Instance.Show(journal);
        else
            Debug.LogWarning("[CollectibleManager] No JournalUI in scene; collected without display.");

        Debug.Log($"Collected {Count}/{Total}");
    }

    /// <summary>Number of collected journals in a given category.</summary>
    public int CountByCategory(JournalCategory category)
    {
        int n = 0;
        for (int i = 0; i < collected.Count; i++)
            if (collected[i].category == category)
                n++;
        return n;
    }

    /// <summary>Read-only access to the collected journals (e.g. for a gallery UI).</summary>
    public IReadOnlyList<JournalData> Collected => collected;
}
