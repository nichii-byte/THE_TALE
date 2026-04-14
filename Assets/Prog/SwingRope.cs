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

    // New: provide a tail position and helper to compute closest point on the rope segment
    public Vector3 TailPosition
    {
        get
        {
            if (m_tailPoint != null) return m_tailPoint.position;
            if (m_endColliders != null && m_endColliders.Length > 0 && m_endColliders[0] != null)
                return m_endColliders[0].transform.position;

            // fallback: approximate tail straight down from anchor by configured rope length
            Vector3 anchor = AnchorPosition;
            return anchor + Vector3.down * m_ropeLength;
        }
    }

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
        // Only update visual tail when an explicit follow target is set.
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
        // Set the visual tail immediately to the attach point instead of continuously following the player.
        if (target == null) return;

        if (m_tailPoint != null)
        {
            m_tailPoint.position = target.position;
        }
        else if (m_endColliders != null && m_endColliders.Length > 0 && m_endColliders[0] != null)
        {
            m_endColliders[0].transform.position = target.position;
        }

        // Do not set m_followTarget here to avoid persistent following which made the tail stick to the player.
        m_followTarget = null;
    }

    public void StopFollow()
    {
        m_followTarget = null;
    }

    // Returns the closest point on the rope segment (anchor -> tail) to the specified world position
    public Vector3 GetClosestPointOnRope(Vector3 worldPos)
    {
        Vector3 a = AnchorPosition;
        Vector3 b = TailPosition;
        Vector3 ab = b - a;
        float abLen2 = ab.sqrMagnitude;
        if (abLen2 < 1e-6f) return a;
        float t = Vector3.Dot(worldPos - a, ab) / abLen2;
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    // Returns parameter t in [0..1] along the rope segment closest to worldPos
    public float GetClosestPointParam(Vector3 worldPos)
    {
        Vector3 a = AnchorPosition;
        Vector3 b = TailPosition;
        Vector3 ab = b - a;
        float abLen2 = ab.sqrMagnitude;
        if (abLen2 < 1e-6f) return 0f;
        float t = Vector3.Dot(worldPos - a, ab) / abLen2;
        return Mathf.Clamp01(t);
    }
}

