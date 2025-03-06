using TreeEditor;
using UnityEngine;

public class RotateCamera : MonoBehaviour
{
    public float rotationSpeed;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float horizonalInput = Input.GetAxis("Horizontal");
        transform.Rotate(Vector3.up, horizonalInput * rotationSpeed * Time.deltaTime);
    }
}
