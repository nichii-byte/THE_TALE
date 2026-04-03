using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

public class CharaController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody m_rb;
    [SerializeField] private Animator m_anim;
    [SerializeField] private GroundCheck m_groundCheck;
    [SerializeField] private Transform m_spawnPoint;

    [Header("Values")]
    [SerializeField] private float m_acceleration = 10f;
    [SerializeField] private float m_maxSpeed = 20f;
    [SerializeField] private float m_dampingOnStop = 10f;
    [SerializeField] private float m_jumpForce = 20;
    [SerializeField] private float m_maxJumpDuration = 1;
    [SerializeField] private float m_fallGravity = 2;

    [Header("Input")]
    [SerializeField] private InputActionReference m_moveInput;
    [SerializeField] private InputActionReference m_jumpInput;

    private Vector3 m_moveInputDirection;
    private bool m_isMoving;
    private bool m_isGrounded;
    private bool m_isJumping;

    private float m_gravityMultiplier = 1;
    void Start()
    {
        m_moveInput.action.Enable();
        m_jumpInput.action.Enable();
    }


    void Update()
    {
        m_moveInputDirection = m_moveInput.action.ReadValue<Vector2>();

        m_moveInputDirection.z = m_moveInputDirection.y;
        m_moveInputDirection.y = 0;

        m_moveInputDirection = (Camera.main.transform.right * m_moveInputDirection.x) + (Camera.main.transform.forward * m_moveInputDirection.z);
        m_moveInputDirection.y = 0;
        m_moveInputDirection.Normalize();

        if (m_moveInputDirection != Vector3.zero)
            OnStartMoving();
        else
            OnStopMoving();

        SetOrientation();

        if (m_jumpInput.action.WasPressedThisFrame() && CanJump())
        {
            OnJump();
        }
    }

    private void FixedUpdate()
    {
        SetDamping();

        SetGravityMultiplier();

        SetIsGround(m_groundCheck.GetIsHitting());

        m_rb.AddForce((Vector3)m_moveInputDirection * m_acceleration, ForceMode.Acceleration);

        m_rb.AddForce(Physics.gravity * (m_gravityMultiplier - 1), ForceMode.Acceleration);

        float directionDot = Vector3.Dot(m_rb.linearVelocity.normalized, m_moveInputDirection.normalized);
        if (directionDot > 0.8f)
            m_rb.linearVelocity *= 0.8f;

        float yVelo = m_rb.linearVelocity.y;
        m_rb.linearVelocity = Vector3.ClampMagnitude(m_rb.linearVelocity, m_maxSpeed);
        m_rb.linearVelocity = new Vector3(m_rb.linearVelocity.x, yVelo, m_rb.linearVelocity.z);
    }

    private void OnStartMoving()
    {
        if (!m_isMoving)
        {
            m_isMoving = true;

            m_anim.SetTrigger("OnStartMoving");
        }
    }

    private void OnStopMoving()
    {
        if (!m_isMoving)
        {
            m_isMoving = false;

            m_anim.SetTrigger("OnStopMoving");
        }
    }

    private void SetDamping()
    {
        if (!m_isGrounded)
            if (m_isMoving) m_rb.linearDamping = 0;
            else m_rb.linearDamping = m_dampingOnStop;

        else
            m_rb.linearDamping = 1;
    }

    private void SetOrientation()
    {
        transform.forward = Vector3.RotateTowards(transform.forward, m_moveInputDirection, Time.deltaTime * 20, 0);
    }

    private void OnJump()
    {
        m_rb.linearDamping = 1;

        m_rb.AddForce(Vector3.up * m_jumpForce, ForceMode.Impulse);
        m_isJumping = true;
        
        StartCoroutine(C_Jump());
    }

    private void OnJumpEnd()
    {
        m_isJumping = false;
    }

    private IEnumerator C_Jump()
    {
        float timer = 0;
        float duration = m_maxJumpDuration;

        while (timer < duration)
        {
            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        OnJumpEnd();
    }

    private void SetIsGround (bool value)
    {
        m_isGrounded = value;
    }

    private bool CanJump()
    {
        if (!m_isGrounded) return false;

        return true;
    }

    private void SetGravityMultiplier()
    {
        if (m_isGrounded)
            m_gravityMultiplier = 1;

        else
        {
            if (m_isJumping)
                m_gravityMultiplier = 1;
            else
                m_gravityMultiplier = m_fallGravity;

        }
    }
}
