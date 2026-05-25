using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance;

    [Header("Screens")]
    [SerializeField] private GameObject m_mainMenuScreen;
    [SerializeField] private Button m_playButton;
    [SerializeField] private Button m_quitButton;

    [SerializeField] private GameObject m_levelNameScreen;
    [SerializeField] private Text m_levelNameText;

    [SerializeField] private GameObject m_deathScreen;
    [SerializeField] private Text m_deathCountText;

    [SerializeField] private GameObject m_endScreen;

    [Header("Level flow")]
    [SerializeField] private string m_levelDisplayName = "LA VILLE";
    [SerializeField] private float m_levelNameDuration = 3f;
    [SerializeField] private float m_deathCountDisplayDuration = 2f;

    private Coroutine m_levelNameRoutine;
    private Coroutine m_deathScreenRoutine;

    public bool CanProcessGameplayInput { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (m_playButton != null)
            m_playButton.onClick.AddListener(OnPlayPressed);

        if (m_quitButton != null)
            m_quitButton.onClick.AddListener(OnQuitPressed);

        ShowMainMenu();
        RefreshDeathCount();
    }

    private void OnDestroy()
    {
        if (m_playButton != null)
            m_playButton.onClick.RemoveListener(OnPlayPressed);

        if (m_quitButton != null)
            m_quitButton.onClick.RemoveListener(OnQuitPressed);
    }

    public void ShowMainMenu()
    {
        StopLevelNameRoutine();
        StopDeathScreenRoutine();

        CanProcessGameplayInput = false;
        Time.timeScale = 0f;
        SetCursorState(false);

        if (m_mainMenuScreen != null) m_mainMenuScreen.SetActive(true);
        if (m_levelNameScreen != null) m_levelNameScreen.SetActive(false);
        if (m_deathScreen != null) m_deathScreen.SetActive(false);
        if (m_endScreen != null) m_endScreen.SetActive(false);
    }

    public void OnPlayPressed()
    {
        if (m_levelNameRoutine != null)
            return;

        StopDeathScreenRoutine();
        CanProcessGameplayInput = false;
        if (m_mainMenuScreen != null) m_mainMenuScreen.SetActive(false);
        if (m_deathScreen != null) m_deathScreen.SetActive(false);
        if (m_endScreen != null) m_endScreen.SetActive(false);

        CheckpointManager.Instance?.BeginSession();
        RefreshDeathCount();

        m_levelNameRoutine = StartCoroutine(PlayLevelNameRoutine());
    }

    public void OnPlayerDied(string reason)
    {
        RefreshDeathCount();

        if (m_endScreen != null && m_endScreen.activeSelf)
            return;

        ShowDeathCountTemporarily();
    }

    public void OnLevelCompleted()
    {
        StopLevelNameRoutine();
        StopDeathScreenRoutine();

        CanProcessGameplayInput = false;
        Time.timeScale = 0f;
        SetCursorState(false);

        if (m_mainMenuScreen != null) m_mainMenuScreen.SetActive(false);
        if (m_levelNameScreen != null) m_levelNameScreen.SetActive(false);
        if (m_deathScreen != null) m_deathScreen.SetActive(false);
        if (m_endScreen != null) m_endScreen.SetActive(true);
    }

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator PlayLevelNameRoutine()
    {
        if (m_levelNameText != null) m_levelNameText.text = m_levelDisplayName;
        if (m_levelNameScreen != null) m_levelNameScreen.SetActive(false);

        Time.timeScale = 0f;
        SetCursorState(false);

        if (m_levelNameScreen != null) m_levelNameScreen.SetActive(true);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, m_levelNameDuration));

        if (m_levelNameScreen != null) m_levelNameScreen.SetActive(false);
        if (m_deathScreen != null) m_deathScreen.SetActive(false);

        CanProcessGameplayInput = true;
        Time.timeScale = 1f;
        SetCursorState(true);
        m_levelNameRoutine = null;
    }

    private void RefreshDeathCount()
    {
        if (m_deathCountText == null)
            return;

        int totalDeaths = DeathTracker.Instance != null ? DeathTracker.Instance.TotalDeaths : 0;
        m_deathCountText.text = "Morts: " + totalDeaths;
    }

    private void StopLevelNameRoutine()
    {
        if (m_levelNameRoutine == null)
            return;

        StopCoroutine(m_levelNameRoutine);
        m_levelNameRoutine = null;
    }

    private void ShowDeathCountTemporarily()
    {
        if (m_deathScreen == null)
            return;

        StopDeathScreenRoutine();
        m_deathScreen.SetActive(true);
        m_deathScreenRoutine = StartCoroutine(HideDeathScreenAfterDelay());
    }

    private IEnumerator HideDeathScreenAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, m_deathCountDisplayDuration));

        if (m_deathScreen != null)
            m_deathScreen.SetActive(false);

        m_deathScreenRoutine = null;
    }

    private void StopDeathScreenRoutine()
    {
        if (m_deathScreenRoutine == null)
            return;

        StopCoroutine(m_deathScreenRoutine);
        m_deathScreenRoutine = null;
    }

    private static void SetCursorState(bool gameplayActive)
    {
        Cursor.lockState = gameplayActive ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !gameplayActive;
    }
}
