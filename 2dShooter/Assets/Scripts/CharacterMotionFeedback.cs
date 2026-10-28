using InspectorLocalization;
using UnityEngine;

/// <summary>Procedural visual animation. Does not use AnimatorController assets.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class CharacterMotionFeedback : MonoBehaviour
{
    [Header("Визуальный объект")]
    [InspectorLabel("Визуальная часть персонажа", "Дочерний объект со SpriteRenderer. Меняется только изображение, не физический коллайдер.")]
    [SerializeField] private Transform visualRoot;

    [Header("Ходьба")]
    [InspectorLabel("Включить анимацию ходьбы", "Покачивает изображение во время движения.")]
    [SerializeField] private bool animateWalking = true;
    [InspectorLabel("Высота шага", "Высота покачивания изображения при ходьбе.")]
    [SerializeField, Range(0f, 0.18f)] private float stepHeight = 0.075f;
    [InspectorLabel("Частота шагов", "Скорость цикла визуальной анимации ходьбы.")]
    [SerializeField, Range(1f, 16f)] private float stepFrequency = 8f;

    [Header("Перезарядка")]
    [InspectorLabel("Анимировать перезарядку", "Сжимает и слегка опускает изображение при перезарядке.")]
    [SerializeField] private bool animateReload = true;
    [InspectorLabel("Сила сжатия", "Насколько уменьшается высота изображения при перезарядке.")]
    [SerializeField, Range(0f, 0.25f)] private float reloadSquash = 0.12f;

    [Header("Выстрел")]
    [InspectorLabel("Сила отдачи изображения", "Коротко сжимает изображение сразу после выстрела.")]
    [SerializeField, Range(0f, 0.35f)] private float fireKick = 0.16f;
    [InspectorLabel("Длительность отдачи", "Сколько длится визуальная реакция на выстрел.")]
    [SerializeField, Range(0.03f, 0.3f)] private float fireKickDuration = 0.13f;

    [Header("Плавность")]
    [InspectorLabel("Сглаживание анимации", "Скорость плавного перехода между визуальными состояниями.")]
    [SerializeField, Range(1f, 30f)] private float smoothing = 16f;

    private Rigidbody2D body;
    private PlayerShooting playerShooting;
    private EnemyWeapon enemyWeapon;
    private EnemyHealth enemyHealth;
    private Vector3 baseScale;
    private Vector3 baseLocalPosition;
    private float stepPhase;
    private float movementBlend;
    private float observedShotTime = -10f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerShooting = GetComponent<PlayerShooting>();
        enemyWeapon = GetComponent<EnemyWeapon>();
        enemyHealth = GetComponent<EnemyHealth>();

        if (visualRoot == null)
        {
            SpriteRenderer childSprite = GetComponentInChildren<SpriteRenderer>(true);
            if (childSprite != null && childSprite.transform != transform)
                visualRoot = childSprite.transform;
            else if (GetComponent<SpriteRenderer>() != null)
                visualRoot = transform;
        }

        if (visualRoot == null)
        {
            Debug.LogError($"[{nameof(CharacterMotionFeedback)}] На объекте '{name}' не найден SpriteRenderer.", this);
            enabled = false;
            return;
        }

        baseScale = visualRoot.localScale;
        baseLocalPosition = visualRoot.localPosition;
        if (playerShooting != null)
            observedShotTime = Mathf.Max(observedShotTime, playerShooting.LastShotTime);
        if (enemyWeapon != null)
            observedShotTime = Mathf.Max(observedShotTime, enemyWeapon.LastShotTime);
    }

    private void Update()
    {
        if (body == null || visualRoot == null)
            return;

        bool isDead = enemyHealth != null && enemyHealth.IsDead;
        float speed = !isDead && animateWalking ? body.linearVelocity.magnitude : 0f;
        float movementTarget = speed > 0.15f ? Mathf.Clamp01(speed / 2f) : 0f;
        movementBlend = Mathf.MoveTowards(movementBlend, movementTarget, Time.deltaTime * 8f);
        if (movementBlend > 0.01f)
            stepPhase += Time.deltaTime * stepFrequency * Mathf.Lerp(0.6f, 1.4f, movementBlend);

        bool reloading = !isDead && animateReload &&
            ((playerShooting != null && playerShooting.IsReloading) ||
             (enemyWeapon != null && enemyWeapon.IsReloading));

        float currentShotTime = observedShotTime;
        if (playerShooting != null)
            currentShotTime = Mathf.Max(currentShotTime, playerShooting.LastShotTime);
        if (enemyWeapon != null)
            currentShotTime = Mathf.Max(currentShotTime, enemyWeapon.LastShotTime);
        if (currentShotTime > observedShotTime + 0.0001f)
            observedShotTime = currentShotTime;

        float shotAge = Time.time - observedShotTime;
        float firePulse = !isDead && shotAge >= 0f && shotAge < fireKickDuration
            ? 1f - shotAge / fireKickDuration : 0f;
        float step = Mathf.Sin(stepPhase);
        float bounce = animateWalking ? Mathf.Abs(step) * stepHeight * movementBlend : 0f;
        float reloadPulse = reloading ? 0.5f + 0.5f * Mathf.Sin(Time.time * 10f) : 0f;

        float horizontalScale = 1f - 0.055f * movementBlend * Mathf.Abs(step);
        float verticalScale = 1f + 0.075f * movementBlend * Mathf.Abs(step);
        horizontalScale += reloadPulse * 0.025f - firePulse * fireKick;
        verticalScale -= reloadPulse * reloadSquash;
        verticalScale += firePulse * fireKick * 0.45f;

        Vector3 targetScale = new Vector3(
            baseScale.x * Mathf.Max(0.5f, horizontalScale),
            baseScale.y * Mathf.Max(0.5f, verticalScale),
            baseScale.z);
        Vector3 targetPosition = baseLocalPosition + Vector3.up * (bounce - reloadPulse * 0.025f);
        float blend = 1f - Mathf.Exp(-Mathf.Max(1f, smoothing) * Time.deltaTime);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, blend);
        visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, targetPosition, blend);
    }

    private void OnDisable() => RestoreVisualPose();
    private void OnDestroy() => RestoreVisualPose();

    private void RestoreVisualPose()
    {
        if (visualRoot == null) return;
        visualRoot.localScale = baseScale;
        visualRoot.localPosition = baseLocalPosition;
    }
}
