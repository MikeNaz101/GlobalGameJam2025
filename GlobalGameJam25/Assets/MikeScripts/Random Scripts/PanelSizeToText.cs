using UnityEngine;
using TMPro;

public class PanelSizeToText : MonoBehaviour
{
    private RectTransform panelRectTransform;
    private TextMeshProUGUI textMeshPro;

    public float paddingX = 10f; // Extra space on the sides
    public float paddingY = 10f; // Extra space on top and bottom

    void Start()
    {
        panelRectTransform = GetComponent<RectTransform>();
        textMeshPro = GetComponentInChildren<TextMeshProUGUI>(); // Assuming TextMeshPro is a child

        if (textMeshPro == null)
        {
            Debug.LogError("TextMeshProUGUI component not found in children!");
            enabled = false; // Disable the script if TextMeshPro is missing
        }
    }

    void Update() // Or call this whenever the text changes
    {
        if (textMeshPro != null)
        {
            Vector2 preferredSize = textMeshPro.GetPreferredValues();
            panelRectTransform.sizeDelta = new Vector2(preferredSize.x + paddingX, preferredSize.y + paddingY);
        }
    }
}