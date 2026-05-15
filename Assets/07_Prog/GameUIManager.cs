using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance;

    [Header("Screens")]
    [SerializeField] private GameObject m_mainMenuScreen;
    [SerializeField] private Button m_playButton;

    [SerializeField] private GameObject m_levelNameScreen;
    [SerializeField] private Text m_levelNameText;

    [SerializeField] private GameObject m_deathScreen;
    [SerializeField] private Text m_deathCountText;

    [SerializeField] private GameObject m_endScreen;

    [Header("Level flow")]
    [SerializeField] private string m_levelDisplayName = "THE TALE";
    [SerializeField] private float m_levelNameDuration = 3f;

    private Coroutine m_levelNameRoutine;

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

        ShowMainMenu();
        RefreshDeathCount();
    }

    private void OnDestroy()
    {
        if (m_playButton != null)
            m_playButton.onClick.RemoveListener(OnPlayPressed);
    }

    public void ShowMainMenu()
    {
        StopLevelNameRoutine();

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

        if (m_deathScreen != null)
            m_deathScreen.SetActive(true);
    }

    public void OnLevelCompleted()
    {
        StopLevelNameRoutine();

        CanProcessGameplayInput = false;
        Time.timeScale = 0f;
        SetCursorState(false);

        if (m_mainMenuScreen != null) m_mainMenuScreen.SetActive(false);
        if (m_levelNameScreen != null) m_levelNameScreen.SetActive(false);
        if (m_deathScreen != null) m_deathScreen.SetActive(false);
        if (m_endScreen != null) m_endScreen.SetActive(true);
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
        if (m_deathScreen != null) m_deathScreen.SetActive(true);

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
        m_deathCountText.text = "Deaths: " + totalDeaths;
    }

    private void StopLevelNameRoutine()
    {
        if (m_levelNameRoutine == null)
            return;

        StopCoroutine(m_levelNameRoutine);
        m_levelNameRoutine = null;
    }

    private static void SetCursorState(bool gameplayActive)
    {
        Cursor.lockState = gameplayActive ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !gameplayActive;
    }
}
