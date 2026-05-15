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
    private bool m_hasSpawnPoint;
    private bool m_isRespawning;
    private bool m_isLevelCompleted;

    public static CheckpointManager Instance => s_instance;

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
        EnsureDeathTrackerExists();
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

    public void RegisterCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null)
            return;

        m_currentSpawnPosition = checkpoint.SpawnPosition;
        m_currentSpawnRotation = checkpoint.SpawnRotation;
        m_hasSpawnPoint = true;
    }

    public void KillPlayer(string deathReason = "Hazard")
    {
        if (m_isRespawning || m_isLevelCompleted)
            return;

        // Report death to tracker / UI
        DeathTracker.Instance?.RecordDeath(deathReason);
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
        ApplyInitialCheckpointOverride();
        CacheInitialSpawn();
        ResetRuntimeObjects();

        if (m_player != null && m_hasSpawnPoint)
        {
            m_player.RespawnAt(m_currentSpawnPosition, m_currentSpawnRotation);
        }
    }

    // Public method to be called by UI when the player presses Play
    public void BeginSession()
    {
        EnsureDeathTrackerExists();
        DeathTracker.Instance?.ResetCounts();
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

        m_isRespawning = false;
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

    private void ApplyInitialCheckpointOverride()
    {
        Checkpoint[] checkpoints = FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] != null && checkpoints[i].IsInitialCheckpoint)
            {
                RegisterCheckpoint(checkpoints[i]);
                return;
            }
        }
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

    private void EnsureDeathTrackerExists()
    {
        if (DeathTracker.Instance != null)
            return;

        GameObject trackerObject = new GameObject("DeathTracker");
        trackerObject.AddComponent<DeathTracker>();
    }
}
