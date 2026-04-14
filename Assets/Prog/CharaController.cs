using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharaController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody m_rb;
    [SerializeField] private Animator m_anim;
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
    [SerializeField] private float m_swingSpring = 100f;
    [SerializeField] private float m_swingDamper = 8f;
    [SerializeField] private float m_swingTolerance = 0.02f;
    [SerializeField] [Range(0.1f, 1f)] private float m_swingMinDistanceRatio = 0.9f;

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

    [Header("Swing options")]
    [SerializeField] private bool m_useAnchorRigidbody = true;
    [SerializeField] private bool m_autoTuneSwing = true;
    private enum SwingPreset { Short, Default, Long }
    [SerializeField] private SwingPreset m_swingPreset = SwingPreset.Default;

    [Header("Swing constraints")]
    [Tooltip("Empêche le joueur de passer au-dessus du point d'attache")]
    [SerializeField] private bool m_preventCrossTop = true;
    [Tooltip("Séparation verticale minimale sous l'ancre (si empêché)")]
    [SerializeField] private float m_topClearance = 0.08f;

    [Header("Swing smoothing / correction")]
    [Tooltip("Temps de réduction progressive de la longueur du joint lors de l'attache si le joueur est plus loin que la longueur configurée")]
    [SerializeField] private float m_jointShrinkDuration = 0.18f;
    [Tooltip("Vitesse de correction de position lorsque la contrainte radiale est appliquée (plus haut = moins abrupt)")]
    [SerializeField] private float m_correctionSpeed = 10f;
    [Tooltip("Facteur appliqué pour dissiper progressivement la vitesse radiale sortante (0..1)")]
    [SerializeField] [Range(0f, 1f)] private float m_radialDampingFactor = 0.5f;
    [Tooltip("Permet d'autoriser une petite marge au-dessus de la length configurée (évite le snap)")]
    [SerializeField] private float m_swingMaxDistanceMultiplier = 1.02f;
    [Tooltip("Distance minimale autorisée entre joueur et ancre (évite pénétration)")]
    [SerializeField] private float m_swingMinDistance = 0.2f;
    [Tooltip("Vitesse d'atténuation de la composante verticale lorsqu'on empêche de dépasser l'ancre")]
    [SerializeField] private float m_topClampSpeed = 6f;

    [Header("Swing visual / rotation")]
    [SerializeField] private float m_swingRotationSpeed = 6f;

    [Header("Swing charge (UI)")]
    [Tooltip("Vitesse à laquelle la barre se recharge en fonction de la vitesse tangentielle")]
    [SerializeField] private float m_chargePerSpeed = 0.12f;
    [Tooltip("Vitesse à laquelle la charge diminue quand on ne swing pas")]
    [SerializeField] private float m_chargeDecay = 0.6f;
    [Tooltip("Multiplicateur d'impulsion provenant de la charge au lâcher")]
    [SerializeField] private float m_chargeBoostMultiplier = 6f;
    [Tooltip("Inertie multipliée sur la vitesse tangentielle au lâcher")]
    [SerializeField] private float m_inertiaMultiplier = 1.0f;
    [SerializeField] private float m_maxCharge = 1f;

    [Header("Detach")]
    [Tooltip("Temps en secondes avant de réactiver les colliders de la corde après s'être détaché")]
    [SerializeField] private float m_reenableEndCollidersDelay = 1.0f;

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
    private Transform m_currentRope;            
    private SwingRope m_currentSwingRope;
    private SpringJoint m_swingJoint;
    private Vector3 m_cachedAnchorPosition;
    private Collider m_currentRopeCollider; // exact collider we triggered on
    private ClimbRope m_currentClimbRope;
    private float m_climbParam = 1f;
    private Vector3 m_climbSideOffsetDirection = Vector3.forward;

    // attachment runtime
    private float m_attachParam = 0f; // 0..1 along rope (0=anchor,1=tail)
    private Vector3 m_attachPoint;

    // swing runtime
    private float m_swingCharge = 0f; // 0..1
    public float SwingChargeNormalized => m_swingCharge;

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

        // decay charge if not swinging
        if (!m_isSwinging)
        {
            m_swingCharge = Mathf.Max(0f, m_swingCharge - m_chargeDecay * Time.deltaTime);
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

        Camera cam = Camera.main;
        Vector3 camForward = cam != null ? cam.transform.forward : Vector3.forward;
        Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;

        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        m_moveDirection = (camForward * input.y + camRight * input.x).normalized;
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

        bool detachPressed = false;
        if (m_detachInput != null)
        {
            detachPressed = m_detachInput.action.WasPressedThisFrame();
        }
        else
        {
            detachPressed = Input.GetMouseButtonDown(0);
        }

        if (m_isSwinging && detachPressed)
        {
            StopSwing(true);
        }
    }

    private void HandleClimbInput()
    {
        if (!m_isClimbing) return;

        bool detachPressed = false;
        if (m_detachInput != null)
        {
            detachPressed = m_detachInput.action.WasPressedThisFrame();
        }

        if (detachPressed)
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

        m_isClimbing = false;
        m_currentClimbRope = null;

        if (m_rb != null)
        {
            m_rb.useGravity = true;
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

        if (m_autoTuneSwing)
        {
            ApplySwingPreset(startLength);
        }

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

        if (m_useAnchorRigidbody && connectRb != null && connectRb != m_rb)
        {
            m_swingJoint.connectedBody = connectRb;
            m_swingJoint.autoConfigureConnectedAnchor = false;
            // connectedAnchor is in connectedBody local space
            m_swingJoint.connectedAnchor = connectRb.transform.InverseTransformPoint(m_attachPoint);
        }
        else
        {
            m_swingJoint.connectedBody = null;
            m_swingJoint.autoConfigureConnectedAnchor = false;
            m_swingJoint.connectedAnchor = m_attachPoint;
        }

        // set joint distances based on chosen start length. If startLength > configuredLength we will shrink it smoothly.
        m_swingJoint.maxDistance = startLength;
        m_swingJoint.minDistance = startLength * m_swingMinDistanceRatio;
        m_swingJoint.spring = m_swingSpring;
        m_swingJoint.damper = m_swingDamper;
        m_swingJoint.tolerance = m_swingTolerance;
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
            m_shrinkRoutine = StartCoroutine(ShrinkJointRoutine(startLength, configuredLength, m_jointShrinkDuration));
        }

        Debug.Log($"Swing attach: attachParam={m_attachParam:F2} startLength={startLength:F2} targetLength={configuredLength:F2} spring={m_swingSpring:F1} damper={m_swingDamper:F1} attachedToRb={(m_swingJoint.connectedBody!=null)}");
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
            m_swingJoint.minDistance = len * m_swingMinDistanceRatio;
            yield return new WaitForFixedUpdate();
        }
        if (m_swingJoint != null)
        {
            m_swingJoint.maxDistance = toLength;
            m_swingJoint.minDistance = toLength * m_swingMinDistanceRatio;
        }
        m_shrinkRoutine = null;
    }

    private void ApplySwingPreset(float ropeLength)
    {
        float length = Mathf.Max(0.5f, ropeLength);

        float presetMul = 1f;
        switch (m_swingPreset)
        {
            case SwingPreset.Short: presetMul = 1.5f; break;   // corde courte = ressort plus rigide
            case SwingPreset.Default: presetMul = 1f; break;
            case SwingPreset.Long: presetMul = 0.6f; break;    // corde longue = ressort plus souple
        }

        // regle simple : spring = base / longueur ; damper = spring * facteur
        float baseFactor = 120f; 
        float computedSpring = Mathf.Clamp(baseFactor / length * presetMul, 20f, 2000f);
        float computedDamper = Mathf.Clamp(computedSpring * 0.08f, 0.5f, 200f);

        m_swingSpring = computedSpring;
        m_swingDamper = computedDamper;
    }

    private void HandleSwingPhysics()
    {
        if (!m_isSwinging || m_swingJoint == null || m_currentSwingRope == null || m_rb == null) return;

        // Do NOT overwrite connectedAnchor each frame. If connectedBody == null, connectedAnchor already contains the world attach point.
        if (m_swingJoint.connectedBody == null)
        {
            // cache the world-space anchor used by the joint
            m_cachedAnchorPosition = (Vector3)m_swingJoint.connectedAnchor;
        }

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

        // charge logic: add charge proportionally to tangential speed and attach factor
        m_swingCharge = Mathf.Clamp(m_swingCharge + tangentialSpeed * m_chargePerSpeed * attachFactor * Time.fixedDeltaTime, 0f, m_maxCharge);

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

                float targetTangentialSpeed = Mathf.Max(
                    tangentialSpeed,
                    Mathf.Lerp(m_walkSpeed, m_runSpeed * 1.35f, inputStrength)
                );
                float newTangentialSpeed = Mathf.MoveTowards(
                    tangentialSpeed,
                    targetTangentialSpeed,
                    m_swingForce * attachFactor * Time.fixedDeltaTime
                );
                newTangentialSpeed = Mathf.Min(newTangentialSpeed, m_maxSwingTangentialSpeed);

                Vector3 newTangentialVelocity = steeredDirection * newTangentialSpeed;
                m_rb.linearVelocity = dir * radialVel + newTangentialVelocity;
                tangentialVel = newTangentialVelocity;
                tangentialSpeed = newTangentialSpeed;
            }
        }

        // --- Contraintes supplémentaires pour éviter d'aller trop loin et empêcher de passer au-dessus de l'ancre ---
        float currentDist = anchorToPlayer.magnitude;
        float jointMax = (m_swingJoint != null) ? m_swingJoint.maxDistance : m_currentSwingRope.RopeLength;
        float allowedMax = jointMax * m_swingMaxDistanceMultiplier;

        // 1) limiter la distance radiale — correction en douceur via MovePosition
        if (currentDist > allowedMax)
        {
            Vector3 targetPos = anchorPosition + dir * Mathf.Max(allowedMax, m_swingMinDistance);
            Vector3 newPos = Vector3.Lerp(m_rb.position, targetPos, 1f - Mathf.Exp(-m_correctionSpeed * Time.fixedDeltaTime));
            // damp radial outward velocity gradually
            float radialVelToRemove = Mathf.Max(0f, radialVel) * m_radialDampingFactor;
            Vector3 newVel = m_rb.linearVelocity - dir * radialVelToRemove;
            m_rb.MovePosition(newPos);
            m_rb.linearVelocity = newVel;
            // recalc anchorToPlayer
            anchorToPlayer = (newPos - anchorPosition);
            currentDist = anchorToPlayer.magnitude;
            dir = anchorToPlayer.normalized;
        }

        // 2) empêcher de dépasser l'ancre (ne jamais passer au-dessus) — lissage vertical
        if (m_preventCrossTop)
        {
            float playerYrelative = m_rb.position.y - anchorPosition.y;
            if (playerYrelative > -m_topClearance)
            {
                // desired y just below anchor
                float targetY = anchorPosition.y - m_topClearance;

                // remove upward velocity smoothly
                Vector3 v = m_rb.linearVelocity;
                v.y = Mathf.Min(v.y, 0f);
                m_rb.linearVelocity = v;

                // gently pull down towards target
                float deltaY = targetY - m_rb.position.y;
                m_rb.AddForce(Vector3.up * (deltaY) * m_topClampSpeed, ForceMode.Acceleration);
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
            transform.rotation = Quaternion.Slerp(transform.rotation, q, m_swingRotationSpeed * Time.deltaTime);
        }
    }

    private void StopSwing(bool jumpOff = false)
    {
        if (!m_isSwinging) return;

        // capture current velocity before destroying joint
        Vector3 releaseVelocity = m_rb != null ? m_rb.linearVelocity : Vector3.zero;

        // stop visual follow immediately
        SwingRope ropeToReactivate = m_currentSwingRope;
        if (ropeToReactivate != null)
        {
            ropeToReactivate.StopFollow();
        }

        m_isSwinging = false;

        // destroy joint
        if (m_swingJoint != null)
        {
            Destroy(m_swingJoint);
            m_swingJoint = null;
        }

        // reapply velocity captured
        if (m_rb != null)
        {
            m_rb.linearVelocity = releaseVelocity;
        }

        if (jumpOff)
        {
            // compute radial and tangential components relative to attach point at release moment
            Vector3 anchorPosition = (ropeToReactivate != null && ropeToReactivate.AnchorRb != null) ? (ropeToReactivate.AnchorRb.position) :
                                     (ropeToReactivate != null ? m_attachPoint : transform.position);
            Vector3 anchorToPlayer = transform.position - anchorPosition;
            if (anchorToPlayer.sqrMagnitude < 1e-6f) anchorToPlayer = Vector3.down;
            Vector3 dir = anchorToPlayer.normalized;
            Vector3 vel = m_rb != null ? m_rb.linearVelocity : Vector3.zero;
            float radialVel = Vector3.Dot(vel, dir);
            Vector3 tangentialVel = vel - dir * radialVel;
            float tangentialSpeed = tangentialVel.magnitude;
            Vector3 tangentialDir = tangentialSpeed > 1e-4f ? tangentialVel.normalized : (transform.forward + Vector3.up).normalized;

            // compute impulse: inherit tangential inertia + charge boost + input direction influence + upward bias
            Vector3 inertiaImpulse = tangentialDir * (tangentialSpeed * m_inertiaMultiplier);
            Vector3 chargeImpulse = tangentialDir * (m_swingCharge * m_chargeBoostMultiplier);

            // add some influence from player input direction
            Vector3 inputDirWorld = (m_moveDirection.sqrMagnitude > 0.01f) ? m_moveDirection.normalized : Vector3.zero;
            Vector3 inputImpulse = Vector3.zero;
            if (inputDirWorld.sqrMagnitude > 0.01f)
            {
                inputImpulse = inputDirWorld * (m_swingCharge * m_chargeBoostMultiplier * 0.6f + tangentialSpeed * 0.4f);
            }

            // upward bias: scaled with charge and also with the vertical component of tangential direction
            Vector3 upwardBias = Vector3.up * (m_swingCharge * m_chargeBoostMultiplier * 0.6f + Mathf.Max(0f, tangentialDir.y) * tangentialSpeed * 0.6f);

            Vector3 totalImpulse = inertiaImpulse + chargeImpulse + inputImpulse + upwardBias;

            if (m_rb != null)
            {
                m_rb.AddForce(totalImpulse, ForceMode.VelocityChange);
            }

            m_isJumping = true;
            m_jumpTimer = 0f;
        }

        // réactiver les colliders aprés un délai configurable pour éviter retriggers immédiats
        if (ropeToReactivate != null)
        {
            StartCoroutine(ReenableEndCollidersCoroutine(m_reenableEndCollidersDelay, ropeToReactivate));
        }

        // clear reference to current swing rope and collider
        m_currentSwingRope = null;
        m_currentRopeCollider = null;

        // reset charge
        m_swingCharge = 0f;
    }

    private IEnumerator ReenableEndCollidersCoroutine(float delay, SwingRope rope)
    {
        yield return new WaitForSeconds(delay);
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

        if (other.CompareTag("SwingRope"))
        {
            m_currentRope = other.transform;
            Debug.Log("Detecte SwingRope trigger: " + other.name);
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

        if (other.CompareTag("SwingRope") && other.transform == m_currentRope)
        {
            m_currentRope = null;
            Debug.Log("Left SwingRope: " + other.name);
        }

        if (other == m_currentRopeCollider)
        {
            m_currentRopeCollider = null;
        }
    }

}
