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
    [SerializeField] private float grabCooldown = 0.15f; // Anti-spam global des clics

    [Header("Cooldowns mains")]
    [SerializeField] private float leftHandCooldown = 0.4f;  // Cooldown propre à la main gauche
    [SerializeField] private float rightHandCooldown = 0.4f; // Cooldown propre à la main droite
    private float leftCooldownTimer = 0f;
    private float rightCooldownTimer = 0f;

    [Header("Mouvement en l'air")]
    [SerializeField] private float swingForce = 25f;
    [SerializeField] private float vaultForce = 8f;
    [SerializeField] private float maxClimbSpeed = 15f; // Limite de vitesse anti-vol

    [Header("Positions au repos")]
    [SerializeField] private Vector3 leftHandIdleOffset = new Vector3(-0.4f, -0.3f, 0.8f);
    [SerializeField] private Vector3 rightHandIdleOffset = new Vector3(0.4f, -0.3f, 0.8f);

    private bool isLeftGripping;
    private bool isRightGripping;
    private Vector3 leftGrabPoint;
    private Vector3 rightGrabPoint;
    private float currentRopeLength;
    private float grabTimer = 0f; // Timer pour le cooldown global

    private void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!playerRb) playerRb = GetComponent<Rigidbody>();

        // Stockage des matériaux d'origine
        if (leftHandRenderer) leftHandOriginalMaterial = leftHandRenderer.material;
        if (rightHandRenderer) rightHandOriginalMaterial = rightHandRenderer.material;
    }

    private void Update()
    {
        // Gestion du cooldown global
        if (grabTimer > 0f) grabTimer -= Time.deltaTime;

        // Gestion des cooldowns individuels des mains
        if (leftCooldownTimer > 0f) leftCooldownTimer -= Time.deltaTime;
        if (rightCooldownTimer > 0f) rightCooldownTimer -= Time.deltaTime;

        HandleGrabInput(0, ref isLeftGripping, ref leftGrabPoint);
        HandleGrabInput(1, ref isRightGripping, ref rightGrabPoint);

        UpdateHandPositions();
        UpdateHandVisuals();
    }

    private void FixedUpdate()
    {
        HandleClimbingPhysics();
    }

    private void HandleGrabInput(int mouseButton, ref bool isGripping, ref Vector3 grabPoint)
    {
        // Sélectionne le timer de cooldown correspondant à la main active
        float specificCooldown = (mouseButton == 0) ? leftCooldownTimer : rightCooldownTimer;

        // QUAND ON S'ACCROCHE (Sécurisé par le cooldown global et le cooldown de la main)
        if (Input.GetMouseButtonDown(mouseButton) && grabTimer <= 0f && specificCooldown <= 0f)
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxReachDistance, climbableLayer))
            {
                isGripping = true;
                grabPoint = hit.point;

                float distToPoint = Vector3.Distance(transform.position, grabPoint);

                if ((mouseButton == 0 && isRightGripping) || (mouseButton == 1 && isLeftGripping))
                    currentRopeLength = (currentRopeLength + distToPoint) / 2f;
                else
                    currentRopeLength = distToPoint;

                currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

                playerRb.linearVelocity *= 0.3f;
                grabTimer = grabCooldown; // Active le délai anti-spam global
            }
        }

        // QUAND ON LÂCHE LA PRISE
        if (Input.GetMouseButtonUp(mouseButton) && isGripping)
        {
            bool wasClimbing = isLeftGripping || isRightGripping;
            isGripping = false;

            // Déclenche le cooldown spécifique à cette main avec sa valeur dédiée
            if (mouseButton == 0) leftCooldownTimer = leftHandCooldown;
            else if (mouseButton == 1) rightCooldownTimer = rightHandCooldown;

            if (wasClimbing && !isLeftGripping && !isRightGripping && Input.GetButton("Jump"))
            {
                Vector3 vaultDirection = (Vector3.up * 1.5f + mainCamera.transform.forward).normalized;
                playerRb.linearVelocity = Vector3.zero;
                playerRb.AddForce(vaultDirection * vaultForce, ForceMode.Impulse);
            }
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
            leftHandRenderer.material = (leftCooldownTimer > 0f) ? handCooldownMaterial : leftHandOriginalMaterial; //si le timer est >0f, alors mettre le material de cooldown, sinon mettre le material d'origine
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

        Vector3 centerOfGrip = Vector3.zero;
        int activeGrips = 0;

        if (isLeftGripping) { centerOfGrip += leftGrabPoint; activeGrips++; }
        if (isRightGripping) { centerOfGrip += rightGrabPoint; activeGrips++; }
        centerOfGrip /= activeGrips;

        float currentDist = Vector3.Distance(transform.position, centerOfGrip);
        Vector3 dirToGrip = (centerOfGrip - transform.position).normalized;

        Vector3 currentVel = playerRb.linearVelocity;

        // 1. Gravité simulée
        currentVel += Physics.gravity * Time.fixedDeltaTime;

        // 2. Se hisser
        if (Input.GetButton("Jump"))
        {
            currentRopeLength -= pullSpeed * Time.fixedDeltaTime;

            if (currentDist <= minRopeLength + 0.5f)
            {
                currentVel.y += pullSpeed * 2f * Time.fixedDeltaTime;
            }
        }
        currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

        // 3. Effet de corde tendue
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

        // 4. Balancement
        Vector3 camForward = mainCamera.transform.forward; camForward.y = 0; camForward.Normalize();
        Vector3 camRight = mainCamera.transform.right; camRight.y = 0; camRight.Normalize();

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 swingDir = (camRight * moveX + camForward * moveZ).normalized;

        currentVel += swingDir * (swingForce * Time.fixedDeltaTime);

        currentVel *= 0.98f;

        // SÉCURITÉ ANTI-VOL : On bride la vélocité maximale autorisée pendant l'escalade
        currentVel = Vector3.ClampMagnitude(currentVel, maxClimbSpeed);

        playerRb.linearVelocity = currentVel;
    }
}