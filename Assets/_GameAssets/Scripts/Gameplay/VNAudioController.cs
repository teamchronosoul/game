using System.Collections;
using UnityEngine;

namespace VN.UI
{
    public class VNAudioController : MonoBehaviour
    {
        [Header("Music")]
        [SerializeField] private AudioSource musicA;
        [SerializeField] private AudioSource musicB;

        [Header("SFX")]
        [SerializeField] private AudioSource sfxSource;

        private bool _aActive = true;
        private Coroutine _musicCo;

        public void PlayMusic(AudioClip clip, float fadeInSeconds, bool loop)
        {
            if (clip == null) return;

            StopMusicInternal(); // останавливаем текущую кросс-корутину

            var from = _aActive ? musicA : musicB;
            var to = _aActive ? musicB : musicA;

            // если тот же клип уже играет – ничего
            if (from != null && from.isPlaying && from.clip == clip)
                return;

            _musicCo = StartCoroutine(CoCrossfade(from, to, clip, fadeInSeconds, loop));
            _aActive = !_aActive;
        }

        public void StopMusic(float fadeOutSeconds)
        {
            StopMusicInternal();
            _musicCo = StartCoroutine(CoFadeOutActive(fadeOutSeconds));
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip);
        }

        private void StopMusicInternal()
        {
            if (_musicCo != null) StopCoroutine(_musicCo);
            _musicCo = null;
        }

        private IEnumerator CoCrossfade(AudioSource from, AudioSource to, AudioClip clip, float fadeIn, bool loop)
        {
            if (to == null) yield break;

            float fromStart = (from != null) ? from.volume : 0f;

            to.clip = clip;
            to.loop = loop;
            to.volume = 0f;
            to.Play();

            float t = 0f;
            float dur = Mathf.Max(0.001f, fadeIn);

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);

                to.volume = k;
                if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);

                yield return null;
            }

            to.volume = 1f;

            if (from != null)
            {
                from.volume = 0f;
                from.Stop();
            }

            _musicCo = null;
        }

        private IEnumerator CoFadeOutActive(float fadeOut)
        {
            var active = _aActive ? musicB : musicA; // потому что _aActive уже переключён на последнем PlayMusic
            var other = _aActive ? musicA : musicB;

            // если нет активного, просто стопаем оба
            if (active == null)
            {
                if (musicA != null) musicA.Stop();
                if (musicB != null) musicB.Stop();
                yield break;
            }

            float start = active.volume;
            float t = 0f;
            float dur = Mathf.Max(0.001f, fadeOut);

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                active.volume = Mathf.Lerp(start, 0f, k);
                yield return null;
            }

            active.volume = 0f;
            active.Stop();

            if (other != null)
            {
                other.volume = 0f;
                other.Stop();
            }

            _musicCo = null;
        }
    }
}