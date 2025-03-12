using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    public Transform firePoint;
    public float bulletForce = 20f;
    private bool _isCharging = false;
    private BasicBulletState _currentBasicBullet;
    private FreezeBulletState _currentFreezeBullet;
    public PlayerStateManager player;
    public BulletStateManager bullet;
    private GameObject currentBullet;
    private float chargeStartTime; // Track when the charge started

    void Update()
    {
        float scrollWheelInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollWheelInput != 0)
        {
            int changeDirection = scrollWheelInput > 0 ? 1 : -1;
            bullet.ChangeBulletType(changeDirection);
        }

        if (Input.GetMouseButtonDown(0))
        {
            StartCharge(); // Instantiate bullet and start tracking charge time
        }

        if (Input.GetMouseButtonUp(0))
        {
            EndCharge(); // Apply force based on charge time
        }
    }

    void StartCharge()
    {
        currentBullet = Instantiate(bullet.bulletPrefab, firePoint.position, firePoint.rotation);
        currentBullet.GetComponent<Renderer>().material.color = bullet.GetCurrentBulletColor();

        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Bullet prefab is missing a Rigidbody!");
            return;
        }

        if (bullet.CurrentBulletType == BulletType.Type1 && player.currentMana >= 10)
        {
            _currentBasicBullet = currentBullet.GetComponent<BasicBulletState>();
            if (_currentBasicBullet != null)
            {
                _currentBasicBullet.StartCharging();
                _isCharging = true;
            }
            player.UseMana(10, player);
        }
        else if (bullet.CurrentBulletType == BulletType.Type3 && player.currentMana >= 20)
        {
            _currentFreezeBullet = currentBullet.GetComponent<FreezeBulletState>();
            if (_currentFreezeBullet != null)
            {
                _currentFreezeBullet.StartCharging();
                _isCharging = true;
            }
            player.UseMana(20, player);
        }
        else if (bullet.CurrentBulletType == BulletType.Type2 && player.currentMana >= 15)
        {
            player.UseMana(15, player);
        }
        chargeStartTime = Time.time; // Record the start time
    }

    void EndCharge()
    {
        float chargeDuration = Time.time - chargeStartTime;
        float chargeMultiplier = 1f;

        if (_isCharging)
        {
            _isCharging = false;

            if (_currentBasicBullet != null)
            {
                chargeMultiplier = _currentBasicBullet.StopCharging();
                _currentBasicBullet = null;
            }
            if (_currentFreezeBullet != null)
            {
                chargeMultiplier = _currentFreezeBullet.StopCharging();
                _currentFreezeBullet = null;
            }
        }

        Rigidbody rb = currentBullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = firePoint.forward * bulletForce * chargeMultiplier;
        }

        currentBullet = null; // Reset currentBullet
        _isCharging = false; // reset isCharging
    }
}