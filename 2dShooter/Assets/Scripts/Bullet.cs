using UnityEngine;

public class Bullet : MonoBehaviour
{
    private float lifetime;

    public void Initialize(float lifetimeSeconds)
    {
        lifetime = lifetimeSeconds;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;

        if (lifetime <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<Bullet>() != null)
            return;

        Destroy(gameObject);
    }
}
