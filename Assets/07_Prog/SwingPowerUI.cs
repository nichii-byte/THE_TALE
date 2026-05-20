using UnityEngine;
using UnityEngine.UI;
public class SwingPowerUI : MonoBehaviour
{
    [SerializeField] private Slider m_slider;
    [SerializeField] private CharaController m_controller;
    [SerializeField] private string m_playerTag = "Player";
    [SerializeField] private bool m_findAtStart = true;
    [SerializeField] private GameObject m_uiRoot;

    private CanvasGroup m_canvasGroup;
    private bool? m_isVisible;

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

        GameObject targetRoot = m_uiRoot != null ? m_uiRoot : gameObject;
        m_canvasGroup = targetRoot.GetComponent<CanvasGroup>();
        if (m_canvasGroup == null)
            m_canvasGroup = targetRoot.AddComponent<CanvasGroup>();

        if (m_controller == null)
        {
            Debug.LogWarning("SwingPowerUI: CharaController not assigned or not found.");
            enabled = false;
            return;
        }

        SetVisible(false);
    }

    private void Update()
    {
        if (m_controller == null) return;

        SetVisible(m_controller.IsSwinging);
        m_slider.value = Mathf.Clamp01(m_controller.SwingChargeNormalized);
    }

    private void SetVisible(bool visible)
    {
        if (m_canvasGroup == null)
            return;

        if (m_isVisible.HasValue && m_isVisible.Value == visible)
            return;

        m_isVisible = visible;
        m_canvasGroup.alpha = visible ? 1f : 0f;
        m_canvasGroup.interactable = visible;
        m_canvasGroup.blocksRaycasts = visible;
    }
}
