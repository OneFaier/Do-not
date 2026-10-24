using UnityEngine;

public class PlayerMovement : MonoBehaviour
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

    private float cameraPitch = 0f;
    private bool isGrounded;
    private float coyoteTimeCounter;
    private float bounceTimer = 0f; // Empêche le sol d'interférer pendant un rebond

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

        // Décompte du timer de rebond
        if (bounceTimer > 0f) bounceTimer -= Time.deltaTime;

        // Gestion du coyote time
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // Assure-toi que le saut n'est autorisé que si la gravité est active
        if (coyoteTimeCounter > 0f && Input.GetButtonDown("Jump") && rb.useGravity)
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
        // On applique le mouvement au sol UNIQUEMENT si on a la gravité, qu'on ne rebondit pas, ET qu'on touche le sol !
        if (!rb.useGravity || bounceTimer > 0f || !isGrounded) return;

        // Utilisation de GetAxisRaw pour un arrêt immédiat sans lissage d'entrée
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Vector3 moveDirection = (transform.right * moveX + transform.forward * moveZ).normalized;
        Vector3 targetVelocity = moveDirection * moveSpeed;

        // Conserve la vitesse verticale de la gravité ou du saut
        targetVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = targetVelocity;
    }

    private void CheckGrounded()
    {
        // Raycast depuis la position du joueur vers le bas
        isGrounded = Physics.SphereCast(transform.position, groundCheckRadius, Vector3.down, out RaycastHit hit, groundCheckDistance);
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Vérifie si l'objet touché possède un composant ClimbableSurface
        ClimbableSurface surface = collision.collider.GetComponent<ClimbableSurface>();
    
        if (surface != null && surface.SurfaceType != null && surface.SurfaceType.hasBounce)
        {
            // Récupère la normale exacte du point d'impact
            Vector3 contactNormal = collision.contacts[0].normal;

            // Annule la vitesse actuelle pour un rebond net et propre
            rb.linearVelocity = Vector3.zero;

            // Applique l'impulsion de rebond en fonction de la force définie dans le SurfaceTypeSO
            rb.AddForce(contactNormal * surface.SurfaceType.bounceForce, ForceMode.Impulse);

            // Bloque le script de mouvement au sol pendant 0.25s pour laisser le rebond s'exprimer
            bounceTimer = 0.25f;
        }
    }
}