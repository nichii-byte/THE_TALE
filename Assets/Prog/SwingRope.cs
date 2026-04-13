using UnityEngine;

public class SwingRope : MonoBehaviour
{
    [SerializeField] private Transform m_anchorPoint;
    [SerializeField] private Rigidbody anchorRb;
    [SerializeField] private float m_ropeLength = 4f;

    [Header("Optional visual / colliders")]
    [SerializeField] private Collider[] m_endColliders;
    [Tooltip("Représente l'extrémité basse / visuelle de la corde.")]
    [SerializeField] private Transform m_tailPoint;

    [Header("Visual follow")]
    [Tooltip("Vitesse de suivi du point visuel (plus grand = suit plus vite)")]
    [SerializeField] private float m_tailFollowSpeed = 40f;

    private Transform m_followTarget;

    public Vector3 AnchorPosition => m_anchorPoint != null ? m_anchorPoint.position : transform.position;

    public Rigidbody AnchorRb => anchorRb ?? (m_anchorPoint != null ? m_anchorPoint.GetComponentInParent<Rigidbody>() : null);

    public float RopeLength => m_ropeLength;

    private void Awake()
    {
        if (anchorRb == null && m_anchorPoint != null)
        {
            anchorRb = m_anchorPoint.GetComponentInParent<Rigidbody>();
        }
    }

    private void OnValidate()
    {
        if (anchorRb == null && m_anchorPoint != null)
            anchorRb = m_anchorPoint.GetComponentInParent<Rigidbody>();
    }

    private void Update()
    {
        if (m_followTarget != null)
        {
            if (m_tailPoint != null)
            {
                m_tailPoint.position = Vector3.Lerp(m_tailPoint.position, m_followTarget.position, Time.deltaTime * m_tailFollowSpeed);
            }
            else if (m_endColliders != null && m_endColliders.Length > 0 && m_endColliders[0] != null)
            {
                Transform t = m_endColliders[0].transform;
                t.position = Vector3.Lerp(t.position, m_followTarget.position, Time.deltaTime * m_tailFollowSpeed);
            }
        }
    }

    public void SetEndCollidersEnabled(bool enabled)
    {
        if (m_endColliders == null) return;
        for (int i = 0; i < m_endColliders.Length; i++)
        {
            if (m_endColliders[i] != null)
                m_endColliders[i].enabled = enabled;
        }
    }

    public void StartFollow(Transform target)
    {
        m_followTarget = target;

        if (m_followTarget != null)
        {
            if (m_tailPoint != null)
                m_tailPoint.position = m_followTarget.position;
            else if (m_endColliders != null && m_endColliders.Length > 0 && m_endColliders[0] != null)
                m_endColliders[0].transform.position = m_followTarget.position;
        }
    }

    public void StopFollow()
    {
        m_followTarget = null;
    }
}

