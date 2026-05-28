using System.Collections;
using UnityEngine;

public class TrafficLightAlternator : MonoBehaviour, IRuntimeResettable
{
    [Header("References")]
    [SerializeField] private Light m_greenLight;
    [SerializeField] private Light m_redLight;

    [Header("Cycle")]
    [SerializeField, Min(0.01f)] private float m_switchInterval = 2f;
    [SerializeField] private bool m_startWithGreen = true;

    private Coroutine m_cycleRoutine;
    private bool m_isGreenActive;

    private void Awake()
    {
        ResolveLightReferences();
    }

    private void OnEnable()
    {
        ResetRuntimeState();
    }

    private void OnDisable()
    {
        StopCycle();
    }

    private void OnValidate()
    {
        m_switchInterval = Mathf.Max(0.01f, m_switchInterval);
    }

    public void ResetRuntimeState()
    {
        ResolveLightReferences();
        StopCycle();
        SetGreenActive(m_startWithGreen);

        if (isActiveAndEnabled)
        {
            m_cycleRoutine = StartCoroutine(CycleRoutine());
        }
    }

    private IEnumerator CycleRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, m_switchInterval));
            SetGreenActive(!m_isGreenActive);
        }
    }

    private void SetGreenActive(bool isGreenActive)
    {
        m_isGreenActive = isGreenActive;

        if (m_greenLight != null)
            m_greenLight.enabled = isGreenActive;

        if (m_redLight != null)
            m_redLight.enabled = !isGreenActive;
    }

    private void StopCycle()
    {
        if (m_cycleRoutine == null)
            return;

        StopCoroutine(m_cycleRoutine);
        m_cycleRoutine = null;
    }

    private void ResolveLightReferences()
    {
        if (m_greenLight != null && m_redLight != null)
            return;

        Light[] childLights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < childLights.Length; i++)
        {
            Light childLight = childLights[i];
            if (childLight == null)
                continue;

            string lightName = childLight.gameObject.name;
            if (m_greenLight == null && lightName.IndexOf("Green", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                m_greenLight = childLight;
            }
            else if (m_redLight == null && lightName.IndexOf("Red", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                m_redLight = childLight;
            }
        }
    }
}
