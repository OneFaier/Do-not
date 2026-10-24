using UnityEngine;

[RequireComponent(typeof(HandStamina))]
public class OmarPlayer : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask climbableLayer;
    [SerializeField] private HandStamina handStamina;

    [Header("Réglages Escalade")]
    [SerializeField] private float grabRadius = 0.25f; // Rayon de la zone de grab (taille du cylindre)
    [SerializeField] private float maxReachDistance = 15f;
    [SerializeField] private float pullSpeed = 8f;
    [SerializeField] private float minRopeLength = 0.5f;
    [SerializeField] private float maxRopeLength = 10f;

    [Header("Cooldowns")]
    [SerializeField] private float grabCooldown = 0.15f;
    [SerializeField] private float leftHandCooldown = 0.4f;
    [SerializeField] private float rightHandCooldown = 0.4f;

    [Header("Mouvement en l'air")]
    [SerializeField] private float swingForce = 25f;
    [SerializeField] private float vaultForce = 8f;

    [Header("Positions au repos")]
    [SerializeField] private Vector3 leftHandIdleOffset =
        new Vector3(-0.4f, -0.3f, 0.8f);

    [SerializeField] private Vector3 rightHandIdleOffset =
        new Vector3(0.4f, -0.3f, 0.8f);

    private bool isLeftGripping;
    private bool isRightGripping;

    private Vector3 leftGrabPoint;
    private Vector3 rightGrabPoint;

    private float currentRopeLength;

    private float grabTimer;
    private float leftCooldownTimer;
    private float rightCooldownTimer;

    private void Start()
    {
        if (!mainCamera)
            mainCamera = Camera.main;

        if (!playerRb)
            playerRb = GetComponent<Rigidbody>();

        if (!handStamina)
            handStamina = GetComponent<HandStamina>();

        // Pour les objets qui possédaient déjà OmarPlayer
        // avant l'ajout du RequireComponent.
        if (!handStamina)
            handStamina = gameObject.AddComponent<HandStamina>();
    }

    private void Update()
    {
        // Mise à jour des cooldowns.
        if (grabTimer > 0f)
            grabTimer -= Time.deltaTime;

        if (leftCooldownTimer > 0f)
            leftCooldownTimer -= Time.deltaTime;

        if (rightCooldownTimer > 0f)
            rightCooldownTimer -= Time.deltaTime;

        // Consommation et récupération des deux mains.
        handStamina.Tick(
            isLeftGripping,
            isRightGripping,
            Time.deltaTime);

        // Une main épuisée lâche automatiquement.
        if (isLeftGripping && handStamina.IsEmpty(0))
            ReleaseHand(0);

        if (isRightGripping && handStamina.IsEmpty(1))
            ReleaseHand(1);

        HandleGrabInput(0, ref isLeftGripping, ref leftGrabPoint);
        HandleGrabInput(1, ref isRightGripping, ref rightGrabPoint);

        HandleVaultInput();

        UpdateHandPositions();
    }

    private void FixedUpdate()
    {
        HandleClimbingPhysics();
    }

    private void HandleGrabInput(
        int mouseButton,
        ref bool isGripping,
        ref Vector3 grabPoint)
    {
        float specificCooldown = mouseButton == 0
            ? leftCooldownTimer
            : rightCooldownTimer;

        if (Input.GetMouseButtonDown(mouseButton)
            && grabTimer <= 0f
            && specificCooldown <= 0f
            && handStamina.CanGrab(mouseButton))
        {
            Ray ray = mainCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxReachDistance,
                climbableLayer))
            {
                isGripping = true;
                grabPoint = hit.point;

                float distToPoint = Vector3.Distance(
                    transform.position,
                    grabPoint);

                // Si l'autre main tient déjà, on fait une moyenne.
                if ((mouseButton == 0 && isRightGripping)
                    || (mouseButton == 1 && isLeftGripping))
                {
                    currentRopeLength =
                        (currentRopeLength + distToPoint) / 2f;
                }
                else
                {
                    currentRopeLength = distToPoint;
                }

                currentRopeLength = Mathf.Clamp(
                    currentRopeLength,
                    minRopeLength,
                    maxRopeLength);

                // Adoucit la vitesse pour encaisser le choc.
                playerRb.linearVelocity *= 0.3f;

                grabTimer = grabCooldown;
            }
        }

        // Lâcher manuel.
        if (Input.GetMouseButtonUp(mouseButton) && isGripping)
        {
            ReleaseHand(mouseButton);
        }
    }

    private void ReleaseHand(int mouseButton)
    {
        if (mouseButton == 0)
        {
            if (!isLeftGripping) return;

            isLeftGripping = false;
            leftCooldownTimer = leftHandCooldown;
        }
        else
        {
            if (!isRightGripping) return;

            isRightGripping = false;
            rightCooldownTimer = rightHandCooldown;
        }

        handStamina.NotifyReleased(mouseButton);
    }

    private void HandleVaultInput()
    {
        // Espace déclenche le vault.
        if (!Input.GetButtonDown("Jump"))
            return;

        // Au moins une main doit être accrochée.
        if (!isLeftGripping && !isRightGripping)
            return;

        // Consomme de la stamina sur les deux mains.
        handStamina.ConsumeVaultStamina();

        // Lâche automatiquement les prises.
        ReleaseHand(0);
        ReleaseHand(1);

        // Même direction et même puissance d'éjection qu'avant.
        Vector3 vaultDirection =
            (Vector3.up * 1.5f + mainCamera.transform.forward).normalized;

        playerRb.linearVelocity = Vector3.zero;

        playerRb.AddForce(
            vaultDirection * vaultForce,
            ForceMode.Impulse);
    }

    private void UpdateHandPositions()
    {
        Vector3 defaultLeft =
            mainCamera.transform.TransformPoint(leftHandIdleOffset);

        Vector3 defaultRight =
            mainCamera.transform.TransformPoint(rightHandIdleOffset);

        leftHand.position = isLeftGripping
            ? leftGrabPoint
            : defaultLeft;

        rightHand.position = isRightGripping
            ? rightGrabPoint
            : defaultRight;
    }

    private void HandleClimbingPhysics()
    {
        bool isClimbing = isLeftGripping || isRightGripping;

        playerRb.useGravity = !isClimbing;

        if (!isClimbing) return;

        Vector3 centerOfGrip = Vector3.zero;
        int activeGrips = 0;

        if (isLeftGripping)
        {
            centerOfGrip += leftGrabPoint;
            activeGrips++;
        }

        if (isRightGripping)
        {
            centerOfGrip += rightGrabPoint;
            activeGrips++;
        }

        centerOfGrip /= activeGrips;

        float currentDist = Vector3.Distance(
            transform.position,
            centerOfGrip);

        Vector3 dirToGrip =
            (centerOfGrip - transform.position).normalized;

        Vector3 currentVel = playerRb.linearVelocity;

        // 1. Gravité simulée.
        currentVel += Physics.gravity * Time.fixedDeltaTime;

        // 2. Hissage en maintenant Shift gauche.
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentRopeLength -= pullSpeed * Time.fixedDeltaTime;

            if (currentDist <= minRopeLength + 0.5f)
            {
                currentVel.y +=
                    pullSpeed * 2f * Time.fixedDeltaTime;
            }
        }

        currentRopeLength = Mathf.Clamp(
            currentRopeLength,
            minRopeLength,
            maxRopeLength);

        // 3. Effet de corde tendue.
        if (currentDist > currentRopeLength)
        {
            float stretch = currentDist - currentRopeLength;

            currentVel +=
                dirToGrip * (stretch * 50f * Time.fixedDeltaTime);

            float outwardSpeed = Vector3.Dot(
                currentVel,
                -dirToGrip);

            if (outwardSpeed > 0)
            {
                currentVel += dirToGrip * outwardSpeed;
            }
        }

        // 4. Balancement avec ZQSD.
        Vector3 camForward = mainCamera.transform.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = mainCamera.transform.right;
        camRight.y = 0;
        camRight.Normalize();

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 swingDir =
            (camRight * moveX + camForward * moveZ).normalized;

        currentVel +=
            swingDir * (swingForce * Time.fixedDeltaTime);

        // Léger frein global.
        currentVel *= 0.98f;

        playerRb.linearVelocity = currentVel;
    }

    // Détection utilisée par le crosshair.
    public bool HasGrabbableTarget()
    {
        if (!mainCamera) return false;

        Ray ray = mainCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        return Physics.Raycast(
            ray,
            maxReachDistance,
            climbableLayer);
    }
}