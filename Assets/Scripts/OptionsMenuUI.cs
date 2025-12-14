using UnityEngine;
using UnityEngine.UI;

public class OptionsMenuUI : MonoBehaviour
{
    [Header("Music Controls")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Toggle musicMuteToggle;
    
    [Header("SFX Controls")]
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Toggle sfxMuteToggle;
    
    private void OnEnable()
    {
        // Initialize UI with current values
        musicVolumeSlider.value = AudioManager.Instance.MusicVolume;
        sfxVolumeSlider.value = AudioManager.Instance.SFXVolume;
        musicMuteToggle.isOn = AudioManager.Instance.MusicMuted;
        sfxMuteToggle.isOn = AudioManager.Instance.SFXMuted;
        
        // Add listeners
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        musicMuteToggle.onValueChanged.AddListener(OnMusicMuteChanged);
        sfxMuteToggle.onValueChanged.AddListener(OnSFXMuteChanged);
    }
    
    private void OnDisable()
    {
        // Remove listeners
        musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
        musicMuteToggle.onValueChanged.RemoveListener(OnMusicMuteChanged);
        sfxMuteToggle.onValueChanged.RemoveListener(OnSFXMuteChanged);
    }
    
    private void OnMusicVolumeChanged(float value)
    {
        AudioManager.Instance.MusicVolume = value;
        OptionsManager.Instance.SaveSettings();
    }
    
    private void OnSFXVolumeChanged(float value)
    {
        AudioManager.Instance.SFXVolume = value;
        OptionsManager.Instance.SaveSettings();
    }
    
    private void OnMusicMuteChanged(bool muted)
    {
        AudioManager.Instance.MusicMuted = muted;
        OptionsManager.Instance.SaveSettings();
    }
    
    private void OnSFXMuteChanged(bool muted)
    {
        AudioManager.Instance.SFXMuted = muted;
        OptionsManager.Instance.SaveSettings();
    }
}