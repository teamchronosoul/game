using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VG;
using YP;

public class Sound : MonoBehaviour
{
    private void Awake()
    {
        instance = this;
    }

    private static Sound instance;
    [SerializeField] private AudioStreamCash audioStreamCash;
    [SerializeField] private SoundsDictionary sounds;
    [SerializeField] private AudioPlayer _music; public static AudioPlayer music => instance._music;
    [SerializeField] private AudioPlayer _sfx; public static AudioPlayer sfx => instance._sfx;
    private readonly Dictionary<string, int> activeSfx = new(); 
    
    private static bool musicClipExists => instance != null
                                           && instance._music != null
                                           && instance._music.audioSource != null
                                           && instance._music.audioSource.clip != null;

    private static IEnumerator waitCash(string key, bool loop)
    {
        while (!instance.audioStreamCash.initialized)
            yield return new WaitForSecondsRealtime(0.2f);

        var clip = instance.sounds.FindSound(key);
    }

    public static void PlayMusic(string key, bool loop)
    {
        if (instance._music.audioSource.volume == 0f) return;

        AudioClip clip = SoundsDictionary.instance.FindSound(key);
        instance._music.PlayClip(clip, loop ? AudioPlayer.PlayType.Loop : AudioPlayer.PlayType.Simple, .1f);
    }

    public static void EnableMusic(bool en)
    {
        instance._music.audioSource.mute = !en;
    }

    public static void EnableSound(bool en)
    {
        instance._sfx.audioSource.mute = !en;
    }

    public static void PlaySFX(string key)
    {
        if (instance._sfx.audioSource.volume == 0f) return;

        if (instance.activeSfx.TryGetValue(key, out int cnt) && cnt >= 2)
            return;                                    

        AudioClip clip = SoundsDictionary.instance.FindSound(key);
        

        instance._sfx.PlayClip(clip, AudioPlayer.PlayType.OneShot, .2f);

        if (!instance.activeSfx.ContainsKey(key)) instance.activeSfx[key] = 0;
        instance.activeSfx[key]++;

        instance.StartCoroutine(instance.ReleaseAfter(clip.length, key));
    }
    
    private IEnumerator ReleaseAfter(float delay, string key)
    {
        yield return new WaitForSeconds(delay);

        if (activeSfx.TryGetValue(key, out int cnt))
        {
            cnt = Mathf.Max(0, cnt - 1);
            if (cnt == 0) activeSfx.Remove(key);
            else          activeSfx[key] = cnt;
        }
    }
}