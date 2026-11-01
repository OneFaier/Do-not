using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Référence au Rigidbody du joueur pour la gestion de la physique.")]
    [SerializeField] private Rigidbody rb;
    [Tooltip("Référence à la caméra principale pour orienter les mouvements par rapport à la vue.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Déplacement & Inertie")]
    [Tooltip("Vitesse maximale de déplacement au sol.")]
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Force appliquée lors d'une impulsion de saut.")]
    [SerializeField] private float jumpForce = 5f;
    [Tooltip("Vitesse d'accélération et de réactivité au sol (Lerp).")]
    [SerializeField] private float groundAcceleration = 15f; 
    [Range(0f, 10f)]
    [Tooltip("Maîtrise de la trajectoire en l'air (0 = inertie pure, 10  = contrôle total immédiat).")]
    [SerializeField] private float airControl = 0.3f;        
    
    [Header("Détection Sol")]
    [Tooltip("Distance du Raycast/SphereCast pour détecter le sol.")]
    [SerializeField] private float groundCheckDistance = 1.1f;
    [Tooltip("Rayon de la sphère pour la détection du sol.")]
    [SerializeField] private float groundCheckRadius = 0.2f;
    [Tooltip("Temps de tolérance pour sauter juste après avoir quitté un bord (Coyote Time).")]
    [SerializeField] private float coyoteTime = 0.2f;

    [Header("Caméra")]
    [Tooltip("Sensibilité de la souris pour la vue FPS.")]
    [SerializeField] private float mouseSensitivity = 2f;
    [Tooltip("Angle maximal de rotation verticale (haut/bas) de la caméra.")]
    [SerializeField] private float maxPitch = 85f;

    // Variables privées d'état
    private float cameraPitch = 0f;
    private bool isGrounded;
    private float coyoteTimeCounter;
    private float bounceTimer = 0f; // Empêche les interférences du sol pendant un rebond physique

    private void Start()
    {
        // Récupération automatique du Rigidbody s'il n'est pas assigné
        if (!rb) rb = GetComponent<Rigidbody>();

        // Verrouillage et masquage du curseur pour l'immersion FPS
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMouseLook();
        CheckGrounded();

        // Décompte du timer de rebond
        if (bounceTimer > 0f) bounceTimer -= Time.deltaTime;

        // Gestion du Coyote Time (maintien du saut hors du sol temporairement)
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        // Déclenchement du saut si les conditions sont réunies
        if (coyoteTimeCounter > 0f && Input.GetButtonDown("Jump") && rb.useGravity)
        {
            Jump();
        }
    }

    private void FixedUpdate()
    {
        // Exécution des calculs physiques à intervalles réguliers
        HandleMovement();
    }

    /// <summary>
    /// Gère la rotation de la caméra (pitch) et du corps du joueur (yaw) via la souris.
    /// </summary>
    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotation horizontale (fait pivoter tout le GameObject du joueur)
        transform.Rotate(Vector3.up * mouseX);

        // Rotation verticale (limitée par maxPitch pour éviter de casser la nuque)
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxPitch, maxPitch);
        cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    /// <summary>
    /// Gère les déplacements fluides, l'inertie horizontale et l'air control.
    /// </summary>
    private void HandleMovement()
    {
        // Si la gravité est coupée (en train d'escalader) ou en plein rebond, on ignore ce script
        if (!rb.useGravity || bounceTimer > 0f) return;

        // Récupération des entrées utilisateur brutes (pas de lissage d'input)
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = (transform.right * moveX + transform.forward * moveZ).normalized;

        // --- PHYSIQUE : Isolation de l'inertie horizontale ---
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        
        // Vitesse cible vers laquelle le joueur souhaite aller selon ses inputs
        Vector3 targetVelocity = inputDir * moveSpeed;
        Vector3 finalHorizontalVelocity;

        if (isGrounded)
        {
            // --- CALCUL PHYSIQUE (AU SOL) : Accélération/Décélération réactive ---
            finalHorizontalVelocity = Vector3.Lerp(horizontalVelocity, targetVelocity, groundAcceleration * Time.fixedDeltaTime);
        }
        else
        {
            // --- CALCUL PHYSIQUE (EN L'AIR) : Air Control et conservation d'élan ---
            Vector3 airVelocity = Vector3.Lerp(horizontalVelocity, targetVelocity, airControl * Time.fixedDeltaTime);
            
            // Si aucune touche n'est pressée en l'air, on conserve 100% de l'élan pur (pas de freinage artificiel)
            if (inputDir.magnitude < 0.1f)
            {
                finalHorizontalVelocity = horizontalVelocity;
            }
            else
            {
                finalHorizontalVelocity = airVelocity;
            }
        }

        // Réapplication de la vélocité calculée tout en préservant la vitesse verticale (gravité, chute, etc.)
        rb.linearVelocity = new Vector3(finalHorizontalVelocity.x, currentVelocity.y, finalHorizontalVelocity.z);
    }

    /// <summary>
    /// Vérifie si le joueur touche le sol à l'aide d'un SphereCast orienté vers le bas.
    /// </summary>
    private void CheckGrounded()
    {
        isGrounded = Physics.SphereCast(transform.position, groundCheckRadius, Vector3.down, out RaycastHit hit, groundCheckDistance);
    }

    /// <summary>
    /// Applique une impulsion verticale instantanée pour le saut.
    /// </summary>
    private void Jump()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
    }
    
    /// <summary>
    /// Gère les collisions avec l'environnement, notamment les surfaces rebondissantes (SurfaceTypeSO).
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        ClimbableSurface surface = collision.collider.GetComponent<ClimbableSurface>();
    
        if (surface != null && surface.SurfaceType != null && surface.SurfaceType.hasBounce)
        {
            // Récupération de la normale du point d'impact exact
            Vector3 contactNormal = collision.contacts[0].normal;

            // Annulation de la vélocité pour repartir proprement sur le rebond
            rb.linearVelocity = Vector3.zero;

            // --- PHYSIQUE : Application d'une impulsion de rebond orientée par la normale ---
            rb.AddForce(contactNormal * surface.SurfaceType.bounceForce, ForceMode.Impulse);

            // Verrouillage temporaire du mouvement au sol pour laisser l'impulsion s'exprimer
            bounceTimer = 0.25f;
        }
    }
}