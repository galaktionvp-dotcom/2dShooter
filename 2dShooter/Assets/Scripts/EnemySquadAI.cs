using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
public sealed class EnemySquadAI : MonoBehaviour
{
    [Header("Построение вражеской команды")]
    [InspectorLabel("Главный враг", "Трансформ основного врага, рядом с которым держится этот боец.")]
    [SerializeField] private Transform leaderTarget;

    [InspectorLabel("Позиция в строю", "Смещение относительно главного врага. Для бойцов команды укажи разные значения.")]
    [SerializeField] private Vector2 followOffset = new Vector2(1.5f, 1f);

    [InspectorLabel("Скорость следования", "Максимальная скорость движения бойца за главным врагом.")]
    [SerializeField, Min(0f)] private float followSpeed = 3.1f;

    [InspectorLabel("Ускорение", "Насколько быстро боец набирает скорость и останавливается.")]
    [SerializeField, Min(0.1f)] private float acceleration = 14f;

    [Header("Атака команды")]
    [InspectorLabel("Дальность атаки", "Максимальная дальность выстрела. Совпадает с дальностью атаки союзников игрока — 6 единиц.")]
    [SerializeField, Min(0.5f)] private float attackRange = 6f;

    [InspectorLabel("Интервал выстрелов", "Пауза между выстрелами. Значение меньше, чем у союзников игрока, делает бойца немного сильнее.")]
    [SerializeField, Min(0.2f)] private float fireInterval = 1.25f;

    [InspectorLabel("Урон выстрела", "Урон одного попадания. Немного выше урона союзников игрока.")]
    [SerializeField, Min(0f)] private float attackDamage = 1.5f;

    [InspectorLabel("Снаряд бойца", "Заранее размещённый в сцене объект с видимым спрайтом, Rigidbody2D и EnemyProjectile. Повторно используется при каждом выстреле.")]
    [SerializeField] private EnemyProjectile projectile;

    [InspectorLabel("Частота поиска цели", "Как часто боец ищет ближайшего игрока или союзника.")]
    [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.25f;

    [Header("Поиск и тактика в бою")]
    [InspectorLabel("Радиус поиска", "Дистанция обнаружения игрока и союзников игрока. Боец замечает цель раньше, чем сможет выстрелить.")]
    [SerializeField, Min(1f)] private float searchRadius = 16f;

    [InspectorLabel("Предпочтительная дистанция", "Расстояние, которое боец старается держать до цели во время перестрелки.")]
    [SerializeField, Min(0.5f)] private float preferredDistance = 4f;

    [InspectorLabel("Дистанция отхода", "Если противник подходит ближе этого значения, боец отступает.")]
    [SerializeField, Min(0.2f)] private float retreatDistance = 1.9f;

    [InspectorLabel("Дистанция раздвижения", "Помогает бойцам не стоять вплотную друг к другу.")]
    [SerializeField, Min(0f)] private float separationRadius = 1.1f;

    [InspectorLabel("Сила раздвижения", "Насколько сильно боец отходит от других бойцов своей команды.")]
    [SerializeField, Min(0f)] private float separationStrength = 1.2f;

    [InspectorLabel("Запас удержания цели", "Боец не переключается на другого противника из-за небольшой разницы в расстоянии.")]
    [SerializeField, Min(0f)] private float targetSwitchBuffer = 1.25f;

    [InspectorLabel("Показывать дальность атаки", "Рисует радиус атаки в окне Scene, когда объект выделен.")]
    [SerializeField] private bool showAttackRange;

    private Rigidbody2D body;
    private EnemyHealth health;
    private PlayerHealth player;
    private AllyHealth[] allies;
    private Transform currentTarget;
    private float nextTargetRefreshTime;
    private float nextShotTime;
    private float desiredRotation;
    private EnemyHealth[] friendlyEnemies;
    private TopDownMapGenerator arena;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        arena = FindFirstObjectByType<TopDownMapGenerator>();

        if (leaderTarget == null)
        {
            EnemyAI leader = FindFirstObjectByType<EnemyAI>();
            if (leader != null)
                leaderTarget = leader.transform;
        }

        if (projectile == null)
            projectile = GetComponentInChildren<EnemyProjectile>(true);

        if (projectile != null)
            projectile.PreparePool(transform);
        else
            Debug.LogError("[EnemySquadAI] Не назначен заранее размещённый снаряд EnemyProjectile.", this);

        attackRange = Mathf.Max(0.5f, attackRange);
        fireInterval = Mathf.Max(0.2f, fireInterval);
        attackDamage = Mathf.Max(0f, attackDamage);
        followSpeed = Mathf.Max(0f, followSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        targetRefreshInterval = Mathf.Max(0.05f, targetRefreshInterval);
        searchRadius = Mathf.Max(1f, searchRadius);
        preferredDistance = Mathf.Max(0.5f, preferredDistance);
        retreatDistance = Mathf.Clamp(retreatDistance, 0.2f, preferredDistance);
        separationRadius = Mathf.Max(0f, separationRadius);
        separationStrength = Mathf.Max(0f, separationStrength);
        targetSwitchBuffer = Mathf.Max(0f, targetSwitchBuffer);

        RefreshFriendlyTargets();
        desiredRotation = body.rotation;
    }

    private void Update()
    {
        if (health != null && health.IsDead)
            return;

        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + targetRefreshInterval;
            FindBestTarget();
        }

        if (currentTarget == null || projectile == null || !IsTargetAlive(currentTarget))
            return;

        Vector2 direction = (Vector2)currentTarget.position - body.position;
        float distance = direction.magnitude;

        if (direction.sqrMagnitude > 0.001f)
            desiredRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (distance > attackRange || !HasClearShot(currentTarget))
            return;

        if (Time.time >= nextShotTime && !projectile.IsTravelling)
        {
            Vector2 shotDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector2.right;

            bool launched = projectile.Launch(
                transform,
                (Vector2)transform.position + shotDirection * 0.55f,
                shotDirection,
                attackDamage,
                12f);

            if (launched)
                nextShotTime = Time.time + fireInterval;
        }
    }

    private void FixedUpdate()
    {
        if (body == null || (health != null && health.IsDead))
            return;

        Vector2 desiredVelocity = Vector2.zero;

        if (currentTarget != null && IsTargetAlive(currentTarget))
        {
            Vector2 toTarget = (Vector2)currentTarget.position - body.position;
            float distance = toTarget.magnitude;

            if (distance > 0.001f)
            {
                desiredRotation = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                desiredVelocity = CalculateStandoffVelocity(toTarget, distance);
            }
        }
        else
        {
            if (leaderTarget == null)
            {
                EnemyAI leader = FindFirstObjectByType<EnemyAI>();
                if (leader != null && leader.transform != transform)
                    leaderTarget = leader.transform;
            }

            Vector2 destination = leaderTarget != null
                ? (Vector2)leaderTarget.position + followOffset
                : player != null ? (Vector2)player.transform.position : body.position;

            if (arena == null)
                arena = FindFirstObjectByType<TopDownMapGenerator>();

            if (arena != null)
                destination = arena.ClampMovementDestination(destination, 0.7f);

            Vector2 toDestination = destination - body.position;
            desiredVelocity = CalculateArriveVelocity(toDestination, 0.3f);

            if (toDestination.sqrMagnitude > 0.001f)
                desiredRotation = Mathf.Atan2(toDestination.y, toDestination.x) * Mathf.Rad2Deg;
        }

        // Раздвижение работает только на короткой дистанции и не задаёт постоянную орбиту вокруг цели.
        desiredVelocity += CalculateSeparation() * separationStrength;
        desiredVelocity = Vector2.ClampMagnitude(desiredVelocity, followSpeed);

        Vector2 nextVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime);

        if (arena == null)
            arena = FindFirstObjectByType<TopDownMapGenerator>();

        if (arena != null)
        {
            nextVelocity = arena.ConstrainMovementVelocity(
                body.position,
                nextVelocity,
                followSpeed,
                Time.fixedDeltaTime,
                0.7f);
        }

        body.linearVelocity = nextVelocity;
        body.MoveRotation(Mathf.MoveTowardsAngle(
            body.rotation,
            desiredRotation,
            720f * Time.fixedDeltaTime));
    }

    private Vector2 CalculateStandoffVelocity(Vector2 toTarget, float distance)
    {
        Vector2 toward = toTarget / distance;

        if (distance < retreatDistance)
        {
            float retreatRatio = 1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, retreatDistance));
            float retreatSpeed = Mathf.Lerp(followSpeed * 0.45f, followSpeed, retreatRatio);
            return -toward * retreatSpeed;
        }

        // Движение только по радиусу: когда удобная дистанция достигнута, боец останавливается,
        // а не продолжает бесконечно кружить вокруг одной и той же цели.
        const float distanceDeadZone = 0.35f;
        float distanceError = distance - preferredDistance;
        float remainingError = Mathf.Abs(distanceError) - distanceDeadZone;

        if (remainingError <= 0f || followSpeed <= 0f)
            return Vector2.zero;

        float desiredSpeed = Mathf.Min(
            followSpeed,
            Mathf.Sqrt(2f * acceleration * remainingError));

        return toward * (distanceError > 0f ? desiredSpeed : -desiredSpeed);
    }

    private Vector2 CalculateArriveVelocity(Vector2 offset, float stopDistance)
    {
        float distance = offset.magnitude;
        if (distance <= stopDistance || followSpeed <= 0f)
            return Vector2.zero;

        float desiredSpeed = Mathf.Min(
            followSpeed,
            Mathf.Sqrt(2f * acceleration * (distance - stopDistance)));

        return offset / distance * desiredSpeed;
    }

    private Vector2 CalculateSeparation()
    {
        if (friendlyEnemies == null || separationRadius <= 0f)
            return Vector2.zero;

        Vector2 separation = Vector2.zero;
        int neighbours = 0;

        for (int i = 0; i < friendlyEnemies.Length; i++)
        {
            EnemyHealth other = friendlyEnemies[i];
            if (other == null || other == health || other.IsDead)
                continue;

            Vector2 offset = body.position - (Vector2)other.transform.position;
            float distance = offset.magnitude;
            if (distance <= 0.001f || distance >= separationRadius)
                continue;

            separation += offset / distance * (1f - distance / separationRadius);
            neighbours++;
        }

        return neighbours > 0 ? separation / neighbours : Vector2.zero;
    }

    private void FindBestTarget()
    {
        RefreshFriendlyTargets();

        Transform bestTarget = null;
        float bestDistance = searchRadius;

        if (player != null && !player.IsDead)
            ConsiderTarget(player.transform, ref bestTarget, ref bestDistance);

        for (int i = 0; i < allies.Length; i++)
        {
            AllyHealth ally = allies[i];
            if (ally == null || ally.IsDead)
                continue;

            ConsiderTarget(ally.transform, ref bestTarget, ref bestDistance);
        }

        if (currentTarget != null && IsTargetAlive(currentTarget))
        {
            float currentDistance = Vector2.Distance(body.position, currentTarget.position);
            if (currentDistance <= searchRadius * 1.35f &&
                (bestTarget == null || currentDistance <= bestDistance + targetSwitchBuffer))
            {
                bestTarget = currentTarget;
            }
        }

        currentTarget = bestTarget;
    }

    private void RefreshFriendlyTargets()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerHealth>();

        allies = FindObjectsByType<AllyHealth>(FindObjectsSortMode.None);
        friendlyEnemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
    }

    private void ConsiderTarget(
        Transform candidate,
        ref Transform bestTarget,
        ref float bestDistance)
    {
        if (candidate == null)
            return;

        float distance = Vector2.Distance(body.position, candidate.position);
        if (distance > bestDistance)
            return;

        bestDistance = distance;
        bestTarget = candidate;
    }

    private bool HasClearShot(Transform candidate)
    {
        if (candidate == null)
            return false;

        Vector2 start = body.position;
        Vector2 end = candidate.position;
        RaycastHit2D[] hits = Physics2D.LinecastAll(start, end);

        Collider2D closestCollider = null;
        float closestFraction = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                continue;

            if (hits[i].fraction < closestFraction)
            {
                closestFraction = hits[i].fraction;
                closestCollider = hitCollider;
            }
        }

        if (closestCollider == null)
            return false;

        PlayerHealth hitPlayer = closestCollider.GetComponentInParent<PlayerHealth>();
        if (hitPlayer != null)
            return hitPlayer.transform == candidate;

        AllyHealth hitAlly = closestCollider.GetComponentInParent<AllyHealth>();
        return hitAlly != null && hitAlly.transform == candidate;
    }

    private bool IsTargetAlive(Transform candidate)
    {
        if (candidate == null)
            return false;

        PlayerHealth targetPlayer = candidate.GetComponentInParent<PlayerHealth>();
        if (targetPlayer != null)
            return !targetPlayer.IsDead;

        AllyHealth targetAlly = candidate.GetComponentInParent<AllyHealth>();
        return targetAlly != null && !targetAlly.IsDead;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showAttackRange)
            return;

        UnityEditor.Handles.color = new Color(1f, 0.25f, 0.15f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(
            transform.position,
            Vector3.forward,
            Mathf.Max(0.5f, attackRange));
    }
#endif
}
