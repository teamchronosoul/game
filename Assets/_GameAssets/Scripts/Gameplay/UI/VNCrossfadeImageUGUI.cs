using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    // Компонент для кроссфейда спрайта через 2 Image (двойной буфер).
    // Требования:
    // - На объекте должны быть 2 Image (A и B) с одинаковым RectTransform.
    // - Оба должны быть Raycast Target = false (обычно).
    public class VNCrossfadeImageUGUI : MonoBehaviour
    {
        [SerializeField] private Image a;
        [SerializeField] private Image b;

        private bool _aIsFront = true;
        private Coroutine _co;

        public void SetInstant(Sprite sprite, bool visible = true)
        {
            StopFade();

            var front = _aIsFront ? a : b;
            var back  = _aIsFront ? b : a;

            ApplyImage(front, sprite, visible ? 1f : 0f);
            DisableImage(back);
        }

        public void Crossfade(Sprite sprite, float seconds, bool visible = true)
        {
            if (seconds <= 0f)
            {
                SetInstant(sprite, visible);
                return;
            }

            StopFade();
            _co = StartCoroutine(CoCrossfade(sprite, seconds, visible));
        }

        public void Hide(float seconds = 0.2f)
        {
            Crossfade(null, seconds, visible: false);
        }

        private IEnumerator CoCrossfade(Sprite sprite, float seconds, bool visible)
        {
            var from = _aIsFront ? a : b;
            var to   = _aIsFront ? b : a;

            // Подготавливаем "to"
            to.sprite = sprite;
            to.enabled = visible && sprite != null;
            SetAlpha(to, 0f);

            // "from" может быть выключен
            if (from != null && from.enabled == false)
                SetAlpha(from, 0f);

            float fromStart = from != null ? from.color.a : 0f;

            float t = 0f;
            float dur = Mathf.Max(0.001f, seconds);

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);

                float toA = (visible && sprite != null) ? k : 0f;
                float fromA = Mathf.Lerp(fromStart, 0f, k);

                SetAlpha(to, toA);
                SetAlpha(from, fromA);

                yield return null;
            }

            // Финализируем
            SetAlpha(to, (visible && sprite != null) ? 1f : 0f);

            if (from != null)
            {
                SetAlpha(from, 0f);
                from.enabled = false;
            }

            if (!(visible && sprite != null))
            {
                to.enabled = false;
                to.sprite = null;
            }

            _aIsFront = !_aIsFront;
            _co = null;
        }

        private void StopFade()
        {
            if (_co != null)
            {
                StopCoroutine(_co);
                _co = null;
            }
        }

        private static void ApplyImage(Image img, Sprite sprite, float alpha)
        {
            if (img == null) return;

            img.sprite = sprite;
            img.enabled = sprite != null && alpha > 0f;

            SetAlpha(img, alpha);

            // если sprite == null — можно выключить сразу
            if (sprite == null)
            {
                img.enabled = false;
                img.sprite = null;
                SetAlpha(img, 0f);
            }
        }

        private static void DisableImage(Image img)
        {
            if (img == null) return;
            img.enabled = false;
            img.sprite = null;
            SetAlpha(img, 0f);
        }

        private static void SetAlpha(Image img, float a)
        {
            if (img == null) return;
            var c = img.color;
            c.a = Mathf.Clamp01(a);
            img.color = c;
        }
    }
}