using InspectorLocalization;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyWeapon : MonoBehaviour
{
    [InspectorLabel("Цель", "Игрок, в которого стреляет враг.")]
    [SerializeField] private Transform target;
    [InspectorLabel("Дальность стрельбы", "Максимальное расстояние до игрока для выстрела.")]
    [SerializeField, Min(1f)] private float attackRange = 6f;
    [InspectorLabel("Интервал выстрелов", "Пауза между выстрелами врага.")]
    [SerializeField, Min(0.2f)] private float fireInterval = 1.4f;
    [InspectorLabel("Урон пули", "Урон, который наносит одно попадание.")]
    [SerializeField, Min(0f)] private float bulletDamage = 8f;
    [InspectorLabel("Скорость пули", "Скорость снаряда.")]
    [SerializeField, Min(1f)] private float bulletSpeed = 10f;
    [InspectorLabel("Разброс", "Небольшой разброс, чтобы враг не попадал всегда.")]
    [SerializeField, Range(0f, 20f)] private float spreadDegrees = 7f;
    [InspectorLabel("Размер очереди", "Количество выстрелов в короткой очереди.")]
    [SerializeField, Range(1, 4)] private int burstSize = 3;
    [InspectorLabel("Пауза между очередями", "Время отдыха после очереди, чтобы игрок успевал уклониться.")]
    [SerializeField, Min(0f)] private float burstPause = 1.2f;

    private EnemyHealth health;
    private PlayerHealth player;
    private float nextShot;
    private int shotsInBurst;
    public bool IsFiring { get; private set; }
    public bool IsReloading { get; private set; }
    public float LastShotTime { get; private set; } = -10f;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (target == null)
        {
            player = FindFirstObjectByType<PlayerHealth>();
            if (player != null) target = player.transform;
        }
        else player = target.GetComponentInParent<PlayerHealth>();
    }

    private void Update()
    {
        IsFiring = false;
        if (health != null && health.IsDead) return;
        if (target == null || player == null || player.IsDead)
        {
            player = FindFirstObjectByType<PlayerHealth>();
            if (player == null) return;
            target = player.transform;
        }

        Vector2 aim = (Vector2)target.position - (Vector2)transform.position;
        if (aim.magnitude > attackRange || Time.time < nextShot) return;

        if (shotsInBurst >= Mathf.Max(1, burstSize))
        {
            shotsInBurst = 0;
            IsReloading = true;
            nextShot = Time.time + burstPause;
            return;
        }

        IsReloading = false;
        nextShot = Time.time + fireInterval;
        shotsInBurst++;
        IsFiring = true;
        LastShotTime = Time.time;
        aim = Quaternion.Euler(0, 0, Random.Range(-spreadDegrees, spreadDegrees)) * aim.normalized;

        GameObject shot = new GameObject("EnemyBullet");
        shot.transform.position = transform.position + (Vector3)(aim * 0.5f);
        shot.transform.right = aim;
        shot.transform.localScale = new Vector3(0.2f, 0.07f, 1f);
        var renderer = shot.AddComponent<SpriteRenderer>();
        renderer.sprite = BulletSprite();
        renderer.color = new Color(1f, 0.25f, 0.12f);
        renderer.sortingOrder = 5;
        var rb = shot.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.linearVelocity = aim * bulletSpeed;
        var collider = shot.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        var bullet = shot.AddComponent<Bullet>();
        bullet.Initialize(2f, bulletDamage, transform);
    }

    private static Sprite sprite;
    private static Sprite BulletSprite()
    {
        if (sprite != null) return sprite;
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
