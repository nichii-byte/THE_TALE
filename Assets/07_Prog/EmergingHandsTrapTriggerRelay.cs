using UnityEngine;

public class EmergingHandsTrapTriggerRelay : MonoBehaviour
{
    private EmergingHandsTrap m_owner;

    public void Initialize(EmergingHandsTrap owner)
    {
        m_owner = owner;
    }

    private void Awake()
    {
        if (m_owner == null)
            m_owner = GetComponentInParent<EmergingHandsTrap>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (m_owner == null)
            m_owner = GetComponentInParent<EmergingHandsTrap>();

        if (m_owner != null)
            m_owner.TryTriggerFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (m_owner == null)
            m_owner = GetComponentInParent<EmergingHandsTrap>();

        if (m_owner != null)
            m_owner.TryTriggerStayFromCollider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (m_owner == null)
            m_owner = GetComponentInParent<EmergingHandsTrap>();

        if (m_owner != null)
            m_owner.TryReleaseFromCollider(other);
    }
}
