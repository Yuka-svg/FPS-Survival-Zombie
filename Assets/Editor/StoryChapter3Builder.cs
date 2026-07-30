using UnityEditor;
using UnityEngine;
using cowsins;

/// <summary>
/// One-shot editor utility that creates the Chapter 3 QuestData assets and
/// wires the StoryManager + Chapter 3 scene objects (Construction site area).
/// Run via the menu: Tools/Story/Build Chapter 3. Safe to re-run (idempotent —
/// updates existing assets/links instead of duplicating).
///
/// Chapter 3 — Công trường (Construction):
///   Quest 5: Tiến vào công trường — reach the construction site entrance.
///   Quest 6: Loot vật tư — interact with a loot crate (find supplies + Shotgun).
///   Quest 7: Khởi động máy phát điện — interact with the generator; a mini-wave
///            of zombies spawns as a final combat encounter before advancing.
/// </summary>
public static class StoryChapter3Builder
{
    private const string QuestFolder = "Assets/Resources/Quests";
    private const string JournalFolder = "Assets/Resources/Journals";

    [MenuItem("Tools/Story/Build Chapter 3")]
    public static void Build()
    {
        EnsureFolder("Assets/Resources", "Quests");

        // ---- Journal rewards ----
        var militaryRecord02 = AssetDatabase.LoadAssetAtPath<JournalData>(
            JournalFolder + "/MilitaryRecord_02.asset");
        var expReport02 = AssetDatabase.LoadAssetAtPath<JournalData>(
            JournalFolder + "/ExperimentReport_02.asset");
        var brotherJournal01 = AssetDatabase.LoadAssetAtPath<JournalData>(
            JournalFolder + "/BrotherJournal_01.asset");

        // ---- Quest assets ----
        var q5 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_05_ReachConstruction.asset",
            questId: 5, chapter: 3,
            title: "Tiến vào công trường",
            description: "Bên kia bệnh viện là một công trường bỏ hoang. Tiếng máy gầm vang vọng giữa đống đổ nạt. Đi sâu vào công trường để tìm manh mối tiếp theo.",
            objective: "Đi đến công trường",
            expReward: 150f,
            journalReward: militaryRecord02,
            notification: "Đã đến công trường! Nhận Hồ sơ quân sự #2. Xung quanh có nhiều zombie — cẩn thận.");

        var q6 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_06_LootSupplies.asset",
            questId: 6, chapter: 3,
            title: "Loot vật tư",
            description: "Tìm thùng vật tư trong công trường. Bên trong có đạn, thuốc và có thể có vũ khí mới. Nhấn E để tương tác với thùng loot.",
            objective: "Tìm và loot thùng vật tư",
            expReward: 200f,
            journalReward: expReport02,
            notification: "Đã loot xong vật tư! Nhận Báo cáo thí nghiệm #2 và Shotgun. Tiếp tục tìm máy phát điện.");

        var q7 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_07_StartGenerator.asset",
            questId: 7, chapter: 3,
            title: "Khởi động máy phát điện",
            description: "Khởi động máy phát điện để mở cửa sang khu dân cư. Tiếng máy thu hút hàng chục zombie — 2 wave (15 + 20). KHÔNG THỂ THOÁT khi đã bắt đầu!",
            objective: "Khởi động máy phát điện và sống sót qua 2 wave (35 zombie)",
            expReward: 500f,
            journalReward: brotherJournal01,
            notification: "Máy phát điện đã khởi động! Sống sót qua 2 wave zombie. Nhận Nhật ký anh trai #1. Mở khóa khu dân cư.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- Wire StoryManager ----
        var smGO = GameObject.Find("StoryManager");
        if (smGO == null)
        {
            Debug.LogError("[StoryChapter3Builder] StoryManager not found in scene.");
            return;
        }
        var sm = smGO.GetComponent<StoryManager>();
        if (sm == null)
        {
            Debug.LogError("[StoryChapter3Builder] StoryManager component missing.");
            return;
        }
        sm.chapter3Quests = new QuestData[] { q5, q6, q7 };
        EditorUtility.SetDirty(sm);

        // ---- Scene setup ----
        SetupScene(q5, q6, q7);

        AssetDatabase.SaveAssets();
        Debug.Log("[StoryChapter3Builder] Chapter 3 built: quests + scene wired.");
    }

    private static void SetupScene(QuestData q5, QuestData q6, QuestData q7)
    {
        // 1) Find the Ch3 zone.
        var ch3 = GameObject.Find("=== WORLD ===/StoryZones/Ch3_Construction");
        if (ch3 == null)
        {
            Debug.LogError("[StoryChapter3Builder] StoryZones/Ch3_Construction not found.");
            return;
        }
        Debug.Log($"[StoryChapter3Builder] Ch3_Construction at {ch3.transform.position}.");

        // 2) Create / configure a Ch3 spawner (Spawm) for the construction area.
        //    Higher caps to support ambient spawns alongside the large Q7 waves.
        var spawnerGO = FindChild(ch3, "Ch3_Spawner");
        if (spawnerGO == null)
        {
            spawnerGO = new GameObject("Ch3_Spawner");
            spawnerGO.transform.SetParent(ch3.transform, false);
        }
        spawnerGO.transform.localPosition = new Vector3(0f, 0f, 50f);
        var spawm = GetOrAdd<Spawm>(spawnerGO);
        spawm.maxZombie = 40;
        spawm.spawnInterval = 1.5f;
        spawm.spawnAreaSize = new Vector3(90f, 0f, 150f);
        spawm.minDistanceFromPlayer = 8f;
        spawm.poolSize = 80;
        spawm.zombiePrefabs = LoadConstructionZombiePrefabs();
        spawm.enabled = false; // ChapterBoundary enables on enter.
        EditorUtility.SetDirty(spawnerGO);
        Debug.Log("[StoryChapter3Builder] Ch3_Spawner wired (maxZombie=40, pool=80).");

        // 3) Create Q5_ReachTrigger at the Ch3 entrance (positive Z edge, near Ch2).
        var q5TriggerGO = FindChild(ch3, "Q5_ReachTrigger");
        if (q5TriggerGO == null)
        {
            q5TriggerGO = new GameObject("Q5_ReachTrigger");
            q5TriggerGO.transform.SetParent(ch3.transform, false);
        }
        q5TriggerGO.transform.localPosition = new Vector3(0f, 1f, 100f);
        var q5Box = GetOrAddBoxCollider(q5TriggerGO);
        q5Box.size = new Vector3(20f, 4f, 8f);
        q5Box.isTrigger = true;
        q5TriggerGO.layer = 2; // Ignore Raycast

        var q5Trigger = GetOrAdd<QuestTrigger>(q5TriggerGO);
        q5Trigger.targetQuest = q5;
        q5Trigger.mode = QuestTrigger.Mode.OnPlayerEnter;
        q5Trigger.oneShot = true;

        // Brief intro cutscene when entering the construction site.
        var q5Cutscene = GetOrAdd<CutscenePlayer>(q5TriggerGO);
        q5Cutscene.title = "Công trường bỏ hoang";
        q5Cutscene.body = "Đống đổ nát, máy móc gỉ sét. Xung quanh im ắng... nhưng không lâu.";
        q5Cutscene.fadeIn = 0.5f;
        q5Cutscene.hold = 3f;
        q5Cutscene.fadeOut = 0.8f;
        q5Trigger.cutscene = q5Cutscene;
        EditorUtility.SetDirty(q5TriggerGO);
        Debug.Log("[StoryChapter3Builder] Q5_ReachTrigger wired (reach + intro cutscene).");

        // 4) Fix Q6_LootInteractable position and wire it as a QuestInteractable.
        var q6GO = FindChild(ch3, "Q6_LootInteractable");
        if (q6GO == null)
        {
            Debug.LogError("[StoryChapter3Builder] Q6_LootInteractable not found.");
            return;
        }
        // Move inside the Ch3 boundary (was misplaced far outside).
        q6GO.transform.localPosition = new Vector3(15f, 1f, 60f);
        q6GO.layer = 9; // Interactable layer
        var q6Box = q6GO.GetComponent<BoxCollider>();
        if (q6Box != null)
        {
            q6Box.size = new Vector3(3f, 2f, 3f);
            q6Box.isTrigger = true;
        }

        var q6Trigger = q6GO.GetComponent<QuestTrigger>();
        if (q6Trigger == null) q6Trigger = q6GO.AddComponent<QuestTrigger>();
        q6Trigger.targetQuest = q6;
        q6Trigger.mode = QuestTrigger.Mode.Manual;
        q6Trigger.oneShot = true;

        var q6Interactable = GetOrAdd<QuestInteractable>(q6GO);
        q6Interactable.questTrigger = q6Trigger;
        q6Interactable.interactText = "Loot thùng vật tư";
        q6Interactable.destroyAfterUse = false;
        q6Interactable.disableColliderAfterUse = true;
        EditorUtility.SetDirty(q6GO);
        Debug.Log("[StoryChapter3Builder] Q6_LootInteractable wired (QuestInteractable + QuestTrigger Manual).");

        // 5) Place Shotgun Pickeable near the loot crate + ammo caches for the big waves.
        PlaceShotgunPickeable(ch3, new Vector3(15f, 1f, 62f));
        PlaceShotgunAmmo(ch3, new Vector3(15f, 1f, 64f), 1);
        PlaceShotgunAmmo(ch3, new Vector3(17f, 1f, 62f), 2);
        // Ammo cache near the generator for wave resupply.
        PlaceShotgunAmmo(ch3, new Vector3(-5f, 1f, 50f), 3);
        PlaceShotgunAmmo(ch3, new Vector3(5f, 1f, 50f), 4);

        // 6) Wire Q7_Generator as a WaveQuestInteractable — 2 waves,
        //    Player is locked inside Ch3 during waves.
        var q7GO = FindChild(ch3, "Q7_Generator");
        if (q7GO == null)
        {
            Debug.LogError("[StoryChapter3Builder] Q7_Generator not found.");
            return;
        }
        // Move generator to a central, defensible position inside Ch3.
        q7GO.transform.localPosition = new Vector3(0f, 1f, 50f);
        q7GO.layer = 9; // Interactable layer
        var q7Box = q7GO.GetComponent<BoxCollider>();
        if (q7Box != null)
        {
            q7Box.size = new Vector3(3f, 3f, 3f);
            q7Box.isTrigger = true;
        }

        var q7Trigger = q7GO.GetComponent<QuestTrigger>();
        if (q7Trigger == null) q7Trigger = q7GO.AddComponent<QuestTrigger>();
        q7Trigger.targetQuest = q7;
        q7Trigger.mode = QuestTrigger.Mode.Manual;
        q7Trigger.oneShot = true;

        // Remove the old QuestInteractable if present from a previous build
        // (WaveQuestInteractable replaces it).
        var oldInteractable = q7GO.GetComponent<QuestInteractable>();
        if (oldInteractable != null)
        {
            Object.DestroyImmediate(oldInteractable, true);
            Debug.Log("[StoryChapter3Builder] Removed old QuestInteractable from Q7_Generator.");
        }

        // Intro cutscene warning before the generator starts and zombies spawn.
        var q7Cutscene = GetOrAdd<CutscenePlayer>(q7GO);
        q7Cutscene.title = "Máy phát điện";
        q7Cutscene.body = "Tiếng máy gầm vang lên. Hàng chục zombie nghe thấy và đang kéo đến... 2 wave! Không thể thoát!";
        q7Cutscene.fadeIn = 0.5f;
        q7Cutscene.hold = 4f;
        q7Cutscene.fadeOut = 0.8f;
        q7Trigger.cutscene = q7Cutscene;

        var q7Wave = GetOrAdd<WaveQuestInteractable>(q7GO);
        q7Wave.questTrigger = q7Trigger;
        q7Wave.interactText = "Khởi động máy phát điện";
        q7Wave.disableColliderAfterUse = true;
        q7Wave.introCutscene = q7Cutscene;
        q7Wave.waveBanner = q7Cutscene; // Reuse the same CutscenePlayer for banners.
        q7Wave.spawnOffset = new Vector3(0f, 0f, 8f);
        q7Wave.spawnSpread = 12f; // Wide spread for large wave counts.
        q7Wave.initialDelay = 1.5f;
        q7Wave.suppressChapterSpawners = true; // Only wave spawns count toward kills.

        // 2 waves: 15 + 20 = 35 enemies total.
        var w1Prefabs = LoadWavePrefabs(15, includeBoss: false, toughRatio: 0.15f);
        var w2Prefabs = LoadWavePrefabs(20, includeBoss: false, toughRatio: 0.2f);

        q7Wave.waves = new WaveQuestInteractable.Wave[]
        {
            new WaveQuestInteractable.Wave
            {
                prefabs = w1Prefabs,
                killsRequired = w1Prefabs.Length,
                waveTitle = "WAVE 1 — 15 ZOMBIE",
                waveSubtitle = "Zombie kéo đến! Giữ vị trí!",
                breatherDelay = 5f,
            },
            new WaveQuestInteractable.Wave
            {
                prefabs = w2Prefabs,
                killsRequired = w2Prefabs.Length,
                waveTitle = "WAVE 2 — 20 ZOMBIE",
                waveSubtitle = "Chưa hết đâu! Còn 20 con nữa!",
                breatherDelay = 0f, // Final wave — no breather after.
            },
        };

        EditorUtility.SetDirty(q7GO);
        Debug.Log($"[StoryChapter3Builder] Q7_Generator wired (2 waves, {w1Prefabs.Length}+{w2Prefabs.Length} enemies, player locked in).");

        // 7) Wire ChapterBoundary for Ch3 + lock boundary reference for Q7.
        var boundary = ch3.GetComponent<ChapterBoundary>();
        if (boundary != null)
        {
            boundary.chapter = 3;
            boundary.spawners = new MonoBehaviour[] { spawm };
            // Lock the player inside Ch3 during Q7 waves.
            q7Wave.lockBoundary = boundary;
            EditorUtility.SetDirty(ch3);
            Debug.Log("[StoryChapter3Builder] ChapterBoundary wired for Ch3 (lockBoundary -> Q7).");
        }

        // 8) Configure SaveRoom_Ch3.
        var saveRoomGO = FindChild(ch3, "SaveRoom_Ch3");
        if (saveRoomGO != null)
        {
            var sr = saveRoomGO.GetComponent<SaveRoom>();
            if (sr == null) sr = saveRoomGO.AddComponent<SaveRoom>();
            sr.healRate = 25f;
            sr.restoreShield = true;
            sr.spawnersToSuppress = new MonoBehaviour[] { spawm };
            sr.chapterTransitionOnEnter = 3; // Play "CHƯƠNG 3" cutscene on first enter.
            EditorUtility.SetDirty(saveRoomGO);
            Debug.Log("[StoryChapter3Builder] SaveRoom_Ch3 wired (heal + suppress Ch3_Spawner + chapter transition).");
        }

        // 9) Place QuestBeacons to guide the player through Ch3.
        PlaceBeacons(ch3, q5, q6, q7, saveRoomGO);

        // 10) Mark the scene dirty so it can be saved.
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    private static void PlaceBeacons(GameObject ch3, QuestData q5, QuestData q6, QuestData q7, GameObject saveRoomGO)
    {
        // Beacon A: SaveRoom_Ch3 — blue chapter guide beacon.
        if (saveRoomGO != null)
        {
            var beaconA = GetOrAdd<QuestBeacon>(saveRoomGO);
            beaconA.showOnQuest = null;
            beaconA.showOnChapter = 3;
            beaconA.beamColor = new Color(0.3f, 0.85f, 1f, 0.5f);
            beaconA.iconColor = new Color(0.4f, 0.9f, 1f, 1f);
            beaconA.ringColor = new Color(0.3f, 0.85f, 1f, 0.6f);
            beaconA.beamHeight = 14f;
            beaconA.hideDistance = 3f;
            EditorUtility.SetDirty(saveRoomGO);
            Debug.Log("[StoryChapter3Builder] Beacon placed on SaveRoom_Ch3 (blue, chapter guide).");
        }

        // Beacon B: Q5 reach trigger — gold for first objective.
        var q5TriggerGO = FindChild(ch3, "Q5_ReachTrigger");
        if (q5TriggerGO != null)
        {
            var beaconB = GetOrAdd<QuestBeacon>(q5TriggerGO);
            beaconB.showOnQuest = q5;
            beaconB.showOnChapter = 0;
            beaconB.beamColor = new Color(1f, 0.85f, 0.3f, 0.5f);
            beaconB.iconColor = new Color(1f, 0.9f, 0.4f, 1f);
            beaconB.ringColor = new Color(1f, 0.85f, 0.3f, 0.6f);
            beaconB.beamHeight = 12f;
            beaconB.hideDistance = 3f;
            EditorUtility.SetDirty(q5TriggerGO);
            Debug.Log("[StoryChapter3Builder] Beacon placed for Q5 (gold, reach trigger).");
        }

        // Beacon C: Q6 loot crate — green for exploration/loot.
        var q6GO = FindChild(ch3, "Q6_LootInteractable");
        if (q6GO != null)
        {
            var beaconC = GetOrAdd<QuestBeacon>(q6GO);
            beaconC.showOnQuest = q6;
            beaconC.showOnChapter = 0;
            beaconC.beamColor = new Color(0.4f, 1f, 0.5f, 0.5f);
            beaconC.iconColor = new Color(0.5f, 1f, 0.6f, 1f);
            beaconC.ringColor = new Color(0.4f, 1f, 0.5f, 0.6f);
            beaconC.beamHeight = 10f;
            beaconC.hideDistance = 3f;
            EditorUtility.SetDirty(q6GO);
            Debug.Log("[StoryChapter3Builder] Beacon placed for Q6 (green, loot crate).");
        }

        // Beacon D: Q7 generator — red for danger (final combat encounter).
        var q7GO = FindChild(ch3, "Q7_Generator");
        if (q7GO != null)
        {
            var beaconD = GetOrAdd<QuestBeacon>(q7GO);
            beaconD.showOnQuest = q7;
            beaconD.showOnChapter = 0;
            beaconD.beamColor = new Color(1f, 0.4f, 0.3f, 0.5f);
            beaconD.iconColor = new Color(1f, 0.5f, 0.4f, 1f);
            beaconD.ringColor = new Color(1f, 0.4f, 0.3f, 0.6f);
            beaconD.beamHeight = 14f;
            beaconD.hideDistance = 3f;
            EditorUtility.SetDirty(q7GO);
            Debug.Log("[StoryChapter3Builder] Beacon placed for Q7 (red, generator).");
        }
    }

    private static void PlaceShotgunPickeable(GameObject ch3, Vector3 localPos)
    {
        var existing = FindChild(ch3, "Shotgun Pickeable (Ch3)");
        if (existing != null)
        {
            Debug.Log("[StoryChapter3Builder] Shotgun Pickeable (Ch3) already exists.");
            return;
        }

        var shotgunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/[PRESETS]WeaponPickeables/Shotgun Pickeable.prefab");
        if (shotgunPrefab == null)
        {
            Debug.LogWarning("[StoryChapter3Builder] Shotgun Pickeable prefab not found.");
            return;
        }

        var shotgun = (GameObject)PrefabUtility.InstantiatePrefab(shotgunPrefab, ch3.scene);
        shotgun.name = "Shotgun Pickeable (Ch3)";
        shotgun.transform.SetParent(ch3.transform, true);
        shotgun.transform.localPosition = localPos;

        // Ensure the WeaponPickeable uses the Shotgun weapon SO.
        var wp = shotgun.GetComponent<WeaponPickeable>();
        if (wp != null)
        {
            var shotgunSO = AssetDatabase.LoadAssetAtPath<Weapon_SO>(
                "Assets/Engine/Cowsins/ScriptableObjects/Weapons/Shotgun.asset");
            if (shotgunSO != null)
            {
                var weaponField = typeof(WeaponPickeable).GetField("weapon",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (weaponField != null) weaponField.SetValue(wp, shotgunSO);
            }
        }

        Debug.Log($"[StoryChapter3Builder] Shotgun Pickeable placed at local {localPos}.");
    }

    private static void PlaceShotgunAmmo(GameObject ch3, Vector3 localPos, int index)
    {
        var name = $"Shotgun Ammo (Ch3) {index:00}";
        var existing = FindChild(ch3, name);
        if (existing != null)
        {
            existing.transform.localPosition = localPos;
            return;
        }

        var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/Bullet Pickeable.prefab");
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[StoryChapter3Builder] Bullet Pickeable prefab not found.");
            return;
        }

        var ammo = (GameObject)PrefabUtility.InstantiatePrefab(bulletPrefab, ch3.scene);
        ammo.name = name;
        ammo.transform.SetParent(ch3.transform, true);
        ammo.transform.localPosition = localPos;
        Debug.Log($"[StoryChapter3Builder] {name} placed at local {localPos}.");
    }

    private static GameObject[] LoadConstructionZombiePrefabs()
    {
        // Construction-themed zombies: road workers, firefighters, hazard suits,
        // bikers, gangsters, prisoners — tough-looking types for a rough area.
        string[] names = {
            "Zombie_Roadworker_Male_01",
            "Zombie_Firefighter_Male_01",
            "Zombie_BioHazardSuit_Male_01",
            "Zombie_Hobo_Male_01",
            "Zombie_Biker_Male_01",
            "Zombie_Gangster_Male_01",
            "Zombie_Prisoner_Male_01",
            "Zombie_RiotCop_Male_01",
        };
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (var n in names)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/OG Prefab/Crooks/" + n + ".prefab");
            if (p != null) list.Add(p);
        }
        return list.ToArray();
    }

    /// <summary>
    /// Builds a wave prefab array with the given count, optionally including a
    /// Boomer boss. A fraction of the wave (toughRatio) uses "tough" zombie types
    /// (firefighter, riot cop, military); the rest use standard types.
    /// </summary>
    private static GameObject[] LoadWavePrefabs(int count, bool includeBoss, float toughRatio)
    {
        // Standard construction zombies (easy/medium).
        string[] standardNames = {
            "Zombie_Roadworker_Male_01",
            "Zombie_Hobo_Male_01",
            "Zombie_Biker_Male_01",
            "Zombie_Gangster_Male_01",
            "Zombie_Prisoner_Male_01",
            "Zombie_Hoodie_Male_01",
            "Zombie_Punk_Male_01",
            "Zombie_Jacket_Male_01",
        };
        // Tough zombies (tanky, harder to kill).
        string[] toughNames = {
            "Zombie_Firefighter_Male_01",
            "Zombie_RiotCop_Male_01",
            "Zombie_Military_Male_01",
            "Zombie_BioHazardSuit_Male_01",
        };

        var standard = LoadZombiePrefabsByNames(standardNames);
        var tough = LoadZombiePrefabsByNames(toughNames);
        if (standard.Length == 0 && tough.Length > 0) standard = tough;

        var list = new System.Collections.Generic.List<GameObject>();
        int toughCount = Mathf.RoundToInt(count * toughRatio);
        int standardCount = count - toughCount;

        // Fill standard zombies (cycling through the array for variety).
        for (int i = 0; i < standardCount; i++)
            list.Add(standard[i % standard.Length]);

        // Fill tough zombies.
        for (int i = 0; i < toughCount; i++)
            list.Add(tough[i % tough.Length]);

        // Shuffle so tough zombies are mixed in, not clustered.
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }

        // Add Boomer boss at the end if requested.
        if (includeBoss)
        {
            var boomer = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/OG Prefab/Boss/SM_Chr_ZombieBoss_Slobber_01.prefab");
            if (boomer != null)
                list.Add(boomer);
            else
                Debug.LogWarning("[StoryChapter3Builder] Boomer (Slobber) prefab not found.");
        }

        return list.ToArray();
    }

    private static GameObject[] LoadZombiePrefabsByNames(string[] names)
    {
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (var n in names)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/OG Prefab/Crooks/" + n + ".prefab");
            if (p != null) list.Add(p);
        }
        return list.ToArray();
    }

    // ---- Helpers ----

    private static QuestData CreateOrUpdateQuest(
        string path, int questId, int chapter, string title,
        string description, string objective, float expReward,
        JournalData journalReward, string notification)
    {
        var existing = AssetDatabase.LoadAssetAtPath<QuestData>(path);
        QuestData q = existing != null ? existing : ScriptableObject.CreateInstance<QuestData>();

        q.questId = questId;
        q.chapter = chapter;
        q.title = title;
        q.description = description;
        q.objective = objective;
        q.expReward = expReward;
        q.journalReward = journalReward;
        q.completionNotification = notification;

        if (existing == null)
            AssetDatabase.CreateAsset(q, path);
        else
            EditorUtility.SetDirty(q);

        return q;
    }

    private static void EnsureFolder(string parent, string folder)
    {
        string path = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }

    private static GameObject FindChild(GameObject parent, string name)
    {
        for (int i = 0; i < parent.transform.childCount; i++)
        {
            var c = parent.transform.GetChild(i);
            if (c.name == name) return c.gameObject;
        }
        return null;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    private static BoxCollider GetOrAddBoxCollider(GameObject go)
    {
        var c = go.GetComponent<BoxCollider>();
        if (c == null) c = go.AddComponent<BoxCollider>();
        return c;
    }
}
