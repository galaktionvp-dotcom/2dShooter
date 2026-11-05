using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyEscortMovementAI : MonoBehaviour
{
    [Header("Построение вражеской команды")]
    [InspectorLabel("Главный враг", "Объект ведущего врага. Если поле пустое, компонент найдёт объект с EnemyAI в сцене.")]
    [SerializeField] private Transform leaderTarget;

    [InspectorLabel("Позиция в строю", "Смещение относительно ведущего врага. Задай разное смещение каждому бойцу.")]
    [SerializeField] private Vector2 followOffset = new Vector2(1.4f, -1f);

    [Header("Движение")]
    [InspectorLabel("Скорость движения", "Максимальная скорость бойца. Значение выше скорости обычного союзника игрока позволяет команде быстрее занимать позиции.")]
    [SerializeField, Min(0f)] private float moveSpeed = 3.6f;

    [InspectorLabel("Ускорение", "Скорость разгона и торможения бойца.")]
    [SerializeField, Min(0.1f)] private float acceleration = 15f;

    [InspectorLabel("Дистанция остановки", "Расстояние до назначенной точки построения, на котором боец останавливается.")]
    [SerializeField, Min(0.1f)] private float stoppingDistance = 0.3f;

    [InspectorLabel("Скорость поворота", "Максимальная скорость поворота спрайта в градусах в секунду.")]
    [SerializeField, Min(1f)] private float rotationSpeed = 720f;

    [InspectorLabel("Показывать точку построения", "Отображает точку следования бойца в окне Scene при выделении объекта.")]
    [SerializeField] private bool showFormationPoint;

    private Rigidbody2D body;
    private float desiredRotation;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        moveSpeed = Mathf.Max(0f, moveSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        stoppingDistance = Mathf.Max(0.1f, stoppingDistance);
        rotationSpeed = Mathf.Max(1f, rotationSpeed);
        ResolveLeader();
        desiredRotation = body.rotation;
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        if (leaderTarget == null)
            ResolveLeader();

        if (leaderTarget == null)
        {
            MoveTowards(Vector2.zero);
            return;
        }

        Vector2 destination = (Vector2)leaderTarget.position + followOffset;
        Vector2 offset = destination - body.position;
        Vector2 desiredVelocity = Vector2.zero;

        if (offset.magnitude > stoppingDistance && moveSpeed > 0f)
        {
            desiredVelocity = offset.normalized * moveSpeed;
            desiredRotation = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        }

        MoveTowards(desiredVelocity);
        body.MoveRotation(Mathf.MoveTowardsAngle(
            body.rotation,
            desiredRotation,
            rotationSpeed * Time.fixedDeltaTime));
    }

    private void ResolveLeader()
    {
        EnemyAI leader = FindFirstObjectByType<EnemyAI>();
        if (leader != null && leader.transform != transform)
            leaderTarget = leader.transform;
    }

    private void MoveTowards(Vector2 desiredVelocity)
    {
        body.linearVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showFormationPoint)
            return;

        Transform leader = leaderTarget;
        if (leader == null)
        {
            EnemyAI foundLeader = FindFirstObjectByType<EnemyAI>();
            if (foundLeader != null)
                leader = foundLeader.transform;
        }

        if (leader == null)
            return;

        UnityEditor.Handles.color = new Color(1f, 0.3f, 0.2f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(
            (Vector2)leader.position + followOffset,
            Vector3.forward,
            stoppingDistance);
    }
#endif
}