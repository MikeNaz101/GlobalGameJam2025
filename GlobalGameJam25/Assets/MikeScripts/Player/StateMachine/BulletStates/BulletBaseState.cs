using UnityEngine;

public abstract class BulletBaseState : MonoBehaviour
{
    public abstract void EnterState(BulletStateManager bullet);

    public abstract void UpdateState(BulletStateManager bullet);
    public virtual void ExitState(BulletStateManager bullet) { }
    public float speed = 20f;
    public float lifeTime = 5f;  // How long the bullet lives before self-destructing.

    protected bool hasHit = false; // Flag to prevent multiple hits.

    public virtual void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public virtual void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    // Abstract method for handling collision.  Each specific bullet type will define this.
    protected abstract void OnHit(GameObject other);

    private void OnTriggerEnter(Collider other)
    {
        if (!hasHit)
        {
            hasHit = true;  // Set the flag immediately.
            OnHit(other.gameObject); // Call the specific OnHit implementation.
        }
    }
    public void SetColor(Color color)
    {
        GetComponent<Renderer>().material.color = color;
    }
}


