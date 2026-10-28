using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterMotionFeedback : MonoBehaviour
{
    [InspectorLabel("Визуальная часть персонажа", "Отдельный дочерний объект со SpriteRenderer. Его движение не затрагивает физический коллайдер.")]
    [SerializeField] private Transform visualRoot;

    [InspectorLabel("Анимация ходьбы", "Включает заметное покачивание и растяжение спрайта, когда персонаж движется.")]
    [SerializeField] private bool animateWalking = true;

    [InspectorLabel("Сила покачивания", "Высота движения спрайта вверх-вниз во время ходьбы.")]
    [SerializeField, Range(0f, 0.18f)] private float bobAmount = 0.075f;

    [InspectorLabel("Частота шагов", "Скорость циклов визуальной анимации ходьбы.")]
    [SerializeField, Range(1f, 14f)] private float stepFrequency = 8f;

    [InspectorLabel("Анимация перезарядки", "Включает заметное сжатие и покачивание спрайта, пока оружие перезаряжается.")]
    [SerializeField] private bool animateReload = true;

    [InspectorLabel("Сила анимации перезарядки", "Насколько спрайт сжимается по высоте во время перезарядки.")]
    [SerializeField, Range(0f, 0.25f)] private float reloadSquash = 0.12f;

    [InspectorLabel("Отдача при выстреле", "Сила короткой визуальной отдачи спрайта после выстрела.")]
    [SerializeField, Range(0f, 0.3f)] private float fireKickScale = 0.16f;

    [InspectorLabel("Плавность анимации", "Насколько быстро спрайт достигает целевого положения и масштаба.")]
    [SerializeField, Range(1f, 30f)] private float animationSmoothing = 16f;

    private Rigidbody2D body;
    private Vector3 baseScale;
    private Vector3 baseLocalPosition;
    private float phase;
    private float movementBlend;
    private PlayerShooting playerShooting;
    private EnemyWeapon enemyWeapon;
    private EnemyHealth enemyHealth;
    private float lastObservedShotTime = -10f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerShooting = GetComponent<PlayerShooting>();
        enemyWeapon = GetComponent<EnemyWeapon>();
        enemyHealth = GetComponent<EnemyHealth>();

        if (visualRoot == null)
        {
            SpriteRenderer ownRenderer = GetComponent<SpriteRenderer>();
            if (ownRenderer != null)
                visualRoot = ownRenderer.transform;
            else
            {
                SpriteRenderer childRenderer = GetComponentInChildren<SpriteRenderer>(true);
                if (childRenderer != null && childRenderer.transform != transform)
                    visualRoot = childRenderer.transform;
            }
        }

        if (visualRoot != null)
        {
            baseScale = visualRoot.localScale;
            baseLocalPosition = visualRoot.localPosition;
        }
    }

    private void Update()
    {
        if (body == null || visualRoot == null)
            return;

        if (enemyHealth != null && enemyHealth.IsDead)
        {
            RestoreVisualPose();
            return;
        }

        float speed = body.linearVelocity.magnitude;
        float movementTarget = animateWalking ? Mathf.Clamp01(speed / 2f) : 0f;
        movementBlend = Mathf.MoveTowards(movementBlend, movementTarget, Time.deltaTime * 8f);

        if (movementBlend > 0.01f)
            phase += Time.deltaTime * stepFrequency * Mathf.Lerp(0.45f, 1.35f, movementBlend);

        bool reloading = animateReload &&
            ((playerShooting != null && playerShooting.IsReloading) ||
             (enemyWeapon != null && enemyWeapon.IsReloading));

        float reloadPulse = reloading
            ? 0.55f + 0.45f * Mathf.Sin(Time.time * 10f)
            : 0f;

        float shotTime = -10f;
        if (playerShooting != null)
            shotTime = Mathf.Max(shotTime, playerShooting.LastShotTime);
        if (enemyWeapon != null)
            shotTime = Mathf.Max(shotTime, enemyWeapon.LastShotTime);

        if (shotTime > lastObservedShotTime)
            lastObservedShotTime = shotTime;

        float shotAge = Time.time - lastObservedShotTime;
        float firePulse = shotAge >= 0f && shotAge < 0.13f
            ? 1f - shotAge / 0.13f
            : 0f;

        float stride = Mathf.Sin(phase * 2f);
        float bounce = animateWalking
            ? Mathf.Abs(Mathf.Sin(phase)) * bobAmount * movementBlend
            : 0f;

        float xFactor = 1f - 0.045f * movementBlend * Mathf.Abs(stride);
        float yFactor = 1f + 0.07f * movementBlend * Mathf.Abs(stride);
        xFactor += reloadPulse * 0.025f;
        yFactor -= reloadPulse * reloadSquash;
        xFactor -= firePulse * fireKickScale;
        yFactor += firePulse * fireKickScale * 0.45f;

        Vector3 targetScale = new Vector3(
            baseScale.x * Mathf.Max(0.5f, xFactor),
            baseScale.y * Mathf.Max(0.5f, yFactor),
            baseScale.z
        );
        Vector3 targetPosition = baseLocalPosition + Vector3.up * bounce;

        float smoothing = Mathf.Max(1f, animationSmoothing);
        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, blend);
        visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, targetPosition, blend);
    }

    private void OnDisable()
    {
        RestoreVisualPose();
    }

    private void RestoreVisualPose()
    {
        if (visualRoot == null)
            return;

        visualRoot.localScale = baseScale;
        visualRoot.localPosition = baseLocalPosition;
    }
}
