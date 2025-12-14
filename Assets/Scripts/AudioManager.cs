using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")] [SerializeField]
    private AudioClip musicClip;

    [Header("Settings")] [SerializeField] private KeyCode muteToggleKey = KeyCode.M;

    private bool musicMuted;
    private AudioSource musicSource;

    // Audio settings
    private float musicVolume = 1f;
    private bool sfxMuted;
    private AudioSource sfxSource;
    private float sfxVolume = 1f;
    public static AudioManager Instance { get; private set; }

    // Properties for UI binding
    public float MusicVolume
    {
        get => musicVolume;
        set
        {
            musicVolume = Mathf.Clamp01(value);
            UpdateMusicVolume();
        }
    }

    public float SFXVolume
    {
        get => sfxVolume;
        set
        {
            sfxVolume = Mathf.Clamp01(value);
            UpdateSFXVolume();
        }
    }

    public bool MusicMuted
    {
        get => musicMuted;
        set
        {
            musicMuted = value;
            UpdateMusicVolume();
        }
    }

    public bool SFXMuted
    {
        get => sfxMuted;
        set
        {
            sfxMuted = value;
            UpdateSFXVolume();
        }
    }

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Setup music source
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = musicClip;
        musicSource.loop = true;
        musicSource.playOnAwake = false;

        // Setup SFX source (for one-shots)
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        // Load settings from OptionsManager
        // OptionsManager.Instance.LoadSettings();

        musicSource.Play();
    }

    private void Update()
    {
        if (Input.GetKeyDown(muteToggleKey)) ToggleMusicMute();
    }

    public void ToggleMusicMute()
    {
        MusicMuted = !MusicMuted;
        // OptionsManager.Instance.SaveSettings();
    }

    public void ToggleSFXMute()
    {
        SFXMuted = !SFXMuted;
        // OptionsManager.Instance.SaveSettings();
    }

    private void UpdateMusicVolume()
    {
        musicSource.volume = musicMuted ? 0f : musicVolume;
    }

    private void UpdateSFXVolume()
    {
        sfxSource.volume = sfxMuted ? 0f : sfxVolume;
    }

    // Call this to play SFX
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && !sfxMuted) sfxSource.PlayOneShot(clip, sfxVolume);
    }
}