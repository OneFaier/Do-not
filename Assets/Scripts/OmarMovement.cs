using UnityEngine;

public class OmarMovement : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform cameraTransform;

    [Header("Déplacement Sol")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;

    [Header("Gravité du joueur")]
    [Tooltip("1 = normale. Active uniquement hors escalade.")]
    [Min(0f)]
    [SerializeField] private float gravityMultiplier = 1.5f;

    [Header("Détection Sol")]
    [SerializeField] private float groundCheckDistance = 1.1f;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private float coyoteTime = 0.2f;

    [Header("Caméra")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Poussée horizontale du vault")]
    [Min(0.01f)]
    [SerializeField] private float vaultBoostDuration = 0.3f;

    [Header("Rebond des surfaces")]
    [Tooltip(
        "Durée pendant laquelle ZQSD ne remplace pas " +
        "la vitesse du rebond. La caméra reste contrôlable.")]
    [Min(0f)]
    [SerializeField] private float bounceControlDelay = 0.25f;

    private OmarPlayer climbing;

    private float cameraPitch;
    private bool isGrounded;
    private float coyoteTimeCounter;

    private Vector3 vaultHorizontalVelocity;
    private float vaultBoostRemaining;
    private float activeVaultBoostDuration;

    private float bounceTimer;

    private bool IsClimbing
    {
        get
        {
            return climbing != null
                ? climbing.IsClimbing
                : !rb.useGravity;
        }
    }

    private void Start()
    {
        if (!rb)
            rb = GetComponent<Rigidbody>();

        climbing = GetComponent<OmarPlayer>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMouseLook();
        CheckGrounded();

        if (isGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;

        if (coyoteTimeCounter > 0f
            && Input.GetButtonDown("Jump")
            && !IsClimbing
            && rb.useGravity
            && bounceTimer <= 0f)
        {
            Jump();
        }
    }

    private void FixedUpdate()
    {
        HandleGroundMovement();
        ApplyExtraGravity();

        if (bounceTimer > 0f)
        {
            bounceTimer = Mathf.Max(
                0f,
                bounceTimer - Time.fixedDeltaTime);
        }
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
        if (IsClimbing || !rb.useGravity)
        {
            CancelVaultBoost();
            return;
        }

        // Laisse l'impulsion du rebond s'exprimer.
        if (bounceTimer > 0f)
            return;

        // Contrôle initial : ZQSD au sol comme en l'air.
        Vector3 targetVelocity = GetInputVelocity();

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

        targetVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = targetVelocity;
    }

    private void ApplyExtraGravity()
    {
        if (IsClimbing || !rb.useGravity)
            return;

        // Unity applique déjà la gravité normale.
        Vector3 extraGravity =
            Physics.gravity
            * (Mathf.Max(0f, gravityMultiplier) - 1f);

        rb.AddForce(extraGravity, ForceMode.Acceleration);
    }

    public void ApplyVaultImpulse(Vector3 impulse)
    {
        if (!rb)
            rb = GetComponent<Rigidbody>();

        bounceTimer = 0f;

        Vector3 vaultVelocity = impulse / rb.mass;

        vaultHorizontalVelocity = new Vector3(
            vaultVelocity.x,
            0f,
            vaultVelocity.z);

        activeVaultBoostDuration =
            Mathf.Max(0.01f, vaultBoostDuration);

        vaultBoostRemaining = activeVaultBoostDuration;

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

    // Appelé quand une nouvelle prise est attrapée.
    public void NotifyGrabbed()
    {
        CancelVaultBoost();
        bounceTimer = 0f;
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

    private void OnCollisionEnter(Collision collision)
    {
        ClimbableSurface surface =
            collision.collider.GetComponentInParent<ClimbableSurface>();

        if (surface == null)
            return;

        SurfaceTypeSO type = surface.SurfaceType;

        if (type == null || !type.hasBounce)
            return;

        if (collision.contactCount == 0)
            return;

        // Un rebond libère les mains pour éviter que
        // la corde et le rebond se contredisent.
        if (climbing)
            climbing.ReleaseForBounce();

        CancelVaultBoost();

        Vector3 contactNormal =
            collision.GetContact(0).normal;

        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;

        rb.AddForce(
            contactNormal * Mathf.Max(0f, type.bounceForce),
            ForceMode.Impulse);

        bounceTimer = Mathf.Max(0f, bounceControlDelay);
        coyoteTimeCounter = 0f;
        isGrounded = false;
    }
}