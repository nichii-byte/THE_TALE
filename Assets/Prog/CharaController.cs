using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharaController : MonoBehaviour, IRuntimeResettable
{
    private const float kSwingTolerance = 0.02f;
    private const float kSwingMinDistanceRatio = 0.9f;
    private const float kSwingTopClearance = 0.08f;
    private const float kSwingAttachShrinkDuration = 0.18f;
    private const float kSwingCorrectionSpeed = 10f;
    private const float kSwingRadialDampingFactor = 0.5f;
    private const float kSwingMaxDistanceMultiplier = 1.02f;
    private const float kSwingMinDistance = 0.2f;
    private const float kSwingTopClampSpeed = 6f;
    private const float kSwingRotationSpeed = 6f;
    private const float kSwingColliderReenableDelay = 1f;
    private const float kSwingChargeGainAtEmpty = 0.45f;
    private const float kSwingChargeGainAtMax = 0.12f;
    private const float kSwingTangentialSpeedMultiplier = 1.25f;
    private const float kSwingBottomAccelerationBoost = 1.35f;
    private const float kSwingLaunchJumpFactor = 0.48f;

    [Header("References")]
    [SerializeField] private Rigidbody m_rb;
    [SerializeField] private GroundCheck m_groundCheck;

    [Header("Movement")]
    [SerializeField] private float m_acceleration = 30f;
    [SerializeField] private float m_airControl = 0.5f;
    [SerializeField] private float m_rotationSpeed = 15f;

    [Header("Run")]
    [SerializeField] private float m_walkSpeed = 3f;
    [SerializeField] private float m_runSpeed = 6f;
    [SerializeField] private float m_speedSmooth = 10f;

    [Header("Damping")]
    [SerializeField] private float m_groundLinearDamping = 8f;
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
    [SerializeField] private float m_swingSteerResponsiveness = 8f;
    [SerializeField] private float m_maxSwingTangentialSpeed = 12f;

    [Header("Climb rope")]
    [SerializeField] private float m_climbSpeed = 2.8f;
    [SerializeField] private float m_climbSnapSpeed = 16f;
    [SerializeField] private float m_climbOffsetFromRope = 0.35f;
    [SerializeField] private float m_climbLinearDamping = 12f;
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
    private Collider m_currentRopeCollider; // exact collider we triggered on
    private ClimbRope m_currentClimbRope;
    private float m_climbParam = 1f;
    private Vector3 m_climbSideOffsetDirection = Vector3.forward;

    // attachment runtime
    private float m_attachParam = 0f; // 0..1 along rope (0=anchor,1=tail)
    private Vector3 m_attachPoint;

    // swing runtime
    private float m_swingCharge = 0f; // 0..maxCharge
    public float SwingChargeNormalized => m_maxCharge > 0f ? Mathf.Clamp01(m_swingCharge / m_maxCharge) : 0f;

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

        if (m_rb != null)
        {
            m_rb.useGravity = true;
            m_rb.linearVelocity = Vector3.zero;
            m_rb.angularVelocity = Vector3.zero;
            m_rb.linearDamping = m_groundLinearDamping;
        }
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
            return;
        }

        ResolveJump();
        if (m_isSwinging)
            HandleSwingPhysics();
        else
            HandleMovement();

        ApplyGravity();
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
        bool actionPressed = m_detachInput != null && m_detachInput.action.WasPressedThisFrame();
        return actionPressed || Input.GetMouseButtonDown(0);
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
        // don't apply standard movement forces while swinging
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
            m_rb.linearDamping = 0f;
            return;
        }

        if (m_isClimbing)
        {
            m_rb.linearDamping = m_climbLinearDamping;
            return;
        }

        m_rb.linearDamping = m_isGrounded ? m_groundLinearDamping : m_airLinearDamping;
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
    }

    private void ApplyGravity()
    {
        // do not apply manual gravity while swinging; Rigidbody.useGravity handles it
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
        // Only handle detach input here. Attach is automatic on trigger enter.
        if (m_isClimbing) return;

        if (m_isSwinging && WasDetachPressed())
        {
            StopSwing(true);
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

        // compute attach point: prefer the exact collider we entered, else closest point on rope
        if (m_currentRopeCollider != null)
        {
            m_attachPoint = m_currentRopeCollider.ClosestPoint(transform.position);
        }
        else
        {
            m_attachPoint = m_currentSwingRope.GetClosestPointOnRope(transform.position);
        }

        // compute param along rope (0..1) using the attach point
        m_attachParam = m_currentSwingRope != null ? m_currentSwingRope.GetClosestPointParam(m_attachPoint) : 0f;

        float currentDistance = Vector3.Distance(m_attachPoint, transform.position);
        float configuredLength = Mathf.Max(0.1f, m_currentSwingRope.RopeLength);

        // choose effective starting length: at least currentDistance to avoid instant snap, but then shrink to configured over a short time
        float startLength = Mathf.Max(currentDistance, configuredLength);

        // pick the rigidbody to connect to: prefer the collider's attached rigidbody, else rope's anchor rb
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
            // connectedAnchor is in connectedBody local space
            m_swingJoint.connectedAnchor = connectRb.transform.InverseTransformPoint(m_attachPoint);
        }
        else
        {
            m_swingJoint.connectedBody = null;
            m_swingJoint.connectedAnchor = m_attachPoint;
        }

        // set joint distances based on chosen start length. If startLength > configuredLength we will shrink it smoothly.
        GetSwingJointSettings(startLength, out float swingSpring, out float swingDamper);
        m_swingJoint.maxDistance = startLength;
        m_swingJoint.minDistance = startLength * kSwingMinDistanceRatio;
        m_swingJoint.spring = swingSpring;
        m_swingJoint.damper = swingDamper;
        m_swingJoint.tolerance = kSwingTolerance;
        m_swingJoint.enableCollision = false;

        // disable rope end colliders to avoid retriggers and start visual follow
        m_currentSwingRope.SetEndCollidersEnabled(false);
        m_currentSwingRope.StartFollow(transform);

        // preserve tangential momentum and remove only the outward radial pull to avoid killing player control.
        Vector3 anchorToPlayer = transform.position - m_attachPoint;
        if (anchorToPlayer.sqrMagnitude > 1e-6f)
        {
            Vector3 radialDir = anchorToPlayer.normalized;
            Vector3 preservedVelocity = m_rb.linearVelocity;
            float outwardSpeed = Mathf.Max(0f, Vector3.Dot(preservedVelocity, radialDir));
            m_rb.linearVelocity = preservedVelocity - radialDir * outwardSpeed;
        }

        // reset charge on attach
        m_swingCharge = 0f;

        // if we started longer than configured, shrink the joint length smoothly to configuredLength
        if (startLength > configuredLength + 0.001f)
        {
            if (m_shrinkRoutine != null) StopCoroutine(m_shrinkRoutine);
            m_shrinkRoutine = StartCoroutine(ShrinkJointRoutine(startLength, configuredLength, kSwingAttachShrinkDuration));
        }
    }

    private IEnumerator ShrinkJointRoutine(float fromLength, float toLength, float duration)
    {
        float t = 0f;
        while (t < duration && m_swingJoint != null)
        {
            t += Time.fixedDeltaTime;
            float alpha = Mathf.SmoothStep(0f, 1f, t / duration);
            float len = Mathf.Lerp(fromLength, toLength, alpha);
            m_swingJoint.maxDistance = len;
            m_swingJoint.minDistance = len * kSwingMinDistanceRatio;
            yield return new WaitForFixedUpdate();
        }
        if (m_swingJoint != null)
        {
            m_swingJoint.maxDistance = toLength;
            m_swingJoint.minDistance = toLength * kSwingMinDistanceRatio;
        }
        m_shrinkRoutine = null;
    }

    private void GetSwingJointSettings(float ropeLength, out float swingSpring, out float swingDamper)
    {
        float length = Mathf.Max(0.5f, ropeLength);
        float baseFactor = 120f; 
        swingSpring = Mathf.Clamp(baseFactor / length, 20f, 2000f);
        swingDamper = Mathf.Clamp(swingSpring * 0.08f, 0.5f, 200f);
    }

    private void HandleSwingPhysics()
    {
        if (!m_isSwinging || m_swingJoint == null || m_currentSwingRope == null || m_rb == null) return;

        Vector2 rawInput = m_moveInput != null ? m_moveInput.action.ReadValue<Vector2>() : Vector2.zero;

        Vector3 anchorPosition = (m_swingJoint.connectedBody != null) ? m_swingJoint.connectedBody.position : (Vector3)m_swingJoint.connectedAnchor;
        Vector3 anchorToPlayer = transform.position - anchorPosition;
        if (anchorToPlayer.sqrMagnitude < 1e-6f) anchorToPlayer = Vector3.down;

        // compute radial and tangential components of velocity
        Vector3 dir = anchorToPlayer.normalized;
        Vector3 vel = m_rb.linearVelocity;
        float radialVel = Vector3.Dot(vel, dir);
        Vector3 tangentialVel = vel - dir * radialVel;
        float tangentialSpeed = tangentialVel.magnitude;

        // attachment factor: if attached closer to tail, provide a bit more force/charge (gameplay tweak)
        float attachFactor = Mathf.Lerp(0.6f, 1.2f, Mathf.Clamp01(m_attachParam));

        float charge01 = SwingChargeNormalized;
        float chargeGainMultiplier = Mathf.Lerp(kSwingChargeGainAtEmpty, kSwingChargeGainAtMax, charge01);
        float chargeGain = tangentialSpeed * m_chargePerSpeed * attachFactor * chargeGainMultiplier * Time.fixedDeltaTime;
        m_swingCharge = Mathf.Clamp(m_swingCharge + chargeGain, 0f, m_maxCharge);

        // steer the tangential velocity toward the requested move direction so the rope feels pilotable.
        if (m_moveDirection.sqrMagnitude > 0.01f)
        {
            Vector3 desiredSwingDirection = Vector3.ProjectOnPlane(m_moveDirection, dir).normalized;
            if (desiredSwingDirection.sqrMagnitude > 0.001f)
            {
                float inputStrength = Mathf.Clamp01(rawInput.magnitude);
                Vector3 currentTangentialDir = tangentialSpeed > 0.01f ? tangentialVel / tangentialSpeed : desiredSwingDirection;
                Vector3 steeredDirection = Vector3.Slerp(
                    currentTangentialDir,
                    desiredSwingDirection,
                    1f - Mathf.Exp(-m_swingSteerResponsiveness * Time.fixedDeltaTime)
                ).normalized;

                float bottomFactor = Mathf.Clamp01(Vector3.Dot(dir, Vector3.down));
                float maxTangentialSpeed = m_maxSwingTangentialSpeed * kSwingTangentialSpeedMultiplier;
                float targetTangentialSpeed = Mathf.Max(
                    tangentialSpeed,
                    Mathf.Lerp(m_runSpeed * 1.1f, maxTangentialSpeed, inputStrength)
                );
                float newTangentialSpeed = Mathf.MoveTowards(
                    tangentialSpeed,
                    targetTangentialSpeed,
                    m_swingForce * attachFactor * Mathf.Lerp(1f, kSwingBottomAccelerationBoost, bottomFactor) * Time.fixedDeltaTime
                );
                newTangentialSpeed = Mathf.Min(newTangentialSpeed, maxTangentialSpeed);

                Vector3 newTangentialVelocity = steeredDirection * newTangentialSpeed;
                m_rb.linearVelocity = dir * radialVel + newTangentialVelocity;
                tangentialVel = newTangentialVelocity;
                tangentialSpeed = newTangentialSpeed;
            }
        }

        // --- Contraintes supplémentaires pour éviter d'aller trop loin et empêcher de passer au-dessus de l'ancre ---
        float currentDist = anchorToPlayer.magnitude;
        float jointMax = (m_swingJoint != null) ? m_swingJoint.maxDistance : m_currentSwingRope.RopeLength;
        float allowedMax = jointMax * kSwingMaxDistanceMultiplier;

        // 1) limiter la distance radiale — correction en douceur via MovePosition
        if (currentDist > allowedMax)
        {
            Vector3 targetPos = anchorPosition + dir * Mathf.Max(allowedMax, kSwingMinDistance);
            Vector3 newPos = Vector3.Lerp(m_rb.position, targetPos, 1f - Mathf.Exp(-kSwingCorrectionSpeed * Time.fixedDeltaTime));
            // damp radial outward velocity gradually
            float radialVelToRemove = Mathf.Max(0f, radialVel) * kSwingRadialDampingFactor;
            Vector3 newVel = m_rb.linearVelocity - dir * radialVelToRemove;
            m_rb.MovePosition(newPos);
            m_rb.linearVelocity = newVel;
            // recalc anchorToPlayer
            anchorToPlayer = (newPos - anchorPosition);
            currentDist = anchorToPlayer.magnitude;
            dir = anchorToPlayer.normalized;
        }

        // 2) empêcher de dépasser l'ancre (ne jamais passer au-dessus) — lissage vertical
        {
            float playerYrelative = m_rb.position.y - anchorPosition.y;
            if (playerYrelative > -kSwingTopClearance)
            {
                // desired y just below anchor
                float targetY = anchorPosition.y - kSwingTopClearance;

                // remove upward velocity smoothly
                Vector3 v = m_rb.linearVelocity;
                v.y = Mathf.Min(v.y, 0f);
                m_rb.linearVelocity = v;

                // gently pull down towards target
                float deltaY = targetY - m_rb.position.y;
                m_rb.AddForce(Vector3.up * (deltaY) * kSwingTopClampSpeed, ForceMode.Acceleration);
            }
        }

        // rotate character towards player input (or tangential motion) while swinging
        Vector3 desiredForward = Vector3.zero;
        if (m_moveDirection.sqrMagnitude > 0.01f)
        {
            desiredForward = Vector3.ProjectOnPlane(m_moveDirection, Vector3.up).normalized;
        }
        else if (tangentialVel.sqrMagnitude > 0.01f)
        {
            desiredForward = Vector3.ProjectOnPlane(tangentialVel.normalized, Vector3.up);
        }

        if (desiredForward.sqrMagnitude > 0.001f)
        {
            Quaternion q = Quaternion.LookRotation(desiredForward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, q, kSwingRotationSpeed * Time.deltaTime);
        }
    }

    private Vector3 GetCurrentSwingAnchorPosition()
    {
        if (m_swingJoint == null) return m_attachPoint;

        if (m_swingJoint.connectedBody != null)
            return m_swingJoint.connectedBody.transform.TransformPoint(m_swingJoint.connectedAnchor);

        return m_swingJoint.connectedAnchor;
    }

    private Vector3 ComputeSwingLaunchVelocity(Vector3 releaseVelocity, Vector3 anchorPosition)
    {
        Vector3 anchorToPlayer = transform.position - anchorPosition;
        if (anchorToPlayer.sqrMagnitude < 1e-6f) anchorToPlayer = Vector3.down;

        Vector3 radialDirection = anchorToPlayer.normalized;
        Vector3 tangentialVelocity = releaseVelocity - radialDirection * Vector3.Dot(releaseVelocity, radialDirection);
        float charge01 = SwingChargeNormalized;

        Vector3 planarDirection = GetCameraPlanarForward();
        Vector3 planarVelocity = Vector3.ProjectOnPlane(tangentialVelocity, Vector3.up);
        if (planarDirection.sqrMagnitude < 0.001f)
        {
            planarDirection = planarVelocity.sqrMagnitude > 0.001f
                ? planarVelocity.normalized
                : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        }

        float forwardMomentum = Mathf.Max(0f, Vector3.Dot(planarVelocity, planarDirection));
        float planarSpeed = m_releaseBaseSpeed
                          + charge01 * m_releaseChargeSpeedBonus
                          + forwardMomentum * (1f + m_releaseMomentumInfluence);

        float releaseHeightFactor = Mathf.Clamp01(Vector3.Dot(radialDirection, Vector3.up) * 0.5f + 0.5f);
        float upwardSpeed = Mathf.Max(
            m_jumpVelocity * kSwingLaunchJumpFactor,
            m_releaseBaseUpwardSpeed
            + charge01 * m_releaseChargeUpwardBonus
            + planarVelocity.magnitude * 0.18f
            + releaseHeightFactor * 1.5f
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
            m_jumpTimer = 0f;
            m_coyoteTimer = 0f;
        }
        else
        {
            m_rb.linearVelocity = launchVelocity;
        }

        // réactiver les colliders aprés un délai configurable pour éviter retriggers immédiats
        if (ropeToReactivate != null)
        {
            StartCoroutine(ReenableEndCollidersCoroutine(ropeToReactivate));
        }

        // clear reference to current swing rope and collider
        m_currentSwingRope = null;
        m_currentRopeCollider = null;

        // reset charge
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

        if (m_shrinkRoutine != null)
        {
            StopCoroutine(m_shrinkRoutine);
            m_shrinkRoutine = null;
        }

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
    }

    private IEnumerator ReenableEndCollidersCoroutine(SwingRope rope)
    {
        yield return new WaitForSeconds(kSwingColliderReenableDelay);
        if (rope != null)
        {
            rope.SetEndCollidersEnabled(true);
        }
    }

    // TRIGGERS for rope detection
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
            m_currentRopeCollider = other; // keep exact collider touched so we can attach to that bone
            Debug.Log("Detecte SwingRope: " + swingRope.name + " (collider=" + other.name + ")");

            // Auto-attach immediately when entering the rope trigger
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

}
