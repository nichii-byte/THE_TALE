using System.Collections.Generic;
using UnityEngine;

public class SwingRope : MonoBehaviour
{
    private const float kMinSegmentLengthSqr = 0.000001f;

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

    [Header("Auto stabilize")]
    [Tooltip("Temps mis par la corde pour ralentir et revenir au repos apres le detach.")]
    [SerializeField] private float m_autoStabilizeDuration = 1.5f;
    [Tooltip("Vitesse a laquelle la chaine revient vers sa pose de repos.")]
    [SerializeField] private float m_autoStabilizePositionSpeed = 6f;
    [Tooltip("Freinage applique a la vitesse lineaire pendant le retour au repos.")]
    [SerializeField] private float m_autoStabilizeLinearDamping = 7f;
    [Tooltip("Freinage applique a la vitesse angulaire pendant le retour au repos.")]
    [SerializeField] private float m_autoStabilizeAngularDamping = 10f;

    private Transform m_followTarget;
    private Transform m_followVisualPoint;
    private Rigidbody m_followVisualRb;
    private Rigidbody m_tailRb;
    private readonly List<Rigidbody> m_orderedChainBodies = new List<Rigidbody>();
    private readonly List<Vector3> m_ropePathPoints = new List<Vector3>();
    private bool m_chainCacheDirty = true;
    private float m_autoStabilizeTimer;

    public Vector3 AnchorPosition => m_anchorPoint != null ? m_anchorPoint.position : transform.position;

    public Rigidbody AnchorRb => anchorRb ?? (m_anchorPoint != null ? m_anchorPoint.GetComponentInParent<Rigidbody>() : null);

    public float RopeLength
    {
        get
        {
            float currentLength = GetCurrentPathLength();
            return currentLength > 0.001f ? currentLength : m_ropeLength;
        }
    }

    // New: provide a tail position and helper to compute closest point on the rope segment
    public Vector3 TailPosition
    {
        get
        {
            EnsureChainCache();
            if (m_ropePathPoints.Count > 0)
                return m_ropePathPoints[m_ropePathPoints.Count - 1];

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
        MarkChainCacheDirty();
    }

    private void OnValidate()
    {
        if (anchorRb == null && m_anchorPoint != null)
            anchorRb = m_anchorPoint.GetComponentInParent<Rigidbody>();

        CacheTailRigidbody();
        MarkChainCacheDirty();
    }

    private void OnTransformChildrenChanged()
    {
        CacheTailRigidbody();
        MarkChainCacheDirty();
    }

    private void Update()
    {
        if (m_followTarget != null && GetActiveFollowRigidbody() == null)
        {
            UpdateTailFollow(Time.deltaTime, false);
        }
    }

    private void FixedUpdate()
    {
        if (m_followTarget != null)
        {
            if (GetActiveFollowRigidbody() != null)
            {
                UpdateTailFollow(Time.fixedDeltaTime, true);
            }

            return;
        }

        if (m_autoStabilizeTimer > 0f)
        {
            UpdateAutoStabilize(Time.fixedDeltaTime);
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

        m_autoStabilizeTimer = 0f;
        m_followTarget = target;
        SetFollowVisualPoint(attachedVisualPoint);
        SnapTailToTarget();
    }

    public void StopFollow()
    {
        m_followTarget = null;
        m_followVisualPoint = null;
        m_followVisualRb = null;
        m_autoStabilizeTimer = Mathf.Max(0f, m_autoStabilizeDuration);
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

    private void UpdateAutoStabilize(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        EnsureChainCache();

        float positionBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, m_autoStabilizePositionSpeed) * deltaTime);
        float linearBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, m_autoStabilizeLinearDamping) * deltaTime);
        float angularBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, m_autoStabilizeAngularDamping) * deltaTime);

        Vector3 anchor = AnchorPosition;
        Vector3 previousPoint = anchor;
        float distanceFromAnchor = 0f;

        for (int i = 0; i < m_orderedChainBodies.Count; i++)
        {
            Rigidbody body = m_orderedChainBodies[i];
            if (body == null)
                continue;

            Vector3 currentPosition = body.position;
            distanceFromAnchor += Vector3.Distance(previousPoint, currentPosition);
            previousPoint = currentPosition;

            if (body == AnchorRb || body.isKinematic)
                continue;

            Vector3 restPosition = anchor + Vector3.down * distanceFromAnchor;
            ApplyAutoStabilizeToBody(body, restPosition, positionBlend, linearBlend, angularBlend);
        }

        Transform tailTransform = ResolveTailTransform();
        if (tailTransform != null)
        {
            Vector3 tailCurrentPosition = m_tailRb != null ? m_tailRb.position : tailTransform.position;
            distanceFromAnchor += Vector3.Distance(previousPoint, tailCurrentPosition);
            Vector3 tailRestPosition = anchor + Vector3.down * distanceFromAnchor;

            if (m_tailRb != null && !IsTrackedDynamicBody(m_tailRb))
            {
                ApplyAutoStabilizeToBody(m_tailRb, tailRestPosition, positionBlend, linearBlend, angularBlend);
            }
            else if (m_tailRb == null)
            {
                tailTransform.position = Vector3.Lerp(tailTransform.position, tailRestPosition, positionBlend);
            }
        }

        m_autoStabilizeTimer -= deltaTime;
        if (m_autoStabilizeTimer <= 0f)
        {
            SnapChainToRest();
            m_autoStabilizeTimer = 0f;
        }
    }

    private void ApplyAutoStabilizeToBody(Rigidbody body, Vector3 targetPosition, float positionBlend, float linearBlend, float angularBlend)
    {
        if (body == null || body.isKinematic)
            return;

        body.linearVelocity = Vector3.Lerp(body.linearVelocity, Vector3.zero, linearBlend);
        body.angularVelocity = Vector3.Lerp(body.angularVelocity, Vector3.zero, angularBlend);
        body.MovePosition(Vector3.Lerp(body.position, targetPosition, positionBlend));
    }

    private void SnapChainToRest()
    {
        EnsureChainCache();

        Vector3 anchor = AnchorPosition;
        Vector3 previousPoint = anchor;
        float distanceFromAnchor = 0f;

        for (int i = 0; i < m_orderedChainBodies.Count; i++)
        {
            Rigidbody body = m_orderedChainBodies[i];
            if (body == null)
                continue;

            Vector3 currentPosition = body.position;
            distanceFromAnchor += Vector3.Distance(previousPoint, currentPosition);
            previousPoint = currentPosition;

            if (body == AnchorRb || body.isKinematic)
                continue;

            Vector3 restPosition = anchor + Vector3.down * distanceFromAnchor;
            body.position = restPosition;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        Transform tailTransform = ResolveTailTransform();
        if (tailTransform == null)
            return;

        Vector3 tailCurrentPosition = m_tailRb != null ? m_tailRb.position : tailTransform.position;
        distanceFromAnchor += Vector3.Distance(previousPoint, tailCurrentPosition);
        Vector3 tailRestPosition = anchor + Vector3.down * distanceFromAnchor;

        if (m_tailRb != null && !IsTrackedDynamicBody(m_tailRb))
        {
            m_tailRb.position = tailRestPosition;
            m_tailRb.linearVelocity = Vector3.zero;
            m_tailRb.angularVelocity = Vector3.zero;
        }
        else if (m_tailRb == null)
        {
            tailTransform.position = tailRestPosition;
        }
    }

    private bool IsTrackedDynamicBody(Rigidbody body)
    {
        if (body == null)
            return false;

        for (int i = 0; i < m_orderedChainBodies.Count; i++)
        {
            if (m_orderedChainBodies[i] == body)
                return true;
        }

        return false;
    }

    // Returns the closest point on the rope segment (anchor -> tail) to the specified world position
    public Vector3 GetClosestPointOnRope(Vector3 worldPos)
    {
        EnsureChainCache();
        if (m_ropePathPoints.Count < 2)
        {
            Vector3 fallbackAnchor = AnchorPosition;
            Vector3 fallbackTail = fallbackAnchor + Vector3.down * m_ropeLength;
            return GetClosestPointOnSegment(fallbackAnchor, fallbackTail, worldPos);
        }

        Vector3 closestPoint = m_ropePathPoints[0];
        float closestDistanceSqr = float.MaxValue;

        for (int i = 1; i < m_ropePathPoints.Count; i++)
        {
            Vector3 candidate = GetClosestPointOnSegment(m_ropePathPoints[i - 1], m_ropePathPoints[i], worldPos);
            float distanceSqr = (worldPos - candidate).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestPoint = candidate;
            }
        }

        return closestPoint;
    }

    // Returns parameter t in [0..1] along the rope segment closest to worldPos
    public float GetClosestPointParam(Vector3 worldPos)
    {
        EnsureChainCache();
        if (m_ropePathPoints.Count < 2)
            return 0f;

        float totalLength = 0f;
        float bestLengthAlongRope = 0f;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 1; i < m_ropePathPoints.Count; i++)
        {
            Vector3 start = m_ropePathPoints[i - 1];
            Vector3 end = m_ropePathPoints[i];
            Vector3 segment = end - start;
            float segmentLength = segment.magnitude;

            if (segmentLength <= 0.0001f)
                continue;

            float segmentT = Mathf.Clamp01(Vector3.Dot(worldPos - start, segment) / (segmentLength * segmentLength));
            Vector3 candidate = start + segment * segmentT;
            float distanceSqr = (worldPos - candidate).sqrMagnitude;

            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                bestLengthAlongRope = totalLength + (segmentLength * segmentT);
            }

            totalLength += segmentLength;
        }

        if (totalLength <= 0.0001f)
            return 0f;

        return Mathf.Clamp01(bestLengthAlongRope / totalLength);
    }

    private void EnsureChainCache()
    {
        if (m_chainCacheDirty)
        {
            RebuildBodyOrderCache();
            m_chainCacheDirty = false;
        }

        RefreshPathPoints();
    }

    private void MarkChainCacheDirty()
    {
        m_chainCacheDirty = true;
    }

    private void RebuildBodyOrderCache()
    {
        m_orderedChainBodies.Clear();

        HashSet<Rigidbody> uniqueBodies = new HashSet<Rigidbody>();
        Rigidbody resolvedAnchorRb = AnchorRb;

        if (resolvedAnchorRb != null)
            uniqueBodies.Add(resolvedAnchorRb);

        Rigidbody[] childBodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < childBodies.Length; i++)
        {
            if (childBodies[i] != null)
                uniqueBodies.Add(childBodies[i]);
        }

        if (uniqueBodies.Count == 0)
            return;

        Dictionary<Rigidbody, List<Rigidbody>> adjacency = new Dictionary<Rigidbody, List<Rigidbody>>();
        foreach (Rigidbody body in uniqueBodies)
        {
            adjacency[body] = new List<Rigidbody>();
        }

        foreach (Rigidbody body in uniqueBodies)
        {
            HingeJoint hinge = body.GetComponent<HingeJoint>();
            if (hinge == null || hinge.connectedBody == null || !uniqueBodies.Contains(hinge.connectedBody))
                continue;

            List<Rigidbody> neighbors = adjacency[body];
            if (!neighbors.Contains(hinge.connectedBody))
                neighbors.Add(hinge.connectedBody);

            List<Rigidbody> connectedNeighbors = adjacency[hinge.connectedBody];
            if (!connectedNeighbors.Contains(body))
                connectedNeighbors.Add(body);
        }

        Rigidbody startBody = resolvedAnchorRb != null && adjacency.ContainsKey(resolvedAnchorRb)
            ? resolvedAnchorRb
            : FindBodyClosestToAnchor(uniqueBodies);

        if (startBody != null)
            TraverseChain(startBody, adjacency);
    }

    private void RefreshPathPoints()
    {
        m_ropePathPoints.Clear();

        AppendPathPoint(AnchorPosition);
        for (int i = 0; i < m_orderedChainBodies.Count; i++)
        {
            Rigidbody body = m_orderedChainBodies[i];
            if (body != null)
                AppendPathPoint(body.position);
        }

        Transform tailTransform = ResolveTailTransform();
        if (tailTransform != null)
            AppendPathPoint(tailTransform.position);

        if (m_ropePathPoints.Count < 2)
            AppendPathPoint(AnchorPosition + Vector3.down * m_ropeLength);
    }

    private Rigidbody FindBodyClosestToAnchor(IEnumerable<Rigidbody> bodies)
    {
        Vector3 anchor = AnchorPosition;
        Rigidbody closestBody = null;
        float closestDistanceSqr = float.MaxValue;

        foreach (Rigidbody body in bodies)
        {
            if (body == null)
                continue;

            float distanceSqr = (body.position - anchor).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestBody = body;
            }
        }

        return closestBody;
    }

    private void TraverseChain(Rigidbody startBody, Dictionary<Rigidbody, List<Rigidbody>> adjacency)
    {
        HashSet<Rigidbody> visited = new HashSet<Rigidbody>();
        Rigidbody previous = null;
        Rigidbody current = startBody;

        while (current != null && visited.Add(current))
        {
            m_orderedChainBodies.Add(current);

            Rigidbody next = null;
            List<Rigidbody> neighbors = adjacency[current];
            for (int i = 0; i < neighbors.Count; i++)
            {
                Rigidbody candidate = neighbors[i];
                if (candidate == null || candidate == previous || visited.Contains(candidate))
                    continue;

                next = candidate;
                break;
            }

            previous = current;
            current = next;
        }
    }

    private void AppendPathPoint(Vector3 point)
    {
        int count = m_ropePathPoints.Count;
        if (count > 0 && (m_ropePathPoints[count - 1] - point).sqrMagnitude <= kMinSegmentLengthSqr)
            return;

        m_ropePathPoints.Add(point);
    }

    private float GetCurrentPathLength()
    {
        EnsureChainCache();
        float totalLength = 0f;

        for (int i = 1; i < m_ropePathPoints.Count; i++)
        {
            totalLength += Vector3.Distance(m_ropePathPoints[i - 1], m_ropePathPoints[i]);
        }

        return totalLength;
    }

    private static Vector3 GetClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 segment = end - start;
        float segmentLengthSqr = segment.sqrMagnitude;
        if (segmentLengthSqr <= kMinSegmentLengthSqr)
            return start;

        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSqr);
        return start + segment * t;
    }
}
