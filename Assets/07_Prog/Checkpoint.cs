using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] private Transform m_spawnPoint;
    [SerializeField] private bool m_setAsInitialCheckpoint;
    [SerializeField] private bool m_isLevelEnd;

    public Vector3 SpawnPosition => (m_spawnPoint != null ? m_spawnPoint : transform).position;
    public Quaternion SpawnRotation => (m_spawnPoint != null ? m_spawnPoint : transform).rotation;
    public bool IsInitialCheckpoint => m_setAsInitialCheckpoint;

    private void Start()
    {
        if (m_setAsInitialCheckpoint && CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.RegisterCheckpoint(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || other.GetComponentInParent<CharaController>() == null)
            return;

        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.RegisterCheckpoint(this);
            if (m_isLevelEnd)
            {
                CheckpointManager.Instance.CompleteLevel();
            }
        }
    }
}
