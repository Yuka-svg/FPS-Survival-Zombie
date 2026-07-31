using System.Collections.Generic;
using UnityEngine;

public static class EnemyRegistry
{
    public static readonly List<IEnemyHealthReadout> ActiveEnemies = new List<IEnemyHealthReadout>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        ActiveEnemies.Clear();
    }

    public static void Register(IEnemyHealthReadout enemy)
    {
        if (enemy != null && !ActiveEnemies.Contains(enemy))
        {
            ActiveEnemies.Add(enemy);
        }
    }

    public static void Unregister(IEnemyHealthReadout enemy)
    {
        if (enemy != null)
        {
            ActiveEnemies.Remove(enemy);
        }
    }
}
