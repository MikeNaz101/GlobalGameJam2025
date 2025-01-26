using UnityEngine;
using UnityEngine.UIElements;

//using namespace ProgressBar;
public class Player : MonoBehaviour
{
    public int maxHealth = 100;
    public int maxMan = 100;

    public int maxSpeed;
    public int maxStamina;
    public int currentHealth;
    public int currentMan;
    public int currentSpeed;
    public int currentStamina;
    public ProgressBar healthBar;
    public GameObject projectilePrefab;  // Drag your projectile prefab here
    public Transform firePoint;  // Point from which the projectile will fire
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHealth = maxHealth;
        currentMan = maxMan;
        currentSpeed = maxSpeed;
        currentStamina = maxStamina;

        // Initialize the health bar to reflect the full health
        if (healthBar != null)
        {
            healthBar.SetMaxStats(maxHealth); // This is where you set max health in your health bar
            healthBar.SetHealth(currentHealth); // Update the health bar to match the current health
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Call the Attack method to handle mouse input
        Attck();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth); // Ensure health doesn't go below 0
        healthBar.SetHealth(currentHealth);
    }
    
    void Attck()
    {
        if (Input.GetMouseButtonDown((int)MouseButton.LeftMouse)) // Left-click attack
        {
            FireProjectile();  // Fire the projectile when the player clicks the mouse
            // You can modify this to actually affect enemies, for now, it's just a placeholder
        }
    }

    void FireProjectile()
    {
        // Instantiate the projectile at the firePoint
        if (projectilePrefab != null && firePoint != null)
        {
            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            
            // Optionally, you could set the speed of the projectile here if needed
            // projectile.GetComponent<PlayerProjectile>().speed = 20f;  // Or any value you want to set dynamically
        }
    }
}
