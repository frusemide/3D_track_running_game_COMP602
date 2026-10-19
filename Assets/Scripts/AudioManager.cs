using UnityEngine;

/// <summary>
/// Manages all game audio including sound effects and background music.
/// This is a singleton that persists across scenes.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _sfxSource;        // For one-shot sound effects
    [SerializeField] private AudioSource _runningSource;    // For looping running sound
    [SerializeField] private AudioSource _crowdSource;      // For looping crowd ambiance
    [SerializeField] private AudioSource _ambienceSource;   // For looping ambient background

    [Header("Sound Effects")]
    [SerializeField] private AudioClip _fallSound;
    [SerializeField] private AudioClip _victorySound;
    [SerializeField] private AudioClip _runningSound;
    [SerializeField] private AudioClip _crowdCheerSound;
    [SerializeField] private AudioClip _buttonClickSound;
    [SerializeField] private AudioClip _ambienceSound;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float _sfxVolume = 0.5f;      // Victory sound volume (halved)
    [Range(0f, 1f)]
    [SerializeField] private float _runningVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float _crowdVolume = 0.25f;   // Crowd sound volume (halved)
    [Range(0f, 1f)]
    [SerializeField] private float _ambienceVolume = 0.15f; // Very low ambient background

    private bool _isRunningPlaying = false;
    private bool _isCrowdPlaying = false;
    private bool _isAmbiencePlaying = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Create audio sources if they don't exist
        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
        }

        if (_runningSource == null)
        {
            _runningSource = gameObject.AddComponent<AudioSource>();
            _runningSource.loop = true;
            _runningSource.playOnAwake = false;
        }

        if (_crowdSource == null)
        {
            _crowdSource = gameObject.AddComponent<AudioSource>();
            _crowdSource.loop = true;
            _crowdSource.playOnAwake = false;
        }

        if (_ambienceSource == null)
        {
            _ambienceSource = gameObject.AddComponent<AudioSource>();
            _ambienceSource.loop = true;
            _ambienceSource.playOnAwake = false;
        }

        UpdateVolumes();
        
        // Start ambience immediately when game starts
        StartAmbienceSound();
    }

    private void UpdateVolumes()
    {
        if (_sfxSource != null)
            _sfxSource.volume = _sfxVolume;

        if (_runningSource != null)
            _runningSource.volume = _runningVolume;

        if (_crowdSource != null)
            _crowdSource.volume = _crowdVolume;

        if (_ambienceSource != null)
            _ambienceSource.volume = _ambienceVolume;
    }

    /// <summary>
    /// Play the fall/stumble sound effect
    /// </summary>
    public void PlayFallSound()
    {
        if (_fallSound != null && _sfxSource != null)
        {
            _sfxSource.PlayOneShot(_fallSound, _sfxVolume);
        }
    }

    /// <summary>
    /// Play the victory sound effect
    /// </summary>
    public void PlayVictorySound()
    {
        if (_victorySound != null && _sfxSource != null)
        {
            _sfxSource.PlayOneShot(_victorySound, _sfxVolume);
        }
    }

    /// <summary>
    /// Start playing the running sound (loops)
    /// </summary>
    public void StartRunningSound()
    {
        if (_runningSound != null && _runningSource != null && !_isRunningPlaying)
        {
            _runningSource.clip = _runningSound;
            _runningSource.pitch = 1f;  // Reset pitch when starting
            _runningSource.Play();
            _isRunningPlaying = true;
        }
    }

    /// <summary>
    /// Stop playing the running sound
    /// </summary>
    public void StopRunningSound()
    {
        if (_runningSource != null && _isRunningPlaying)
        {
            _runningSource.Stop();
            _isRunningPlaying = false;
        }
    }

    /// <summary>
    /// Adjust running sound volume and pitch based on speed (0-1 normalized)
    /// </summary>
    public void SetRunningVolume(float normalizedSpeed)
    {
        if (_runningSource != null)
        {
            _runningSource.volume = Mathf.Lerp(0.3f, _runningVolume, normalizedSpeed);
            // Pitch ranges from 0.8 (slow jog) to 1.4 (full sprint) for realistic speed effect
            _runningSource.pitch = Mathf.Lerp(0.8f, 1.4f, normalizedSpeed);
        }
    }

    /// <summary>
    /// Start playing the crowd cheer background sound (loops)
    /// </summary>
    public void StartCrowdSound()
    {
        if (_crowdCheerSound != null && _crowdSource != null && !_isCrowdPlaying)
        {
            _crowdSource.clip = _crowdCheerSound;
            _crowdSource.Play();
            _isCrowdPlaying = true;
        }
    }

    /// <summary>
    /// Start playing the crowd cheer with a delay (for race start synchronization)
    /// </summary>
    public void StartCrowdSoundDelayed(float delay)
    {
        if (_crowdCheerSound != null && _crowdSource != null && !_isCrowdPlaying)
        {
            StartCoroutine(StartCrowdAfterDelay(delay));
        }
    }

    private System.Collections.IEnumerator StartCrowdAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartCrowdSound();
    }

    /// <summary>
    /// Stop playing the crowd sound
    /// </summary>
    public void StopCrowdSound()
    {
        if (_crowdSource != null && _isCrowdPlaying)
        {
            _crowdSource.Stop();
            _isCrowdPlaying = false;
        }
    }

    /// <summary>
    /// Stop all audio
    /// </summary>
    public void StopAllAudio()
    {
        StopRunningSound();
        StopCrowdSound();

        if (_sfxSource != null)
            _sfxSource.Stop();
    }

    /// <summary>
    /// Set master volume for sound effects
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// <summary>
    /// Set master volume for running sounds
    /// </summary>
    public void SetRunningMasterVolume(float volume)
    {
        _runningVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// <summary>
    /// Set master volume for crowd sounds
    /// </summary>
    public void SetCrowdMasterVolume(float volume)
    {
        _crowdVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    /// <summary>
    /// Play button click sound effect
    /// </summary>
    public void PlayButtonClickSound()
    {
        if (_buttonClickSound != null && _sfxSource != null)
        {
            _sfxSource.PlayOneShot(_buttonClickSound, _sfxVolume);
        }
    }

    /// <summary>
    /// Start playing ambient background sound (loops)
    /// </summary>
    public void StartAmbienceSound()
    {
        if (_ambienceSound != null && _ambienceSource != null && !_isAmbiencePlaying)
        {
            _ambienceSource.clip = _ambienceSound;
            _ambienceSource.Play();
            _isAmbiencePlaying = true;
        }
    }

    /// <summary>
    /// Stop playing ambient background sound
    /// </summary>
    public void StopAmbienceSound()
    {
        if (_ambienceSource != null && _isAmbiencePlaying)
        {
            _ambienceSource.Stop();
            _isAmbiencePlaying = false;
        }
    }
}
