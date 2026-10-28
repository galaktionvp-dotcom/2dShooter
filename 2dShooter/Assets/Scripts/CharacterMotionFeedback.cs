using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterMotionFeedback : MonoBehaviour
{
    [InspectorLabel("Анимация ходьбы", "Включает процедурное покачивание и лёгкое сжатие персонажа при движении. Работает даже без набора кадров анимации.")]
    [SerializeField] private bool animateWalking = true;

    [InspectorLabel("Сила покачивания", "Максимальная амплитуда движения вверх-вниз во время шага.")]
    [SerializeField, Range(0f, 0.1f)] private float bobAmount = 0.025f;

    [InspectorLabel("Частота шагов", "Как быстро проигрывается цикл ходьбы.")]
    [SerializeField, Range(1f, 14f)] private float stepFrequency = 8f;

    [InspectorLabel("Сжатие при шаге", "Небольшое изменение формы по вертикали во время шага для более живого движения.")]
    [SerializeField, Range(0f, 0.08f)] private float squashAmount = 0.025f;

    [InspectorLabel("Порог движения", "Минимальная скорость, при которой запускается анимация ходьбы.")]
    [SerializeField, Min(0f)] private float movementThreshold = 0.15f;

    private Rigidbody2D body;
    private Vector3 baseScale;
    private float phase;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        baseScale = transform.localScale;
        movementThreshold = Mathf.Max(0f, movementThreshold);
    }

    private void Update()
    {
        if (body == null)
            return;

        if (!animateWalking || body.linearVelocity.magnitude <= movementThreshold)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 14f);
            return;
        }

        float speedFactor = Mathf.Clamp01(body.linearVelocity.magnitude / 3.5f);
        phase += Time.deltaTime * stepFrequency * Mathf.Lerp(0.65f, 1.25f, speedFactor);

        // Небольшой подъём на каждом шаге и мягкое сжатие корпуса.
        float step = Mathf.Sin(phase);
        float bob = Mathf.Abs(step) * bobAmount * speedFactor;
        float squash = Mathf.Abs(step) * squashAmount * speedFactor;

        Vector3 targetScale = new Vector3(
            baseScale.x * (1f + squash * 0.5f),
            baseScale.y + bob - baseScale.y * squash,
            baseScale.z
        );

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 18f);
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
    }
}