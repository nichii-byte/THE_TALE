using UnityEngine;
using UnityEngine.UI;
public class SwingPowerUI : MonoBehaviour
{
    [SerializeField] private Slider m_slider;
    [SerializeField] private CharaController m_controller;
    [SerializeField] private string m_playerTag = "Player";
    [SerializeField] private bool m_findAtStart = true;

    private void Start()
    {
        if (m_slider == null)
        {
            Debug.LogWarning("SwingPowerUI: Slider not assigned.");
            enabled = false;
            return;
        }

        if (m_controller == null && m_findAtStart)
        {
            GameObject go = GameObject.FindGameObjectWithTag(m_playerTag);
            if (go != null) m_controller = go.GetComponent<CharaController>();
        }

        if (m_controller == null)
        {
            Debug.LogWarning("SwingPowerUI: CharaController not assigned or not found.");
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (m_controller == null) return;
        m_slider.value = Mathf.Clamp01(m_controller.SwingChargeNormalized);
    }
}
