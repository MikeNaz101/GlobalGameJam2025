using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public int damage = 20; // Adjust as needed
    public float speed = 15f;
    public float lifetime = 3f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyBubble enemy = other.GetComponent<EnemyBubble>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
                Destroy(gameObject); // Destroy projectile on impact
            }
        }
    }
}
