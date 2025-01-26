using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public float speed = 20f;  // Projectile speed
    public int damage = 10;    // Damage dealt by the projectile

    void Start()
    {
        // Destroy the projectile after 5 seconds to avoid clutter
        Destroy(gameObject, 5f);
    }

    void Update()
    {
        // Move the projectile forward
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        // If it hits something, apply damage (e.g., to an enemy)
        if (collision.gameObject.CompareTag("Enemy"))
        {
            // Deal damage to the enemy (assuming the enemy has a TakeDamage method)
            EnemyBubble enemy = collision.gameObject.GetComponent<EnemyBubble>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
            Destroy(gameObject);  // Destroy the projectile after collision
        }
    }
}