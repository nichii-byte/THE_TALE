using UnityEngine;

public class DeathZone : MonoBehaviour
{
    [SerializeField] private bool m_useTrigger = true;
    [SerializeField] private bool m_useCollision = false;
    [SerializeField] private string m_deathReason = "Hazard";

    private void OnTriggerEnter(Collider other)
    {
        if (!m_useTrigger)
            return;

        TryKill(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!m_useTrigger)
            return;

        TryKill(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!m_useCollision || collision == null)
            return;

        TryKill(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!m_useCollision || collision == null)
            return;

        TryKill(collision.collider);
    }

    private void TryKill(Component source)
    {
        if (source == null || source.GetComponentInParent<CharaController>() == null)
            return;

        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.KillPlayer(m_deathReason);
        }
    }
}
