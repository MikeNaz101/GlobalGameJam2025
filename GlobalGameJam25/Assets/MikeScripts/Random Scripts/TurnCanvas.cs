using UnityEngine; // Juicy core functionalities!

public class TurnCanvas : MonoBehaviour
{
    // A little spot to remember our main camera's transform
    private Transform mainCameraTransform;

    // This happens once, right at the start, like the first blush of dawn!
    void Start()
    {
        // Let's try to find the main camera, the star of our show!
        // It usually has the tag "MainCamera".
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
            // Debug.Log("Found the main camera, sweet!"); // Uncomment this line if you want a little confirmation message in your console!
        }
        else
        {
            // Oh dear, couldn't find it! Make sure your camera is tagged correctly, honey.
            Debug.LogWarning("FruityBillboardCanvas: Couldn't find the 'MainCamera'! Please make sure your main camera has the tag 'MainCamera'.");
        }
    }

    // This happens every frame, just after all the movement calculations, keeping things smooth like a peach!
    void LateUpdate()
    {
        // If we don't have our camera reference, we can't do our little dance!
        if (mainCameraTransform == null)
        {
            // Maybe the camera appeared later? Let's check again, just in case!
            if (Camera.main != null) {
                mainCameraTransform = Camera.main.transform;
            } else {
                return; // Still no camera? Can't rotate, sugarplum!
            }
        }

        // 1. Find the yummy direction from this canvas *to* the camera.
        Vector3 lookDirection = mainCameraTransform.position - transform.position;

        // 2. Create the perfect rotation! We want the *front* of the canvas (usually its 'forward' direction)
        //    to face the camera. LookRotation points the 'forward' (Z+ axis) *along* the vector.
        //    So, to make the front face the camera, we need to look in the *opposite* direction. Sneaky!
        Quaternion targetRotation = Quaternion.LookRotation(-lookDirection);

        // 3. Apply that fabulous rotation to this canvas! Now turn and pose!
        transform.rotation = targetRotation;

        // --- Alternative Peachy Method (using LookAt) ---
        // You could also use LookAt, which is simpler to type, but sometimes needs a little twist!
        // transform.LookAt(mainCameraTransform); // This makes the canvas's forward (Z+) point directly AT the camera.
                                                // Often, this means the *back* of the canvas faces the camera. Boo!
        // transform.Rotate(0f, 180f, 0f, Space.Self); // So we give it a little 180-degree twirl around its own Y-axis to face the front! Ta-da!
        // Uncomment the two lines above and comment out the 'Quaternion targetRotation = ...' and 'transform.rotation = ...' lines
        // if you prefer this method, sweetie pie!
    }
}