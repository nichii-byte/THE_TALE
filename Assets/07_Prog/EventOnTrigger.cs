using UnityEngine;
using UnityEngine.Events;

public class EventOnTrigger : MonoBehaviour
{
    public UnityEvent m_onTriggerEnterEvent;

    private void OnTriggerEnter(Collider other)
    {
        CharaController chara = other.GetComponentInParent<CharaController>();

        if (chara)
        {
            m_onTriggerEnterEvent.Invoke();
        }
    }
}
