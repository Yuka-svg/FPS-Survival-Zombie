using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor utility that scatters ExplosiveBarrel and NonFracturedCrate_01
/// prefabs across all 5 story chapters at tactically interesting but
/// non-blocking positions.
///
/// ExplosiveBarrels are placed near combat chokepoints, wrecks, and barricades
/// so the player can use them as environmental weapons against zombie waves.
/// NonFracturedCrate_01 props are placed near building walls, construction
/// sites, and alleyways as cover and loot containers — always offset from the
/// main walkable path so they never block the player.
///
/// Run via the menu: Tools/Story/Place Barrels & Crates. Safe to re-run
/// (idempotent — checks for existing named instances under the dedicated
/// "BarrelsAndCrates" container and skips re-creating them).
///
/// All new objects are parented to "=== WORLD ===/BarrelsAndCrates" with
/// sub-containers "Barrels" and "Crates".
/// </summary>
public static class StoryBarrelCratePlacer
{
    private const string BarrelPrefabPath =
        "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/ExplosiveBarrel.prefab";
    private const string CratePrefabPath =
        "Assets/Engine/Cowsins/Prefabs/DragAndDropExtras/NonFracturedCrate_01.prefab";

    [MenuItem("Tools/Story/Place Barrels and Crates")]
    public static void Place()
    {
        var world = GameObject.Find("=== WORLD ===");
        if (world == null)
        {
            Debug.LogError("[StoryBarrelCratePlacer] '=== WORLD ===' not found.");
            return;
        }

        var root = FindChild(world, "BarrelsAndCrates");
        if (root == null)
        {
            root = new GameObject("BarrelsAndCrates");
            root.transform.SetParent(world.transform, false);
        }
        root.transform.localPosition = Vector3.zero;

        var barrelsContainer = EnsureChild(root, "Barrels");
        var cratesContainer = EnsureChild(root, "Crates");

        var barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
        var cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CratePrefabPath);

        if (barrelPrefab == null)
        {
            Debug.LogError("[StoryBarrelCratePlacer] ExplosiveBarrel prefab not found at " + BarrelPrefabPath);
            return;
        }
        if (cratePrefab == null)
        {
            Debug.LogError("[StoryBarrelCratePlacer] NonFracturedCrate_01 prefab not found at " + CratePrefabPath);
            return;
        }

        PlaceBarrels(barrelsContainer, barrelPrefab);
        PlaceCrates(cratesContainer, cratePrefab);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[StoryBarrelCratePlacer] Barrels & crates placed successfully.");
    }

    // ===================================================================
    //  Explosive barrels — near combat areas, wrecks, barricades, chokepoints
    //  Y is set to 0.5 so the barrel sits on the ground (collider center ~0.66h).
    // ===================================================================
    private static void PlaceBarrels(GameObject container, GameObject prefab)
    {
        // (position, Y rotation) — placed off the main path, near walls/wrecks/barricades
        var placements = new (Vector3, float)[]
        {
            // ---- Ch1 Tutorial Camp — near the camp entrance and perimeter ----
            (new Vector3(225f, 0.5f, -8f), 0f),      // near wreck at camp approach
            (new Vector3(233f, 0.5f, 8f), 90f),       // beside camp wall
            (new Vector3(240f, 0.5f, -5f), 45f),      // near journal_4 area

            // ---- Ch2 Hospital — near entrance barricades and side alley ----
            (new Vector3(144f, 0.5f, 8f), 0f),        // near hospital barricade
            (new Vector3(138f, 0.5f, 12f), 90f),      // beside barricade
            (new Vector3(155f, 0.5f, 22f), 180f),     // near wreck by hospital
            (new Vector3(165f, 0.5f, 28f), 0f),       // along hospital road
            (new Vector3(170f, 0.5f, 35f), 90f),      // near hospital side

            // ---- Ch3 Construction — near barriers, generator, crates ----
            (new Vector3(92f, 0.5f, -98f), 0f),       // near construction barricade
            (new Vector3(96f, 0.5f, -102f), 90f),     // beside barricade
            (new Vector3(110f, 0.5f, -120f), 0f),     // near construction wreck
            (new Vector3(118f, 0.5f, -145f), 45f),    // near construction site
            (new Vector3(125f, 0.5f, -155f), 180f),   // near wreck pile
            (new Vector3(100f, 0.5f, -108f), 90f),    // near pallet area

            // ---- Ch4 Residential — near quarantine checkpoint, wrecks, houses ----
            (new Vector3(-14f, 0.5f, -148f), 0f),     // at quarantine checkpoint
            (new Vector3(-10f, 0.5f, -152f), 90f),    // beside sandbag wall
            (new Vector3(37f, 0.5f, -100f), 0f),      // near roadblock
            (new Vector3(42f, 0.5f, -112f), 45f),     // near wreck fire
            (new Vector3(52f, 0.5f, -138f), 180f),    // near dumpster/dead body
            (new Vector3(60f, 0.5f, -182f), 0f),      // near road barrier
            (new Vector3(48f, 0.5f, -220f), 90f),     // near residential wreck
            (new Vector3(30f, 0.5f, -205f), 0f),      // near save room area edge

            // ---- Ch5 Apartment Bridge — near bridge approach, apartments ----
            (new Vector3(82f, 0.5f, 25f), 0f),        // near apartment wrecks
            (new Vector3(88f, 0.5f, 35f), 90f),       // along bridge approach
            (new Vector3(95f, 0.5f, 45f), 180f),      // near apartment entrance
            (new Vector3(105f, 0.5f, 55f), 0f),       // near bridge road
            (new Vector3(55f, 0.5f, 45f), 45f),       // near Ch5 apartment (offset from save room)
            (new Vector3(20f, 0.5f, 50f), 90f),        // inside apartment ground
            (new Vector3(-5f, 0.5f, 30f), 0f),         // near bomb objective area
            (new Vector3(-10f, 0.5f, 10f), 180f),      // near escape route
        };

        int placed = 0;
        for (int i = 0; i < placements.Length; i++)
        {
            var (pos, rotY) = placements[i];
            var name = $"Barrel_{i + 1:00}";
            if (FindChild(container, name) != null) continue;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            placed++;
        }
        Debug.Log($"[StoryBarrelCratePlacer] Placed {placed} explosive barrels (total slots: {placements.Length}).");
    }

    // ===================================================================
    //  NonFracturedCrate_01 — near building walls, construction, alleyways
    //  as cover and loot containers. Y=0.5 to sit on ground.
    // ===================================================================
    private static void PlaceCrates(GameObject container, GameObject prefab)
    {
        var placements = new (Vector3, float)[]
        {
            // ---- Ch1 Tutorial Camp — near camp perimeter ----
            (new Vector3(235f, 0.5f, 10f), 0f),        // beside camp building
            (new Vector3(242f, 0.5f, -10f), 90f),      // near camp edge

            // ---- Ch2 Hospital — near hospital walls and side ----
            (new Vector3(145f, 0.5f, 15f), 0f),        // beside hospital entrance
            (new Vector3(160f, 0.5f, 30f), 45f),       // near hospital side wall
            (new Vector3(175f, 0.5f, 32f), 90f),       // along hospital road

            // ---- Ch3 Construction — near construction site, pallets ----
            (new Vector3(98f, 0.5f, -105f), 0f),       // near pallet area
            (new Vector3(112f, 0.5f, -125f), 90f),     // near construction crates
            (new Vector3(120f, 0.5f, -148f), 0f),      // near large crate
            (new Vector3(130f, 0.5f, -160f), 180f),    // near construction wreck

            // ---- Ch4 Residential — near houses, checkpoint, alleys ----
            (new Vector3(-16f, 0.5f, -145f), 0f),      // at quarantine checkpoint
            (new Vector3(25f, 0.5f, -95f), 90f),       // near residential house
            (new Vector3(45f, 0.5f, -125f), 0f),       // near dumpster
            (new Vector3(55f, 0.5f, -170f), 45f),      // near road barrier
            (new Vector3(35f, 0.5f, -200f), 90f),      // near save room approach
            (new Vector3(52f, 0.5f, -225f), 0f),       // near residential wreck

            // ---- Ch5 Apartment Bridge — near apartments, bridge ----
            (new Vector3(85f, 0.5f, 28f), 0f),         // near apartment wrecks
            (new Vector3(92f, 0.5f, 40f), 90f),        // along bridge approach
            (new Vector3(100f, 0.5f, 50f), 0f),        // near bridge road
            (new Vector3(45f, 0.5f, 45f), 45f),        // near Ch5 apartment
            (new Vector3(15f, 0.5f, 55f), 90f),        // inside apartment area
            (new Vector3(-8f, 0.5f, 20f), 0f),         // near bomb area
            (new Vector3(-15f, 0.5f, 5f), 180f),       // near escape route
        };

        int placed = 0;
        for (int i = 0; i < placements.Length; i++)
        {
            var (pos, rotY) = placements[i];
            var name = $"Crate_{i + 1:00}";
            if (FindChild(container, name) != null) continue;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            placed++;
        }
        Debug.Log($"[StoryBarrelCratePlacer] Placed {placed} crates (total slots: {placements.Length}).");
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
        child.transform.localPosition = Vector3.zero;
        return child;
    }

    private static GameObject FindChild(GameObject parent, string name)
    {
        for (int i = 0; i < parent.transform.childCount; i++)
        {
            var child = parent.transform.GetChild(i);
            if (child.name == name) return child.gameObject;
        }
        return null;
    }
}
