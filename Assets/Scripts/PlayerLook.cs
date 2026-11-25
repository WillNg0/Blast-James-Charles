using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    [Header("Camera")]
    public Camera cam;

    [SerializeField] private float distance = 5f;
    [SerializeField] private float height = 1.5f;

    [Header("Camera Rotation")]
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Camera Follow")]
    [SerializeField] private float followSpeed = 15f;

    private float yaw;
    private float pitch = 15f;

    private void Start()
    {
        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Start camera facing the same direction as player
        yaw = transform.eulerAngles.y;
    }

    public void ProcessLook(Vector2 input)
    {
        // Trackpad / mouse movement
        yaw += input.x * sensitivity;
        pitch -= input.y * sensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (cam == null)
            return;

        Quaternion rotation =
            Quaternion.Euler(pitch, yaw, 0f);

        // Orbit around a point above Mushy's feet
        Vector3 targetPosition =
            transform.position + Vector3.up * height;

        Vector3 desiredPosition =
            targetPosition -
            rotation * Vector3.forward * distance;

        cam.transform.position = Vector3.Lerp(
            cam.transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        cam.transform.rotation = rotation;
    }
}