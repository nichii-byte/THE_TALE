using UnityEngine;

public class PendulumTrap : MonoBehaviour, IRuntimeResettable
{
    [SerializeField] private Transform m_pivot;
    [SerializeField] private Vector3 m_localAxis = Vector3.forward;
    [SerializeField] private float m_maxAngle = 65f;
    [SerializeField] private float m_cycleDuration = 2.2f;
    [SerializeField] private float m_phaseOffsetDegrees = 0f;

    private Quaternion m_initialLocalRotation;
    private float m_runtimeTime;

    private void Awake()
    {
        if (m_pivot == null)
            m_pivot = transform;

        m_initialLocalRotation = m_pivot.localRotation;
        ResetRuntimeState();
    }

    private void Update()
    {
        if (m_pivot == null)
            return;

        m_runtimeTime += Time.deltaTime;
        float duration = Mathf.Max(0.01f, m_cycleDuration);
        float phase = (m_runtimeTime / duration) * Mathf.PI * 2f + m_phaseOffsetDegrees * Mathf.Deg2Rad;
        Vector3 axis = m_localAxis.sqrMagnitude > 0.001f ? m_localAxis.normalized : Vector3.forward;
        float angle = Mathf.Sin(phase) * m_maxAngle;

        m_pivot.localRotation = m_initialLocalRotation * Quaternion.AngleAxis(angle, axis);
    }

    public void ResetRuntimeState()
    {
        m_runtimeTime = 0f;

        if (m_pivot != null)
        {
            m_pivot.localRotation = m_initialLocalRotation;
        }
    }
}
