using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    public Transform firePoint;
    public float bulletForce = 20f;
    private int timeCharged = 0;
    private bool _isCharging = false;
    private BasicBulletState _currentBasicBullet;
    private FreezeBulletState _currentFreezeBullet;
    public PlayerStateManager player;
    public BulletStateManager bullet;

    void Update()
    {
        // Scroll wheel input (remains the same)
        float scrollWheelInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollWheelInput != 0)
        {
            int changeDirection = scrollWheelInput > 0 ? 1 : -1;
            BulletStateManager.Instance.ChangeBulletType(changeDirection);
        }

        // Left Mouse Button Down
        if (Input.GetMouseButtonDown(0))
        {
            _isCharging = true;
            StartAttack();
        }
        if (_isCharging)
        {
            timeCharged = timeCharged + (int)Time.deltaTime;
        }

        // Left Mouse Button Up
        if (Input.GetMouseButtonUp(0))
        {
            EndAttack();
            _isCharging = false;
        }
    }

    void StartAttack()
    {
        // Common logic for *all* bullet types: Instantiate and set color.
        GameObject newBullet = Instantiate(bullet.bulletPrefab, firePoint.position, firePoint.rotation);
        newBullet.GetComponent<Renderer>().material.color = bullet.GetCurrentBulletColor();

        float chargeMultiplier = 1f; // Default multiplier

        // Add Rigidbody and apply force *here*, for all bullets.
        Rigidbody rb = newBullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            //NO FORCE IS ADDED YET
        }
        else
        {
            Debug.LogError("Bullet prefab is missing a Rigidbody!"); // Good practice!
        }

        if (bullet.CurrentBulletType == BulletType.Type1 && player.currentMana >= 10)
        {
            // Basic Bullet specific logic (charging)
            _currentBasicBullet = newBullet.GetComponent<BasicBulletState>(); // Get from the *instantiated* bullet
            if (_currentBasicBullet != null)
            {
                _currentBasicBullet.StartCharging();
                _isCharging = true;
            }
            player.UseMana(10, player);
        }
        else if (bullet.CurrentBulletType == BulletType.Type3 && player.currentMana >= 20)
        {
            // Freeze Bullet specific logic (charging)
            _currentFreezeBullet = newBullet.GetComponent<FreezeBulletState>(); // Get from the *instantiated* bullet
            if (_currentFreezeBullet != null)
            {
                _currentFreezeBullet.StartCharging();
                _isCharging = true;
            }
            player.UseMana(20, player);
        }
        else if (bullet.CurrentBulletType == BulletType.Type2 && player.currentMana >= 15)
        {
            // Teleportation Bullet (no charging, already fired)
            chargeMultiplier = 1f; // No charge for teleport
            player.UseMana(15, player);
        }
        // Apply force *after* checking for charge
        if (rb != null)
        {
            rb.AddForce(firePoint.forward * bulletForce * chargeMultiplier, ForceMode.Impulse);
        }
    }


    void EndAttack()
    {
        //Charging Logic
        if (_isCharging)
        {
            _isCharging = false;

            //Get Multiplier from charge
            float chargeMultiplier = 1f;
            if (_currentBasicBullet != null)
            {
                //chargeMultiplier = _currentBasicBullet.StopCharging(); // Get charge level and stop
                _currentBasicBullet = null; // Clean up
            }
            if (_currentFreezeBullet != null)
            {
                //chargeMultiplier = _currentFreezeBullet.StopCharging();
                _currentFreezeBullet = null;
            }

            // Find the Rigidbody of the instantiated bullet and modify its velocity.
            //This is the fix for our original problem of where to put the force.
            foreach (var rBody in FindObjectsOfType<Rigidbody>())
            {
                //This is the key! We check the name, since it's the bullet we just made!
                if (rBody.gameObject.name.StartsWith(bullet.bulletPrefab.name))
                {
                    //Get the direction and magnitude
                    Vector3 fireDirection = firePoint.forward;
                    float fireMagnitude = bulletForce * chargeMultiplier;

                    //Multiply our force
                    rBody.linearVelocity = fireDirection * fireMagnitude;
                    break; // Exit after we've done what we need to do
                }
            }

        }
    }
}