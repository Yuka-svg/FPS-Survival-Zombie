using UnityEngine;

/// <summary>
/// Helper dùng chung để rơi loot cho mọi enemy (ZombieAI, TankBossAI, BoomerAI, ...).
/// Hỗ trợ loot table (mỗi entry roll độc lập) + fallback legacy single-drop,
/// kèm hiệu ứng pop (parabol + spin) qua <see cref="LootPop"/> và trail effect
/// qua <see cref="LootTrail"/> với settings từ enemy.
/// </summary>
public static class LootDropHelper
{
    /// <summary>
    /// Roll loot table (mỗi entry độc lập) + fallback legacy, spawn tại
    /// <paramref name="position"/> + <paramref name="heightOffset"/>, gắn
    /// <see cref="LootPop"/> nếu <paramref name="popOnDeath"/> và
    /// <see cref="LootTrail"/> với <paramref name="trailSettings"/>.
    /// </summary>
    public static void TryDropLoot(
        LootDropEntry[] lootTable,
        GameObject fallbackPrefab,
        float fallbackDropChance,
        Vector3 position,
        float heightOffset,
        bool popOnDeath,
        float popUpwardSpeed,
        float popHorizontalSpeed,
        LootTrailSettings trailSettings)
    {
        bool usedTable = false;

        if (lootTable != null && lootTable.Length > 0)
        {
            usedTable = true;
            for (int i = 0; i < lootTable.Length; i++)
            {
                var entry = lootTable[i];
                if (entry.prefab == null)
                    continue;

                if (Random.Range(0f, 100f) <= entry.dropChance)
                {
                    int min = Mathf.Max(1, entry.minQuantity);
                    int max = Mathf.Max(min, entry.maxQuantity);
                    int count = Random.Range(min, max + 1);
                    for (int q = 0; q < count; q++)
                        SpawnLoot(entry.prefab, position, heightOffset,
                                  popOnDeath, popUpwardSpeed, popHorizontalSpeed,
                                  trailSettings);
                }
            }
        }

        if (!usedTable && fallbackPrefab != null &&
            Random.Range(0f, 100f) <= fallbackDropChance)
        {
            SpawnLoot(fallbackPrefab, position, heightOffset,
                      popOnDeath, popUpwardSpeed, popHorizontalSpeed,
                      trailSettings);
        }
    }

    public static GameObject SharedGiftBoxPrefab;

    public static void TryDropGiftBox(
        Vector3 position,
        float heightOffset,
        GameObject giftBoxPrefab,
        float giftBoxDropChance,
        bool popOnDeath = true,
        float popUpwardSpeed = 4.5f,
        float popHorizontalSpeed = 2.5f,
        LootTrailSettings trailSettings = null)
    {
        if (GameModeManager.CurrentMode != GameMode.Endless) return;
        if (giftBoxDropChance <= 0f) return;

        GameObject prefab = giftBoxPrefab != null ? giftBoxPrefab : SharedGiftBoxPrefab;
        if (prefab == null) return;

        if (Random.Range(0f, 100f) <= giftBoxDropChance)
        {
            Vector3 dropPos = position;
            dropPos.y += heightOffset;
            GameObject giftBox = Object.Instantiate(prefab, dropPos, Quaternion.identity);

            if (popOnDeath)
            {
                var pop = giftBox.GetComponent<LootPop>();
                if (pop == null)
                    pop = giftBox.AddComponent<LootPop>();
                pop.upwardSpeed = popUpwardSpeed;
                pop.horizontalSpeed = popHorizontalSpeed;
                pop.Launch(dropPos);

                if (trailSettings != null)
                {
                    var trail = giftBox.GetComponent<LootTrail>();
                    if (trail == null)
                        trail = giftBox.AddComponent<LootTrail>();
                    trail.Initialize(trailSettings);
                }
            }
        }
    }

    static void SpawnLoot(
        GameObject prefab,
        Vector3 position,
        float heightOffset,
        bool popOnDeath,
        float popUpwardSpeed,
        float popHorizontalSpeed,
        LootTrailSettings trailSettings)
    {
        Vector3 dropPos = position;
        dropPos.y += heightOffset;
        GameObject loot = Object.Instantiate(
            prefab,
            dropPos,
            Quaternion.identity);

        if (popOnDeath)
        {
            var pop = loot.GetComponent<LootPop>();
            if (pop == null)
                pop = loot.AddComponent<LootPop>();
            pop.upwardSpeed = popUpwardSpeed;
            pop.horizontalSpeed = popHorizontalSpeed;
            pop.Launch(dropPos);

            // Gắn trail effect (TrailRenderer + glow particle) runtime với
            // settings từ enemy. Initialize phải gọi ngay sau AddComponent
            // để build trail trước khi OnEnable chạy.
            if (trailSettings != null)
            {
                var trail = loot.GetComponent<LootTrail>();
                if (trail == null)
                    trail = loot.AddComponent<LootTrail>();
                trail.Initialize(trailSettings);
            }
        }
    }
}
