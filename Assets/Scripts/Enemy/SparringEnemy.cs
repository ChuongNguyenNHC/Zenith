using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SparringEnemy : MonoBehaviour, IDamageable
{
    public enum AIState
    {
        Idle,
        Approach,
        Windup,
        Attack,
        HitStun,
        Parried
    }

    [Header("Movement & Combat Ranges")]
    [SerializeField] private float moveSpeed = 2.4f;
    [SerializeField] private float engageRange = 6.5f;
    [SerializeField] private float stopDistance = 1.65f;
    [SerializeField] private float rotationSpeed = 540f;
    [SerializeField] private float knockbackFriction = 8.0f;
    [SerializeField] private float gravity = -19.62f;

    [Header("Sparring Timings & Structure")]
    [SerializeField] private float attackCooldown = 2.0f;
    [SerializeField] private float windupDuration = 0.45f;
    [SerializeField] private float lowSweepWindupDuration = 0.56f;
    [SerializeField] private float attackDuration = 0.72f;
    [SerializeField] private float lowSweepDuration = 0.84f;
    [SerializeField] private float attackHitRatio = 0.38f;
    [SerializeField] private float attackSpeedMultiplier = 0.85f;
    [SerializeField] private float hitStunDuration = 0.55f;
    [SerializeField] private float parryStaggerDuration = 1.25f;
    [SerializeField] private float structureDecayPerSecond = 8f;
    [SerializeField] private float structureRecoveryDelay = 3.0f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Renderer[] flashRenderers;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color parryFlashColor = new Color(1.0f, 0.82f, 0.2f);
    [SerializeField] private Color lowSweepTelegraphColor = new Color(1.0f, 0.02f, 0.22f);

    private const float MaxStructureValue = 100f;

    private CharacterController controller;
    private PlayerController playerTarget;
    private AIState state = AIState.Idle;
    private float stateTimer;
    private float cooldownTimer = 0.85f;
    private bool hasDealtAttackHit;
    private Vector3 knockbackVelocity;
    private float verticalVelocity;
    private int attackSequenceIndex;
    private AttackHeight? forcedNextAttackHeight;
    private AttackHeight pendingAttackHeight = AttackHeight.HighMid;
    private float structureValue;
    private float timeSinceStructureHit = 99f;
    private float activeWindupDuration = 0.45f;

    private readonly CombatFlashController flashController = new CombatFlashController();
    private AttackWarningSignal warningSignal;

    public AIState CurrentState => state;
    public float WindupNormalized => state == AIState.Windup && activeWindupDuration > 0.001f
        ? 1f - Mathf.Clamp01(stateTimer / activeWindupDuration)
        : 0f;
    public int HitCount { get; private set; }
    public int ParryStaggerCount { get; private set; }
    public int StructureBreakCount { get; private set; }
    public AttackHeight PendingAttackHeight => pendingAttackHeight;
    public bool IsLowSweep => pendingAttackHeight == AttackHeight.Low;
    public float Structure => structureValue;
    public float MaxStructure => MaxStructureValue;
    public float StructureNormalized => Mathf.Clamp01(structureValue / MaxStructureValue);

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackStateHash = Animator.StringToHash("Atk_P_1");
    private static readonly int LowSweepStateHash = Animator.StringToHash("front sweep");
    private static readonly int HitFrontHash = Animator.StringToHash("Hit_F");
    private static readonly int HitBackHash = Animator.StringToHash("Hit_B");
    private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.minMoveDistance = 0f;
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        if (flashRenderers == null || flashRenderers.Length == 0)
        {
            flashRenderers = GetComponentsInChildren<Renderer>();
        }

        flashController.Initialize(flashRenderers);
        warningSignal = new AttackWarningSignal(transform);
    }

    private void OnDestroy()
    {
        warningSignal?.Dispose();
    }

    private void Start()
    {
        playerTarget = FindAnyObjectByType<PlayerController>();
    }

    private void Update()
    {
        if (playerTarget == null)
        {
            playerTarget = FindAnyObjectByType<PlayerController>();
        }

        ApplyGravity();

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        timeSinceStructureHit += Time.deltaTime;
        if (timeSinceStructureHit >= structureRecoveryDelay && state != AIState.Parried && state != AIState.HitStun)
        {
            structureValue = Mathf.MoveTowards(structureValue, 0f, structureDecayPerSecond * Time.deltaTime);
        }

        flashController.Update(Time.deltaTime);

        Vector3 horizontalMove = UpdateKnockback();
        horizontalMove += UpdateAIState();

        Vector3 totalMove = new Vector3(horizontalMove.x, verticalVelocity * Time.deltaTime, horizontalMove.z);
        controller.Move(totalMove);

        warningSignal?.UpdateVisual(state == AIState.Windup, WindupNormalized, IsLowSweep, Camera.main);
    }

    private Vector3 UpdateKnockback()
    {
        if (knockbackVelocity.sqrMagnitude <= 0.01f) return Vector3.zero;
        Vector3 step = knockbackVelocity * Time.deltaTime;
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackFriction * 5.0f * Time.deltaTime);
        return step;
    }

    private float GetPlanarDistanceToPlayer()
    {
        if (playerTarget == null) return float.MaxValue;
        Vector3 delta = playerTarget.transform.position - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private Vector3 UpdateAIState()
    {
        Vector3 aiMove = Vector3.zero;
        float dist = GetPlanarDistanceToPlayer();

        switch (state)
        {
            case AIState.Idle:
                SetMoveAnim(0f);
                if (playerTarget != null && dist <= engageRange)
                {
                    FacePlayer();
                    if (dist > stopDistance + 0.08f)
                    {
                        state = AIState.Approach;
                    }
                    else if (cooldownTimer <= 0f)
                    {
                        EnterWindup();
                    }
                }
                break;

            case AIState.Approach:
                if (playerTarget == null || dist > engageRange * 1.25f)
                {
                    state = AIState.Idle;
                    break;
                }

                FacePlayer();
                if (dist > stopDistance + 0.05f)
                {
                    Vector3 toPlayer = playerTarget.transform.position - transform.position;
                    toPlayer.y = 0f;
                    float step = Mathf.Min(dist - stopDistance, moveSpeed * Time.deltaTime);
                    aiMove = toPlayer.normalized * step;
                    SetMoveAnim(1f);
                }
                else
                {
                    SetMoveAnim(0f);
                    if (cooldownTimer <= 0f)
                    {
                        EnterWindup();
                    }
                    else
                    {
                        state = AIState.Idle;
                    }
                }
                break;

            case AIState.Windup:
                SetMoveAnim(0f);
                FacePlayer();
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    BeginAttack();
                }
                break;

            case AIState.Attack:
                SetMoveAnim(0f);
                stateTimer += Time.deltaTime;
                float activeDuration = IsLowSweep ? lowSweepDuration : attackDuration;
                if (!hasDealtAttackHit && stateTimer >= activeDuration * attackHitRatio)
                {
                    hasDealtAttackHit = true;
                    TryHitPlayer();
                }
                if (stateTimer >= activeDuration)
                {
                    cooldownTimer = attackCooldown;
                    ReturnToLocomotion();
                }
                break;

            case AIState.HitStun:
            case AIState.Parried:
                SetMoveAnim(0f);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                {
                    cooldownTimer = Mathf.Max(cooldownTimer, 0.7f);
                    ReturnToLocomotion();
                }
                break;
        }

        return aiMove;
    }

    public void SetForcedAttackHeight(AttackHeight? nextHeight)
    {
        forcedNextAttackHeight = nextHeight;
    }

    private void EnterWindup()
    {
        state = AIState.Windup;
        pendingAttackHeight = forcedNextAttackHeight.HasValue
            ? forcedNextAttackHeight.Value
            : (attackSequenceIndex % 2 == 1 ? AttackHeight.Low : AttackHeight.HighMid);
        attackSequenceIndex++;

        float activeWindup = IsLowSweep ? lowSweepWindupDuration : windupDuration;
        activeWindupDuration = activeWindup;
        stateTimer = activeWindup;
        Color cue = IsLowSweep ? lowSweepTelegraphColor : new Color(1.0f, 0.03f, 0.01f);
        flashController.TriggerFlash(cue, activeWindup, pulseHz: 8.5f, baseBlend: 0.65f, emissionIntensity: 1.9f);
    }

    private void BeginAttack()
    {
        state = AIState.Attack;
        stateTimer = 0f;
        hasDealtAttackHit = false;

        int stateHash = IsLowSweep && animator != null && animator.HasState(0, LowSweepStateHash)
            ? LowSweepStateHash
            : AttackStateHash;

        if (animator != null && animator.HasState(0, stateHash))
        {
            animator.speed = IsLowSweep ? attackSpeedMultiplier * 0.92f : attackSpeedMultiplier;
            animator.CrossFadeInFixedTime(stateHash, 0.08f);
        }
    }

    private void TryHitPlayer()
    {
        if (playerTarget == null) return;
        if (GetPlanarDistanceToPlayer() <= stopDistance + 0.75f)
        {
            playerTarget.ReceiveEnemyAttack(transform, 5.5f, this, pendingAttackHeight);
        }
    }

    private void ReturnToLocomotion()
    {
        state = AIState.Idle;
        if (animator != null)
        {
            animator.speed = 1.0f;
            if (animator.HasState(0, LocomotionHash))
            {
                animator.CrossFadeInFixedTime(LocomotionHash, 0.12f);
            }
        }
    }

    private void FacePlayer()
    {
        if (playerTarget == null) return;
        Vector3 dir = playerTarget.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Time.deltaTime);
        }
    }

    private void SetMoveAnim(float speedValue)
    {
        if (animator == null) return;
        animator.SetFloat(SpeedHash, speedValue, 0.1f, Time.deltaTime);
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    public void TakeHit(Vector3 hitDirection, float knockbackForce)
    {
        HitCount++;
        hitDirection.y = 0f;
        knockbackVelocity = hitDirection.normalized * knockbackForce;

        state = AIState.HitStun;
        stateTimer = hitStunDuration;
        cooldownTimer = Mathf.Max(cooldownTimer, 0.9f);
        flashController.TriggerFlash(hitColor, 0.28f, pulseHz: 14f, baseBlend: 0.72f, emissionIntensity: 2.0f);

        if (animator != null)
        {
            animator.speed = 1.0f;
            if (animator.HasState(0, HitFrontHash))
            {
                animator.Play(HitFrontHash, 0, 0f);
            }
        }

        TakeStructureDamage(knockbackForce * 2.2f, hitDirection, false);
    }

    public void TakeStructureDamage(float amount, Vector3 pushDirection, bool staggerOnBreak = true)
    {
        timeSinceStructureHit = 0f;
        structureValue += amount;
        if (structureValue >= MaxStructureValue)
        {
            structureValue = 0f;
            StructureBreakCount++;
            if (staggerOnBreak)
            {
                TakeParryStagger(pushDirection);
            }
            else
            {
                state = AIState.Parried;
                stateTimer = parryStaggerDuration * 1.15f;
                cooldownTimer = attackCooldown + 0.8f;
                flashController.TriggerFlash(parryFlashColor, 0.40f, pulseHz: 6f, baseBlend: 0.60f, emissionIntensity: 2.4f);
            }
        }
    }

    public void TakeParryStagger(Vector3 pushDirection)
    {
        ParryStaggerCount++;
        pushDirection.y = 0f;
        knockbackVelocity = pushDirection.normalized * 6.5f;

        state = AIState.Parried;
        stateTimer = parryStaggerDuration;
        cooldownTimer = attackCooldown + 0.5f;
        flashController.TriggerFlash(parryFlashColor, 0.35f, pulseHz: 6f, baseBlend: 0.60f, emissionIntensity: 2.4f);

        if (animator != null)
        {
            animator.speed = 0.85f;
            int hash = animator.HasState(0, HitBackHash) ? HitBackHash : HitFrontHash;
            if (animator.HasState(0, hash))
            {
                animator.Play(hash, 0, 0f);
            }
        }

        TakeStructureDamage(38f, pushDirection, false);
    }

    public void OpenCounterPunishWindow(float punishDuration)
    {
        cooldownTimer = Mathf.Max(cooldownTimer, attackCooldown + punishDuration);
        flashController.TriggerFlash(new Color(0.25f, 0.92f, 1.0f), 0.24f, pulseHz: 0f, baseBlend: 0.55f, emissionIntensity: 2.0f);
        if (animator != null && state == AIState.Attack)
        {
            animator.speed = 0.55f;
        }
    }
}
