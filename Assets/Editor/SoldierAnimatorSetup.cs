using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;

/// <summary>
/// Editor utility to create placeholder animations and AnimatorController
/// for the Soldier_Knight prefab using the existing sprite.
/// </summary>
public static class SoldierAnimatorSetup
{
    const string AnimFolder = "Assets/Art/Soldiers/Animations";
    const string ControllerPath = "Assets/Art/Soldiers/Animations/SoldierKnight.controller";
    const string IdleClipPath = "Assets/Art/Soldiers/Animations/SoldierKnight_Idle.anim";
    const string RunClipPath = "Assets/Art/Soldiers/Animations/SoldierKnight_FrontRun.anim";
    const string PrefabPath = "Assets/Prefabs/Soldiers/Soldier_Knight.prefab";

    [MenuItem("Tools/Soldier Setup/Create Animator + Animations")]
    public static void CreateAll()
    {
        // Ensure folder exists
        if (!AssetDatabase.IsValidFolder(AnimFolder))
        {
            var fullPath = Path.Combine(Application.dataPath, "Art/Soldiers/Animations");
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
            AssetDatabase.Refresh();
        }

        CreateIdleClip();
        CreateRunClip();
        CreateAnimatorController();
        AssignToPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SoldierAnimatorSetup] All animations and controller created successfully.");
    }

    static void SetClipLoopTime(AnimationClip clip)
    {
        // Set loopTime via SerializedObject on the saved asset
        var serializedObj = new SerializedObject(clip);
        var loopTimeProp = serializedObj.FindProperty("m_LoopTime");
        if (loopTimeProp != null)
        {
            loopTimeProp.boolValue = true;
            serializedObj.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("[SoldierAnimatorSetup] Could not find m_LoopTime property on clip.");
        }
    }

    static void CreateIdleClip()
    {
        // Idle: subtle scale pulse only — NO position/rotation curves
        // (position curves override transform.position every frame, breaking movement)
        var clip = new AnimationClip();
        clip.name = "SoldierKnight_Idle";

        // Scale pulse — all 3 axes, base 0.2 to match prefab scale (anim curves override transform)
        const float s = 0.2f;
        var scaleCurve = new AnimationCurve(
            new Keyframe(0f, s), new Keyframe(0.5f, s * 1.02f), new Keyframe(1f, s)
        );
        clip.SetCurve("", typeof(Transform), "m_LocalScale.x", scaleCurve);
        clip.SetCurve("", typeof(Transform), "m_LocalScale.y", scaleCurve);
        clip.SetCurve("", typeof(Transform), "m_LocalScale.z", new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f)));

        AssetDatabase.CreateAsset(clip, IdleClipPath);
        SetClipLoopTime(clip);
        EditorUtility.SetDirty(clip);
        Debug.Log($"[SoldierAnimatorSetup] Created idle clip: {IdleClipPath}");
    }

    static void CreateRunClip()
    {
        // Run: scale bounce only — NO position/rotation curves
        // (position curves override transform.position every frame, breaking movement)
        var clip = new AnimationClip();
        clip.name = "SoldierKnight_FrontRun";

        // Scale bounce — all 3 axes, base 0.2 to match prefab scale
        const float s = 0.2f;
        clip.SetCurve("", typeof(Transform), "m_LocalScale.x", new AnimationCurve(
            new Keyframe(0f, s), new Keyframe(0.5f, s), new Keyframe(1f, s)));
        clip.SetCurve("", typeof(Transform), "m_LocalScale.y", new AnimationCurve(
            new Keyframe(0f, s), new Keyframe(0.25f, s * 1.05f), new Keyframe(0.5f, s * 0.98f),
            new Keyframe(0.75f, s * 1.05f), new Keyframe(1f, s)));
        clip.SetCurve("", typeof(Transform), "m_LocalScale.z", new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f)));

        AssetDatabase.CreateAsset(clip, RunClipPath);
        SetClipLoopTime(clip);
        EditorUtility.SetDirty(clip);
        Debug.Log($"[SoldierAnimatorSetup] Created run clip: {RunClipPath}");
    }

    static void CreateAnimatorController()
    {
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.name = "SoldierKnight";

        // Add parameters
        controller.AddParameter("Run", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Idle", AnimatorControllerParameterType.Bool);

        // Load clips
        var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        var runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RunClipPath);

        // Get root state machine
        var rootStateMachine = controller.layers[0].stateMachine;

        // Create states
        var idleState = rootStateMachine.AddState("Idle");
        idleState.motion = idleClip;

        var runState = rootStateMachine.AddState("FrontRun");
        runState.motion = runClip;

        // Set idle as default
        rootStateMachine.defaultState = idleState;

        // Idle -> FrontRun transition (on Run trigger)
        var idleToRun = idleState.AddTransition(runState);
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.15f;
        idleToRun.AddCondition(AnimatorConditionMode.If, 0, "Run");

        // FrontRun -> Idle transition (on Idle trigger)
        var runToIdle = runState.AddTransition(idleState);
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.15f;
        runToIdle.AddCondition(AnimatorConditionMode.If, 0, "Idle");

        AssetDatabase.SaveAssets();
        Debug.Log($"[SoldierAnimatorSetup] Created animator controller: {ControllerPath}");
    }

    static void AssignToPrefab()
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("[SoldierAnimatorSetup] Failed to load animator controller!");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[SoldierAnimatorSetup] Prefab not found at {PrefabPath}");
            return;
        }

        // Add Animator component if missing, assign controller
        var animator = prefab.GetComponent<Animator>();
        if (animator == null)
        {
            animator = prefab.AddComponent<Animator>();
            Debug.Log("[SoldierAnimatorSetup] Added Animator component to prefab.");
        }
        animator.runtimeAnimatorController = controller;

        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
        Debug.Log($"[SoldierAnimatorSetup] Assigned animator controller to {PrefabPath}");
    }
}
