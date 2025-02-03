using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

//using namespace ProgressBar;
public class Player : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;
    public int healthRecoveryRate = 2; // Health points per second

    public int maxMana = 100;
    public int manaCost = 10;
    public int currentMana;
    public int manaRecoveryRate = 5; // Mana points per second

    public int maxStamina = 100;
    public int currentStamina;
    public int staminaRecoveryRate = 10; // Stamina points per second

    public int maxSpeed = 10;
    public int currentSpeed = 5;

    private Vector3 position;
    public ProgressBar healthBar;
    public GameObject projectilePrefab;  // projectile prefab here
    public GameObject shellPrefab;       // Visual-only projectile (dummy)
    public Transform firePoint;  // Point from which the projectile will fire

    public float orbitRadius = 2f;       // Distance from player
    public float orbitSpeed = 50f;       // Speed of rotation
    public int maxShells;            // Max floating shells
    public int currentShells;
    private List<GameObject> orbitingShells = new List<GameObject>();
    void Start()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
        currentSpeed = maxSpeed;
        currentStamina = maxStamina;
        position = transform.position;
        maxShells = maxMana / manaCost;
        currentShells = maxShells;
        SpawnSpiritBubbleShells();
        Debug.Log("Your currentHealth starts as: " + currentHealth);

        // Start recovery for all stats
        StartCoroutine(RecoverHealthOverTime());
        StartCoroutine(RecoverManaOverTime());
        StartCoroutine(RecoverStaminaOverTime());
    }

    private IEnumerator RecoverHealthOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); // Wait 1 second between recovery
            currentHealth += healthRecoveryRate;
        }
    }

    private IEnumerator RecoverManaOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f); // Wait 1 second between recovery
            currentMana += manaRecoveryRate;
            if (currentShells < (currentMana/manaCost))
            {
                currentShells++;
                orbitingShells[currentShells - 1].GetComponent<MeshRenderer>().enabled = true;
                
            }
        }
    }

    private IEnumerator RecoverStaminaOverTime()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f); // Wait 1 second between recovery
            currentStamina += staminaRecoveryRate;
        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateShellPositions();
        // Call the Attack method to handle mouse input
        Attck();
    }

    public void TakeDamage(int damage)
    {
        Debug.Log("Your currentHealth before calculating damage is: " + currentHealth);
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth); // Ensure health doesn't go below 0
        Debug.Log("Player Took  " + damage + " damage! Your health is now: " + currentHealth);

    }

    // Method to use mana
    public void UseMana(int mCost)
    {
        currentMana -= mCost;
        currentMana = Mathf.Clamp(currentMana - manaCost, 0, maxMana);
    }

    // Method to consume stamina
    public void ConsumeStamina(int staminaCost)
    {
        currentStamina -= staminaCost;
        currentStamina = Mathf.Clamp(currentStamina - staminaCost, 0, maxStamina);
    }


    void Attck()
    {
        if (currentMana > manaCost && Input.GetMouseButtonDown((int)MouseButton.LeftMouse)) // Left-click attack
        {
            //orbitingShells[currentShells].SetActive(false);
            orbitingShells[currentShells-1].GetComponent<MeshRenderer>().enabled = false;
            currentShells -= 1;
            FireProjectile();  // Fire the projectile when the player clicks the mouse
            UseMana(manaCost);
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

    void SpawnSpiritBubbleShells()
    {
        for (int i = 0; i < maxShells; i++)
        {
            Vector3 spawnPosition = transform.position + new Vector3(orbitRadius, 0, 0);
            GameObject shell = Instantiate(shellPrefab, spawnPosition, Quaternion.identity);
            orbitingShells.Add(shell);
        }
    }

    void UpdateShellPositions()
    {
        for (int i = 0; i < orbitingShells.Count; i++)
        {
            if (orbitingShells[i] != null)
            {
                float angle = Time.time * orbitSpeed + (i * 360f / maxShells);
                float x = firePoint.position.x + orbitRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
                float z = firePoint.position.z + orbitRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
                orbitingShells[i].transform.position = new Vector3(x, firePoint.position.y, z);
            }
        }
    }
}
