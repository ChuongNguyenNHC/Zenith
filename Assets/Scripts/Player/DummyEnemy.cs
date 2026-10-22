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
    private Material dummyMaterial;
    private Color originalColor;
    private float flashTimer = 0f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (dummyRenderer == null)
        {
            dummyRenderer = GetComponentInChildren<Renderer>();
        }

        if (dummyRenderer != null)
        {
            dummyMaterial = dummyRenderer.material;
            if (dummyMaterial.HasProperty("_BaseColor"))
            {
                originalColor = dummyMaterial.GetColor("_BaseColor");
            }
            else if (dummyMaterial.HasProperty("_Color"))
            {
                originalColor = dummyMaterial.color;
            }
        }
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

        // Hiệu ứng nhấp nháy đỏ khi ăn đòn
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && dummyMaterial != null)
            {
                SetColor(originalColor);
            }
        }
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
        direction.y = 0f;
        // Đặt vận tốc đẩy lùi
        knockbackVelocity = direction.normalized * force;

        // Nhấp nháy màu đỏ báo hiệu trúng đòn (Hit reaction)
        if (dummyMaterial != null)
        {
            SetColor(hitColor);
            flashTimer = 0.15f;
        }

        if (animator != null)
        {
            animator.Play("Hit_F", 0, 0f);
        }
    }

    private void SetColor(Color color)
    {
        if (dummyMaterial.HasProperty("_BaseColor"))
        {
            dummyMaterial.SetColor("_BaseColor", color);
        }
        else if (dummyMaterial.HasProperty("_Color"))
        {
            dummyMaterial.color = color;
        }
    }
}
