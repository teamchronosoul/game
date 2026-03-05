using UnityEngine;
using VG;

namespace YP
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioPlayer : MonoBehaviour
    {
        public enum PlayType
        { Simple, Loop, OneShot };

        private AudioSource _audioSource; public AudioSource audioSource
        {
            get
            {
                _audioSource ??= GetComponent<AudioSource>();
                return _audioSource;
            }
        }

        public void PlayClip(AudioClip clip, PlayType playType, float volume)
        {
            audioSource.clip = AudioStreamCash.available ? AudioStreamCash.GetClip(clip.name) : clip;
            audioSource.volume = volume;
            
            switch (playType)
            {
                case PlayType.Simple:
                    audioSource.loop = false;
                    audioSource.Play();
                    break;

                case PlayType.Loop:
                    audioSource.loop = true;
                    audioSource.Play();
                    break;

                case PlayType.OneShot:
                    audioSource.PlayOneShot(audioSource.clip);
                    break;
            }
        }
    }
}