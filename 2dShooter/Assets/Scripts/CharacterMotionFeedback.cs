using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterMotionFeedback : MonoBehaviour
{
    [InspectorLabel("Анимация ходьбы", "Включает лёгкое покачивание персонажа при движении.")]
    [SerializeField] private bool animateWalking = true;
    [InspectorLabel("Сила покачивания", "Небольшая амплитуда движения вверх-вниз во время ходьбы.")]
    [SerializeField, Range(0f, 0.1f)] private float bobAmount = 0.025f;
    [InspectorLabel("Частота шагов", "Как быстро меняется анимация ходьбы.")]
    [SerializeField, Range(1f, 14f)] private float stepFrequency = 8f;
    [InspectorLabel("Анимация перезарядки", "Добавляет короткое сжатие персонажа во время перезарядки оружия.")]
    [SerializeField] private bool animateReload = true;
    [InspectorLabel("Сила анимации перезарядки", "Небольшое изменение масштаба персонажа во время перезарядки.")]
    [SerializeField, Range(0f, 0.12f)] private float reloadSquash = 0.035f;

    private Rigidbody2D body;
    private Vector3 baseScale;
    private float phase;
    private PlayerShooting playerShooting;
    private EnemyWeapon enemyWeapon;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        baseScale = transform.localScale;
        playerShooting = GetComponent<PlayerShooting>();
        enemyWeapon = GetComponent<EnemyWeapon>();
    }

    private void Update()
    {
        if (!animateWalking || body == null) return;
        float speed = body.linearVelocity.magnitude;
        bool reloading = animateReload &&
            ((playerShooting != null && playerShooting.IsReloading) ||
             (enemyWeapon != null && enemyWeapon.IsReloading));

        float bob = 0f;
        if (speed > 0.15f)
        {
            phase += Time.deltaTime * stepFrequency;
            bob = Mathf.Abs(Mathf.Sin(phase)) * bobAmount;
        }

        float squash = reloading ? reloadSquash : 0f;
        Vector3 targetScale = new Vector3(
            baseScale.x * (1f + squash * 0.35f),
            baseScale.y + bob - squash,
            baseScale.z
        );
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 12f);
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
    }
}
