using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable, IEnemyMoveaable
{
    [field: SerializeField] public float MaxHealth { get; set; } = 100f;
    public float CurrentHealth {  get; set; }
    public Rigidbody RB {  get; set; }
    public bool IsFacingRight {  get; set; } = true;

    private void Start()
    {
        CurrentHealth = MaxHealth;

        RB = GetComponent<Rigidbody>();
    }
    public void Damage(float damageAmount)
    {
        CurrentHealth -= damageAmount;

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    public void Die()
    {
    }

    #region Movement Functions
    public void MoveEnemy(Vector3 velocity)
    {
        RB.angularVelocity = velocity;
        CheckForLeftOrRightFacing(velocity);
    }

    public void CheckForLeftOrRightFacing(Vector3 velocity)
    {
        if (IsFacingRight && velocity.x > 0f)
        {
            Vector3 rotator = new Vector3(transform.rotation.x, 180f, transform.rotation.z);
            transform.rotation = Quaternion.Euler(rotator);
            IsFacingRight = !IsFacingRight;
        }
        else if (!IsFacingRight && velocity.x > 0f)
        {
            Vector3 rotator = new Vector3(transform.rotation.x, 0f, transform.rotation.z);
            transform.rotation = Quaternion.Euler(rotator);
            IsFacingRight = !IsFacingRight;
        }
    }
    #endregion 

    #region Animation Triggers

    private void AnimationTriggerEvent(AnimationTriggerType triggerType)
    {
        //TODO: fill in once State Machine is created!
    }
    public enum AnimationTriggerType
    {
        EnemyDamaged,
        PlayFootstepSound
    }
    #endregion
}