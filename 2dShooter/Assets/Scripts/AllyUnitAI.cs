using InspectorLocalization;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class AllyUnitAI : MonoBehaviour
{
    [Header("Следование за игроком")]
    [InspectorLabel("Игрок", "Трансформ игрока, за которым следует союзник. Обычно назначается объект player из сцены.")]
    [SerializeField] private Transform playerTarget;

    [InspectorLabel("Позиция рядом с игроком", "Смещение точки построения относительно игрока. Для разных союзников укажи разные значения.")]
    [SerializeField] private Vector2 followOffset = new Vector2(-1.25f, 1f);

    [InspectorLabel("Дистанция остановки", "Насколько близко союзник подходит к своей точке построения.")]
    [SerializeField, Min(0.1f)] private float stopDistance = 0.25f;

    [InspectorLabel("Скорость следования", "Максимальная скорость союзника. Она не должна мешать игроку управлять персонажем.")]
    [SerializeField, Min(0f)] private float followSpeed = 3f;

    [InspectorLabel("Ускорение", "Насколько быстро союзник разгоняется и останавливается.")]
    [SerializeField, Min(0.1f)] private float acceleration = 14f;

    [Header("Поддержка огнём")]
    [InspectorLabel("Дальность атаки", "Максимальное расстояние, на котором союзник может атаковать врага. Установлено как у врага — 6 единиц.")]
    [SerializeField, Min(0.5f)] private float attackRange = 6f;

    [InspectorLabel("Интервал атаки", "Пауза между выстрелами союзника. Чем больше значение, тем слабее поддержка огнём.")]
    [SerializeField, Min(0.2f)] private float fireInterval = 1.5f;

    [InspectorLabel("Урон союзника", "Урон одного попадания. Небольшое значение делает союзника слабее обычного врага.")]
    [SerializeField, Min(0f)] private float attackDamage = 1f;

    [InspectorLabel("Снаряд союзника", "Заранее размещённый в сцене объект с видимым спрайтом, Rigidbody2D и AllyProjectile. Он повторно используется при каждом выстреле.")]
    [SerializeField] private AllyProjectile projectile;

    [InspectorLabel("Частота поиска врагов", "Как часто союзник обновляет список целей. Меньшее значение быстрее реагирует, но чаще проверяет сцену.")]
    [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.25f;

    [InspectorLabel("Показывать дальность атаки", "Рисует дальность атаки вокруг союзника только в окне Scene, когда объект выделен.")]
    [SerializeField] private bool showAttackRange;

    private Rigidbody2D body;
    private AllyHealth allyHealth;
    private EnemyHealth currentTarget;
    private EnemyHealth[] enemies;
    private float nextTargetRefreshTime;
    private float nextAttackTime;
    private float desiredRotation;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        allyHealth = GetComponent<AllyHealth>();

        if (playerTarget == null)
        {
            PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
            if (player != null)
                playerTarget = player.transform;
        }

        if (projectile == null)
            projectile = GetComponentInChildren<AllyProjectile>(true);

        if (projectile != null)
            projectile.PreparePool(transform);
        else
            Debug.LogError("[AllyUnitAI] Не назначен заранее размещённый снаряд AllyProjectile.", this);

        stopDistance = Mathf.Max(0.1f, stopDistance);
        followSpeed = Mathf.Max(0f, followSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        attackRange = Mathf.Max(0.5f, attackRange);
        fireInterval = Mathf.Max(0.2f, fireInterval);
        attackDamage = Mathf.Max(0f, attackDamage);
        targetRefreshInterval = Mathf.Max(0.05f, targetRefreshInterval);

        RefreshEnemyList();
        desiredRotation = body.rotation;
    }

    private void Update()
    {
        if (allyHealth != null && allyHealth.IsDead)
            return;

        if (playerTarget == null)
        {
            PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
            if (player != null)
                playerTarget = player.transform;
        }

        if (Time.time >= nextTargetRefreshTime)
        {
            nextTargetRefreshTime = Time.time + targetRefreshInterval;
            FindBestTarget();
        }

        if (currentTarget == null || currentTarget.IsDead || projectile == null)
            return;

        Vector2 direction = (Vector2)currentTarget.transform.position - body.position;
        float distance = direction.magnitude;

        if (distance > attackRange || !HasClearShot(currentTarget))
        {
            currentTarget = null;
            return;
        }

        if (direction.sqrMagnitude > 0.001f)
            desiredRotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (Time.time >= nextAttackTime && !projectile.IsTravelling)
        {
            Vector2 shotDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector2.right;

            bool launched = projectile.Launch(
                transform,
                (Vector2)transform.position + shotDirection * 0.55f,
                shotDirection,
                attackDamage);

            if (launched)
                nextAttackTime = Time.time + fireInterval;
        }
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        if (playerTarget != null)
        {
            Vector2 destination = (Vector2)playerTarget.position + followOffset;
            Vector2 toDestination = destination - body.position;

            Vector2 desiredVelocity = toDestination.magnitude <= stopDistance
                ? Vector2.zero
                : toDestination.normalized * followSpeed;

            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity,
                desiredVelocity,
                acceleration * Time.fixedDeltaTime);
        }
        else
        {
            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity,
                Vector2.zero,
                acceleration * Time.fixedDeltaTime);
        }

        body.MoveRotation(Mathf.MoveTowardsAngle(
            body.rotation,
            desiredRotation,
            720f * Time.fixedDeltaTime));
    }

    private void RefreshEnemyList()
    {
        enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
    }

    private void FindBestTarget()
    {
        if (enemies == null || enemies.Length == 0)
            RefreshEnemyList();

        EnemyHealth bestTarget = null;
        float bestDistance = attackRange;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            float distance = Vector2.Distance(body.position, enemy.transform.position);
            if (distance > bestDistance || !HasClearShot(enemy))
                continue;

            bestDistance = distance;
            bestTarget = enemy;
        }

        currentTarget = bestTarget;

        if (currentTarget == null)
            RefreshEnemyList();
    }

    private bool HasClearShot(EnemyHealth enemy)
    {
        if (enemy == null)
            return false;

        Vector2 start = body.position;
        Vector2 end = enemy.transform.position;
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

        return closestCollider != null &&
               closestCollider.GetComponentInParent<EnemyHealth>() == enemy;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showAttackRange)
            return;

        UnityEditor.Handles.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        UnityEditor.Handles.DrawWireDisc(
            transform.position,
            Vector3.forward,
            Mathf.Max(0.5f, attackRange));
    }
#endif
}
