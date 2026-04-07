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

    [Header("Fil")]
    [SerializeField] private GameObject m_filPrefab;
    [SerializeField] private int m_maxFil = 3;

    [Header("Fil Aim")]
    [SerializeField] private LayerMask m_anchorLayer;
    [SerializeField] private float m_maxAimDistance = 20f;

    [Header("Input")]
    [SerializeField] private InputActionReference m_moveInput;
    [SerializeField] private InputActionReference m_runInput;
    [SerializeField] private InputActionReference m_jumpInput;
    [SerializeField] private InputActionReference m_filAimInput;
    [SerializeField] private InputActionReference m_clickInput;

    private List<GameObject> m_spawnedFils = new List<GameObject>();

    private Vector3 m_moveDirection;

    private bool m_isGrounded;
    private bool m_isJumping;
    private bool m_onFil;
    private bool m_isAiming;

    private float m_jumpTimer;
    private float m_currentSpeed;
    private float m_gravity;
    private float m_jumpVelocity;

    private Vector3? m_firstPoint;

    void Start()
    {
        m_moveInput.action.Enable();
        m_runInput.action.Enable();
        m_jumpInput.action.Enable();
        m_filAimInput.action.Enable();
        m_clickInput.action.Enable();

        m_gravity = (-2f * m_jumpHeight) / Mathf.Pow(m_jumpTimeToApex, 2);
        m_jumpVelocity = (2f * m_jumpHeight) / m_jumpTimeToApex;
    }

    void Update()
    {
        HandleInput();
        UpdateSpeed();
        HandleRotation();
        HandleJumpInput();
        HandleFilAim();
    }

    void FixedUpdate()
    {
        CheckGround();
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
        float control = m_isGrounded ? 1f : m_airControl;

        Vector3 targetVelocity = m_moveDirection * m_currentSpeed;
        Vector3 velocity = m_rb.linearVelocity;

        Vector3 velocityChange = (targetVelocity - new Vector3(velocity.x, 0, velocity.z)) * m_acceleration * control;

        m_rb.AddForce(velocityChange, ForceMode.Acceleration);
    }

    private void HandleRotation()
    {
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

    private void ApplyGravity()
    {
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

    // FIL SYSTEM (AIM 2 POINTS)

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
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * m_maxAimDistance, Color.red, 1f);

        if (Physics.Raycast(ray, out hit, m_maxAimDistance, m_anchorLayer))
        {
            if (m_firstPoint == null)
            {
                m_firstPoint = hit.point;
                Debug.Log("Point A sélectionne");
            }
            else
            {
                CreateFil(m_firstPoint.Value, hit.point);
                m_firstPoint = null;

                Debug.Log("Fil cree");
            }
        }
    }

    private void CreateFil(Vector3 start, Vector3 end)
    {
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