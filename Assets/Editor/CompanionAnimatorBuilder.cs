using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds the AnimatorController for the companion NPC using animations from
/// the "Lite Rifle Pack" (Mixamo — created for the PolygonApocalypse Biker
/// character, so retargeting is perfect).
///
/// States (7 states, L4D2-style smooth transitions, same pattern as Zombie controller):
///   Idle    — BlendTree: idle aiming ↔ rifle walk ↔ run forward (Speed 0..1)
///   Move    — (covered by BlendTree at Speed 0.5)
///   Run     — (covered by BlendTree at Speed 1.0)
///   Shoot   — Firing Rifle (1-shot, via CrossFade from CompanionAI)
///   Hurt    — Hit Reaction (1-shot, via CrossFade from CompanionAI)
///   Downed  — idle crouching (loop, incapacitated, sits down waiting for revive)
///   Reload  — Reloading (1-shot, via CrossFade from CompanionAI)
///   Revive  — turn 90 left (1-shot, stands back up)
///
/// Parameters:
///   Speed    (float)   — 0..1, drives Idle/Walk/Run blend
///   Downed   (bool)    — true when companion is downed
///   Revive   (trigger) — fires revive from Downed
///
/// Run via menu: Tools/Story/Build Companion Animator
/// </summary>
public static class CompanionAnimatorBuilder
{
    private const string OutputFolder = "Assets/Animation/Companion";
    private const string OutputPath = OutputFolder + "/CompanionAnimatorController.controller";

    // Lite Rifle Pack paths (Mixamo — perfect retarget for Biker).
    private const string IdleFbx       = "Assets/Animation/Lite Rifle Pack/idle aiming.fbx";
    private const string WalkFbx       = "Assets/Animation/Lite Rifle Pack/Characters@Rifle Walk (1).fbx";
    private const string RunForwardFbx = "Assets/Animation/Lite Rifle Pack/run forward.fbx";
    private const string ShootFbx      = "Assets/Animation/Lite Rifle Pack/Characters@Firing Rifle.fbx";
    private const string HitFbx        = "Assets/Animation/Lite Rifle Pack/Characters@Hit Reaction.fbx";
    private const string DownedFbx     = "Assets/Animation/Lite Rifle Pack/idle crouching.fbx";
    private const string ReloadFbx     = "Assets/Animation/Lite Rifle Pack/Characters@Reloading.fbx";
    private const string ReviveFbx     = "Assets/Animation/Lite Rifle Pack/turn 90 left.fbx";

    [MenuItem("Tools/Story/Build Companion Animator")]
    public static void Build()
    {
        EnsureFolder("Assets/Animation", "Companion");

        // Load animation clips.
        var idleClip       = LoadClip(IdleFbx);
        var walkClip       = LoadClip(WalkFbx);
        var runForwardClip = LoadClip(RunForwardFbx);
        var shootClip      = LoadClip(ShootFbx);
        var hitClip        = LoadClip(HitFbx);
        var downedClip     = LoadClip(DownedFbx);
        var reloadClip     = LoadClip(ReloadFbx);
        var reviveClip     = LoadClip(ReviveFbx);

        if (idleClip == null || walkClip == null || runForwardClip == null)
        {
            Debug.LogError("[CompanionAnimatorBuilder] One or more required locomotion clips not found.");
            return;
        }

        // Ensure looping clips loop.
        EnsureLoop(IdleFbx);
        EnsureLoop(WalkFbx);
        EnsureLoop(RunForwardFbx);
        EnsureLoop(DownedFbx);

        // Delete existing controller and create fresh.
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(OutputPath) != null)
            AssetDatabase.DeleteAsset(OutputPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(OutputPath);

        // ---- Parameters ----
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Downed", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Revive", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        // ---- Idle state with 3-child BlendTree (same as Zombie) ----
        var idleState = sm.AddState("Idle");
        var blendTree = new BlendTree();
        AssetDatabase.AddObjectToAsset(blendTree, controller);
        blendTree.name = "LocomotionBlend";
        blendTree.blendParameter = "Speed";
        blendTree.AddChild(idleClip, 0f);           // Speed 0 -> Idle
        blendTree.AddChild(walkClip, 0.5f);         // Speed 0.5 -> Walk
        blendTree.AddChild(runForwardClip, 1f);     // Speed 1 -> Run
        blendTree.useAutomaticThresholds = false;
        blendTree.minThreshold = 0f;
        blendTree.maxThreshold = 1f;
        blendTree.blendType = BlendTreeType.Simple1D;
        idleState.motion = blendTree;

        // ---- Shoot state (1-shot, triggered via CrossFade from CompanionAI) ----
        var shootState = sm.AddState("Shoot");
        shootState.motion = shootClip;

        // ---- Hit state (1-shot, triggered via CrossFade from CompanionAI) ----
        var hitState = sm.AddState("Hit");
        hitState.motion = hitClip != null ? hitClip : idleClip;

        // ---- Downed state (loop, incapacitated, sits down waiting for revive) ----
        var downedState = sm.AddState("Downed");
        downedState.motion = downedClip != null ? downedClip : idleClip;

        // ---- Reload state (1-shot, triggered via CrossFade from CompanionAI) ----
        var reloadState = sm.AddState("Reload");
        reloadState.motion = reloadClip != null ? reloadClip : idleClip;

        // ---- Revive state (1-shot, stands back up) ----
        var reviveState = sm.AddState("Revive");
        reviveState.motion = reviveClip != null ? reviveClip : idleClip;

        // ---- Default state ----
        sm.defaultState = idleState;

        // ===== TRANSITIONS =====

        // ---- Shoot -> Idle (return after shoot, via exitTime) ----
        // exitTime=1.0 lets the full 0.27s Firing Rifle clip play before
        // transitioning; duration=0.25 gives a smooth blend back to idle.
        var t = shootState.AddTransition(idleState);
        t.hasExitTime = true; t.exitTime = 1.0f; t.duration = 0.25f;

        // ---- Hit -> Idle (return after hit) ----
        t = hitState.AddTransition(idleState);
        t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.25f;

        // ---- Reload -> Idle (return after reload) ----
        t = reloadState.AddTransition(idleState);
        t.hasExitTime = true; t.exitTime = 1.0f; t.duration = 0.25f;

        // ---- Downed (AnyState, only when Downed bool is true) ----
        var anyToDowned = sm.AddAnyStateTransition(downedState);
        anyToDowned.AddCondition(AnimatorConditionMode.If, 0, "Downed");
        anyToDowned.hasExitTime = false;
        anyToDowned.duration = 0.2f;
        anyToDowned.canTransitionToSelf = false;

        // ---- Downed -> Revive -> Idle (via Revive trigger + Downed=false) ----
        t = downedState.AddTransition(reviveState);
        t.AddCondition(AnimatorConditionMode.If, 0, "Revive");
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "Downed");
        t.hasExitTime = false; t.duration = 0.3f;

        t = reviveState.AddTransition(idleState);
        t.hasExitTime = true; t.exitTime = 0.8f; t.duration = 0.3f;

        // Enable IK pass on layer 0 so OnAnimatorIK is called (for left-hand grip).
        // NOTE: AnimatorControllerLayer is a struct, so we must reassign the whole array.
        var layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CompanionAnimatorBuilder] Companion AnimatorController built with 7 states (Idle/Move/Run/Shoot/Hurt/Downed/Reload + Revive) + 3-child BlendTree + IK pass at: " + OutputPath);
    }

    /// <summary>Loads the non-preview AnimationClip from an FBX.</summary>
    private static AnimationClip LoadClip(string fbxPath)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        AnimationClip best = null;
        foreach (var a in clips)
        {
            if (a is AnimationClip ac)
            {
                if (ac.name.StartsWith("__preview__")) continue;
                best = ac;
            }
        }
        if (best == null)
            Debug.LogWarning("[CompanionAnimatorBuilder] No animation clip found in: " + fbxPath);
        return best;
    }

    /// <summary>Ensures an FBX clip is set to loop via ModelImporter.</summary>
    private static void EnsureLoop(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = new ModelImporterClipAnimation[1];
            clips[0] = new ModelImporterClipAnimation();
            clips[0].name = "mixamo.com";
            clips[0].takeName = "mixamo.com";
            clips[0].firstFrame = 0;
            clips[0].lastFrame = 100;
        }
        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (!clips[i].loopTime) { clips[i].loopTime = true; changed = true; }
        }
        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static void EnsureFolder(string parent, string folder)
    {
        string path = parent + "/" + folder;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, folder);
    }
}
