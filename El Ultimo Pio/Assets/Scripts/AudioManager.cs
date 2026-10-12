using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================================
//  MÚSICA DE FONDO + SONIDO DE LOS BOTONES
//  - Se pone UNA sola vez en la escena MainMenu y sobrevive a los cambios de escena
//    (la música no se corta al pasar del menú al lobby).
//  - Si hay "Gameplay Music", cambia a esa música durante la partida (con un fundido).
//  - Agrega el MISMO sonido de clic a TODOS los botones de todas las escenas,
//    incluidos los que se crean por código (pantalla de resultados).
// =====================================================================================
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Música de fondo")]
    [Tooltip("Música del menú y del lobby (y de la partida si no pones otra abajo)")]
    [SerializeField] private AudioClip menuMusic;
    [Tooltip("Opcional: música distinta durante la partida")]
    [SerializeField] private AudioClip gameplayMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.4f;
    [SerializeField] private float fadeSeconds = 0.8f;
    [SerializeField] private string gameplaySceneName = "Gameplay";

    [Header("Sonido de los botones")]
    [SerializeField] private AudioClip buttonClick;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.8f;

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        // Si volvemos al menú, ya existe uno: se queda el primero (la música no se reinicia)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (Instance == this) OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookAllButtons();

        AudioClip wanted = (scene.name == gameplaySceneName && gameplayMusic != null) ? gameplayMusic : menuMusic;
        PlayMusic(wanted);
    }

    // =================================================================================
    //  Música
    // =================================================================================

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return; // ya está sonando: no se reinicia

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(SwitchMusic(clip));
    }

    private IEnumerator SwitchMusic(AudioClip clip)
    {
        // Baja la que suena
        if (musicSource.isPlaying)
        {
            float start = musicSource.volume;
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                musicSource.volume = Mathf.Lerp(start, 0f, t / fadeSeconds);
                yield return null;
            }
        }

        // Sube la nueva
        musicSource.clip = clip;
        musicSource.volume = 0f;
        musicSource.Play();
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, musicVolume, t / fadeSeconds);
            yield return null;
        }
        musicSource.volume = musicVolume;
        fadeRoutine = null;
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (fadeRoutine == null) musicSource.volume = musicVolume;
    }

    // =================================================================================
    //  Botones
    // =================================================================================

    private void HookAllButtons()
    {
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include))
            AddClickSound(b);
    }

    // Lo llama GameManager para los botones que crea por código
    public static void AddClickSound(Button b)
    {
        if (b == null || b.GetComponent<ClickSoundMarker>() != null) return;
        b.gameObject.AddComponent<ClickSoundMarker>(); // evita ponerle el sonido dos veces
        b.onClick.AddListener(PlayClick);
    }

    public static void PlayClick()
    {
        if (Instance == null || Instance.buttonClick == null) return;
        Instance.sfxSource.PlayOneShot(Instance.buttonClick, Instance.clickVolume);
    }
}
