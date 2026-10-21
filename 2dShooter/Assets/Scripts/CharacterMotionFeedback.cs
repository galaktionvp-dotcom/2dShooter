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

    private Rigidbody2D body;
    private Vector3 baseScale;
    private float phase;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (!animateWalking || body == null) return;
        float speed = body.linearVelocity.magnitude;
        if (speed > 0.15f)
        {
            phase += Time.deltaTime * stepFrequency;
            float bob = Mathf.Abs(Mathf.Sin(phase)) * bobAmount;
            transform.localScale = new Vector3(baseScale.x, baseScale.y + bob, baseScale.z);
        }
        else
        {
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 12f);
        }
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
    }
}
