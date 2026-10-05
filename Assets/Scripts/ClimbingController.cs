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
    [SerializeField] private float maxReachDistance = 10f;
    [SerializeField] private float pullSpeed = 6f;
    [SerializeField] private float hangDistance = 1.2f;

    [Header("Mouvement en l'air (Balance & Hissage)")]
    [SerializeField] private float swingForce = 15f; // Force de balancier (gauche/droite/avant/arrière)
    [SerializeField] private float heaveForce = 10f; // Force pour se hisser vers le haut (saut/espace)
    [SerializeField] private float maxSwingVelocity = 8f; // Limite la vitesse de balancement

    [Header("Positions au repos (relatives à la caméra)")]
    [SerializeField] private Vector3 leftHandIdleOffset = new Vector3(-0.4f, -0.3f, 0.8f);
    [SerializeField] private Vector3 rightHandIdleOffset = new Vector3(0.4f, -0.3f, 0.8f);

    private bool isLeftGripping;
    private bool isRightGripping;
    private Vector3 leftGrabPoint;
    private Vector3 rightGrabPoint;

    private void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
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
        if (Input.GetMouseButtonDown(mouseButton))
        {
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxReachDistance, climbableLayer))
            {
                isGripping = true;
                grabPoint = hit.point;
                // Optionnel : Réinitialiser un peu la vélocité à l'accroche pour éviter un effet "élastique" trop violent
                playerRb.linearVelocity *= 0.5f;
            }
        }

        if (Input.GetMouseButtonUp(mouseButton))
        {
            isGripping = false;
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
        playerRb.useGravity = !isClimbing;

        if (!isClimbing) return;

        // 1. Force d'attraction vers la/les prise(s) (la base de ton script précédent)
        Vector3 totalPull = Vector3.zero;
        int activeGrips = 0;
        Vector3 centerOfGrip = Vector3.zero;

        if (isLeftGripping)
        {
            totalPull += CalculatePullDirection(leftGrabPoint);
            centerOfGrip += leftGrabPoint;
            activeGrips++;
        }

        if (isRightGripping)
        {
            totalPull += CalculatePullDirection(rightGrabPoint);
            centerOfGrip += rightGrabPoint;
            activeGrips++;
        }

        Vector3 baseVelocity = Vector3.zero;
        if (activeGrips > 0)
        {
            baseVelocity = (totalPull / activeGrips) * pullSpeed;
            centerOfGrip /= activeGrips;
        }

        // 2. Ajout du balancement (Swing) et du hissage (Heave) commandé par le joueur
        Vector3 playerInputVelocity = HandleMidAirMovement(centerOfGrip);

        // On combine la force de traction naturelle vers la prise + le mouvement du joueur
        Vector3 finalVelocity = baseVelocity + playerInputVelocity;

        // On applique la vélocité, en s'assurant que le joueur ne parte pas à une vitesse infinie
        playerRb.linearVelocity = Vector3.ClampMagnitude(finalVelocity, pullSpeed + maxSwingVelocity);
    }

    private Vector3 CalculatePullDirection(Vector3 grabPoint)
    {
        Vector3 targetPlayerPos = grabPoint - Vector3.up * hangDistance;
        Vector3 direction = targetPlayerPos - transform.position;
        return Vector3.ClampMagnitude(direction, 1f);
    }

    // Nouvelle fonction pour gérer les mouvements quand on est accroché
    private Vector3 HandleMidAirMovement(Vector3 gripCenter)
    {
        Vector3 inputMovement = Vector3.zero;

        // Récupérer les inputs ZQSD/WASD
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // Calculer la direction du balancier par rapport à la caméra
        // On annule la composante Y (haut/bas) de la caméra pour ne se balancer que sur le plan horizontal
        Vector3 camForward = mainCamera.transform.forward;
        camForward.y = 0;
        camForward.Normalize();
        Vector3 camRight = mainCamera.transform.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 swingDirection = (camRight * moveX + camForward * moveZ).normalized;
        inputMovement += swingDirection * swingForce;

        // Se hisser / S'éjecter vers le haut (Touche Saut/Espace)
        if (Input.GetButton("Jump"))
        {
            // On ajoute une force vers le haut, mais aussi un peu vers l'avant (vers la prise)
            // pour aider à passer par-dessus un rebord.
            Vector3 heaveDir = Vector3.up + (gripCenter - transform.position).normalized * 0.5f;
            inputMovement += heaveDir.normalized * heaveForce;
        }

        return inputMovement;
    }
}