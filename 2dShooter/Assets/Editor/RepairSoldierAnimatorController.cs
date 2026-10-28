#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class RepairSoldierAnimatorController
{
    private const string ControllerPath = "Assets/Animations/SoldierAnimator.controller";
    private const string ControllerGuid = "a5555555555545558555555555555555";
    private const string IdlePath = "Assets/Animations/SoldierIdle.anim";
    private const string WalkPath = "Assets/Animations/SoldierWalk.anim";
    private const string ReloadPath = "Assets/Animations/SoldierReload.anim";
    private const string FirePath = "Assets/Animations/SoldierFire.anim";

    private const string ControllerMeta =
        "fileFormatVersion: 2\n" +
        "guid: " + ControllerGuid + "\n" +
        "NativeFormatImporter:\n" +
        "  externalObjects: {}\n" +
        "  mainObjectFileID: 9100000\n" +
        "  userData:\n" +
        "  assetBundleName:\n" +
        "  assetBundleVariant:\n";

    static RepairSoldierAnimatorController()
    {
        // Unity automatically repairs the controller once this Editor script compiles.
        EditorApplication.delayCall += AutoRepairBrokenController;
    }

    [MenuItem("Tools/2dShooter/Repair Soldier Animator Controller")]
    private static void ManualRepair()
    {
        RebuildController();
    }

    private static string AbsoluteAssetPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void AutoRepairBrokenController()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string fullPath = AbsoluteAssetPath(ControllerPath);
        if (!File.Exists(fullPath))
            return;

        try
        {
            string serialized = File.ReadAllText(fullPath);
            // These IDs are only present in the manually serialized broken controller.
            if (!serialized.Contains("1234567801"))
                return;

            RebuildController();
        }
        catch (Exception exception)
        {
            Debug.LogError("[Soldier Animator] Автоматическое восстановление не удалось: " + exception);
        }
    }

    private static AnimationCurve Curve(params float[] timeValuePairs)
    {
        Keyframe[] keys = new Keyframe[timeValuePairs.Length / 2];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]);

        return new AnimationCurve(keys);
    }

    private static AnimationClip RebuildClip(
        string assetPath,
        string clipName,
        bool loop,
        AnimationCurve positionY,
        AnimationCurve scaleX,
        AnimationCurve scaleY)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

        if (clip == null)
        {
            // A broken hand-written .anim is replaced with an asset created by Unity itself.
            AssetDatabase.DeleteAsset(assetPath);
            string absolutePath = AbsoluteAssetPath(assetPath);
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
            if (File.Exists(absolutePath + ".meta"))
                File.Delete(absolutePath + ".meta");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            clip = new AnimationClip { name = clipName, frameRate = 60f };
            AssetDatabase.CreateAsset(clip, assetPath);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            AnimationUtility.SetEditorCurve(clip, binding, null);

        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"),
            positionY);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.x"),
            scaleX);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.y"),
            scaleY);

        clip.name = clipName;
        clip.frameRate = 60f;
        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void RebuildController()
    {
        string fullControllerPath = AbsoluteAssetPath(ControllerPath);
        string metaPath = fullControllerPath + ".meta";

        try
        {
            // Rebuild every clip through Unity APIs so malformed hand-written YAML is not reused.
            AnimationClip idleClip = RebuildClip(
                IdlePath, "Idle", true,
                Curve(0f, 0f, 0.4f, 0.018f, 0.8f, 0f),
                Curve(0f, 1f, 0.4f, 1.015f, 0.8f, 1f),
                Curve(0f, 1f, 0.4f, 0.99f, 0.8f, 1f));

            AnimationClip walkClip = RebuildClip(
                WalkPath, "Walk", true,
                Curve(0f, 0f, 0.1f, 0.07f, 0.2f, 0.008f, 0.3f, 0.07f, 0.4f, 0f),
                Curve(0f, 1f, 0.1f, 0.94f, 0.2f, 1f, 0.3f, 0.94f, 0.4f, 1f),
                Curve(0f, 1f, 0.1f, 1.07f, 0.2f, 0.96f, 0.3f, 1.07f, 0.4f, 1f));

            AnimationClip reloadClip = RebuildClip(
                ReloadPath, "Reload", false,
                Curve(0f, 0f, 0.25f, -0.035f, 0.75f, -0.02f, 1.1f, 0f),
                Curve(0f, 1f, 0.25f, 1.06f, 0.75f, 1.025f, 1.1f, 1f),
                Curve(0f, 1f, 0.25f, 0.84f, 0.75f, 0.92f, 1.1f, 1f));

            AnimationClip fireClip = RebuildClip(
                FirePath, "Fire", false,
                Curve(0f, 0f, 0.035f, -0.025f, 0.12f, 0f),
                Curve(0f, 1f, 0.035f, 0.82f, 0.12f, 1f),
                Curve(0f, 1f, 0.035f, 1.1f, 0.12f, 1f));

            // Remove the corrupted controller and preserve its GUID for scene references.
            AssetDatabase.DeleteAsset(ControllerPath);
            // Delete the physical file too: a controller with a broken importer can survive
            // AssetDatabase.DeleteAsset on some Unity versions.
            if (File.Exists(fullControllerPath))
                File.Delete(fullControllerPath);
            if (File.Exists(metaPath))
                File.Delete(metaPath);
            File.WriteAllText(metaPath, ControllerMeta);

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            if (controller == null)
                throw new InvalidOperationException("Unity вернула пустой AnimatorController.");

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in stateMachine.states)
                stateMachine.RemoveState(child.state);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Reloading", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);

            AnimatorState idle = stateMachine.AddState("Idle");
            idle.motion = idleClip;
            AnimatorState walk = stateMachine.AddState("Walk");
            walk.motion = walkClip;
            AnimatorState reload = stateMachine.AddState("Reload");
            reload.motion = reloadClip;
            AnimatorState fire = stateMachine.AddState("Fire");
            fire.motion = fireClip;
            stateMachine.defaultState = idle;

            AnimatorStateTransition idleToWalk = idle.AddTransition(walk);
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            idleToWalk.duration = 0.08f;

            AnimatorStateTransition walkToIdle = walk.AddTransition(idle);
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            walkToIdle.duration = 0.08f;

            AnimatorStateTransition anyToReload = stateMachine.AddAnyStateTransition(reload);
            anyToReload.AddCondition(AnimatorConditionMode.If, 0f, "Reloading");
            anyToReload.hasExitTime = false;
            anyToReload.duration = 0.04f;

            AnimatorStateTransition reloadToIdle = reload.AddTransition(idle);
            reloadToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Reloading");
            reloadToIdle.hasExitTime = false;
            reloadToIdle.duration = 0.05f;

            AnimatorStateTransition anyToFire = stateMachine.AddAnyStateTransition(fire);
            anyToFire.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
            anyToFire.hasExitTime = false;
            anyToFire.duration = 0.025f;

            AnimatorStateTransition fireToIdle = fire.AddTransition(idle);
            fireToIdle.hasExitTime = true;
            fireToIdle.exitTime = 0.95f;
            fireToIdle.duration = 0.04f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log(
                "[Soldier Animator] Готово: клипы и контроллер пересозданы средствами Unity. " +
                "GUID контроллера сохранён; проблемные переходы из старого файла удалены.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[Soldier Animator] Ошибка при пересоздании контроллера: " + exception);
        }
    }
}
#endif
