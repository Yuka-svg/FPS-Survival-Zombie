using UnityEditor;
using UnityEngine;
using cowsins;

/// <summary>
/// One-shot editor utility that decorates the Chapter 4 Residential area with
/// story-appropriate props: 10 optional lore journals about the residents'
/// lives before the outbreak, plus ammo/health/gold pickups scattered across
/// houses, trailers, the motel, and abandoned shelters.
///
/// Run via the menu: Tools/Story/Decorate Chapter 4. Safe to re-run (idempotent
/// — updates existing assets/objects instead of duplicating).
///
/// The 10 lore journals are placed under a "Ch4_LoreJournals" sub-container so
/// they do NOT count toward Q9's required 6 collectibles (the
/// StoryChapter4Builder scans only direct children of Ch4 for Q9). They are
/// still collectible by the player and appear in the journal gallery as
/// optional lore entries.
///
/// All items are placed under a "Ch4_Items" sub-container for clean hierarchy
/// management. Only applies to Story mode (the scene containing Ch4_Residential).
/// </summary>
public static class StoryChapter4Decorator
{
    private const string JournalFolder = "Assets/Resources/Journals";

    [MenuItem("Tools/Story/Decorate Chapter 4")]
    public static void Decorate()
    {
        EnsureFolder("Assets/Resources", "Journals");

        // ---- Create 10 lore journal assets ----
        var journals = new JournalData[10];
        journals[0] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_04.asset",
            id: 21, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #4 — Cô giáo Lan",
            content: "Ngày 14 tháng 3. Hôm nay là ngày cuối cùng tôi đứng trên bục giảng. Lũ trẻ hoảng sợ, phụ huynh gọi điện liên tục hỏi khi nào mở lại trường. Hiệu trưởng bảo tạm đóng cửa hai tuần. Hai tuần... ai cũng nói hai tuần. Tôi để lại cuốn nhật ký này trong ngăn bàn, phòng khi có ai quay lại. Các em học trò của tôi, cô xin lỗi vì đã không bảo vệ được các em.");
        journals[1] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_05.asset",
            id: 22, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #5 — Chú Ba tiệm tạp hóa",
            content: "Kho đã cạn sạch từ hôm qua. Người ta giành giật nhau từng gói mì, từng lon cá. Tôi không trách họ, ai cũng sợ. Cảnh sát đến canh nhưng rồi chính cảnh sát cũng bỏ đi. Để lại mấy thùng nước kho sau quầy, ai cần thì lấy. Cửa tiệm mở cửa cuối cùng. Chúc may mắn.");
        journals[2] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_06.asset",
            id: 23, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #6 — Mẹ của hai đứa nhỏ",
            content: "Minh và Hà đã ngủ. Tôi không dám ngủ. Bên ngoài tiếng la hét chưa dứt. Tủ lạnh trống, nước cũng sắp hết. Nếu ai đọc được cuốn này, xin hãy chăm sóc hai con tôi. Minh 7 tuổi, răng cửa cửa đã lung lay. Hà mới 4, hay khóc đêm. Chúng là cả thế giới của tôi. Tôi sẽ cố sống đến cùng nhưng nếu không...");
        journals[3] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_07.asset",
            id: 24, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #7 — Học sinh tên Tuấn",
            content: "Quân đội đến rồi! Họ lập trạm kiểm dịch ở cuối phố. Mẹ bảo không được ra ngoài. Hàng rào kẽm gai chắn ngang đường. Thầy giáo nhắn trên nhóm lớp là tạm ngưng học online vì mất điện. Mình chỉ muốn ra ngoài chơi bóng với tụi bạn. Sao mọi người sợ hãi thế nhỉ? Mình 16 tuổi, mình không sợ.");
        journals[4] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_08.asset",
            id: 25, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #8 — Ông Sáu, cựu chiến binh",
            content: "Bà xã tôi đã biến từ đêm qua. Tôi trói được bà vào ghế nhưng tiếng bà kêu... tôi không chịu nổi. 78 tuổi, sống với nhau 55 năm. Tôi viết dòng này rồi sẽ ra ngoài. Đạn còn 3 viên. Xin đừng trách tôi. Cuốn nhật ký này để lại cho ai còn sống trong khu phố. Hãy cẩn thận, chúng không phải người nữa.");
        journals[5] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_09.asset",
            id: 26, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #9 — Y tá tên Hoa",
            content: "Bệnh viện quá tải từ ngày mùng 8. Tôi làm ca liên tục 36 tiếng rồi ngã gục. Bác sĩ trưởng bảo bệnh nhân bị cắn sẽ biến trong vòng 6 giờ, không có thuốc. Chúng tôi chỉ còn biết băng bó và an ủi. Tôi bỏ về nhà hôm nay, không quay lại nữa. Xin lỗi các bệnh nhân. Tôi để lại mấy hộp thuốc và bông băng trong tủ, ai cần thì lấy.");
        journals[6] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_10.asset",
            id: 27, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #10 — Thợ sửa xe tên Khang",
            content: "Chuyến xe buýt cuối cùng rời thành phố lúc 5 giờ sáng. Tôi không kịp. Xe của tôi hỏng máy từ tuần trước, không có phụ tùng. Đứa hàng xóm lái chiếc Toyota đi được nửa đường thì bị chặn ở trạm kiểm dịch. Nghe nói quân đội nổ súng vào bất cứ ai cố vượt rào. Tôi ở lại, trong xưởng này. Có đồ ăn và nước cho vài ngày.");
        journals[7] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_11.asset",
            id: 28, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #11 — Chủ quán phở",
            content: "Ngày mở quán cuối cùng: 12 tháng 3. Khách vắng dần từ hôm có tin dịch. Tôi nấu nồi phở cuối cho mấy ông hàng xóm già. Ăn xong ai cũng khóc. Tôi để lại nồi nước dùng còn đun nhỏ lửa, mấy kg thịt trong tủ đông. Ai đó nếu còn sống, cứ ăn. Không ai thu tiền nữa đâu. Quán mở cửa tự do.");
        journals[8] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_12.asset",
            id: 29, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #12 — Sinh viên năm nhất",
            content: "Trường đóng cửa, ký túc xá phong tỏa. Mình về nhà nhưng nhà cũng không còn ai. Bố mẹ đi công tác chưa về. Mình ở lại căn nhà này một mình. Điện cắt rồi, nước cũng yếu. Mình tìm thấy mấy cuốn nhật ký của hàng xóm trong thùng rác — họ bỏ lại khi bỏ chạy. Mình sẽ giữ lại, phòng khi có ai quay về tìm.");
        journals[9] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_13.asset",
            id: 30, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #13 — Người cha ở nhà cuối dãy",
            content: "Tôi nghe nói có một chàng trai trẻ đang tìm anh trai mình trong thành phố. Nếu cậu ta đến đây, cho cậu ta biết: anh trai cậu từng ở nhà số 14, đã chuyển đi từ trước khi dịch bùng. Cậu ấy để lại chìa khóa và một bức thư cho người em. Tôi giấu dưới gạch lò sưởi. Chúc cậu ấy may mắn — khu này không còn an toàn nữa.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- Scene setup: place journals + items ----
        SetupScene(journals);

        AssetDatabase.SaveAssets();
        Debug.Log("[StoryChapter4Decorator] Chapter 4 decorated: 10 lore journals + items placed.");
    }

    private static void SetupScene(JournalData[] journals)
    {
        var ch4 = GameObject.Find("=== WORLD ===/StoryZones/Ch4_Residential");
        if (ch4 == null)
        {
            Debug.LogError("[StoryChapter4Decorator] Ch4_Residential not found.");
            return;
        }

        // ---- Lore journals sub-container (NOT direct children of Ch4, so Q9
        //      does not count them as required). ----
        var loreContainer = FindChild(ch4, "Ch4_LoreJournals");
        if (loreContainer == null)
        {
            loreContainer = new GameObject("Ch4_LoreJournals");
            loreContainer.transform.SetParent(ch4.transform, false);
        }
        loreContainer.transform.localPosition = Vector3.zero;

        // ---- Items sub-container ----
        var itemsContainer = FindChild(ch4, "Ch4_Items");
        if (itemsContainer == null)
        {
            itemsContainer = new GameObject("Ch4_Items");
            itemsContainer.transform.SetParent(ch4.transform, false);
        }
        itemsContainer.transform.localPosition = Vector3.zero;

        // ---- Place 10 lore journals inside buildings ----
        PlaceLoreJournals(loreContainer, journals);

        // ---- Place ammo / health / gold across buildings & abandoned houses ----
        PlaceItems(itemsContainer);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    /// <summary>
    /// Places 10 lore journal collectibles at positions inside/near buildings
    /// in the Ch4 residential area. Each journal uses the Journal.prefab and
    /// gets a Collectible component wired to the corresponding JournalData.
    /// </summary>
    private static void PlaceLoreJournals(GameObject container, JournalData[] journals)
    {
        var journalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefab/Journal.prefab");
        if (journalPrefab == null)
        {
            Debug.LogError("[StoryChapter4Decorator] Journal.prefab not found.");
            return;
        }

        // Positions chosen to sit inside buildings (houses, trailers, motel,
        // junk shelter) across the Ch4 residential area. Y=1 to sit on floor.
        Vector3[] positions = {
            new Vector3(53f, 1f, -140f),    // House_01 (2)
            new Vector3(49f, 1f, -120f),    // House_02 (1)
            new Vector3(48f, 1f, -99f),     // House_01 (1)
            new Vector3(17f, 1f, -85f),     // House_01
            new Vector3(19f, 1f, -101f),    // House_02
            new Vector3(23f, 1f, -122f),    // House_03
            new Vector3(-43f, 1f, -88f),    // Motel
            new Vector3(58f, 1f, -191f),    // Trailer
            new Vector3(20f, 1f, -191f),    // Trailer
            new Vector3(13f, 1f, -171f),    // Junk Shelter
        };
        string[] names = {
            "LoreJournal_21_Teacher",
            "LoreJournal_22_Shopkeeper",
            "LoreJournal_23_Mother",
            "LoreJournal_24_Student",
            "LoreJournal_25_Veteran",
            "LoreJournal_26_Nurse",
            "LoreJournal_27_Mechanic",
            "LoreJournal_28_RestaurantOwner",
            "LoreJournal_29_Freshman",
            "LoreJournal_30_Father",
        };

        for (int i = 0; i < journals.Length; i++)
        {
            var existing = FindChild(container, names[i]);
            if (existing != null)
            {
                // Update journal reference if needed.
                var col = existing.GetComponent<Collectible>();
                if (col != null && col.journal != journals[i])
                {
                    col.journal = journals[i];
                    EditorUtility.SetDirty(col);
                }
                continue;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(journalPrefab, container.scene);
            go.name = names[i];
            go.transform.SetParent(container.transform, true);
            go.transform.position = positions[i];

            var collectible = go.GetComponent<Collectible>();
            if (collectible != null)
            {
                collectible.journal = journals[i];
                EditorUtility.SetDirty(collectible);
            }

            Debug.Log($"[StoryChapter4Decorator] Placed {names[i]} at {positions[i]}.");
        }
    }

    /// <summary>
    /// Places ammo (Bullet Pickeable), health (Healthpack), and gold (Coin)
    /// pickups across buildings and abandoned houses in the Ch4 area.
    /// </summary>
    private static void PlaceItems(GameObject container)
    {
        var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/Bullet Pickeable.prefab");
        var healthPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/PowerUps/Healthpack.prefab");
        var coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/Coin.prefab");

        if (bulletPrefab == null) Debug.LogWarning("[StoryChapter4Decorator] Bullet Pickeable prefab not found.");
        if (healthPrefab == null) Debug.LogWarning("[StoryChapter4Decorator] Healthpack prefab not found.");
        if (coinPrefab == null) Debug.LogWarning("[StoryChapter4Decorator] Coin prefab not found.");

        // ---- Ammo placements (8) — inside houses, trailers, near save room ----
        Vector3[] ammoPositions = {
            new Vector3(53f, 1f, -142f),    // House_01 (2)
            new Vector3(49f, 1f, -122f),    // House_02 (1)
            new Vector3(48f, 1f, -101f),    // House_01 (1)
            new Vector3(17f, 1f, -87f),     // House_01
            new Vector3(23f, 1f, -124f),    // House_03
            new Vector3(58f, 1f, -193f),    // Trailer
            new Vector3(40f, 1f, -215f),    // Trailer
            new Vector3(-43f, 1f, -90f),    // Motel
        };
        PlacePickups(container, bulletPrefab, "Ch4_Ammo_", ammoPositions);

        // ---- Health placements (6) — inside houses, quarantine tents, motel ----
        Vector3[] healthPositions = {
            new Vector3(51f, 1f, -140f),    // near House_01 (2)
            new Vector3(19f, 1f, -103f),    // near House_02
            new Vector3(48f, 1f, -97f),     // near House_01 (1)
            new Vector3(-41f, 1f, -140f),   // near Quarantine tents
            new Vector3(25f, 1f, -218f),    // near Trailer
            new Vector3(-45f, 1f, -86f),    // Motel
        };
        PlacePickups(container, healthPrefab, "Ch4_Health_", healthPositions);

        // ---- Gold placements (10) — inside houses, trailers, motel ----
        Vector3[] goldPositions = {
            new Vector3(53f, 1f, -138f),    // House_01 (2)
            new Vector3(49f, 1f, -118f),    // House_02 (1)
            new Vector3(48f, 1f, -96f),     // House_01 (1)
            new Vector3(17f, 1f, -83f),     // House_01
            new Vector3(19f, 1f, -99f),     // House_02
            new Vector3(23f, 1f, -120f),    // House_03
            new Vector3(58f, 1f, -189f),    // Trailer
            new Vector3(20f, 1f, -189f),    // Trailer
            new Vector3(40f, 1f, -213f),    // Trailer
            new Vector3(-45f, 1f, -84f),    // Motel
        };
        PlacePickups(container, coinPrefab, "Ch4_Gold_", goldPositions);
    }

    private static void PlacePickups(GameObject container, GameObject prefab, string namePrefix, Vector3[] positions)
    {
        if (prefab == null) return;

        for (int i = 0; i < positions.Length; i++)
        {
            var name = $"{namePrefix}{i + 1:00}";
            var existing = FindChild(container, name);
            if (existing != null)
            {
                existing.transform.position = positions[i];
                continue;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.scene);
            go.name = name;
            go.transform.SetParent(container.transform, true);
            go.transform.position = positions[i];
            Debug.Log($"[StoryChapter4Decorator] Placed {name} at {positions[i]}.");
        }
    }

    // ---- Helpers ----

    private static JournalData CreateOrUpdateJournal(
        string path, int id, JournalCategory category, string title, string content)
    {
        var existing = AssetDatabase.LoadAssetAtPath<JournalData>(path);
        JournalData jd = existing != null ? existing : ScriptableObject.CreateInstance<JournalData>();

        jd.id = id;
        jd.category = category;
        jd.title = title;
        jd.content = content;

        if (existing == null)
            AssetDatabase.CreateAsset(jd, path);
        else
            EditorUtility.SetDirty(jd);

        return jd;
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
}
