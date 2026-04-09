using UnityEngine;

public class PullableObject : MonoBehaviour
{
 private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Pull(Vector3 targetPosition, float force)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        rb.AddForce(direction * force, ForceMode.Acceleration);
    }
}
