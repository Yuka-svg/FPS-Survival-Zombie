using UnityEditor;
using UnityEngine;
using cowsins;

/// <summary>
/// One-shot editor utility that decorates the Chapter 5 Apartment + Broken Bridge
/// area with story-appropriate props: 8 optional lore journals from the apartment
/// residents and military personnel at the bridge, plus ammo/health/gold pickups
/// scattered across the high-rise buildings, shops, and the bridge approach.
///
/// Also adds atmospheric decoration (wrecked cars, dead bodies, barricades,
/// quarantine signs, fire/smoke/blood FX) specific to the Ch5 area that the
/// global StoryMapDecorator does not cover.
///
/// Run via the menu: Tools/Story/Decorate Chapter 5. Safe to re-run (idempotent
/// — updates existing assets/objects instead of duplicating).
///
/// The 8 lore journals are placed under a "Ch5_LoreJournals" sub-container so
/// they do NOT count toward Q10's required 4 collectibles (the
/// StoryChapter5Builder scans only direct children of Ch5 for Q10). They are
/// still collectible by the player and appear in the journal gallery as
/// optional lore entries.
///
/// All items are placed under a "Ch5_Items" sub-container for clean hierarchy
/// management. Decoration props go under "Ch5_Decor" sub-containers.
/// </summary>
public static class StoryChapter5Decorator
{
    private const string JournalFolder = "Assets/Resources/Journals";
    private const string PropsFolder = "Assets/Map/PolygonApocalypse/Prefabs/Props";
    private const string VehiclesFolder = "Assets/Map/PolygonApocalypse/Prefabs/Vehicles";
    private const string DeadBodiesFolder = "Assets/Map/PolygonApocalypse/Prefabs/DeadBodies";
    private const string FXFolder = "Assets/Map/PolygonApocalypse/Prefabs/FX/Prefabbed";

    [MenuItem("Tools/Story/Decorate Chapter 5")]
    public static void Decorate()
    {
        EnsureFolder("Assets/Resources", "Journals");

        // ---- Create 8 lore journal assets ----
        var journals = new JournalData[8];
        journals[0] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_14.asset",
            id: 31, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #14 — Cư dân tầng 12",
            content: "Ngày 20 tháng 3. Chúng tôi đã barricade toàn bộ tầng 12 từ hôm qua. Thang máy bị cắt, cửa cầu thang bị chèn bằng tủ lạnh và bàn ghế. Có khoảng 15 người đang trú ẩn ở đây. Nước và đồ ăn chỉ đủ cho 3 ngày nữa. Tiếng kêu gào bên ngoài không ngừng. Tôi nhìn xuống đường thấy quân đội đang rút lui, bỏ lại xe và vũ khí. Họ cũng bỏ chúng tôi rồi.");
        journals[1] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_15.asset",
            id: 32, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #15 — Người mẹ ở tầng 8",
            content: "Con tôi, bé Mia, đã bị cắn hôm qua. Tôi trói nó vào giường. Bác sĩ nói 6 giờ là sẽ biến. Đã 8 tiếng rồi mà nó vẫn còn nhận ra tôi. Nó gọi 'mẹ ơi' bằng giọng yếu ớt. Tôi không biết phải làm gì. Nếu ai đọc được cuốn này, xin hãy cho tôi biết — có cách nào cứu nó không? Tôi nghe nói có nghiên cứu thuốc giải ở đâu đó. Xin hãy...");
        journals[2] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_16.asset",
            id: 33, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #16 — Bảo vệ chung cư",
            content: "Tôi làm bảo vệ tòa nhà này 12 năm. Đêm qua là đêm kinh hoàng nhất. Khoảng 10 giờ, có người gõ cửa ầm ầm. Tôi mở thì thấy một người toàn thân máu, mắt trắng dã. Nó lao vào cắn tôi. Tôi đẩy được và chạy lên lầu. Bây giờ tôi đang ở phòng bảo vệ tầng 1, cửa đã khóa. Camera vẫn hoạt động. Tôi thấy chúng đi theo cầu thang lên từng tầng. Không ai an toàn nữa.");
        journals[3] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_17.asset",
            id: 34, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #17 — Sinh viên y khoa",
            content: "Tôi là sinh viên y khoa năm cuối, đang thực tập ở bệnh viện thì dịch bùng. Tôi về chung cư và barricade trong phòng. Từ những gì quan sát được, virus lây qua cắn, ủ bệnh 4-6 giờ. Biểu hiện: mắt trắng, mất ý thức, hung dữ. Tôi nghi ngờ đây là biến thể của virus rage đã được chỉnh sửa. Nếu có ai đang nghiên cứu thuốc giải, hãy liên hệ tần số radio 87.5. Tôi có kiến thức y khoa, có thể giúp được.");
        journals[4] = CreateOrUpdateJournal(
            JournalFolder + "/NeighborJournal_18.asset",
            id: 35, category: JournalCategory.NeighborJournal,
            title: "Nhật ký hàng xóm #18 — Ông già ở tầng 15",
            content: "78 tuổi, sống một mình. Vợ mất năm ngoái. Con cái ở nước ngoài, gọi điện không được — mạng bị cắt. Tôi viết dòng này ở ban công tầng 15, nhìn xuống đường. Quân đội đã bỏ đi. Xe bọc thép còn đỗ dưới đó nhưng không ai trong. Tôi thấy có thanh niên đang chạy về phía cầu. Cầu đã gãy từ hôm qua — anh ta không biết. Tôi hét nhưng tiếng tôi không đủ lớn. Chúc may mắn, con trai.");
        journals[5] = CreateOrUpdateJournal(
            JournalFolder + "/MilitaryRecord_04.asset",
            id: 36, category: JournalCategory.MilitaryRecord,
            title: "Báo cáo quân đội #4 — Lệnh rút lui",
            content: "LỆNH RÚT LUI — MẬT. 23:00 ngày 19/3. Toàn bộ quân đội rút khỏi khu chung cư và cầu vượt. Lý do: mất kiểm soát, tỷ lệ lây nhiễm quá cao. Lệnh phá hủy cầu để ngăn lây lan sang bờ bên kia. Kíp công binh đã gắn charges ở trụ chính. Bom sẽ kích nổ sau 24 giờ. Bất kỳ ai còn ở khu vực này được xem là đã mất. Không quay lại cứu. Ký: Đại tá Morrison.");
        journals[6] = CreateOrUpdateJournal(
            JournalFolder + "/MilitaryRecord_05.asset",
            id: 37, category: JournalCategory.MilitaryRecord,
            title: "Báo cáo quân đội #5 — Kíp công binh",
            content: "Nhật ký kíp công binh. Chúng tôi gắn 8 charges ở trụ cầu. Nhưng khi chuẩn bị rút, một thành viên bị cắn. Anh ta biến ngay trong xe. Xe đâm vào rào, 2 người chết. Tôi là người sống sót duy nhất. Charges đã gắn nhưng tôi chưa kích nổ — tôi cần lệnh. Nhưng radio không liên lạc được. Tôi đang ẩn trong tòa nhà bên cầu. Nếu ai đọc được: bom đã gắn nhưng chưa kích. Cần bộ kích nổ. Tìm trong tòa cao tầng gần đó.");
        journals[7] = CreateOrUpdateJournal(
            JournalFolder + "/CureRecord_02.asset",
            id: 38, category: JournalCategory.CureRecord,
            title: "Ghi chép thuốc giải #2 — Bệnh nhân 001",
            content: "Ghi chép cuối. Bệnh nhân 001 — chính là người anh trai của một thanh niên đang tìm anh trong thành phố. Anh ta tình nguyện thử thuốc giải prototype. Lần thử đầu: sốt cao, co giật, nhưng không biến. Lần thử hai: mắt trở lại bình thường, nhận thức rõ. Nhưng rồi thuốc bị mất — bị quân đội tịch thu khi rút lui. Anh ta vẫn còn sống, đang ẩn ở đâu đó trong tòa cao tầng. Nếu em anh đọc được: anh vẫn còn sống. Tìm anh ở tầng cao nhất.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- Scene setup: place journals + items + decoration ----
        SetupScene(journals);

        AssetDatabase.SaveAssets();
        Debug.Log("[StoryChapter5Decorator] Chapter 5 decorated: 8 lore journals + items + props.");
    }

    private static void SetupScene(JournalData[] journals)
    {
        var ch5 = GameObject.Find("=== WORLD ===/StoryZones/Ch5_ApartmentBridge");
        if (ch5 == null)
        {
            Debug.LogError("[StoryChapter5Decorator] Ch5_ApartmentBridge not found.");
            return;
        }

        // ---- Lore journals sub-container (NOT direct children of Ch5, so Q10
        //      does not count them as required). ----
        var loreContainer = FindChild(ch5, "Ch5_LoreJournals");
        if (loreContainer == null)
        {
            loreContainer = new GameObject("Ch5_LoreJournals");
            loreContainer.transform.SetParent(ch5.transform, false);
        }
        loreContainer.transform.localPosition = Vector3.zero;

        // ---- Items sub-container ----
        var itemsContainer = FindChild(ch5, "Ch5_Items");
        if (itemsContainer == null)
        {
            itemsContainer = new GameObject("Ch5_Items");
            itemsContainer.transform.SetParent(ch5.transform, false);
        }
        itemsContainer.transform.localPosition = Vector3.zero;

        // ---- Decoration sub-container ----
        var decorContainer = FindChild(ch5, "Ch5_Decor");
        if (decorContainer == null)
        {
            decorContainer = new GameObject("Ch5_Decor");
            decorContainer.transform.SetParent(ch5.transform, false);
        }
        decorContainer.transform.localPosition = Vector3.zero;

        // ---- Place 8 lore journals inside/near buildings ----
        PlaceLoreJournals(loreContainer, journals);

        // ---- Place ammo / health / gold across buildings & bridge approach ----
        PlaceItems(itemsContainer);

        // ---- Place atmospheric decoration (wrecks, dead bodies, barricades, FX) ----
        PlaceDecoration(decorContainer);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    /// <summary>
    /// Places 8 lore journal collectibles at positions inside/near buildings
    /// in the Ch5 apartment + bridge area. Each journal uses the Journal.prefab
    /// and gets a Collectible component wired to the corresponding JournalData.
    /// Positions are in WORLD coordinates (the journals are parented to the
    /// loreContainer which is at Ch5's origin, so localPosition ≈ world position
    /// offset by Ch5's position — we use SetParent with true to preserve world pos).
    /// </summary>
    private static void PlaceLoreJournals(GameObject container, JournalData[] journals)
    {
        var journalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefab/Journal.prefab");
        if (journalPrefab == null)
        {
            Debug.LogError("[StoryChapter5Decorator] Journal.prefab not found.");
            return;
        }

        // Positions in WORLD space — inside high-rise buildings, shops, near bridge.
        // Ch5 area buildings (world coords):
        //  HighRise (6.5, 0, 62), (55.5, 0, 60.5), (35.5, 0, 72), (-57.5, 0, 16.5), (-91.5, 0, 24)
        //  Commercial (-74.5, 0, 23.5), (13.5, 0, 77), (54.5, 0, 74.5), (-45, 0, 42.5), (-17.5, 0, 24.5)
        //  Industrial (-29, 0, 62.5), (-38, 0, 22), (-100.4, 0, 11.6)
        //  Apartment (25.3, 0, 24.1), Cafe (53, 0, 13.5)
        Vector3[] positions = {
            new Vector3(55.5f, 1f, 60.5f),     // HighRise — resident floor 12
            new Vector3(35.5f, 1f, 72f),       // HighRise — mother on floor 8
            new Vector3(6.5f, 1f, 62f),        // HighRise — security guard
            new Vector3(-17.5f, 1f, 24.5f),    // Commercial — med student
            new Vector3(25.3f, 1f, 24.1f),     // Apartment — old man floor 15
            new Vector3(-91.5f, 1f, 24f),      // HighRise near bridge — military order
            new Vector3(-100.4f, 1f, 11.6f),   // Industrial near bridge — engineer team
            new Vector3(-45f, 1f, 42.5f),      // Commercial — cure record
        };
        string[] names = {
            "LoreJournal_31_ResidentFloor12",
            "LoreJournal_32_MotherFloor8",
            "LoreJournal_33_SecurityGuard",
            "LoreJournal_34_MedStudent",
            "LoreJournal_35_OldManFloor15",
            "LoreJournal_36_MilitaryRetreatOrder",
            "LoreJournal_37_EngineerTeam",
            "LoreJournal_38_CureRecordPatient001",
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

            Debug.Log($"[StoryChapter5Decorator] Placed {names[i]} at {positions[i]}.");
        }
    }

    /// <summary>
    /// Places ammo (Bullet Pickeable), health (Healthpack), and gold (Coin)
    /// pickups across buildings and the bridge approach in the Ch5 area.
    /// </summary>
    private static void PlaceItems(GameObject container)
    {
        var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/Bullet Pickeable.prefab");
        var healthPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/PowerUps/Healthpack.prefab");
        var coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/Coin.prefab");

        if (bulletPrefab == null) Debug.LogWarning("[StoryChapter5Decorator] Bullet Pickeable prefab not found.");
        if (healthPrefab == null) Debug.LogWarning("[StoryChapter5Decorator] Healthpack prefab not found.");
        if (coinPrefab == null) Debug.LogWarning("[StoryChapter5Decorator] Coin prefab not found.");

        // ---- Ammo placements (10) — inside high-rises, shops, along escape route ----
        Vector3[] ammoPositions = {
            new Vector3(55.5f, 1f, 62f),       // HighRise near SaveRoom
            new Vector3(35.5f, 1f, 70f),       // HighRise
            new Vector3(6.5f, 1f, 60f),        // HighRise near bomb
            new Vector3(-17.5f, 1f, 26f),      // Commercial
            new Vector3(25.3f, 1f, 26f),       // Apartment
            new Vector3(-45f, 1f, 40f),        // Commercial
            new Vector3(-74.5f, 1f, 25f),      // Commercial near bridge approach
            new Vector3(-91.5f, 1f, 26f),      // HighRise near bridge
            new Vector3(-100.4f, 1f, 13f),     // Industrial near bridge
            new Vector3(-60f, 1f, 20f),        // Along escape route
        };
        PlacePickups(container, bulletPrefab, "Ch5_Ammo_", ammoPositions);

        // ---- Health placements (7) — inside buildings, near bridge ----
        Vector3[] healthPositions = {
            new Vector3(53f, 1f, 60f),         // Near SaveRoom
            new Vector3(8f, 1f, 60f),          // HighRise near bomb
            new Vector3(-15f, 1f, 26f),        // Commercial
            new Vector3(-43f, 1f, 42f),        // Commercial
            new Vector3(-72f, 1f, 25f),        // Commercial near bridge
            new Vector3(-90f, 1f, 25f),        // HighRise near bridge
            new Vector3(-50f, 1f, 15f),        // Along escape route
        };
        PlacePickups(container, healthPrefab, "Ch5_Health_", healthPositions);

        // ---- Gold placements (12) — inside buildings, shops, along escape ----
        Vector3[] goldPositions = {
            new Vector3(55.5f, 1f, 58f),       // HighRise
            new Vector3(35.5f, 1f, 70f),       // HighRise
            new Vector3(6.5f, 1f, 64f),        // HighRise
            new Vector3(53f, 1f, 15f),         // Cafe
            new Vector3(44f, 1f, 16f),         // Shop
            new Vector3(25.3f, 1f, 22f),       // Apartment
            new Vector3(-17.5f, 1f, 22f),      // Commercial
            new Vector3(-45f, 1f, 44f),        // Commercial
            new Vector3(-74.5f, 1f, 21f),      // Commercial near bridge
            new Vector3(-91.5f, 1f, 22f),      // HighRise near bridge
            new Vector3(-100.4f, 1f, 10f),     // Industrial near bridge
            new Vector3(-55f, 1f, 18f),        // Along escape route
        };
        PlacePickups(container, coinPrefab, "Ch5_Gold_", goldPositions);
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
            Debug.Log($"[StoryChapter5Decorator] Placed {name} at {positions[i]}.");
        }
    }

    // ===================================================================
    //  Atmospheric decoration — wrecks, dead bodies, barricades, FX
    //  specific to the Ch5 apartment + broken bridge area
    // ===================================================================
    private static void PlaceDecoration(GameObject container)
    {
        var wrecksContainer = EnsureChild(container, "Wrecks");
        var deadContainer = EnsureChild(container, "DeadBodies");
        var barricadeContainer = EnsureChild(container, "Barricades");
        var propsContainer = EnsureChild(container, "Props");
        var fxContainer = EnsureChild(container, "FX");

        PlaceCh5Wrecks(wrecksContainer);
        PlaceCh5DeadBodies(deadContainer);
        PlaceCh5Barricades(barricadeContainer);
        PlaceCh5Props(propsContainer);
        PlaceCh5FX(fxContainer);
    }

    private static void PlaceCh5Wrecks(GameObject container)
    {
        string[] wreckPrefabs = {
            "SM_Prop_Car_Wrecked_01",
            "SM_Prop_Car_Wrecked_SUV_01",
            "SM_Prop_Car_Wrecked_Van_01",
            "SM_Prop_Car_Wrecked_Rusted_01",
            "SM_Prop_Car_Wrecked_OpenBoot_01",
            "SM_Prop_Car_Wrecked_Squished_01",
        };

        // Wrecks across the apartment area roads and bridge approach.
        Vector3[] positions = {
            new Vector3(45f, 0f, 20f),     // Road near shops
            new Vector3(50f, 0f, 25f),     // Road near apartment
            new Vector3(20f, 0f, 30f),     // Near apartment building
            new Vector3(10f, 0f, 40f),     // Near high-rise
            new Vector3(-30f, 0f, 30f),    // Midway to bridge
            new Vector3(-50f, 0f, 20f),    // Bridge approach
            new Vector3(-70f, 0f, 15f),    // Bridge approach
            new Vector3(-85f, 0f, 10f),    // Near bridge
            new Vector3(-95f, 0f, 5f),     // At bridge entrance
            new Vector3(-100f, 0f, 0f),    // At broken bridge
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var prefabName = wreckPrefabs[i % wreckPrefabs.Length];
            var name = $"Ch5_Wreck_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadProp(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = positions[i];
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        Debug.Log($"[StoryChapter5Decorator] Placed {positions.Length} wrecks in Ch5.");
    }

    private static void PlaceCh5DeadBodies(GameObject container)
    {
        string[] deadBodyPrefabs = {
            "SM_DeadBody_Male_01", "SM_DeadBody_Male_02", "SM_DeadBody_Male_03",
            "SM_DeadBody_Male_04", "SM_DeadBody_Male_06", "SM_DeadBody_Male_07",
            "SM_DeadBody_Female_01", "SM_DeadBody_Female_02", "SM_DeadBody_Female_03",
            "SM_DeadBody_Female_04", "SM_DeadBody_Female_06",
            "SM_DeadBody_Pile_01", "SM_DeadBody_Pile_02",
            "SM_Prop_BodyBag_01", "SM_Prop_BodyBag_02", "SM_Prop_BodyBag_Pile_01",
            "SM_Prop_DeadBody_Laying_Male_01", "SM_Prop_DeadBody_Laying_Female_01",
            "SM_Prop_DeadBody_Sitting_Male_01", "SM_Prop_DeadBody_Sitting_Male_02",
        };

        // Dead bodies inside buildings, on roads, near bridge — mass casualties
        // from the military retreat and apartment outbreak.
        Vector3[] positions = {
            new Vector3(55f, 0f, 60f),     // HighRise lobby
            new Vector3(35f, 0f, 70f),     // HighRise lobby
            new Vector3(6f, 0f, 60f),      // HighRise near bomb
            new Vector3(45f, 0f, 22f),     // Road near shops
            new Vector3(25f, 0f, 24f),     // Apartment entrance
            new Vector3(-17f, 0f, 24f),    // Commercial
            new Vector3(-45f, 0f, 42f),    // Commercial
            new Vector3(-50f, 0f, 20f),    // Bridge approach — body bag pile
            new Vector3(-72f, 0f, 22f),    // Bridge approach
            new Vector3(-85f, 0f, 12f),    // Near bridge — military casualties
            new Vector3(-92f, 0f, 20f),    // Near bridge
            new Vector3(-98f, 0f, 8f),     // At bridge entrance
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var prefabName = deadBodyPrefabs[i % deadBodyPrefabs.Length];
            var name = $"Ch5_DeadBody_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadDeadBody(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = positions[i];
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        Debug.Log($"[StoryChapter5Decorator] Placed {positions.Length} dead bodies in Ch5.");
    }

    private static void PlaceCh5Barricades(GameObject container)
    {
        // Barricades, sandbags, quarantine signs at the apartment entrance and
        // bridge approach — military last-stand positions.
        var placements = new (string, Vector3, float)[] {
            // Apartment building barricades
            ("SM_Prop_Barricade_01", new Vector3(10f, 0f, 55f), 0f),
            ("SM_Prop_Barricade_02", new Vector3(12f, 0f, 58f), 0f),
            ("SM_Prop_Sandbag_Wall_01", new Vector3(8f, 0f, 52f), 90f),
            // Bridge approach — military checkpoint
            ("SM_Prop_Barricade_03", new Vector3(-60f, 0f, 15f), 0f),
            ("SM_Prop_Barricade_05", new Vector3(-62f, 0f, 18f), 0f),
            ("SM_Prop_Sandbag_Wall_02", new Vector3(-58f, 0f, 12f), 90f),
            ("SM_Prop_Sign_Quarantine_01", new Vector3(-65f, 0f, 10f), 180f),
            ("SM_Prop_Sign_Danger_01", new Vector3(-90f, 0f, 8f), 180f),
            // Bridge entrance — final roadblock
            ("SM_Prop_Barricade_04", new Vector3(-95f, 0f, 5f), 0f),
            ("SM_Prop_Barricade_06", new Vector3(-97f, 0f, 8f), 0f),
            ("SM_Prop_Road_Barrier_01", new Vector3(-93f, 0f, 2f), 0f),
            ("SM_Prop_Road_Barrier_02", new Vector3(-99f, 0f, 12f), 0f),
            // Cones
            ("SM_Prop_Cone_01", new Vector3(-61f, 0f, 14f), 0f),
            ("SM_Prop_Cone_01", new Vector3(-94f, 0f, 6f), 0f),
            // Floodlight at bridge checkpoint
            ("SM_Prop_Floodlights_01", new Vector3(-63f, 0f, 12f), 180f),
        };

        for (int i = 0; i < placements.Length; i++)
        {
            var (prefabName, pos, rotY) = placements[i];
            var name = $"Ch5_Barricade_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadProp(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        }
        Debug.Log($"[StoryChapter5Decorator] Placed {placements.Length} barricades/signs in Ch5.");
    }

    private static void PlaceCh5Props(GameObject container)
    {
        // Misc props — trash, crates, dumpsters, shopping carts, ammo boxes
        // to convey looting and abandonment in the apartment area.
        var placements = new (string, Vector3, float)[] {
            // Trash bags — looting aftermath
            ("SM_Prop_TrashBag_01", new Vector3(50f, 0f, 20f), 0f),
            ("SM_Prop_TrashBag_01", new Vector3(20f, 0f, 30f), 0f),
            ("SM_Prop_TrashBag_01", new Vector3(-45f, 0f, 40f), 0f),
            ("SM_Prop_TrashBag_01", new Vector3(-80f, 0f, 15f), 0f),
            // Dumpsters
            ("SM_Prop_Dumpster_01", new Vector3(48f, 0f, 18f), 90f),
            ("SM_Prop_Dumpster_01", new Vector3(-40f, 0f, 38f), 0f),
            ("SM_Prop_Dumpster_01", new Vector3(-85f, 0f, 10f), 90f),
            // Shopping carts — abandoned
            ("SM_Prop_ShoppingCart_01", new Vector3(44f, 0f, 22f), 30f),
            ("SM_Prop_ShoppingCart_01", new Vector3(-50f, 0f, 18f), 120f),
            // Crates — military supply crates near bridge
            ("SM_Prop_Crate_01", new Vector3(-62f, 0f, 16f), 0f),
            ("SM_Prop_Crate_02", new Vector3(-64f, 0f, 14f), 0f),
            ("SM_Prop_Crate_Large_01", new Vector3(-60f, 0f, 18f), 0f),
            // Ammo boxes — military leftover
            ("SM_Prop_Ammo_Box_01", new Vector3(-61f, 0f, 15f), 0f),
            ("SM_Prop_Ammo_Box_Open_01", new Vector3(-63f, 0f, 17f), 0f),
            // Pallets
            ("SM_Prop_Pallet_01", new Vector3(-66f, 0f, 13f), 0f),
            // Hydrants
            ("SM_Prop_Hydrant_01", new Vector3(40f, 0f, 25f), 0f),
            ("SM_Prop_Hydrant_01", new Vector3(-70f, 0f, 20f), 0f),
        };

        for (int i = 0; i < placements.Length; i++)
        {
            var (prefabName, pos, rotY) = placements[i];
            var name = $"Ch5_Prop_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadProp(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        }
        Debug.Log($"[StoryChapter5Decorator] Placed {placements.Length} misc props in Ch5.");
    }

    private static void PlaceCh5FX(GameObject container)
    {
        // Atmospheric FX — fire, smoke, blood, flies, road flares
        var placements = new (string, Vector3)[] {
            // Fire near wrecks
            ("FX_Fire_01", new Vector3(45f, 0f, 22f)),
            ("FX_Fire_02", new Vector3(-50f, 0f, 20f)),
            ("FX_Fire_01", new Vector3(-95f, 0f, 5f)),
            ("FX_Fire_03", new Vector3(-85f, 0f, 12f)),
            // Smoke from fires
            ("FX_Smoke_Black_01", new Vector3(45f, 0f, 22f)),
            ("FX_Smoke_Large_01", new Vector3(-50f, 0f, 20f)),
            ("FX_Smoke_Black_01", new Vector3(-95f, 0f, 5f)),
            // Blood splats near dead bodies
            ("FX_BloodSplat_01", new Vector3(55f, 0f, 60f)),
            ("FX_BloodSplat_01", new Vector3(6f, 0f, 60f)),
            ("FX_BloodSplat_01", new Vector3(-45f, 0f, 42f)),
            ("FX_BloodSplat_01", new Vector3(-85f, 0f, 12f)),
            ("FX_BloodSplat_01", new Vector3(-98f, 0f, 8f)),
            // Flies over body piles
            ("FX_Flies_01", new Vector3(-50f, 0f, 20f)),
            ("FX_Flies_01", new Vector3(-85f, 0f, 12f)),
            ("FX_Flies_01", new Vector3(6f, 0f, 60f)),
            // Road flares at bridge checkpoint
            ("Fx_RoadFlare_01", new Vector3(-60f, 0f, 15f)),
            ("Fx_RoadFlare_01", new Vector3(-95f, 0f, 5f)),
        };

        for (int i = 0; i < placements.Length; i++)
        {
            var (prefabName, pos) = placements[i];
            var name = $"Ch5_FX_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadFX(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = pos;
        }
        Debug.Log($"[StoryChapter5Decorator] Placed {placements.Length} FX objects in Ch5.");
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

    private static GameObject EnsureChild(GameObject parent, string name)
    {
        var child = FindChild(parent, name);
        if (child == null)
        {
            child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
        }
        return child;
    }

    private static GameObject InstantiatePrefab(GameObject prefab, GameObject parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.scene);
        go.transform.SetParent(parent.transform, true);
        return go;
    }

    private static GameObject LoadProp(string name)
        => AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsFolder}/{name}.prefab");

    private static GameObject LoadDeadBody(string name)
        => AssetDatabase.LoadAssetAtPath<GameObject>($"{DeadBodiesFolder}/{name}.prefab");

    private static GameObject LoadFX(string name)
        => AssetDatabase.LoadAssetAtPath<GameObject>($"{FXFolder}/{name}.prefab");

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
