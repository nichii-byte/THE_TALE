using System.Collections;
using UnityEngine;

public class EmergingHandsTrap : MonoBehaviour, IRuntimeResettable
{
    [SerializeField] private Transform m_handRoot;
    [SerializeField] private Collider m_damageCollider;
    [SerializeField] private Vector3 m_extendedLocalOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float m_extendDuration = 0.18f;
    [SerializeField] private float m_holdDuration = 0.8f;
    [SerializeField] private float m_retractDuration = 0.22f;

    private Vector3 m_hiddenLocalPosition;
    private Coroutine m_sequenceRoutine;

    private void Awake()
    {
        if (m_handRoot == null)
            m_handRoot = transform;

        m_hiddenLocalPosition = m_handRoot.localPosition;
        ResetRuntimeState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || other.GetComponentInParent<CharaController>() == null)
            return;

        if (m_sequenceRoutine != null)
            StopCoroutine(m_sequenceRoutine);

        m_sequenceRoutine = StartCoroutine(EmergeRoutine());
    }

    public void ResetRuntimeState()
    {
        if (m_sequenceRoutine != null)
        {
            StopCoroutine(m_sequenceRoutine);
            m_sequenceRoutine = null;
        }

        if (m_handRoot != null)
        {
            m_handRoot.localPosition = m_hiddenLocalPosition;
        }

        if (m_damageCollider != null)
        {
            m_damageCollider.enabled = false;
        }
    }

    private IEnumerator EmergeRoutine()
    {
        if (m_handRoot == null)
            yield break;

        if (m_damageCollider != null)
        {
            m_damageCollider.enabled = false;
        }

        Vector3 extendedPosition = m_hiddenLocalPosition + m_extendedLocalOffset;

        yield return MoveHand(m_hiddenLocalPosition, extendedPosition, m_extendDuration);

        if (m_damageCollider != null)
        {
            m_damageCollider.enabled = true;
        }

        if (m_holdDuration > 0f)
            yield return new WaitForSeconds(m_holdDuration);

        if (m_damageCollider != null)
        {
            m_damageCollider.enabled = false;
        }

        yield return MoveHand(extendedPosition, m_hiddenLocalPosition, m_retractDuration);
        m_sequenceRoutine = null;
    }

    private IEnumerator MoveHand(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            m_handRoot.localPosition = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            m_handRoot.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        m_handRoot.localPosition = to;
    }
}
