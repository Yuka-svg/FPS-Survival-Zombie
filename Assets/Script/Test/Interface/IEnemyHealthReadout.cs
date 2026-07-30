using System;

public enum EnemyType
{
    Normal,
    Special,
    Boss
}

/// <summary>
/// Read-only health surface used by world-space UI such as <c>EnemyHealthBar</c>.
/// Lets one bar component work for any enemy type (regular zombies, special
/// enemies like the Boomer) without the bar hard-referencing a concrete class.
/// Implementors expose state they already track internally; combat logic is
/// unaffected.
/// </summary>
public interface IEnemyHealthReadout
{
    /// <summary>Normalized health in [0,1]. 1 = full, 0 = dead/empty.</summary>
    float HealthFraction { get; }

    /// <summary>True once the enemy has died (may still be animating/pooling).</summary>
    bool IsDead { get; }

    /// <summary>
    /// Raised whenever health changes (damage or death). Argument is the new
    /// <see cref="HealthFraction"/> in [0,1].
    /// </summary>
    event Action<float> OnHealthChanged;

    /// <summary>Identifies if the enemy is Normal, Special, or Boss.</summary>
    EnemyType EnemyType { get; }
}
