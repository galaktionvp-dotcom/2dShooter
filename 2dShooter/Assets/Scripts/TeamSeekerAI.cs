using InspectorLocalization;
using UnityEngine;

/// <summary>
/// Safe movement-only team search for ArenaTest.
/// Characters patrol their assigned points, detect the nearest opposing TeamMember,
/// and approach it to a configurable stand-off distance. No weapons, damage or attacks.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(TeamMember))]
public sealed class TeamSeekerAI : MonoBehaviour
{
    [Header("Поиск противоположной команды")]
    [InspectorLabel("Радиус поиска", "Максимальная дистанция обнаружения ближайшего персонажа противоположной команды.")]
    [SerializeField, Min(1f)] private float searchRadius = 12f;

    [InspectorLabel("Дистанция встречи", "На каком расстоянии боец остановится от найденного персонажа. Это только безопасная дистанция сближения, не атака.")]
    [SerializeField, Min(0.5f)] private float meetDistance = 2f;

    [InspectorLabel("Интервал обновления цели", "Как часто боец повторно ищет ближайшего участника противоположной команды.")]
    [SerializeField, Min(0.1f)] private float targetRefreshInterval = 0.4f;

    [Header("Патрулирование")]
    [InspectorLabel("Точки патруля", "Заранее размещённые пустые объекты на карте. Если противник не найден, боец перемещается между этими точками.")]
    [SerializeField] private Transform[] patrolPoints;

    [InspectorLabel("Дистанция точки", "Насколько близко боец должен подойти к точке патруля, прежде чем выбрать следующую.")]
    [SerializeField, Min(0.1f)] private float waypointDistance = 0.35f;

    [Header("Движение")]
    [InspectorLabel("Скорость", "Максимальная скорость перемещения персонажа.")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.5f;

    [InspectorLabel("Ускорение", "Насколько быстро персонаж разгоняется и тормозит.")]
    [SerializeField, Min(0.1f)] private float acceleration = 12f;

    [InspectorLabel("Скорость поворота", "Максимальная скорость поворота Rigidbody2D в градусах в секунду.")]
    [SerializeField, Min(1f)] private float rotationSpeed = 540f;

    [InspectorLabel("Показывать радиус поиска", "Показывает радиус поиска в окне Scene при выделении персонажа.")]
    [SerializeField] private bool showSearchRadius = true;

    private Rigidbody2D body;
    private TeamMember member;
    private TeamMember currentTarget;
    private TeamMember[] allMembers;
    private float nextRefreshTime;
    private int patrolIndex;
    private float desiredRotation;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        member = GetComponent<TeamMember>();
        searchRadius = Mathf.Max(1f, searchRadius);
        meetDistance = Mathf.Max(0.5f, meetDistance);
        targetRefreshInterval = Mathf.Max(0.1f, targetRefreshInterval);
        waypointDistance = Mathf.Max(0.1f, waypointDistance);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        rotationSpeed = Mathf.Max(1f, rotationSpeed);
        desiredRotation = body.rotation;
        RefreshMembers();
    }

    private void Update()
    {
        if (Time.time < nextRefreshTime)
            return;

        nextRefreshTime = Time.time + targetRefreshInterval;
        FindNearestOpponent();
    }

    private void FixedUpdate()
    {
        if (body == null || member == null)
            return;

        Vector2 destination = body.position;
        bool hasDestination = false;

        if (currentTarget != null && currentTarget.isActiveAndEnabled)
        {
            Vector2 targetPosition = currentTarget.transform.position;
            Vector2 offset = targetPosition - body.position;

            if (offset.magnitude <= searchRadius * 1.25f)
            {
                if (offset.magnitude > meetDistance)
                {
                    destination = targetPosition;
                    hasDestination = true;
                }
                else
                {
                    Move(Vector2.zero);
                    return;
                }
            }
            else
            {
                currentTarget = null;
            }
        }

        if (!hasDestination && patrolPoints != null && patrolPoints.Length > 0)
        {
            Transform waypoint = patrolPoints[patrolIndex % patrolPoints.Length];
            if (waypoint != null)
            {
                destination = waypoint.position;
                Vector2 toWaypoint = destination - body.position;

                if (toWaypoint.magnitude <= waypointDistance)
                {
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                    waypoint = patrolPoints[patrolIndex];
                    if (waypoint != null)
                        destination = waypoint.position;
                }

                hasDestination = waypoint != null;
            }
        }

        if (!hasDestination)
        {
            Move(Vector2.zero);
            return;
        }

        Vector2 direction = (destination - body.position);
        if (currentTarget != null && direction.magnitude <= meetDistance)
            direction = Vector2.zero;

        Vector2 desiredVelocity = direction.sqrMagnitude > 0.001f
            ? direction.normalized * moveSpeed
            : Vector2.zero;

        if (desiredVelocity.sqrMagnitude > 0.001f)
            desiredRotation = Mathf.Atan2(desiredVelocity.y, desiredVelocity.x) * Mathf.Rad2Deg;

        Move(desiredVelocity);
        body.MoveRotation(Mathf.MoveTowardsAngle(body.rotation, desiredRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    private void FindNearestOpponent()
    {
        if (allMembers == null || allMembers.Length == 0)
            RefreshMembers();

        TeamMember best = null;
        float bestDistanceSqr = searchRadius * searchRadius;

        for (int i = 0; i < allMembers.Length; i++)
        {
            TeamMember candidate = allMembers[i];
            if (candidate == null || candidate == member || !candidate.isActiveAndEnabled)
                continue;

            if (candidate.CurrentTeam == member.CurrentTeam)
                continue;

            float distanceSqr = ((Vector2)candidate.transform.position - body.position).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                best = candidate;
            }
        }

        currentTarget = best;
    }

    private void RefreshMembers()
    {
        allMembers = FindObjectsByType<TeamMember>(FindObjectsSortMode.None);
    }

    private void Move(Vector2 desiredVelocity)
    {
        body.linearVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (showSearchRadius)
        {
            UnityEditor.Handles.color = new Color(0.25f, 0.8f, 1f, 0.9f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, Mathf.Max(1f, searchRadius));
        }

        if (patrolPoints == null)
            return;

        UnityEditor.Handles.color = new Color(0.3f, 1f, 0.5f, 0.8f);
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null)
                continue;

            UnityEditor.Handles.DrawLine(transform.position, patrolPoints[i].position);
            UnityEditor.Handles.DrawWireDisc(patrolPoints[i].position, Vector3.forward, 0.15f);
        }
    }
#endif
}
