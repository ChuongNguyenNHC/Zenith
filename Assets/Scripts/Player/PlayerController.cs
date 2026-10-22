using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Tốc độ đi bộ")]
    [SerializeField] private float walkSpeed = 3.5f;

    [Tooltip("Tốc độ chạy nhanh (Sprint)")]
    [SerializeField] private float sprintSpeed = 6.5f;

    [Tooltip("Thời gian làm mượt góc xoay tự do của nhân vật")]
    [SerializeField] private float rotationSmoothTime = 0.12f;

    [Header("Combo Settings")]
    [Tooltip("Danh sách tên chính xác của 5 Animation State trong Animator")]
    [SerializeField] private string[] comboStateNames = new string[] 
    { 
        "Atk_P_1", 
        "Atk_P_2", 
        "elbow chop", 
        "back fist", 
        "spinning axe kick" 
    };

    [Tooltip("Hệ số tăng tốc độ ra đòn (1.0 = bình thường, 1.25 -> 1.4 = nhanh, dứt khoát chuẩn Sifu)")]
    [Range(0.8f, 2.0f)]
    [SerializeField] private float attackSpeedMultiplier = 1.3f;

    [Tooltip("Cửa sổ thời gian cho phép bấm nối tiếp combo (giây)")]
    [SerializeField] private float comboWindowTime = 1.8f;

    [Tooltip("Thời gian chuyển mượt giữa các đòn đánh")]
    [SerializeField] private float transitionDuration = 0.08f;

    [Header("Soft-Lock Settings")]
    [Tooltip("Bán kính tối đa để tự động phát hiện và hút vào kẻ địch (mét)")]
    [SerializeField] private float attackRange = 5.0f;

    [Tooltip("Tốc độ xoay mượt mặt về phía kẻ địch khi đấm (độ/giây)")]
    [SerializeField] private float softLockRotationSpeed = 1200.0f;

    [Tooltip("Khoảng cách dừng riêng cho từng đòn (1: đấm ~1.0m, 2: đấm ~1.05m, 3: chỏ ~0.95m, 4: đấm lưng ~1.1m, 5: đá rìu dài ~1.45m)")]
    [SerializeField] private float[] stopDistancesPerHit = new float[] { 1.0f, 1.05f, 0.95f, 1.1f, 1.45f };

    [Tooltip("Thời điểm trúng đòn theo % animation (đòn 1-4 đấm nhanh ~25-30%, đòn 5 xoay người đá rìu ~55% khi gót chân bổ xuống)")]
    [SerializeField] private float[] hitTimingRatios = new float[] { 0.28f, 0.28f, 0.25f, 0.30f, 0.55f };

    [Tooltip("Thời gian khựng hình khi va chạm (Hitstop) tạo cảm giác lực nặng (giây)")]
    [SerializeField] private float hitstopDuration = 0.07f;

    [Tooltip("Tốc độ lướt áp sát vào kẻ địch")]
    [SerializeField] private float dashSpeed = 9.5f;

    [Header("Gravity & Grounding")]
    [SerializeField] private float gravity = -19.62f;

    [Header("References")]
    [Tooltip("Transform của Camera chính")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Animator của nhân vật")]
    [SerializeField] private Animator animator;

    private CharacterController characterController;
    private Vector2 moveInput;
    private bool isSprinting;
    private float currentSpeed;
    private float verticalVelocity;
    private float rotationVelocity;

    // Quản lý Combo & Input Buffering (Hệ thống hàng đợi đòn đánh)
    private int comboStep = 0; // 0: Idle, 1 -> 5: Đang thực hiện đòn tương ứng
    private float comboResetTimer = 0f; // Bộ đếm hủy combo nếu sau khi đánh xong mà không bấm tiếp
    private bool isAttacking = false;
    private bool hasBufferedAttack = false; // Nhận và nhớ lệnh bấm trước
    private float currentAttackDuration = 0f;
    private float attackTimer = 0f;
    private float hitstopTimer = 0f; // Đóng băng một khoảnh khắc khi đánh trúng
    private bool hasDealtDamage = false; // Đã gây sát thương/knockback cho đòn hiện tại chưa
    private Transform targetEnemy;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        ApplyGravity();

        if (hitstopTimer > 0f)
        {
            hitstopTimer -= Time.deltaTime;
            if (hitstopTimer <= 0f && animator != null)
            {
                animator.speed = attackSpeedMultiplier;
            }
            return;
        }

        if (isAttacking)
        {
            attackTimer += Time.deltaTime;

            if (!hasDealtDamage)
            {
                float hitTimingRatio = GetCurrentHitTimingRatio();
                if (attackTimer >= currentAttackDuration * hitTimingRatio)
                {
                    CheckAndApplyHit();
                }
            }

            if (attackTimer >= currentAttackDuration)
            {
                if (hasBufferedAttack)
                {
                    hasBufferedAttack = false;
                    ExecuteNextComboStep();
                }
                else
                {
                    if (animator != null) animator.speed = 1.0f;

                    isAttacking = false;
                    comboResetTimer = comboWindowTime;
                }
            }

            HandleCombatMovement();
        }
        else
        {
            if (comboStep > 0)
            {
                comboResetTimer -= Time.deltaTime;
                if (comboResetTimer <= 0f)
                {
                    comboStep = 0;
                    targetEnemy = null;
                }
            }

            HandleFreeMovement();
        }

        UpdateAnimator();
    }

    private void HandleFreeMovement()
    {
        Vector3 direction = new Vector3(moveInput.x, 0.0f, moveInput.y).normalized;
        Vector3 moveVector = Vector3.zero;

        if (direction.magnitude >= 0.1f)
        {
            float cameraAngleY = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cameraAngleY;
            float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime);

            transform.rotation = Quaternion.Euler(0.0f, smoothAngle, 0.0f);

            Vector3 moveDirection = Quaternion.Euler(0.0f, targetAngle, 0.0f) * Vector3.forward;
            float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 10f);

            moveVector = moveDirection.normalized * (currentSpeed * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.Lerp(currentSpeed, 0f, Time.deltaTime * 10f);
        }

        moveVector.y = verticalVelocity * Time.deltaTime;
        characterController.Move(moveVector);
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0.0f)
            {
                verticalVelocity = -0.5f;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    private void HandleCombatMovement()
    {
        Vector3 moveVector = Vector3.zero;

        if (targetEnemy != null)
        {
            Vector3 directionToEnemy = (targetEnemy.position - transform.position);
            directionToEnemy.y = 0f;

            if (directionToEnemy.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToEnemy);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, softLockRotationSpeed * Time.deltaTime);
            }

            float distance = directionToEnemy.magnitude;
            float currentStopDist = GetCurrentStopDistance();
            if (distance > currentStopDist)
            {
                moveVector = directionToEnemy.normalized * (dashSpeed * Time.deltaTime);
            }
        }
        else
        {
            if (attackTimer < currentAttackDuration * 0.45f)
            {
                moveVector = transform.forward * (3.0f * Time.deltaTime);
            }
        }

        moveVector.y = verticalVelocity * Time.deltaTime;
        characterController.Move(moveVector);
    }

    private Transform FindBestTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies == null || enemies.Length == 0) return null;

        Transform bestTarget = null;
        float highestScore = float.MinValue;

        Vector3 inputDir = new Vector3(moveInput.x, 0.0f, moveInput.y).normalized;
        Vector3 preferredDirection = transform.forward;

        bool hasInput = inputDir.magnitude >= 0.1f;
        if (hasInput && cameraTransform != null)
        {
            float cameraAngleY = cameraTransform.eulerAngles.y;
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraAngleY;
            preferredDirection = Quaternion.Euler(0.0f, targetAngle, 0.0f) * Vector3.forward;
        }

        foreach (GameObject enemy in enemies)
        {
            Vector3 directionToEnemy = enemy.transform.position - transform.position;
            directionToEnemy.y = 0f;

            float distance = directionToEnemy.magnitude;
            if (distance <= attackRange)
            {
                float angle = Vector3.Angle(preferredDirection, directionToEnemy);
                float anglePenalty = angle / 180f;
                float score = (attackRange - distance) * 2.0f + (1.0f - anglePenalty) * 5.0f;

                if (hasInput && angle < 60f)
                {
                    score += 10.0f;
                }

                if (score > highestScore)
                {
                    highestScore = score;
                    bestTarget = enemy.transform;
                }
            }
        }

        return bestTarget;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float targetAnimSpeed = (!isAttacking && moveInput.magnitude >= 0.1f) ? (isSprinting ? 1.5f : 1.0f) : 0.0f;
        animator.SetFloat(SpeedHash, targetAnimSpeed, 0.1f, Time.deltaTime);
    }







    #region Input Handlers

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnSprint(InputValue value)
    {
        isSprinting = value.isPressed;
    }

    
    public void OnAttack(InputValue value)
    {
        if (!value.isPressed) return;

        if (isAttacking)
        {
            if (attackTimer > currentAttackDuration * 0.25f)
            {
                hasBufferedAttack = true;
            }
            return;
        }

        ExecuteNextComboStep();
    }

    
    private void ExecuteNextComboStep()
    {
        comboStep++;
        if (comboStep > comboStateNames.Length)
        {
            comboStep = 1;
        }

        string targetState = comboStateNames[comboStep - 1];

        if (animator != null)
        {
            animator.speed = attackSpeedMultiplier;
            animator.CrossFadeInFixedTime(targetState, transitionDuration);
        }

        targetEnemy = FindBestTarget();
        isAttacking = true;
        hasBufferedAttack = false;
        hasDealtDamage = false; 
        attackTimer = 0f;

        currentAttackDuration = GetStateDuration(targetState) / attackSpeedMultiplier;
    }

    private void CheckAndApplyHit()
    {
        hasDealtDamage = true;

        if (targetEnemy != null)
        {
            float distToTarget = Vector3.Distance(transform.position, targetEnemy.position);
            float maxReach = GetCurrentStopDistance() + 0.65f;

            if (distToTarget <= maxReach)
            {
                IDamageable damageable = targetEnemy.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    float knockbackForce = (comboStep == comboStateNames.Length) ? 14.0f : 7.0f;
                    Vector3 hitDir = (targetEnemy.position - transform.position).normalized;
                    damageable.TakeHit(hitDir, knockbackForce);

                    if (hitstopDuration > 0f)
                    {
                        hitstopTimer = hitstopDuration;
                        if (animator != null) animator.speed = 0.05f;
                    }
                }
            }
        }
    }

    private float GetCurrentHitTimingRatio()
    {
        if (hitTimingRatios != null && hitTimingRatios.Length >= comboStep && comboStep > 0)
        {
            return hitTimingRatios[comboStep - 1];
        }
        return 0.35f;
    }

    private float GetCurrentStopDistance()
    {
        if (stopDistancesPerHit != null && stopDistancesPerHit.Length >= comboStep && comboStep > 0)
        {
            return stopDistancesPerHit[comboStep - 1];
        }
        return 1.1f;
    }

    private float GetStateDuration(string stateName)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.Equals(stateName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return Mathf.Max(0.35f, clip.length * 0.80f);
                }
            }
        }

        return 0.50f;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
