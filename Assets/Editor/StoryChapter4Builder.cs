using UnityEditor;
using UnityEngine;
using cowsins;

/// <summary>
/// One-shot editor utility that creates the Chapter 4 QuestData assets and
/// wires the StoryManager + Chapter 4 scene objects (Residential area).
/// Run via the menu: Tools/Story/Build Chapter 4. Safe to re-run (idempotent —
/// updates existing assets/links instead of duplicating).
///
/// Chapter 4 — Khu dân cư (Residential):
///   Quest 8: Đến khu dân cư — reach the residential area entrance (save room).
///   Quest 9: Thu thập nhật ký — collect all 6 journals/records scattered
///            across the residential area to learn more about the brother's
///            whereabouts before advancing to Chapter 5 (the apartment).
/// </summary>
public static class StoryChapter4Builder
{
    private const string QuestFolder = "Assets/Resources/Quests";
    private const string JournalFolder = "Assets/Resources/Journals";

    [MenuItem("Tools/Story/Build Chapter 4")]
    public static void Build()
    {
        EnsureFolder("Assets/Resources", "Quests");

        // ---- Journal rewards ----
        var militaryRecord03 = AssetDatabase.LoadAssetAtPath<JournalData>(
            JournalFolder + "/MilitaryRecord_03.asset");
        var brotherJournal02 = AssetDatabase.LoadAssetAtPath<JournalData>(
            JournalFolder + "/BrotherJournal_02.asset");

        // ---- Quest assets ----
        var q8 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_08_ReachResidential.asset",
            questId: 8, chapter: 4,
            title: "Đến khu dân cư",
            description: "Qua công trường là khu dân cư im ắng. Nhà cửa đóng kín, không một bóng người... nhưng không có nghĩa là an toàn. Tìm phòng an toàn để nghỉ ngơi trước khi khám phá khu vực.",
            objective: "Đi đến khu dân cư và tìm phòng an toàn",
            expReward: 150f,
            journalReward: militaryRecord03,
            notification: "Đã đến khu dân cư! Nhận Hồ sơ quân sự #3. Xung quanh có nhiều zombie — cẩn thận.");

        var q9 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_09_CollectJournals.asset",
            questId: 9, chapter: 4,
            title: "Thu thập nhật ký",
            description: "Tìm và thu thập đủ 6 cuốn nhật ký/hồ sơ trong khu dân cư: nhật ký anh trai, bác sĩ, người lính và 3 nhật ký hàng xóm. Những manh mối này sẽ chỉ đường đến chung cư — nơi anh trai đang ẩn náu.",
            objective: "Thu thập đủ 6 nhật ký/hồ sơ trong khu dân cư",
            expReward: 300f,
            journalReward: brotherJournal02,
            notification: "Đã thu thập đầy đủ nhật ký! Nhận Nhật ký anh trai #2. Mở khóa chung cư — nơi cuối cùng.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- Wire StoryManager ----
        var smGO = GameObject.Find("StoryManager");
        if (smGO == null)
        {
            Debug.LogError("[StoryChapter4Builder] StoryManager not found in scene.");
            return;
        }
        var sm = smGO.GetComponent<StoryManager>();
        if (sm == null)
        {
            Debug.LogError("[StoryChapter4Builder] StoryManager component missing.");
            return;
        }
        sm.chapter4Quests = new QuestData[] { q8, q9 };
        EditorUtility.SetDirty(sm);

        // ---- Scene setup ----
        SetupScene(q8, q9);

        AssetDatabase.SaveAssets();
        Debug.Log("[StoryChapter4Builder] Chapter 4 built: quests + scene wired.");
    }

    private static void SetupScene(QuestData q8, QuestData q9)
    {
        // 1) Find the Ch4 zone.
        var ch4 = GameObject.Find("=== WORLD ===/StoryZones/Ch4_Residential");
        if (ch4 == null)
        {
            Debug.LogError("[StoryChapter4Builder] StoryZones/Ch4_Residential not found.");
            return;
        }
        Debug.Log($"[StoryChapter4Builder] Ch4_Residential at {ch4.transform.position}.");

        // 2) Create / configure a Ch4 spawner (Spawm) for the residential area.
        var spawnerGO = FindChild(ch4, "Ch4_Spawner");
        if (spawnerGO == null)
        {
            spawnerGO = new GameObject("Ch4_Spawner");
            spawnerGO.transform.SetParent(ch4.transform, false);
        }
        spawnerGO.transform.localPosition = new Vector3(0f, 0f, 0f);
        var spawm = GetOrAdd<Spawm>(spawnerGO);
        spawm.maxZombie = 25;
        spawm.spawnInterval = 2.0f;
        spawm.spawnAreaSize = new Vector3(80f, 0f, 200f);
        spawm.minDistanceFromPlayer = 8f;
        spawm.poolSize = 50;
        spawm.zombiePrefabs = LoadResidentialZombiePrefabs();
        spawm.enabled = false; // ChapterBoundary enables on enter.
        EditorUtility.SetDirty(spawnerGO);
        Debug.Log("[StoryChapter4Builder] Ch4_Spawner wired (maxZombie=25, pool=50).");

        // 3) Wire Q8_ReachSaveRoom — reach trigger at the Ch4 entrance.
        var q8TriggerGO = FindChild(ch4, "Q8_ReachSaveRoom");
        if (q8TriggerGO == null)
        {
            Debug.LogError("[StoryChapter4Builder] Q8_ReachSaveRoom not found.");
            return;
        }
        var q8Box = q8TriggerGO.GetComponent<BoxCollider>();
        if (q8Box != null)
        {
            // Q8_ReachSaveRoom is at local (16,1,5.41); SaveRoom_Ch4 is at local
            // (10.72,1,4.24). The trigger must cover BOTH so the player completes
            // Q8 by entering the save room. Center the box between them and size
            // it generously.
            q8Box.center = new Vector3(-0.5f, 0f, 0.1f);
            q8Box.size = new Vector3(22f, 5f, 14f);
            q8Box.isTrigger = true;
        }
        q8TriggerGO.layer = 2; // Ignore Raycast

        var q8Trigger = GetOrAdd<QuestTrigger>(q8TriggerGO);
        q8Trigger.targetQuest = q8;
        q8Trigger.mode = QuestTrigger.Mode.OnPlayerEnter;
        q8Trigger.oneShot = true;

        // Brief intro cutscene when entering the residential area.
        var q8Cutscene = GetOrAdd<CutscenePlayer>(q8TriggerGO);
        q8Cutscene.title = "Khu dân cư";
        q8Cutscene.body = "Nhà cửa đóng kín, không một bóng người. Im ắng đến mức đáng sợ... nhưng không có nghĩa là an toàn.";
        q8Cutscene.fadeIn = 0.5f;
        q8Cutscene.hold = 3f;
        q8Cutscene.fadeOut = 0.8f;
        q8Trigger.cutscene = q8Cutscene;
        EditorUtility.SetDirty(q8TriggerGO);
        Debug.Log("[StoryChapter4Builder] Q8_ReachSaveRoom wired (reach + intro cutscene).");

        // 4) Create Q9_CollectibleObjective — completes Q9 when every Ch4
        //    collectible (journal) has been picked up by the player.
        var q9ObjGO = FindChild(ch4, "Q9_CollectibleObjective");
        if (q9ObjGO == null)
        {
            q9ObjGO = new GameObject("Q9_CollectibleObjective");
            q9ObjGO.transform.SetParent(ch4.transform, false);
        }
        q9ObjGO.transform.localPosition = Vector3.zero;

        // Gather every Collectible under Ch4 (the journals the player must find).
        var required = new System.Collections.Generic.List<Collectible>();
        for (int i = 0; i < ch4.transform.childCount; i++)
        {
            var col = ch4.transform.GetChild(i).GetComponent<Collectible>();
            if (col != null) required.Add(col);
        }

        var collectObj = GetOrAdd<CollectibleQuestObjective>(q9ObjGO);
        collectObj.targetQuest = q9;
        collectObj.requiredCollectibles = required.ToArray();

        // Cutscene played once all journals are collected (story beat).
        var q9Cutscene = GetOrAdd<CutscenePlayer>(q9ObjGO);
        q9Cutscene.title = "Manh mối cuối cùng";
        q9Cutscene.body = "Từ những nhật ký này, bạn biết anh trai đang ẩn náu ở chung cư nearby. Phải đến đó trước khi quá muộn.";
        q9Cutscene.fadeIn = 0.6f;
        q9Cutscene.hold = 4f;
        q9Cutscene.fadeOut = 1.0f;
        collectObj.completionCutscene = q9Cutscene;
        EditorUtility.SetDirty(q9ObjGO);
        Debug.Log($"[StoryChapter4Builder] Q9_CollectibleObjective wired (need {required.Count} collectibles).");

        // 5) Wire ChapterBoundary for Ch4.
        var boundary = ch4.GetComponent<ChapterBoundary>();
        if (boundary != null)
        {
            boundary.chapter = 4;
            boundary.spawners = new MonoBehaviour[] { spawm };
            EditorUtility.SetDirty(ch4);
            Debug.Log("[StoryChapter4Builder] ChapterBoundary wired for Ch4.");
        }

        // 6) Configure SaveRoom_Ch4 (suppress Ch4 spawner while resting).
        var saveRoomGO = FindChild(ch4, "SaveRoom_Ch4");
        if (saveRoomGO != null)
        {
            var sr = saveRoomGO.GetComponent<SaveRoom>();
            if (sr == null) sr = saveRoomGO.AddComponent<SaveRoom>();
            sr.healRate = 25f;
            sr.restoreShield = true;
            sr.spawnersToSuppress = new MonoBehaviour[] { spawm };
            sr.chapterTransitionOnEnter = 4; // Play "CHƯƠNG 4" cutscene on first enter.
            EditorUtility.SetDirty(saveRoomGO);
            Debug.Log("[StoryChapter4Builder] SaveRoom_Ch4 wired (heal + suppress Ch4_Spawner + chapter transition).");
        }

        // 7) Place QuestBeacons to guide the player through Ch4.
        PlaceBeacons(ch4, q8, q9, saveRoomGO);

        // 8) Mark the scene dirty so it can be saved.
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    private static void PlaceBeacons(GameObject ch4, QuestData q8, QuestData q9, GameObject saveRoomGO)
    {
        // Beacon A: SaveRoom_Ch4 — blue chapter guide beacon.
        if (saveRoomGO != null)
        {
            var beaconA = GetOrAdd<QuestBeacon>(saveRoomGO);
            beaconA.showOnQuest = null;
            beaconA.showOnChapter = 4;
            beaconA.beamColor = new Color(0.3f, 0.85f, 1f, 0.5f);
            beaconA.iconColor = new Color(0.4f, 0.9f, 1f, 1f);
            beaconA.ringColor = new Color(0.3f, 0.85f, 1f, 0.6f);
            beaconA.beamHeight = 14f;
            beaconA.hideDistance = 3f;
            EditorUtility.SetDirty(saveRoomGO);
            Debug.Log("[StoryChapter4Builder] Beacon placed on SaveRoom_Ch4 (blue, chapter guide).");
        }

        // Beacon B: Q8 reach trigger — gold for first objective.
        var q8TriggerGO = FindChild(ch4, "Q8_ReachSaveRoom");
        if (q8TriggerGO != null)
        {
            var beaconB = GetOrAdd<QuestBeacon>(q8TriggerGO);
            beaconB.showOnQuest = q8;
            beaconB.showOnChapter = 0;
            beaconB.beamColor = new Color(1f, 0.85f, 0.3f, 0.5f);
            beaconB.iconColor = new Color(1f, 0.9f, 0.4f, 1f);
            beaconB.ringColor = new Color(1f, 0.85f, 0.3f, 0.6f);
            beaconB.beamHeight = 12f;
            beaconB.hideDistance = 3f;
            EditorUtility.SetDirty(q8TriggerGO);
            Debug.Log("[StoryChapter4Builder] Beacon placed for Q8 (gold, reach trigger).");
        }

        // Beacon C: every Ch4 collectible — green beams mark exploration targets
        // while Q9 is active.
        for (int i = 0; i < ch4.transform.childCount; i++)
        {
            var child = ch4.transform.GetChild(i);
            var col = child.GetComponent<Collectible>();
            if (col == null) continue;

            var beacon = GetOrAdd<QuestBeacon>(child.gameObject);
            beacon.showOnQuest = q9;
            beacon.showOnChapter = 0;
            beacon.beamColor = new Color(0.4f, 1f, 0.5f, 0.5f); // Green for exploration.
            beacon.iconColor = new Color(0.5f, 1f, 0.6f, 1f);
            beacon.ringColor = new Color(0.4f, 1f, 0.5f, 0.6f);
            beacon.beamHeight = 10f;
            beacon.hideDistance = 3f;
            EditorUtility.SetDirty(child.gameObject);
            Debug.Log($"[StoryChapter4Builder] Beacon placed on {child.name} (green, Q9 collectible).");
        }
    }

    private static GameObject[] LoadResidentialZombiePrefabs()
    {
        // Residential-themed zombies: families, neighbors, everyday people.
        string[] names = {
            "Zombie_Mother_Female_01",
            "Zombie_Father_Male_01",
            "Zombie_Grandma_Female_01",
            "Zombie_Grandpa_Male_01",
            "Zombie_Daughter_Female_01",
            "Zombie_Son_Male_01",
            "Zombie_SchoolBoy_Male_01",
            "Zombie_SchoolGirl_Female_01",
            "Zombie_Hoodie_Male_01",
            "Zombie_Jacket_Male_01",
            "Zombie_Hipster_Male_01",
            "Zombie_Hipster_Female_01",
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
