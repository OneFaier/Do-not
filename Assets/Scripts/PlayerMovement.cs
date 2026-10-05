using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform cameraTransform;

    [Header("Déplacement Sol")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 1.1f;

    [Header("Caméra")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxPitch = 85f;

    private float cameraPitch = 0f;
    private bool isGrounded;

    private void Start()
    {
        if (!rb) rb = GetComponent<Rigidbody>();

        // Verrouille le curseur au centre de l'écran pour la vue FPS
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMouseLook();
        CheckGrounded();

        if (isGrounded && Input.GetButtonDown("Jump") && rb.useGravity)
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
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotation horizontale du corps du joueur
        transform.Rotate(Vector3.up * mouseX);

        // Rotation verticale de la caméra (Haut/Bas)
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxPitch, maxPitch);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleGroundMovement()
    {
        // Si la gravité est désactivée (en train de grimper), on laisse le ClimbingController gérer la physique
        if (!rb.useGravity) return;

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 moveDirection = transform.right * moveX + transform.forward * moveZ;
        Vector3 targetVelocity = moveDirection.normalized * moveSpeed;

        // Conserve la vitesse verticale de la gravité ou du saut
        targetVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = targetVelocity;
    }

    private void CheckGrounded()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
    }
}