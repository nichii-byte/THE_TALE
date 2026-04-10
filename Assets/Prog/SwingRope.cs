using UnityEngine;

public class SwingRope : MonoBehaviour
{
    [SerializeField] private Transform m_anchorPoint;
    [SerializeField] private Rigidbody anchorRb;
    [SerializeField] private float m_ropeLength = 4f;

    public Vector3 AnchorPosition => m_anchorPoint != null ? m_anchorPoint.position : transform.position;
    public float RopeLength => m_ropeLength;
}

