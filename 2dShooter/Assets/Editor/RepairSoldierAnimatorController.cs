#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RepairSoldierAnimatorController
{
    private const string ControllerPath = "Assets/Animations/SoldierAnimator.controller";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/2dShooter/Repair Soldier Animator Controller")]
    private static void RebuildController()
    {
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/SoldierIdle.anim");
        AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/SoldierWalk.anim");
        AnimationClip reloadClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/SoldierReload.anim");
        AnimationClip fireClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/SoldierFire.anim");

        if (idleClip == null || walkClip == null || reloadClip == null || fireClip == null)
        {
            Debug.LogError(
                "Не удалось восстановить анимации: один или несколько AnimationClip не импортированы. " +
                "Проверь Console на ошибки импорта файлов в Assets/Animations.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        GameObject enemy = GameObject.Find("Enemy");

        if (!scene.IsValid() || player == null || enemy == null)
        {
            Debug.LogError(
                "Не удалось найти player и Enemy в SampleScene. Контроллер не был заменён.");
            return;
        }

        // Remove the broken serialized asset unconditionally before creating a fresh one.
        // This avoids relying on Unity being able to load the malformed controller first.
        AssetDatabase.DeleteAsset(ControllerPath);
        AssetDatabase.Refresh();

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
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

        AssignController(player, controller);
        AssignController(enemy, controller);

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);

        Debug.Log("SoldierAnimator.controller восстановлен через Unity Editor API и назначен игроку и врагу.");
    }

    private static void AssignController(GameObject character, AnimatorController controller)
    {
        Animator[] animators = character.GetComponentsInChildren<Animator>(true);
        if (animators.Length == 0)
        {
            Debug.LogWarning("У объекта '" + character.name + "' не найден Animator.", character);
            return;
        }

        foreach (Animator animator in animators)
        {
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
        }
    }
}
#endif
