using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.XR;

public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform spawnPoint;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float rotationSpeed = 20f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float fallMultiplier = 3f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer;
    private bool isGrounded;

    private Vector3 moveDirection;

    void Update()
    {
        HandleMovementInput();
        HandleRotation();
        HandleJump();
        ApplyBetterGravity();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Camera cam = Camera.main;

        Vector3 right = cam.transform.right;
        right.y = 0;

        Vector3 forward = cam.transform.forward;
        forward.y = 0;

        Vector3 direction = (right * horizontal + forward * vertical).normalized;

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        moveDirection = direction * currentSpeed;
    }

    private void MovePlayer()
    {
        Vector3 velocity = rb.linearVelocity;

        if (moveDirection.magnitude < 0.1f)
        {
            rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
        }
        else
        {
            rb.linearVelocity = new Vector3(moveDirection.x, velocity.y, moveDirection.z);
        }
    }

    private void HandleRotation()
    {
        if (moveDirection.magnitude >= 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            isGrounded = false;
        }
    }

    private void ApplyBetterGravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier -1) * Time.deltaTime;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            isGrounded = true;
        }
    }
}
