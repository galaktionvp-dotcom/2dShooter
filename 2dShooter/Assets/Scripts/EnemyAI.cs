using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Обнаружение игрока")]
    [InspectorLabel("Цель", "Трансформ игрока, которого враг должен обнаружить и преследовать.")]
    [SerializeField] private Transform target;

    [InspectorLabel("Радиус обнаружения", "Максимальное расстояние, на котором враг замечает игрока и начинает погоню.")]
    [SerializeField, Min(0.5f)] private float detectionRadius = 7f;

    [Header("Преследование")]
    [InspectorLabel("Скорость преследования", "Максимальная скорость движения врага к игроку. Невысокая скорость оставляет время на реакцию.")]
    [SerializeField, Min(0f)] private float chaseSpeed = 2.4f;

    [InspectorLabel("Ускорение движения", "Насколько быстро враг набирает скорость и останавливается.")]
    [SerializeField, Min(0.1f)] private float acceleration = 10f;

    [InspectorLabel("Дистанция остановки", "Расстояние до игрока, на котором враг перестаёт двигаться и может атаковать касанием.")]
    [SerializeField, Min(0.1f)] private float stoppingDistance = 0.55f;

    [InspectorLabel("Дистанция потери цели", "Если игрок оторвётся дальше этого расстояния, враг прекратит погоню. Должна быть больше радиуса обнаружения.")]
    [SerializeField, Min(0.5f)] private float loseTargetDistance = 9f;

    [Header("Отладка")]
    [InspectorLabel("Показывать радиус в редакторе", "Рисует радиус обнаружения вокруг врага, когда он выделен в окне Scene.")]
    [SerializeField] private bool showDetectionRadius = true;

    private Rigidbody2D body;
    private EnemyHealth enemyHealth;
    private PlayerHealth targetHealth;
    private bool isChasing;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        enemyHealth = GetComponent<EnemyHealth>();

        detectionRadius = Mathf.Max(0.5f, detectionRadius);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        stoppingDistance = Mathf.Max(0.1f, stoppingDistance);
        loseTargetDistance = Mathf.Max(detectionRadius, loseTargetDistance);

        ResolveTarget();
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        if (enemyHealth != null && enemyHealth.IsDead)
        {
            StopMoving();
            return;
        }

        if (target == null || targetHealth == null)
            ResolveTarget();

        if (target == null || (targetHealth != null && targetHealth.IsDead))
        {
            isChasing = false;
            StopMoving();
            return;
        }

        Vector2 toTarget = (Vector2)target.position - body.position;
        float distance = toTarget.magnitude;

        // Враг запоминает цель, пока игрок не оторвётся достаточно далеко.
        if (isChasing)
        {
            if (distance > loseTargetDistance)
                isChasing = false;
        }
        else if (distance <= detectionRadius)
        {
            isChasing = true;
        }

        if (!isChasing || distance <= stoppingDistance || chaseSpeed <= 0f)
        {
            StopMoving();
            return;
        }

        Vector2 desiredVelocity = toTarget.normalized * chaseSpeed;
        body.linearVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime
        );
    }

    private void ResolveTarget()
    {
        if (target == null)
        {
            PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
            if (player != null)
                target = player.transform;
        }

        targetHealth = target != null
            ? target.GetComponentInParent<PlayerHealth>()
            : null;
    }

    private void StopMoving()
    {
        if (body == null)
            return;

        body.linearVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            Vector2.zero,
            acceleration * Time.fixedDeltaTime
        );
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDetectionRadius)
            return;

        UnityEditor.Handles.color = new Color(1f, 0.65f, 0.15f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, Mathf.Max(0.5f, detectionRadius));
    }
#endif
}
