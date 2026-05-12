using System.Collections;
using System.Collections.Generic;
using _GameAssets.Scripts.Gameplay.UI;
using CandyCoded.HapticFeedback;
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

        [Tooltip("Optional Spine slots. If character is rendered through Spine, these must also be hidden while the artifact is shown.")]
        [SerializeField] private VNSpineCharacterSlotUGUI leftSpineSlot;
        [SerializeField] private VNSpineCharacterSlotUGUI centerSpineSlot;
        [SerializeField] private VNSpineCharacterSlotUGUI rightSpineSlot;

        [Tooltip("Recommended mode. Characters are hidden only visually for the artifact presentation and then restored. This does not clear sprite buffers or Spine state.")]
        [SerializeField] private bool temporarilyDisableCharacterSlotObjects = true;

        [Tooltip("Extra character-related objects to hide together with the slots, if some character visuals live outside the standard slot fields.")]
        [SerializeField] private GameObject[] extraCharacterObjectsToHide;

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

        [Header("Artifact haptic")]
        [SerializeField] private bool useHaptic = true;
        [SerializeField] [Min(1)] private int hapticPulseCount = 4;
        [SerializeField] [Min(0.01f)] private float hapticPulseInterval = 0.08f;
        [SerializeField] private bool useHeavyHaptic = false;

        private Coroutine _routine;
        private Coroutine _hapticRoutine;

        private GameObject[] _hiddenCharacterObjects;
        private bool[] _hiddenCharacterObjectStates;

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

            StopHapticRoutine();
            RestoreTemporarilyHiddenCharacters();
        }

        private void HandleArtifactShown(VN.VNRunner.VNArtifactPayload payload)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            StopHapticRoutine();
            ResetArtifactVibration();
            RestoreTemporarilyHiddenCharacters();
            HideAllCharacters();

            _routine = StartCoroutine(PlayRoutine(payload));
            Debug.Log("[VN] Artifact shown received: " + payload.artifactId);
        }

        private void HideAllCharacters()
        {
            if (temporarilyDisableCharacterSlotObjects)
            {
                HideCharactersTemporarily();
                return;
            }

            float fade = Mathf.Max(0f, hideCharactersFadeSeconds);

            HideSlot(leftSlot, fade);
            HideSlot(centerSlot, fade);
            HideSlot(rightSlot, fade);

            HideSpineSlot(leftSpineSlot, fade);
            HideSpineSlot(centerSpineSlot, fade);
            HideSpineSlot(rightSpineSlot, fade);
        }

        private void HideSlot(VNCrossfadeImageUGUI slot, float fade)
        {
            if (slot == null) return;

            if (fade <= 0f)
                slot.SetInstantHidden();
            else
                slot.Crossfade((Sprite)null, fade, false);
        }

        private void HideSpineSlot(VNSpineCharacterSlotUGUI slot, float fade)
        {
            if (slot == null) return;

            if (fade <= 0f)
                slot.SetInstantHidden();
            else
                slot.Hide(fade);
        }

        private void HideCharactersTemporarily()
        {
            var targets = BuildCharacterHideTargets();

            _hiddenCharacterObjects = targets;
            _hiddenCharacterObjectStates = new bool[targets.Length];

            for (int i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null)
                    continue;

                _hiddenCharacterObjectStates[i] = target.activeSelf;
                if (target.activeSelf)
                    target.SetActive(false);
            }
        }

        private void RestoreTemporarilyHiddenCharacters()
        {
            if (_hiddenCharacterObjects == null || _hiddenCharacterObjectStates == null)
                return;

            int count = Mathf.Min(_hiddenCharacterObjects.Length, _hiddenCharacterObjectStates.Length);
            for (int i = 0; i < count; i++)
            {
                var target = _hiddenCharacterObjects[i];
                if (target != null)
                    target.SetActive(_hiddenCharacterObjectStates[i]);
            }

            _hiddenCharacterObjects = null;
            _hiddenCharacterObjectStates = null;
        }

        private GameObject[] BuildCharacterHideTargets()
        {
            var list = new List<GameObject>(12);

            AddUniqueHideTarget(list, leftSlot != null ? leftSlot.gameObject : null);
            AddUniqueHideTarget(list, centerSlot != null ? centerSlot.gameObject : null);
            AddUniqueHideTarget(list, rightSlot != null ? rightSlot.gameObject : null);

            AddUniqueHideTarget(list, leftSpineSlot != null ? leftSpineSlot.gameObject : null);
            AddUniqueHideTarget(list, centerSpineSlot != null ? centerSpineSlot.gameObject : null);
            AddUniqueHideTarget(list, rightSpineSlot != null ? rightSpineSlot.gameObject : null);

            if (extraCharacterObjectsToHide != null)
            {
                for (int i = 0; i < extraCharacterObjectsToHide.Length; i++)
                    AddUniqueHideTarget(list, extraCharacterObjectsToHide[i]);
            }

            return list.ToArray();
        }

        private static void AddUniqueHideTarget(List<GameObject> list, GameObject target)
        {
            if (target == null || list.Contains(target))
                return;

            list.Add(target);
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

            StopHapticRoutine();
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

            StartExtendedHaptic();

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
            StopHapticRoutine();

            if (root != null)
                root.SetActive(false);

            RestoreTemporarilyHiddenCharacters();

            runner?.NotifyArtifactPresentationFinished();
            _routine = null;
        }

        private void StartExtendedHaptic()
        {
            if (!useHaptic)
                return;

            StopHapticRoutine();
            _hapticRoutine = StartCoroutine(PlayExtendedHapticRoutine());
        }

        private void StopHapticRoutine()
        {
            if (_hapticRoutine != null)
            {
                StopCoroutine(_hapticRoutine);
                _hapticRoutine = null;
            }
        }

        private IEnumerator PlayExtendedHapticRoutine()
        {
            int count = Mathf.Max(1, hapticPulseCount);
            float interval = Mathf.Max(0.01f, hapticPulseInterval);

            for (int i = 0; i < count; i++)
            {
                TriggerHapticPulse();

                if (i < count - 1)
                    yield return new WaitForSecondsRealtime(interval);
            }

            _hapticRoutine = null;
        }

        private void TriggerHapticPulse()
        {
            if (!VNSettingsWindowUGUI.VibrationEnabled)
                return;
            
            if (!useHaptic)
                return;

#if UNITY_IOS || UNITY_ANDROID
            if (useHeavyHaptic)
                HapticFeedback.HeavyFeedback();
            else
                HapticFeedback.LightFeedback();
#endif
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