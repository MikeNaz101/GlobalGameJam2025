using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

//using namespace ProgressBar;
public class PlayerManager : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;
    public int healthRecoveryRate = 2; // Health points per second
    public int maxMana = 100;
    public int currentMana;
     public int manaRecoveryRate = 5; // Mana points per second

    public int maxSpeed;
    public int currentSpeed;
    public int staminaRecoveryRate = 10; // Stamina points per second

    public int maxStamina;
    public int currentStamina;
    public Vector3 position;
    
    
    
    
    public ProgressBar healthBar;
    public GameObject projectilePrefab;  // Drag your projectile prefab here
    public Transform firePoint;  // Point from which the projectile will fire
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
        currentSpeed = maxSpeed;
        currentStamina = maxStamina;
        position = transform.position;
        
        // Start recovery for all stats
        StartCoroutine(RecoverHealthOverTime());
        StartCoroutine(RecoverManaOverTime());
        StartCoroutine(RecoverStaminaOverTime());
    }

    private IEnumerator RecoverHealthOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // Wait 1 second between recovery
            RecoverHealth(healthRecoveryRate);
        }
    }

    private IEnumerator RecoverManaOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // Wait 1 second between recovery
            RecoverMana(manaRecoveryRate);
        }
    }

    private IEnumerator RecoverStaminaOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // Wait 1 second between recovery
            RecoverStamina(staminaRecoveryRate);
        }
    }

    private void RecoverHealth(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
    }

    private void RecoverMana(int amount)
    {
        currentMana = Mathf.Clamp(currentMana + amount, 0, maxMana);
    }

    private void RecoverStamina(int amount)
    {
        currentStamina = Mathf.Clamp(currentStamina + amount, 0, maxStamina);
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
        //currentHealth = Mathf.Max(0, currentHealth); // Ensure health doesn't go below 0
        Debug.Log("Player Took  " + damage + " damage!");
        //healthBar.SetHealth(currentHealth);
    }

    // Example method to use mana
    public void UseMana(int manaCost)
    {
        currentMana = Mathf.Clamp(currentMana - manaCost, 0, maxMana);
    }

    // Example method to consume stamina
    public void ConsumeStamina(int staminaCost)
    {
        currentStamina = Mathf.Clamp(currentStamina - staminaCost, 0, maxStamina);
    }

    
    void Attck()
    {
        if (currentMana > 10 && Input.GetMouseButtonDown((int)MouseButton.LeftMouse)) // Left-click attack
        {
            FireProjectile();  // Fire the projectile when the player clicks the mouse
            UseMana(10);
            // You can modify this to actually affect enemies, for now, it's just a placeholder
        }
    }

    void FireProjectile()
    {
        // Instantiate the projectile at the firePoint
        if (projectilePrefab != null && firePoint != null)
        {
            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
            //PlayerProjectile playerProjectile = projectile.GetComponent<PlayerProjectile>();
            // Aim the projectile at the player
            //Vector3 shootDirection = (firePoint.position).normalized;
            //rb.linearVelocity = shootDirection * projectileSpeed;
        
            
            // Optionally, you could set the speed of the projectile here if needed
            // projectile.GetComponent<PlayerProjectile>().speed = 20f;  // Or any value you want to set dynamically
        }
    }
}
