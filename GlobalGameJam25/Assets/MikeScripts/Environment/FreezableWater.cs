using UnityEngine;
using System.Collections; // Required for Coroutine

[RequireComponent(typeof(Collider))] // Ensure a Collider is present
[RequireComponent(typeof(Renderer))] // Ensure a Renderer is present
public class FreezableWater : MonoBehaviour
{
    [Header("Freezing Properties")]
    [Tooltip("The material to apply when the water is frozen.")]
    public Material frozenMaterial;

    [Tooltip("How long the water stays frozen (solid) in seconds.")]
    public float freezeDuration = 5.0f;

    // --- Private Variables ---
    private Collider objectCollider;
    private Renderer objectRenderer;
    private Material originalMaterial;
    private bool isFrozen = false;
    private Coroutine unfreezeCoroutine; // To keep track of the unfreeze timer

    void Awake()
    {
        // Get references to required components
        objectCollider = GetComponent<Collider>();
        objectRenderer = GetComponent<Renderer>();

        // Store the original material at the start
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material; // Store the instance material
        }
        else
        {
            Debug.LogError("FreezableWater: Renderer component not found!", this);
            enabled = false; // Disable script if no renderer
            return;
        }

        // Check if required fields are set
        if (frozenMaterial == null)
        {
            Debug.LogError("FreezableWater: Frozen Material has not been assigned in the Inspector!", this);
            enabled = false; // Disable script if frozen material is missing
            return;
        }

        // --- CORRECTED CHECK ---
        // Verify the collider starts as a trigger
        if (!objectCollider.isTrigger)
        {
            Debug.LogWarning("FreezableWater: Collider attached to this object starts as non-trigger (Is Trigger = false). It should ideally start as a Trigger (Is Trigger = true) for OnTriggerEnter to detect bullets.", this);
        }
    }

    // --- CORRECTED METHOD ---
    // This method is called when another collider enters this object's trigger volume
    void OnTriggerEnter(Collider other)
    {
        // Ignore triggers if already frozen
        if (isFrozen)
        {
            return;
        }

        // Check if the object that entered our trigger has the FreezeBullet component
        // Use 'other.gameObject' for trigger events
        FreezeBullet freezeBullet = other.gameObject.GetComponent<FreezeBullet>();

        if (freezeBullet != null)
        {
            Debug.Log($"Water trigger entered by {other.gameObject.name}. Freezing!");

            // We found a freeze bullet, initiate the freezing process
            Freeze();

            // Optional: Destroy the bullet when it enters the water trigger
            // Destroy(other.gameObject);
            // Note: Check if your FreezeBullet needs to be destroyed here,
            // or if its own logic handles hitting triggers appropriately.
        }
    }

    void Freeze()
    {
        if (isFrozen) return; // Double-check to prevent issues

        isFrozen = true;

        // Change material to frozen material
        objectRenderer.material = frozenMaterial;

        // --- CORRECTED LOGIC ---
        // Set collider to be NON-trigger (solid)
        objectCollider.isTrigger = false;

        // Stop any previous unfreeze timer if it exists (safety measure)
        if (unfreezeCoroutine != null)
        {
            StopCoroutine(unfreezeCoroutine);
        }

        // Start the timer to unfreeze
        unfreezeCoroutine = StartCoroutine(UnfreezeTimer());
    }

    IEnumerator UnfreezeTimer()
    {
        // Wait for the specified duration
        yield return new WaitForSeconds(freezeDuration);

        // Time's up, unfreeze the water
        Unfreeze();
    }

    void Unfreeze()
    {
        // Only unfreeze if currently frozen
        if (!isFrozen) return;

        Debug.Log("Unfreezing water.");
        isFrozen = false;

        // Change material back to the original
        objectRenderer.material = originalMaterial;

        // --- CORRECTED LOGIC ---
        // Set collider back to being a trigger (passable)
        objectCollider.isTrigger = true;

        // Clear the coroutine tracker
        unfreezeCoroutine = null;
    }

    // Optional: Ensure state resets if the object is disabled/destroyed while frozen
    void OnDisable()
    {
        // If the object is disabled while frozen, stop the timer and revert immediately
        if (isFrozen)
        {
             if (unfreezeCoroutine != null)
             {
                 StopCoroutine(unfreezeCoroutine);
                 unfreezeCoroutine = null;
             }
             // Revert state immediately on disable (calls Unfreeze which sets isTrigger = true)
             Unfreeze();
        }
    }
}