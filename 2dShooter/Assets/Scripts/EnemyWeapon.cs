using InspectorLocalization;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyWeapon : MonoBehaviour
{
    [InspectorLabel("Цель", "Начальная цель врага. Во время игры враг автоматически выбирает ближайшего игрока или союзника.")]
    [SerializeField] private Transform target;

    [InspectorLabel("Дальность стрельбы", "Максимальное расстояние до игрока или союзника для выстрела.")]
    [SerializeField, Min(1f)] private float attackRange = 6f;

    [InspectorLabel("Интервал выстрелов", "Пауза между выстрелами врага.")]
    [SerializeField, Min(0.2f)] private float fireInterval = 1.4f;

    [InspectorLabel("Урон пули", "Урон, который наносит одно попадание игроку или союзнику.")]
    [SerializeField, Min(0f)] private float bulletDamage = 8f;

    [InspectorLabel("Скорость пули", "Скорость полёта снаряда.")]
    [SerializeField, Min(1f)] private float bulletSpeed = 10f;

    [InspectorLabel("Разброс", "Небольшой разброс, чтобы враг не попадал всегда.")]
    [SerializeField, Range(0f, 20f)] private float spreadDegrees = 7f;

    [InspectorLabel("Размер очереди", "Количество выстрелов в короткой очереди.")]
    [SerializeField, Range(1, 4)] private int burstSize = 3;

    [InspectorLabel("Пауза между очередями", "Время отдыха после очереди, чтобы цель успевала уклониться.")]
    [SerializeField, Min(0f)] private float burstPause = 1.2f;

    [InspectorLabel("Снаряд врага", "Заранее размещённый в сцене объект с EnemyProjectile. Повторно используется при каждом выстреле.")]
    [SerializeField] private EnemyProjectile projectile;

    private EnemyHealth health;
    private PlayerHealth player;
    private AllyHealth[] allies;
    private float nextShot;
    private int shotsInBurst;

    public bool IsFiring { get; private set; }
    public bool IsReloading { get; private set; }
    public float LastShotTime { get; private set; } = -10f;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();

        if (target != null)
            player = target.GetComponentInParent<PlayerHealth>();

        if (player == null)
            player = FindFirstObjectByType<PlayerHealth>();

        allies = FindObjectsByType<AllyHealth>(FindObjectsSortMode.None);

        if (projectile == null)
            projectile = GetComponentInChildren<EnemyProjectile>(true);

        if (projectile != null)
            projectile.PreparePool(transform);
        else
            Debug.LogError("[EnemyWeapon] Не назначен заранее размещённый снаряд EnemyProjectile.", this);
    }

    private void Update()
    {
        IsFiring = false;
        if ((health != null && health.IsDead) || projectile == null)
            return;

        SelectNearestTarget();
        if (target == null)
            return;

        Vector2 aim = (Vector2)target.position - (Vector2)transform.position;
        if (aim.magnitude > attackRange || Time.time < nextShot)
            return;

        if (shotsInBurst >= Mathf.Max(1, burstSize))
        {
            shotsInBurst = 0;
            IsReloading = true;
            nextShot = Time.time + burstPause;
            return;
        }

        aim = Quaternion.Euler(
            0f,
            0f,
            Random.Range(-spreadDegrees, spreadDegrees)) * aim.normalized;

        if (!projectile.Launch(
                transform,
                (Vector2)transform.position + aim * 0.5f,
                aim,
                bulletDamage,
                bulletSpeed))
        {
            return;
        }

        IsReloading = false;
        nextShot = Time.time + fireInterval;
        shotsInBurst++;
        IsFiring = true;
        LastShotTime = Time.time;
    }

    private void SelectNearestTarget()
    {
        Transform bestTarget = null;
        float bestDistance = attackRange;

        if (player == null || player.IsDead)
            player = FindFirstObjectByType<PlayerHealth>();

        if (player != null && !player.IsDead)
            ConsiderTarget(player.transform, ref bestTarget, ref bestDistance);

        if (allies == null || allies.Length == 0)
            allies = FindObjectsByType<AllyHealth>(FindObjectsSortMode.None);

        for (int i = 0; i < allies.Length; i++)
        {
            AllyHealth ally = allies[i];
            if (ally == null || ally.IsDead)
                continue;

            ConsiderTarget(ally.transform, ref bestTarget, ref bestDistance);
        }

        target = bestTarget;
    }

    private void ConsiderTarget(
        Transform candidate,
        ref Transform bestTarget,
        ref float bestDistance)
    {
        if (candidate == null)
            return;

        float distance = Vector2.Distance(transform.position, candidate.position);
        if (distance <= bestDistance)
        {
            bestDistance = distance;
            bestTarget = candidate;
        }
    }
}
