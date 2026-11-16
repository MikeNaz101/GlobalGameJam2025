using UnityEngine;

public class TriggerEvent : MonoBehaviour
{
    public string eventName;
    public bool triggerOnce = true;
    private bool hasBeenTriggered = false;
    private GameManager gameManager;

    /// <summary>Fired after a trigger registers its event (used by the save system).</summary>
    public static event System.Action<TriggerEvent> OnFired;

    /// <summary>Marks a one-shot trigger as already used, without firing its event (when loading a save).</summary>
    public void MarkAlreadyTriggered()
    {
        hasBeenTriggered = true;
        if (triggerOnce)
        {
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

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
                OnFired?.Invoke(this);
                if (triggerOnce)
                {
                    GetComponent<Collider>().enabled = false;
                }
            }
        }
    }
}