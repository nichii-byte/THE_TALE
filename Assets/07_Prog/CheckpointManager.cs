using System.Collections;
using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    private static CheckpointManager s_instance;

    [Header("References")]
    [SerializeField] private CharaController m_player;
    [SerializeField] private Transform m_initialSpawnPoint;

    [Header("Respawn")]
    [SerializeField] private float m_respawnDelay = 0.1f;
    [SerializeField] private bool m_watchVoidY = true;
    [SerializeField] private float m_voidY = -25f;

    private Vector3 m_currentSpawnPosition;
    private Quaternion m_currentSpawnRotation = Quaternion.identity;
    private Checkpoint m_currentCheckpoint;
    private bool m_hasSpawnPoint;
    private bool m_isRespawning;
    private bool m_isLevelCompleted;
    private bool m_sessionStarted;

    public static CheckpointManager Instance => s_instance;
    public bool IsRespawning => m_isRespawning;

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Debug.LogWarning("CheckpointManager: duplicate instance detected.");
            enabled = false;
            return;
        }

        s_instance = this;
        ResolvePlayerReference();
        CacheInitialSpawn();
    }

    private void Start()
    {
        // Session reset is now triggered by the UI Play button via BeginSession()
    }

    private void OnDisable()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    private void Update()
    {
        if (!m_watchVoidY || m_isRespawning || m_isLevelCompleted)
            return;

        ResolvePlayerReference();
        if (m_player != null && m_player.transform.position.y < m_voidY)
        {
            KillPlayer("Void");
        }
    }

    public void RegisterCheckpoint(Checkpoint checkpoint, bool playFeedback = true)
    {
        if (checkpoint == null)
            return;

        bool changedCheckpoint = m_currentCheckpoint != checkpoint;
        m_currentCheckpoint = checkpoint;
        m_currentSpawnPosition = checkpoint.SpawnPosition;
        m_currentSpawnRotation = checkpoint.SpawnRotation;
        m_hasSpawnPoint = true;

        if (playFeedback && m_sessionStarted && changedCheckpoint)
        {
            checkpoint.PlayActivationParticles();
        }
    }

    public void KillPlayer(string deathReason = "Hazard")
    {
        if (m_isRespawning || m_isLevelCompleted)
            return;

        GameUIManager.Instance?.OnPlayerDied(deathReason);

        ResolvePlayerReference();
        if (m_player == null)
        {
            Debug.LogWarning("CheckpointManager: no player found for respawn after " + deathReason + ".");
            return;
        }

        StartCoroutine(RespawnRoutine());
    }

    public void ResetSessionState()
    {
        ResolvePlayerReference();
        m_isRespawning = false;
        m_isLevelCompleted = false;
        m_currentCheckpoint = null;
        m_hasSpawnPoint = false;

        if (!ApplyInitialCheckpointOverride())
        {
            CacheInitialSpawn();
        }

        ResetRuntimeObjects();

        if (m_player != null && m_hasSpawnPoint)
        {
            m_player.RespawnAt(m_currentSpawnPosition, m_currentSpawnRotation);
        }
    }

    // Public method to be called by UI when the player presses Play
    public void BeginSession()
    {
        m_sessionStarted = true;
        ResetSessionState();
    }

    public void CompleteLevel()
    {
        if (m_isRespawning || m_isLevelCompleted)
            return;

        m_isLevelCompleted = true;
        GameUIManager.Instance?.OnLevelCompleted();
    }

    private IEnumerator RespawnRoutine()
    {
        m_isRespawning = true;
        ResetRuntimeObjects();

        if (m_respawnDelay > 0f)
            yield return new WaitForSeconds(m_respawnDelay);
        else
            yield return null;

        ResolvePlayerReference();
        if (m_player != null && m_hasSpawnPoint)
        {
            m_player.RespawnAt(m_currentSpawnPosition, m_currentSpawnRotation);
        }

        ResetRuntimeObjects();
        m_isRespawning = false;
        PlayCurrentCheckpointFeedback();
    }

    private void ResolvePlayerReference()
    {
        if (m_player == null)
        {
            m_player = FindFirstObjectByType<CharaController>();
        }
    }

    private void CacheInitialSpawn()
    {
        if (m_hasSpawnPoint)
            return;

        Transform spawnSource = m_initialSpawnPoint;
        if (spawnSource == null && m_player != null)
            spawnSource = m_player.transform;
        if (spawnSource == null)
            spawnSource = transform;

        m_currentSpawnPosition = spawnSource.position;
        m_currentSpawnRotation = spawnSource.rotation;
        m_hasSpawnPoint = true;
    }

    private bool ApplyInitialCheckpointOverride()
    {
        Checkpoint[] checkpoints = FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] != null && checkpoints[i].IsInitialCheckpoint)
            {
                RegisterCheckpoint(checkpoints[i], false);
                return true;
            }
        }

        return false;
    }

    private void ResetRuntimeObjects()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IRuntimeResettable resettable)
            {
                resettable.ResetRuntimeState();
            }
        }
    }

    private void PlayCurrentCheckpointFeedback()
    {
        if (m_sessionStarted && m_currentCheckpoint != null)
            m_currentCheckpoint.PlayActivationParticles();
    }
}
