using UnityEngine;

public class SwingRope : MonoBehaviour
{
    [SerializeField] private Transform m_anchorPoint;
    [SerializeField] private float m_ropeLength = 4f;

    public Vector3 AnchorPosition => m_anchorPoint != null ? m_anchorPoint.position : transform.position;
    public float RopeLength => m_ropeLength;
}

//Côté gameplay, quand le joueur entre dans le trigger et appuie sur saut, il s’accroche; avec le stick, il pompe le balancier; avec saut, il se relâche avec un boost.