using UnityEngine;

public class SunMovement : MonoBehaviour
{
    // Length of one full day in real-time minutes
    public float dayLengthInMinutes = 15f;

    // Cached rotation speed
    private float rotationSpeed;

    void Start()
    {
        // Calculate rotation speed: 360 degrees per full day
        rotationSpeed = 360f / (dayLengthInMinutes * 60f);
    }

    void Update()
    {
        // Rotate around the X axis to simulate sun movement
        transform.Rotate(Vector3.right * rotationSpeed * Time.deltaTime);
    }
}
