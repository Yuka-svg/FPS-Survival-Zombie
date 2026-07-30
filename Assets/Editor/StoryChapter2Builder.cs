using UnityEditor;
using UnityEngine;
using cowsins;

/// <summary>
/// One-shot editor utility that creates the Chapter 2 QuestData assets and
/// wires the StoryManager + Chapter 2 scene objects (Hospital area).
/// Run via the menu: Tools/Story/Build Chapter 2. Safe to re-run (idempotent —
/// updates existing assets/links instead of duplicating).
///
/// Chapter 2 — Bệnh viện (Hospital):
///   Quest 3: Dọn dẹp — clear zombies outside the hospital (kill-count objective).
///   Quest 4: Tìm kiếm bệnh nhân — explore the buildings, defeat a Boomer, and
///            collect all 5 journals/records in the hospital to complete the quest
///            (collectible-count objective).
/// </summary>
public static class StoryChapter2Builder
{
    private const string QuestFolder = "Assets/Resources/Quests";
    private const string JournalFolder = "Assets/Resources/Journals";

    [MenuItem("Tools/Story/Build Chapter 2")]
    public static void Build()
    {
        EnsureFolder("Assets/Resources", "Quests");

        // ---- Journal rewards ----
        var expReport01 = AssetDatabase.LoadAssetAtPath<JournalData>(
            JournalFolder + "/ExperimentReport_01.asset");
        var expReportFull = AssetDatabase.LoadAssetAtPath<JournalData>(
            JournalFolder + "/ExperimentReport_Full_01.asset");

        // ---- Quest assets ----
        var q3 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_03_ClearExterior.asset",
            questId: 3, chapter: 2,
            title: "Dọn dẹp",
            description: "Làm quen với cơ chế Wave Zombie. Tiêu diệt toàn bộ zombie xuất hiện bên ngoài bệnh viện trước khi được bước vào bên trong.",
            objective: "Tiêu diệt toàn bộ zombie bên ngoài bệnh viện",
            expReward: 150f,
            journalReward: expReport01,
            notification: "Khu vực bên ngoài đã an toàn! Nhận Báo cáo thí nghiệm #1.");

        var q4 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_04_SearchPatients.asset",
            questId: 4, chapter: 2,
            title: "Tìm kiếm bệnh nhân",
            description: "Tìm kiếm hồ sơ bệnh nhân thử nghiệm trong bệnh viện. Đi qua từng tòa nhà, xử lý đối thủ và thu thập đủ 5 cuốn nhật ký/hồ sơ còn thiếu. Tòa nhà 2 có Boomer và SMG.",
            objective: "Thu thập đủ 5 nhật ký/hồ sơ trong bệnh viện",
            expReward: 250f,
            journalReward: expReportFull,
            notification: "Đã thu thập đầy đủ hồ sơ! Nhận Báo cáo thí nghiệm hoàn chỉnh. Mở khóa khu vực tiếp theo.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- Wire StoryManager ----
        var smGO = GameObject.Find("StoryManager");
        if (smGO == null)
        {
            Debug.LogError("[StoryChapter2Builder] StoryManager not found in scene.");
            return;
        }
        var sm = smGO.GetComponent<StoryManager>();
        if (sm == null)
        {
            Debug.LogError("[StoryChapter2Builder] StoryManager component missing.");
            return;
        }
        sm.chapter2Quests = new QuestData[] { q3, q4 };
        EditorUtility.SetDirty(sm);

        // ---- Scene setup ----
        SetupScene(q3, q4);

        AssetDatabase.SaveAssets();
        Debug.Log("[StoryChapter2Builder] Chapter 2 built: quests + scene wired.");
    }

    private static void SetupScene(QuestData q3, QuestData q4)
    {
        // 1) Find the Ch2 zone.
        var ch2 = GameObject.Find("=== WORLD ===/StoryZones/Ch2_Hospital");
        if (ch2 == null)
        {
            Debug.LogError("[StoryChapter2Builder] StoryZones/Ch2_Hospital not found.");
            return;
        }
        Debug.Log($"[StoryChapter2Builder] Ch2_Hospital at {ch2.transform.position}.");

        // 2) Create / configure a Ch2 spawner (Spawm) for the hospital area.
        var spawnerGO = FindChild(ch2, "Ch2_Spawner");
        if (spawnerGO == null)
        {
            spawnerGO = new GameObject("Ch2_Spawner");
            spawnerGO.transform.SetParent(ch2.transform, false);
        }
        // Center the spawner in the hospital area (slightly toward the entrance).
        spawnerGO.transform.localPosition = new Vector3(-6f, 0f, 8f);
        var spawm = GetOrAdd<Spawm>(spawnerGO);
        spawm.maxZombie = 20;
        spawm.spawnInterval = 2.5f;
        spawm.spawnAreaSize = new Vector3(50f, 0f, 40f);
        spawm.minDistanceFromPlayer = 8f;
        spawm.poolSize = 40;
        // Use a subset of zombie prefabs (hospital-themed).
        spawm.zombiePrefabs = LoadHospitalZombiePrefabs();
        spawm.enabled = false; // ChapterBoundary enables on enter.
        EditorUtility.SetDirty(spawnerGO);
        Debug.Log("[StoryChapter2Builder] Ch2_Spawner wired.");

        // 3) Create Q3_KillObjective (kill N zombies outside).
        var q3ObjGO = FindChild(ch2, "Q3_KillObjective");
        if (q3ObjGO == null)
        {
            q3ObjGO = new GameObject("Q3_KillObjective");
            q3ObjGO.transform.SetParent(ch2.transform, false);
        }
        q3ObjGO.transform.localPosition = new Vector3(-6f, 0f, 8f);
        var killObj = GetOrAdd<KillCountObjective>(q3ObjGO);
        killObj.targetQuest = q3;
        killObj.targetCount = 5; // Player-friendly: 5 instead of 8.
        EditorUtility.SetDirty(q3ObjGO);
        Debug.Log("[StoryChapter2Builder] Q3_KillObjective wired (kill 5 zombies).");

        // 4) Create Q4_Building2_BoomerTrigger — spawns a Boomer when the player
        //    enters Building 2 (where Experiment Report #2 + SMG are).
        var boomerTriggerGO = FindChild(ch2, "Q4_Building2_BoomerTrigger");
        if (boomerTriggerGO == null)
        {
            boomerTriggerGO = new GameObject("Q4_Building2_BoomerTrigger");
            boomerTriggerGO.transform.SetParent(ch2.transform, false);
        }
        boomerTriggerGO.transform.localPosition = new Vector3(-10f, 1f, 8f); // Building 2 area
        var boomerBox = GetOrAddBoxCollider(boomerTriggerGO);
        boomerBox.size = new Vector3(12f, 4f, 12f);
        boomerBox.isTrigger = true;
        boomerTriggerGO.layer = 2; // Ignore Raycast (so it doesn't block bullets)

        var boomerSpawn = GetOrAdd<SpawnOnPlayerEnter>(boomerTriggerGO);
        boomerSpawn.prefab = LoadBoomerPrefab();
        boomerSpawn.spawnOffset = new Vector3(0f, 0f, -8f);
        boomerSpawn.oneShot = true;

        // Warning cutscene before the Boomer spawns — gives the player a heads-up.
        var boomerCutscene = GetOrAdd<CutscenePlayer>(boomerTriggerGO);
        boomerCutscene.title = "CẢNH BÁO";
        boomerCutscene.body = "Tiếng gầm vang lên từ bên trong tòa nhà... Một con Boomer!";
        boomerCutscene.fadeIn = 0.4f;
        boomerCutscene.hold = 2.5f;
        boomerCutscene.fadeOut = 0.6f;
        boomerSpawn.cutscene = boomerCutscene;
        boomerSpawn.delayAfterCutscene = 0.5f;
        EditorUtility.SetDirty(boomerTriggerGO);
        Debug.Log("[StoryChapter2Builder] Q4_Building2_BoomerTrigger wired (Boomer + warning cutscene).");

        // 5) Place SMG Pickeable in Building 2.
        PlaceSMGPickeable(ch2, new Vector3(-10f, 1f, 10f));

        // 6) Create Q4_CollectibleObjective — completes Q4 when every Ch2
        //    collectible (journal) has been picked up by the player.
        var q4ObjGO = FindChild(ch2, "Q4_CollectibleObjective");
        if (q4ObjGO == null)
        {
            q4ObjGO = new GameObject("Q4_CollectibleObjective");
            q4ObjGO.transform.SetParent(ch2.transform, false);
        }
        q4ObjGO.transform.localPosition = Vector3.zero;

        // Gather every Collectible under Ch2 (the journals the player must find).
        var required = new System.Collections.Generic.List<Collectible>();
        for (int i = 0; i < ch2.transform.childCount; i++)
        {
            var col = ch2.transform.GetChild(i).GetComponent<Collectible>();
            if (col != null) required.Add(col);
        }

        var collectObj = GetOrAdd<CollectibleQuestObjective>(q4ObjGO);
        collectObj.targetQuest = q4;
        collectObj.requiredCollectibles = required.ToArray();

        // Cutscene played once all records are collected (story beat).
        var q4Cutscene = GetOrAdd<CutscenePlayer>(q4ObjGO);
        q4Cutscene.title = "Báo cáo thí nghiệm hoàn chỉnh";
        q4Cutscene.body = "Bạn đã thu thập đủ hồ sơ. Bệnh nhân số 001... chính là anh trai mình. Phải tìm anh ấy trước khi quá muộn.";
        q4Cutscene.fadeIn = 0.6f;
        q4Cutscene.hold = 4f;
        q4Cutscene.fadeOut = 1.0f;
        collectObj.completionCutscene = q4Cutscene;
        EditorUtility.SetDirty(q4ObjGO);
        Debug.Log($"[StoryChapter2Builder] Q4_CollectibleObjective wired (need {required.Count} collectibles).");

        // Remove the legacy Q4_FinalTrigger if it still exists in the scene.
        var legacyQ4Trigger = FindChild(ch2, "Q4_FinalTrigger");
        if (legacyQ4Trigger != null)
        {
            UnityEngine.Object.DestroyImmediate(legacyQ4Trigger);
            Debug.Log("[StoryChapter2Builder] Removed legacy Q4_FinalTrigger.");
        }

        // 7) Wire ChapterBoundary for Ch2 (spawners, trigger volume — no walls).
        var boundary = ch2.GetComponent<ChapterBoundary>();
        if (boundary != null)
        {
            boundary.chapter = 2;
            boundary.spawners = new MonoBehaviour[] { spawm };
            // No walls — the boundary trigger volume handles one-way locking.
            EditorUtility.SetDirty(ch2);
            Debug.Log("[StoryChapter2Builder] ChapterBoundary wired for Ch2 (trigger volume, no walls).");
        }

        // 8) Configure SaveRoom_Ch2 (suppress Ch2 spawner while resting).
        var saveRoomGO = FindChild(ch2, "SaveRoom_Ch2");
        if (saveRoomGO != null)
        {
            var sr = saveRoomGO.GetComponent<SaveRoom>();
            if (sr == null) sr = saveRoomGO.AddComponent<SaveRoom>();
            sr.healRate = 25f;
            sr.restoreShield = true;
            sr.spawnersToSuppress = new MonoBehaviour[] { spawm };
            sr.chapterTransitionOnEnter = 0; // Transition cutscene plays at Q2 completion (Ch1 builder).
            EditorUtility.SetDirty(saveRoomGO);
            Debug.Log("[StoryChapter2Builder] SaveRoom_Ch2 wired (heal + suppress Ch2_Spawner).");
        }

        // 9) Place QuestBeacons to guide the player through Ch2.
        PlaceBeacons(ch2, q3, q4, saveRoomGO);

        // 10) Mark the scene dirty so it can be saved.
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    private static void PlaceBeacons(GameObject ch2, QuestData q3, QuestData q4, GameObject saveRoomGO)
    {
        // Beacon A: SaveRoom_Ch2 — guides player to the save room when they first
        // enter Ch2 (before any quest is active). Uses showOnChapter=2.
        if (saveRoomGO != null)
        {
            var beaconA = GetOrAdd<QuestBeacon>(saveRoomGO);
            beaconA.showOnQuest = null;
            beaconA.showOnChapter = 2;
            beaconA.beamColor = new Color(0.3f, 0.85f, 1f, 0.5f); // Blue for save room.
            beaconA.iconColor = new Color(0.4f, 0.9f, 1f, 1f);
            beaconA.ringColor = new Color(0.3f, 0.85f, 1f, 0.6f);
            beaconA.beamHeight = 14f;
            beaconA.hideDistance = 3f;
            EditorUtility.SetDirty(saveRoomGO);
            Debug.Log("[StoryChapter2Builder] Beacon placed on SaveRoom_Ch2 (blue, chapter guide).");
        }

        // Beacon B: Q3 kill area — shows during Q3 (kill zombies outside).
        var q3ObjGO = FindChild(ch2, "Q3_KillObjective");
        if (q3ObjGO != null)
        {
            // Move the beacon to a visible spot (center of the exterior area).
            var beaconBGO = FindChild(ch2, "Q3_Beacon");
            if (beaconBGO == null)
            {
                beaconBGO = new GameObject("Q3_Beacon");
                beaconBGO.transform.SetParent(ch2.transform, false);
            }
            beaconBGO.transform.localPosition = new Vector3(-2f, 0f, 13f); // Near hospital entrance.
            var beaconB = GetOrAdd<QuestBeacon>(beaconBGO);
            beaconB.showOnQuest = q3;
            beaconB.showOnChapter = 0;
            beaconB.beamColor = new Color(1f, 0.4f, 0.3f, 0.5f); // Red for combat.
            beaconB.iconColor = new Color(1f, 0.5f, 0.4f, 1f);
            beaconB.ringColor = new Color(1f, 0.4f, 0.3f, 0.6f);
            beaconB.beamHeight = 12f;
            beaconB.hideWhenClose = false; // Always show during combat.
            EditorUtility.SetDirty(beaconBGO);
            Debug.Log("[StoryChapter2Builder] Beacon placed for Q3 (red, combat area).");
        }

        // Beacon C: every Ch2 collectible — guides the player to each journal
        // while Q4 is active. Green beams mark exploration targets.
        for (int i = 0; i < ch2.transform.childCount; i++)
        {
            var child = ch2.transform.GetChild(i);
            var col = child.GetComponent<Collectible>();
            if (col == null) continue;

            var beacon = GetOrAdd<QuestBeacon>(child.gameObject);
            beacon.showOnQuest = q4;
            beacon.showOnChapter = 0;
            beacon.beamColor = new Color(0.4f, 1f, 0.5f, 0.5f); // Green for exploration.
            beacon.iconColor = new Color(0.5f, 1f, 0.6f, 1f);
            beacon.ringColor = new Color(0.4f, 1f, 0.5f, 0.6f);
            beacon.beamHeight = 10f;
            beacon.hideDistance = 3f;
            EditorUtility.SetDirty(child.gameObject);
            Debug.Log($"[StoryChapter2Builder] Beacon placed on {child.name} (green, Q4 collectible).");
        }
    }

    private static void PlaceSMGPickeable(GameObject ch2, Vector3 localPos)
    {
        var worldGO = GameObject.Find("=== WORLD ===");
        if (worldGO == null) return;

        var existing = FindChild(ch2, "SMG Pickeable (Ch2)");
        if (existing != null)
        {
            Debug.Log("[StoryChapter2Builder] SMG Pickeable (Ch2) already exists.");
            return;
        }

        var smgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/[PRESETS]WeaponPickeables/SMG Pickeable.prefab");
        if (smgPrefab == null)
        {
            Debug.LogWarning("[StoryChapter2Builder] SMG Pickeable prefab not found.");
            return;
        }

        var smg = (GameObject)PrefabUtility.InstantiatePrefab(smgPrefab, worldGO.scene);
        smg.name = "SMG Pickeable (Ch2)";
        smg.transform.SetParent(ch2.transform, true);
        smg.transform.localPosition = localPos;

        // Ensure the WeaponPickeable uses the SMG weapon SO.
        var wp = smg.GetComponent<WeaponPickeable>();
        if (wp != null)
        {
            var smgSO = AssetDatabase.LoadAssetAtPath<Weapon_SO>(
                "Assets/Engine/Cowsins/ScriptableObjects/Weapons/SMG.asset");
            if (smgSO != null)
            {
                var weaponField = typeof(WeaponPickeable).GetField("weapon",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (weaponField != null) weaponField.SetValue(wp, smgSO);
            }
        }

        Debug.Log($"[StoryChapter2Builder] SMG Pickeable placed at local {localPos}.");
    }

    private static GameObject[] LoadHospitalZombiePrefabs()
    {
        // Hospital-themed zombies: patient, paramedic, doctor-like, businessman, etc.
        string[] names = {
            "Zombie_Patient_Female_01",
            "Zombie_Paramedic_Female_01",
            "Zombie_BioHazardSuit_Male_01",
            "Zombie_Businessman_Male_01",
            "Zombie_BusinessShirt_Male_01",
            "Zombie_Business_Female_01",
            "Zombie_ShopKeeper_Male_01",
            "Zombie_ShopKeeper_Female_01",
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

    private static GameObject LoadBoomerPrefab()
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefab/OG Prefab/Boss/SM_Chr_ZombieBoss_Slobber_01.prefab");
        if (p == null)
            Debug.LogWarning("[StoryChapter2Builder] Boomer prefab not found.");
        return p;
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
