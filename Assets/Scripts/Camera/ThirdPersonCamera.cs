//Cái này khó vậy ae T_T

using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    // Mục tiêu cần bám
    [Header("Target To Follow")]
    [Tooltip("Đối tượng nhân vật mà Camera cần bám theo")]
    [SerializeField] private Transform target;


    // Camera
    [Header("Camera Boom Settings (Gậy Cần Cẩu)")]
    [Tooltip("Điểm cắm cần cẩu trên người nhân vật (X: Lệch vai phải, Y: Chiều cao ngực/vai)")]
    [SerializeField] private Vector3 pivotOffset = new Vector3(1f, 1f, 0.5f);

    [Tooltip("Độ dài cần cẩu (Khoảng cách từ camera tới nhân vật)")]
    [SerializeField] private float cameraDistance = 3.2f;

    [Tooltip("Độ làm mượt khi di chuyển bám theo nhân vật")]
    [SerializeField] private float followDamping = 15f;


    // Xoay chuột
    [Header("Mouse Orbit / Rotation")]
    [Tooltip("Độ nhạy chuột ngang (Yaw)")]
    [SerializeField] private float mouseSensitivityX = 1.2f;

    [Tooltip("Độ nhạy chuột dọc (Pitch)")]
    [SerializeField] private float mouseSensitivityY = 1.0f;

    [Tooltip("Góc chúc xuống tối đa (nhìn từ trên cao xuống lưng)")]
    [SerializeField] private float maxPitch = 60.0f;

    [Tooltip("Góc ngước lên tối đa (nhìn từ dưới đất lên)")]
    [SerializeField] private float minPitch = -20.0f;

    [Header("Wall Collision (Chống xuyên tường)")]
    [Tooltip("Bật tính năng tự co camera khi va vào tường")]
    [SerializeField] private bool enableCollision = true;
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField] private float collisionRadius = 0.2f;
    [SerializeField] private float minDistance = 0.8f;

    private float currentYaw;
    private float currentPitch = 12.0f;
    private float currentDistance;

    private void Start()
    {
        // Lock the f in brotha
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentDistance = cameraDistance;

        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
            transform.position = target.position + pivotOffset - (transform.forward * currentDistance);
        }
    }

    private void Update()
    {
        // Nhấn chuột trái để lock lại nếu lỡ click ra ngoài
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Nhấn Esc để mở chuột debug
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleInput();
        UpdateCameraTransform();
    }

    private void HandleInput()
    {
        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            currentYaw += mouseDelta.x * mouseSensitivityX * 0.1f;
            currentPitch -= mouseDelta.y * mouseSensitivityY * 0.1f;

            // Giới hạn góc nhìn trên/dưới
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }
    }

    private void UpdateCameraTransform()
    {
        
        Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0.0f);

       
        Vector3 shoulderOffset = targetRotation * pivotOffset;
        Vector3 pivotPoint = target.position + shoulderOffset;

        // Wall collision
        float desiredDistance = cameraDistance;
        if (enableCollision)
        {
            Vector3 backDirection = targetRotation * Vector3.back;
            Ray ray = new Ray(pivotPoint, backDirection);
            if (Physics.SphereCast(ray, collisionRadius, out RaycastHit hit, cameraDistance, collisionLayers))
            {
                desiredDistance = Mathf.Clamp(hit.distance, minDistance, cameraDistance);
            }
        }

        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * 20f);

        //Khóa camera vô char
        Vector3 targetCameraPosition = pivotPoint - (targetRotation * Vector3.forward * currentDistance);

        transform.position = Vector3.Lerp(transform.position, targetCameraPosition, Time.deltaTime * followDamping);
        transform.rotation = targetRotation;
    }
}
