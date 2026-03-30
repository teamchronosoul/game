using UnityEngine;

namespace VN.UI
{
    public class VNAudioController : MonoBehaviour
    {
        public void PlayMusic(AudioClip clip, float fadeInSeconds, bool loop)
        {
            if (clip == null)
                return;

            Sound.PlayMusic(clip, loop, fadeInSeconds);
        }

        public void PlayMusic(string musicKey, float fadeInSeconds, bool loop)
        {
            if (string.IsNullOrWhiteSpace(musicKey))
                return;

            Sound.PlayMusic(musicKey, loop, fadeInSeconds);
        }

        public void StopMusic(float fadeOutSeconds)
        {
            Sound.StopMusic(fadeOutSeconds);
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null)
                return;

            Sound.PlaySFX(clip);
        }

        public void PlaySfx(string sfxKey)
        {
            if (string.IsNullOrWhiteSpace(sfxKey))
                return;

            Sound.PlaySFX(sfxKey);
        }
    }
}