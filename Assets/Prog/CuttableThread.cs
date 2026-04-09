using UnityEngine;

public class CuttableThread : MonoBehaviour
{
    [SerializeField] private int m_threadReward = 1;
    [SerializeField] private GameObject m_objectToDisable;
    [SerializeField] private bool m_destroyOnCut = true;

    private bool m_isCut;

    public bool CanBeCut => !m_isCut;

    public bool Cut(CharaController charaController)
    {
        if (m_isCut || charaController == null || !charaController.HasScissors)
            return false;

        m_isCut = true;
        charaController.AddThreads(m_threadReward);

        GameObject target = m_objectToDisable != null ? m_objectToDisable : gameObject;

        if (m_destroyOnCut)
        {
            Destroy(target);
        }
        else
        {
            target.SetActive(false);
        }

        return true;
    }
}
