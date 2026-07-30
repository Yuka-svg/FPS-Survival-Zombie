using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using cowsins;

/// <summary>
/// Central background music (BGM) controller. Singleton, DontDestroyOnLoad.
/// Uses two AudioSources for smooth crossfades and ignores AudioListener.pause
/// so music keeps playing during credits/paused cutscenes.
///
/// Volume is driven by PlayerPrefs keys "musicVolume" and "masterVolume" (0-1),
/// which are the same keys the existing pause-menu sliders write to. If a
/// GameSettingsManager with a masterMixer is present, AudioSources are also
/// routed through its "Music" mixer group for additional mixer-level control.
///
/// Integration points:
///   - MainMenuManager.Start()           -> PlayMenuMusic()
///   - MainMenuManager (load game scene) -> PlayChapterMusic(1)
///   - StoryManager.OnChapterChanged     -> PlayChapterMusic(newChapter)
///   - SpecialEnemyDirector.TrySpawnTank -> PlayFinalBossMusic()
///   - TankBossAI.Die()                   -> StopFinalBossMusic()
///   - CreditsSequence.Play()            -> PlayAfterCreditMusic()
///   - CreditsSequence (load main menu)  -> PlayMenuMusic() (auto via scene load)
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Music Clips")]
    [Tooltip("Main menu background music (loops).")]
    public AudioClip menuMusic;
    [Tooltip("Chapter 1 background music (loops). Leave null for silent Ch1.")]
    public AudioClip chapter1Music;
    [Tooltip("Chapter 2 background music (loops).")]
    public AudioClip chapter2Music;
    [Tooltip("Chapter 3 background music (loops).")]
    public AudioClip chapter3Music;
    [Tooltip("Chapter 4 background music (loops).")]
    public AudioClip chapter4Music;
    [Tooltip("Chapter 5 background music (loops).")]
    public AudioClip chapter5Music;
    [Tooltip("Final boss (Tank) fight music. Crossfades in when a Tank spawns, out when it dies.")]
    public AudioClip finalBossMusic;
    [Tooltip("Music played during the credits sequence.")]
    public AudioClip afterCreditMusic;

    [Header("Fade")]
    [Tooltip("Default crossfade duration between tracks (seconds).")]
    public float crossfadeDuration = 2f;
    [Tooltip("Fade-in duration when starting music from silence.")]
    public float fadeInDuration = 1.5f;
    [Tooltip("Fade-out duration when stopping music.")]
    public float fadeOutDuration = 1.5f;

    [Header("Volume")]
    [Tooltip("Base music volume (0-1) before the music/master slider multipliers are applied.")]
    [Range(0f, 1f)]
    public float baseVolume = 0.6f;
    [Tooltip("How often (seconds) to re-read volume settings from PlayerPrefs in case the user changed them.")]
    public float volumePollInterval = 0.5f;

    private AudioSource _srcA;
    private AudioSource _srcB;
    private AudioSource _active;

    // Per-source fade values (0-1) driven by crossfade/fade routines. The actual
    // AudioSource.volume is computed each frame as: fadeVolume * settingsVolume * baseVolume.
    private float _fadeA;
    private float _fadeB;
    private float _settingsVolume = 1f; // musicVolume * masterVolume from PlayerPrefs.
    private float _volumePollTimer;

    private AudioClip _currentChapterClip; // The chapter music currently in effect (for boss-music return).
    private int _currentChapter = 0;       // 0 = none, 1-5 = chapters.
    private bool _inFinalBoss;             // True while final-boss music is playing.
    private Coroutine _fadeCo;
    private bool _mixerRouted;             // True once AudioSources have been routed to the Music mixer group.

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _srcA = gameObject.AddComponent<AudioSource>();
        _srcB = gameObject.AddComponent<AudioSource>();
        SetupSource(_srcA);
        SetupSource(_srcB);
        _active = _srcA;
        _fadeA = 0f;
        _fadeB = 0f;
        RefreshSettingsVolume();
    }

    private void Start()
    {
        // GameSettingsManager may not be awake yet in the MainMenu scene — retry here.
        TryRouteToMixerGroup();
    }

    private void SetupSource(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = true;
        s.priority = 0; // Highest priority for music.
        s.spatialBlend = 0f; // 2D.
        s.volume = 0f;
        s.ignoreListenerPause = true; // Keep playing during AudioListener.pause (credits).
    }

    /// <summary>
    /// Attempts to route both AudioSources through the "Music" group of
    /// GameSettingsManager.masterMixer. Safe to call multiple times — only
    /// applies the routing once. Called from Start() and HandleSceneLoaded().
    /// </summary>
    private void TryRouteToMixerGroup()
    {
        if (_mixerRouted) return;
        var gsm = GameSettingsManager.Instance;
        if (gsm == null || gsm.masterMixer == null) return;

        AudioMixerGroup[] groups = gsm.masterMixer.FindMatchingGroups("Music");
        if (groups == null || groups.Length == 0) return;

        _srcA.outputAudioMixerGroup = groups[0];
        _srcB.outputAudioMixerGroup = groups[0];
        _mixerRouted = true;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-attempt mixer routing (GameSettingsManager may have just been created).
        TryRouteToMixerGroup();

        // Auto-handle menu music when the MainMenu scene loads (defensive —
        // MainMenuManager.Start also calls PlayMenuMusic, but this catches cases
        // like returning from credits where the manager may have been destroyed).
        if (scene.name.ToLowerInvariant().Contains("menu"))
        {
            PlayMenuMusic();
        }
    }

    private void Update()
    {
        // Periodically re-read volume settings from PlayerPrefs so changes made
        // in the pause menu take effect without requiring an explicit callback.
        _volumePollTimer -= Time.unscaledDeltaTime;
        if (_volumePollTimer <= 0f)
        {
            _volumePollTimer = volumePollInterval;
            RefreshSettingsVolume();
        }

        // Apply the combined volume to both sources each frame.
        float v = _settingsVolume * baseVolume;
        _srcA.volume = _fadeA * v;
        _srcB.volume = _fadeB * v;
    }

    /// <summary>Reads musicVolume and masterVolume from PlayerPrefs and combines them.</summary>
    private void RefreshSettingsVolume()
    {
        float musicVol = PlayerPrefs.GetFloat("musicVolume", 1f);
        float masterVol = PlayerPrefs.GetFloat("masterVolume", 1f);
        _settingsVolume = Mathf.Clamp01(musicVol) * Mathf.Clamp01(masterVol);
    }

    /// <summary>Call this to force an immediate re-read of volume settings (e.g. right after the user closes the settings panel).</summary>
    public void RefreshVolume() => RefreshSettingsVolume();

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Plays the main menu music with a fade-in.</summary>
    public void PlayMenuMusic()
    {
        _inFinalBoss = false;
        _currentChapter = 0;
        _currentChapterClip = null;
        CrossfadeTo(menuMusic, crossfadeDuration);
    }

    /// <summary>Plays the chapter background music for the given chapter (1-5).</summary>
    public void PlayChapterMusic(int chapter)
    {
        if (chapter < 1 || chapter > 5) return;
        _currentChapter = chapter;
        _currentChapterClip = GetChapterClip(chapter);
        _inFinalBoss = false;
        CrossfadeTo(_currentChapterClip, crossfadeDuration);
    }

    /// <summary>Crossfades to the final-boss music. Remembers the current chapter
    /// music so StopFinalBossMusic() can return to it.</summary>
    public void PlayFinalBossMusic()
    {
        if (_inFinalBoss) return;
        _inFinalBoss = true;
        CrossfadeTo(finalBossMusic, crossfadeDuration);
    }

    /// <summary>Crossfades back to the current chapter music after the final boss
    /// dies. If no chapter music is set (e.g. boss spawned outside a chapter),
    /// fades to silence.</summary>
    public void StopFinalBossMusic()
    {
        if (!_inFinalBoss) return;
        _inFinalBoss = false;
        CrossfadeTo(_currentChapterClip, crossfadeDuration);
    }

    /// <summary>Plays the credits music.</summary>
    public void PlayAfterCreditMusic()
    {
        _inFinalBoss = false;
        CrossfadeTo(afterCreditMusic, crossfadeDuration);
    }

    /// <summary>Stops all music with a fade-out.</summary>
    public void StopMusic()
    {
        _inFinalBoss = false;
        FadeOutActive(fadeOutDuration);
    }

    /// <summary>
    /// Crossfades from the currently playing track to a new clip. If the new
    /// clip is null, fades to silence. If the same clip is already playing,
    /// does nothing. If no track is currently playing, fades in.
    /// </summary>
    public void CrossfadeTo(AudioClip clip, float duration)
    {
        if (clip == null)
        {
            FadeOutActive(duration);
            return;
        }

        if (_active != null && _active.isPlaying && _active.clip == clip)
            return; // Already playing this track.

        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(CrossfadeRoutine(clip, duration));
    }

    /// <summary>Plays a clip immediately with a fade-in (no fade-out of previous).</summary>
    public void PlayMusic(AudioClip clip, float fadeDuration)
    {
        if (clip == null) return;
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeInRoutine(clip, fadeDuration));
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    private AudioClip GetChapterClip(int chapter)
    {
        switch (chapter)
        {
            case 1: return chapter1Music;
            case 2: return chapter2Music;
            case 3: return chapter3Music;
            case 4: return chapter4Music;
            case 5: return chapter5Music;
            default: return null;
        }
    }

    private IEnumerator CrossfadeRoutine(AudioClip clip, float duration)
    {
        if (duration <= 0f) duration = 0.01f;

        // Determine the incoming source (the one not currently active).
        AudioSource incoming = (_active == _srcA) ? _srcB : _srcA;
        AudioSource outgoing = _active;
        bool incomingIsA = (incoming == _srcA);

        // Setup incoming.
        incoming.clip = clip;
        if (incomingIsA) _fadeA = 0f; else _fadeB = 0f;
        incoming.Play();

        // If outgoing is playing, fade it out; otherwise we just fade in.
        bool hasOutgoing = outgoing != null && outgoing.isPlaying;
        float outStart = hasOutgoing ? (outgoing == _srcA ? _fadeA : _fadeB) : 0f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(t / duration);
            // Ease in-out.
            float e = f * f * (3f - 2f * f);
            if (incomingIsA) _fadeA = e; else _fadeB = e;
            if (hasOutgoing)
            {
                if (outgoing == _srcA) _fadeA = outStart * (1f - e);
                else _fadeB = outStart * (1f - e);
            }
            yield return null;
        }

        if (incomingIsA) _fadeA = 1f; else _fadeB = 1f;
        if (hasOutgoing)
        {
            if (outgoing == _srcA) _fadeA = 0f; else _fadeB = 0f;
            outgoing.Stop();
        }

        _active = incoming;
        _fadeCo = null;
    }

    private IEnumerator FadeInRoutine(AudioClip clip, float duration)
    {
        if (duration <= 0f) duration = 0.01f;
        AudioSource incoming = (_active == _srcA) ? _srcB : _srcA;
        AudioSource outgoing = _active;
        bool incomingIsA = (incoming == _srcA);

        incoming.clip = clip;
        if (incomingIsA) _fadeA = 0f; else _fadeB = 0f;
        incoming.Play();

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(t / duration);
            if (incomingIsA) _fadeA = f; else _fadeB = f;
            yield return null;
        }
        if (incomingIsA) _fadeA = 1f; else _fadeB = 1f;

        if (outgoing != null && outgoing.isPlaying)
        {
            outgoing.Stop();
            if (outgoing == _srcA) _fadeA = 0f; else _fadeB = 0f;
        }
        _active = incoming;
        _fadeCo = null;
    }

    private void FadeOutActive(float duration)
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        if (duration <= 0f) duration = 0.01f;
        AudioSource outgoing = _active;
        if (outgoing == null || !outgoing.isPlaying)
        {
            _fadeCo = null;
            yield break;
        }

        bool outIsA = (outgoing == _srcA);
        float startFade = outIsA ? _fadeA : _fadeB;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(t / duration);
            if (outIsA) _fadeA = startFade * (1f - f);
            else _fadeB = startFade * (1f - f);
            yield return null;
        }
        if (outIsA) _fadeA = 0f; else _fadeB = 0f;
        outgoing.Stop();
        _fadeCo = null;
    }
}
