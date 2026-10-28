using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class CharacterMotionFeedback : MonoBehaviour
{
    [Header("Animator")]
    [InspectorLabel("Аниматор персонажа", "Компонент Animator на дочернем объекте с изображением персонажа. На нём должен быть назначен SoldierAnimator.controller.")]
    [SerializeField] private Animator animator;

    [InspectorLabel("Имя параметра скорости", "Имя Float-параметра в Animator Controller, который переключает Idle и Walk.")]
    [SerializeField] private string speedParameter = "Speed";

    [InspectorLabel("Имя параметра перезарядки", "Имя Bool-параметра в Animator Controller, который запускает и завершает Reload.")]
    [SerializeField] private string reloadParameter = "Reloading";

    [InspectorLabel("Имя триггера выстрела", "Имя Trigger-параметра в Animator Controller для короткой анимации Fire.")]
    [SerializeField] private string fireTriggerParameter = "Fire";

    [Header("Состояния персонажа")]
    [InspectorLabel("Анимация ходьбы", "Передаёт скорость движения в Animator, чтобы запускать циклическую анимацию Walk.")]
    [SerializeField] private bool animateWalking = true;

    [InspectorLabel("Анимация перезарядки", "Передаёт состояние перезарядки оружия в Animator.")]
    [SerializeField] private bool animateReload = true;

    private Rigidbody2D body;
    private PlayerShooting playerShooting;
    private EnemyWeapon enemyWeapon;
    private EnemyHealth enemyHealth;
    private int speedHash;
    private int reloadHash;
    private int fireHash;
    private float lastObservedShotTime = -10f;
    private bool warnedAboutAnimator;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerShooting = GetComponent<PlayerShooting>();
        enemyWeapon = GetComponent<EnemyWeapon>();
        enemyHealth = GetComponent<EnemyHealth>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        speedHash = Animator.StringToHash(speedParameter);
        reloadHash = Animator.StringToHash(reloadParameter);
        fireHash = Animator.StringToHash(fireTriggerParameter);

        if (animator == null)
        {
            Debug.LogError(
                $"[{nameof(CharacterMotionFeedback)}] На объекте '{name}' не найден Animator. " +
                "Назначь SoldierAnimator.controller компоненту Animator на дочернем визуальном объекте.",
                this);
            enabled = false;
        }
        else if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError(
                $"[{nameof(CharacterMotionFeedback)}] У Animator на объекте '{animator.name}' не назначен Controller.",
                animator);
            warnedAboutAnimator = true;
        }
    }

    private void Update()
    {
        if (body == null || animator == null)
            return;

        if (animator.runtimeAnimatorController == null)
        {
            if (!warnedAboutAnimator)
            {
                Debug.LogError(
                    $"[{nameof(CharacterMotionFeedback)}] У Animator на объекте '{animator.name}' не назначен Controller.",
                    animator);
                warnedAboutAnimator = true;
            }
            return;
        }

        bool isDead = enemyHealth != null && enemyHealth.IsDead;
        float speed = animateWalking && !isDead ? body.linearVelocity.magnitude : 0f;
        bool reloading = !isDead && animateReload &&
            ((playerShooting != null && playerShooting.IsReloading) ||
             (enemyWeapon != null && enemyWeapon.IsReloading));

        animator.SetFloat(speedHash, speed);
        animator.SetBool(reloadHash, reloading);

        float shotTime = -10f;
        if (playerShooting != null)
            shotTime = Mathf.Max(shotTime, playerShooting.LastShotTime);
        if (enemyWeapon != null)
            shotTime = Mathf.Max(shotTime, enemyWeapon.LastShotTime);

        if (!isDead && shotTime > lastObservedShotTime + 0.0001f)
        {
            lastObservedShotTime = shotTime;
            animator.ResetTrigger(fireHash);
            animator.SetTrigger(fireHash);
        }
    }

    private void OnDisable()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        animator.SetFloat(speedHash, 0f);
        animator.SetBool(reloadHash, false);
        animator.ResetTrigger(fireHash);
    }
}
