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
    [SerializeField] private AudioClip _finishSound;          // Local player crosses the finish line (separate from the podium victory sound)
    [SerializeField] private AudioClip _raceStartingSound;    // Host started a race ("race about to start" banner appears)
    [SerializeField] private AudioClip _resultsSound;         // Results screen appears (race over, standings shown)

    [Header("Countdown (Ready / Set / GO!)")]
    [Tooltip("Plays when the \"Ready\" banner appears.")]
    [SerializeField] private AudioClip _readySound;
    [Tooltip("Plays when the banner switches to \"Set\".")]
    [SerializeField] private AudioClip _setSound;
    [Tooltip("Plays on \"GO!\", the moment the race starts.")]
    [SerializeField] private AudioClip _goSound;

    [Header("Background Music")]
    [Tooltip("Loops while in the gathering lobby. Fades in on arrival.")]
    [SerializeField] private AudioClip _lobbyMusic;
    [Tooltip("Race music tracks. Starts when players are moved to the start line, plays through the countdown and race, and fades out when you finish. Silent on the results and podium. One is picked at random for each race, never the same one twice in a row when there are several, and every player hears the same track.")]
    [SerializeField] private AudioClip[] _raceMusicTracks;
    [Tooltip("Seconds the lobby music takes to fade in when you arrive in the lobby.")]
    [Min(0f)]
    [SerializeField] private float _lobbyMusicFadeIn = 3f;
    [Tooltip("Seconds the race music takes to fade out once you finish the race.")]
    [Min(0f)]
    [SerializeField] private float _raceMusicFadeOut = 2f;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float _sfxVolume = 0.5f;      // Victory sound volume (halved)
    [Range(0f, 1f)]
    [SerializeField] private float _runningVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float _crowdVolume = 0.25f;   // Crowd sound volume (halved)
    [Range(0f, 1f)]
    [SerializeField] private float _ambienceVolume = 0.15f; // Very low ambient background
    [Range(0f, 1f)]
    [SerializeField] private float _musicVolume = 0.3f;     // Background music (lobby + race)

    private bool _isRunningPlaying = false;
    private bool _isCrowdPlaying = false;
    private bool _isAmbiencePlaying = false;

    // Background music: one looping source, switched between tracks by SetMusic().
    public enum MusicTrack { None, Lobby, Race }
    private AudioSource _musicSource;
    private AudioClip _wantedMusic;        // clip SetMusic last asked for (null = silence)
    private float _musicTargetVolume;      // volume the music is fading towards
    private float _musicFadeRate;          // volume change per second (0 = jump straight there)

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

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;

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

        // Music volume is driven by the fade in Update(); just retarget an audible track.
        if (_musicSource != null && _wantedMusic != null)
            _musicTargetVolume = _musicVolume;
    }

    /// <summary>How many race music tracks are assigned (RaceManager uses this to pick one).</summary>
    public int RaceMusicTrackCount => _raceMusicTracks != null ? _raceMusicTracks.Length : 0;

    /// <summary>
    /// Switch the background music. Safe to call every frame: it only acts when the wanted
    /// track changes. Lobby music fades in; race music starts at full volume; switching to
    /// MusicTrack.None after race music fades it out (Race Music Fade Out), otherwise stops it.
    /// </summary>
    public void SetMusic(MusicTrack track, int raceTrackIndex = 0)
    {
        if (_musicSource == null)
            return;

        AudioClip clip = track == MusicTrack.Lobby ? _lobbyMusic
                       : track == MusicTrack.Race ? GetRaceTrack(raceTrackIndex)
                       : null;

        if (clip == _wantedMusic)
            return;   // nothing changed

        bool wasRaceMusic = _wantedMusic != null && _wantedMusic != _lobbyMusic;
        _wantedMusic = clip;

        if (clip == null)
        {
            // Silence: fade race music out, cut anything else straight away.
            StartMusicFade(0f, wasRaceMusic ? _raceMusicFadeOut : 0f);
            return;
        }

        // New track: start it from the beginning.
        _musicSource.Stop();
        _musicSource.clip = clip;

        if (track == MusicTrack.Lobby)
        {
            _musicSource.volume = 0f;
            StartMusicFade(_musicVolume, _lobbyMusicFadeIn);
        }
        else
        {
            _musicSource.volume = _musicVolume;
            StartMusicFade(_musicVolume, 0f);
        }

        _musicSource.Play();
    }

    private void StartMusicFade(float targetVolume, float seconds)
    {
        _musicTargetVolume = targetVolume;
        float distance = Mathf.Abs(_musicSource.volume - targetVolume);
        _musicFadeRate = seconds > 0f ? Mathf.Max(distance, 0.0001f) / seconds : 0f;
    }

    // Runs the music fades. Unscaled time, so fades still work if the game is paused.
    private void Update()
    {
        if (_musicSource == null)
            return;

        float volume = _musicSource.volume;
        if (!Mathf.Approximately(volume, _musicTargetVolume))
        {
            _musicSource.volume = _musicFadeRate > 0f
                ? Mathf.MoveTowards(volume, _musicTargetVolume, _musicFadeRate * Time.unscaledDeltaTime)
                : _musicTargetVolume;
        }

        // Faded all the way out with nothing wanted: stop the source.
        if (_wantedMusic == null && _musicSource.isPlaying && _musicSource.volume <= 0.0001f)
        {
            _musicSource.Stop();
            _musicSource.clip = null;
        }
    }

    // The race track at this index, or null if there are none (out-of-range indices wrap).
    private AudioClip GetRaceTrack(int index)
    {
        int count = RaceMusicTrackCount;
        if (count == 0)
            return null;

        index = ((index % count) + count) % count;
        return _raceMusicTracks[index];
    }

    /// <summary>
    /// Set master volume for background music
    /// </summary>
    public void SetMusicMasterVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
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
    /// Play the finish line sound effect (local player crossed the line)
    /// </summary>
    public void PlayFinishSound()
    {
        if (_finishSound != null && _sfxSource != null)
        {
            _sfxSource.PlayOneShot(_finishSound, _sfxVolume);
        }
        else if (_finishSound == null)
        {
            Debug.LogWarning("[AudioManager] No Finish Sound assigned. Drag a clip into the Finish Sound slot on the AudioManager object.");
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

    // --- Countdown: one sound per banner (called by RaceHud when the banner changes) ---

    /// <summary>Play the "Ready" countdown sound</summary>
    public void PlayReadySound() => PlaySfx(_readySound);

    /// <summary>Play the "Set" countdown sound</summary>
    public void PlaySetSound() => PlaySfx(_setSound);

    /// <summary>Play the "GO!" countdown sound</summary>
    public void PlayGoSound() => PlaySfx(_goSound);

    /// <summary>Play the results screen sound (when the race ends and standings are shown)</summary>
    public void PlayResultsSound() => PlaySfx(_resultsSound);

    private void PlaySfx(AudioClip clip)
    {
        if (clip != null && _sfxSource != null)
        {
            _sfxSource.PlayOneShot(clip, _sfxVolume);
        }
    }

    /// <summary>
    /// Play the "race about to start" sound (when the host starts a race and the banner appears)
    /// </summary>
    public void PlayRaceStartingSound()
    {
        if (_raceStartingSound != null && _sfxSource != null)
        {
            _sfxSource.PlayOneShot(_raceStartingSound, _sfxVolume);
        }
    }
}
