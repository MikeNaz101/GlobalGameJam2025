using System.Collections;
using TMPro;
using UnityEngine;

public class TypeWriterEffect : MonoBehaviour
{
    [SerializeField] private float typingSpeed = 0.05f; // Speed of text appearing, like sweet juice flowing!
    private Coroutine typingCoroutine;

    // This will now start the typing AND handle hiding after a duration.
    // Pass the text UI element, the message, and how long it should stay visible AFTER typing finishes.
    public void StartTyping(TMP_Text textComponent, string text, float visibleDuration)
    {
        if (textComponent == null)
        {
            Debug.LogError("Text Component is null!", this.gameObject);
            return;
        }

        // Stop any previous typing/hiding sequence for this component
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        // Make sure the text component is active and clear before starting
        textComponent.gameObject.SetActive(true);
        textComponent.text = "";

        // Start the new typing and hiding coroutine
        typingCoroutine = StartCoroutine(TypeTextAndHide(textComponent, text, visibleDuration));
    }

    // If you need to stop typing and clear immediately (e.g., closing a menu)
    public void StopAndClear(TMP_Text textComponent)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        if (textComponent != null)
        {
            textComponent.text = "";
            textComponent.gameObject.SetActive(false);
        }
    }

    private IEnumerator TypeTextAndHide(TMP_Text textComponent, string text, float visibleDuration)
    {
        // Type out the message, character by character
        foreach (char letter in text.ToCharArray())
        {
            textComponent.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        // Keep the full message visible for the specified duration
        yield return new WaitForSeconds(visibleDuration);

        // Now, hide the text component
        textComponent.text = "";
        textComponent.gameObject.SetActive(false);

        // Reset coroutine tracker
        typingCoroutine = null;
    }
}