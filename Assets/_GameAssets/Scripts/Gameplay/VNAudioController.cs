using System;
using System.Reflection;
using UnityEngine;

namespace VN.UI
{
    public class VNAudioController : MonoBehaviour
    {
        public void PlayMusic(AudioClip clip, float fadeInSeconds, bool loop)
        {
            PlayMusic(clip, fadeInSeconds, loop, 1f);
        }

        public void PlayMusic(AudioClip clip, float fadeInSeconds, bool loop, float volume)
        {
            if (clip == null)
                return;

            volume = Mathf.Clamp01(volume);

            if (!TryInvokeSoundMethod("PlayMusic", clip, loop, fadeInSeconds, volume))
                Sound.PlayMusic(clip, loop, fadeInSeconds);
        }

        public void PlayMusic(string musicKey, float fadeInSeconds, bool loop)
        {
            PlayMusic(musicKey, fadeInSeconds, loop, 1f);
        }

        public void PlayMusic(string musicKey, float fadeInSeconds, bool loop, float volume)
        {
            if (string.IsNullOrWhiteSpace(musicKey))
                return;

            volume = Mathf.Clamp01(volume);

            if (!TryInvokeSoundMethod("PlayMusic", musicKey, loop, fadeInSeconds, volume))
                Sound.PlayMusic(musicKey, loop, fadeInSeconds);
        }

        public void StopMusic(float fadeOutSeconds)
        {
            Sound.StopMusic(fadeOutSeconds);
        }

        public void PlaySfx(AudioClip clip)
        {
            PlaySfx(clip, 1f);
        }

        public void PlaySfx(AudioClip clip, float volume)
        {
            if (clip == null)
                return;

            volume = Mathf.Clamp01(volume);

            if (!TryInvokeSoundMethod("PlaySFX", clip, volume))
                Sound.PlaySFX(clip);
        }

        public void PlaySfx(string sfxKey)
        {
            PlaySfx(sfxKey, 1f);
        }

        public void PlaySfx(string sfxKey, float volume)
        {
            if (string.IsNullOrWhiteSpace(sfxKey))
                return;

            volume = Mathf.Clamp01(volume);

            if (!TryInvokeSoundMethod("PlaySFX", sfxKey, volume))
                Sound.PlaySFX(sfxKey);
        }

        private static bool TryInvokeSoundMethod(string methodName, params object[] args)
        {
            try
            {
                var methods = typeof(Sound).GetMethods(BindingFlags.Public | BindingFlags.Static);

                for (var i = 0; i < methods.Length; i++)
                {
                    var method = methods[i];

                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                        continue;

                    var parameters = method.GetParameters();
                    if (parameters.Length != args.Length)
                        continue;

                    if (!CanPassArguments(parameters, args))
                        continue;

                    method.Invoke(null, args);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VNAudioController] Failed to call Sound.{methodName} with volume overload: {e.Message}");
            }

            return false;
        }

        private static bool CanPassArguments(ParameterInfo[] parameters, object[] args)
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                var expected = parameters[i].ParameterType;
                var arg = args[i];

                if (arg == null)
                    continue;

                if (!expected.IsInstanceOfType(arg))
                    return false;
            }

            return true;
        }
    }
}
