using UnityEngine;

public class ThreadPickup : MonoBehaviour
{
    [SerializeField] private int m_threadAmount = 1;
    [SerializeField] private bool m_destroyOnPickup = true;

    private void OnTriggerEnter(Collider other)
    {
        CharaController charaController = other.GetComponentInParent<CharaController>();
        if (charaController == null)
            return;

        charaController.AddThreads(m_threadAmount);

        if (m_destroyOnPickup)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
