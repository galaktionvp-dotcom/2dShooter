using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float deceleration = 40f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (GetComponent<PlayerShooting>() == null)
            gameObject.AddComponent<PlayerShooting>();
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = Vector2.zero;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            moveInput.y += 1f;

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            moveInput.y -= 1f;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            moveInput.x += 1f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            moveInput.x -= 1f;

        moveInput = moveInput.normalized;
    }

    private void FixedUpdate()
    {
        Vector2 targetVelocity = moveInput * maxSpeed;
        float rate = moveInput.sqrMagnitude > 0f ? acceleration : deceleration;

        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity,
            targetVelocity,
            rate * Time.fixedDeltaTime
        );
    }
}
