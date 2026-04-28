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
    [Tooltip("Decalage applique entre le joueur et l'extremite visuelle de la corde pendant le swing.")]
    [SerializeField] private Vector3 m_followOffset = Vector3.zero;

    private Transform m_followTarget;
    private Transform m_followVisualPoint;
    private Rigidbody m_followVisualRb;
    private Rigidbody m_tailRb;

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

        CacheTailRigidbody();
    }

    private void OnValidate()
    {
        if (anchorRb == null && m_anchorPoint != null)
            anchorRb = m_anchorPoint.GetComponentInParent<Rigidbody>();

        CacheTailRigidbody();
    }

    private void Update()
    {
        if (GetActiveFollowRigidbody() == null)
        {
            UpdateTailFollow(Time.deltaTime, false);
        }
    }

    private void FixedUpdate()
    {
        if (GetActiveFollowRigidbody() != null)
        {
            UpdateTailFollow(Time.fixedDeltaTime, true);
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

    public void StartFollow(Transform target, Transform attachedVisualPoint = null)
    {
        if (target == null) return;

        m_followTarget = target;
        SetFollowVisualPoint(attachedVisualPoint);
        SnapTailToTarget();
    }

    public void StopFollow()
    {
        m_followTarget = null;
        m_followVisualPoint = null;
        m_followVisualRb = null;
    }

    private void UpdateTailFollow(float deltaTime, bool useRigidBody)
    {
        if (m_followTarget == null)
            return;

        Transform movingTransform = ResolveFollowTransform();
        if (movingTransform == null)
            return;

        Rigidbody activeRb = GetActiveFollowRigidbody();
        Vector3 targetPosition = m_followTarget.position + m_followOffset;
        float factor = 1f - Mathf.Exp(-Mathf.Max(0.01f, m_tailFollowSpeed) * deltaTime);
        Vector3 nextPosition = Vector3.Lerp(movingTransform.position, targetPosition, factor);

        if (useRigidBody && activeRb != null)
        {
            activeRb.MovePosition(nextPosition);
        }
        else
        {
            movingTransform.position = nextPosition;
        }
    }

    private void SnapTailToTarget()
    {
        Transform movingTransform = ResolveFollowTransform();
        if (m_followTarget == null || movingTransform == null)
            return;

        Rigidbody activeRb = GetActiveFollowRigidbody();
        Vector3 targetPosition = m_followTarget.position + m_followOffset;
        if (activeRb != null)
        {
            activeRb.position = targetPosition;
            activeRb.linearVelocity = Vector3.zero;
            activeRb.angularVelocity = Vector3.zero;
        }
        else
        {
            movingTransform.position = targetPosition;
        }
    }

    private void SetFollowVisualPoint(Transform attachedVisualPoint)
    {
        m_followVisualPoint = attachedVisualPoint != null ? attachedVisualPoint : ResolveTailTransform();
        m_followVisualRb = m_followVisualPoint != null ? m_followVisualPoint.GetComponent<Rigidbody>() : null;
    }

    private Transform ResolveFollowTransform()
    {
        if (m_followVisualPoint != null)
            return m_followVisualPoint;

        return ResolveTailTransform();
    }

    private Rigidbody GetActiveFollowRigidbody()
    {
        if (m_followVisualRb != null)
            return m_followVisualRb;

        return m_tailRb;
    }

    private Transform ResolveTailTransform()
    {
        if (m_tailPoint != null)
            return m_tailPoint;

        if (m_endColliders != null && m_endColliders.Length > 0 && m_endColliders[0] != null)
            return m_endColliders[0].transform;

        return null;
    }

    private void CacheTailRigidbody()
    {
        Transform tailTransform = ResolveTailTransform();
        m_tailRb = tailTransform != null ? tailTransform.GetComponent<Rigidbody>() : null;
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
