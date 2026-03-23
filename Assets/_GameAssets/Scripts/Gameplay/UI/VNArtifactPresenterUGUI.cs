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

        [Header("Artifact vibration")]
        [SerializeField] private bool useVibration = true;
        [SerializeField] [Min(0f)] private float vibrationPositionAmplitude = 6f;
        [SerializeField] [Min(0f)] private float vibrationRotationAmplitude = 1.25f;
        [SerializeField] [Min(0f)] private float vibrationFrequency = 22f;
        [SerializeField] [Range(0f, 1f)] private float vibrationStrengthOnFadeIn = 0.35f;
        [SerializeField] [Range(0f, 1f)] private float vibrationStrengthOnScale = 0.6f;
        [SerializeField] [Range(0f, 1f)] private float vibrationStrengthOnHold = 1f;
        [SerializeField] [Range(0f, 1f)] private float vibrationStrengthOnFadeOut = 0.45f;

        private Coroutine _routine;

        private Vector2 _baseAnchoredPosition;
        private Quaternion _baseLocalRotation;

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
            {
                _baseAnchoredPosition = artifactTransform.anchoredPosition;
                _baseLocalRotation = artifactTransform.localRotation;
                artifactTransform.localScale = Vector3.zero;
            }
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

            ResetArtifactVibration();
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
            {
                _baseAnchoredPosition = artifactTransform.anchoredPosition;
                _baseLocalRotation = artifactTransform.localRotation;
                artifactTransform.localScale = Vector3.zero;
            }

            ResetArtifactVibration();
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
                ApplyArtifactVibration(vibrationStrengthOnFadeIn * k);

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
                ApplyArtifactVibration(vibrationStrengthOnScale);

                yield return null;
            }

            SetArtifactScale(Vector3.one * 1.2f);

            t = 0f;
            while (t < settle)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / settle);

                SetArtifactScale(Vector3.LerpUnclamped(Vector3.one * 1.2f, Vector3.one, k));
                ApplyArtifactVibration(Mathf.Lerp(vibrationStrengthOnScale, vibrationStrengthOnHold, k));

                yield return null;
            }

            SetArtifactScale(Vector3.one);

            if (hold > 0f)
            {
                t = 0f;
                while (t < hold)
                {
                    t += Time.deltaTime;
                    ApplyArtifactVibration(vibrationStrengthOnHold);
                    yield return null;
                }
            }

            t = 0f;
            while (t < fadeOut)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fadeOut);

                SetDimAlpha(Mathf.Lerp(payload.dimAlpha, 0f, k));
                SetArtifactAlpha(Mathf.Lerp(1f, 0f, k));
                ApplyArtifactVibration(Mathf.Lerp(vibrationStrengthOnFadeOut, 0f, k));

                yield return null;
            }

            SetDimAlpha(0f);
            SetArtifactAlpha(0f);
            ResetArtifactVibration();

            if (root != null)
                root.SetActive(false);

            runner?.NotifyArtifactPresentationFinished();
            _routine = null;
        }

        private void ApplyArtifactVibration(float strength)
        {
            if (!useVibration || artifactTransform == null || strength <= 0f)
            {
                ResetArtifactVibration();
                return;
            }

            float time = Time.unscaledTime * vibrationFrequency;

            float posX = (Mathf.PerlinNoise(time, 11.37f) * 2f - 1f) * vibrationPositionAmplitude * strength;
            float posY = (Mathf.PerlinNoise(19.83f, time) * 2f - 1f) * vibrationPositionAmplitude * strength;
            float rotZ = (Mathf.PerlinNoise(time, 47.12f) * 2f - 1f) * vibrationRotationAmplitude * strength;

            artifactTransform.anchoredPosition = _baseAnchoredPosition + new Vector2(posX, posY);
            artifactTransform.localRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, rotZ);
        }

        private void ResetArtifactVibration()
        {
            if (artifactTransform == null) return;

            artifactTransform.anchoredPosition = _baseAnchoredPosition;
            artifactTransform.localRotation = _baseLocalRotation;
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