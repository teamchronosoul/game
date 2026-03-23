using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VG;
using YP;

public class Sound : MonoBehaviour
{
    private static Sound instance;

    [SerializeField] private AudioStreamCash audioStreamCash;
    [SerializeField] private SoundsDictionary sounds;

    [Header("Players")]
    [SerializeField] private AudioPlayer _music;
    [SerializeField] private AudioPlayer _sfx;

    [Header("Default Volumes")]
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.1f;
    [SerializeField] [Range(0f, 1f)] private float sfxVolume = 0.2f;

    public static AudioPlayer music => instance != null ? instance._music : null;
    public static AudioPlayer sfx => instance != null ? instance._sfx : null;
    public static bool IsReady => instance != null && instance._music != null && instance._sfx != null;

    private readonly Dictionary<string, int> activeSfx = new();

    private Coroutine _musicFadeRoutine;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private bool CacheNotReady =>
        AudioStreamCash.available &&
        audioStreamCash != null &&
        !audioStreamCash.initialized;

    private static string NormalizeClipName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return Path.GetFileNameWithoutExtension(name.Trim());
    }

    private bool IsSameMusicAlreadyPlaying(AudioClip clip)
    {
        if (_music == null || _music.audioSource == null || !_music.audioSource.isPlaying || clip == null)
            return false;

        var current = _music.audioSource.clip;
        if (current == null)
            return false;

        return NormalizeClipName(current.name) == NormalizeClipName(clip.name);
    }

    private void StopMusicFadeRoutine()
    {
        if (_musicFadeRoutine != null)
        {
            StopCoroutine(_musicFadeRoutine);
            _musicFadeRoutine = null;
        }
    }

    public static void PlayMusic(string key, bool loop)
    {
        PlayMusic(key, loop, 0f);
    }

    public static void PlayMusic(string key, bool loop, float fadeInSeconds)
    {
        if (instance == null || string.IsNullOrWhiteSpace(key))
            return;

        if (instance._music == null || instance._music.audioSource == null)
            return;

        if (instance._music.audioSource.mute || instance.musicVolume <= 0f)
            return;

        AudioClip clip = SoundsDictionary.instance != null
            ? SoundsDictionary.instance.FindSound(key)
            : null;

        if (clip == null)
        {
            Debug.LogWarning($"[Sound] Music clip not found by key: {key}");
            return;
        }

        PlayMusic(clip, loop, fadeInSeconds);
    }

    public static void PlayMusic(AudioClip clip, bool loop)
    {
        PlayMusic(clip, loop, 0f);
    }

    public static void PlayMusic(AudioClip clip, bool loop, float fadeInSeconds)
    {
        if (instance == null || clip == null)
            return;

        if (instance._music == null || instance._music.audioSource == null)
            return;

        if (instance._music.audioSource.mute || instance.musicVolume <= 0f)
            return;

        if (instance.CacheNotReady)
        {
            instance.StartCoroutine(instance.CoPlayMusicWhenReady(clip, loop, fadeInSeconds));
            return;
        }

        instance.PlayMusicInternal(clip, loop, fadeInSeconds);
    }

    private IEnumerator CoPlayMusicWhenReady(AudioClip clip, bool loop, float fadeInSeconds)
    {
        while (CacheNotReady)
            yield return null;

        PlayMusicInternal(clip, loop, fadeInSeconds);
    }

    private void PlayMusicInternal(AudioClip clip, bool loop, float fadeInSeconds)
    {
        if (clip == null || _music == null || _music.audioSource == null)
            return;

        StopMusicFadeRoutine();

        if (IsSameMusicAlreadyPlaying(clip))
            return;

        if (fadeInSeconds > 0f)
        {
            _music.PlayClip(clip, loop ? AudioPlayer.PlayType.Loop : AudioPlayer.PlayType.Simple, 0f);
            _musicFadeRoutine = StartCoroutine(CoFadeMusicTo(musicVolume, fadeInSeconds, stopAfterFade: false));
        }
        else
        {
            _music.PlayClip(clip, loop ? AudioPlayer.PlayType.Loop : AudioPlayer.PlayType.Simple, musicVolume);
        }
    }

    public static void StopMusic(float fadeOutSeconds = 0f)
    {
        if (instance == null || instance._music == null || instance._music.audioSource == null)
            return;

        instance.StopMusicInternal(fadeOutSeconds);
    }

    private void StopMusicInternal(float fadeOutSeconds)
    {
        StopMusicFadeRoutine();

        if (!_music.audioSource.isPlaying)
            return;

        if (fadeOutSeconds > 0f)
            _musicFadeRoutine = StartCoroutine(CoFadeMusicTo(0f, fadeOutSeconds, stopAfterFade: true));
        else
            _music.audioSource.Stop();
    }

    private IEnumerator CoFadeMusicTo(float targetVolume, float duration, bool stopAfterFade)
    {
        if (_music == null || _music.audioSource == null)
            yield break;

        AudioSource source = _music.audioSource;
        float startVolume = source.volume;
        float time = 0f;
        float dur = Mathf.Max(0.0001f, duration);

        while (time < dur)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / dur);
            source.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        source.volume = targetVolume;

        if (stopAfterFade)
        {
            source.Stop();
            source.volume = musicVolume;
        }

        _musicFadeRoutine = null;
    }

    public static void EnableMusic(bool enabled)
    {
        if (instance == null || instance._music == null || instance._music.audioSource == null)
            return;

        instance._music.audioSource.mute = !enabled;
    }

    public static void EnableSound(bool enabled)
    {
        if (instance == null || instance._sfx == null || instance._sfx.audioSource == null)
            return;

        instance._sfx.audioSource.mute = !enabled;
    }

    public static void PlaySFX(string key)
    {
        if (instance == null || string.IsNullOrWhiteSpace(key))
            return;

        AudioClip clip = SoundsDictionary.instance != null
            ? SoundsDictionary.instance.FindSound(key)
            : null;

        if (clip == null)
        {
            Debug.LogWarning($"[Sound] SFX clip not found by key: {key}");
            return;
        }

        PlaySFX(clip);
    }

    public static void PlaySFX(AudioClip clip)
    {
        if (instance == null || clip == null)
            return;

        if (instance._sfx == null || instance._sfx.audioSource == null)
            return;

        if (instance._sfx.audioSource.mute || instance.sfxVolume <= 0f)
            return;

        if (instance.CacheNotReady)
        {
            instance.StartCoroutine(instance.CoPlaySfxWhenReady(clip));
            return;
        }

        instance.PlaySfxInternal(clip);
    }

    private IEnumerator CoPlaySfxWhenReady(AudioClip clip)
    {
        while (CacheNotReady)
            yield return null;

        PlaySfxInternal(clip);
    }

    private void PlaySfxInternal(AudioClip clip)
    {
        if (clip == null || _sfx == null || _sfx.audioSource == null)
            return;

        string key = NormalizeClipName(clip.name);
        if (string.IsNullOrEmpty(key))
            key = clip.name;

        if (activeSfx.TryGetValue(key, out int cnt) && cnt >= 2)
            return;

        _sfx.PlayClip(clip, AudioPlayer.PlayType.OneShot, sfxVolume);

        if (!activeSfx.ContainsKey(key))
            activeSfx[key] = 0;

        activeSfx[key]++;

        StartCoroutine(ReleaseAfter(clip.length, key));
    }

    private IEnumerator ReleaseAfter(float delay, string key)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (activeSfx.TryGetValue(key, out int cnt))
        {
            cnt = Mathf.Max(0, cnt - 1);

            if (cnt == 0)
                activeSfx.Remove(key);
            else
                activeSfx[key] = cnt;
        }
    }
}