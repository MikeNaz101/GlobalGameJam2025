using UnityEngine;

public class PlayerBodyRotator : MonoBehaviour
{
    // You can reuse sensitivity or set a new one here
    public float mouseSensitivity = 100f;

    void Update()
    {
        // Only handle horizontal rotation based on Mouse X
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        transform.Rotate(Vector3.up * mouseX); // Use transform.Rotate since it's on the body itself
    }

    // Optional: Add cursor lock here if it's not handled elsewhere
    void Start()
    {
        // It's often better to handle cursor lock in a central game manager,
        // but you can put it here for now if needed.
        // Cursor.lockState = CursorLockMode.Locked;
    }
}