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
    [SerializeField] private float maxReachDistance = 15f;
    [SerializeField] private float grabSnapRadius = 0.35f;
    [SerializeField] private float pullSpeed = 8f;
    [SerializeField] private float maxClimbSpeed = 15f;
    [SerializeField] private float minRopeLength = 0.5f;
    [SerializeField] private float maxRopeLength = 10f;

    [Header("Cooldowns")]
    [SerializeField] private float grabCooldown = 0.15f;
    [SerializeField] private float leftHandCooldown = 0.4f;
    [SerializeField] private float rightHandCooldown = 0.4f;

    [Header("Balancement")]
    [SerializeField] private float swingForce = 25f;

    [Header("Vault")]
    [SerializeField] private float vaultForce = 8f;

    [Tooltip("0 = horizontal, 45 = diagonal, 90 = vertical.")]
    [Range(0f, 90f)]
    [SerializeField] private float vaultAngle = 35f;

    [Header("Positions au repos")]
    [SerializeField] private Vector3 leftHandIdleOffset = new Vector3(-0.4f, -0.3f, 0.8f);
    [SerializeField] private Vector3 rightHandIdleOffset = new Vector3(0.4f, -0.3f, 0.8f);

    private OmarMovement movement;

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

    private float leftGrabDuration;
    private float rightGrabDuration;

    private float currentRopeLength;

    private float grabTimer;
    private float leftCooldownTimer;
    private float rightCooldownTimer;

    public bool IsClimbing => isLeftGripping || isRightGripping;

    private void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!playerRb) playerRb = GetComponent<Rigidbody>();
        if (!handStamina) handStamina = GetComponent<HandStamina>();
        if (!handStamina) handStamina = gameObject.AddComponent<HandStamina>();

        movement = GetComponent<OmarMovement>();
    }

    private void Update()
    {
        if (grabTimer > 0f) grabTimer -= Time.deltaTime;
        if (leftCooldownTimer > 0f) leftCooldownTimer -= Time.deltaTime;
        if (rightCooldownTimer > 0f) rightCooldownTimer -= Time.deltaTime;

        handStamina.Tick(isLeftGripping, isRightGripping, Time.deltaTime);

        if (isLeftGripping && handStamina.IsEmpty(0)) ReleaseHand(0);
        if (isRightGripping && handStamina.IsEmpty(1)) ReleaseHand(1);

        HandleGrabInput(0, ref isLeftGripping, ref leftGrabPoint, ref leftGrabNormal, ref leftSurface, ref leftCollider, ref leftGrabDuration);
        HandleGrabInput(1, ref isRightGripping, ref rightGrabPoint, ref rightGrabNormal, ref rightSurface, ref rightCollider, ref rightGrabDuration);

        HandleVaultInput();
        UpdateHandPositions();
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

    private bool TryFindGrabTarget(out RaycastHit hit, out ClimbableSurface surface)
    {
        hit = default;
        surface = null;
        if (!mainCamera) return false;

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out hit, maxReachDistance, climbableLayer) &&
            !Physics.SphereCast(ray, grabSnapRadius, out hit, maxReachDistance, climbableLayer))
            return false;

        surface = hit.collider.GetComponentInParent<ClimbableSurface>();
        return CanClimbSurface(surface);
    }

    private void HandleGrabInput(
        int mouseButton,
        ref bool isGripping,
        ref Vector3 grabPoint,
        ref Vector3 grabNormal,
        ref ClimbableSurface handSurface,
        ref Collider handCollider,
        ref float grabDuration)
    {
        float specificCooldown = mouseButton == 0 ? leftCooldownTimer : rightCooldownTimer;

        if (Input.GetMouseButtonDown(mouseButton)
            && grabTimer <= 0f
            && specificCooldown <= 0f
            && handStamina.CanGrab(mouseButton))
        {
            if (TryFindGrabTarget(out RaycastHit hit, out ClimbableSurface surface))
            {
                isGripping = true;
                grabPoint = hit.point;
                grabNormal = hit.normal;
                handSurface = surface;
                handCollider = hit.collider;
                grabDuration = 0f;

                if (movement) movement.NotifyGrabbed();

                float distToPoint = Vector3.Distance(transform.position, grabPoint);

                if ((mouseButton == 0 && isRightGripping) || (mouseButton == 1 && isLeftGripping))
                    currentRopeLength = (currentRopeLength + distToPoint) / 2f;
                else
                    currentRopeLength = distToPoint;

                currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

                playerRb.linearVelocity *= 0.3f;
                grabTimer = grabCooldown;

                SurfaceTypeSO type = GetSurfaceType(surface);
                if (type != null && type.grabSound != null)
                    AudioSource.PlayClipAtPoint(type.grabSound, hit.point);
            }
        }

        if (isGripping)
        {
            if (handCollider == null || !handCollider.enabled || !handCollider.gameObject.activeInHierarchy)
            {
                ReleaseHand(mouseButton);
                return;
            }

            grabDuration += Time.deltaTime;

            SurfaceTypeSO type = GetSurfaceType(handSurface);
            if (type != null)
            {
                if (!type.canClimb)
                {
                    ReleaseHand(mouseButton);
                    return;
                }

                if (type.isSlippery)
                    SlideGrabPoint(ref grabPoint, ref grabNormal, handCollider, type);

                if (type.useMaxGrabTime && type.maxGrabTime > 0f && grabDuration >= type.maxGrabTime)
                {
                    ReleaseHand(mouseButton);
                    return;
                }
            }
        }

        if (Input.GetMouseButtonUp(mouseButton) && isGripping)
            ReleaseHand(mouseButton);
    }

    private void SlideGrabPoint(ref Vector3 grabPoint, ref Vector3 grabNormal, Collider handCollider, SurfaceTypeSO type)
    {
        Vector3 slideDirection = grabNormal.y < -0.5f
            ? (transform.position - grabPoint)
            : Vector3.down;

        if (grabNormal.y < -0.5f)
        {
            slideDirection.y = 0f;
            slideDirection.Normalize();
        }

        Vector3 nextGrabPoint = grabPoint + slideDirection * type.slideSpeed * Time.deltaTime;
        Ray slideRay = new Ray(nextGrabPoint + grabNormal * 0.3f, -grabNormal);

        if (handCollider.Raycast(slideRay, out RaycastHit slideHit, 0.6f))
        {
            grabPoint = slideHit.point;
            grabNormal = slideHit.normal;
        }
    }

    private void ReleaseHand(int mouseButton)
    {
        if (mouseButton == 0)
        {
            if (!isLeftGripping) return;
            isLeftGripping = false;
            leftCooldownTimer = leftHandCooldown;
            leftSurface = null;
            leftCollider = null;
            leftGrabDuration = 0f;
        }
        else
        {
            if (!isRightGripping) return;
            isRightGripping = false;
            rightCooldownTimer = rightHandCooldown;
            rightSurface = null;
            rightCollider = null;
            rightGrabDuration = 0f;
        }

        handStamina.NotifyReleased(mouseButton);
    }

    public void ReleaseForBounce()
    {
        ReleaseHand(0);
        ReleaseHand(1);
        playerRb.useGravity = true;
    }

    private void HandleVaultInput()
    {
        if (!Input.GetButtonDown("Jump") || !IsClimbing) return;

        handStamina.ConsumeVaultStamina();
        ReleaseHand(0);
        ReleaseHand(1);

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
        float angleRadians = vaultAngle * Mathf.Deg2Rad;

        Vector3 vaultDirection = horizontalDirection * Mathf.Cos(angleRadians) + Vector3.up * Mathf.Sin(angleRadians);
        Vector3 impulse = vaultDirection * vaultForce;

        playerRb.useGravity = true;

        if (movement)
            movement.ApplyVaultImpulse(impulse);
        else
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.AddForce(impulse, ForceMode.Impulse);
        }
    }

    private void UpdateHandPositions()
    {
        leftHand.position = isLeftGripping ? leftGrabPoint : mainCamera.transform.TransformPoint(leftHandIdleOffset);
        rightHand.position = isRightGripping ? rightGrabPoint : mainCamera.transform.TransformPoint(rightHandIdleOffset);
    }

    private float GetHandGripMultiplier(ClimbableSurface surface)
    {
        SurfaceTypeSO type = GetSurfaceType(surface);
        return type != null ? type.gripMultiplier : 1f;
    }

    private float GetGripMultiplier()
    {
        float total = 0f;
        int count = 0;

        if (isLeftGripping) { total += GetHandGripMultiplier(leftSurface); count++; }
        if (isRightGripping) { total += GetHandGripMultiplier(rightSurface); count++; }

        return count > 0 ? total / count : 1f;
    }

    private void HandleClimbingPhysics()
    {
        playerRb.useGravity = !IsClimbing;
        if (!IsClimbing) return;

        Vector3 centerOfGrip = Vector3.zero;
        int activeGrips = 0;

        if (isLeftGripping) { centerOfGrip += leftGrabPoint; activeGrips++; }
        if (isRightGripping) { centerOfGrip += rightGrabPoint; activeGrips++; }
        centerOfGrip /= activeGrips;

        float currentDist = Vector3.Distance(transform.position, centerOfGrip);
        Vector3 dirToGrip = (centerOfGrip - transform.position).normalized;
        Vector3 currentVel = playerRb.linearVelocity;

        currentVel += Physics.gravity * Time.fixedDeltaTime;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            float effectivePullSpeed = pullSpeed * GetGripMultiplier();
            currentRopeLength -= effectivePullSpeed * Time.fixedDeltaTime;

            if (currentDist <= minRopeLength + 0.5f)
                currentVel = Vector3.Lerp(currentVel, dirToGrip * effectivePullSpeed, 10f * Time.fixedDeltaTime);
        }

        currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

        if (currentDist > currentRopeLength)
        {
            float stretch = currentDist - currentRopeLength;
            currentVel += dirToGrip * (stretch * 50f * Time.fixedDeltaTime);

            float outwardSpeed = Vector3.Dot(currentVel, -dirToGrip);
            if (outwardSpeed > 0f)
                currentVel += dirToGrip * outwardSpeed;
        }

        Vector3 camForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;

        Vector3 swingDir = (camRight * Input.GetAxis("Horizontal") + camForward * Input.GetAxis("Vertical")).normalized;
        currentVel += swingDir * (swingForce * Time.fixedDeltaTime);
        currentVel *= 0.98f;
        currentVel = Vector3.ClampMagnitude(currentVel, maxClimbSpeed);

        playerRb.linearVelocity = currentVel;
    }

    public bool HasGrabbableTarget()
    {
        return TryFindGrabTarget(out _, out _);
    }

    private void OnDrawGizmosSelected()
    {
        Camera cam = mainCamera ? mainCamera : Camera.main;
        if (!cam) return;

        Vector3 end = cam.transform.position + cam.transform.forward * maxReachDistance;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(cam.transform.position, end);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(end, grabSnapRadius);
    }
}