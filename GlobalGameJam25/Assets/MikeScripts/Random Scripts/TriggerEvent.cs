using UnityEngine;

public class TriggerEvent : MonoBehaviour
{
    public string eventName;
    public bool triggerOnce = true;
    private bool hasBeenTriggered = false;
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("GameManager not found!");
            enabled = false;
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
                    GetComponent<Collider>().enabled = false;
                }
            }
        }
    }
}