using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharaController : MonoBehaviour, IRuntimeResettable
{
    private const float kSwingMinDistance = 0.2f;
    private const float kSwingRotationSpeed = 6f;
    private const float kSwingColliderReenableDelay = 1f;
    private const float kSwingLaunchJumpFactor = 0.48f;
    private const float kSwingAxisInputThreshold = 0.2f;
    private const float kSwingChargeDecayPerSecond = 0.45f;
    private const float kSwingAngleChargeMultiplier = 2.2f;
    private const float kSwingReleaseAnglePlanarBonus = 2.5f;
    private const float kSwingReleaseAngleUpwardBonus = 1.75f;

    [Header("References")]
    [SerializeField] private Rigidbody m_rb;
    [SerializeField] private GroundCheck m_groundCheck;
    [SerializeField] private Animator m_anim; 

    [Header("Movement")]
    [SerializeField] private float m_acceleration = 30f;
    [SerializeField] private float m_airControl = 0.5f;
    [SerializeField] private float m_rotationSpeed = 15f;

    [Header("Run")]
    [SerializeField] private float m_walkSpeed = 3f;
    [SerializeField] private float m_runSpeed = 6f;
    [SerializeField] private float m_speedSmooth = 10f;

    [Header("Damping")]
    [SerializeField] private float m_groundLinearDamping = 0.2f;
    [SerializeField] private float m_airLinearDamping = 0.2f;

    [Header("Jump")]
    [SerializeField] private float m_jumpHeight = 2.5f;
    [SerializeField] private float m_jumpTimeToApex = 0.4f;
    [SerializeField] private float m_maxJumpHoldTime = 0.25f;
    [SerializeField] private float m_lowJumpMultiplier = 2.2f;
    [SerializeField] private float m_fallMultiplier = 2.5f;

    [Header("Jump assist")]
    [SerializeField] private float m_jumpBufferTime = 0.12f;
    [SerializeField] private float m_coyoteTime = 0.12f;

    [Header("Swing (SpringJoint)")]
    [SerializeField] private float m_swingForce = 20f;

    [Header("Swing control")]
    [SerializeField] private float m_maxSwingTangentialSpeed = 12f;

    [Header("Swing limits")]
    [SerializeField] [Range(0.8f, 1.1f)] private float m_swingAnchorMinDistanceFactor = 0.97f;
    [SerializeField] private float m_swingTopClearance = 0.08f;
    [SerializeField] private float m_swingTopRecoverySpeed = 2.75f;

    [Header("Climb rope")]
    [SerializeField] private float m_climbSpeed = 2.8f;
    [SerializeField] private float m_climbSnapSpeed = 16f;
    [SerializeField] private float m_climbOffsetFromRope = 0.35f;
    [SerializeField] private float m_climbLinearDamping = 1f;
    [SerializeField] private float m_climbJumpForwardBoost = 1.5f;
    [SerializeField] private float m_climbJumpUpwardMultiplier = 0.9f;

    [Header("Swing charge (UI)")]
    [Tooltip("Vitesse à laquelle la barre se recharge en fonction de la vitesse tangentielle")]
    [SerializeField] private float m_chargePerSpeed = 0.12f;
    [SerializeField] private float m_maxCharge = 1f;
    [SerializeField] private float m_releaseBaseSpeed = 5f;
    [SerializeField] private float m_releaseChargeSpeedBonus = 10f;
    [SerializeField] private float m_releaseBaseUpwardSpeed = 3f;
    [SerializeField] private float m_releaseChargeUpwardBonus = 5f;
    [SerializeField] private float m_releaseMomentumInfluence = 0.6f;

    [Header("Input")]
    [SerializeField] private InputActionReference m_moveInput;
    [SerializeField] private InputActionReference m_runInput;
    [SerializeField] private InputActionReference m_jumpInput;
    [SerializeField] private InputActionReference m_detachInput;

    // States
    private bool m_isGrounded;
    private bool m_isJumping;
    private bool m_isSwinging;
    private bool m_isClimbing;

    // physics & movement
    private float m_jumpTimer; 
    private float m_jumpBufferTimer;
    private float m_coyoteTimer;
    private float m_currentSpeed;
    private float m_gravity;
    private float m_jumpVelocity;
    private Vector3 m_moveDirection;
    private bool m_ignoreJumpUntilReleased;

    // rope / swing
    private SwingRope m_currentSwingRope;
    private SpringJoint m_swingJoint;
    private Collider m_currentRopeCollider; 
    private ClimbRope m_currentClimbRope;
    private float m_climbParam = 1f;
    private Vector3 m_climbSideOffsetDirection = Vector3.forward;

    // attachment runtime
    private float m_attachParam = 0f; 
    private Vector3 m_attachPoint;

    // swing runtime
    private float m_swingCharge = 0f; 
    public bool IsSwinging => m_isSwinging;
    public float SwingChargeNormalized => m_maxCharge > 0f ? Mathf.Clamp01(m_swingCharge / m_maxCharge) : 0f;
    private Vector3 m_swingAxis = Vector3.forward;
    private Vector3 m_swingPlaneNormal = Vector3.right;
    private float m_minSwingDistanceFromAnchor = 0f;
    private float m_swingRadiusFromAnchor = 0f;

    private Coroutine m_shrinkRoutine;

    private void Start()
    {
        if (m_moveInput != null) m_moveInput.action.Enable();
        if (m_runInput != null) m_runInput.action.Enable();
        if (m_jumpInput != null) m_jumpInput.action.Enable();
        if (m_detachInput != null) m_detachInput.action.Enable();

        m_gravity = (-2f * m_jumpHeight) / Mathf.Pow(m_jumpTimeToApex, 2);
        m_jumpVelocity = (2f * m_jumpHeight) / m_jumpTimeToApex;

        if (m_rb == null) Debug.LogError("CharaController: Rigidbody reference is missing.");
        if (m_groundCheck == null) Debug.LogWarning("CharaController: GroundCheck missing - ground detection will fail.");
        if (m_anim == null) Debug.LogWarning("CharaController: Animator not assigned. Animator parameters won't be updated.");

        ResetRuntimeState();
    }

    private void OnDisable()
    {
        ResetSwingRuntimeImmediately();
        ExitClimbState();
    }

    public void ResetRuntimeState()
    {
        ResetSwingRuntimeImmediately();
        ExitClimbState();

        m_isGrounded = false;
        m_isJumping = false;
        m_jumpTimer = 0f;
        m_jumpBufferTimer = 0f;
        m_coyoteTimer = 0f;
        m_currentSpeed = 0f;
        m_moveDirection = Vector3.zero;
        m_ignoreJumpUntilReleased = false;
        m_attachParam = 0f;
        m_attachPoint = Vector3.zero;
        m_climbParam = 1f;
        m_climbSideOffsetDirection = Vector3.forward;
        m_swingAxis = GetCameraPlanarForward();
        m_swingPlaneNormal = Vector3.Cross(Vector3.up, m_swingAxis).normalized;
        if (m_swingPlaneNormal.sqrMagnitude < 1e-4f)
            m_swingPlaneNormal = Vector3.right;
        m_minSwingDistanceFromAnchor = 0f;
        m_swingRadiusFromAnchor = 0f;

        if (m_rb != null)
        {
            m_rb.useGravity = true;
            m_rb.linearVelocity = Vector3.zero;
            m_rb.angularVelocity = Vector3.zero;
            m_rb.linearDamping = GetClampedLinearDamping(m_airLinearDamping);
        }

        CheckGround();

        UpdateAnimatorParameters(true);
    }

    public void RespawnAt(Vector3 worldPosition, Quaternion worldRotation)
    {
        ResetRuntimeState();

        transform.SetPositionAndRotation(worldPosition, worldRotation);

        if (m_rb != null)
        {
            m_rb.position = worldPosition;
            m_rb.rotation = worldRotation;
            m_rb.linearVelocity = Vector3.zero;
            m_rb.angularVelocity = Vector3.zero;
            m_rb.Sleep();
        }

        Physics.SyncTransforms();
    }

    private void Update()
    {
        HandleInput();
        HandleSwingInput();
        HandleClimbInput();
        UpdateSpeed();
        HandleRotation();
        HandleJumpInput();
        SetDamping();

        if (m_ignoreJumpUntilReleased && (m_jumpInput == null || !m_jumpInput.action.IsPressed()))
        {
            m_ignoreJumpUntilReleased = false;
        }

    }

    private void FixedUpdate()
    {
        CheckGround();
        if (m_isClimbing)
        {
            HandleClimbMovement();
            UpdateAnimatorParameters(false);
            return;
        }

        ResolveJump();
        if (m_isSwinging)
            HandleSwingPhysics();
        else
            HandleMovement();

        ApplyGravity();

        UpdateAnimatorParameters(false);
    }

    // INPUT
    private void HandleInput()
    {
        Vector2 input = Vector2.zero;
        if (m_moveInput != null) input = m_moveInput.action.ReadValue<Vector2>();

        Vector3 camForward = GetCameraPlanarForward();
        Vector3 camRight = Vector3.Cross(Vector3.up, camForward).normalized;

        m_moveDirection = (camForward * input.y + camRight * input.x).normalized;
    }

    private bool WasDetachPressed()
    {
        return m_detachInput != null && m_detachInput.action.WasPressedThisFrame();
    }

    private bool WasSwingLaunchPressed()
    {
        return m_jumpInput != null && m_jumpInput.action.WasPressedThisFrame();
    }

    private float GetClampedLinearDamping(float damping)
    {
        return Mathf.Clamp(damping, 0f, 1f);
    }

    private Vector3 GetCameraPlanarForward()
    {
        Camera cam = Camera.main;
        Vector3 planarForward = cam != null ? cam.transform.forward : transform.forward;
        planarForward.y = 0f;

        if (planarForward.sqrMagnitude < 0.001f)
        {
            planarForward = transform.forward;
            planarForward.y = 0f;
        }

        return planarForward.sqrMagnitude > 0.001f ? planarForward.normalized : Vector3.forward;
    }

    // MOVEMENT
    private void HandleMovement()
    {
        if (m_isSwinging || m_isClimbing) return;

        float control = m_isGrounded ? 1f : m_airControl;

        Vector3 targetVelocity = m_moveDirection * m_currentSpeed;

        Vector3 velocity = m_rb != null ? m_rb.linearVelocity : Vector3.zero;

        Vector3 velocityChange = (targetVelocity - new Vector3(velocity.x, 0f, velocity.z)) * m_acceleration * control;

        if (m_rb != null) m_rb.AddForce(velocityChange, ForceMode.Acceleration);
    }

    private void HandleRotation()
    {
        if (m_isSwinging || m_isClimbing) return;

        if (m_moveDirection.sqrMagnitude < 0.01f) return;

        Vector3 targetForward = Vector3.RotateTowards(
            transform.forward,
            m_moveDirection,
            m_rotationSpeed * Time.deltaTime,
            0f
        );

        transform.forward = targetForward;
    }

    private void UpdateSpeed()
    {
        if (m_isSwinging || m_isClimbing)
        {
            m_currentSpeed = 0f;
            return;
        }

        bool isRunning = m_runInput != null && m_runInput.action.IsPressed() && m_moveDirection.sqrMagnitude > 0.1f;
        float targetSpeed = isRunning ? m_runSpeed : m_walkSpeed;
        m_currentSpeed = Mathf.Lerp(m_currentSpeed, targetSpeed, Time.deltaTime * m_speedSmooth);
    }

    private void SetDamping()
    {
        if (m_rb == null) return;
        if (m_isSwinging)
        {
            m_rb.linearDamping = GetClampedLinearDamping(m_airLinearDamping);
            return;
        }

        if (m_isClimbing)
        {
            m_rb.linearDamping = GetClampedLinearDamping(m_climbLinearDamping);
            return;
        }

        float targetDamping = m_isGrounded ? m_groundLinearDamping : m_airLinearDamping;
        m_rb.linearDamping = GetClampedLinearDamping(targetDamping);
    }

    // JUMP
    private void HandleJumpInput()
    {
        if (m_jumpInput == null) return;

        if (m_ignoreJumpUntilReleased)
        {
            m_jumpBufferTimer = 0f;
            return;
        }

        if (m_isClimbing)
        {
            m_jumpBufferTimer = 0f;
            return;
        }

        if (m_jumpInput.action.WasPressedThisFrame())
        {
            m_jumpBufferTimer = m_jumpBufferTime;
        }
        else
        {
            m_jumpBufferTimer = Mathf.Max(0f, m_jumpBufferTimer - Time.deltaTime);
        }

        if (m_jumpInput.action.IsPressed() && m_isJumping)
        {
            m_jumpTimer += Time.deltaTime;
        }

        if (m_jumpInput.action.WasReleasedThisFrame())
        {
            m_jumpTimer = m_maxJumpHoldTime;
        }
    }

    private void ResolveJump()
    {
        if (m_isClimbing) return;
        if (m_isSwinging) return;
        if (m_jumpBufferTimer <= 0f) return;
        if (!m_isGrounded && m_coyoteTimer <= 0f) return;

        Jump();
        m_jumpBufferTimer = 0f;
        m_coyoteTimer = 0f;
    }

    private void Jump()
    {
        if (m_rb == null) return;

        Vector3 velocity = m_rb.linearVelocity;
        velocity.y = 0f;
        m_rb.linearVelocity = velocity;

        m_rb.AddForce(Vector3.up * m_jumpVelocity, ForceMode.VelocityChange);

        m_isJumping = true;
        m_jumpTimer = 0f;

        if (m_anim != null)
        {
            m_anim.SetTrigger("JumpTrigger");
            m_anim.SetBool("IsJumping", true);
        }
    }

    private void ApplyGravity()
    {
        if (m_isSwinging || m_isClimbing) return;
        if (m_rb == null) return;

        float gravityMultiplier = 1f;
        bool canUseFullJump = m_jumpInput != null
                              && m_jumpInput.action.IsPressed()
                              && m_isJumping
                              && m_jumpTimer < m_maxJumpHoldTime;

        if (m_rb.linearVelocity.y < -0.01f)
        {
            gravityMultiplier = m_fallMultiplier;
        }
        else if (m_rb.linearVelocity.y > 0.01f && !canUseFullJump)
        {
            gravityMultiplier = m_lowJumpMultiplier;
        }

        m_rb.AddForce(Vector3.up * m_gravity * gravityMultiplier, ForceMode.Acceleration);
    }

    private void CheckGround()
    {
        if (m_groundCheck == null) { m_isGrounded = false; return; }

        m_isGrounded = m_groundCheck.GetIsHitting();
        if (m_isGrounded)
        {
            m_coyoteTimer = m_coyoteTime;

            if (m_rb == null || m_rb.linearVelocity.y <= 0.05f)
            {
                m_isJumping = false;
                m_jumpTimer = 0f;
            }
        }
        else
        {
            m_coyoteTimer = Mathf.Max(0f, m_coyoteTimer - Time.fixedDeltaTime);
        }
    }

    // SWING (SpringJoint)  
    private void HandleSwingInput()
    {
        if (m_isClimbing) return;
        if (!m_isSwinging) return;

        if (WasSwingLaunchPressed())
        {
            StopSwing(true);
            return;
        }

        if (WasDetachPressed())
        {
            StopSwing(false);
        }
    }

    private void HandleClimbInput()
    {
        if (!m_isClimbing) return;

        if (WasDetachPressed())
        {
            StopClimb(false);
            return;
        }

        if (m_jumpInput != null && m_jumpInput.action.WasPressedThisFrame())
        {
            StopClimb(true);
        }
    }

    private void UpdateSwingAxisFromInput(Vector2 rawInput, bool forceDefaultAxis)
    {
        Vector3 desiredAxis = GetDesiredSwingAxis(rawInput);
        if (desiredAxis.sqrMagnitude < 0.001f)
        {
            if (!forceDefaultAxis)
                return;

            desiredAxis = GetCameraPlanarForward();
        }

        m_swingAxis = Vector3.ProjectOnPlane(desiredAxis, Vector3.up).normalized;
        if (m_swingAxis.sqrMagnitude < 0.001f)
            m_swingAxis = Vector3.forward;

        m_swingPlaneNormal = Vector3.Cross(Vector3.up, m_swingAxis).normalized;
        if (m_swingPlaneNormal.sqrMagnitude < 0.001f)
            m_swingPlaneNormal = Vector3.right;
    }

    private Vector3 GetDesiredSwingAxis(Vector2 rawInput)
    {
        Vector3 cameraForward = GetCameraPlanarForward();
        Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward).normalized;

        if (Mathf.Abs(rawInput.y) >= kSwingAxisInputThreshold && Mathf.Abs(rawInput.y) >= Mathf.Abs(rawInput.x))
            return cameraForward * Mathf.Sign(rawInput.y);

        if (Mathf.Abs(rawInput.x) >= kSwingAxisInputThreshold)
            return cameraRight * Mathf.Sign(rawInput.x);

        return Vector3.zero;
    }

    private float GetSwingInputStrength(Vector2 rawInput)
    {
        float dominantInput = Mathf.Abs(rawInput.y) >= Mathf.Abs(rawInput.x) ? rawInput.y : rawInput.x;
        return Mathf.Clamp01(Mathf.Abs(dominantInput));
    }

    private void StartClimb(ClimbRope climbRope)
    {
        if (climbRope == null || m_rb == null) return;
        if (m_isSwinging || m_isClimbing) return;

        m_currentClimbRope = climbRope;
        m_isClimbing = true;
        m_isJumping = false;
        m_jumpTimer = 0f;
        m_jumpBufferTimer = 0f;
        m_rb.useGravity = false;
        m_rb.linearVelocity = Vector3.zero;

        m_climbParam = climbRope.GetClosestPointParam(transform.position);

        Vector3 closestPoint = climbRope.GetPointAlongRope(m_climbParam);
        Vector3 ropeDirection = climbRope.RopeDirection;
        Vector3 sideDirection = Vector3.ProjectOnPlane(transform.position - closestPoint, ropeDirection);
        if (sideDirection.sqrMagnitude < 1e-4f)
        {
            sideDirection = climbRope.GetDefaultSideDirection();
        }

        m_climbSideOffsetDirection = sideDirection.normalized;
        SnapToClimbRope(1f);

        Debug.Log("Detecte ClimbRope: " + climbRope.name);
    }

    private void HandleClimbMovement()
    {
        if (!m_isClimbing || m_currentClimbRope == null || m_rb == null) return;

        Vector2 rawInput = m_moveInput != null ? m_moveInput.action.ReadValue<Vector2>() : Vector2.zero;
        float ropeLength = Mathf.Max(0.1f, m_currentClimbRope.RopeLength);
        m_climbParam = Mathf.Clamp01(m_climbParam - (rawInput.y * m_climbSpeed * Time.fixedDeltaTime / ropeLength));

        SnapToClimbRope(1f - Mathf.Exp(-m_climbSnapSpeed * Time.fixedDeltaTime));

        if (m_climbParam >= 0.999f && rawInput.y < -0.1f && m_isGrounded)
        {
            StopClimb(false);
        }
    }

    private void SnapToClimbRope(float snapAlpha)
    {
        if (m_currentClimbRope == null || m_rb == null) return;

        Vector3 targetPosition = m_currentClimbRope.GetPointAlongRope(m_climbParam) + m_climbSideOffsetDirection * m_climbOffsetFromRope;
        Vector3 snappedPosition = Vector3.Lerp(m_rb.position, targetPosition, snapAlpha);
        m_rb.MovePosition(snappedPosition);
        m_rb.linearVelocity = Vector3.zero;

        Vector3 desiredForward = -m_climbSideOffsetDirection;
        if (desiredForward.sqrMagnitude > 1e-4f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredForward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, snapAlpha);
        }
    }

    private void StopClimb(bool jumpOff)
    {
        if (!m_isClimbing) return;

        Vector3 releaseDirection = m_climbSideOffsetDirection.sqrMagnitude > 1e-4f
            ? m_climbSideOffsetDirection
            : -transform.forward;

        ExitClimbState();

        if (m_rb != null)
        {
            m_rb.linearVelocity = Vector3.zero;

            if (jumpOff)
            {
                Vector3 jumpImpulse = Vector3.up * (m_jumpVelocity * m_climbJumpUpwardMultiplier)
                                      + releaseDirection * m_climbJumpForwardBoost;
                m_rb.AddForce(jumpImpulse, ForceMode.VelocityChange);
            }
        }

        if (jumpOff)
        {
            m_isJumping = true;
            m_anim.SetTrigger("JumpTrigger");
            m_jumpTimer = 0f;
            m_coyoteTimer = 0f;
            m_ignoreJumpUntilReleased = true;
        }
    }

    private void ExitClimbState()
    {
        m_isClimbing = false;
        m_currentClimbRope = null;
        

        if (m_rb != null)
        {
            m_rb.useGravity = true;
        }
    }

    private void StartSwingWithJoint()
    {
        if (m_isSwinging || m_isClimbing || m_currentSwingRope == null || m_rb == null) return;

        m_isSwinging = true;
        m_isJumping = false;
        m_jumpTimer = 0f;
        m_rb.useGravity = true;

        if (m_swingJoint == null)
        {
            m_swingJoint = gameObject.AddComponent<SpringJoint>();
        }

        if (m_currentRopeCollider != null)
        {
            m_attachPoint = m_currentRopeCollider.ClosestPoint(transform.position);
        }
        else
        {
            m_attachPoint = m_currentSwingRope.GetClosestPointOnRope(transform.position);
        }

        m_attachParam = m_currentSwingRope != null ? m_currentSwingRope.GetClosestPointParam(m_attachPoint) : 0f;

        float currentDistance = Vector3.Distance(m_attachPoint, transform.position);
        float swingDistance = Mathf.Clamp(currentDistance, kSwingMinDistance, Mathf.Max(kSwingMinDistance, m_climbOffsetFromRope));
        float anchorDistanceAtAttach = Vector3.Distance(m_currentSwingRope.AnchorPosition, m_attachPoint);
        float ropeBasedMinDistance = m_currentSwingRope.RopeLength * Mathf.Clamp01(m_attachParam) * m_swingAnchorMinDistanceFactor;
        m_minSwingDistanceFromAnchor = Mathf.Max(kSwingMinDistance, anchorDistanceAtAttach, ropeBasedMinDistance);
        m_swingRadiusFromAnchor = Mathf.Max(
            kSwingMinDistance,
            Vector3.Distance(m_currentSwingRope.AnchorPosition, transform.position),
            m_minSwingDistanceFromAnchor
        );

        Rigidbody connectRb = null;
        if (m_currentRopeCollider != null)
        {
            connectRb = m_currentRopeCollider.attachedRigidbody ?? m_currentRopeCollider.GetComponentInParent<Rigidbody>();
        }
        if (connectRb == null && m_currentSwingRope != null)
        {
            connectRb = m_currentSwingRope.AnchorRb;
        }

        m_swingJoint.autoConfigureConnectedAnchor = false;
        if (connectRb != null && connectRb != m_rb)
        {
            m_swingJoint.connectedBody = connectRb;
            m_swingJoint.connectedAnchor = connectRb.transform.InverseTransformPoint(m_attachPoint);
        }
        else
        {
            m_swingJoint.connectedBody = null;
            m_swingJoint.connectedAnchor = m_attachPoint;
        }

        m_swingJoint.maxDistance = swingDistance;
        m_swingJoint.minDistance = swingDistance;
        m_swingJoint.spring = 0f;
        m_swingJoint.damper = 0f;
        m_swingJoint.tolerance = 0f;
        m_swingJoint.enableCollision = false;

        m_currentSwingRope.SetEndCollidersEnabled(false);
        Transform attachedBone = m_currentRopeCollider != null
            ? (m_currentRopeCollider.attachedRigidbody != null ? m_currentRopeCollider.attachedRigidbody.transform : m_currentRopeCollider.transform)
            : null;
        m_currentSwingRope.StartFollow(transform, attachedBone);

        Vector2 rawInput = m_moveInput != null ? m_moveInput.action.ReadValue<Vector2>() : Vector2.zero;
        UpdateSwingAxisFromInput(rawInput, true);

        Vector3 anchorToPlayer = transform.position - m_attachPoint;
        if (anchorToPlayer.sqrMagnitude > 1e-6f)
        {
            Vector3 radialDir = anchorToPlayer.normalized;
            Vector3 snappedPosition = m_attachPoint + radialDir * swingDistance;
            bool reachedTopLimit = false;
            Vector3 swingAxisPlanar = Vector3.ProjectOnPlane(m_swingAxis, Vector3.up).normalized;
            if (swingAxisPlanar.sqrMagnitude < 1e-6f)
                swingAxisPlanar = Vector3.forward;

            snappedPosition = ConstrainPositionAroundRopeAnchor(snappedPosition, swingAxisPlanar, ref reachedTopLimit);
            m_rb.position = snappedPosition;
            transform.position = snappedPosition;
            m_swingRadiusFromAnchor = Mathf.Max(
                kSwingMinDistance,
                Vector3.Distance(m_currentSwingRope.AnchorPosition, snappedPosition),
                m_minSwingDistanceFromAnchor
            );

            Vector3 preservedVelocity = m_rb.linearVelocity;
            m_rb.linearVelocity = preservedVelocity - radialDir * Vector3.Dot(preservedVelocity, radialDir);
        }

        m_swingCharge = 0f;
    }

    private void HandleSwingPhysics()
    {
        if (!m_isSwinging || m_swingJoint == null || m_currentSwingRope == null || m_rb == null) return;

        Vector2 rawInput = m_moveInput != null ? m_moveInput.action.ReadValue<Vector2>() : Vector2.zero;
        UpdateSwingAxisFromInput(rawInput, false);

        Vector3 anchorPosition = m_currentSwingRope.AnchorPosition;
        Vector3 anchorToPlayer = transform.position - anchorPosition;
        if (anchorToPlayer.sqrMagnitude < 1e-6f) anchorToPlayer = Vector3.down;

        float ropeDistance = Mathf.Max(kSwingMinDistance, m_swingRadiusFromAnchor, m_minSwingDistanceFromAnchor);
        Vector3 planarAnchorToPlayer = Vector3.ProjectOnPlane(anchorToPlayer, m_swingPlaneNormal);
        Vector3 swingDirection = planarAnchorToPlayer.sqrMagnitude > 1e-6f
            ? planarAnchorToPlayer.normalized
            : Vector3.ProjectOnPlane(Vector3.down, m_swingPlaneNormal).normalized;
        if (swingDirection.sqrMagnitude < 1e-6f)
            swingDirection = Vector3.down;

        Vector3 swingAxisPlanar = Vector3.ProjectOnPlane(m_swingAxis, Vector3.up).normalized;
        if (swingAxisPlanar.sqrMagnitude < 1e-6f)
            swingAxisPlanar = Vector3.forward;

        float maxVerticalDirection = -Mathf.Clamp01(m_swingTopClearance / Mathf.Max(ropeDistance, kSwingMinDistance));
        bool reachedTopLimit = false;
        if (swingDirection.y > maxVerticalDirection)
        {
            float horizontalSign = Mathf.Sign(Vector3.Dot(swingDirection, swingAxisPlanar));
            if (Mathf.Approximately(horizontalSign, 0f))
                horizontalSign = 1f;

            float horizontalMagnitude = Mathf.Sqrt(Mathf.Max(0f, 1f - maxVerticalDirection * maxVerticalDirection));
            swingDirection = (swingAxisPlanar * horizontalSign * horizontalMagnitude) + (Vector3.up * maxVerticalDirection);
            swingDirection.Normalize();
            reachedTopLimit = true;
        }

        Vector3 constrainedPosition = anchorPosition + swingDirection * ropeDistance;
        constrainedPosition = ConstrainPositionAroundRopeAnchor(constrainedPosition, swingAxisPlanar, ref reachedTopLimit);
        m_rb.MovePosition(constrainedPosition);

        Vector3 dir = constrainedPosition - anchorPosition;
        if (dir.sqrMagnitude > 1e-6f)
            dir.Normalize();
        else
            dir = swingDirection;
        float maxTangentialSpeed = Mathf.Max(m_runSpeed, m_maxSwingTangentialSpeed);
        Vector3 vel = Vector3.ProjectOnPlane(m_rb.linearVelocity, m_swingPlaneNormal);
        Vector3 tangentialVel = vel - dir * Vector3.Dot(vel, dir);
        tangentialVel = Vector3.ClampMagnitude(tangentialVel, maxTangentialSpeed);

        if (reachedTopLimit && Vector3.Dot(tangentialVel, Vector3.up) > 0f)
        {
            tangentialVel = Vector3.ProjectOnPlane(tangentialVel, Vector3.up);
            tangentialVel = Vector3.ProjectOnPlane(tangentialVel, m_swingPlaneNormal);
        }

        if (reachedTopLimit)
        {
            tangentialVel += Vector3.down * m_swingTopRecoverySpeed;
            tangentialVel = Vector3.ProjectOnPlane(tangentialVel, dir);
            tangentialVel = Vector3.ProjectOnPlane(tangentialVel, m_swingPlaneNormal);
        }

        m_rb.linearVelocity = tangentialVel;
        float tangentialSpeed = tangentialVel.magnitude;

        float attachFactor = Mathf.Lerp(0.6f, 1.2f, Mathf.Clamp01(m_attachParam));
        Vector3 desiredTangent = Vector3.Cross(m_swingPlaneNormal, dir).normalized;
        if (Vector3.Dot(desiredTangent, m_swingAxis) < 0f)
            desiredTangent = -desiredTangent;

        float inputStrength = GetSwingInputStrength(rawInput);
        bool canPumpSwing = inputStrength > 0f && desiredTangent.sqrMagnitude > 0.001f;
        if (canPumpSwing)
        {
            float pumpVelocity = m_swingForce * attachFactor * inputStrength * Time.fixedDeltaTime;
            tangentialVel += desiredTangent * pumpVelocity;
            tangentialVel = Vector3.ProjectOnPlane(tangentialVel, dir);
            tangentialVel = Vector3.ProjectOnPlane(tangentialVel, m_swingPlaneNormal);
            tangentialVel = Vector3.ClampMagnitude(tangentialVel, maxTangentialSpeed);
            m_rb.linearVelocity = tangentialVel;
            tangentialSpeed = tangentialVel.magnitude;
        }

        float angleFromBottom = Vector3.Angle(Vector3.down, dir);
        float swingAngle01 = Mathf.Clamp01(angleFromBottom / 90f);
        if (canPumpSwing)
        {
            float chargeGain = ((tangentialSpeed * m_chargePerSpeed) + (swingAngle01 * m_chargePerSpeed * kSwingAngleChargeMultiplier))
                             * attachFactor
                             * Time.fixedDeltaTime;
            m_swingCharge = Mathf.Clamp(m_swingCharge + chargeGain, 0f, m_maxCharge);
        }
        else
        {
            m_swingCharge = Mathf.MoveTowards(m_swingCharge, 0f, kSwingChargeDecayPerSecond * Time.fixedDeltaTime);
        }

        Vector3 desiredForward = Vector3.ProjectOnPlane(m_swingAxis, Vector3.up).normalized;
        if (desiredForward.sqrMagnitude < 0.001f && tangentialVel.sqrMagnitude > 0.01f)
            desiredForward = Vector3.ProjectOnPlane(tangentialVel.normalized, Vector3.up);

        if (desiredForward.sqrMagnitude > 0.001f)
        {
            Quaternion q = Quaternion.LookRotation(desiredForward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, q, kSwingRotationSpeed * Time.deltaTime);
        }
    }

    private Vector3 GetCurrentSwingAnchorPosition()
    {
        if (m_currentSwingRope != null) return m_currentSwingRope.AnchorPosition;
        if (m_swingJoint == null) return m_attachPoint;

        if (m_swingJoint.connectedBody != null)
            return m_swingJoint.connectedBody.transform.TransformPoint(m_swingJoint.connectedAnchor);

        return m_swingJoint.connectedAnchor;
    }

    private Vector3 ConstrainPositionAroundRopeAnchor(Vector3 candidatePosition, Vector3 swingAxisPlanar, ref bool reachedTopLimit)
    {
        if (m_currentSwingRope == null)
            return candidatePosition;

        Vector3 ropeAnchorPosition = m_currentSwingRope.AnchorPosition;
        Vector3 ropeAnchorToPlayer = candidatePosition - ropeAnchorPosition;
        if (ropeAnchorToPlayer.sqrMagnitude < 1e-6f)
            ropeAnchorToPlayer = Vector3.down;

        float anchorDistance = ropeAnchorToPlayer.magnitude;
        float minimumDistance = Mathf.Max(kSwingMinDistance, m_minSwingDistanceFromAnchor);
        if (anchorDistance < minimumDistance)
        {
            ropeAnchorToPlayer = ropeAnchorToPlayer.normalized * minimumDistance;
            candidatePosition = ropeAnchorPosition + ropeAnchorToPlayer;
            anchorDistance = minimumDistance;
        }

        float maxVerticalDirection = -Mathf.Clamp01(m_swingTopClearance / Mathf.Max(anchorDistance, kSwingMinDistance));
        Vector3 anchorDirection = ropeAnchorToPlayer.normalized;
        if (anchorDirection.y > maxVerticalDirection)
        {
            if (swingAxisPlanar.sqrMagnitude < 1e-6f)
                swingAxisPlanar = Vector3.forward;

            float horizontalSign = Mathf.Sign(Vector3.Dot(anchorDirection, swingAxisPlanar));
            if (Mathf.Approximately(horizontalSign, 0f))
                horizontalSign = 1f;

            float horizontalMagnitude = Mathf.Sqrt(Mathf.Max(0f, 1f - maxVerticalDirection * maxVerticalDirection));
            anchorDirection = (swingAxisPlanar * horizontalSign * horizontalMagnitude) + (Vector3.up * maxVerticalDirection);
            candidatePosition = ropeAnchorPosition + anchorDirection.normalized * anchorDistance;
            reachedTopLimit = true;
        }

        return candidatePosition;
    }

    private Vector3 ComputeSwingLaunchVelocity(Vector3 releaseVelocity, Vector3 anchorPosition)
    {
        Vector3 anchorToPlayer = transform.position - anchorPosition;
        if (anchorToPlayer.sqrMagnitude < 1e-6f) anchorToPlayer = Vector3.down;

        Vector3 radialDirection = anchorToPlayer.normalized;
        Vector3 tangentialVelocity = Vector3.ProjectOnPlane(
            releaseVelocity - radialDirection * Vector3.Dot(releaseVelocity, radialDirection),
            m_swingPlaneNormal
        );
        float charge01 = SwingChargeNormalized;
        float swingAngle01 = Mathf.Clamp01(Vector3.Angle(Vector3.down, radialDirection) / 90f);

        Vector3 planarVelocity = Vector3.ProjectOnPlane(tangentialVelocity, Vector3.up);
        Vector3 planarDirection = planarVelocity.sqrMagnitude > 0.001f
            ? planarVelocity.normalized
            : Vector3.ProjectOnPlane(m_swingAxis, Vector3.up).normalized;

        if (planarDirection.sqrMagnitude < 0.001f)
            planarDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        float forwardMomentum = planarVelocity.magnitude;
        float planarSpeed = Mathf.Max(m_runSpeed, forwardMomentum)
                          + m_releaseBaseSpeed
                          + charge01 * m_releaseChargeSpeedBonus
                          + forwardMomentum * m_releaseMomentumInfluence
                          + swingAngle01 * kSwingReleaseAnglePlanarBonus;
        float upwardSpeed = Mathf.Max(
            m_jumpVelocity * kSwingLaunchJumpFactor,
            m_releaseBaseUpwardSpeed
            + charge01 * m_releaseChargeUpwardBonus
            + forwardMomentum * 0.18f
            + swingAngle01 * kSwingReleaseAngleUpwardBonus
        );

        return planarDirection * planarSpeed + Vector3.up * upwardSpeed;
    }

    private void StopSwing(bool jumpOff = false)
    {
        if (!m_isSwinging || m_rb == null) return;

        Vector3 releaseVelocity = m_rb.linearVelocity;
        Vector3 anchorPosition = GetCurrentSwingAnchorPosition();
        Vector3 launchVelocity = jumpOff
            ? ComputeSwingLaunchVelocity(releaseVelocity, anchorPosition)
            : releaseVelocity;

        SwingRope ropeToReactivate = DetachSwingInternals();
        m_rb.linearVelocity = Vector3.zero;
        m_rb.angularVelocity = Vector3.zero;

        if (jumpOff)
        {
            m_rb.AddForce(launchVelocity, ForceMode.VelocityChange);
            m_isJumping = true;
            m_anim.SetTrigger("JumpTrigger");
            m_jumpTimer = 0f;
            m_coyoteTimer = 0f;
            m_jumpBufferTimer = 0f;
            m_ignoreJumpUntilReleased = true;
        }
        else
        {
            m_rb.linearVelocity = launchVelocity;
        }

        if (ropeToReactivate != null)
        {
            StartCoroutine(ReenableEndCollidersCoroutine(ropeToReactivate));
        }

        m_currentSwingRope = null;
        m_currentRopeCollider = null;

        m_swingCharge = 0f;
    }

    private SwingRope DetachSwingInternals()
    {
        SwingRope ropeToReactivate = m_currentSwingRope;
        if (ropeToReactivate != null)
        {
            ropeToReactivate.StopFollow();
        }

        m_isSwinging = false;

        if (m_swingJoint != null)
        {
            Destroy(m_swingJoint);
            m_swingJoint = null;
        }

        return ropeToReactivate;
    }

    private void ResetSwingRuntimeImmediately()
    {
        SwingRope ropeToReset = DetachSwingInternals();
        if (ropeToReset != null)
        {
            ropeToReset.SetEndCollidersEnabled(true);
        }

        m_currentSwingRope = null;
        m_currentRopeCollider = null;
        m_swingCharge = 0f;
        m_minSwingDistanceFromAnchor = 0f;
        m_swingRadiusFromAnchor = 0f;
    }

    private IEnumerator ReenableEndCollidersCoroutine(SwingRope rope)
    {
        yield return new WaitForSeconds(kSwingColliderReenableDelay);
        if (rope != null)
        {
            rope.SetEndCollidersEnabled(true);
        }
    }

    // TRIGGERS
    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        ClimbRope climbRope = other.GetComponentInParent<ClimbRope>();
        if (climbRope == null) climbRope = other.GetComponentInChildren<ClimbRope>();

        if (climbRope != null && !m_isSwinging && !m_isClimbing)
        {
            StartClimb(climbRope);
            return;
        }

        SwingRope swingRope = other.GetComponentInParent<SwingRope>();
        if (swingRope == null) swingRope = other.GetComponentInChildren<SwingRope>();

        if (swingRope != null)
        {
            m_currentSwingRope = swingRope;
            m_currentRopeCollider = other;
            Debug.Log("Detecte SwingRope: " + swingRope.name + " (collider=" + other.name + ")");

            if (!m_isSwinging && !m_isClimbing)
            {
                StartSwingWithJoint();
            }
        }

    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;

        ClimbRope climbRope = other.GetComponentInParent<ClimbRope>();
        if (climbRope == null) climbRope = other.GetComponentInChildren<ClimbRope>();

        if (climbRope != null && climbRope == m_currentClimbRope && !m_isClimbing)
        {
            m_currentClimbRope = null;
            Debug.Log("Left ClimbRope: " + climbRope.name);
        }

        SwingRope swingRope = other.GetComponentInParent<SwingRope>();
        if (swingRope == null) swingRope = other.GetComponentInChildren<SwingRope>();

        if (swingRope != null && swingRope == m_currentSwingRope && !m_isSwinging)
        {
            m_currentSwingRope = null;
            Debug.Log("Left SwingRope: " + swingRope.name);
        }

        if (other == m_currentRopeCollider)
        {
            m_currentRopeCollider = null;
        }
    }

    // Animator synchronization
    private void UpdateAnimatorParameters(bool force)
    {
        if (m_anim == null) return;

        bool isWalking = !m_isSwinging && !m_isClimbing && m_moveDirection.sqrMagnitude > 0.01f && !(m_runInput != null && m_runInput.action.IsPressed());
        bool isRunning = !m_isSwinging && !m_isClimbing && (m_runInput != null && m_runInput.action.IsPressed()) && m_moveDirection.sqrMagnitude > 0.1f;
        bool onGround = m_isGrounded;
        bool isClimbing = m_isClimbing;
        bool isSwinging = m_isSwinging;

        if (force)
        {
            onGround = true;
            isClimbing = false;
            isSwinging = false;
        }

        m_anim.SetBool("IsWalking", isWalking);
        m_anim.SetBool("IsRunning", isRunning);
        m_anim.SetBool("OnGround", onGround);
        m_anim.SetBool("IsClimbing", isClimbing);
        m_anim.SetBool("IsSwinging", isSwinging);
    }

}
