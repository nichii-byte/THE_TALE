using UnityEngine;

public class GroundCheck : MonoBehaviour
{

    [Header("Values")]
    [SerializeField] private Vector3 m_direction;
    [SerializeField] private float m_distance = 1;
    [SerializeField] private LayerMask m_groundLayers;

    private bool m_isHitting = false;

    private void DoRaycast()
    {
        RaycastHit hit;

        if(Physics.Raycast(transform.position, m_direction, out hit, m_distance, m_groundLayers))
        {
            SetIsHitting(true);
        }
        else
        {
            SetIsHitting(false);
        }
    }

    private void SetIsHitting(bool value)
    {
        m_isHitting = value;
    }

    public bool GetIsHitting()
    {
        return m_isHitting;
    }

    public Vector3 GetRayEndWorldPosition()
    {
        Vector3 direction = m_direction.sqrMagnitude > 1e-6f ? m_direction.normalized : Vector3.down;
        return transform.position + direction * m_distance;
    }

    private void FixedUpdate()
    {
       DoRaycast();
    }

    private void OnDrawGizmosSelected()
    {
        if (GetIsHitting())
            Gizmos.color = Color.green;
        else
            Gizmos.color = Color.red;

        Vector3 from = transform.position;
        Vector3 to = from + m_direction.normalized * m_distance;
        Gizmos.DrawLine(from, to);
    }
}
