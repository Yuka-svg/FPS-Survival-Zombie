using UnityEngine;

/// <summary>
/// Lightweight static bridge between CompanionManager (skip logic) and
/// WaveQuestInteractable (boss spawning). When the player accepts the
/// companion's stage-2 dialogue, CompanionManager sets
/// <see cref="PendingReducedBossHP"/> to a non-zero value. The next boss
/// spawned by WaveQuestInteractable reads and consumes this value, applying
/// it as the new maxHealth (and currentHealth) before the boss starts fighting.
///
/// This avoids a hard dependency from WaveQuestInteractable on CompanionManager.
/// </summary>
public static class CompanionBossSkipHook
{
    /// <summary>
    /// If &gt; 0, the next ISpecialEnemy spawned by WaveQuestInteractable will
    /// have its maxHealth set to this value. Consumed (set to 0) after one use.
    /// </summary>
    public static int PendingReducedBossHP = 0;

    /// <summary>Reset static state for a new game session.</summary>
    public static void ResetStaticState()
    {
        PendingReducedBossHP = 0;
    }
}
