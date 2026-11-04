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

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();

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

        if (distance > attackRange || !HasClearShot(currentTarget))
        {
            currentTarget = null;
            return;
        }

        if (direction.sqrMagnitude > 0.001f)
            desiredRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

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
        if (body == null)
            return;

        bool holdingPosition =
            currentTarget != null &&
            IsTargetAlive(currentTarget) &&
            Vector2.Distance(body.position, currentTarget.position) <= attackRange;

        Vector2 desiredVelocity = Vector2.zero;

        if (!holdingPosition)
        {
            Vector2 destination;

            if (leaderTarget != null)
            {
                destination = (Vector2)leaderTarget.position + followOffset;
            }
            else
            {
                if (player == null)
                    player = FindFirstObjectByType<PlayerHealth>();

                destination = player != null
                    ? (Vector2)player.transform.position
                    : body.position;
            }

            Vector2 toDestination = destination - body.position;
            if (toDestination.magnitude > 0.3f)
            {
                desiredVelocity = toDestination.normalized * followSpeed;
                desiredRotation = Mathf.Atan2(toDestination.y, toDestination.x) * Mathf.Rad2Deg;
            }
        }

        body.linearVelocity = Vector2.MoveTowards(
            body.linearVelocity,
            desiredVelocity,
            acceleration * Time.fixedDeltaTime);

        body.MoveRotation(Mathf.MoveTowardsAngle(
            body.rotation,
            desiredRotation,
            720f * Time.fixedDeltaTime));
    }

    private void FindBestTarget()
    {
        RefreshFriendlyTargets();

        Transform bestTarget = null;
        float bestDistance = attackRange;

        if (player != null && !player.IsDead)
            ConsiderTarget(player.transform, ref bestTarget, ref bestDistance);

        for (int i = 0; i < allies.Length; i++)
        {
            AllyHealth ally = allies[i];
            if (ally == null || ally.IsDead)
                continue;

            ConsiderTarget(ally.transform, ref bestTarget, ref bestDistance);
        }

        currentTarget = bestTarget;
    }

    private void RefreshFriendlyTargets()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerHealth>();

        allies = FindObjectsByType<AllyHealth>(FindObjectsSortMode.None);
    }

    private void ConsiderTarget(
        Transform candidate,
        ref Transform bestTarget,
        ref float bestDistance)
    {
        if (candidate == null)
            return;

        float distance = Vector2.Distance(body.position, candidate.position);
        if (distance > bestDistance || !HasClearShot(candidate))
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
