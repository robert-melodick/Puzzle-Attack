using UnityEngine;

public class OptionsManager : MonoBehaviour
{
    public static OptionsManager Instance { get; private set; }
    
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string MUSIC_MUTED_KEY = "MusicMuted";
    private const string SFX_MUTED_KEY = "SFXMuted";
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, AudioManager.Instance.MusicVolume);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, AudioManager.Instance.SFXVolume);
        PlayerPrefs.SetInt(MUSIC_MUTED_KEY, AudioManager.Instance.MusicMuted ? 1 : 0);
        PlayerPrefs.SetInt(SFX_MUTED_KEY, AudioManager.Instance.SFXMuted ? 1 : 0);
        PlayerPrefs.Save();
    }
    
    public void LoadSettings()
    {
        AudioManager.Instance.MusicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 1f);
        AudioManager.Instance.SFXVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
        AudioManager.Instance.MusicMuted = PlayerPrefs.GetInt(MUSIC_MUTED_KEY, 0) == 1;
        AudioManager.Instance.SFXMuted = PlayerPrefs.GetInt(SFX_MUTED_KEY, 0) == 1;
    }
    
    public void ResetToDefaults()
    {
        AudioManager.Instance.MusicVolume = 1f;
        AudioManager.Instance.SFXVolume = 1f;
        AudioManager.Instance.MusicMuted = false;
        AudioManager.Instance.SFXMuted = false;
        SaveSettings();
    }
}