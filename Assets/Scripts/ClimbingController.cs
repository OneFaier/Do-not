using UnityEngine;

public class ClimbingController : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Rigidbody playerRb;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask climbableLayer;

    [Header("Réglages Escalade")]
    [SerializeField] private float maxReachDistance = 15f;
    [SerializeField] private float pullSpeed = 8f; // Vitesse de hissage
    [SerializeField] private float minRopeLength = 0.5f; // Distance minimale entre le corps et la main

    [Header("Mouvement en l'air")]
    [SerializeField] private float swingForce = 25f; // Force de balancier ZQSD
    [SerializeField] private float vaultForce = 8f; // Puissance de l'éjection au-dessus du mur

    [Header("Positions au repos")]
    [SerializeField] private Vector3 leftHandIdleOffset = new Vector3(-0.4f, -0.3f, 0.8f);
    [SerializeField] private Vector3 rightHandIdleOffset = new Vector3(0.4f, -0.3f, 0.8f);

    private bool isLeftGripping;
    private bool isRightGripping;
    private Vector3 leftGrabPoint;
    private Vector3 rightGrabPoint;

    // La longueur actuelle de ton bras/corde
    private float currentRopeLength;

    private void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!playerRb) playerRb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        HandleGrabInput(0, ref isLeftGripping, ref leftGrabPoint);
        HandleGrabInput(1, ref isRightGripping, ref rightGrabPoint);

        UpdateHandPositions();
    }

    private void FixedUpdate()
    {
        HandleClimbingPhysics();
    }

    private void HandleGrabInput(int mouseButton, ref bool isGripping, ref Vector3 grabPoint)
    {
        // QUAND ON S'ACCROCHE
        if (Input.GetMouseButtonDown(mouseButton))
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxReachDistance, climbableLayer))
            {
                isGripping = true;
                grabPoint = hit.point;

                float distToPoint = Vector3.Distance(transform.position, grabPoint);

                // Si on tenait déjà avec l'autre main, on fait une moyenne
                if ((mouseButton == 0 && isRightGripping) || (mouseButton == 1 && isLeftGripping))
                    currentRopeLength = (currentRopeLength + distToPoint) / 2f;
                else
                    currentRopeLength = distToPoint;

                // On adoucit la vitesse pour encaisser le choc de l'accroche
                playerRb.linearVelocity *= 0.3f;
            }
        }

        // QUAND ON LÂCHE LA PRISE
        if (Input.GetMouseButtonUp(mouseButton))
        {
            isGripping = false;

            // Si on lâche TOUTES les mains tout en se hissant (Espace), on s'éjecte par-dessus
            if (!isLeftGripping && !isRightGripping && Input.GetButton("Jump"))
            {
                // Calcule une trajectoire vers le haut et un peu vers l'avant
                Vector3 vaultDirection = (Vector3.up * 1.5f + mainCamera.transform.forward).normalized;

                // Reset la vitesse pour éviter que le balancier n'abîme le saut
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

    private void HandleClimbingPhysics()
    {
        bool isClimbing = isLeftGripping || isRightGripping;

        // On simule nous-même la gravité pendant la grimpe pour plus de contrôle
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

        // 1. GRAVITÉ SIMULÉE (pour balancer naturellement)
        currentVel += Physics.gravity * Time.fixedDeltaTime;

        // 2. SE HISSER (Raccourcir la corde)
        if (Input.GetButton("Jump"))
        {
            currentRopeLength -= pullSpeed * Time.fixedDeltaTime;

            // Si on est déjà collé à la prise, on force le corps à MONTER le long du mur
            // C'est ce qui te permet de dépasser ta main sans rester coincé en dessous !
            if (currentDist <= minRopeLength + 0.5f)
            {
                currentVel.y += pullSpeed * 2f * Time.fixedDeltaTime;
            }
        }
        currentRopeLength = Mathf.Max(currentRopeLength, minRopeLength);

        // 3. EFFET DE CORDE TENDUE (Tether)
        if (currentDist > currentRopeLength)
        {
            // On tire le joueur vers la prise s'il dépasse la longueur de sa corde
            float stretch = currentDist - currentRopeLength;
            currentVel += dirToGrip * (stretch * 50f * Time.fixedDeltaTime);

            // On annule la vitesse qui nous éloignerait davantage de la prise
            float outwardSpeed = Vector3.Dot(currentVel, -dirToGrip);
            if (outwardSpeed > 0)
            {
                currentVel += dirToGrip * outwardSpeed;
            }
        }

        // 4. BALANCEMENT (WASD / ZQSD)
        Vector3 camForward = mainCamera.transform.forward; camForward.y = 0; camForward.Normalize();
        Vector3 camRight = mainCamera.transform.right; camRight.y = 0; camRight.Normalize();

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 swingDir = (camRight * moveX + camForward * moveZ).normalized;

        currentVel += swingDir * (swingForce * Time.fixedDeltaTime);

        // Léger frein global pour éviter de partir dans tous les sens à l'infini
        currentVel *= 0.98f;

        // On applique les forces au joueur
        playerRb.linearVelocity = currentVel;
    }
}