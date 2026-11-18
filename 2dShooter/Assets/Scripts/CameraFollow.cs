using InspectorLocalization;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Следование за игроком")]
    [InspectorLabel("Цель камеры", "Трансформ персонажа, за которым следует камера. Обычно это объект player.")]
    [SerializeField] private Transform target;

    [InspectorLabel("Плавность камеры", "Время сглаживания движения камеры. Меньшее значение делает следование более быстрым.")]
    [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;

    [InspectorLabel("Опережение камеры", "Небольшое смещение камеры в направлении движения игрока.")]
    [SerializeField, Min(0f)] private float lookAheadDistance = 2f;

    [InspectorLabel("Плавность опережения", "Скорость сглаживания смещения камеры в направлении движения.")]
    [SerializeField, Min(0.01f)] private float lookAheadSmoothTime = 0.15f;

    [Header("Границы карты")]
    [InspectorLabel("Ограничивать камеру картой", "Не позволяет камере показывать пространство за пределами арены. Размер видимой области учитывается автоматически.")]
    [SerializeField] private bool constrainToBounds = true;

    [InspectorLabel("Левая нижняя граница", "Левая и нижняя координаты внутренней игровой области карты.")]
    [SerializeField] private Vector2 boundsMin = new Vector2(-15f, -10f);

    [InspectorLabel("Правая верхняя граница", "Правая и верхняя координаты внутренней игровой области карты.")]
    [SerializeField] private Vector2 boundsMax = new Vector2(15f, 10f);

    private Vector3 followVelocity;
    private Vector3 lookAheadVelocity;
    private Vector3 currentLookAhead;
    private Camera followCamera;

    private void Awake()
    {
        followCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 targetPosition = target.position;
        targetPosition.z = transform.position.z;

        Vector3 moveDirection = Vector3.zero;

        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null)
            moveDirection = targetRb.linearVelocity.normalized;

        Vector3 desiredLookAhead = moveDirection * lookAheadDistance;

        currentLookAhead = Vector3.SmoothDamp(
            currentLookAhead,
            desiredLookAhead,
            ref lookAheadVelocity,
            lookAheadSmoothTime
        );

        targetPosition += currentLookAhead;

        targetPosition = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            smoothTime
        );

        if (constrainToBounds && followCamera != null && followCamera.orthographic)
            targetPosition = ClampCameraToBounds(targetPosition);

        transform.position = targetPosition;
    }

    private Vector3 ClampCameraToBounds(Vector3 position)
    {
        float halfHeight = followCamera.orthographicSize;
        float halfWidth = halfHeight * followCamera.aspect;

        float minX = boundsMin.x + halfWidth;
        float maxX = boundsMax.x - halfWidth;
        float minY = boundsMin.y + halfHeight;
        float maxY = boundsMax.y - halfHeight;

        // Если окно камеры шире или выше арены, центрируем его по этой оси.
        position.x = minX > maxX
            ? (boundsMin.x + boundsMax.x) * 0.5f
            : Mathf.Clamp(position.x, minX, maxX);

        position.y = minY > maxY
            ? (boundsMin.y + boundsMax.y) * 0.5f
            : Mathf.Clamp(position.y, minY, maxY);

        return position;
    }
}
