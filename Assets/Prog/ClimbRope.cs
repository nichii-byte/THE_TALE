using UnityEngine;

public class ClimbRope : MonoBehaviour
{
    [SerializeField] private Transform m_topPoint;
    [SerializeField] private Transform m_bottomPoint;
    [SerializeField] private float m_ropeLength = 4f;
    [SerializeField] private Vector3 m_defaultSideDirection = Vector3.forward;

    public Vector3 TopPosition => m_topPoint != null ? m_topPoint.position : transform.position;

    public Vector3 BottomPosition
    {
        get
        {
            if (m_bottomPoint != null) return m_bottomPoint.position;
            return TopPosition + Vector3.down * m_ropeLength;
        }
    }

    public Vector3 RopeVector => BottomPosition - TopPosition;

    public Vector3 RopeDirection
    {
        get
        {
            Vector3 ropeVector = RopeVector;
            return ropeVector.sqrMagnitude > 1e-6f ? ropeVector.normalized : Vector3.down;
        }
    }

    public float RopeLength
    {
        get
        {
            float currentLength = RopeVector.magnitude;
            return currentLength > 1e-4f ? currentLength : Mathf.Max(0.1f, m_ropeLength);
        }
    }

    public Vector3 GetPointAlongRope(float t)
    {
        return Vector3.Lerp(TopPosition, BottomPosition, Mathf.Clamp01(t));
    }

    public float GetClosestPointParam(Vector3 worldPos)
    {
        Vector3 top = TopPosition;
        Vector3 ropeVector = RopeVector;
        float lengthSquared = ropeVector.sqrMagnitude;
        if (lengthSquared < 1e-6f) return 0f;

        float t = Vector3.Dot(worldPos - top, ropeVector) / lengthSquared;
        return Mathf.Clamp01(t);
    }

    public Vector3 GetClosestPointOnRope(Vector3 worldPos)
    {
        return GetPointAlongRope(GetClosestPointParam(worldPos));
    }

    public Vector3 GetDefaultSideDirection()
    {
        Vector3 ropeDirection = RopeDirection;
        Vector3 sideDirection = Vector3.ProjectOnPlane(transform.forward, ropeDirection);

        if (sideDirection.sqrMagnitude < 1e-4f)
        {
            sideDirection = Vector3.ProjectOnPlane(m_defaultSideDirection, ropeDirection);
        }

        if (sideDirection.sqrMagnitude < 1e-4f)
        {
            sideDirection = Vector3.Cross(ropeDirection, Vector3.right);
        }

        if (sideDirection.sqrMagnitude < 1e-4f)
        {
            sideDirection = Vector3.Cross(ropeDirection, Vector3.forward);
        }

        return sideDirection.normalized;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f);
        Gizmos.DrawLine(TopPosition, BottomPosition);
        Gizmos.DrawSphere(TopPosition, 0.06f);
        Gizmos.DrawSphere(BottomPosition, 0.06f);
    }
}
