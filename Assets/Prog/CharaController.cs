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

    [Header("Balance")]
    [SerializeField] private float m_balanceSpeed = 2f;
    [SerializeField] private float m_balanceForce = 5f;
    [SerializeField] private float m_maxBalanceAngle = 30f;

    [Header("Climb")]
    [SerializeField] private float m_climbSpeed = 3f;
    [SerializeField] private float m_climbJumpForce = 5f;

    [Header("Swing")]
    [SerializeField] private float m_swingForce = 20f;
    [SerializeField] private float m_swingReleaseBoost = 2f;

    [Header("Fil")]
    [SerializeField] private GameObject m_filPrefab;
    [SerializeField] private int m_maxFil = 3;

    [Header("Inventory")]
    [SerializeField] private bool m_startWithScissors = true;
    [SerializeField] private int m_startThreadCount = 1;
    [SerializeField] private int m_maxThreadCount = 5;

    [Header("Fil Aim")]
    [SerializeField] private LayerMask m_anchorLayer;
    [SerializeField] private float m_maxAimDistance = 20f;
    [SerializeField] private float m_pullForce = 20f;
    [SerializeField] private float m_aimTurnSpeed = 20f;

    [Header("Input")]
    [SerializeField] private InputActionReference m_moveInput;
    [SerializeField] private InputActionReference m_runInput;
    [SerializeField] private InputActionReference m_jumpInput;
    [SerializeField] private InputActionReference m_filAimInput;
    [SerializeField] private InputActionReference m_clickInput;

    private List<GameObject> m_spawnedFils = new List<GameObject>();

    private bool m_isGrounded;
    private bool m_isJumping;
    private bool m_onFil;
    private bool m_isAiming;
    private bool m_isClimbing;
    private bool m_isSwinging;

    private float m_jumpTimer;
    private float m_currentSpeed;
    private float m_gravity;
    private float m_jumpVelocity;
    private float m_balance;
    private float m_balanceVelocity;

    private Vector3 m_moveDirection;
    private Vector3? m_firstPoint;

    private Transform m_currentRope;
    private SwingRope m_currentSwingRope;
    private SpringJoint m_swingJoint;

    public bool HasScissors { get; private set; }
    public int ThreadCount { get; private set; }

    void Start()
    {
        m_moveInput.action.Enable();
        m_runInput.action.Enable();
        m_jumpInput.action.Enable();
        m_filAimInput.action.Enable();
        m_clickInput.action.Enable();

        m_gravity = (-2f * m_jumpHeight) / Mathf.Pow(m_jumpTimeToApex, 2);
        m_jumpVelocity = (2f * m_jumpHeight) / m_jumpTimeToApex;

        HasScissors = m_startWithScissors;
        ThreadCount = Mathf.Clamp(m_startThreadCount, 0, m_maxThreadCount);
    }

    void Update()
    {
        HandleInput();
        HandleSwing();
        UpdateSpeed();
        HandleRotation();
        HandleJumpInput();
        HandleClimb();
        HandleFilAim();
    }

    void FixedUpdate()
    {
        CheckGround();
        HandleSwingPhysics();
        HandleMovement();
        ApplyGravity();
    }

    // INPUT

    private void HandleInput()
    {
        Vector2 input = m_moveInput.action.ReadValue<Vector2>();

        Vector3 camForward = Camera.main.transform.forward;
        Vector3 camRight = Camera.main.transform.right;

        camForward.y = 0;
        camRight.y = 0;

        m_moveDirection = (camForward.normalized * input.y + camRight.normalized * input.x).normalized;
    }

    // MOVEMENT

    private void HandleMovement()
    {
        if (m_isClimbing || m_isSwinging)
            return;

        float control = m_isGrounded ? 1f : m_airControl;

        Vector3 targetVelocity = m_moveDirection * m_currentSpeed;
        Vector3 velocity = m_rb.linearVelocity;

        Vector3 velocityChange = (targetVelocity - new Vector3(velocity.x, 0, velocity.z)) * m_acceleration * control;

        m_rb.AddForce(velocityChange, ForceMode.Acceleration);
    }

    private void HandleRotation()
    {
        if (m_isSwinging)
            return;

        if (m_isAiming)
        {
            RotateTowardsAim();
            return;
        }

        if (m_moveDirection.sqrMagnitude < 0.01f)
            return;

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

        bool isRunning = m_runInput.action.IsPressed() && m_moveDirection.sqrMagnitude > 0.1f;

        float targetSpeed = isRunning ? m_runSpeed : m_walkSpeed;

        if (m_onFil)
        {
            targetSpeed *= 0.5f;
        }

        m_currentSpeed = Mathf.Lerp(m_currentSpeed, targetSpeed, Time.deltaTime * m_speedSmooth);
    }

    // JUMP

    private void HandleJumpInput()
    {
        if (m_isClimbing || m_isSwinging)
            return;

        if (m_jumpInput.action.WasPressedThisFrame() && m_isGrounded)
        {
            Jump();
        }

        if (m_jumpInput.action.IsPressed() && m_isJumping)
        {
            m_jumpTimer += Time.deltaTime;

            if (m_jumpTimer < m_maxJumpHoldTime)
            {
                m_rb.AddForce(Vector3.up * m_jumpVelocity * 0.5f, ForceMode.Acceleration);
            }
        }

        if (m_jumpInput.action.WasReleasedThisFrame())
        {
            m_isJumping = false;
        }
    }

    private void Jump()
    {
        Vector3 velocity = m_rb.linearVelocity;
        velocity.y = 0;
        m_rb.linearVelocity = velocity;

        m_rb.AddForce(Vector3.up * m_jumpVelocity, ForceMode.VelocityChange);

        m_isJumping = true;
        m_jumpTimer = 0;
    }

    private void HandleBalance()
{
        if (!m_onFil)
        {
            m_balance = 0;
            return;
        }

        m_balance += Random.Range(-1f, 1f) * m_balanceSpeed * Time.deltaTime;
        float input = m_moveInput.action.ReadValue<Vector2>().x;
        m_balance -= input * m_balanceForce * Time.deltaTime;
        m_balance = Mathf.Clamp(m_balance, -m_maxBalanceAngle, m_maxBalanceAngle);
        transform.localRotation = Quaternion.Euler(0, transform.eulerAngles.y, m_balance);

        if (Mathf.Abs(m_balance) >= m_maxBalanceAngle)
        {
            FallOffFil();
        }
    }

    private void ApplyGravity()
    {
        if (m_isClimbing || m_isSwinging)
            return;

        if (m_rb.linearVelocity.y < 0)
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
        m_isGrounded = m_groundCheck.GetIsHitting();

        if (m_isGrounded)
        {
            m_isJumping = false;
        }
    }

    // FIL SYSTEM

    private void HandleFilAim()
    {
        m_isAiming = m_filAimInput.action.IsPressed();

        if (!m_isAiming)
        {
            m_firstPoint = null;
            return;
        }

        if (m_clickInput.action.WasPressedThisFrame())
        {
            TrySelectPoint();
        }
    }

    private void TrySelectPoint()
    {
        Ray ray = GetAimRay();
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * m_maxAimDistance, Color.red, 1f);

        if (Physics.Raycast(ray, out hit, m_maxAimDistance))
        {
            CuttableThread cuttableThread = hit.collider.GetComponentInParent<CuttableThread>();
            if (cuttableThread != null && cuttableThread.CanBeCut)
            {
                if (cuttableThread.Cut(this))
                {
                    Debug.Log("Fil coupe et recupere");
                }
                else if (!HasScissors)
                {
                    Debug.Log("Il faut les ciseaux pour couper ce fil");
                }

                return;
            }
        }

        if (Physics.Raycast(ray, out hit, m_maxAimDistance, m_anchorLayer))
        {
            if (m_firstPoint == null)
            {
                m_firstPoint = hit.point;
                Debug.Log("Point A s�lectionne");
            }
            else
            {
                CreateFil(m_firstPoint.Value, hit.point);
                m_firstPoint = null;

                Debug.Log("Fil cree");
            }
        }
        if (Physics.Raycast(ray, out hit, m_maxAimDistance))
        {
            PullableObject pullableObject = hit.collider.GetComponent<PullableObject>();
            if (pullableObject != null)
            {
                pullableObject.Pull(transform.position, m_pullForce);
                return;
            }
            if (((1 << hit.collider.gameObject.layer) & m_anchorLayer) != 0)
            {
                if (m_firstPoint == null)
                {
                    m_firstPoint = hit.point;
                }
                else
                {
                    CreateFil(m_firstPoint.Value, hit.point);
                    m_firstPoint = null;
                }
            }
        }
    }

    private void CreateFil(Vector3 start, Vector3 end)
    {
        if (!TryUseThread())
        {
            Debug.Log("Plus de fil dans l'inventaire");
            return;
        }

        GameObject filObj = Instantiate(m_filPrefab);

        Fil fil = filObj.GetComponent<Fil>();
        fil.Init(start, end);

        if (m_spawnedFils.Count >= m_maxFil)
        {
            Destroy(m_spawnedFils[0]);
            m_spawnedFils.RemoveAt(0);
        }

        m_spawnedFils.Add(filObj);
    }

    private void FallOffFil()
    {
        m_onFil = false;

        m_rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);

        Debug.Log("Le moine tombe du fil !");
    }

    private void HandleSwing()
    {
        bool jumpPressed = m_jumpInput.action.WasPressedThisFrame();

        if (!m_isSwinging)
        {
            if (m_currentSwingRope != null && jumpPressed)
            {
                StartSwing();
            }

            return;
        }

        if (jumpPressed)
        {
            StopSwing(true);
        }
    }

    private void HandleSwingPhysics()
    {
        if (!m_isSwinging || m_swingJoint == null || m_currentSwingRope == null)
            return;

        Vector3 anchorPosition = m_currentSwingRope.AnchorPosition;
        Vector3 ropeDirection = (transform.position - anchorPosition).normalized;
        Vector3 tangentDirection = Vector3.ProjectOnPlane(m_moveDirection, ropeDirection).normalized;

        if (tangentDirection.sqrMagnitude > 0.001f)
        {
            m_rb.AddForce(tangentDirection * m_swingForce, ForceMode.Acceleration);
        }
    }

    private void StartSwing()
    {
        if (m_isClimbing)
        {
            StopClimb();
        }

        m_isSwinging = true;
        m_isJumping = false;
        m_jumpTimer = 0f;
        m_rb.useGravity = true;

        if (m_swingJoint == null)
        {
            m_swingJoint = gameObject.AddComponent<SpringJoint>();
        }

        Vector3 anchorPosition = m_currentSwingRope.AnchorPosition;
        Vector3 anchorToPlayer = transform.position - anchorPosition;

        if (anchorToPlayer.sqrMagnitude < 0.001f)
        {
            anchorToPlayer = Vector3.down;
        }

        float ropeLength = Mathf.Max(0.5f, m_currentSwingRope.RopeLength);

        transform.position = anchorPosition + anchorToPlayer.normalized * ropeLength;

        m_swingJoint.autoConfigureConnectedAnchor = false;
        m_swingJoint.connectedBody = null;
        m_swingJoint.connectedAnchor = anchorPosition;
        m_swingJoint.maxDistance = ropeLength;
        m_swingJoint.minDistance = ropeLength * 0.95f;
        m_swingJoint.spring = 0f;
        m_swingJoint.damper = 0f;
        m_swingJoint.tolerance = 0.02f;
        m_swingJoint.enableCollision = false;
    }

    private void StopSwing(bool jumpOff = false)
    {
        if (!m_isSwinging)
            return;

        Vector3 releaseVelocity = m_rb.linearVelocity;

        m_isSwinging = false;

        if (m_swingJoint != null)
        {
            Destroy(m_swingJoint);
            m_swingJoint = null;
        }

        if (!jumpOff)
            return;

        Vector3 releaseDirection = releaseVelocity.sqrMagnitude > 0.01f
            ? releaseVelocity.normalized
            : (transform.forward + Vector3.up).normalized;

        m_rb.linearVelocity = releaseVelocity + releaseDirection * m_swingReleaseBoost;
        m_isJumping = true;
        m_jumpTimer = 0f;
    }

    // FIL CLIMB

    private void OnTriggerEnter(Collider other)
    {
        SwingRope swingRope = other.GetComponentInParent<SwingRope>();
        if (swingRope != null)
        {
            m_currentSwingRope = swingRope;
        }

        if (other.CompareTag("Fil"))
        {
            m_currentRope = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        SwingRope swingRope = other.GetComponentInParent<SwingRope>();
        if (swingRope != null && swingRope == m_currentSwingRope)
        {
            m_currentSwingRope = null;

            if (m_isSwinging)
            {
                StopSwing();
            }
        }

        if (other.CompareTag("Fil") && other.transform == m_currentRope)
        {
            m_currentRope = null;

            if (m_isClimbing)
            {
                StopClimb();
            }
        }
    }

    private void HandleClimb()
    {
        if (m_isSwinging)
            return;

        if (m_currentRope == null)
            return;

        bool jumpPressed = m_jumpInput.action.WasPressedThisFrame();

        if (!m_isClimbing)
        {
            if (jumpPressed)
            {
                StartClimb();
            }

            return;
        }

        float vertical = m_moveInput.action.ReadValue<Vector2>().y;
        Vector3 move = Vector3.up * vertical * m_climbSpeed;

        m_rb.linearVelocity = move;

        Vector3 ropePos = m_currentRope.position;
        transform.position = new Vector3(ropePos.x, transform.position.y, ropePos.z);

        if (jumpPressed)
        {
            StopClimb(true);
        }
    }

    private void StartClimb()
    {
        m_isClimbing = true;
        m_rb.useGravity = false;
        m_rb.linearVelocity = Vector3.zero;
    }

    private void StopClimb(bool jumpOff = false)
    {
        m_isClimbing = false;
        m_rb.useGravity = true;

        if (!jumpOff)
            return;

        Vector3 jumpDirection = Vector3.up;

        if (m_moveDirection.sqrMagnitude > 0.01f)
        {
            jumpDirection = (Vector3.up + m_moveDirection).normalized;
        }

        m_rb.linearVelocity = Vector3.zero;
        m_rb.AddForce(jumpDirection * m_climbJumpForce, ForceMode.VelocityChange);
        m_isJumping = true;
        m_jumpTimer = 0f;
    }

    public void GiveScissors()
    {
        if (HasScissors)
            return;

        HasScissors = true;
        Debug.Log("Ciseaux recuperes");
    }

    public void AddThreads(int amount)
    {
        if (amount <= 0)
            return;

        int previousCount = ThreadCount;
        ThreadCount = Mathf.Clamp(ThreadCount + amount, 0, m_maxThreadCount);

        if (ThreadCount > previousCount)
        {
            Debug.Log("Fils en stock : " + ThreadCount);
        }
    }

    private bool TryUseThread()
    {
        if (ThreadCount <= 0)
            return false;

        ThreadCount--;
        Debug.Log("Fil utilise. Stock restant : " + ThreadCount);
        return true;
    }

    private void RotateTowardsAim()
    {
        if (!TryGetAimPoint(out Vector3 aimPoint))
            return;

        Vector3 lookDirection = aimPoint - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            m_aimTurnSpeed * Time.deltaTime
        );
    }

    private Ray GetAimRay()
    {
        Camera currentCamera = Camera.main;
        if (currentCamera == null)
        {
            return new Ray(transform.position + Vector3.up, transform.forward);
        }

        return currentCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    }

    private bool TryGetAimPoint(out Vector3 aimPoint)
    {
        Ray ray = GetAimRay();

        if (Physics.Raycast(ray, out RaycastHit hit, m_maxAimDistance))
        {
            aimPoint = hit.point;
            return true;
        }

        aimPoint = ray.origin + ray.direction * m_maxAimDistance;
        return true;
    }

    // FIL COLLISION

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Fil"))
        {
            m_onFil = true;
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Fil"))
        {
            m_onFil = false;
        }
    }
}
