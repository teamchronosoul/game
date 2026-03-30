using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNLoadingScreenController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private Slider progressBar;
        [SerializeField] private Image progressFill;

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 0.25f;
        [SerializeField] private float minimumVisibleTime = 1.0f;
        [SerializeField] private float progressMoveSpeed = 1.0f;

        private float _shownProgress;

        private void Awake()
        {
            if (root != null)
            {
                root.alpha = 0f;
                root.interactable = false;
                root.blocksRaycasts = true;
            }

            if (progressBar != null)
            {
                progressBar.minValue = 0f;
                progressBar.maxValue = 1f;
                progressBar.value = 0f;
            }

            if (progressFill != null)
                progressFill.fillAmount = 0f;
        }

        private void Start()
        {
            StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            string targetScene = VN.VNSceneLoader.ConsumeTargetScene();

            if (string.IsNullOrWhiteSpace(targetScene))
            {
                Debug.LogError("[VN] Loading screen opened without target scene.");
                yield break;
            }

            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                Debug.LogError($"[VN] Target scene '{targetScene}' is not added to Build Settings.");
                yield break;
            }

            yield return FadeCanvas(0f, 1f, fadeInDuration);

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);

            if (loadOperation == null)
            {
                Debug.LogError($"[VN] Failed to start loading scene '{targetScene}'.");
                yield break;
            }

            loadOperation.allowSceneActivation = false;

            float visibleTimer = 0f;

            while (loadOperation.progress < 0.9f || visibleTimer < minimumVisibleTime)
            {
                visibleTimer += Time.unscaledDeltaTime;

                float targetProgress = Mathf.Clamp01(loadOperation.progress / 0.9f) * 0.9f;
                _shownProgress = Mathf.MoveTowards(_shownProgress, targetProgress, progressMoveSpeed * Time.unscaledDeltaTime);

                ApplyProgress(_shownProgress);
                yield return null;
            }

            while (_shownProgress < 1f)
            {
                _shownProgress = Mathf.MoveTowards(_shownProgress, 1f, progressMoveSpeed * 1.5f * Time.unscaledDeltaTime);
                ApplyProgress(_shownProgress);
                yield return null;
            }

            yield return null;
            loadOperation.allowSceneActivation = true;
        }

        private void ApplyProgress(float value)
        {
            value = Mathf.Clamp01(value);

            if (progressBar != null)
                progressBar.value = value;

            if (progressFill != null)
                progressFill.fillAmount = value;
        }

        private IEnumerator FadeCanvas(float from, float to, float duration)
        {
            if (root == null)
                yield break;

            root.alpha = from;

            if (duration <= 0f)
            {
                root.alpha = to;
                yield break;
            }

            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                root.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            root.alpha = to;
        }
    }
}