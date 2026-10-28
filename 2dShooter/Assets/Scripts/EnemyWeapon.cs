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
    [SerializeField, Range(0f, 20f)] private float spreadDegrees = 5f;

    [Header("Магазин врага")]
    [InspectorLabel("Размер магазина", "Сколько выстрелов враг делает до перезарядки.")]
    [SerializeField, Min(1)] private int magazineSize = 8;
    [InspectorLabel("Длительность перезарядки", "Сколько секунд занимает перезарядка врага.")]
    [SerializeField, Min(0f)] private float reloadDuration = 1.5f;

    private EnemyHealth health;
    private PlayerHealth player;
    private CharacterSpriteAnimator characterAnimator;
    private int ammoInMagazine;
    private float reloadFinishTime;
    private bool isReloading;
    private float nextShot;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        characterAnimator = GetComponent<CharacterSpriteAnimator>();
        magazineSize = Mathf.Max(1, magazineSize);
        reloadDuration = Mathf.Max(0f, reloadDuration);
        ammoInMagazine = magazineSize;

        if (target == null)
        {
            player = FindFirstObjectByType<PlayerHealth>();
            if (player != null) target = player.transform;
        }
        else player = target.GetComponentInParent<PlayerHealth>();
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;
        if (target == null || player == null || player.IsDead)
        {
            player = FindFirstObjectByType<PlayerHealth>();
            if (player == null) return;
            target = player.transform;
        }

        Vector2 aim = (Vector2)target.position - (Vector2)transform.position;
        if (aim.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);

        if (isReloading)
        {
            if (Time.time < reloadFinishTime)
                return;

            isReloading = false;
            ammoInMagazine = magazineSize;
            characterAnimator?.FinishReload();
        }

        if (ammoInMagazine <= 0)
        {
            StartReload();
            return;
        }

        if (aim.magnitude > attackRange || Time.time < nextShot) return;
        nextShot = Time.time + fireInterval;
        ammoInMagazine--;
        aim = Quaternion.Euler(0, 0, Random.Range(-spreadDegrees, spreadDegrees)) * aim.normalized;

        characterAnimator?.PlayShoot();
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

    private void StartReload()
    {
        isReloading = true;
        reloadFinishTime = Time.time + reloadDuration;
        characterAnimator?.BeginReload(reloadDuration);

        if (reloadDuration <= 0f)
        {
            isReloading = false;
            ammoInMagazine = magazineSize;
            characterAnimator?.FinishReload();
        }
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
