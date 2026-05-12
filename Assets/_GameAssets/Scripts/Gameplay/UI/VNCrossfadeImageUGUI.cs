using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    // Компонент для кроссфейда через двойной буфер.
    //
    // Работает с:
    // - Image + Sprite
    // - RawImage + Sprite
    // - RawImage + Texture
    //
    // Внешний код менять не нужно:
    // SetInstant(Sprite)
    // Crossfade(Sprite, seconds)
    // Hide(seconds)
    public class VNCrossfadeImageUGUI : MonoBehaviour
    {
        [Header("Image buffer")]
        [SerializeField] private Image a;
        [SerializeField] private Image b;

        [Header("RawImage buffer")]
        [SerializeField] private RawImage rawA;
        [SerializeField] private RawImage rawB;

        private bool _aIsFront = true;
        private Coroutine _co;

        private bool UseRawImage => rawA != null || rawB != null;

        public void SetInstant(Sprite sprite, bool visible = true)
        {
            StopFade();

            if (UseRawImage)
                SetInstantRaw(sprite, visible);
            else
                SetInstantImage(sprite, visible);
        }

        public void Crossfade(Sprite sprite, float seconds, bool visible = true)
        {
            if (seconds <= 0f)
            {
                SetInstant(sprite, visible);
                return;
            }

            StopFade();

            _co = UseRawImage
                ? StartCoroutine(CoCrossfadeRaw(sprite, seconds, visible))
                : StartCoroutine(CoCrossfadeImage(sprite, seconds, visible));
        }

        // Дополнительно: если где-то нужно напрямую передать Texture.
        // Старый код это не ломает.
        public void SetInstant(Texture texture, bool visible = true)
        {
            StopFade();
            SetInstantRaw(texture, visible);
        }

        public void Crossfade(Texture texture, float seconds, bool visible = true)
        {
            if (seconds <= 0f)
            {
                SetInstant(texture, visible);
                return;
            }

            StopFade();
            _co = StartCoroutine(CoCrossfadeRaw(texture, seconds, visible));
        }

        public void Hide(float seconds = 0.2f)
        {
            Crossfade((Sprite)null, seconds, visible: false);
        }

        // =========================
        // Image / Sprite
        // =========================

        private void SetInstantImage(Sprite sprite, bool visible)
        {
            var front = _aIsFront ? a : b;
            var back  = _aIsFront ? b : a;

            ApplyImage(front, sprite, visible ? 1f : 0f);
            DisableImage(back);
        }

        private IEnumerator CoCrossfadeImage(Sprite sprite, float seconds, bool visible)
        {
            var from = _aIsFront ? a : b;
            var to   = _aIsFront ? b : a;

            if (to != null)
            {
                to.sprite = sprite;
                to.enabled = visible && sprite != null;
                SetAlpha(to, 0f);
            }

            if (from != null && from.enabled == false)
                SetAlpha(from, 0f);

            float fromStart = from != null ? from.color.a : 0f;

            float t = 0f;
            float dur = Mathf.Max(0.001f, seconds);

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);

                float toA = visible && sprite != null ? k : 0f;
                float fromA = Mathf.Lerp(fromStart, 0f, k);

                SetAlpha(to, toA);
                SetAlpha(from, fromA);

                yield return null;
            }

            SetAlpha(to, visible && sprite != null ? 1f : 0f);

            if (from != null)
            {
                SetAlpha(from, 0f);
                from.enabled = false;
            }

            if (!(visible && sprite != null))
                DisableImage(to);

            _aIsFront = !_aIsFront;
            _co = null;
        }

        // =========================
        // RawImage / Sprite
        // =========================

        private void SetInstantRaw(Sprite sprite, bool visible)
        {
            var front = _aIsFront ? rawA : rawB;
            var back  = _aIsFront ? rawB : rawA;

            ApplyRawImage(front, sprite, visible ? 1f : 0f);
            DisableRawImage(back);
        }

        private IEnumerator CoCrossfadeRaw(Sprite sprite, float seconds, bool visible)
        {
            var from = _aIsFront ? rawA : rawB;
            var to   = _aIsFront ? rawB : rawA;

            if (to != null)
            {
                ApplySpriteToRawImage(to, sprite);
                to.enabled = visible && sprite != null;
                SetAlpha(to, 0f);
            }

            if (from != null && from.enabled == false)
                SetAlpha(from, 0f);

            float fromStart = from != null ? from.color.a : 0f;

            float t = 0f;
            float dur = Mathf.Max(0.001f, seconds);

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);

                float toA = visible && sprite != null ? k : 0f;
                float fromA = Mathf.Lerp(fromStart, 0f, k);

                SetAlpha(to, toA);
                SetAlpha(from, fromA);

                yield return null;
            }

            SetAlpha(to, visible && sprite != null ? 1f : 0f);

            if (from != null)
            {
                SetAlpha(from, 0f);
                from.enabled = false;
            }

            if (!(visible && sprite != null))
                DisableRawImage(to);

            _aIsFront = !_aIsFront;
            _co = null;
        }

        // =========================
        // RawImage / Texture
        // =========================

        private void SetInstantRaw(Texture texture, bool visible)
        {
            var front = _aIsFront ? rawA : rawB;
            var back  = _aIsFront ? rawB : rawA;

            ApplyRawImage(front, texture, visible ? 1f : 0f);
            DisableRawImage(back);
        }

        private IEnumerator CoCrossfadeRaw(Texture texture, float seconds, bool visible)
        {
            var from = _aIsFront ? rawA : rawB;
            var to   = _aIsFront ? rawB : rawA;

            if (to != null)
            {
                to.texture = texture;
                to.uvRect = new Rect(0f, 0f, 1f, 1f);
                to.enabled = visible && texture != null;
                SetAlpha(to, 0f);
            }

            if (from != null && from.enabled == false)
                SetAlpha(from, 0f);

            float fromStart = from != null ? from.color.a : 0f;

            float t = 0f;
            float dur = Mathf.Max(0.001f, seconds);

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);

                float toA = visible && texture != null ? k : 0f;
                float fromA = Mathf.Lerp(fromStart, 0f, k);

                SetAlpha(to, toA);
                SetAlpha(from, fromA);

                yield return null;
            }

            SetAlpha(to, visible && texture != null ? 1f : 0f);

            if (from != null)
            {
                SetAlpha(from, 0f);
                from.enabled = false;
            }

            if (!(visible && texture != null))
                DisableRawImage(to);

            _aIsFront = !_aIsFront;
            _co = null;
        }


        // Used by Spine characters: keeps Image_A/Image_B as an invisible alignment proxy.
        // The old sprite is assigned and SetNativeSize() is called, but alpha stays 0,
        // so the player does not see the sprite. Spine can then be placed exactly
        // in the center of the same Image where the sprite character would stand.
        public RectTransform PrepareTransparentSpriteProxy(Sprite sprite, bool setNativeSize = true)
        {
            StopFade();

            if (UseRawImage)
                return PrepareTransparentRawProxy(sprite);

            var proxy = a != null ? a : b;
            var other = proxy == a ? b : a;

            if (proxy == null)
                return transform as RectTransform;

            proxy.sprite = sprite;
            proxy.enabled = sprite != null;
            proxy.raycastTarget = false;
            SetAlpha(proxy, 0f);

            if (sprite != null && setNativeSize)
                proxy.SetNativeSize();

            DisableImage(other);

            // Keep Image_A as the reference buffer whenever it exists.
            _aIsFront = proxy == a;

            return proxy.transform as RectTransform;
        }

        private RectTransform PrepareTransparentRawProxy(Sprite sprite)
        {
            var proxy = rawA != null ? rawA : rawB;
            var other = proxy == rawA ? rawB : rawA;

            if (proxy == null)
                return transform as RectTransform;

            ApplySpriteToRawImage(proxy, sprite);
            proxy.enabled = sprite != null;
            proxy.raycastTarget = false;
            SetAlpha(proxy, 0f);
            DisableRawImage(other);

            _aIsFront = proxy == rawA;

            return proxy.transform as RectTransform;
        }


        // Used by Spine alignment: returns the real visual buffer RectTransform, not necessarily this root.
        // Character slot roots are often big containers, while the actual Image child is the place
        // where a sprite character would stand. Aligning Spine to this rect fixes unwanted centering.
        public RectTransform GetReferenceRectTransform()
        {
            if (UseRawImage)
            {
                var front = _aIsFront ? rawA : rawB;
                var back = _aIsFront ? rawB : rawA;

                if (front != null) return front.transform as RectTransform;
                if (back != null) return back.transform as RectTransform;
            }
            else
            {
                var front = _aIsFront ? a : b;
                var back = _aIsFront ? b : a;

                if (front != null) return front.transform as RectTransform;
                if (back != null) return back.transform as RectTransform;
            }

            return transform as RectTransform;
        }

        public void SetInstantHidden()
        {
            StopFade();

            DisableImage(a);
            DisableImage(b);
            DisableRawImage(rawA);
            DisableRawImage(rawB);
        }

        // =========================
        // Helpers
        // =========================

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

            if (sprite == null)
                DisableImage(img);
        }

        private static void DisableImage(Image img)
        {
            if (img == null) return;

            img.enabled = false;
            img.sprite = null;
            SetAlpha(img, 0f);
        }

        private static void ApplyRawImage(RawImage img, Sprite sprite, float alpha)
        {
            if (img == null) return;

            ApplySpriteToRawImage(img, sprite);

            img.enabled = sprite != null && alpha > 0f;
            SetAlpha(img, alpha);

            if (sprite == null)
                DisableRawImage(img);
        }

        private static void ApplyRawImage(RawImage img, Texture texture, float alpha)
        {
            if (img == null) return;

            img.texture = texture;
            img.uvRect = new Rect(0f, 0f, 1f, 1f);
            img.enabled = texture != null && alpha > 0f;

            SetAlpha(img, alpha);

            if (texture == null)
                DisableRawImage(img);
        }

        private static void DisableRawImage(RawImage img)
        {
            if (img == null) return;

            img.enabled = false;
            img.texture = null;
            img.uvRect = new Rect(0f, 0f, 1f, 1f);
            SetAlpha(img, 0f);
        }

        private static void ApplySpriteToRawImage(RawImage img, Sprite sprite)
        {
            if (img == null) return;

            if (sprite == null)
            {
                img.texture = null;
                img.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            img.texture = sprite.texture;
            img.uvRect = GetSpriteUVRect(sprite);
        }

        private static Rect GetSpriteUVRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return new Rect(0f, 0f, 1f, 1f);

            Rect textureRect = sprite.textureRect;
            Texture texture = sprite.texture;

            return new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height
            );
        }

        private static void SetAlpha(Graphic graphic, float a)
        {
            if (graphic == null) return;

            var c = graphic.color;
            c.a = Mathf.Clamp01(a);
            graphic.color = c;
        }
    }
}