using UnityEngine;

public class TriggerEvent : MonoBehaviour
{
    public string eventName; // A unique name or ID for this event
    public bool triggerOnce = true; // Should this event only fire once?
    private bool hasBeenTriggered = false;
    private GameManager gameManager;

    void Start()
    {
        // Find the GameManager using FindAnyObjectByType (faster if any instance is okay)
        gameManager = FindAnyObjectByType<GameManager>();

        // Alternatively, if you need to ensure you get the *first* GameManager:
        // gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager == null)
        {
            Debug.LogError("GameManager not found in the scene!");
            enabled = false; // Disable if no GameManager
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (gameManager != null && other.CompareTag("Player"))
        {
            if (!hasBeenTriggered || !triggerOnce)
            {
                gameManager.RegisterTriggeredEvent(eventName);
                hasBeenTriggered = true;
                if (triggerOnce)
                {
                    // Optionally disable the trigger object after it fires once
                    Collider triggerCollider = GetComponent<Collider>();
                    if (triggerCollider != null)
                    {
                        triggerCollider.enabled = false;
                    }
                    // Or you could destroy the GameObject:
                    // Destroy(gameObject);
                }
            }
        }
    }
}