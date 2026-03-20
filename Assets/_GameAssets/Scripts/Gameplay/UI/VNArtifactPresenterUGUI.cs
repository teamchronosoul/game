using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNArtifactPresenterUGUI : MonoBehaviour
    {
        [Header("Runner")]
        [SerializeField] private VN.VNRunner runner;

        [Header("Roots")]
        [SerializeField] private GameObject root;

        [Header("Dim")]
        [SerializeField] private Image dimImage;

        [Header("Artifact")]
        [SerializeField] private Image artifactImage;
        [SerializeField] private RectTransform artifactTransform;

        [Header("Character slots to hide")]
        [SerializeField] private VNCrossfadeImageUGUI leftSlot;
        [SerializeField] private VNCrossfadeImageUGUI centerSlot;
        [SerializeField] private VNCrossfadeImageUGUI rightSlot;
        [SerializeField] private float hideCharactersFadeSeconds = 0.2f;

        private Coroutine _routine;

        private void Awake()
        {
            if (root != null)
                root.SetActive(false);

            if (dimImage != null)
            {
                var c = dimImage.color;
                c.a = 0f;
                dimImage.color = c;
            }

            if (artifactImage != null)
            {
                var c = artifactImage.color;
                c.a = 0f;
                artifactImage.color = c;
            }

            if (artifactTransform != null)
                artifactTransform.localScale = Vector3.zero;
        }

        private void OnEnable()
        {
            if (runner != null)
                runner.OnArtifactShown += HandleArtifactShown;
        }

        private void OnDisable()
        {
            if (runner != null)
                runner.OnArtifactShown -= HandleArtifactShown;
        }

        private void HandleArtifactShown(VN.VNRunner.VNArtifactPayload payload)
        {
            if (_routine != null)
                StopCoroutine(_routine);

            HideAllCharacters();

            _routine = StartCoroutine(PlayRoutine(payload));
            Debug.Log("[VN] Artifact shown received: " + payload.artifactId);
        }

        private void HideAllCharacters()
        {
            float fade = Mathf.Max(0f, hideCharactersFadeSeconds);

            HideSlot(leftSlot, fade);
            HideSlot(centerSlot, fade);
            HideSlot(rightSlot, fade);
        }

        private void HideSlot(VNCrossfadeImageUGUI slot, float fade)
        {
            if (slot == null) return;

            if (fade <= 0f)
                slot.SetInstant(null, false);
            else
                slot.Crossfade(null, fade, false);
        }

        private IEnumerator PlayRoutine(VN.VNRunner.VNArtifactPayload payload)
        {
            if (root != null)
                root.SetActive(true);

            if (artifactImage != null)
            {
                artifactImage.sprite = payload.sprite;
                artifactImage.preserveAspect = true;
                artifactImage.SetNativeSize();
            }

            if (artifactTransform != null)
                artifactTransform.localScale = Vector3.zero;

            SetDimAlpha(0f);
            SetArtifactAlpha(0f);

            float fadeIn = Mathf.Max(0.0001f, payload.fadeInSeconds);
            float scaleUp = Mathf.Max(0.0001f, payload.scaleUpSeconds);
            float settle = Mathf.Max(0.0001f, payload.scaleSettleSeconds);
            float hold = Mathf.Max(0f, payload.holdSeconds);
            float fadeOut = Mathf.Max(0.0001f, payload.fadeOutSeconds);

            float t = 0f;
            while (t < fadeIn)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fadeIn);
                SetDimAlpha(payload.dimAlpha * k);
                SetArtifactAlpha(k);
                yield return null;
            }

            SetDimAlpha(payload.dimAlpha);
            SetArtifactAlpha(1f);

            t = 0f;
            while (t < scaleUp)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / scaleUp);
                SetArtifactScale(Vector3.LerpUnclamped(Vector3.zero, Vector3.one * 1.2f, k));
                yield return null;
            }

            SetArtifactScale(Vector3.one * 1.2f);

            t = 0f;
            while (t < settle)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / settle);
                SetArtifactScale(Vector3.LerpUnclamped(Vector3.one * 1.2f, Vector3.one, k));
                yield return null;
            }

            SetArtifactScale(Vector3.one);

            if (hold > 0f)
                yield return new WaitForSeconds(hold);

            t = 0f;
            while (t < fadeOut)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fadeOut);
                SetDimAlpha(Mathf.Lerp(payload.dimAlpha, 0f, k));
                SetArtifactAlpha(Mathf.Lerp(1f, 0f, k));
                yield return null;
            }

            SetDimAlpha(0f);
            SetArtifactAlpha(0f);

            if (root != null)
                root.SetActive(false);

            runner?.NotifyArtifactPresentationFinished();
            _routine = null;
        }

        private void SetDimAlpha(float a)
        {
            if (dimImage == null) return;
            var c = dimImage.color;
            c.a = a;
            dimImage.color = c;
        }

        private void SetArtifactAlpha(float a)
        {
            if (artifactImage == null) return;
            var c = artifactImage.color;
            c.a = a;
            artifactImage.color = c;
        }

        private void SetArtifactScale(Vector3 scale)
        {
            if (artifactTransform == null) return;
            artifactTransform.localScale = scale;
        }
    }
}