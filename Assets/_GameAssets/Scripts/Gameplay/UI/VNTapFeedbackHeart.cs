using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNTapFeedbackHeart : MonoBehaviour
    {
        [SerializeField] private RectTransform canvasRoot;
        [SerializeField] private Image heartPrefab;

        [Min(0.1f)] public float lifeSeconds = 1.2f;
        public float scaleFrom = 1.0f;
        public float scaleTo = 1.25f;

        public void Spawn(Vector2 screenPosition)
        {
            if (canvasRoot == null || heartPrefab == null) return;

            var img = Instantiate(heartPrefab, canvasRoot);
            var rt = img.rectTransform;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screenPosition, null, out var local);
            rt.anchoredPosition = local;

            img.gameObject.SetActive(true);
            StartCoroutine(CoAnim(img));
        }

        private IEnumerator CoAnim(Image img)
        {
            float t = 0f;

            var rt = img.rectTransform;
            var c0 = img.color;

            while (t < lifeSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / lifeSeconds);

                float a = Mathf.Lerp(1f, 0f, k);
                img.color = new Color(c0.r, c0.g, c0.b, a);

                float sc = Mathf.Lerp(scaleFrom, scaleTo, k);
                rt.localScale = Vector3.one * sc;

                yield return null;
            }

            Destroy(img.gameObject);
        }
    }
}