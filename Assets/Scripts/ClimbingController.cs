using UnityEngine;

public class ClimbingController : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Référence au Rigidbody du joueur.")]
    [SerializeField] private Rigidbody playerRb;
    [Tooltip("Transform représentant la main gauche.")]
    [SerializeField] private Transform leftHand;
    [Tooltip("Transform représentant la main droite.")]
    [SerializeField] private Transform rightHand;
    [Tooltip("Caméra principale pour les rayons d'accroche.")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("Masque de calque définissant les surfaces grimpables.")]
    [SerializeField] private LayerMask climbableLayer;

    [Header("Visuels mains")]
    [SerializeField] private Renderer leftHandRenderer;
    [SerializeField] private Renderer rightHandRenderer;
    [SerializeField] private Material handCooldownMaterial;
    private Material leftHandOriginalMaterial;
    private Material rightHandOriginalMaterial;

    [Header("Réglages Escalade")]
    [Tooltip("Portée maximale des rayons pour attraper une surface.")]
    [SerializeField] private float maxReachDistance = 15f;
    [Tooltip("Vitesse de traction le long de la corde imaginaire.")]
    [SerializeField] private float pullSpeed = 8f;
    [Tooltip("Longueur minimale de la corde d'accroche.")]
    [SerializeField] private float minRopeLength = 0.5f;
    [Tooltip("Longueur maximale de la corde d'accroche.")]
    [SerializeField] private float maxRopeLength = 10f;
    [Tooltip("Délai de sécurité entre deux accroches.")]
    [SerializeField] private float grabCooldown = 0.15f;

    [Header("Cooldowns mains")]
    [SerializeField] private float leftHandCooldown = 0.4f;
    [SerializeField] private float rightHandCooldown = 0.4f;
    private float leftCooldownTimer = 0f;
    private float rightCooldownTimer = 0f;

    [Header("Mouvement en l'air & Élan")]
    [Tooltip("Force de propulsion latérale lors du balancement (swing).")]
    [SerializeField] private float swingForce = 25f;
    [Tooltip("Force d'éjection (vault) lors du lâcher avec saut.")]
    [SerializeField] private float vaultForce = 8f;
    [Tooltip("Direction relative (offset) personnalisable pour l'éjection du vault.")]
    [SerializeField] private Vector3 vaultDirectionOffset = new Vector3(0f, 0.5f, 1f); 
    [Tooltip("Vitesse maximale absolue en mode escalade/balancement.")]
    [SerializeField] private float maxClimbSpeed = 15f;

    [Header("Positions au repos")]
    [SerializeField] private Vector3 leftHandIdleOffset = new Vector3(-0.4f, -0.3f, 0.8f);
    [SerializeField] private Vector3 rightHandIdleOffset = new Vector3(0.4f, -0.3f, 0.8f);

    // États d'accroche et points d'impact
    private bool isLeftGripping;
    private bool isRightGripping;
    private Vector3 leftGrabPoint;
    private Vector3 rightGrabPoint;
    private Vector3 leftGrabNormal;
    private Vector3 rightGrabNormal;

    private ClimbableSurface leftSurface;
    private ClimbableSurface rightSurface;
    private Collider leftCollider;
    private Collider rightCollider;

    private float leftGrabDuration = 0f;
    private float rightGrabDuration = 0f;
    private float currentRopeLength;
    private float grabTimer = 0f;

    private void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!playerRb) playerRb = GetComponent<Rigidbody>();

        if (leftHandRenderer) leftHandOriginalMaterial = leftHandRenderer.material;
        if (rightHandRenderer) rightHandOriginalMaterial = rightHandRenderer.material;
    }

    private void Update()
    {
        // Décompte des différents timers de cooldowns
        if (grabTimer > 0f) grabTimer -= Time.deltaTime;
        if (leftCooldownTimer > 0f) leftCooldownTimer -= Time.deltaTime;
        if (rightCooldownTimer > 0f) rightCooldownTimer -= Time.deltaTime;

        // Gestion des inputs pour chaque main (0 = Main Gauche/Clic G, 1 = Main Droite/Clic D)
        HandleGrabInput(0, ref isLeftGripping, ref leftGrabPoint, ref leftGrabNormal, ref leftSurface, ref leftCollider, ref leftGrabDuration, leftHandCooldown);
        HandleGrabInput(1, ref isRightGripping, ref rightGrabPoint, ref rightGrabNormal, ref rightSurface, ref rightCollider, ref rightGrabDuration, rightHandCooldown);

        UpdateHandPositions();
        UpdateHandVisuals();
    }

    private void FixedUpdate()
    {
        HandleClimbingPhysics();
    }

    private SurfaceTypeSO GetSurfaceType(ClimbableSurface surface)
    {
        return surface != null ? surface.SurfaceType : null;
    }

    private bool CanClimbSurface(ClimbableSurface surface)
    {
        SurfaceTypeSO type = GetSurfaceType(surface);
        return type == null || type.canClimb;
    }

    /// <summary>
    /// Gère la logique d'accrochage, de glissement sur les surfaces et de lâcher pour une main spécifique.
    /// </summary>
    private void HandleGrabInput(int mouseButton, ref bool isGripping, ref Vector3 grabPoint, ref Vector3 grabNormal, ref ClimbableSurface handSurface, ref Collider handCollider, ref float grabDuration, float handCooldown)
    {
        float specificCooldown = (mouseButton == 0) ? leftCooldownTimer : rightCooldownTimer;

        // --- ACCROCHAGE ---
        if (Input.GetMouseButtonDown(mouseButton) && grabTimer <= 0f && specificCooldown <= 0f)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxReachDistance, climbableLayer))
            {
                ClimbableSurface surface = hit.collider.GetComponentInParent<ClimbableSurface>();

                if (CanClimbSurface(surface))
                {
                    isGripping = true;
                    grabPoint = hit.point;
                    grabNormal = hit.normal;
                    handSurface = surface;
                    handCollider = hit.collider;
                    grabDuration = 0f;

                    float distToPoint = Vector3.Distance(transform.position, grabPoint);

                    // Calcul de la longueur de corde initiale
                    if ((mouseButton == 0 && isRightGripping) || (mouseButton == 1 && isLeftGripping))
                        currentRopeLength = (currentRopeLength + distToPoint) / 2f;
                    else
                        currentRopeLength = distToPoint;

                    currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

                    // Amortissement de la vitesse à l'accroche pour éviter les secousses violentes
                    playerRb.linearVelocity *= 0.3f;
                    grabTimer = grabCooldown;
                }
            }
        }

        // --- GESTION DU GLISSEMENT SUR LES SURFACES GLISSANTES ---
        if (isGripping)
        {
            if (handCollider == null || !handCollider.enabled || !handCollider.gameObject.activeInHierarchy)
            {
                ForceReleaseHand(mouseButton, ref isGripping, ref handSurface, ref handCollider, handCooldown);
                return;
            }

            grabDuration += Time.deltaTime;

            if (handSurface != null)
            {
                SurfaceTypeSO type = GetSurfaceType(handSurface);

                if (type != null)
                {
                    if (!type.canClimb)
                    {
                        ForceReleaseHand(mouseButton, ref isGripping, ref handSurface, ref handCollider, handCooldown);
                        return;
                    }

                    if (type.isSlippery)
                    {
                        Vector3 nextGrabPoint = grabPoint;

                        if (grabNormal.y < -0.5f) // Cas d'un plafond
                        {
                            Vector3 dirTowardsPlayer = (transform.position - grabPoint);
                            dirTowardsPlayer.y = 0;
                            dirTowardsPlayer.Normalize();
                            if (dirTowardsPlayer == Vector3.zero) dirTowardsPlayer = transform.forward;

                            nextGrabPoint += dirTowardsPlayer * type.slideSpeed * Time.deltaTime;
                        }
                        else // Cas d'un mur vertical
                        {
                            nextGrabPoint += Vector3.down * type.slideSpeed * Time.deltaTime;
                        }

                        // Sécurité : Vérifie si le point de glissement reste sur le même collider exact
                        if (handCollider != null)
                        {
                            Vector3 rayOrigin = nextGrabPoint + (grabNormal * 0.3f);
                            if (handCollider.Raycast(new Ray(rayOrigin, -grabNormal), out RaycastHit slideHit, 0.6f))
                            {
                                grabPoint = slideHit.point;
                                grabNormal = slideHit.normal;
                            }
                        }
                    }

                    // Lâchage automatique si le temps maximum d'accroche est atteint
                    if (type.useMaxGrabTime && type.maxGrabTime > 0f && grabDuration >= type.maxGrabTime)
                    {
                        ForceReleaseHand(mouseButton, ref isGripping, ref handSurface, ref handCollider, handCooldown);
                        return;
                    }
                }
            }
        }

        // --- LÂCHAGE MANUEL ---
        if (Input.GetMouseButtonUp(mouseButton) && isGripping)
        {
            ForceReleaseHand(mouseButton, ref isGripping, ref handSurface, ref handCollider, handCooldown);
        }
    }

    /// <summary>
    /// Force le lâchage d'une main et applique l'impulsion cinétique de sortie (Vault ou Inertie de Swing).
    /// </summary>
    private void ForceReleaseHand(int mouseButton, ref bool isGripping, ref ClimbableSurface handSurface, ref Collider handCollider, float handCooldown)
    {
        bool wasClimbing = isLeftGripping || isRightGripping;
        isGripping = false;
        handSurface = null;
        handCollider = null;

        // Application du cooldown sur la main correspondante
        if (mouseButton == 0) leftCooldownTimer = handCooldown;
        else if (mouseButton == 1) rightCooldownTimer = handCooldown;

        // Si c'était la dernière main accrochée et qu'on lâche tout
        if (wasClimbing && !isLeftGripping && !isRightGripping)
        {
            // --- PHYSIQUE : Gestion de l'élan au lâcher ---
            if (Input.GetButton("Jump"))
            {
                // Impulsion directionnelle personnalisée (Vault) combinée à l'inertie
                Vector3 customDir = (mainCamera.transform.right * vaultDirectionOffset.x + 
                                     mainCamera.transform.up * vaultDirectionOffset.y + 
                                     mainCamera.transform.forward * vaultDirectionOffset.z).normalized;

                playerRb.AddForce(customDir * vaultForce, ForceMode.Impulse);
            }
            else
            {
                // On ne touche à rien : le Rigidbody conserve naturellement 100% de la vélocité 
                // cinétique et directionnelle accumulée pendant le balancement (swing).
            }
        }
    }

    /// <summary>
    /// Met à jour la position visuelle des mains du joueur (soit ancrées au point d'accroche, soit au repos).
    /// </summary>
    private void UpdateHandPositions()
    {
        Vector3 defaultLeft = mainCamera.transform.TransformPoint(leftHandIdleOffset);
        Vector3 defaultRight = mainCamera.transform.TransformPoint(rightHandIdleOffset);

        leftHand.position = isLeftGripping ? leftGrabPoint : defaultLeft;
        rightHand.position = isRightGripping ? rightGrabPoint : defaultRight;
    }

    /// <summary>
    /// Met à jour les textures visuelles des mains en fonction des cooldowns actifs.
    /// </summary>
    private void UpdateHandVisuals()
    {
        if (leftHandRenderer && handCooldownMaterial)
        {
            leftHandRenderer.material = (leftCooldownTimer > 0f) ? handCooldownMaterial : leftHandOriginalMaterial;
        }

        if (rightHandRenderer && handCooldownMaterial)
        {
            rightHandRenderer.material = (rightCooldownTimer > 0f) ? handCooldownMaterial : rightHandOriginalMaterial;
        }
    }

    /// <summary>
    /// Moteur physique de l'escalade : gestion de la corde élastique, de la gravité désactivée et du balancement.
    /// </summary>
    private void HandleClimbingPhysics()
    {
        bool isClimbing = isLeftGripping || isRightGripping;

        // Désactivation de la gravité globale du Rigidbody tant qu'on est accroché
        playerRb.useGravity = !isClimbing;

        if (!isClimbing) return;

        // Calcul du multiplicateur global de prise selon les surfaces touchées
        float gripMultiplier = 1f;
        int activeSurfacesCount = 0;
        if (isLeftGripping && leftSurface != null && leftSurface.SurfaceType != null)
        {
            gripMultiplier += leftSurface.SurfaceType.gripMultiplier;
            activeSurfacesCount++;
        }
        if (isRightGripping && rightSurface != null && rightSurface.SurfaceType != null)
        {
            gripMultiplier += rightSurface.SurfaceType.gripMultiplier;
            activeSurfacesCount++;
        }
        if (activeSurfacesCount > 0) gripMultiplier /= activeSurfacesCount;

        // Calcul du centre géométrique de la prise (si une ou deux mains sont accrochées)
        Vector3 centerOfGrip = Vector3.zero;
        int activeGrips = 0;

        if (isLeftGripping) { centerOfGrip += leftGrabPoint; activeGrips++; }
        if (isRightGripping) { centerOfGrip += rightGrabPoint; activeGrips++; }
        centerOfGrip /= activeGrips;

        float currentDist = Vector3.Distance(transform.position, centerOfGrip);
        Vector3 dirToGrip = (centerOfGrip - transform.position).normalized;

        Vector3 currentVel = playerRb.linearVelocity;

        // Simulation d'une micro-gravité interne pour les mains
        currentVel += Physics.gravity * Time.fixedDeltaTime;

        // Raccourcissement de la corde si le joueur maintient la touche de saut (traction)
        if (Input.GetButton("Jump"))
        {
            currentRopeLength -= (pullSpeed * gripMultiplier) * Time.fixedDeltaTime;

            if (currentDist <= minRopeLength + 0.5f)
            {
                currentVel.y += pullSpeed * 2f * Time.fixedDeltaTime;
            }
        }
        currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

        // --- CALCUL PHYSIQUE IMPORTANT : Contrainte de tension de corde (Pendule/Spring) ---
        if (currentDist > currentRopeLength)
        {
            float stretch = currentDist - currentRopeLength;
            currentVel += dirToGrip * (stretch * 50f * Time.fixedDeltaTime);

            float outwardSpeed = Vector3.Dot(currentVel, -dirToGrip);
            if (outwardSpeed > 0)
            {
                currentVel += dirToGrip * outwardSpeed;
            }
        }

        Vector3 camForward = mainCamera.transform.forward; camForward.y = 0; camForward.Normalize();
        Vector3 camRight = mainCamera.transform.right; camRight.y = 0; camRight.Normalize();

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 swingDir = (camRight * moveX + camForward * moveZ).normalized;

        // --- CALCUL PHYSIQUE : Application de la force de balancement (Swing) ---
        currentVel += swingDir * (swingForce * Time.fixedDeltaTime);
        currentVel *= 0.98f; // Frottement d'air amortissant légèrement le mouvement

        // Limitation stricte de la vitesse maximale en escalade
        currentVel = Vector3.ClampMagnitude(currentVel, maxClimbSpeed);
        playerRb.linearVelocity = currentVel;
    }

    /// <summary>
    /// Vérifie si un élément grimpable est dans le champ de vision et à portée.
    /// Utilisé notamment par le crosshair pour changer de couleur.
    /// </summary>
    public bool HasGrabbableTarget()
    {
        if (!mainCamera)
            return false;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, maxReachDistance, climbableLayer))
        {
            return false;
        }

        ClimbableSurface surface = hit.collider.GetComponentInParent<ClimbableSurface>();

        return CanClimbSurface(surface);
    }
}