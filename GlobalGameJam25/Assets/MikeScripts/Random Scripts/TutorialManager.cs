using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Collections; // For Coroutines

public class TutorialManager : MonoBehaviour
{
    public List<string> tutorialMessages;
    public TextMeshProUGUI tutorialTextUI;
    public GameObject tutorialPanelUI;
    public TypeWriterEffect typeWriterEffect; // Reference to the TypeWriterEffect script
    public float messageVisibleDuration = 3f; // How long the full message stays after typing

    public static TutorialManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple TutorialManagers found. Destroying the extra.");
            Destroy(gameObject);
        }

        if (tutorialPanelUI != null)
        {
            tutorialPanelUI.SetActive(false); // Hide the panel initially
        }

        if (typeWriterEffect == null)
        {
            Debug.LogError("TypeWriterEffect not assigned in TutorialManager!");
        }
    }

    public void ShowTutorial(string tutorialID)
    {
        if (int.TryParse(tutorialID, out int messageIndex))
        {
            if (messageIndex >= 0 && messageIndex < tutorialMessages.Count)
            {
                if (tutorialTextUI != null && tutorialPanelUI != null && typeWriterEffect != null)
                {
                    tutorialPanelUI.SetActive(true);
                    typeWriterEffect.StartTyping(tutorialTextUI, tutorialMessages[messageIndex], messageVisibleDuration);
                    Debug.Log("TutorialManager: Showing tutorial " + tutorialID + ": " + tutorialMessages[messageIndex]);
                }
                else
                {
                    Debug.LogError("Tutorial UI elements or TypeWriterEffect not assigned!");
                }
            }
            else
            {
                Debug.LogError("TutorialManager: Invalid tutorial ID: " + tutorialID);
            }
        }
        else
        {
            Debug.LogError("TutorialManager: Invalid tutorial ID format: " + tutorialID);
        }
    }

    public void HideTutorial()
    {
        if (tutorialPanelUI != null)
        {
            tutorialPanelUI.SetActive(false);
        }
    }
}