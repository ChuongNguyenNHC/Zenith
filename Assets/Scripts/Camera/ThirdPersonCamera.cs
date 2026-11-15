using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target To Follow")]
    [Tooltip("Đối tượng nhân vật mà Camera cần bám theo")]
    [SerializeField] private Transform target;

    [Header("Camera Boom Settings")]
    [Tooltip("Điểm neo tính từ chân/tâm nhân vật (X: lệch vai, Y: chiều cao vai/ngực tính từ pivot Player, Z: tiến/lùi)")]
    [SerializeField] private Vector3 pivotOffset = new Vector3(0.24f, 0.52f, 0.0f);

    [Tooltip("Khoảng cách chuẩn từ pivot tới camera")]
    [SerializeField] private float cameraDistance = 5.0f;

    [SerializeField] private float minZoomDistance = 3.2f;
    [SerializeField] private float maxZoomDistance = 7.5f;
    [SerializeField] private float zoomScrollStep = 0.35f;

    [Tooltip("Độ bám mượt vị trí pivot (cao = bám sát tức thì, không rung lắc khi xoay chuột)")]
    [SerializeField] private float followDamping = 22f;

    [Header("Mouse Orbit / Rotation")]
    [SerializeField] private float mouseSensitivityX = 1.2f;
    [SerializeField] private float mouseSensitivityY = 1.0f;
    [SerializeField] private float maxPitch = 65.0f;
    [SerializeField] private float minPitch = -22.0f;

    [Header("Dynamic FOV")]
    [SerializeField] private float baseFov = 62f;
    [SerializeField] private float sprintFovBoost = 4.0f;
    [SerializeField] private float fovDamping = 8f;

    [Header("Target Lock-On Framing")]
    [SerializeField] private float lockOnYawDamping = 12f;
    [SerializeField] private float lockOnPitchTarget = 14f;
    [SerializeField] private float lockOnPitchDamping = 5f;
    [SerializeField] private float lockOnMinHorizontalDistance = 1.0f;
    [SerializeField] private Vector3 lockOnPivotOffset = new Vector3(0.32f, 0.58f, 0.0f);
    [SerializeField] private float lockOnDistanceOffset = 0.65f;

    [Header("Wall Collision")]
    [SerializeField] private bool enableCollision = true;
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField] private float collisionRadius = 0.22f;
    [SerializeField] private float minDistance = 0.90f;

    private Camera cam;
    private float currentYaw;
    private float currentPitch = 13.0f;
    private float currentDistance;
    private Vector3 smoothedTargetPos;

    private Transform lockOnTarget;
    private float lockOnBlend;

    private bool isSprinting;
    private float shakeTimer;
    private float shakeDuration;
    private float shakeIntensity;

    public float CurrentDistance => currentDistance;
    public float CurrentFov => cam != null ? cam.fieldOfView : baseFov;
    public float CurrentYaw => currentYaw;
    public float CurrentPitch => currentPitch;
    public Vector3 PivotOffset => pivotOffset;
    public Transform LockedTarget => lockOnTarget;
    public bool IsLockedOn => lockOnTarget != null && lockOnTarget.gameObject.activeInHierarchy;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        currentDistance = cameraDistance;

        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
            smoothedTargetPos = target.position;
            ApplyCameraPose(Time.deltaTime);
        }
    }

    private void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (lockOnTarget != null && !lockOnTarget.gameObject.activeInHierarchy)
        {
            lockOnTarget = null;
        }

        HandleInput();
        UpdateLockOnFraming(Time.deltaTime);
        ApplyCameraPose(Time.deltaTime);
        UpdateDynamicFov(Time.deltaTime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            smoothedTargetPos = target.position;
            currentYaw = target.eulerAngles.y;
        }
    }

    public void SetLockOnTarget(Transform newLockTarget)
    {
        lockOnTarget = newLockTarget;
    }

    public void SetSprinting(bool sprinting)
    {
        isSprinting = sprinting;
    }

    public void AddImpactShake(float intensity, float duration)
    {
        shakeIntensity = Mathf.Max(shakeIntensity, intensity);
        shakeDuration = Mathf.Max(duration, 0.01f);
        shakeTimer = shakeDuration;
    }

    private void HandleInput()
    {
        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            if (!IsLockedOn)
            {
                currentYaw += mouseDelta.x * mouseSensitivityX * 0.1f;

                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) >= 1f)
                {
                    cameraDistance = Mathf.Clamp(
                        cameraDistance - Mathf.Sign(scrollY) * zoomScrollStep,
                        minZoomDistance,
                        maxZoomDistance);
                }
            }
            currentPitch -= mouseDelta.y * mouseSensitivityY * 0.1f;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }
    }

    private void UpdateLockOnFraming(float dt)
    {
        bool locked = IsLockedOn;
        lockOnBlend = Mathf.Lerp(lockOnBlend, locked ? 1f : 0f, 1f - Mathf.Exp(-10f * dt));
        if (!locked) return;

        Vector3 toEnemy = lockOnTarget.position - target.position;
        Vector3 toEnemyFlat = new Vector3(toEnemy.x, 0f, toEnemy.z);
        if (toEnemyFlat.sqrMagnitude >= lockOnMinHorizontalDistance * lockOnMinHorizontalDistance)
        {
            float targetYaw = Quaternion.LookRotation(toEnemyFlat).eulerAngles.y;
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, 1f - Mathf.Exp(-lockOnYawDamping * dt));
        }

        currentPitch = Mathf.Lerp(currentPitch, lockOnPitchTarget, 1f - Mathf.Exp(-lockOnPitchDamping * dt));
    }

    private void ApplyCameraPose(float dt)
    {
        // Smooth ONLY the target pivot position so orbiting the mouse never causes chord lag/wobble.
        float pivotAlpha = 1f - Mathf.Exp(-followDamping * dt);
        smoothedTargetPos = Vector3.Lerp(smoothedTargetPos, target.position, pivotAlpha);

        Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0.0f);
        Vector3 effectivePivot = Vector3.Lerp(pivotOffset, lockOnPivotOffset, lockOnBlend);
        Vector3 pivotPoint = smoothedTargetPos + (targetRotation * effectivePivot);

        float baseDistance = cameraDistance + lockOnDistanceOffset * lockOnBlend;
        float desiredDistance = enableCollision
            ? ComputeCollisionDistance(pivotPoint, targetRotation * Vector3.back, baseDistance)
            : baseDistance;

        // Pull in fast on wall contact, recover smoothly when clearing walls.
        float distRate = desiredDistance < currentDistance ? 35f : 12f;
        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, 1f - Mathf.Exp(-distRate * dt));

        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;
            float falloff = Mathf.Clamp01(shakeTimer / shakeDuration);
            shakeOffset = Random.insideUnitSphere * (shakeIntensity * falloff);
            if (shakeTimer <= 0f) shakeIntensity = 0f;
        }

        transform.position = pivotPoint - (targetRotation * Vector3.forward * currentDistance) + shakeOffset;
        transform.rotation = targetRotation;
    }

    private float ComputeCollisionDistance(Vector3 pivotPoint, Vector3 backDirection, float maxTargetDistance)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            pivotPoint,
            collisionRadius,
            backDirection,
            maxTargetDistance,
            collisionLayers,
            QueryTriggerInteraction.Ignore);

        float closest = maxTargetDistance;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (col.transform == target || col.transform.IsChildOf(target)) continue;
            if (col.CompareTag("Enemy") || col.CompareTag("Player")) continue;

            if (hits[i].distance < closest)
            {
                closest = hits[i].distance;
            }
        }

        return Mathf.Clamp(closest, minDistance, maxTargetDistance);
    }

    private void UpdateDynamicFov(float dt)
    {
        if (cam == null) return;
        float targetFov = isSprinting ? baseFov + sprintFovBoost : baseFov;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovDamping * dt));
    }
}
