using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerShooting : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private float fireRate = 8f;
    [SerializeField] private float bulletSpeed = 18f;
    [SerializeField] private float bulletLifetime = 1.5f;
    [SerializeField] private int magazineSize = 12;
    [SerializeField] private float reloadTime = 1.1f;
    [SerializeField] private float spreadAngle = 2.5f;

    [Header("Feedback")]
    [SerializeField] private float recoilDistance = 0.08f;
    [SerializeField] private float muzzleFlashDuration = 0.04f;

    private Camera mainCamera;
    private float nextShotTime;
    private int ammo;
    private bool reloading;
    private float reloadFinishTime;
    private Vector3 originalScale;

    private void Awake()
    {
        ammo = magazineSize;
        originalScale = transform.localScale;
        mainCamera = Camera.main;
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

        nextShotTime = Time.time + 1f / fireRate;
        ammo--;

        float spread = Random.Range(-spreadAngle, spreadAngle);
        Vector2 direction = Quaternion.Euler(0f, 0f, spread) * transform.right;

        SpawnBullet(direction);
        ApplyRecoil();
        ShowMuzzleFlash(direction);
    }

    private void SpawnBullet(Vector2 direction)
    {
        GameObject bulletObject = new GameObject("Bullet");
        bulletObject.transform.position = transform.position + (Vector3)(direction * 0.55f);

        SpriteRenderer renderer = bulletObject.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateWhiteSprite();
        renderer.color = new Color(1f, 0.85f, 0.2f);
        bulletObject.transform.localScale = new Vector3(0.16f, 0.06f, 1f);
        bulletObject.transform.right = direction;

        Rigidbody2D bulletRb = bulletObject.AddComponent<Rigidbody2D>();
        bulletRb.gravityScale = 0f;
        bulletRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        bulletRb.linearVelocity = direction * bulletSpeed;

        CircleCollider2D collider = bulletObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.5f;

        Bullet bullet = bulletObject.AddComponent<Bullet>();
        bullet.Initialize(bulletLifetime);
    }

    private void ApplyRecoil()
    {
        transform.position -= transform.right * recoilDistance;
    }

    private void ShowMuzzleFlash(Vector2 direction)
    {
        GameObject flash = new GameObject("MuzzleFlash");
        flash.transform.position = transform.position + (Vector3)(direction * 0.65f);
        flash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        SpriteRenderer renderer = flash.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateWhiteSprite();
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

    private Sprite CreateWhiteSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.name = "GeneratedWhiteTexture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
    }
}
