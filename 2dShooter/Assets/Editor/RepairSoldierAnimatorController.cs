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
        // Rebuild the legacy hand-serialized controller when the editor reloads this script.
        EditorApplication.delayCall += AutoRepairBrokenController;
    }

    [MenuItem("Tools/2dShooter/Repair Soldier Animator Controller")]
    private static void ManualRepair()
    {
        RebuildController();
    }

    private static void AutoRepairBrokenController()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string fullPath = Path.GetFullPath(ControllerPath);
        if (!File.Exists(fullPath))
            return;

        try
        {
            string serialized = File.ReadAllText(fullPath);
            if (!serialized.Contains("1234567801"))
                return;

            RebuildController();
        }
        catch (Exception exception)
        {
            Debug.LogError("[Soldier Animator] Автоматическое восстановление не удалось: " + exception);
        }
    }

    private static void RebuildController()
    {
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
        AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkPath);
        AnimationClip reloadClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ReloadPath);
        AnimationClip fireClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FirePath);

        if (idleClip == null || walkClip == null || reloadClip == null || fireClip == null)
        {
            Debug.LogError(
                "[Soldier Animator] Не удалось восстановить контроллер: один или несколько клипов не импортированы. " +
                "Проверь ошибки импорта в Console.");
            return;
        }

        string fullControllerPath = Path.GetFullPath(ControllerPath);
        string metaPath = fullControllerPath + ".meta";

        try
        {
            // Delete the corrupted asset but restore its original meta/GUID before recreating it.
            AssetDatabase.DeleteAsset(ControllerPath);
            File.WriteAllText(metaPath, ControllerMeta);

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            if (controller == null)
            {
                Debug.LogError("[Soldier Animator] Unity не смогла создать новый контроллер.");
                return;
            }

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
            AssetDatabase.Refresh();

            Debug.Log(
                "[Soldier Animator] Контроллер пересоздан штатным API Unity. GUID сохранён.");
        }
        catch (Exception exception)
        {
            Debug.LogError("[Soldier Animator] Ошибка при пересоздании контроллера: " + exception);
        }
    }
}
#endif
