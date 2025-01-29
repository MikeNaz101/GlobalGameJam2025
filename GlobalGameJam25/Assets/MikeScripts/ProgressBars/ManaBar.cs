using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;
using UnityEngine.UI;

public class ManaBar : MonoBehaviour
{
    public Slider manaSlider;

    public void SetMaxStats(int mana)
    {
        manaSlider.maxValue = mana;
        manaSlider.value = mana;
    }

    public void SetMana(int mana)
    {
        manaSlider.value = mana;
    }
}
