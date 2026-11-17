using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Tốc độ đi bộ")]
    [SerializeField] private float walkSpeed = 3.8f;

    [Tooltip("Tốc độ chạy nhanh (Sprint)")]
    [SerializeField] private float sprintSpeed = 7.0f;

    [Tooltip("Tốc độ di chuyển khi đang thủ thế (Guard)")]
    [SerializeField] private float guardWalkSpeed = 2.2f;

    [Tooltip("Gia tốc khởi động (cao = bứt tốc nhanh, nhạy chuẩn Sifu)")]
    [SerializeField] private float acceleration = 28.0f;

    [Tooltip("Lực hãm khi nhả phím di chuyển (cao = dừng gọn, không trượt băng)")]
    [SerializeField] private float deceleration = 34.0f;

    [Tooltip("Thời gian làm mượt góc xoay tự do của nhân vật")]
    [SerializeField] private float rotationSmoothTime = 0.06f;

    [Tooltip("Độ nghiêng thân trên khi chạy nước rút (độ)")]
    [SerializeField] private float sprintForwardLeanAngle = 6.0f;

    [Tooltip("Độ nghiêng thân khi cua gắt (độ)")]
    [SerializeField] private float turnBankAngle = 5.5f;

    [Header("Dodge / Step-Dodge Settings")]
    [Tooltip("Tốc độ lướt né đòn (Space / C)")]
    [SerializeField] private float dodgeSpeed = 13.2f;

    [Tooltip("Thời lượng lướt né đòn (giây)")]
    [SerializeField] private float dodgeDuration = 0.25f;

    [Tooltip("Thời gian bất tử khi né đòn (I-frames)")]
    [SerializeField] private float dodgeInvulnDuration = 0.19f;

    [Tooltip("Thời gian hồi giữa hai lần né liên tiếp (giây)")]
    [SerializeField] private float dodgeCooldown = 0.14f;

    [Header("Sifu Avoid (Hold Q + Flick WASD) & Structure Settings")]
    [Tooltip("Thời lượng động tác lách/hụp/nhảy tại chỗ (Sifu Avoid)")]
    [SerializeField] private float avoidDuration = 0.34f;

    [Tooltip("Khung thời gian Avoid có hiệu lực tránh đòn")]
    [SerializeField] private float avoidActiveWindow = 0.30f;

    [Tooltip("Thời gian hồi giữa hai lần Avoid liên tiếp")]
    [SerializeField] private float avoidCooldown = 0.06f;

    [Tooltip("Thời gian mở cửa sổ phản công sau khi Avoid thành công")]
    [SerializeField] private float avoidCounterWindow = 0.85f;

    [Tooltip("Lượng Structure hồi lại mỗi lần Avoid chuẩn")]
    [SerializeField] private float avoidStructureRestore = 24f;

    [Tooltip("Lượng Structure tích tụ khi đỡ thường (Block)")]
    [SerializeField] private float blockStructureDamage = 24f;

    [Tooltip("Tốc độ hồi Structure tự nhiên")]
    [SerializeField] private float structureRecoveryRate = 18f;

    [Tooltip("Độ trễ trước khi Structure bắt đầu hồi")]
    [SerializeField] private float structureRecoveryDelay = 1.8f;

    [Tooltip("Thời gian choáng nếu vỡ Structure (Guard Break)")]
    [SerializeField] private float guardBreakDuration = 0.65f;

    [Header("Manual Target Lock-On Settings")]
    [SerializeField] private float lockOnMaxRange = 13.5f;
    [SerializeField] private float lockOnBreakRange = 16.0f;
    [SerializeField] private float lockOnStrafeSpeed = 3.0f;
    [SerializeField] private float lockOnRotationSpeed = 720f;

    [Header("Guard & Deflect (Parry) Settings")]
    [Tooltip("Cửa sổ thời gian bấm Q để Phản Đòn hoàn hảo (Parry/Deflect)")]
    [SerializeField] private float parryWindowDuration = 0.24f;

    [Header("Combo Settings (Light & Heavy)")]
    [Tooltip("Chuỗi 5 đòn đánh nhẹ (LMB)")]
    [SerializeField] private string[] comboStateNames = new string[]
    {
        "Atk_P_1",
        "Atk_P_2",
        "elbow chop",
        "back fist",
        "spinning axe kick"
    };

    [Tooltip("Chuỗi 3 đòn đánh nặng / quét trụ (RMB)")]
    [SerializeField] private string[] heavyComboStateNames = new string[]
    {
        "knee strike",
        "spinning elbow",
        "front sweep"
    };

    [Tooltip("Hệ số tăng tốc độ ra đòn (1.0 = bình thường, 1.25 -> 1.4 = nhanh, dứt khoát chuẩn Sifu)")]
    [Range(0.8f, 2.0f)]
    [SerializeField] private float attackSpeedMultiplier = 1.35f;

    [Tooltip("Cửa sổ thời gian cho phép bấm nối tiếp combo (giây)")]
    [SerializeField] private float comboWindowTime = 1.8f;

    [Tooltip("Thời gian chuyển mượt giữa các đòn đánh")]
    [SerializeField] private float transitionDuration = 0.08f;

    [Tooltip("Tỷ lệ % thời lượng đòn đánh cho phép ngắt recovery để di chuyển ngay (WASD)")]
    [Range(0.5f, 1.0f)]
    [SerializeField] private float recoveryCancelRatio = 0.72f;

    [Header("Soft-Lock Settings")]
    [Tooltip("Bán kính tối đa để tự động phát hiện và hút vào kẻ địch (mét)")]
    [SerializeField] private float attackRange = 5.0f;

    [Tooltip("Tốc độ xoay mượt mặt về phía kẻ địch khi đấm (độ/giây)")]
    [SerializeField] private float softLockRotationSpeed = 1200.0f;

    [Tooltip("Khoảng cách dừng cho đòn đánh nhẹ")]
    [SerializeField] private float[] stopDistancesPerHit = new float[] { 1.05f, 1.05f, 1.0f, 1.15f, 1.4f };

    [Tooltip("Khoảng cách dừng cho đòn đánh nặng")]
    [SerializeField] private float[] heavyStopDistances = new float[] { 0.95f, 1.05f, 1.35f };

    [Tooltip("Thời điểm trúng đòn nhẹ theo % animation")]
    [SerializeField] private float[] hitTimingRatios = new float[] { 0.28f, 0.28f, 0.25f, 0.30f, 0.55f };

    [Tooltip("Thời điểm trúng đòn nặng theo % animation")]
    [SerializeField] private float[] heavyHitTimingRatios = new float[] { 0.32f, 0.38f, 0.42f };

    [Tooltip("Thời gian khựng hình khi va chạm (Hitstop)")]
    [SerializeField] private float hitstopDuration = 0.065f;

    [Tooltip("Tốc độ lướt áp sát vào kẻ địch")]
    [SerializeField] private float dashSpeed = 10.5f;

    [Header("Gravity & Grounding")]
    [SerializeField] private float gravity = -19.62f;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    private CharacterController characterController;
    private PlayerInput playerInput;
    private InputAction sprintAction;
    private ThirdPersonCamera thirdPersonCamera;
    private Transform spineBone;

    private Vector2 moveInput;
    private bool isSprinting;
    private bool isGuarding;
    private bool wasGuardingLastFrame;
    private float parryWindowTimer;
    private bool useTestInputOverride;

    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float rotationVelocity;
    private float currentForwardLean;
    private float currentBankLean;
    private float lastYaw;

    private const float MaxPlayerStructure = 100f;

    // Dodge, Avoid & Structure state
    private bool isDodging;
    private float dodgeTimer;
    private float dodgeCooldownTimer;
    private Vector3 dodgeDirection;
    private bool isAvoiding;
    private float avoidTimer;
    private float avoidCooldownTimer;
    private AvoidDirection currentAvoidDirection = AvoidDirection.None;
    private AvoidDirection lastAvoidDirectionUsed = AvoidDirection.None;
    private float avoidCounterTimer;
    private float invulnerableTimer;
    private float hitReactionTimer;
    private Vector3 externalKnockback;
    private float playerStructure;
    private float timeSinceStructureDamaged = 99f;
    private float structureRecoveryFreezeTimer;
    private float guardBreakStunTimer;

    // Lock-On state
    private Transform lockedTarget;
    private float scrollSwitchCooldown;

    // Combat state
    private int comboStep = 0;
    private bool currentAttackIsHeavy = false;
    private float comboResetTimer = 0f;
    private bool isAttacking = false;
    private bool hasBufferedAttack = false;
    private bool bufferedAttackIsHeavy = false;
    private float currentAttackDuration = 0f;
    private float attackTimer = 0f;
    private float hitstopTimer = 0f;
    private bool hasDealtDamage = false;
    private Transform targetEnemy;

    // Telemetry / HUD feedback
    private string lastDefenseEvent = "";
    private float defenseEventTimer = 0f;
    private readonly CombatFlashController hitFlashController = new CombatFlashController();
    private static readonly Color PlayerHitFlashColor = Color.red;

    public float HitFlashNormalized => hitFlashController.NormalizedRemaining;

    public int ComboStep => comboStep;
    public int MaxComboSteps => currentAttackIsHeavy
        ? (heavyComboStateNames != null ? heavyComboStateNames.Length : 0)
        : (comboStateNames != null ? comboStateNames.Length : 0);
    public bool IsHeavyAttack => currentAttackIsHeavy;
    public bool IsAttacking => isAttacking;
    public bool HasDealtDamage => hasDealtDamage;
    public float AttackProgress => isAttacking && currentAttackDuration > 0.001f ? Mathf.Clamp01(attackTimer / currentAttackDuration) : 0f;
    public bool IsDodging => isDodging;
    public bool IsAvoiding => isAvoiding;
    public AvoidDirection CurrentAvoidDirection => currentAvoidDirection;
    public AvoidDirection LastAvoidDirectionUsed => lastAvoidDirectionUsed;
    public bool HasAvoidCounterWindow => avoidCounterTimer > 0f;
    public float Structure => playerStructure;
    public float MaxStructure => MaxPlayerStructure;
    public float StructureNormalized => Mathf.Clamp01(playerStructure / MaxPlayerStructure);
    public bool IsStructureBroken => guardBreakStunTimer > 0f;
    public Transform LockedTarget => lockedTarget;
    public bool IsLockedOn => lockedTarget != null && lockedTarget.gameObject.activeInHierarchy;
    public bool IsActivelyLockOnStrafing => IsLockedOn && !IsSprinting && !isAttacking && !isDodging && !isAvoiding && !IsGuarding && hitReactionTimer <= 0f;
    public bool IsSprinting => isSprinting && !isGuarding && !isAttacking && !isDodging && !isAvoiding && moveInput.sqrMagnitude >= 0.01f;
    public bool IsGuarding => isGuarding && !isAttacking && !isDodging && !isAvoiding && guardBreakStunTimer <= 0f && hitReactionTimer <= 0f;
    public bool IsParryWindowActive => IsGuarding && parryWindowTimer > 0f;
    public bool IsInvulnerable => invulnerableTimer > 0f;
    public bool HasBufferedAttack => hasBufferedAttack;
    public Transform TargetEnemy => targetEnemy;
    public float CurrentPlanarSpeed => new Vector3(planarVelocity.x, 0f, planarVelocity.z).magnitude;
    public float CurrentForwardLean => currentForwardLean;
    public string LastDefenseEvent => defenseEventTimer > 0f ? lastDefenseEvent : string.Empty;
    public int ParriesLanded { get; private set; }
    public int EvadesLanded { get; private set; }
    public int AvoidsLanded { get; private set; }
    public int BlocksLanded { get; private set; }
    public string CurrentMoveName
    {
        get
        {
            if (comboStep <= 0) return "Idle";
            string[] list = currentAttackIsHeavy ? heavyComboStateNames : comboStateNames;
            if (list != null && comboStep <= list.Length) return list[comboStep - 1];
            return "Idle";
        }
    }

    private bool usingLockOnBlendState;
    private bool testGuardHeld;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int LocomotionStateHash = Animator.StringToHash("Locomotion");
    private static readonly int LockOnLocomotionHash = Animator.StringToHash("LockOnLocomotion");
    private static readonly int QuickstepForwardHash = Animator.StringToHash("Quickstep_F");
    private static readonly int QuickstepBackwardHash = Animator.StringToHash("Quickstep_B");
    private static readonly int QuickstepLeftHash = Animator.StringToHash("Quickstep_L");
    private static readonly int QuickstepRightHash = Animator.StringToHash("Quickstep_R");
    private static readonly int AvoidLeftHash = Animator.StringToHash("Avoid_Left");
    private static readonly int AvoidRightHash = Animator.StringToHash("Avoid_Right");
    private static readonly int AvoidDuckHash = Animator.StringToHash("Avoid_Duck");
    private static readonly int AvoidHopHash = Animator.StringToHash("Avoid_Hop");
    private static readonly int GuardIdleHash = Animator.StringToHash("Guard_Idle");
    private static readonly int GuardHitHash = Animator.StringToHash("Guard_Hit");
    private static readonly int HitFrontHash = Animator.StringToHash("Hit_F");

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.minMoveDistance = 0f;
        }
        playerInput = GetComponent<PlayerInput>();

        if (playerInput != null && playerInput.actions != null)
        {
            sprintAction = playerInput.actions["Sprint"];
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform != null)
        {
            thirdPersonCamera = cameraTransform.GetComponent<ThirdPersonCamera>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            if (animator.isHuman)
            {
                spineBone = animator.GetBoneTransform(HumanBodyBones.Spine);
            }
        }

        hitFlashController.Initialize(GetComponentsInChildren<Renderer>());

        lastYaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        PollActionInputs();
        UpdateTimers();
        ValidateLockOnTarget();
        ApplyGravity();

        if (hitstopTimer > 0f)
        {
            hitstopTimer -= Time.deltaTime;
            if (hitstopTimer <= 0f && animator != null)
            {
                animator.speed = isAttacking ? attackSpeedMultiplier : 1.0f;
            }
            return;
        }

        if (hitReactionTimer > 0f)
        {
            HandleHitReaction();
        }
        else if (isDodging)
        {
            HandleDodgeMovement();
        }
        else if (isAvoiding)
        {
            HandleAvoidMovement();
        }
        else if (isAttacking)
        {
            UpdateAttackState();
        }
        else
        {
            UpdateComboResetTimer();
            HandleFreeMovement();
        }

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetSprinting(IsSprinting);
            thirdPersonCamera.SetLockOnTarget(IsLockedOn ? lockedTarget : null);
        }

        UpdateAnimator();
    }

    private void LateUpdate()
    {
        ApplyProceduralSprintLean();
    }

    private void PollActionInputs()
    {
        if (useTestInputOverride) return;

        if (sprintAction != null)
        {
            isSprinting = sprintAction.IsPressed();
        }

        // Manual Target Lock-On toggle (MMB or Tab)
        bool lockPressed = (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame) ||
                           (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame);
        if (lockPressed)
        {
            ToggleLockOn();
        }

        // Switch locked target (Scroll wheel or 1/2 / [/])
        if (IsLockedOn)
        {
            if (Mouse.current != null && scrollSwitchCooldown <= 0f)
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) >= 5f)
                {
                    CycleLockOnTarget(scrollY > 0f ? -1 : 1);
                    scrollSwitchCooldown = 0.14f;
                }
            }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.leftBracketKey.wasPressedThisFrame)
                {
                    CycleLockOnTarget(-1);
                }
                else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.rightBracketKey.wasPressedThisFrame)
                {
                    CycleLockOnTarget(1);
                }
            }
        }

        bool guardHeld = Keyboard.current != null &&
                         (Keyboard.current.qKey.isPressed || Keyboard.current.leftCtrlKey.isPressed);

        if (guardHeld && !wasGuardingLastFrame)
        {
            BeginGuard();
        }
        else if (!guardHeld && wasGuardingLastFrame)
        {
            EndGuard();
        }
        wasGuardingLastFrame = guardHeld;

        if (IsGuarding && !isAvoiding && avoidCooldownTimer <= 0f)
        {
            AvoidDirection avoidDir = PollAvoidFlickDirection();
            if (avoidDir != AvoidDirection.None)
            {
                TryStartAvoid(avoidDir);
            }
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            HandleAttackRequest(true);
        }
    }

    private AvoidDirection PollAvoidFlickDirection()
    {
        if (Keyboard.current == null) return AvoidDirection.None;
        if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame) return AvoidDirection.Left;
        if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame) return AvoidDirection.Right;
        if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame) return AvoidDirection.Down;
        if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame) return AvoidDirection.Hop;
        return AvoidDirection.None;
    }

    private void UpdateTimers()
    {
        float dt = Time.deltaTime;
        if (dodgeCooldownTimer > 0f) dodgeCooldownTimer -= dt;
        if (avoidCooldownTimer > 0f) avoidCooldownTimer -= dt;
        if (avoidCounterTimer > 0f) avoidCounterTimer -= dt;
        if (invulnerableTimer > 0f) invulnerableTimer -= dt;
        if (parryWindowTimer > 0f) parryWindowTimer -= dt;
        if (defenseEventTimer > 0f) defenseEventTimer -= dt;
        if (guardBreakStunTimer > 0f) guardBreakStunTimer -= dt;
        if (scrollSwitchCooldown > 0f) scrollSwitchCooldown -= dt;
        if (structureRecoveryFreezeTimer > 0f) structureRecoveryFreezeTimer -= dt;
        hitFlashController.Update(dt);

        timeSinceStructureDamaged += dt;
        if (timeSinceStructureDamaged >= structureRecoveryDelay &&
            structureRecoveryFreezeTimer <= 0f &&
            guardBreakStunTimer <= 0f &&
            playerStructure > 0f)
        {
            float recovery = IsGuarding ? structureRecoveryRate * 0.35f : structureRecoveryRate;
            playerStructure = Mathf.MoveTowards(playerStructure, 0f, recovery * dt);
        }
    }

    private void UpdateComboResetTimer()
    {
        if (comboStep > 0)
        {
            comboResetTimer -= Time.deltaTime;
            if (comboResetTimer <= 0f)
            {
                comboStep = 0;
                currentAttackIsHeavy = false;
                if (!IsLockedOn) targetEnemy = null;
            }
        }
    }

    private void BeginGuard()
    {
        if (isDodging || isAvoiding || guardBreakStunTimer > 0f || hitReactionTimer > 0f) return;

        // Allow guard-canceling recovery frames just like Sifu.
        if (isAttacking && attackTimer < currentAttackDuration * 0.45f) return;

        if (isAttacking)
        {
            isAttacking = false;
            hasBufferedAttack = false;
        }

        isGuarding = true;
        usingLockOnBlendState = false;
        parryWindowTimer = parryWindowDuration;
        targetEnemy = IsLockedOn ? lockedTarget : FindBestTarget();

        if (animator != null && animator.HasState(0, GuardIdleHash))
        {
            animator.speed = 1.0f;
            animator.CrossFadeInFixedTime(GuardIdleHash, 0.08f);
        }
    }

    private void EndGuard()
    {
        if (!isGuarding) return;
        isGuarding = false;
        parryWindowTimer = 0f;

        if (!isAttacking && !isDodging && !isAvoiding && animator != null)
        {
            animator.speed = 1.0f;
            RestoreLocomotionOrGuardState();
        }
    }

    private void HandleHitReaction()
    {
        hitReactionTimer -= Time.deltaTime;
        Vector3 step = externalKnockback * Time.deltaTime;
        externalKnockback = Vector3.MoveTowards(externalKnockback, Vector3.zero, 28f * Time.deltaTime);

        Vector3 moveVector = new Vector3(step.x, verticalVelocity * Time.deltaTime, step.z);
        characterController.Move(moveVector);

        if (hitReactionTimer <= 0f && animator != null)
        {
            animator.speed = 1.0f;
            RestoreLocomotionOrGuardState();
        }
    }

    private void UpdateAttackState()
    {
        attackTimer += Time.deltaTime;

        HandleCombatMovement();

        if (!hasDealtDamage)
        {
            float hitTimingRatio = GetCurrentHitTimingRatio();
            if (attackTimer >= currentAttackDuration * hitTimingRatio)
            {
                CheckAndApplyHit();
            }
        }

        // Sifu-style recovery cancel: move out cleanly once the hit has connected.
        if (hasDealtDamage && !hasBufferedAttack && moveInput.sqrMagnitude >= 0.04f && attackTimer >= currentAttackDuration * recoveryCancelRatio)
        {
            CancelAttackToLocomotion();
            HandleFreeMovement();
            return;
        }

        if (attackTimer >= currentAttackDuration)
        {
            if (hasBufferedAttack)
            {
                bool nextIsHeavy = bufferedAttackIsHeavy;
                hasBufferedAttack = false;
                ExecuteNextComboStep(nextIsHeavy);
            }
            else
            {
                if (animator != null) animator.speed = 1.0f;
                isAttacking = false;
                usingLockOnBlendState = false;
                comboResetTimer = comboWindowTime;
                RestoreLocomotionOrGuardState();
            }
        }
    }

    private void CancelAttackToLocomotion()
    {
        isAttacking = false;
        hasBufferedAttack = false;
        usingLockOnBlendState = false;
        comboResetTimer = comboWindowTime;

        if (animator != null)
        {
            animator.speed = 1.0f;
            RestoreLocomotionOrGuardState();
        }
    }

    private Vector3 GetCameraRelativeInputDirection()
    {
        Vector3 input = new Vector3(moveInput.x, 0.0f, moveInput.y);
        if (input.sqrMagnitude < 0.01f) return Vector3.zero;

        input.Normalize();
        float cameraAngleY = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
        float targetAngle = Mathf.Atan2(input.x, input.z) * Mathf.Rad2Deg + cameraAngleY;
        return Quaternion.Euler(0.0f, targetAngle, 0.0f) * Vector3.forward;
    }

    private void HandleFreeMovement()
    {
        Vector3 desiredDir = GetCameraRelativeInputDirection();
        bool hasInput = desiredDir.sqrMagnitude > 0.01f;

        if (IsGuarding)
        {
            if (IsLockedOn) targetEnemy = lockedTarget;
            else if (targetEnemy == null) targetEnemy = FindBestTarget();

            if (targetEnemy != null)
            {
                FaceTransformPlanar(targetEnemy, softLockRotationSpeed * 0.75f);
            }

            Vector3 targetVelocity = hasInput ? desiredDir * guardWalkSpeed : Vector3.zero;
            float rate = hasInput ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, rate * Time.deltaTime);
        }
        else if (IsLockedOn && !IsSprinting)
        {
            FaceTransformPlanar(lockedTarget, lockOnRotationSpeed);
            Vector3 targetVelocity = hasInput ? desiredDir * lockOnStrafeSpeed : Vector3.zero;
            float rate = hasInput ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, rate * Time.deltaTime);
        }
        else if (hasInput)
        {
            float targetAngle = Mathf.Atan2(desiredDir.x, desiredDir.z) * Mathf.Rad2Deg;
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, targetAngle));

            float effectiveTurnTime = angleDiff > 100f ? rotationSmoothTime * 0.45f : rotationSmoothTime;
            float smoothAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, effectiveTurnTime);
            transform.rotation = Quaternion.Euler(0.0f, smoothAngle, 0.0f);

            float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
            float alignment = Mathf.Lerp(0.45f, 1.0f, Mathf.Clamp01(1.0f - (angleDiff / 180f)));
            Vector3 targetVelocity = desiredDir * (targetSpeed * alignment);
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, acceleration * Time.deltaTime);
        }
        else
        {
            planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        Vector3 moveVector = new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z) * Time.deltaTime;
        characterController.Move(moveVector);
    }

    private void FaceTransformPlanar(Transform lookTarget, float degPerSec)
    {
        if (lookTarget == null) return;
        Vector3 toTarget = lookTarget.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, degPerSec * Time.deltaTime);
        }
    }

    private void HandleDodgeMovement()
    {
        dodgeTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(dodgeTimer / Mathf.Max(0.05f, dodgeDuration));

        // Two-phase Step-Dodge: fast burst then sharp foot-plant brake down to zero tail slide.
        float speedFactor = progress < 0.58f
            ? 1.25f * Mathf.Sin((progress / 0.58f) * Mathf.PI * 0.52f + 0.30f)
            : Mathf.Pow(1f - ((progress - 0.58f) / 0.42f), 2.2f) * 0.65f;

        Vector3 stepVelocity = dodgeDirection * (dodgeSpeed * speedFactor);
        planarVelocity = progress < 0.85f ? stepVelocity * 0.35f : Vector3.zero;

        if (IsLockedOn)
        {
            FaceTransformPlanar(lockedTarget, softLockRotationSpeed * 0.65f);
        }

        Vector3 moveVector = new Vector3(stepVelocity.x, verticalVelocity, stepVelocity.z) * Time.deltaTime;
        characterController.Move(moveVector);

        if (dodgeTimer >= dodgeDuration)
        {
            isDodging = false;
            planarVelocity = Vector3.zero;
            dodgeCooldownTimer = dodgeCooldown;
            if (animator != null) animator.speed = 1.0f;
            RestoreLocomotionOrGuardState();
        }
    }

    private void HandleAvoidMovement()
    {
        avoidTimer += Time.deltaTime;
        // Sifu Avoid stays firmly anchored in place inside melee pocket!
        planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, 65f * Time.deltaTime);

        Transform focusTarget = IsLockedOn ? lockedTarget : (targetEnemy != null ? targetEnemy : FindBestTarget());
        if (focusTarget != null)
        {
            FaceTransformPlanar(focusTarget, softLockRotationSpeed * 0.85f);
        }

        Vector3 moveVector = new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z) * Time.deltaTime;
        characterController.Move(moveVector);

        if (avoidTimer >= avoidDuration)
        {
            isAvoiding = false;
            currentAvoidDirection = AvoidDirection.None;
            avoidCooldownTimer = avoidCooldown;
            if (animator != null) animator.speed = 1.0f;

            bool stillGuarding = (useTestInputOverride && testGuardHeld) ||
                                 (!useTestInputOverride && Keyboard.current != null && (Keyboard.current.qKey.isPressed || Keyboard.current.leftCtrlKey.isPressed));
            if (stillGuarding && guardBreakStunTimer <= 0f)
            {
                isGuarding = true;
                wasGuardingLastFrame = true;
                if (animator != null && animator.HasState(0, GuardIdleHash))
                {
                    animator.CrossFadeInFixedTime(GuardIdleHash, 0.08f);
                }
            }
            else
            {
                RestoreLocomotionOrGuardState();
            }
        }
    }

    private void RestoreLocomotionOrGuardState()
    {
        if (animator == null) return;
        bool useLockState = IsActivelyLockOnStrafing && animator.HasState(0, LockOnLocomotionHash);
        int targetHash = useLockState ? LockOnLocomotionHash : LocomotionStateHash;
        usingLockOnBlendState = useLockState;
        if (animator.HasState(0, targetHash))
        {
            animator.CrossFadeInFixedTime(targetHash, 0.10f);
        }
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0.0f) verticalVelocity = -0.5f;
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
            Vector3 directionToEnemy = targetEnemy.position - transform.position;
            directionToEnemy.y = 0f;

            if (directionToEnemy.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToEnemy);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, softLockRotationSpeed * Time.deltaTime);
            }

            float distance = directionToEnemy.magnitude;
            float currentStopDist = GetCurrentStopDistance();
            float remaining = distance - currentStopDist;

            if (remaining > 0.02f)
            {
                float step = Mathf.Min(remaining, dashSpeed * Time.deltaTime);
                moveVector = directionToEnemy.normalized * step;
            }
        }
        else if (attackTimer < currentAttackDuration * 0.35f)
        {
            moveVector = transform.forward * (2.6f * Time.deltaTime);
        }

        moveVector.y = verticalVelocity * Time.deltaTime;
        characterController.Move(moveVector);
    }

    private Transform FindBestTarget()
    {
        if (IsLockedOn) return lockedTarget;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies == null || enemies.Length == 0) return null;

        Transform bestTarget = null;
        float highestScore = float.MinValue;

        Vector3 preferredDirection = GetCameraRelativeInputDirection();
        bool hasInput = preferredDirection.sqrMagnitude >= 0.01f;
        if (!hasInput)
        {
            preferredDirection = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                : transform.forward;
        }

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null || !enemy.activeInHierarchy) continue;

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

    private void ValidateLockOnTarget()
    {
        if (lockedTarget == null) return;
        if (!lockedTarget.gameObject.activeInHierarchy)
        {
            lockedTarget = null;
            return;
        }

        Vector3 flat = lockedTarget.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude > lockOnBreakRange)
        {
            lockedTarget = null;
        }
    }

    private System.Collections.Generic.List<Transform> CollectLockOnCandidates()
    {
        System.Collections.Generic.List<Transform> list = new System.Collections.Generic.List<Transform>();
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies == null) return list;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject go = enemies[i];
            if (go == null || !go.activeInHierarchy) continue;
            Vector3 flat = go.transform.position - transform.position;
            flat.y = 0f;
            if (flat.magnitude <= lockOnMaxRange)
            {
                list.Add(go.transform);
            }
        }

        // Deterministic screen/azimuth sort around camera forward -> right so 1/2 switching feels natural left-to-right.
        Vector3 refForward = cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
            : transform.forward;
        if (refForward.sqrMagnitude < 0.01f) refForward = transform.forward;

        list.Sort((a, b) =>
        {
            float angA = Vector3.SignedAngle(refForward, Vector3.ProjectOnPlane(a.position - transform.position, Vector3.up), Vector3.up);
            float angB = Vector3.SignedAngle(refForward, Vector3.ProjectOnPlane(b.position - transform.position, Vector3.up), Vector3.up);
            return angA.CompareTo(angB);
        });
        return list;
    }

    public void ToggleLockOn()
    {
        if (IsLockedOn)
        {
            lockedTarget = null;
            targetEnemy = null;
            if (!isAttacking && !isDodging && !isAvoiding && !IsGuarding && animator != null)
            {
                RestoreLocomotionOrGuardState();
            }
            return;
        }

        System.Collections.Generic.List<Transform> candidates = CollectLockOnCandidates();
        if (candidates.Count == 0) return;

        Vector3 camForward = cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
            : transform.forward;

        Transform best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            Transform c = candidates[i];
            Vector3 flat = c.position - transform.position;
            flat.y = 0f;
            float dist = flat.magnitude;
            float align = Vector3.Dot(camForward, flat.sqrMagnitude > 0.001f ? flat.normalized : camForward);
            float score = align * 6.0f + (lockOnMaxRange - dist);
            if (c.GetComponent<SparringEnemy>() != null) score += 2.5f;
            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        SetLockOnTarget(best);
    }

    public void CycleLockOnTarget(int directionStep)
    {
        System.Collections.Generic.List<Transform> candidates = CollectLockOnCandidates();
        if (candidates.Count == 0)
        {
            SetLockOnTarget(null);
            return;
        }

        if (candidates.Count == 1)
        {
            SetLockOnTarget(candidates[0]);
            return;
        }

        int currentIndex = lockedTarget != null ? candidates.IndexOf(lockedTarget) : -1;
        if (currentIndex < 0)
        {
            SetLockOnTarget(candidates[0]);
            return;
        }

        int step = directionStep >= 0 ? 1 : -1;
        int nextIndex = (currentIndex + step + candidates.Count) % candidates.Count;
        SetLockOnTarget(candidates[nextIndex]);
    }

    public void SetLockOnTarget(Transform target)
    {
        lockedTarget = target;
        if (lockedTarget != null)
        {
            targetEnemy = lockedTarget;
        }
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetLockOnTarget(IsLockedOn ? lockedTarget : null);
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        float planarSpeed = CurrentPlanarSpeed;
        float animTarget = 0f;

        if (!isAttacking && !isDodging && !isAvoiding && !IsGuarding && hitReactionTimer <= 0f)
        {
            if (planarSpeed > 0.1f)
            {
                // Map Idle (0) -> Walk (1.0) -> true Run_F clip (1.8) at natural 1.0x speed.
                animTarget = planarSpeed <= walkSpeed
                    ? Mathf.Clamp01(planarSpeed / Mathf.Max(0.1f, walkSpeed))
                    : 1.0f + Mathf.Clamp01((planarSpeed - walkSpeed) / Mathf.Max(0.1f, sprintSpeed - walkSpeed)) * 0.8f;
            }
        }

        animator.SetFloat(SpeedHash, animTarget, 0.07f, Time.deltaTime);

        Vector3 localVel = transform.InverseTransformDirection(planarVelocity);
        float maxStrafeRef = Mathf.Max(0.1f, lockOnStrafeSpeed);
        animator.SetFloat(MoveXHash, Mathf.Clamp(localVel.x / maxStrafeRef, -1f, 1f), 0.08f, Time.deltaTime);
        animator.SetFloat(MoveZHash, Mathf.Clamp(localVel.z / maxStrafeRef, -1f, 1f), 0.08f, Time.deltaTime);

        if (!isAttacking && !isDodging && !isAvoiding && !IsGuarding && hitReactionTimer <= 0f && hitstopTimer <= 0f)
        {
            // Dedicated Run_F clip plays at natural 1.0x cadence instead of accelerating Walk_F!
            animator.speed = 1.0f;

            bool wantsLockState = IsActivelyLockOnStrafing && animator.HasState(0, LockOnLocomotionHash);
            if (wantsLockState != usingLockOnBlendState)
            {
                usingLockOnBlendState = wantsLockState;
                int desiredHash = wantsLockState ? LockOnLocomotionHash : LocomotionStateHash;
                if (animator.HasState(0, desiredHash))
                {
                    animator.CrossFadeInFixedTime(desiredHash, 0.10f);
                }
            }
        }
    }

    private void ApplyProceduralSprintLean()
    {
        float dt = Time.deltaTime;
        float currentYaw = transform.eulerAngles.y;
        float yawRate = dt > 0.0001f ? Mathf.DeltaAngle(lastYaw, currentYaw) / dt : 0f;
        lastYaw = currentYaw;

        float targetForwardLean = IsSprinting ? sprintForwardLeanAngle : 0f;
        float speedFactor = Mathf.Clamp01(CurrentPlanarSpeed / Mathf.Max(0.1f, sprintSpeed));
        float targetBank = (!isAttacking && !isDodging && !isAvoiding && !IsGuarding)
            ? Mathf.Clamp(yawRate / 360f, -1f, 1f) * turnBankAngle * speedFactor
            : 0f;

        float alpha = 1f - Mathf.Exp(-12f * dt);
        currentForwardLean = Mathf.Lerp(currentForwardLean, targetForwardLean, alpha);
        currentBankLean = Mathf.Lerp(currentBankLean, targetBank, alpha);

        if (spineBone != null && (Mathf.Abs(currentForwardLean) > 0.05f || Mathf.Abs(currentBankLean) > 0.05f))
        {
            Quaternion pitch = Quaternion.AngleAxis(currentForwardLean, transform.right);
            Quaternion bank = Quaternion.AngleAxis(-currentBankLean, transform.forward);
            spineBone.rotation = pitch * bank * spineBone.rotation;
        }
    }

    #region Combat & Defense Resolution

    public void ReceiveEnemyAttack(Transform attacker, float knockbackForce, SparringEnemy sourceEnemy, AttackHeight attackHeight = AttackHeight.HighMid)
    {
        Vector3 awayDir = attacker != null ? (transform.position - attacker.position) : -transform.forward;
        awayDir.y = 0f;
        if (awayDir.sqrMagnitude < 0.001f) awayDir = -transform.forward;
        awayDir.Normalize();

        // 1. Sifu Avoid (Hold Q + Flick Direction): stays in pocket, checks High/Mid vs Low height match!
        if (isAvoiding && avoidTimer <= avoidActiveWindow)
        {
            if (DoesAvoidMatchAttackHeight(currentAvoidDirection, attackHeight))
            {
                AvoidsLanded++;
                lastAvoidDirectionUsed = currentAvoidDirection;
                avoidCounterTimer = avoidCounterWindow;
                playerStructure = Mathf.Max(0f, playerStructure - avoidStructureRestore);
                structureRecoveryFreezeTimer = 0f;
                TriggerDefenseBanner("AVOID!");

                if (thirdPersonCamera != null)
                {
                    thirdPersonCamera.AddImpactShake(0.08f, 0.08f);
                }

                hitstopTimer = 0.06f;
                if (animator != null) animator.speed = 0.15f;

                if (sourceEnemy != null)
                {
                    sourceEnemy.OpenCounterPunishWindow(avoidCounterWindow);
                }
                return;
            }

            // Wrong height weave (e.g., Duck vs Low Sweep or Hop vs High Punch) gets caught!
            isAvoiding = false;
            currentAvoidDirection = AvoidDirection.None;
        }

        // 2. Step-Dodge startup i-frames: repositions cleanly, freezes Structure recovery briefly.
        if (IsInvulnerable)
        {
            EvadesLanded++;
            structureRecoveryFreezeTimer = 0.9f;
            TriggerDefenseBanner("EVADE!");
            return;
        }

        // 3. Timed Deflect / Parry
        if (IsParryWindowActive)
        {
            ParriesLanded++;
            TriggerDefenseBanner("PARRY!");
            PlayGuardImpactFeedback(attacker);

            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.AddImpactShake(0.16f, 0.12f);
            }

            hitstopTimer = 0.08f;
            if (animator != null) animator.speed = 0.1f;

            if (sourceEnemy != null)
            {
                sourceEnemy.TakeParryStagger(-awayDir);
            }
            return;
        }

        // 4. Held Guard Block (builds Player Structure and triggers Guard Break at 100)
        if (IsGuarding)
        {
            BlocksLanded++;
            PlayGuardImpactFeedback(attacker);

            float structGain = attackHeight == AttackHeight.Low ? blockStructureDamage * 1.25f : blockStructureDamage;
            playerStructure += structGain;
            timeSinceStructureDamaged = 0f;

            if (playerStructure >= MaxPlayerStructure)
            {
                playerStructure = 0f;
                isGuarding = false;
                wasGuardingLastFrame = false;
                guardBreakStunTimer = guardBreakDuration;
                hitReactionTimer = 0.55f;
                externalKnockback = awayDir * (knockbackForce * 1.15f);
                TriggerDefenseBanner("GUARD BROKEN!");
                hitFlashController.TriggerFlash(PlayerHitFlashColor, 0.55f, pulseHz: 10f, baseBlend: 0.72f, emissionIntensity: 2.0f);

                if (animator != null && animator.HasState(0, HitFrontHash))
                {
                    animator.speed = 1.0f;
                    animator.Play(HitFrontHash, 0, 0f);
                }
                if (thirdPersonCamera != null)
                {
                    thirdPersonCamera.AddImpactShake(0.18f, 0.14f);
                }
                return;
            }

            TriggerDefenseBanner("BLOCKED");
            externalKnockback = awayDir * (knockbackForce * 0.45f);
            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.AddImpactShake(0.06f, 0.08f);
            }
            return;
        }

        // 5. Unblocked Hit
        isAttacking = false;
        hasBufferedAttack = false;
        hitReactionTimer = 0.42f;
        externalKnockback = awayDir * knockbackForce;
        playerStructure = Mathf.Min(90f, playerStructure + 14f);
        timeSinceStructureDamaged = 0f;
        hitFlashController.TriggerFlash(PlayerHitFlashColor, 0.42f, pulseHz: 14f, baseBlend: 0.72f, emissionIntensity: 2.0f);

        if (animator != null && animator.HasState(0, HitFrontHash))
        {
            animator.speed = 1.0f;
            animator.Play(HitFrontHash, 0, 0f);
        }

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.AddImpactShake(0.11f, 0.10f);
        }
    }

    private static bool DoesAvoidMatchAttackHeight(AvoidDirection dir, AttackHeight height)
    {
        if (height == AttackHeight.Low)
        {
            return dir == AvoidDirection.Hop;
        }

        return dir == AvoidDirection.Left ||
               dir == AvoidDirection.Right ||
               dir == AvoidDirection.Down;
    }

    private void PlayGuardImpactFeedback(Transform attacker)
    {
        if (attacker != null)
        {
            Vector3 toAttacker = attacker.position - transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(toAttacker);
            }
        }

        if (animator != null && animator.HasState(0, GuardHitHash))
        {
            animator.speed = 1.0f;
            animator.Play(GuardHitHash, 0, 0f);
        }
    }

    private void TriggerDefenseBanner(string label)
    {
        lastDefenseEvent = label;
        defenseEventTimer = 0.9f;
    }

    #endregion

    #region Input Handlers & Scriptable Test Hooks

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnSprint(InputValue value)
    {
        isSprinting = value.isPressed;
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed) TryStartDodge();
    }

    public void OnCrouch(InputValue value)
    {
        if (value.isPressed) TryStartDodge();
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed) HandleAttackRequest(false);
    }

    public void SetTestInputs(Vector2 move, bool sprint, bool guard)
    {
        useTestInputOverride = true;
        moveInput = move;
        isSprinting = sprint;
        testGuardHeld = guard;

        if (guard && !wasGuardingLastFrame) BeginGuard();
        else if (!guard && wasGuardingLastFrame) EndGuard();
        wasGuardingLastFrame = guard;
    }

    public void ClearTestInputOverride()
    {
        useTestInputOverride = false;
        testGuardHeld = false;
    }

    public void TriggerLightAttack() => HandleAttackRequest(false);
    public void TriggerHeavyAttack() => HandleAttackRequest(true);
    public void TriggerDodge() => TryStartDodge();
    public void TriggerAvoid(AvoidDirection direction) => TryStartAvoid(direction);

    private void HandleAttackRequest(bool isHeavy)
    {
        if (isDodging || hitReactionTimer > 0f || guardBreakStunTimer > 0f) return;

        if (isAvoiding)
        {
            // Sifu signature cancel: strike immediately during an Avoid Counter window or late weave!
            if (avoidCounterTimer <= 0f && avoidTimer < avoidDuration * 0.45f) return;
            isAvoiding = false;
            currentAvoidDirection = AvoidDirection.None;
        }

        if (isGuarding)
        {
            EndGuard();
        }

        if (isAttacking)
        {
            if (attackTimer > currentAttackDuration * 0.15f)
            {
                hasBufferedAttack = true;
                bufferedAttackIsHeavy = isHeavy;
            }
            return;
        }

        ExecuteNextComboStep(isHeavy);
    }

    private void TryStartDodge()
    {
        if (isDodging || dodgeCooldownTimer > 0f || hitReactionTimer > 0f || guardBreakStunTimer > 0f) return;

        isAttacking = false;
        hasBufferedAttack = false;
        isAvoiding = false;
        currentAvoidDirection = AvoidDirection.None;
        isGuarding = false;
        wasGuardingLastFrame = false;
        parryWindowTimer = 0f;
        hitstopTimer = 0f;
        comboResetTimer = comboWindowTime;

        Vector3 inputDir = GetCameraRelativeInputDirection();
        bool hasInput = inputDir.sqrMagnitude > 0.01f;

        int targetAnimHash;
        string targetAnimName;
        if (IsLockedOn)
        {
            FaceTransformPlanar(lockedTarget, 3600f);
            dodgeDirection = hasInput ? inputDir : -transform.forward;

            if (!hasInput)
            {
                targetAnimHash = QuickstepBackwardHash;
                targetAnimName = "Quickstep_B";
            }
            else
            {
                float dotForward = Vector3.Dot(dodgeDirection, transform.forward);
                float dotRight = Vector3.Dot(dodgeDirection, transform.right);
                if (dotForward > 0.55f)
                {
                    targetAnimHash = QuickstepForwardHash;
                    targetAnimName = "Quickstep_F";
                }
                else if (dotForward < -0.55f)
                {
                    targetAnimHash = QuickstepBackwardHash;
                    targetAnimName = "Quickstep_B";
                }
                else if (dotRight >= 0f)
                {
                    targetAnimHash = QuickstepRightHash;
                    targetAnimName = "Quickstep_R";
                }
                else
                {
                    targetAnimHash = QuickstepLeftHash;
                    targetAnimName = "Quickstep_L";
                }
            }
        }
        else
        {
            if (hasInput)
            {
                dodgeDirection = inputDir;
                transform.rotation = Quaternion.LookRotation(dodgeDirection);
                targetAnimHash = QuickstepForwardHash;
                targetAnimName = "Quickstep_F";
            }
            else
            {
                dodgeDirection = -transform.forward;
                targetAnimHash = QuickstepBackwardHash;
                targetAnimName = "Quickstep_B";
            }
        }

        isDodging = true;
        usingLockOnBlendState = false;
        dodgeTimer = 0f;
        invulnerableTimer = dodgeInvulnDuration;

        if (animator != null)
        {
            float clipLen = GetRawClipLength(targetAnimName, 0.45f);
            animator.speed = Mathf.Clamp((clipLen * 0.88f) / Mathf.Max(0.12f, dodgeDuration), 1.15f, 2.85f);
            if (animator.HasState(0, targetAnimHash))
            {
                animator.CrossFadeInFixedTime(targetAnimHash, 0.035f);
            }
            else if (animator.HasState(0, LocomotionStateHash))
            {
                animator.CrossFadeInFixedTime(LocomotionStateHash, 0.04f);
            }
        }
    }

    private void TryStartAvoid(AvoidDirection direction)
    {
        bool canChainAvoid = isAvoiding && avoidCounterTimer > 0f && avoidTimer >= 0.12f;
        if (direction == AvoidDirection.None || isDodging || (isAvoiding && !canChainAvoid) || (!canChainAvoid && avoidCooldownTimer > 0f) || hitReactionTimer > 0f || guardBreakStunTimer > 0f)
        {
            return;
        }

        if (isAttacking && attackTimer < currentAttackDuration * 0.45f) return;

        isAttacking = false;
        hasBufferedAttack = false;
        isGuarding = false;
        parryWindowTimer = 0f;
        hitstopTimer = 0f;
        comboResetTimer = comboWindowTime;

        isAvoiding = true;
        usingLockOnBlendState = false;
        avoidTimer = 0f;
        currentAvoidDirection = direction;
        lastAvoidDirectionUsed = direction;

        Transform focus = IsLockedOn ? lockedTarget : (targetEnemy != null ? targetEnemy : FindBestTarget());
        if (focus != null)
        {
            targetEnemy = focus;
            FaceTransformPlanar(focus, 3600f);
        }

        int targetAnimHash;
        string targetAnimName;
        switch (direction)
        {
            case AvoidDirection.Left:
                targetAnimHash = AvoidLeftHash;
                targetAnimName = "Avoid_Left";
                break;
            case AvoidDirection.Right:
                targetAnimHash = AvoidRightHash;
                targetAnimName = "Avoid_Right";
                break;
            case AvoidDirection.Hop:
                targetAnimHash = AvoidHopHash;
                targetAnimName = "Avoid_Hop";
                break;
            default:
                targetAnimHash = AvoidDuckHash;
                targetAnimName = "Avoid_Duck";
                break;
        }

        if (animator != null)
        {
            float clipLen = GetRawClipLength(targetAnimName, 0.36f);
            animator.speed = Mathf.Clamp((clipLen * 0.92f) / Mathf.Max(0.15f, avoidDuration), 0.95f, 4.0f);
            if (animator.HasState(0, targetAnimHash))
            {
                animator.CrossFadeInFixedTime(targetAnimHash, 0.035f);
            }
        }
    }

    private void ExecuteNextComboStep(bool isHeavy)
    {
        planarVelocity = Vector3.zero;
        string[] activeList = isHeavy ? heavyComboStateNames : comboStateNames;
        if (activeList == null || activeList.Length == 0) return;

        if (currentAttackIsHeavy != isHeavy)
        {
            // Smoothly mix from Light -> Heavy or Heavy -> Light mid-chain!
            comboStep = 1;
            currentAttackIsHeavy = isHeavy;
        }
        else
        {
            comboStep++;
            if (comboStep > activeList.Length)
            {
                comboStep = 1;
            }
        }

        string targetState = activeList[comboStep - 1];

        if (animator != null)
        {
            animator.speed = attackSpeedMultiplier;
            if (animator.HasState(0, Animator.StringToHash(targetState)))
            {
                animator.CrossFadeInFixedTime(targetState, transitionDuration);
            }
        }

        targetEnemy = FindBestTarget();
        isAttacking = true;
        usingLockOnBlendState = false;
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
            Vector3 toTarget = targetEnemy.position - transform.position;
            toTarget.y = 0f;
            float distToTarget = toTarget.magnitude;
            float maxReach = GetCurrentStopDistance() + 0.70f;

            if (distToTarget <= maxReach)
            {
                IDamageable damageable = targetEnemy.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    bool isFinisher = comboStep == MaxComboSteps;
                    float knockbackForce = currentAttackIsHeavy
                        ? (isFinisher ? 16.0f : 11.0f)
                        : (isFinisher ? 14.0f : 7.0f);

                    Vector3 hitDir = distToTarget > 0.001f ? toTarget / distToTarget : transform.forward;
                    damageable.TakeHit(hitDir, knockbackForce);

                    if (thirdPersonCamera != null)
                    {
                        float shake = currentAttackIsHeavy || isFinisher ? 0.14f : 0.06f;
                        thirdPersonCamera.AddImpactShake(shake, 0.10f);
                    }

                    float hitstop = currentAttackIsHeavy ? hitstopDuration * 1.25f : hitstopDuration;
                    if (hitstop > 0f)
                    {
                        hitstopTimer = hitstop;
                        if (animator != null) animator.speed = 0.05f;
                    }
                }
            }
        }
    }

    private float GetCurrentHitTimingRatio()
    {
        float[] table = currentAttackIsHeavy ? heavyHitTimingRatios : hitTimingRatios;
        if (table != null && table.Length >= comboStep && comboStep > 0)
        {
            return table[comboStep - 1];
        }
        return 0.35f;
    }

    private float GetCurrentStopDistance()
    {
        float[] table = currentAttackIsHeavy ? heavyStopDistances : stopDistancesPerHit;
        if (table != null && table.Length >= comboStep && comboStep > 0)
        {
            return table[comboStep - 1];
        }
        return 1.1f;
    }

    private float GetRawClipLength(string stateName, float fallbackLength)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name.Equals(stateName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip.length;
                }
            }
        }

        return fallbackLength;
    }

    private float GetStateDuration(string stateName)
    {
        float raw = GetRawClipLength(stateName, 0f);
        if (raw > 0f)
        {
            return Mathf.Max(0.35f, raw * 0.80f);
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
