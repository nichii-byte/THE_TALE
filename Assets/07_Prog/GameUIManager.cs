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

    [SerializeField] private GameObject m_endScreen;

    [Header("Audio")]
    [SerializeField] private AudioSource m_musicSource;
    [SerializeField] private AudioClip m_mainMenuMusic;
    [SerializeField] private AudioClip m_gameplayMusic;
    [SerializeField] [Range(0f, 1f)] private float m_mainMenuMusicVolume = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float m_gameplayMusicVolume = 0.7f;

    [Header("Level flow")]
    [SerializeField] private string m_levelDisplayName = "LA VILLE";
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

        if (m_quitButton != null)
            m_quitButton.onClick.AddListener(OnQuitPressed);

        ShowMainMenu();
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

        CanProcessGameplayInput = false;
        Time.timeScale = 0f;
        SetCursorState(false);

        if (m_mainMenuScreen != null) m_mainMenuScreen.SetActive(true);
        if (m_levelNameScreen != null) m_levelNameScreen.SetActive(false);
        if (m_deathScreen != null) m_deathScreen.SetActive(false);
        if (m_endScreen != null) m_endScreen.SetActive(false);

        PlayMusic(m_mainMenuMusic, m_mainMenuMusicVolume);
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
        PlayMusic(m_gameplayMusic, m_gameplayMusicVolume);

        m_levelNameRoutine = StartCoroutine(PlayLevelNameRoutine());
    }

    public void OnPlayerDied(string _)
    {
        if (m_endScreen != null && m_endScreen.activeSelf)
            return;

        if (m_deathScreen != null)
            m_deathScreen.SetActive(false);
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

    private void PlayMusic(AudioClip clip, float volume)
    {
        EnsureMusicSource();
        if (m_musicSource == null)
            return;

        m_musicSource.playOnAwake = false;
        m_musicSource.loop = true;
        m_musicSource.volume = Mathf.Clamp01(volume);

        if (clip == null)
        {
            m_musicSource.Stop();
            m_musicSource.clip = null;
            return;
        }

        if (m_musicSource.clip == clip && m_musicSource.isPlaying)
            return;

        m_musicSource.clip = clip;
        m_musicSource.Play();
    }

    private void EnsureMusicSource()
    {
        if (m_musicSource == null)
            m_musicSource = GetComponent<AudioSource>();

        if (m_musicSource == null)
            m_musicSource = gameObject.AddComponent<AudioSource>();
    }
}
