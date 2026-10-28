using InspectorLocalization;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerShooting : MonoBehaviour
{
    [Header("Оружие")]
    [InspectorLabel("Скорострельность", "Количество выстрелов в секунду.")]
    [SerializeField, Min(0.1f)] private float fireRate = 8f;

    [InspectorLabel("Скорость пули", "Скорость полёта пули в единицах Unity в секунду.")]
    [SerializeField, Min(0.1f)] private float bulletSpeed = 18f;

    [InspectorLabel("Урон пули", "Количество здоровья, которое отнимает одно попадание.")]
    [SerializeField, Min(0f)] private float bulletDamage = 1f;

    [InspectorLabel("Время жизни пули", "Сколько секунд пуля существует после создания.")]
    [SerializeField, Min(0.01f)] private float bulletLifetime = 1.5f;

    [InspectorLabel("Размер магазина", "Количество патронов в одном магазине.")]
    [SerializeField, Min(1)] private int magazineSize = 12;

    [InspectorLabel("Время перезарядки", "Время, необходимое для полной перезарядки, в секундах.")]
    [SerializeField, Min(0f)] private float reloadTime = 1.1f;

    [InspectorLabel("Разброс", "Максимальный угол отклонения пули от направления прицеливания, в градусах.")]
    [SerializeField, Min(0f)] private float spreadAngle = 2.5f;

    [Header("Обратная связь")]
    [InspectorLabel("Дистанция отдачи", "Расстояние, на которое игрок отталкивается назад после выстрела.")]
    [SerializeField, Min(0f)] private float recoilDistance = 0.08f;

    [InspectorLabel("Длительность вспышки", "Сколько секунд отображается вспышка при выстреле.")]
    [SerializeField, Min(0f)] private float muzzleFlashDuration = 0.04f;

    private static Sprite whiteSprite;
    private Camera mainCamera;
    private float nextShotTime;
    private int ammo;
    private bool reloading;
    private float reloadFinishTime;
    private float lastShotTime = -10f;

    public bool IsReloading => reloading;
    public bool IsFiring => Time.time - lastShotTime < 0.12f;
    public float LastShotTime => lastShotTime;
    public int CurrentAmmo => ammo;

    private void Awake()
    {
        fireRate = Mathf.Max(0.1f, fireRate);
        bulletSpeed = Mathf.Max(0.1f, bulletSpeed);
        bulletLifetime = Mathf.Max(0.01f, bulletLifetime);
        magazineSize = Mathf.Max(1, magazineSize);
        reloadTime = Mathf.Max(0f, reloadTime);
        spreadAngle = Mathf.Max(0f, spreadAngle);
        bulletDamage = Mathf.Max(0f, bulletDamage);

        ammo = magazineSize;
        mainCamera = Camera.main;
        EnsureWhiteSprite();
    }

    private void Update()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        AimAtMouse();

        if (reloading)
        {
            if (Time.time >= reloadFinishTime)
            {
                reloading = false;
                ammo = magazineSize;
            }

            return;
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartReload();
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            TryShoot();

        if (ammo <= 0)
            StartReload();
    }

    private void AimAtMouse()
    {
        if (mainCamera == null || Mouse.current == null)
            return;

        Vector3 mouseScreenPosition = Mouse.current.position.ReadValue();
        mouseScreenPosition.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);

        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        Vector2 direction = mouseWorldPosition - transform.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void TryShoot()
    {
        if (Time.time < nextShotTime || ammo <= 0)
            return;

        nextShotTime = Time.time + 1f / Mathf.Max(0.1f, fireRate);
        ammo--;
        lastShotTime = Time.time;

        float spread = Random.Range(-spreadAngle, spreadAngle);
        Vector2 direction = Quaternion.Euler(0f, 0f, spread) * transform.right;

        SpawnBullet(direction);
        ApplyRecoil(direction);
        ShowMuzzleFlash(direction);
    }

    private void SpawnBullet(Vector2 direction)
    {
        GameObject bulletObject = new GameObject("Bullet");
        bulletObject.transform.position = transform.position + (Vector3)(direction * 0.55f);
        bulletObject.transform.right = direction;

        SpriteRenderer renderer = bulletObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = new Color(1f, 0.85f, 0.2f);
        bulletObject.transform.localScale = new Vector3(0.16f, 0.06f, 1f);

        Rigidbody2D bulletRb = bulletObject.AddComponent<Rigidbody2D>();
        bulletRb.gravityScale = 0f;
        bulletRb.bodyType = RigidbodyType2D.Dynamic;
        bulletRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        bulletRb.linearVelocity = direction * bulletSpeed;

        CircleCollider2D bulletCollider = bulletObject.AddComponent<CircleCollider2D>();
        bulletCollider.isTrigger = true;
        bulletCollider.radius = 0.5f;

        Bullet bullet = bulletObject.AddComponent<Bullet>();
        bullet.Initialize(bulletLifetime, bulletDamage, transform);
    }

    private void ApplyRecoil(Vector2 direction)
    {
        transform.position -= (Vector3)(direction * recoilDistance);
    }

    private void ShowMuzzleFlash(Vector2 direction)
    {
        if (muzzleFlashDuration <= 0f)
            return;

        GameObject flash = new GameObject("MuzzleFlash");
        flash.transform.position = transform.position + (Vector3)(direction * 0.65f);
        flash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        SpriteRenderer renderer = flash.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = new Color(1f, 0.65f, 0.15f, 0.9f);
        flash.transform.localScale = new Vector3(0.35f, 0.14f, 1f);

        Destroy(flash, muzzleFlashDuration);
    }

    private void StartReload()
    {
        if (reloading || ammo == magazineSize)
            return;

        reloading = true;
        reloadFinishTime = Time.time + reloadTime;
    }

    private static Sprite GetWhiteSprite()
    {
        EnsureWhiteSprite();
        return whiteSprite;
    }

    private static void EnsureWhiteSprite()
    {
        if (whiteSprite != null)
            return;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "GeneratedWhiteTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        whiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
        whiteSprite.name = "GeneratedWhiteSprite";
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
    }
}
