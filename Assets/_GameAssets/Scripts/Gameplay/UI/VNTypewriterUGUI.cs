using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNTypewriterUGUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI targetText;

        [Header("Typing")]
        [Min(1f)] public float charsPerSecond = 35f;
        [Min(0f)] public float punctuationPauseSeconds = 0.03f; // небольшая пауза при .,!?

        public event Action OnFinished;

        private Coroutine _co;
        private string _full;

        public void Begin(string fullText)
        {
            _full = fullText ?? "";
            StopTyping();

            if (targetText != null) targetText.text = "";
            _co = StartCoroutine(CoType());
        }

        public void RevealInstant()
        {
            StopTyping();
            if (targetText != null) targetText.text = _full ?? "";
            OnFinished?.Invoke();
        }

        public void StopTyping()
        {
            if (_co != null) StopCoroutine(_co);
            _co = null;
        }

        private IEnumerator CoType()
        {
            string s = _full ?? "";
            var sb = new StringBuilder(s.Length);

            int i = 0;
            float delayPerChar = 1f / Mathf.Max(1f, charsPerSecond);

            while (i < s.Length)
            {
                char c = s[i];

                // RichText tag: копируем целиком без задержки
                if (c == '<')
                {
                    int end = s.IndexOf('>', i);
                    if (end >= 0)
                    {
                        sb.Append(s, i, (end - i + 1));
                        i = end + 1;
                        if (targetText != null) targetText.text = sb.ToString();
                        yield return null;
                        continue;
                    }
                }

                sb.Append(c);
                i++;

                if (targetText != null) targetText.text = sb.ToString();

                float wait = delayPerChar;

                if (punctuationPauseSeconds > 0f && (c == '.' || c == '!' || c == '?' || c == ',' || c == ';' || c == ':'))
                    wait += punctuationPauseSeconds;

                yield return new WaitForSeconds(wait);
            }

            _co = null;
            OnFinished?.Invoke();
        }
    }
}