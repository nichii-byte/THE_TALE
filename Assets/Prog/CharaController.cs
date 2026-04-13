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

    [Header("Jump")]
    [SerializeField] private float m_jumpHeight = 2.5f;
    [SerializeField] private float m_jumpTimeToApex = 0.4f;
    [SerializeField] private float m_maxJumpHoldTime = 0.25f;
    [SerializeField] private float m_fallMultiplier = 2.5f;

    [Header("Swing (SpringJoint)")]
    [SerializeField] private float m_swingForce = 20f;
    [SerializeField] private float m_swingReleaseBoost = 4f;

    [SerializeField] private float m_swingSpring = 100f;
    [SerializeField] private float m_swingDamper = 8f;
    [SerializeField] private float m_swingTolerance = 0.02f;
    [SerializeField] [Range(0.1f, 1f)] private float m_swingMinDistanceRatio = 0.9f;

    [Header("Swing options")]
    [SerializeField] private bool m_useAnchorRigidbody = true;
    [SerializeField] private bool m_autoTuneSwing = true;
    private enum SwingPreset { Short, Default, Long }
    [SerializeField] private SwingPreset m_swingPreset = SwingPreset.Default;

    [Header("Swing constraints")]
    [Tooltip("Permet d'autoriser une petite marge au-dessus de la length configurée (évite le snap)")]
    [SerializeField] private float m_swingMaxDistanceMultiplier = 1.02f;
    [Tooltip("Distance minimale autorisée entre joueur et ancre (évite pénétration)")]
    [SerializeField] private float m_swingMinDistance = 0.2f;
    [Tooltip("Empêche le joueur de passer au-dessus du point d'attache")]
    [SerializeField] private bool m_preventCrossTop = true;
    [Tooltip("Séparation verticale minimale sous l'ancre (si empêché)")]
    [SerializeField] private float m_topClearance = 0.08f;

    [Header("Swing smoothing / correction")]
    [Tooltip("Vitesse de correction de position lorsque la contrainte est appliquée (plus haut = moins abrupt)")]
    [SerializeField] private float m_correctionSpeed = 10f;
    [Tooltip("Facteur appliqué pour dissiper progressivement la vitesse radiale sortante (0..1)")]
    [SerializeField] [Range(0f, 1f)] private float m_radialDampingFactor = 0.5f;
    [Tooltip("Vitesse d'atténuation de la composante verticale lorsqu'on empêche de dépasser l'ancre")]
    [SerializeField] private float m_topClampSpeed = 6f;

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

    [Header("Input")]
    [SerializeField] private InputActionReference m_moveInput;
    [SerializeField] private InputActionReference m_runInput;
    [SerializeField] private InputActionReference m_jumpInput;

    // States
    private bool m_isGrounded;
    private bool m_isJumping;
    private bool m_isClimbing;
    private bool m_isSwinging;

    // physics & movement
    private float m_jumpTimer; 
    private float m_currentSpeed;
    private float m_gravity;
    private float m_jumpVelocity;
    private Vector3 m_moveDirection;

    // rope / swing
    private Transform m_currentRope;            
    private SwingRope m_currentSwingRope;
    private SpringJoint m_swingJoint;
    private Vector3 m_cachedAnchorPosition;

    // swing runtime
    private float m_swingCharge = 0f; // 0..1
    public float SwingChargeNormalized => m_swingCharge;

    private void Start()
    {
        if (m_moveInput != null) m_moveInput.action.Enable();
        if (m_runInput != null) m_runInput.action.Enable();
        if (m_jumpInput != null) m_jumpInput.action.Enable();

        m_gravity = (-2f * m_jumpHeight) / Mathf.Pow(m_jumpTimeToApex, 2);
        m_jumpVelocity = (2f * m_jumpHeight) / m_jumpTimeToApex;

        if (m_rb == null) Debug.LogError("CharaController: Rigidbody reference is missing.");
        if (m_groundCheck == null) Debug.LogWarning("CharaController: GroundCheck missing - ground detection will fail.");
    }

    private void Update()
    {
        HandleInput();
        HandleSwingInput();
        UpdateSpeed();
        HandleRotation();
        HandleJumpInput();
        SetDamping();

        // decay charge if not swinging
        if (!m_isSwinging)
        {
            m_swingCharge = Mathf.Max(0f, m_swingCharge - m_chargeDecay * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        CheckGround();
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
        if (m_isClimbing || m_isSwinging) return;

        float control = m_isGrounded ? 1f : m_airControl;

        Vector3 targetVelocity = m_moveDirection * m_currentSpeed;

        Vector3 velocity = m_rb != null ? m_rb.linearVelocity : Vector3.zero;

        Vector3 velocityChange = (targetVelocity - new Vector3(velocity.x, 0f, velocity.z)) * m_acceleration * control;

        if (m_rb != null) m_rb.AddForce(velocityChange, ForceMode.Acceleration);
    }

    private void HandleRotation()
    {
        if (m_isSwinging) return;

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
        if (m_isSwinging)
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
        m_rb.linearDamping = m_isSwinging ? 0f : 20f;
    }

    // JUMP
    private void HandleJumpInput()
    {
        if (m_isClimbing) return;
        if (m_jumpInput == null) return;

        if (m_jumpInput.action.WasPressedThisFrame() && m_isGrounded && !m_isSwinging)
        {
            Jump();
        }

        if (m_jumpInput.action.IsPressed() && m_isJumping)
        {
            m_jumpTimer += Time.deltaTime;
            if (m_jumpTimer < m_maxJumpHoldTime)
            {
                if (m_rb != null) m_rb.AddForce(Vector3.up * m_jumpVelocity * 0.5f, ForceMode.Acceleration);
            }
        }

        if (m_jumpInput.action.WasReleasedThisFrame())
        {
            m_isJumping = false;
        }
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
        if (m_isClimbing || m_isSwinging) return;
        if (m_rb == null) return;

        if (m_rb.linearVelocity.y < 0f)
        {
            m_rb.AddForce(Vector3.up * m_gravity * m_fallMultiplier, ForceMode.Acceleration);
        }
        else
        {
            m_rb.AddForce(Vector3.up * m_gravity, ForceMode.Acceleration);
        }
    }

    private void CheckGround()
    {
        if (m_groundCheck == null) { m_isGrounded = false; return; }

        m_isGrounded = m_groundCheck.GetIsHitting();
        if (m_isGrounded) m_isJumping = false;
    }

    // SWING (SpringJoint)  
    private void HandleSwingInput()
    {
        if (m_jumpInput == null) return;

        bool jumpPressed = m_jumpInput.action.WasPressedThisFrame();

        if (!m_isSwinging)
        {
            if (m_currentSwingRope != null && jumpPressed)
            {
                StartSwingWithJoint();
            }
            return;
        }

        if (jumpPressed)
        {
            StopSwing(true);
        }
    }

    private void StartSwingWithJoint()
    {
        if (m_isSwinging || m_currentSwingRope == null || m_rb == null) return;

        m_isSwinging = true;
        m_isJumping = false;
        m_jumpTimer = 0f;
        m_rb.useGravity = true;

        if (m_swingJoint == null)
        {
            m_swingJoint = gameObject.AddComponent<SpringJoint>();
        }

        // anchor world pos and current distance
        Vector3 anchorWorldPos = m_currentSwingRope.AnchorPosition;
        float currentDistance = Vector3.Distance(anchorWorldPos, transform.position);
        float configuredLength = Mathf.Max(0.5f, m_currentSwingRope.RopeLength);

        // choose effective length: at least currentDistance to avoid snapping inside anchor
        float ropeLength = Mathf.Max(configuredLength, currentDistance);

        if (m_autoTuneSwing)
        {
            ApplySwingPreset(ropeLength);
        }

        Rigidbody anchorRb = m_currentSwingRope.AnchorRb;
        if (m_useAnchorRigidbody && anchorRb != null && anchorRb != m_rb)
        {
            m_swingJoint.connectedBody = anchorRb;
            m_swingJoint.autoConfigureConnectedAnchor = false;
            // connectedAnchor is in connectedBody local space
            m_swingJoint.connectedAnchor = anchorRb.transform.InverseTransformPoint(anchorWorldPos);
        }
        else
        {
            m_swingJoint.connectedBody = null;
            m_swingJoint.autoConfigureConnectedAnchor = false;
            m_swingJoint.connectedAnchor = anchorWorldPos;
        }

        m_swingJoint.maxDistance = ropeLength;
        m_swingJoint.minDistance = ropeLength * m_swingMinDistanceRatio;
        m_swingJoint.spring = m_swingSpring;
        m_swingJoint.damper = m_swingDamper;
        m_swingJoint.tolerance = m_swingTolerance;
        m_swingJoint.enableCollision = false;

        // disable rope end colliders to avoid retriggers and start visual follow
        m_currentSwingRope.SetEndCollidersEnabled(false);
        m_currentSwingRope.StartFollow(transform);

        // zero player linear velocity for consistent attach
        m_rb.linearVelocity = Vector3.zero;

        // reset charge on attach
        m_swingCharge = 0f;

        Debug.Log($"Swing attach: length={ropeLength:F2} spring={m_swingSpring:F1} damper={m_swingDamper:F1}");
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

        if (m_swingJoint.connectedBody == null)
        {
            m_cachedAnchorPosition = m_currentSwingRope.AnchorPosition;
            m_swingJoint.connectedAnchor = m_cachedAnchorPosition;
        }

        Vector2 rawInput = m_moveInput != null ? m_moveInput.action.ReadValue<Vector2>() : Vector2.zero;

        Vector3 anchorPosition = (m_swingJoint.connectedBody != null) ? m_swingJoint.connectedBody.position : m_swingJoint.connectedAnchor;
        Vector3 anchorToPlayer = transform.position - anchorPosition;
        if (anchorToPlayer.sqrMagnitude < 1e-6f) anchorToPlayer = Vector3.down;

        // compute radial and tangential components of velocity
        Vector3 dir = anchorToPlayer.normalized;
        Vector3 vel = m_rb.linearVelocity;
        float radialVel = Vector3.Dot(vel, dir);
        Vector3 tangentialVel = vel - dir * radialVel;
        float tangentialSpeed = tangentialVel.magnitude;

        // charge logic: add charge proportionally to tangential speed
        m_swingCharge = Mathf.Clamp(m_swingCharge + tangentialSpeed * m_chargePerSpeed * Time.fixedDeltaTime, 0f, m_maxCharge);

        // apply player input as tangential force to increase swing amplitude (unchanged)
        Camera cam = Camera.main;
        Vector3 camForward = cam != null ? cam.transform.forward : Vector3.forward;
        Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;

        Vector3 forwardSwing = Vector3.Cross(anchorToPlayer, camRight).normalized;
        Vector3 rightSwing = Vector3.Cross(anchorToPlayer, camForward).normalized;

        Vector3 tangentForce = (-rightSwing * m_swingForce * rawInput.x) + (forwardSwing * m_swingForce * rawInput.y);
        m_rb.AddForce(tangentForce, ForceMode.Acceleration);

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
                Vector3 targetPos = new Vector3(m_rb.position.x, targetY, m_rb.position.z);

                // ensure horizontal radial distance preserved by projecting onto circle at targetY
                float horizontalDist = Mathf.Sqrt(Mathf.Max(0f, (currentDist * currentDist) - (anchorPosition.y - m_rb.position.y) * (anchorPosition.y - m_rb.position.y)));
                if (horizontalDist > 0.001f)
                {
                    Vector3 horizontalDir = (new Vector3(m_rb.position.x - anchorPosition.x, 0f, m_rb.position.z - anchorPosition.z)).normalized;
                    targetPos.x = anchorPosition.x + horizontalDir.x * horizontalDist;
                    targetPos.z = anchorPosition.z + horizontalDir.z * horizontalDist;
                }

                Vector3 newPos = Vector3.Lerp(m_rb.position, targetPos, 1f - Mathf.Exp(-m_topClampSpeed * Time.fixedDeltaTime));
                // remove upward velocity smoothly
                Vector3 v = m_rb.linearVelocity;
                v.y = Mathf.Min(v.y, 0f);
                m_rb.MovePosition(newPos);
                m_rb.linearVelocity = v;
            }
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
            // compute radial and tangential components relative to anchor at release moment
            Vector3 anchorPosition = (ropeToReactivate != null && ropeToReactivate.AnchorRb != null) ? (ropeToReactivate.AnchorRb.position) :
                                     (ropeToReactivate != null ? ropeToReactivate.AnchorPosition : transform.position);
            Vector3 anchorToPlayer = transform.position - anchorPosition;
            if (anchorToPlayer.sqrMagnitude < 1e-6f) anchorToPlayer = Vector3.down;
            Vector3 dir = anchorToPlayer.normalized;
            Vector3 vel = m_rb != null ? m_rb.linearVelocity : Vector3.zero;
            float radialVel = Vector3.Dot(vel, dir);
            Vector3 tangentialVel = vel - dir * radialVel;
            float tangentialSpeed = tangentialVel.magnitude;
            Vector3 tangentialDir = tangentialSpeed > 1e-4f ? tangentialVel.normalized : (transform.forward + Vector3.up).normalized;

            // compute impulse: inherit tangential inertia + charge boost + upward bias
            Vector3 inertiaImpulse = tangentialDir * (tangentialSpeed * m_inertiaMultiplier);
            Vector3 chargeImpulse = tangentialDir * (m_swingCharge * m_chargeBoostMultiplier);
            Vector3 upwardBias = Vector3.up * (m_swingCharge * 1.2f); // small upward boost scaled with charge

            Vector3 totalImpulse = inertiaImpulse + chargeImpulse + upwardBias;

            if (m_rb != null)
            {
                m_rb.AddForce(totalImpulse, ForceMode.VelocityChange);
            }

            m_isJumping = true;
            m_jumpTimer = 0f;
        }

        // réactiver les colliders aprés un petit délai pour éviter retriggers immédiats
        if (ropeToReactivate != null)
        {
            StartCoroutine(ReenableEndCollidersCoroutine(0.25f, ropeToReactivate));
        }

        // clear reference to current swing rope
        m_currentSwingRope = null;

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

        SwingRope swingRope = other.GetComponentInParent<SwingRope>();
        if (swingRope == null) swingRope = other.GetComponentInChildren<SwingRope>();

        if (swingRope != null)
        {
            m_currentSwingRope = swingRope;
            Debug.Log("Detecte SwingRope: " + swingRope.name);
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
    }

}
