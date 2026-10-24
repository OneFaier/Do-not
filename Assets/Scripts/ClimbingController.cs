using UnityEngine;

public class ClimbingController : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask climbableLayer;

    [Header("Visuels mains")]
    [SerializeField] private Renderer leftHandRenderer;
    [SerializeField] private Renderer rightHandRenderer;
    [SerializeField] private Material handCooldownMaterial;
    private Material leftHandOriginalMaterial;
    private Material rightHandOriginalMaterial;

    [Header("Réglages Escalade")]
    [SerializeField] private float maxReachDistance = 15f;
    [SerializeField] private float pullSpeed = 8f;
    [SerializeField] private float minRopeLength = 0.5f;
    [SerializeField] private float maxRopeLength = 10f;
    [SerializeField] private float grabCooldown = 0.15f;

    [Header("Cooldowns mains")]
    [SerializeField] private float leftHandCooldown = 0.4f;
    [SerializeField] private float rightHandCooldown = 0.4f;
    private float leftCooldownTimer = 0f;
    private float rightCooldownTimer = 0f;

    [Header("Mouvement en l'air")]
    [SerializeField] private float swingForce = 25f;
    [SerializeField] private float vaultForce = 8f;
    [SerializeField] private float maxClimbSpeed = 15f;

    [Header("Positions au repos")]
    [SerializeField] private Vector3 leftHandIdleOffset = new Vector3(-0.4f, -0.3f, 0.8f);
    [SerializeField] private Vector3 rightHandIdleOffset = new Vector3(0.4f, -0.3f, 0.8f);

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
        if (grabTimer > 0f) grabTimer -= Time.deltaTime;
        if (leftCooldownTimer > 0f) leftCooldownTimer -= Time.deltaTime;
        if (rightCooldownTimer > 0f) rightCooldownTimer -= Time.deltaTime;

        HandleGrabInput(0, ref isLeftGripping, ref leftGrabPoint, ref leftGrabNormal, ref leftSurface, ref leftCollider, ref leftGrabDuration, leftHandCooldown);
        HandleGrabInput(1, ref isRightGripping, ref rightGrabPoint, ref rightGrabNormal, ref rightSurface, ref rightCollider, ref rightGrabDuration, rightHandCooldown);

        UpdateHandPositions();
        UpdateHandVisuals();
    }

    private void FixedUpdate()
    {
        HandleClimbingPhysics();
    }

    private void HandleGrabInput(int mouseButton, ref bool isGripping, ref Vector3 grabPoint, ref Vector3 grabNormal, ref ClimbableSurface handSurface, ref Collider handCollider, ref float grabDuration, float handCooldown)
    {
        float specificCooldown = (mouseButton == 0) ? leftCooldownTimer : rightCooldownTimer;

        // ACCROCHAGE
        if (Input.GetMouseButtonDown(mouseButton) && grabTimer <= 0f && specificCooldown <= 0f)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxReachDistance, climbableLayer))
            {
                ClimbableSurface surface = hit.collider.GetComponent<ClimbableSurface>();

                if (surface == null || surface.SurfaceType.canClimb)
                {
                    isGripping = true;
                    grabPoint = hit.point;
                    grabNormal = hit.normal;
                    handSurface = surface;
                    handCollider = hit.collider;
                    grabDuration = 0f;

                    float distToPoint = Vector3.Distance(transform.position, grabPoint);

                    if ((mouseButton == 0 && isRightGripping) || (mouseButton == 1 && isLeftGripping))
                        currentRopeLength = (currentRopeLength + distToPoint) / 2f;
                    else
                        currentRopeLength = distToPoint;

                    currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

                    playerRb.linearVelocity *= 0.3f;
                    grabTimer = grabCooldown;
                }
            }
        }

        // GESTION DU GLISSEMENT ET VERROUILLAGE AUX BORDS
        if (isGripping)
        {
            grabDuration += Time.deltaTime;

            if (handSurface != null && handSurface.SurfaceType != null)
            {
                SurfaceTypeSO type = handSurface.SurfaceType;

                // 1. Glissement avec blocage strict aux limites de l'objet
                if (type.isSlippery)
                {
                    Vector3 nextGrabPoint = grabPoint;

                    if (grabNormal.y < -0.5f) // Plafond
                    {
                        Vector3 dirTowardsPlayer = (transform.position - grabPoint);
                        dirTowardsPlayer.y = 0;
                        dirTowardsPlayer.Normalize();
                        if (dirTowardsPlayer == Vector3.zero) dirTowardsPlayer = transform.forward;

                        nextGrabPoint += dirTowardsPlayer * type.slideSpeed * Time.deltaTime;
                    }
                    else // Mur vertical
                    {
                        nextGrabPoint += Vector3.down * type.slideSpeed * Time.deltaTime;
                    }

                    // SÉCURITÉ LIMITES : On vérifie si le prochain point est toujours sur le même collider exact
                    if (handCollider != null)
                    {
                        Vector3 rayOrigin = nextGrabPoint + (grabNormal * 0.3f);
                        if (Physics.Raycast(rayOrigin, -grabNormal, out RaycastHit slideHit, 0.6f, climbableLayer))
                        {
                            // Si on reste sur le même objet, on applique le mouvement de glissement
                            if (slideHit.collider == handCollider)
                            {
                                grabPoint = slideHit.point;
                                grabNormal = slideHit.normal;
                            }
                            // Sinon (on atteint le bord) : on ne fait rien, la main se bloque net au bord et s'arrête de glisser !
                        }
                        // Si le raycast rate (on sort du collider), on bloque la main sur place sans la faire décrocher
                    }
                }

                // 2. Lâchage automatique si le temps max est atteint (si activé)
                if (type.useMaxGrabTime && type.maxGrabTime > 0f && grabDuration >= type.maxGrabTime)
                {
                    ForceReleaseHand(mouseButton, ref isGripping, ref handSurface, ref handCollider, handCooldown);
                    return;
                }
            }
        }

        // LÂCHAGE MANUEL
        if (Input.GetMouseButtonUp(mouseButton) && isGripping)
        {
            ForceReleaseHand(mouseButton, ref isGripping, ref handSurface, ref handCollider, handCooldown);
        }
    }

    private void ForceReleaseHand(int mouseButton, ref bool isGripping, ref ClimbableSurface handSurface, ref Collider handCollider, float handCooldown)
    {
        bool wasClimbing = isLeftGripping || isRightGripping;
        isGripping = false;
        handSurface = null;
        handCollider = null;

        if (mouseButton == 0) leftCooldownTimer = handCooldown;
        else if (mouseButton == 1) rightCooldownTimer = handCooldown;

        if (wasClimbing && !isLeftGripping && !isRightGripping && Input.GetButton("Jump"))
        {
            Vector3 vaultDirection = (Vector3.up * 1.5f + mainCamera.transform.forward).normalized;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.AddForce(vaultDirection * vaultForce, ForceMode.Impulse);
        }
    }

    private void UpdateHandPositions()
    {
        Vector3 defaultLeft = mainCamera.transform.TransformPoint(leftHandIdleOffset);
        Vector3 defaultRight = mainCamera.transform.TransformPoint(rightHandIdleOffset);

        leftHand.position = isLeftGripping ? leftGrabPoint : defaultLeft;
        rightHand.position = isRightGripping ? rightGrabPoint : defaultRight;
    }

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

    private void HandleClimbingPhysics()
    {
        bool isClimbing = isLeftGripping || isRightGripping;

        playerRb.useGravity = !isClimbing;

        if (!isClimbing) return;

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

        Vector3 centerOfGrip = Vector3.zero;
        int activeGrips = 0;

        if (isLeftGripping) { centerOfGrip += leftGrabPoint; activeGrips++; }
        if (isRightGripping) { centerOfGrip += rightGrabPoint; activeGrips++; }
        centerOfGrip /= activeGrips;

        float currentDist = Vector3.Distance(transform.position, centerOfGrip);
        Vector3 dirToGrip = (centerOfGrip - transform.position).normalized;

        Vector3 currentVel = playerRb.linearVelocity;

        currentVel += Physics.gravity * Time.fixedDeltaTime;

        if (Input.GetButton("Jump"))
        {
            currentRopeLength -= (pullSpeed * gripMultiplier) * Time.fixedDeltaTime;

            if (currentDist <= minRopeLength + 0.5f)
            {
                currentVel.y += pullSpeed * 2f * Time.fixedDeltaTime;
            }
        }
        currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

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

        currentVel += swingDir * (swingForce * Time.fixedDeltaTime);
        currentVel *= 0.98f;

        currentVel = Vector3.ClampMagnitude(currentVel, maxClimbSpeed);
        playerRb.linearVelocity = currentVel;
    }
}