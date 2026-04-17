using System.Collections;
using UnityEngine;

public class TimedFlameTrap : MonoBehaviour, IRuntimeResettable
{
    [SerializeField] private GameObject m_flameVisual;
    [SerializeField] private Collider m_damageCollider;
    [SerializeField] private bool m_startActive = true;
    [SerializeField] private float m_activeDuration = 2f;
    [SerializeField] private float m_inactiveDuration = 2f;

    private bool m_isActive;
    private Coroutine m_cycleRoutine;

    private void Start()
    {
        RestartCycle();
    }

    private void OnDisable()
    {
        if (m_cycleRoutine != null)
        {
            StopCoroutine(m_cycleRoutine);
            m_cycleRoutine = null;
        }
    }

    public void ResetRuntimeState()
    {
        RestartCycle();
    }

    private void RestartCycle()
    {
        if (m_cycleRoutine != null)
        {
            StopCoroutine(m_cycleRoutine);
        }

        SetFlameState(m_startActive);

        if (isActiveAndEnabled)
        {
            m_cycleRoutine = StartCoroutine(CycleRoutine());
        }
    }

    private IEnumerator CycleRoutine()
    {
        while (true)
        {
            float waitDuration = m_isActive ? m_activeDuration : m_inactiveDuration;
            yield return new WaitForSeconds(Mathf.Max(0.01f, waitDuration));
            SetFlameState(!m_isActive);
        }
    }

    private void SetFlameState(bool isActive)
    {
        m_isActive = isActive;

        if (m_flameVisual != null)
        {
            m_flameVisual.SetActive(isActive);
        }

        if (m_damageCollider != null)
        {
            m_damageCollider.enabled = isActive;
        }
    }
}
