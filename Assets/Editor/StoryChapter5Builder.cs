using UnityEditor;
using UnityEngine;
using cowsins;

/// <summary>
/// One-shot editor utility that creates the Chapter 5 QuestData assets and
/// wires the StoryManager + Chapter 5 scene objects (Apartment + Broken Bridge).
/// Run via the menu: Tools/Story/Build Chapter 5. Safe to re-run (idempotent —
/// updates existing assets/links instead of duplicating).
///
/// Chapter 5 — Khu chung cư + Cây cầu gãy (Apartment + Broken Bridge):
///   Quest 10: Tìm bộ kích nổ — collect the 4 final journals/documents scattered
///             across the apartment area (brother's final journal, military
///             records, cure record) to learn the full truth and find the bomb.
///   Quest 11: Đối đầu Boss cuối — interact with the bomb at the apartment top
///             floor; Tank (Brute boss) spawns and must be defeated to fully
///             activate the bomb. Player is locked inside during the fight.
///   Quest 12: Thoát khỏi thị trấn — reach the broken bridge escape trigger
///             before the bomb explodes. Completing this finishes the story.
/// </summary>
public static class StoryChapter5Builder
{
    private const string QuestFolder = "Assets/Resources/Quests";
    private const string JournalFolder = "Assets/Resources/Journals";

    [MenuItem("Tools/Story/Build Chapter 5")]
    public static void Build()
    {
        EnsureFolder("Assets/Resources", "Quests");

        // ---- Journal rewards ----
        var expReport03 = AssetDatabase.LoadAssetAtPath<JournalData>(
            JournalFolder + "/ExperimentReport_03.asset");

        // ---- Quest assets ----
        var q10 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_10_FindDetonator.asset",
            questId: 10, chapter: 5,
            title: "Tìm bộ kích nổ",
            description: "Tìm bộ kích nổ và thu thập hồ sơ bệnh nhân số 001 cùng các tài liệu cuối cùng trong khu chung cư. Những manh mối này sẽ hé lộ toàn bộ sự thật về nguồn gốc dịch bệnh và người anh trai.",
            objective: "Thu thập đủ 4 tài liệu/journal trong khu chung cư",
            expReward: 300f,
            journalReward: expReport03,
            notification: "Đã thu thập đủ tài liệu! Nhận Báo cáo thí nghiệm #3. Bộ kích nổ ở tầng trên chung cư — đến đó để kích hoạt quả bom.");

        var q11 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_11_DefeatTank.asset",
            questId: 11, chapter: 5,
            title: "Đối đầu Boss cuối",
            description: "Kích hoạt quả bom tại tầng trên chung cư. Boss cuối — Tank — xuất hiện và ngăn cản. Tiêu diệt Tank, sau đó kích hoạt lại quả bom để hoàn tất. KHÔNG THỂ THOÁT khi đã bắt đầu!",
            objective: "Tiêu diệt Tank và kích hoạt lại quả bom",
            expReward: 1000f,
            journalReward: null,
            notification: "Quả bom đã được kích hoạt hoàn toàn! CHẠY NGAY đến cầu gãy trước khi vụ nổ!");

        var q12 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_12_EscapeTown.asset",
            questId: 12, chapter: 5,
            title: "Thoát khỏi thị trấn",
            description: "Chạy đến cây cầu gãy và nhảy xuống dòng sông bên dưới trước khi vụ nổ xảy ra. Mang theo toàn bộ tài liệu đã thu thập — đây là hồi kết của hành trình.",
            objective: "Chạy đến cầu gãy và thoát khỏi thị trấn",
            expReward: 500f,
            journalReward: null,
            notification: "Bạn đã thoát khỏi thị trán! Toàn bộ zombie bị tiêu diệt. Hành trình khép lại...");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- Wire StoryManager ----
        var smGO = GameObject.Find("StoryManager");
        if (smGO == null)
        {
            Debug.LogError("[StoryChapter5Builder] StoryManager not found in scene.");
            return;
        }
        var sm = smGO.GetComponent<StoryManager>();
        if (sm == null)
        {
            Debug.LogError("[StoryChapter5Builder] StoryManager component missing.");
            return;
        }
        sm.chapter5Quests = new QuestData[] { q10, q11, q12 };
        EditorUtility.SetDirty(sm);

        // ---- Scene setup ----
        SetupScene(q10, q11, q12);

        AssetDatabase.SaveAssets();
        Debug.Log("[StoryChapter5Builder] Chapter 5 built: quests + scene wired.");
    }

    private static void SetupScene(QuestData q10, QuestData q11, QuestData q12)
    {
        // 1) Find the Ch5 zone.
        var ch5 = GameObject.Find("=== WORLD ===/StoryZones/Ch5_ApartmentBridge");
        if (ch5 == null)
        {
            Debug.LogError("[StoryChapter5Builder] StoryZones/Ch5_ApartmentBridge not found.");
            return;
        }
        Debug.Log($"[StoryChapter5Builder] Ch5_ApartmentBridge at {ch5.transform.position}.");

        // 2) Create / configure a Ch5 spawner (Spawm) for the apartment area.
        //    Higher caps for the final chapter — more dangerous area.
        var spawnerGO = FindChild(ch5, "Ch5_Spawner");
        if (spawnerGO == null)
        {
            spawnerGO = new GameObject("Ch5_Spawner");
            spawnerGO.transform.SetParent(ch5.transform, false);
        }
        spawnerGO.transform.localPosition = new Vector3(-40f, 0f, 0f);
        var spawm = GetOrAdd<Spawm>(spawnerGO);
        spawm.maxZombie = 30;
        spawm.spawnInterval = 1.5f;
        spawm.spawnAreaSize = new Vector3(120f, 0f, 80f);
        spawm.minDistanceFromPlayer = 8f;
        spawm.poolSize = 60;
        spawm.zombiePrefabs = LoadApartmentZombiePrefabs();
        spawm.enabled = false; // ChapterBoundary enables on enter.
        EditorUtility.SetDirty(spawnerGO);
        Debug.Log("[StoryChapter5Builder] Ch5_Spawner wired (maxZombie=30, pool=60).");

        // 3) Wire Q10_CollectibleObjective — completes Q10 when every Ch5
        //    collectible (journal) has been picked up by the player.
        var q10ObjGO = FindChild(ch5, "Q10_CollectibleObjective");
        if (q10ObjGO == null)
        {
            q10ObjGO = new GameObject("Q10_CollectibleObjective");
            q10ObjGO.transform.SetParent(ch5.transform, false);
        }
        q10ObjGO.transform.localPosition = Vector3.zero;

        // Gather every Collectible under Ch5 (the journals the player must find).
        var required = new System.Collections.Generic.List<Collectible>();
        for (int i = 0; i < ch5.transform.childCount; i++)
        {
            var col = ch5.transform.GetChild(i).GetComponent<Collectible>();
            if (col != null) required.Add(col);
        }

        var collectObj = GetOrAdd<CollectibleQuestObjective>(q10ObjGO);
        collectObj.targetQuest = q10;
        collectObj.requiredCollectibles = required.ToArray();

        // Cutscene played once all documents are collected (story beat before boss).
        var q10Cutscene = GetOrAdd<CutscenePlayer>(q10ObjGO);
        q10Cutscene.title = "Sự thật cuối cùng";
        q10Cutscene.body = "Từ những tài liệu này, bạn hiểu toàn bộ sự thật: anh trai chính là bệnh nhân số 001 — người thử thuốc đầu tiên. Bộ kích nổ ở tầng trên. Phải kết thúc mọi thứ.";
        q10Cutscene.fadeIn = 0.6f;
        q10Cutscene.hold = 5f;
        q10Cutscene.fadeOut = 1.0f;
        collectObj.completionCutscene = q10Cutscene;
        EditorUtility.SetDirty(q10ObjGO);
        Debug.Log($"[StoryChapter5Builder] Q10_CollectibleObjective wired (need {required.Count} collectibles).");

        // 4) Wire Q11_BombObjective as a WaveQuestInteractable — Tank boss fight.
        //    The player interacts with the bomb, Tank spawns, must be defeated.
        var q11GO = FindChild(ch5, "Q11_BombObjective");
        if (q11GO == null)
        {
            Debug.LogError("[StoryChapter5Builder] Q11_BombObjective not found.");
            return;
        }
        q11GO.layer = 9; // Interactable layer
        var q11Box = q11GO.GetComponent<BoxCollider>();
        if (q11Box != null)
        {
            q11Box.size = new Vector3(3f, 3f, 3f);
            q11Box.isTrigger = true;
        }

        var q11Trigger = q11GO.GetComponent<QuestTrigger>();
        if (q11Trigger == null) q11Trigger = q11GO.AddComponent<QuestTrigger>();
        q11Trigger.targetQuest = q11;
        q11Trigger.mode = QuestTrigger.Mode.Manual;
        q11Trigger.oneShot = true;

        // Remove old QuestInteractable if present (WaveQuestInteractable replaces it).
        var oldInteractable = q11GO.GetComponent<QuestInteractable>();
        if (oldInteractable != null)
        {
            Object.DestroyImmediate(oldInteractable, true);
            Debug.Log("[StoryChapter5Builder] Removed old QuestInteractable from Q11_BombObjective.");
        }

        // Intro cutscene before the bomb activation and Tank spawns.
        var q11Cutscene = GetOrAdd<CutscenePlayer>(q11GO);
        q11Cutscene.title = "Kích hoạt quả bom";
        q11Cutscene.body = "Bạn bắt đầu gắn bộ kích nổ vào quả bom... nhưng có thứ gì đó khổng lồ đang tiến đến. TANK xuất hiện! Tiêu diệt nó rồi kích hoạt lại quả bom!";
        q11Cutscene.fadeIn = 0.6f;
        q11Cutscene.hold = 5f;
        q11Cutscene.fadeOut = 1.0f;
        q11Trigger.cutscene = q11Cutscene;

        var q11Wave = GetOrAdd<WaveQuestInteractable>(q11GO);
        q11Wave.questTrigger = q11Trigger;
        q11Wave.interactText = "Kích hoạt quả bom";
        q11Wave.disableColliderAfterUse = true;
        q11Wave.introCutscene = q11Cutscene;
        q11Wave.waveBanner = q11Cutscene;
        q11Wave.spawnOffset = new Vector3(0f, 0f, 10f);
        q11Wave.spawnSpread = 15f; // Wide spread for the boss + minions.
        q11Wave.initialDelay = 2f;
        q11Wave.suppressChapterSpawners = true;

        // Phase 2: after defeating Tank, the player must interact with the bomb
        // AGAIN to fully activate it (per Level Design doc — "đánh bại Tank để
        // hoàn tất quá trình kích nổ").
        q11Wave.requireSecondInteraction = true;
        q11Wave.secondInteractText = "Kích hoạt lại quả bom";
        q11Wave.completionCutscene = q11Cutscene; // Reuse the same CutscenePlayer for the completion banner.

        // Single wave: Tank boss + minion zombies. Tank must be defeated to complete.
        var bossWavePrefabs = LoadBossWavePrefabs(tankBoss: true, minionCount: 15);

        q11Wave.waves = new WaveQuestInteractable.Wave[]
        {
            new WaveQuestInteractable.Wave
            {
                prefabs = bossWavePrefabs,
                killsRequired = bossWavePrefabs.Length,
                waveTitle = "BOSS — TANK",
                waveSubtitle = "Tank xuất hiện! Tiêu diệt nó để kích hoạt hoàn toàn quả bom!",
                breatherDelay = 0f, // No breather — this is the final fight.
            },
        };

        EditorUtility.SetDirty(q11GO);
        Debug.Log($"[StoryChapter5Builder] Q11_BombObjective wired (Tank boss + {bossWavePrefabs.Length - 1} minions, player locked in).");

        // 5) Place ammo and health caches near the bomb for the boss fight.
        PlaceAmmo(ch5, new Vector3(-50f, 1f, -40f), 1);
        PlaceAmmo(ch5, new Vector3(-60f, 1f, -45f), 2);
        PlaceAmmo(ch5, new Vector3(-55f, 1f, -35f), 3);
        PlaceHealth(ch5, new Vector3(-52f, 1f, -38f), 1);
        PlaceHealth(ch5, new Vector3(-58f, 1f, -42f), 2);

        // 6) Wire Q12_EscapeTrigger — reach trigger at the broken bridge.
        var q12GO = FindChild(ch5, "Q12_EscapeTrigger");
        if (q12GO == null)
        {
            Debug.LogError("[StoryChapter5Builder] Q12_EscapeTrigger not found.");
            return;
        }
        var q12Box = q12GO.GetComponent<BoxCollider>();
        if (q12Box != null)
        {
            q12Box.size = new Vector3(12f, 6f, 12f);
            q12Box.isTrigger = true;
        }
        q12GO.layer = 2; // Ignore Raycast

        var q12Trigger = q12GO.GetComponent<QuestTrigger>();
        if (q12Trigger == null) q12Trigger = q12GO.AddComponent<QuestTrigger>();
        q12Trigger.targetQuest = q12;
        q12Trigger.mode = QuestTrigger.Mode.OnPlayerEnter;
        q12Trigger.oneShot = true;

        // Final escape cutscene when the player reaches the broken bridge.
        var q12Cutscene = GetOrAdd<CutscenePlayer>(q12GO);
        q12Cutscene.title = "Thoát khỏi thị trấn";
        q12Cutscene.body = "Bạn nhảy xuống dòng sông bên dưới cầu gãy. Vài giây sau, quả bom phát nổ — toàn bộ zombie trong thành phố bị tiêu diệt. Hành trình khép lại...";
        q12Cutscene.fadeIn = 0.8f;
        q12Cutscene.hold = 6f;
        q12Cutscene.fadeOut = 2.0f;
        q12Trigger.cutscene = q12Cutscene;
        EditorUtility.SetDirty(q12GO);
        Debug.Log("[StoryChapter5Builder] Q12_EscapeTrigger wired (reach + final escape cutscene).");

        // 7) Wire ChapterBoundary for Ch5 + lock boundary reference for Q11.
        var boundary = ch5.GetComponent<ChapterBoundary>();
        if (boundary != null)
        {
            boundary.chapter = 5;
            boundary.spawners = new MonoBehaviour[] { spawm };
            // Lock the player inside Ch5 during the Tank boss fight.
            q11Wave.lockBoundary = boundary;
            EditorUtility.SetDirty(ch5);
            Debug.Log("[StoryChapter5Builder] ChapterBoundary wired for Ch5 (lockBoundary -> Q11).");
        }

        // 8) Configure SaveRoom_Ch5 (suppress Ch5 spawner while resting).
        var saveRoomGO = FindChild(ch5, "SaveRoom_Ch5");
        if (saveRoomGO != null)
        {
            var sr = saveRoomGO.GetComponent<SaveRoom>();
            if (sr == null) sr = saveRoomGO.AddComponent<SaveRoom>();
            sr.healRate = 25f;
            sr.restoreShield = true;
            sr.spawnersToSuppress = new MonoBehaviour[] { spawm };
            sr.chapterTransitionOnEnter = 5; // Play "CHƯƠNG 5" cutscene on first enter.
            EditorUtility.SetDirty(saveRoomGO);
            Debug.Log("[StoryChapter5Builder] SaveRoom_Ch5 wired (heal + suppress Ch5_Spawner + chapter transition).");
        }

        // 9) Place QuestBeacons to guide the player through Ch5.
        PlaceBeacons(ch5, q10, q11, q12, saveRoomGO);

        // 10) Mark the scene dirty so it can be saved.
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    private static void PlaceBeacons(GameObject ch5, QuestData q10, QuestData q11, QuestData q12, GameObject saveRoomGO)
    {
        // Beacon A: SaveRoom_Ch5 — blue chapter guide beacon.
        if (saveRoomGO != null)
        {
            var beaconA = GetOrAdd<QuestBeacon>(saveRoomGO);
            beaconA.showOnQuest = null;
            beaconA.showOnChapter = 5;
            beaconA.beamColor = new Color(0.3f, 0.85f, 1f, 0.5f);
            beaconA.iconColor = new Color(0.4f, 0.9f, 1f, 1f);
            beaconA.ringColor = new Color(0.3f, 0.85f, 1f, 0.6f);
            beaconA.beamHeight = 14f;
            beaconA.hideDistance = 3f;
            EditorUtility.SetDirty(saveRoomGO);
            Debug.Log("[StoryChapter5Builder] Beacon placed on SaveRoom_Ch5 (blue, chapter guide).");
        }

        // Beacon B: every Ch5 collectible — green beams mark exploration targets
        // while Q10 is active.
        for (int i = 0; i < ch5.transform.childCount; i++)
        {
            var child = ch5.transform.GetChild(i);
            var col = child.GetComponent<Collectible>();
            if (col == null) continue;

            var beacon = GetOrAdd<QuestBeacon>(child.gameObject);
            beacon.showOnQuest = q10;
            beacon.showOnChapter = 0;
            beacon.beamColor = new Color(0.4f, 1f, 0.5f, 0.5f); // Green for exploration.
            beacon.iconColor = new Color(0.5f, 1f, 0.6f, 1f);
            beacon.ringColor = new Color(0.4f, 1f, 0.5f, 0.6f);
            beacon.beamHeight = 10f;
            beacon.hideDistance = 3f;
            EditorUtility.SetDirty(child.gameObject);
            Debug.Log($"[StoryChapter5Builder] Beacon placed on {child.name} (green, Q10 collectible).");
        }

        // Beacon C: Q11 bomb objective — red for danger (final boss fight).
        var q11GO = FindChild(ch5, "Q11_BombObjective");
        if (q11GO != null)
        {
            var beaconC = GetOrAdd<QuestBeacon>(q11GO);
            beaconC.showOnQuest = q11;
            beaconC.showOnChapter = 0;
            beaconC.beamColor = new Color(1f, 0.3f, 0.2f, 0.6f);
            beaconC.iconColor = new Color(1f, 0.4f, 0.3f, 1f);
            beaconC.ringColor = new Color(1f, 0.3f, 0.2f, 0.7f);
            beaconC.beamHeight = 16f;
            beaconC.hideDistance = 3f;
            EditorUtility.SetDirty(q11GO);
            Debug.Log("[StoryChapter5Builder] Beacon placed for Q11 (red, bomb/boss).");
        }

        // Beacon D: Q12 escape trigger — gold for the final escape.
        var q12GO = FindChild(ch5, "Q12_EscapeTrigger");
        if (q12GO != null)
        {
            var beaconD = GetOrAdd<QuestBeacon>(q12GO);
            beaconD.showOnQuest = q12;
            beaconD.showOnChapter = 0;
            beaconD.beamColor = new Color(1f, 0.85f, 0.3f, 0.6f);
            beaconD.iconColor = new Color(1f, 0.9f, 0.4f, 1f);
            beaconD.ringColor = new Color(1f, 0.85f, 0.3f, 0.7f);
            beaconD.beamHeight = 18f;
            beaconD.hideDistance = 3f;
            EditorUtility.SetDirty(q12GO);
            Debug.Log("[StoryChapter5Builder] Beacon placed for Q12 (gold, escape).");
        }
    }

    private static void PlaceAmmo(GameObject ch5, Vector3 localPos, int index)
    {
        var name = $"Boss Fight Ammo (Ch5) {index:00}";
        var existing = FindChild(ch5, name);
        if (existing != null)
        {
            existing.transform.localPosition = localPos;
            return;
        }

        var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/Bullet Pickeable.prefab");
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[StoryChapter5Builder] Bullet Pickeable prefab not found.");
            return;
        }

        var ammo = (GameObject)PrefabUtility.InstantiatePrefab(bulletPrefab, ch5.scene);
        ammo.name = name;
        ammo.transform.SetParent(ch5.transform, true);
        ammo.transform.localPosition = localPos;
        Debug.Log($"[StoryChapter5Builder] {name} placed at local {localPos}.");
    }

    private static void PlaceHealth(GameObject ch5, Vector3 localPos, int index)
    {
        var name = $"Boss Fight Health (Ch5) {index:00}";
        var existing = FindChild(ch5, name);
        if (existing != null)
        {
            existing.transform.localPosition = localPos;
            return;
        }

        var healthPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/PowerUps/Healthpack.prefab");
        if (healthPrefab == null)
        {
            Debug.LogWarning("[StoryChapter5Builder] Healthpack prefab not found.");
            return;
        }

        var health = (GameObject)PrefabUtility.InstantiatePrefab(healthPrefab, ch5.scene);
        health.name = name;
        health.transform.SetParent(ch5.transform, true);
        health.transform.localPosition = localPos;
        Debug.Log($"[StoryChapter5Builder] {name} placed at local {localPos}.");
    }

    private static GameObject[] LoadApartmentZombiePrefabs()
    {
        // Apartment/city-themed zombies: residents, office workers, service staff.
        string[] names = {
            "Zombie_Businessman_Male_01",
            "Zombie_BusinessShirt_Male_01",
            "Zombie_Business_Female_01",
            "Zombie_Bellboy_Male_01",
            "Zombie_Coat_Female_01",
            "Zombie_Jacket_Male_01",
            "Zombie_Jacket_Female_01",
            "Zombie_Hipster_Male_01",
            "Zombie_Hipster_Female_01",
            "Zombie_Hoodie_Male_01",
            "Zombie_GamerGirl_Female_01",
            "Zombie_Father_Male_01",
            "Zombie_Mother_Female_01",
            "Zombie_Daughter_Female_01",
            "Zombie_Son_Male_01",
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
    /// Builds the boss wave prefab array: Tank (Brute) boss + N minion zombies.
    /// The Tank is the final boss; minions are tough apartment zombies to make
    /// the fight challenging but not overwhelming.
    /// </summary>
    private static GameObject[] LoadBossWavePrefabs(bool tankBoss, int minionCount)
    {
        // Minion zombies for the boss fight — tough types to pressure the player.
        string[] minionNames = {
            "Zombie_Military_Male_01",
            "Zombie_Firefighter_Male_01",
            "Zombie_RiotCop_Male_01",
            "Zombie_BioHazardSuit_Male_01",
            "Zombie_Footballer_Male_01",
            "Zombie_Jock_Male_01",
            "Zombie_Biker_Male_01",
            "Zombie_Gangster_Male_01",
        };
        var minions = LoadZombiePrefabsByNames(minionNames);
        if (minions.Length == 0)
        {
            // Fallback to apartment zombies if tough types aren't found.
            minions = LoadApartmentZombiePrefabs();
        }

        var list = new System.Collections.Generic.List<GameObject>();

        // Fill minions (cycling through the array for variety).
        for (int i = 0; i < minionCount; i++)
            list.Add(minions[i % minions.Length]);

        // Shuffle so minions are mixed in.
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }

        // Add Tank (Brute) boss at the end if requested.
        if (tankBoss)
        {
            var tank = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/OG Prefab/Boss/SM_Chr_ZombieBoss_Brute_01.prefab");
            if (tank != null)
                list.Add(tank);
            else
                Debug.LogWarning("[StoryChapter5Builder] Tank (Brute) prefab not found.");
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
}
