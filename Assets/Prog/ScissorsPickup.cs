using UnityEngine;

public class ScissorsPickup : MonoBehaviour
{
    [SerializeField] private bool m_destroyOnPickup = true;

    private void OnTriggerEnter(Collider other)
    {
        CharaController charaController = other.GetComponentInParent<CharaController>();
        if (charaController == null)
            return;

        charaController.GiveScissors();

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
