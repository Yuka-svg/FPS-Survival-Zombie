using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// One-shot editor utility that builds the companion NPC prefab from the
/// existing SM_Chr_Biker_Male_01 character model. Adds all required gameplay
/// components (NavMeshAgent, CompanionAI, DialogueBubble, etc.) and saves the
/// prefab to Assets/Resources/Companion/BikerSurvivor.prefab so
/// CompanionManager can load it at runtime via Resources.Load.
///
/// Run via the menu: Tools/Story/Build Companion Prefab. Safe to re-run
/// (idempotent — updates the existing prefab in place).
/// </summary>
public static class CompanionPrefabBuilder
{
    private const string SourcePrefabPath =
        "Assets/Map/PolygonApocalypse/Prefabs/Characters/SM_Chr_Biker_Male_01.prefab";
    private const string OutputFolder = "Assets/Resources/Companion";
    private const string OutputPrefabPath = OutputFolder + "/BikerSurvivor.prefab";

    [MenuItem("Tools/Story/Build Companion Prefab")]
    public static void Build()
    {
        EnsureFolder("Assets/Resources", "Companion");

        var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (sourcePrefab == null)
        {
            Debug.LogError("[CompanionPrefabBuilder] Source prefab not found: " + SourcePrefabPath);
            return;
        }

        // Instantiate temporarily in the scene to add components.
        var tempGO = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
        tempGO.name = "BikerSurvivor";

        // Set layer to Interactable (9) so InteractManager can detect it.
        tempGO.layer = LayerMask.NameToLayer("Interactable");

        // Add a CapsuleCollider (trigger) for interaction detection if none exists.
        // Made larger (radius 0.6, height 2.0) so it's easier to aim at.
        var cap = tempGO.GetComponent<CapsuleCollider>();
        if (cap == null)
        {
            cap = tempGO.AddComponent<CapsuleCollider>();
        }
        cap.isTrigger = true;
        cap.height = 2.0f;
        cap.radius = 0.6f;
        cap.center = new Vector3(0f, 1.0f, 0f);

        // Add a non-trigger CapsuleCollider on a child for physics? No — the
        // companion doesn't need to block physics. The trigger collider is
        // enough for InteractManager raycast detection.

        // NavMeshAgent (required by CompanionAI).
        var agent = tempGO.GetComponent<NavMeshAgent>();
        if (agent == null) agent = tempGO.AddComponent<NavMeshAgent>();
        agent.height = 1.8f;
        agent.radius = 0.4f;
        agent.baseOffset = 0f;
        agent.speed = 3.5f;
        agent.acceleration = 8f; // Smooth acceleration (L4D2 style)
        agent.angularSpeed = 360f; // Turn quickly to face enemies
        agent.stoppingDistance = 3f; // Don't crowd the player
        agent.autoBraking = true;

        // AudioSource (required by CompanionAI).
        var audio = tempGO.GetComponent<AudioSource>();
        if (audio == null) audio = tempGO.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.minDistance = 1f;
        audio.maxDistance = 30f;

        // Rigidbody (kinematic — needed for some collision events).
        var rb = tempGO.GetComponent<Rigidbody>();
        if (rb == null) rb = tempGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // CompanionAI.
        var ai = tempGO.GetComponent<CompanionAI>();
        if (ai == null) ai = tempGO.AddComponent<CompanionAI>();
        // Try to grab the Animator from the child mesh.
        if (ai.animator == null)
            ai.animator = tempGO.GetComponentInChildren<Animator>();

        // Assign the Companion AnimatorController (built by CompanionAnimatorBuilder).
        if (ai.animator != null)
        {
            var companionController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animation/Companion/CompanionAnimatorController.controller");
            if (companionController != null)
            {
                ai.animator.runtimeAnimatorController = companionController;
                ai.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // CRITICAL: Disable root motion — NavMeshAgent controls position.
                // Root motion from Mixamo animations causes the model to sink into the ground.
                ai.animator.applyRootMotion = false;
                Debug.Log("[CompanionPrefabBuilder] Assigned CompanionAnimatorController.");
            }
            else
            {
                Debug.LogWarning("[CompanionPrefabBuilder] CompanionAnimatorController not found. Run Tools/Story/Build Companion Animator first.");
            }

            // Attach shotgun model to the right hand bone so the companion
            // visibly holds a weapon (matches the rifle animation poses).
            AttachShotgunToHand(ai.animator);
        }

        // DialogueBubble.
        if (tempGO.GetComponent<DialogueBubble>() == null)
            tempGO.AddComponent<DialogueBubble>();

        // CompanionDialogueTrigger (cowsins.Interactable subclass).
        var trigger = tempGO.GetComponent<CompanionDialogueTrigger>();
        if (trigger == null) trigger = tempGO.AddComponent<CompanionDialogueTrigger>();
        trigger.interactText = "Nói chuyện";

        // CompanionHealthBar.
        if (tempGO.GetComponent<CompanionHealthBar>() == null)
            tempGO.AddComponent<CompanionHealthBar>();

        // CompanionRescueUI (world-space prompt + progress bar shown while Downed).
        if (tempGO.GetComponent<CompanionRescueUI>() == null)
            tempGO.AddComponent<CompanionRescueUI>();

        // Create a simple muzzle flash + tracer VFX prefab and assign it.
        var muzzlePrefab = CreateMuzzleFlashPrefab();
        ai.muzzleFlashPrefab = muzzlePrefab;

        // Assign a shotgun fire sound if available.
        var shotgunFireClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Engine/Cowsins/SFX/Weapons/Shotgun/Shotgun_Fire_SFX.wav");
        if (shotgunFireClip == null)
        {
            // Try alternate path.
            var guids = AssetDatabase.FindAssets("Shotgun_Fire t:AudioClip");
            if (guids.Length > 0)
                shotgunFireClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (shotgunFireClip != null)
        {
            ai.shootClip = shotgunFireClip;
            Debug.Log("[CompanionPrefabBuilder] Assigned shotgun fire sound.");
        }
        else
        {
            Debug.LogWarning("[CompanionPrefabBuilder] Shotgun fire sound not found — companion will fire silently.");
        }

        // Save as a prefab (overwrite if exists).
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath);
        if (existing != null)
        {
            PrefabUtility.SaveAsPrefabAsset(tempGO, OutputPrefabPath);
            Debug.Log("[CompanionPrefabBuilder] Updated existing prefab: " + OutputPrefabPath);
        }
        else
        {
            PrefabUtility.SaveAsPrefabAsset(tempGO, OutputPrefabPath);
            Debug.Log("[CompanionPrefabBuilder] Created new prefab: " + OutputPrefabPath);
        }

        // Clean up the temporary scene instance.
        Object.DestroyImmediate(tempGO);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CompanionPrefabBuilder] Companion prefab build complete.");
    }

    /// <summary>
    /// Adds a CompanionManager to the active scene if one doesn't already exist.
    /// The manager is a singleton (DontDestroyOnLoad) that spawns the companion
    /// after Chapter 2 and orchestrates the dialogue/skip logic.
    /// </summary>
    [MenuItem("Tools/Story/Setup Companion Manager")]
    public static void SetupManager()
    {
        var existing = Object.FindFirstObjectByType<CompanionManager>();
        if (existing != null)
        {
            Debug.Log("[CompanionPrefabBuilder] CompanionManager already exists in scene.");
            return;
        }

        var go = new GameObject("CompanionManager");
        go.AddComponent<CompanionManager>();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("[CompanionPrefabBuilder] CompanionManager added to scene. Save the scene to persist.");
    }

    private static void EnsureFolder(string parent, string folder)
    {
        string path = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }

    /// <summary>
    /// Attaches a shotgun model to the companion's right hand bone.
    /// Searches for common Mixamo/Polygon hand bone names and parents the
    /// shotgun prefab under it with an offset that matches a 2-handed rifle grip.
    /// </summary>
    private static void AttachShotgunToHand(Animator animator)
    {
        if (animator == null) return;

        // Common right-hand bone names in Mixamo / Polygon humanoid rigs.
        string[] handBoneNames = { "mixamorig:RightHand", "RightHand", "rightHand", "Hand_R", "R_Hand" };
        Transform rightHand = null;
        foreach (var name in handBoneNames)
        {
            rightHand = animator.transform.Find(name);
            if (rightHand == null)
                rightHand = FindDeepChild(animator.transform, name);
            if (rightHand != null) break;
        }

        if (rightHand == null)
        {
            // Fallback: use Animator GetBoneTransform.
            var boneTransform = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (boneTransform != null)
                rightHand = boneTransform;
        }

        if (rightHand == null)
        {
            Debug.LogWarning("[CompanionPrefabBuilder] Right hand bone not found — shotgun will not be attached.");
            return;
        }

        // Remove existing shotgun child if present (for re-runs).
        var existing = rightHand.Find("CompanionShotgun");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        // Load shotgun prefab. Try PolygonApocalypse shotgun first, then Cowsins.
        GameObject shotgunPrefab = null;
        string[] shotgunPaths = {
            "Assets/Map/PolygonApocalypse/Prefabs/Weapons/Guns/SM_Wep_Shotgun_01.prefab",
            "Assets/Engine/Cowsins/Models/Weapons/Shotgun.fbx",
        };
        foreach (var p in shotgunPaths)
        {
            shotgunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (shotgunPrefab != null) break;
        }

        if (shotgunPrefab == null)
        {
            // Search by name.
            var guids = AssetDatabase.FindAssets("Shotgun t:Prefab t:Model");
            if (guids.Length > 0)
                shotgunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (shotgunPrefab == null)
        {
            Debug.LogWarning("[CompanionPrefabBuilder] Shotgun prefab/model not found — companion will have empty hands.");
            return;
        }

        // Instantiate the shotgun as a child of the right hand.
        var shotgunGO = (GameObject)PrefabUtility.InstantiatePrefab(shotgunPrefab);
        shotgunGO.name = "CompanionShotgun";
        shotgunGO.transform.SetParent(rightHand, false);

        // The SM_Wep_Shotgun_01 mesh is 0.617m long along local Z, with the
        // grip at z≈0 and the barrel tip at z≈0.617. The Mixamo "idle aiming"
        // pose has the right hand rotated so that the palm does NOT simply
        // face +Z — the hand is tilted. We computed the local rotation that
        // makes the gun's forward (barrel) match the chest's forward and the
        // gun's up match world up, so the gun sits naturally in the aiming
        // pose with the left hand reaching the forestock at z≈0.39.
        shotgunGO.transform.localPosition = new Vector3(0f, 0f, 0f);
        shotgunGO.transform.localRotation = Quaternion.Euler(335.8f, 66.5f, 255.4f);
        shotgunGO.transform.localScale = new Vector3(1f, 1f, 1f);

        // Set layer to match companion (Interactable) so it doesn't cause issues.
        SetLayerRecursive(shotgunGO, rightHand.gameObject.layer);

        Debug.Log("[CompanionPrefabBuilder] Shotgun attached to right hand bone: " + rightHand.name);
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name) return child;
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
    }

    /// <summary>
    /// Creates a simple muzzle flash VFX prefab (a glowing quad + point light
    /// that auto-destroys after 0.1s). Saved to Assets/Resources/Companion/MuzzleFlash.prefab.
    /// </summary>
    private static GameObject CreateMuzzleFlashPrefab()
    {
        const string path = "Assets/Resources/Companion/MuzzleFlash.prefab";

        // Check if it already exists.
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        // Build a temporary GameObject with a simple particle-like effect.
        var tempGO = new GameObject("MuzzleFlash");

        // Add a small point light for a flash effect.
        var light = tempGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.8f, 0.3f, 1f);
        light.intensity = 3f;
        light.range = 5f;

        // Add a simple auto-destroy component (since we can't easily make a
        // ParticleSystem in code for a prefab, we use a light flash + destroy).
        // The CompanionAI already destroys the prefab after 1s, so we just need
        // the light to fade. We'll add a tiny script via a MonoScript? No —
        // simpler: just let CompanionAI.Destroy(fx, 1f) handle cleanup.

        // Save as prefab.
        var prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, path);
        Object.DestroyImmediate(tempGO);
        Debug.Log("[CompanionPrefabBuilder] MuzzleFlash prefab created at: " + path);
        return prefab;
    }
}
