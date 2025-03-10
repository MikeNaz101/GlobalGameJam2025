// ShellColorController.cs
using UnityEngine;

public class ShellColorController : MonoBehaviour
{
    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
        {
            Debug.LogError("Shell prefab is missing a Renderer component!");
        }
    }

    public void SetColor(Color color)
    {
        if (_renderer != null)
        {
            _renderer.material.color = color;
        }
    }
}