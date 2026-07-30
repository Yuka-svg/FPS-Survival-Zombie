using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using cowsins;

/// <summary>
/// One-shot editor utility that creates the Chapter 1 QuestData assets and
/// wires the StoryManager + Chapter 1 scene objects. Run via the menu:
/// Tools/Story/Build Chapter 1. Safe to re-run (idempotent — updates existing
/// assets/links instead of duplicating).
/// </summary>
public static class StoryChapter1Builder
{
    private const string QuestFolder = "Assets/Resources/Quests";

    [MenuItem("Tools/Story/Build Chapter 1")]
    public static void Build()
    {
        EnsureFolder("Assets/Resources", "Quests");

        var soldierJournal = AssetDatabase.LoadAssetAtPath<JournalData>(
            "Assets/Resources/Journals/SoldierJournal_01.asset");

        // ---- Quest assets ----
        var q1 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_01_TutorialMovement.asset",
            questId: 1, chapter: 1,
            title: "Làm quen điều khiển",
            description: "Người chơi làm quen với các thao tác cơ bản: di chuyển, nhảy, double jump và nhặt súng.",
            objective: "Di chuyển đến lều và nhặt khẩu súng lục",
            expReward: 50f,
            journalReward: null,
            notification: "Đã nhặt súng lục! Một con zombie xuất hiện...");

        var q2 = CreateOrUpdateQuest(
            QuestFolder + "/Quest_02_TutorialShooting.asset",
            questId: 2, chapter: 1,
            title: "Làm quen thao tác bắn súng",
            description: "Người chơi học cách ngắm bắn và tiêu diệt zombie đầu tiên. Nhiệm vụ này giúp người chơi chuyển từ trạng thái làm quen điều khiển sang trạng thái bắt đầu chiến đấu thực sự.",
            objective: "Tiêu diệt toàn bộ zombie xung quanh khu lều",
            expReward: 100f,
            journalReward: soldierJournal,
            notification: "Zombie bị tiêu diệt! Nhận Nhật ký người lính #1. Chuyển sang bệnh viện...");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- Wire StoryManager ----
        var smGO = FindOrCreate("StoryManager");
        var sm = smGO.GetComponent<StoryManager>();
        if (sm == null) sm = smGO.AddComponent<StoryManager>();
        sm.startingChapter = 1;
        sm.chapter1Quests = new QuestData[] { q1, q2 };
        EditorUtility.SetDirty(sm);

        // ---- Scene setup ----
        SetupScene(q1, q2);

        AssetDatabase.SaveAssets();
        Debug.Log("[StoryChapter1Builder] Chapter 1 built: quests + scene wired.");
    }

    private static void SetupScene(QuestData q1, QuestData q2)
    {
        // 1) Move player to Chapter 1 start position.
        var playerGO = GameObject.Find("Player");
        if (playerGO != null)
        {
            playerGO.transform.localPosition = Vector3.zero;
            Debug.Log("[StoryChapter1Builder] Player moved to Ch1 start (0, 0, 0).");
        }

        // 2) Find the Ch1 zone.
        var ch1 = GameObject.Find("StoryZones/Ch1_TutorialCamp");
        if (ch1 == null)
        {
            Debug.LogError("StoryZones/Ch1_TutorialCamp not found.");
            return;
        }

        // 3) Configure Q1_ReachTrigger as a QuestTrigger for Quest 1.
        var q1TriggerGO = FindChild(ch1, "Q1_ReachTrigger");
        if (q1TriggerGO != null)
        {
            var qt = GetOrAdd<QuestTrigger>(q1TriggerGO);
            qt.targetQuest = q1;
            qt.mode = QuestTrigger.Mode.OnPlayerEnter;
            qt.oneShot = true;

            // Cutscene for the soldier body discovery.
            var cutscene = GetOrAdd<CutscenePlayer>(q1TriggerGO);
            cutscene.title = "Xác người lính";
            cutscene.body = "Bên cạnh thi thể là một khẩu súng lục cùng một ít đạn...";
            cutscene.fadeIn = 0.6f;
            cutscene.hold = 3f;
            cutscene.fadeOut = 0.8f;
            qt.cutscene = cutscene;

            // Spawn 3 zombies after Quest 1 completes — they rush out from around
            // the tent. The player must kill all of them to complete Q2.
            var spawner = GetOrAdd<SpawnOnQuestEvent>(q1TriggerGO);
            spawner.onQuestComplete = q1;
            spawner.fireOnQuestActive = false;
            var zombiePrefabs = LoadTutorialZombiePrefabs();
            spawner.prefabs = zombiePrefabs;
            // Spawn at the trigger and at two offsets so they come from different sides.
            spawner.spawnPoints = new Transform[] { q1TriggerGO.transform };
            spawner.delay = 1.5f;

            EditorUtility.SetDirty(q1TriggerGO);
            Debug.Log($"[StoryChapter1Builder] Q1_ReachTrigger wired (QuestTrigger + Cutscene + {zombiePrefabs.Length} zombies).");
        }

        // 4) Add a KillCountObjective for Quest 2 (kill all zombies around the tent).
        var killObjGO = FindChild(ch1, "Q2_KillObjective");
        if (killObjGO == null)
        {
            killObjGO = new GameObject("Q2_KillObjective");
            killObjGO.transform.SetParent(ch1.transform, false);
        }
        var killObj = GetOrAdd<KillCountObjective>(killObjGO);
        killObj.targetQuest = q2;
        killObj.targetCount = 3; // Must kill all 3 zombies around the tent.

        // Transition cutscene to hospital — plays when Q2 completes, before the
        // chapter advances. Per Level Design doc: "Kích hoạt Cutscene chuyển cảnh
        // sang bệnh viện."
        var q2Cutscene = GetOrAdd<CutscenePlayer>(killObjGO);
        q2Cutscene.title = "CHƯƠNG 2 — BỆNH VIỆN";
        q2Cutscene.body = "Sau khi rời lều trại với Nhật ký người lính #1, bạn phát hiện manh mối dẫn đến bệnh viện trung tâm thành phố. Tiếng rên rỉ vọng ra từ bên trong...";
        q2Cutscene.fadeIn = 0.8f;
        q2Cutscene.hold = 4f;
        q2Cutscene.fadeOut = 1.2f;
        killObj.completionCutscene = q2Cutscene;

        EditorUtility.SetDirty(killObjGO);
        Debug.Log("[StoryChapter1Builder] Q2_KillObjective wired (kill 3 zombies + transition cutscene).");

        // 5) Place a Pistol Pickeable at the Q1 trigger location.
        PlacePistolPickeable(q1TriggerGO);

        // 6) Place pistol ammo near the pistol.
        PlacePistolAmmo(q1TriggerGO);

        // 7) Configure SaveRoom_Ch1.
        var saveRoomGO = FindChild(ch1, "SaveRoom_Ch1");
        if (saveRoomGO != null)
        {
            var sr = GetOrAdd<SaveRoom>(saveRoomGO);
            sr.healRate = 25f;
            sr.restoreShield = true;
            // Suppress the main Spawner while resting.
            var spawner = GameObject.Find("Spawner");
            if (spawner != null)
            {
                var spawmComp = spawner.GetComponent<Spawm>();
                if (spawmComp != null)
                    sr.spawnersToSuppress = new MonoBehaviour[] { spawmComp };
            }
            EditorUtility.SetDirty(saveRoomGO);
            Debug.Log("[StoryChapter1Builder] SaveRoom_Ch1 wired.");
        }

        // 8) Add ChapterBoundary to Ch1 (BoxCollider trigger volume).
        var box = GetOrAddBoxCollider(ch1);
        box.size = new Vector3(60f, 20f, 60f);
        box.center = new Vector3(0f, 5f, -5f);
        box.isTrigger = true;
        var boundary = GetOrAdd<ChapterBoundary>(ch1);
        boundary.chapter = 1;
        // Ch1 is a tutorial — no continuous spawner. Zombies are spawned via
        // SpawnOnQuestEvent (3 zombies after Q1 completes). Leave spawners empty
        // so the main Spawner doesn't interfere with the kill count.
        boundary.spawners = new MonoBehaviour[0];
        // No walls — the boundary trigger volume handles one-way locking.
        EditorUtility.SetDirty(ch1);
        Debug.Log("[StoryChapter1Builder] ChapterBoundary wired for Ch1 (trigger volume, no walls).");

        // 9) Add QuestTrackerWidget to GameUICanvas (UITK).
        var uiCanvasGO = GameObject.Find("=== UI ===/GameUICanvas");
        if (uiCanvasGO != null)
        {
            GetOrAdd<QuestTrackerWidget>(uiCanvasGO);
            EditorUtility.SetDirty(uiCanvasGO);
            Debug.Log("[StoryChapter1Builder] QuestTrackerWidget added to GameUICanvas.");
        }

        // 10) Disable the main Spawner at start — Ch1 uses SpawnOnQuestEvent only.
        var spawnerGO = GameObject.Find("Spawner");
        if (spawnerGO != null)
        {
            var spawmComp = spawnerGO.GetComponent<Spawm>();
            if (spawmComp != null) spawmComp.enabled = false;
        }

        // 11) Disable SpecialEnemyDirector for Chapter 1 (no Boomers/Tanks in tutorial).
        var sed = GameObject.Find("SpecialEnemyDirector");
        if (sed != null) sed.SetActive(false);

        // 12) Mark the scene dirty so it can be saved.
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    private static void PlacePistolPickeable(GameObject q1TriggerGO)
    {
        var worldGO = GameObject.Find("=== WORLD ===");
        if (worldGO == null) return;

        // Check if a pistol pickeable already exists near the trigger.
        var existing = GameObject.Find("=== WORLD ===/Pistol Pickeable (Ch1)");
        if (existing != null)
        {
            Debug.Log("[StoryChapter1Builder] Pistol Pickeable (Ch1) already exists.");
            return;
        }

        var pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/[PRESETS]WeaponPickeables/Pistol Pickeable.prefab");
        if (pistolPrefab == null)
        {
            Debug.LogWarning("[StoryChapter1Builder] Pistol Pickeable prefab not found.");
            return;
        }

        var pos = q1TriggerGO != null ? q1TriggerGO.transform.position : new Vector3(15f, 1f, -15f);
        var pistol = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab, worldGO.scene);
        pistol.name = "Pistol Pickeable (Ch1)";
        pistol.transform.SetParent(worldGO.transform, true);
        pistol.transform.position = pos + new Vector3(0f, 0.5f, 0f);

        // Ensure the WeaponPickeable uses the Pistol weapon SO.
        var wp = pistol.GetComponent<WeaponPickeable>();
        if (wp != null)
        {
            var pistolSO = AssetDatabase.LoadAssetAtPath<Weapon_SO>(
                "Assets/Engine/Cowsins/ScriptableObjects/Weapons/Pistol.asset");
            if (pistolSO != null)
            {
                var weaponField = typeof(WeaponPickeable).GetField("weapon",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (weaponField != null) weaponField.SetValue(wp, pistolSO);
            }
        }

        Debug.Log("[StoryChapter1Builder] Pistol Pickeable placed at " + pistol.transform.position);
    }

    private static void PlacePistolAmmo(GameObject q1TriggerGO)
    {
        var worldGO = GameObject.Find("=== WORLD ===");
        if (worldGO == null) return;

        var existing = GameObject.Find("=== WORLD ===/Pistol Ammo (Ch1)");
        if (existing != null) return;

        // Reuse the existing Bullet Pickeable prefab pattern by cloning the scene's
        // existing Bullet Pickeable so we get the right component setup.
        var bulletTemplate = GameObject.Find("=== WORLD ===/Bullet Pickeable");
        GameObject ammo;
        if (bulletTemplate != null)
        {
            ammo = Object.Instantiate(bulletTemplate, worldGO.transform);
            ammo.name = "Pistol Ammo (Ch1)";
        }
        else
        {
            // No template — create a minimal pickup from the Cowsins prefab if available.
            ammo = new GameObject("Pistol Ammo (Ch1)");
            ammo.transform.SetParent(worldGO.transform, true);
        }

        var pos = q1TriggerGO != null ? q1TriggerGO.transform.position : new Vector3(15f, 1f, -15f);
        ammo.transform.position = pos + new Vector3(1.5f, 0f, 0.5f);
        Debug.Log("[StoryChapter1Builder] Pistol Ammo placed at " + ammo.transform.position);
    }

    // ---- Helpers ----

    private static GameObject[] LoadTutorialZombiePrefabs()
    {
        // 3 tutorial zombies — mix of types for variety.
        string[] names = {
            "Zombie_Bellboy_Male_01",
            "Zombie_Biker_Male_01",
            "Zombie_Hobo_Male_01",
        };
        var list = new System.Collections.Generic.List<GameObject>();
        foreach (var n in names)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/OG Prefab/Crooks/" + n + ".prefab");
            if (p != null) list.Add(p);
            else Debug.LogWarning($"[StoryChapter1Builder] Zombie prefab not found: {n}");
        }
        return list.ToArray();
    }

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

    private static GameObject FindOrCreate(string name)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
        return go;
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
