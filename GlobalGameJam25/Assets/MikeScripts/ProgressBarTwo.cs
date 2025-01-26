using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode()]
public class ProgressBarTwo : MonoBehaviour
{
    #if UNITY_EDITOR
        [MenuItem("GameObject/UI/Linear Progress Bar")]
        public static void AddLinearProgressBar()
        {
            GameObject obj = Instantiate(Resources.Load<GameObject>("UI/Linear Progress Bar"));
            obj.transform.SetParent(Selection.activeGameObject.transform, false);
        }
    #endif

    public int maximum;
    public int minimum;
    public int current;
    public Image mask;
    public Image fill;
    public Color color;

    void Start()
    {
        GetCurrentFill(); // Initialize the progress bar
    }

    // Call this function from your player script whenever the mana value changes
    public void UpdateMana(int newCurrentMana) 
    {
        current = newCurrentMana;
        GetCurrentFill(); 
    }

    void Update()
    {
        GetCurrentFill();
    }

    // Call this function if the maximum mana needs to be updated
    public void UpdateMaxMana(int newMaxMana)
    {
        maximum = newMaxMana;
        GetCurrentFill();
    }

    void GetCurrentFill()
    {
        if (maximum <= minimum) 
        {
            Debug.LogError("Maximum mana must be greater than minimum mana!");
            return;
        }

        float currentOffset = current - minimum;
        float maximumOffset = maximum - minimum;
        float fillAmount = currentOffset / maximumOffset;
        mask.fillAmount = fillAmount;
        fill.color = color;
    }
}