using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public int damage = 20; // Adjust as needed
    public float speed = 15f;
    public float lifetime = 5f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // Move the projectile forward every frame
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed; //added linear velocity instead of velocity  // Apply velocity to move the projectile
            //rb.velocity = transform.forward * speed;  // Apply velocity to move the projectile
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Debug.Log("Projectile hit the Enemy!");
            EnemyBubble enemy = other.GetComponent<EnemyBubble>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Destroy(gameObject); // Destroy projectile on impact
            }
        }
    }
}
