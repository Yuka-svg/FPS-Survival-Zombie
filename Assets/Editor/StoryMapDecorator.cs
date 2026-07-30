using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor utility that decorates the Story mode map with additional
/// props to convey a collapsed, overrun city: wrecked cars, dead bodies, body
/// bags, emergency vehicles (ambulance, army truck, news van, prison bus),
/// crashed helicopter, barricades, sandbag walls, quarantine signs, road
/// barriers, cones, crates, trash bags, dumpsters, shopping carts, and
/// atmospheric FX (fire, smoke, blood splats, flies).
///
/// Run via the menu: Tools/Story/Decorate Map (City Collapse). Safe to re-run
/// (idempotent — checks for existing named instances under the dedicated
/// "CityCollapse" container and skips re-creating them).
///
/// IMPORTANT: This script ONLY ADDS new objects. It does NOT modify, move, or
/// delete any existing GameObject in the scene. All new objects are parented
/// to a single "=== WORLD ===/CityCollapse" container with sub-containers per
/// category, leaving the user's existing "Map (Do Not Touch)" hierarchy and
/// all quest/story wiring untouched.
/// </summary>
public static class StoryMapDecorator
{
    private const string PropsFolder = "Assets/Map/PolygonApocalypse/Prefabs/Props";
    private const string VehiclesFolder = "Assets/Map/PolygonApocalypse/Prefabs/Vehicles";
    private const string DeadBodiesFolder = "Assets/Map/PolygonApocalypse/Prefabs/DeadBodies";
    private const string FXFolder = "Assets/Map/PolygonApocalypse/Prefabs/FX/Prefabbed";

    [MenuItem("Tools/Story/Decorate Map (City Collapse)")]
    public static void Decorate()
    {
        var world = GameObject.Find("=== WORLD ===");
        if (world == null)
        {
            Debug.LogError("[StoryMapDecorator] '=== WORLD ===' not found.");
            return;
        }

        // Root container for all new decoration objects.
        var root = FindChild(world, "CityCollapse");
        if (root == null)
        {
            root = new GameObject("CityCollapse");
            root.transform.SetParent(world.transform, false);
        }
        root.transform.localPosition = Vector3.zero;

        // ---- Sub-containers per category ----
        var wrecksContainer = EnsureChild(root, "Wrecks");
        var deadContainer = EnsureChild(root, "DeadBodies");
        var emergencyContainer = EnsureChild(root, "EmergencyVehicles");
        var barricadeContainer = EnsureChild(root, "Barricades");
        var propsContainer = EnsureChild(root, "Props");
        var fxContainer = EnsureChild(root, "FX");

        // ---- 1) Wrecked cars across the city ----
        PlaceWrecks(wrecksContainer);

        // ---- 2) Dead bodies & body bags ----
        PlaceDeadBodies(deadContainer);

        // ---- 3) Emergency vehicles (failed evacuation story) ----
        PlaceEmergencyVehicles(emergencyContainer);

        // ---- 4) Barricades, sandbags, quarantine signs ----
        PlaceBarricades(barricadeContainer);

        // ---- 5) Misc props (trash, crates, dumpsters, shopping carts) ----
        PlaceMiscProps(propsContainer);

        // ---- 6) Atmospheric FX (fire, smoke, blood, flies) ----
        PlaceFX(fxContainer);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[StoryMapDecorator] Map decoration complete.");
    }

    // ===================================================================
    //  Wrecked cars — scattered across all chapter areas
    // ===================================================================
    private static void PlaceWrecks(GameObject container)
    {
        string[] wreckPrefabs = {
            "SM_Prop_Car_Wrecked_01",
            "SM_Prop_Car_Wrecked_Bus_01",
            "SM_Prop_Car_Wrecked_SUV_01",
            "SM_Prop_Car_Wrecked_Van_01",
            "SM_Prop_Car_Wrecked_Rusted_01",
            "SM_Prop_Car_Wrecked_OpenBoot_01",
            "SM_Prop_Car_Wrecked_Squished_01",
            "SM_Prop_Car_Wrecked_Stack_01",
        };

        // Positions across the city — roads, intersections, near buildings.
        // Clustered to feel like traffic jams / pile-ups during evacuation.
        Vector3[] positions = {
            // Ch4 Residential area — main road through the neighborhood
            new Vector3(35f, 0f, -100f),
            new Vector3(40f, 0f, -105f),
            new Vector3(45f, 0f, -110f),
            new Vector3(38f, 0f, -115f),
            new Vector3(50f, 0f, -130f),
            new Vector3(55f, 0f, -135f),
            new Vector3(20f, 0f, -160f),
            new Vector3(25f, 0f, -165f),
            new Vector3(60f, 0f, -180f),
            new Vector3(65f, 0f, -185f),
            new Vector3(30f, 0f, -200f),
            new Vector3(35f, 0f, -205f),
            new Vector3(45f, 0f, -220f),
            new Vector3(50f, 0f, -225f),
            // Ch3 Construction area
            new Vector3(95f, 0f, -100f),
            new Vector3(100f, 0f, -110f),
            new Vector3(110f, 0f, -120f),
            new Vector3(115f, 0f, -130f),
            new Vector3(120f, 0f, -150f),
            new Vector3(125f, 0f, -160f),
            // Ch2 Hospital surroundings
            new Vector3(140f, 0f, 10f),
            new Vector3(145f, 0f, 15f),
            new Vector3(155f, 0f, 20f),
            new Vector3(160f, 0f, 25f),
            new Vector3(170f, 0f, 30f),
            // Ch5 Apartment Bridge area
            new Vector3(80f, 0f, 20f),
            new Vector3(85f, 0f, 30f),
            new Vector3(90f, 0f, 40f),
            new Vector3(100f, 0f, 50f),
            new Vector3(110f, 0f, 60f),
            // Ch1 Tutorial Camp approach
            new Vector3(220f, 0f, -10f),
            new Vector3(225f, 0f, -5f),
            new Vector3(230f, 0f, 0f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var prefabName = wreckPrefabs[i % wreckPrefabs.Length];
            var name = $"CityCollapse_Wreck_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadProp(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = positions[i];
            // Random Y rotation for variety.
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        Debug.Log($"[StoryMapDecorator] Placed wrecked cars at {positions.Length} locations.");
    }

    // ===================================================================
    //  Dead bodies & body bags — victims of the outbreak
    // ===================================================================
    private static void PlaceDeadBodies(GameObject container)
    {
        string[] deadBodyPrefabs = {
            "SM_DeadBody_Male_01", "SM_DeadBody_Male_02", "SM_DeadBody_Male_03",
            "SM_DeadBody_Male_04", "SM_DeadBody_Male_06", "SM_DeadBody_Male_07",
            "SM_DeadBody_Male_09", "SM_DeadBody_Male_10", "SM_DeadBody_Male_13",
            "SM_DeadBody_Female_01", "SM_DeadBody_Female_02", "SM_DeadBody_Female_03",
            "SM_DeadBody_Female_04", "SM_DeadBody_Female_06", "SM_DeadBody_Female_08",
            "SM_DeadBody_Female_10", "SM_DeadBody_Female_12", "SM_DeadBody_Female_14",
            "SM_DeadBody_Pile_01", "SM_DeadBody_Pile_02",
            "SM_Prop_BodyBag_01", "SM_Prop_BodyBag_02", "SM_Prop_BodyBag_03",
            "SM_Prop_BodyBag_Pile_01",
            "SM_Prop_DeadBody_Laying_Male_01", "SM_Prop_DeadBody_Laying_Female_01",
            "SM_Prop_DeadBody_Sitting_Male_01", "SM_Prop_DeadBody_Sitting_Male_02",
        };

        // Positions — inside houses, near wrecks, on roads, in piles.
        Vector3[] positions = {
            // Ch4 Residential — inside/near houses
            new Vector3(52f, 0f, -139f),
            new Vector3(48f, 0f, -120f),
            new Vector3(50f, 0f, -100f),
            new Vector3(18f, 0f, -86f),
            new Vector3(20f, 0f, -102f),
            new Vector3(24f, 0f, -123f),
            new Vector3(-44f, 0f, -88f),
            // Ch4 — near wrecks / road
            new Vector3(38f, 0f, -108f),
            new Vector3(42f, 0f, -112f),
            new Vector3(55f, 0f, -138f),
            new Vector3(60f, 0f, -188f),
            new Vector3(32f, 0f, -208f),
            new Vector3(48f, 0f, -222f),
            // Ch4 — body bag pile near quarantine tents
            new Vector3(-40f, 0f, -142f),
            new Vector3(-38f, 0f, -145f),
            new Vector3(-42f, 0f, -138f),
            // Ch3 Construction
            new Vector3(98f, 0f, -105f),
            new Vector3(112f, 0f, -125f),
            new Vector3(118f, 0f, -155f),
            new Vector3(120f, 0f, -140f),
            // Ch2 Hospital — mass casualties
            new Vector3(142f, 0f, 12f),
            new Vector3(148f, 0f, 18f),
            new Vector3(158f, 0f, 22f),
            new Vector3(162f, 0f, 28f),
            new Vector3(155f, 0f, 15f),
            // Ch5 Apartment Bridge
            new Vector3(85f, 0f, 25f),
            new Vector3(95f, 0f, 35f),
            new Vector3(105f, 0f, 55f),
            // Ch1 Tutorial Camp
            new Vector3(225f, 0f, -8f),
            new Vector3(232f, 0f, 2f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var prefabName = deadBodyPrefabs[i % deadBodyPrefabs.Length];
            var name = $"CityCollapse_Dead_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadDeadBody(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = positions[i];
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        Debug.Log($"[StoryMapDecorator] Placed dead bodies at {positions.Length} locations.");
    }

    // ===================================================================
    //  Emergency vehicles — failed evacuation story
    // ===================================================================
    private static void PlaceEmergencyVehicles(GameObject container)
    {
        // (prefabName, position, rotationY)
        var placements = new (string, Vector3, float)[] {
            // Ambulances near hospital (Ch2)
            ("SM_Veh_Ambulance_01", new Vector3(145f, 0f, 8f), 90f),
            ("SM_Veh_Ambulance_01", new Vector3(152f, 0f, 8f), 90f),
            // Army trucks at quarantine checkpoint (Ch4 entrance)
            ("SM_Veh_Army_Truck_01", new Vector3(-15f, 0f, -150f), 0f),
            ("SM_Veh_Army_Truck_01", new Vector3(-15f, 0f, -160f), 0f),
            // Army trucks near hospital
            ("SM_Veh_Army_Truck_01", new Vector3(165f, 0f, 12f), 90f),
            // News van — reporter never left
            ("SM_Veh_NewsVan_01", new Vector3(95f, 0f, -95f), 180f),
            ("SM_Veh_NewsVan_01", new Vector3(135f, 0f, 35f), 270f),
            // Prison bus — failed evacuation transport
            ("SM_Veh_Prison_Bus_01", new Vector3(70f, 0f, -140f), 90f),
            // BigRig with damaged tanker — road block
            ("SM_Veh_BigRig_Trailer_Tanker_Damaged_01", new Vector3(105f, 0f, -115f), 0f),
            // Apocalyptic motorbike
            ("SM_Veh_Motorbike_Apoco_01", new Vector3(42f, 0f, -118f), 45f),
        };

        for (int i = 0; i < placements.Length; i++)
        {
            var (prefabName, pos, rotY) = placements[i];
            var name = $"CityCollapse_Emergency_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadVehicle(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        }
        Debug.Log($"[StoryMapDecorator] Placed {placements.Length} emergency vehicles.");
    }

    // ===================================================================
    //  Barricades, sandbags, quarantine signs — martial law remnants
    // ===================================================================
    private static void PlaceBarricades(GameObject container)
    {
        var placements = new (string, Vector3, float)[] {
            // Quarantine checkpoint at Ch4 entrance
            ("SM_Prop_Barricade_01", new Vector3(-12f, 0f, -145f), 0f),
            ("SM_Prop_Barricade_Wired_01", new Vector3(-10f, 0f, -148f), 0f),
            ("SM_Prop_Sandbag_Wall_01", new Vector3(-8f, 0f, -152f), 0f),
            ("SM_Prop_Sandbag_Wall_02", new Vector3(-6f, 0f, -155f), 0f),
            ("SM_Prop_Sign_Quarantine_01", new Vector3(-14f, 0f, -142f), 180f),
            ("SM_Prop_Wall_Quarantine_Gate_01", new Vector3(-18f, 0f, -150f), 90f),
            // Roadblocks on Ch4 main road
            ("SM_Prop_Barricade_03", new Vector3(35f, 0f, -95f), 0f),
            ("SM_Prop_Barricade_05", new Vector3(37f, 0f, -98f), 0f),
            ("SM_Prop_Road_Barrier_01", new Vector3(60f, 0f, -175f), 0f),
            ("SM_Prop_Road_Barrier_02", new Vector3(62f, 0f, -178f), 0f),
            // Hospital entrance barricades
            ("SM_Prop_Barricade_01", new Vector3(140f, 0f, 5f), 90f),
            ("SM_Prop_Barricade_02", new Vector3(143f, 0f, 5f), 90f),
            ("SM_Prop_Sandbag_Wall_01", new Vector3(146f, 0f, 5f), 90f),
            ("SM_Prop_Sign_Danger_01", new Vector3(138f, 0f, 5f), 270f),
            // Construction site barriers
            ("SM_Prop_Barricade_04", new Vector3(90f, 0f, -95f), 0f),
            ("SM_Prop_Barricade_06", new Vector3(92f, 0f, -98f), 0f),
            // Cones scattered
            ("SM_Prop_Cone_01", new Vector3(33f, 0f, -92f), 0f),
            ("SM_Prop_Cone_01", new Vector3(63f, 0f, -172f), 0f),
            ("SM_Prop_Cone_01", new Vector3(141f, 0f, 2f), 0f),
            // Floodlights at checkpoint
            ("SM_Prop_Floodlights_01", new Vector3(-16f, 0f, -145f), 180f),
            ("SM_Prop_Generator_01", new Vector3(-20f, 0f, -150f), 0f),
        };

        for (int i = 0; i < placements.Length; i++)
        {
            var (prefabName, pos, rotY) = placements[i];
            var name = $"CityCollapse_Barricade_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadProp(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        }
        Debug.Log($"[StoryMapDecorator] Placed {placements.Length} barricades/signs.");
    }

    // ===================================================================
    //  Misc props — trash, crates, dumpsters, shopping carts
    // ===================================================================
    private static void PlaceMiscProps(GameObject container)
    {
        var placements = new (string, Vector3, float)[] {
            // Trash bags — looting aftermath
            ("SM_Prop_TrashBag_01", new Vector3(50f, 0f, -138f), 0f),
            ("SM_Prop_TrashBag_01", new Vector3(22f, 0f, -100f), 0f),
            ("SM_Prop_TrashBag_01", new Vector3(-42f, 0f, -90f), 0f),
            ("SM_Prop_TrashBag_01", new Vector3(95f, 0f, -100f), 0f),
            ("SM_Prop_TrashBag_01", new Vector3(148f, 0f, 20f), 0f),
            // Dumpsters
            ("SM_Prop_Dumpster_01", new Vector3(53f, 0f, -142f), 90f),
            ("SM_Prop_Dumpster_01", new Vector3(20f, 0f, -88f), 0f),
            ("SM_Prop_Dumpster_01", new Vector3(-40f, 0f, -88f), 90f),
            ("SM_Prop_Dumpster_01", new Vector3(118f, 0f, -145f), 0f),
            // Shopping carts — abandoned
            ("SM_Prop_ShoppingCart_01", new Vector3(45f, 0f, -130f), 30f),
            ("SM_Prop_ShoppingCart_01", new Vector3(58f, 0f, -185f), 120f),
            ("SM_Prop_ShoppingCart_01", new Vector3(155f, 0f, 18f), 200f),
            // Crates
            ("SM_Prop_Crate_01", new Vector3(-18f, 0f, -148f), 0f),
            ("SM_Prop_Crate_02", new Vector3(-16f, 0f, -150f), 0f),
            ("SM_Prop_Crate_Open_01", new Vector3(95f, 0f, -105f), 0f),
            ("SM_Prop_Crate_Large_01", new Vector3(120f, 0f, -155f), 0f),
            // Pallets
            ("SM_Prop_Pallet_01", new Vector3(100f, 0f, -110f), 0f),
            ("SM_Prop_Pallet_02", new Vector3(115f, 0f, -145f), 0f),
            // Hydrants
            ("SM_Prop_Hydrant_01", new Vector3(40f, 0f, -120f), 0f),
            ("SM_Prop_Hydrant_01", new Vector3(65f, 0f, -195f), 0f),
            ("SM_Prop_Hydrant_01", new Vector3(150f, 0f, 10f), 0f),
        };

        for (int i = 0; i < placements.Length; i++)
        {
            var (prefabName, pos, rotY) = placements[i];
            var name = $"CityCollapse_Prop_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadProp(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        }
        Debug.Log($"[StoryMapDecorator] Placed {placements.Length} misc props.");
    }

    // ===================================================================
    //  Atmospheric FX — fire, smoke, blood, flies
    // ===================================================================
    private static void PlaceFX(GameObject container)
    {
        var placements = new (string, Vector3)[] {
            // Fire near wrecks
            ("FX_Fire_01", new Vector3(38f, 0f, -108f)),
            ("FX_Fire_01", new Vector3(95f, 0f, -102f)),
            ("FX_Fire_02", new Vector3(115f, 0f, -128f)),
            ("FX_Fire_01", new Vector3(145f, 0f, 12f)),
            ("FX_Fire_03", new Vector3(60f, 0f, -182f)),
            // Smoke from fires
            ("FX_Smoke_Black_01", new Vector3(38f, 0f, -108f)),
            ("FX_Smoke_Black_01", new Vector3(95f, 0f, -102f)),
            ("FX_Smoke_Large_01", new Vector3(115f, 0f, -128f)),
            ("FX_Smoke_Black_01", new Vector3(145f, 0f, 12f)),
            // Blood splats near dead bodies
            ("FX_BloodSplat_01", new Vector3(52f, 0f, -139f)),
            ("FX_BloodSplat_01", new Vector3(48f, 0f, -120f)),
            ("FX_BloodSplat_01", new Vector3(20f, 0f, -102f)),
            ("FX_BloodSplat_01", new Vector3(-40f, 0f, -142f)),
            ("FX_BloodSplat_01", new Vector3(142f, 0f, 12f)),
            ("FX_BloodSplat_01", new Vector3(118f, 0f, -155f)),
            // Flies over body piles
            ("FX_Flies_01", new Vector3(-40f, 0f, -142f)),
            ("FX_Flies_01", new Vector3(142f, 0f, 12f)),
            ("FX_Flies_01", new Vector3(38f, 0f, -112f)),
            // Road flares at checkpoint
            ("Fx_RoadFlare_01", new Vector3(-12f, 0f, -145f)),
            ("Fx_RoadFlare_01", new Vector3(-10f, 0f, -150f)),
        };

        for (int i = 0; i < placements.Length; i++)
        {
            var (prefabName, pos) = placements[i];
            var name = $"CityCollapse_FX_{i + 1:00}_{prefabName}";
            if (FindChild(container, name) != null) continue;

            var prefab = LoadFX(prefabName);
            if (prefab == null) continue;

            var go = InstantiatePrefab(prefab, container);
            go.name = name;
            go.transform.position = pos;
        }
        Debug.Log($"[StoryMapDecorator] Placed {placements.Length} FX objects.");
    }

    // ===================================================================
    //  Helpers
    // ===================================================================

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

    private static GameObject LoadVehicle(string name)
        => AssetDatabase.LoadAssetAtPath<GameObject>($"{VehiclesFolder}/{name}.prefab");

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
