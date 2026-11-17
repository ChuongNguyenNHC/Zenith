using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class DummyEnemy : MonoBehaviour, IDamageable
{
    [Header("Settings")]
    [Tooltip("Khối lượng cản lực đẩy lùi (càng cao thì càng nhanh dừng lại)")]
    [SerializeField] private float knockbackFriction = 8.0f;

    [Tooltip("Trọng lực")]
    [SerializeField] private float gravity = -19.62f;

    [Tooltip("Animator của Dummy (nếu có để diễn hoạt giật mình)")]
    [SerializeField] private Animator animator;

    [Header("Visual Feedback")]
    [Tooltip("MeshRenderer để nhấp nháy đỏ khi trúng đòn")]
    [SerializeField] private Renderer dummyRenderer;
    [SerializeField] private Color hitColor = Color.red;

    private CharacterController characterController;
    private Vector3 knockbackVelocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private readonly CombatFlashController flashController = new CombatFlashController();

    public int HitCount { get; private set; }
    public float LastHitForce { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        Renderer[] renderers = dummyRenderer != null
            ? new[] { dummyRenderer }
            : GetComponentsInChildren<Renderer>();
        flashController.Initialize(renderers);
    }

    private void Update()
    {
        ApplyGravity();

        // Xử lý lực đẩy lùi (Knockback)
        Vector3 horizontalMove = Vector3.zero;
        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            horizontalMove = knockbackVelocity * Time.deltaTime;
            // Giảm dần lực đẩy lùi theo ma sát
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackFriction * 5.0f * Time.deltaTime);
        }

        Vector3 totalMove = horizontalMove;
        totalMove.y = verticalVelocity * Time.deltaTime;
        characterController.Move(totalMove);

        flashController.Update(Time.deltaTime);
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

    /// <summary>
    /// Nhận sát thương và chịu lực đẩy lùi (Knockback) từ đòn đánh của Player
    /// </summary>
    public void TakeHit(Vector3 direction, float force)
    {
        HitCount++;
        LastHitForce = force;
        direction.y = 0f;
        // Đặt vận tốc đẩy lùi
        knockbackVelocity = direction.normalized * force;

        // Nhấp nháy đỏ phát sáng báo hiệu trúng đòn (Hit reaction)
        flashController.TriggerFlash(hitColor, 0.26f, pulseHz: 14f, baseBlend: 0.72f, emissionIntensity: 2.8f);

        if (animator != null && animator.HasState(0, Animator.StringToHash("Hit_F")))
        {
            animator.Play("Hit_F", 0, 0f);
        }
    }
}
