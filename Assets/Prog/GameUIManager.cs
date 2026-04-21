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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        ShowMainMenu();
        if (m_playButton != null)
            m_playButton.onClick.AddListener(OnPlayPressed);
    }

    public void ShowMainMenu()
    {
        if (m_mainMenuScreen != null) m_mainMenuScreen.SetActive(true);
        if (m_levelNameScreen != null) m_levelNameScreen.SetActive(false);
        if (m_deathScreen != null) m_deathScreen.SetActive(false);
    }

    public void OnPlayPressed()
    {
        if (m_mainMenuScreen != null) m_mainMenuScreen.SetActive(false);
        // start session
        CheckpointManager.Instance?.BeginSession();
        // show level name
        StartCoroutine(PlayLevelNameRoutine("Level 1"));
    }

    private IEnumerator PlayLevelNameRoutine(string levelName)
    {
        if (m_levelNameText != null) m_levelNameText.text = levelName;
        if (m_levelNameScreen != null) m_levelNameScreen.SetActive(true);
        yield return new WaitForSeconds(3f);
        if (m_levelNameScreen != null) m_levelNameScreen.SetActive(false);
    }

    public void OnPlayerDied(string reason)
    {
        if (m_deathScreen != null)
        {
            m_deathScreen.SetActive(true);
            if (m_deathCountText != null)
            {
                m_deathCountText.text = "Deaths: " + DeathTracker.Instance.TotalDeaths;
            }
        }
    }
}
