using UnityEngine;

public class OmarMovement : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform cameraTransform;

    [Header("Déplacement Sol")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;

    [Header("Détection Sol")]
    [SerializeField] private float groundCheckDistance = 1.1f;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private float coyoteTime = 0.2f;

    [Header("Caméra")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Poussée horizontale du vault")]
    [Tooltip("Durée pendant laquelle la poussée horizontale diminue jusqu'à zéro.")]
    [Min(0.01f)]
    [SerializeField] private float vaultBoostDuration = 0.3f;

    private float cameraPitch = 0f;
    private bool isGrounded;
    private float coyoteTimeCounter;

    private Vector3 vaultHorizontalVelocity;
    private float vaultBoostRemaining;
    private float activeVaultBoostDuration;

    private void Start()
    {
        if (!rb)
            rb = GetComponent<Rigidbody>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMouseLook();
        CheckGrounded();

        // Gestion initiale du coyote time.
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (coyoteTimeCounter > 0f
            && Input.GetButtonDown("Jump")
            && rb.useGravity)
        {
            Jump();
        }
    }

    private void FixedUpdate()
    {
        HandleGroundMovement();
    }

    private void HandleMouseLook()
    {
        float mouseX =
            Input.GetAxis("Mouse X") * mouseSensitivity;

        float mouseY =
            Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;

        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxPitch,
            maxPitch);

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private Vector3 GetInputVelocity()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection =
            transform.right * moveX
            + transform.forward * moveZ;

        return moveDirection.normalized * moveSpeed;
    }

    private void HandleGroundMovement()
    {
        // Pendant l'escalade, OmarPlayer gère la physique.
        if (!rb.useGravity)
        {
            CancelVaultBoost();
            return;
        }

        // Même déplacement qu'avant, y compris en l'air.
        Vector3 targetVelocity = GetInputVelocity();

        // Ajoute uniquement le supplément de vitesse du vault.
        if (vaultBoostRemaining > 0f)
        {
            float remainingRatio = Mathf.Clamp01(
                vaultBoostRemaining / activeVaultBoostDuration);

            targetVelocity +=
                vaultHorizontalVelocity * remainingRatio;

            vaultBoostRemaining = Mathf.Max(
                0f,
                vaultBoostRemaining - Time.fixedDeltaTime);
        }

        // La vitesse verticale reste gérée par la physique.
        targetVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = targetVelocity;
    }

    // Appelé uniquement lors d'un vault.
    public void ApplyVaultImpulse(Vector3 impulse)
    {
        if (!rb)
            rb = GetComponent<Rigidbody>();

        // Comme ForceMode.Impulse : variation de vitesse = impulsion / masse.
        Vector3 vaultVelocity = impulse / rb.mass;

        vaultHorizontalVelocity = new Vector3(
            vaultVelocity.x,
            0f,
            vaultVelocity.z);

        activeVaultBoostDuration =
            Mathf.Max(0.01f, vaultBoostDuration);

        vaultBoostRemaining = activeVaultBoostDuration;

        // Remplace l'ancien élan, comme le vault initial,
        // et conserve le déplacement ZQSD en supplément.
        Vector3 velocity =
            GetInputVelocity() + vaultHorizontalVelocity;

        velocity.y = vaultVelocity.y;
        rb.linearVelocity = velocity;

        coyoteTimeCounter = 0f;
    }

    public void CancelVaultBoost()
    {
        vaultHorizontalVelocity = Vector3.zero;
        vaultBoostRemaining = 0f;
    }

    private void CheckGrounded()
    {
        isGrounded = Physics.SphereCast(
            transform.position,
            groundCheckRadius,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance);
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            jumpForce,
            rb.linearVelocity.z);
    }
}