using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace VN.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class VNCutscenePlayerUGUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Корень катсцены. Если пусто, используется объект с этим компонентом.")]
        [SerializeField] private GameObject root;

        [Tooltip("RawImage, в который выводится видео.")]
        [SerializeField] private RawImage output;

        [Tooltip("VideoPlayer для воспроизведения. Можно повесить на этот же объект.")]
        [SerializeField] private VideoPlayer videoPlayer;

        [Tooltip("Опционально. Если задан, звук видео будет идти через этот AudioSource.")]
        [SerializeField] private AudioSource audioSource;

        [Header("Aspect")]
        [Tooltip("Автоматически выставлять AspectRatioFitter по размеру видео.")]
        [SerializeField] private bool updateAspectFromVideo = true;

        [Tooltip("EnvelopeParent = видео заполняет весь экран без полос, но может обрезаться. FitInParent = видно целиком, но могут быть полосы.")]
        [SerializeField] private AspectRatioFitter.AspectMode aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        [SerializeField] private AspectRatioFitter aspectRatioFitter;

        [Header("Render Texture")]
        [Tooltip("Создавать RenderTexture под размер клипа автоматически. Обычно лучше оставить включенным.")]
        [SerializeField] private bool createRuntimeRenderTexture = true;

        [SerializeField, Min(16)] private int fallbackTextureWidth = 1920;
        [SerializeField, Min(16)] private int fallbackTextureHeight = 1080;

        [Header("Startup")]
        [SerializeField] private bool hideOnAwake = true;
        [SerializeField] private bool prepareBeforePlay = true;

        [Header("Anti Glitch Startup")]
        [Tooltip("Скрывать RawImage до первого готового кадра. Убирает пиксельный мусор/битые блоки на устройствах, пока VideoPlayer подготавливает RenderTexture.")]
        [SerializeField] private bool hideOutputUntilFirstFrame = true;

        [Tooltip("Очищать RenderTexture перед запуском видео. Это убирает старые/неинициализированные пиксели.")]
        [SerializeField] private bool clearTextureBeforePlay = true;

        [Tooltip("Сколько ждать первый кадр перед тем, как показать уже очищенную текстуру. 0 = ждать бесконечно.")]
        [SerializeField, Min(0f)] private float firstFrameTimeoutSeconds = 2f;

        private CanvasGroup _canvasGroup;
        private RenderTexture _runtimeTexture;
        private Coroutine _routine;
        private bool _preparedPlayRequested;
        private bool _waitingForFirstFrame;
        private bool _firstFrameReady;
        private float _pendingFadeInSeconds;
        private Color _outputBaseColor = Color.white;
        private bool _hasOutputBaseColor;

        private void Awake()
        {
            if (root == null)
                root = gameObject;

            if (videoPlayer == null)
                videoPlayer = GetComponent<VideoPlayer>();

            if (output == null)
                output = GetComponentInChildren<RawImage>(true);

            if (aspectRatioFitter == null && output != null)
                aspectRatioFitter = output.GetComponent<AspectRatioFitter>();

            CaptureOutputBaseColor();

            _canvasGroup = GetComponent<CanvasGroup>();

            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.isLooping = true;
                videoPlayer.waitForFirstFrame = true;
                videoPlayer.prepareCompleted += OnPrepared;
                videoPlayer.frameReady += OnFrameReady;
                videoPlayer.errorReceived += OnVideoError;
            }

            if (hideOnAwake)
                HideImmediate();
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnPrepared;
                videoPlayer.frameReady -= OnFrameReady;
                videoPlayer.errorReceived -= OnVideoError;
                videoPlayer.sendFrameReadyEvents = false;
            }

            ReleaseRuntimeTexture();
        }

        public void Show(VNRunner.VNCutscenePayload payload)
        {
            if (payload.clip == null)
            {
                Debug.LogWarning("[VNCutscenePlayerUGUI] Can't show cutscene: clip is null.");
                HideImmediate();
                return;
            }

            if (videoPlayer == null || output == null)
            {
                Debug.LogWarning("[VNCutscenePlayerUGUI] Can't show cutscene: VideoPlayer or RawImage is not assigned.");
                return;
            }

            StopRoutine();
            CaptureOutputBaseColor();

            _preparedPlayRequested = false;
            _waitingForFirstFrame = hideOutputUntilFirstFrame;
            _firstFrameReady = false;
            _pendingFadeInSeconds = Mathf.Max(0f, payload.fadeInSeconds);

            if (root != null)
                root.SetActive(true);

            ConfigureRaycast(payload.blockInput);
            ConfigureAspect(payload.clip);
            ConfigureRenderTexture(payload.clip);
            ConfigureVideo(payload);

            if (clearTextureBeforePlay)
                ClearTargetTexture();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = payload.blockInput;
                _canvasGroup.blocksRaycasts = payload.blockInput;
            }

            SetOutputAlpha(hideOutputUntilFirstFrame ? 0f : 1f);

            if (videoPlayer != null)
                videoPlayer.sendFrameReadyEvents = hideOutputUntilFirstFrame;

            if (hideOutputUntilFirstFrame)
                _routine = StartCoroutine(RevealAfterFirstFrameRoutine(_pendingFadeInSeconds));

            if (prepareBeforePlay)
            {
                _preparedPlayRequested = true;
                videoPlayer.Prepare();
            }
            else
            {
                _preparedPlayRequested = false;
                videoPlayer.Play();

                if (!hideOutputUntilFirstFrame)
                    BeginFadeIn(_pendingFadeInSeconds);
            }
        }

        public void Hide(float fadeOutSeconds)
        {
            StopRoutine();

            if (fadeOutSeconds > 0f && _canvasGroup != null && root != null && root.activeSelf)
                _routine = StartCoroutine(FadeOutAndStop(fadeOutSeconds));
            else
                HideImmediate();
        }

        public void HideImmediate()
        {
            StopRoutine();
            _preparedPlayRequested = false;
            _waitingForFirstFrame = false;
            _firstFrameReady = false;

            if (videoPlayer != null)
            {
                videoPlayer.sendFrameReadyEvents = false;

                if (videoPlayer.isPlaying)
                    videoPlayer.Stop();

                videoPlayer.clip = null;
            }

            if (audioSource != null)
                audioSource.Stop();

            if (output != null)
            {
                output.texture = null;
                SetOutputAlpha(0f);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            ConfigureRaycast(false);
            ReleaseRuntimeTexture();

            if (root != null)
                root.SetActive(false);
        }

        private void ConfigureVideo(VNRunner.VNCutscenePayload payload)
        {
            videoPlayer.Stop();
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = payload.clip;
            videoPlayer.isLooping = true;
            videoPlayer.playbackSpeed = 1f;
            videoPlayer.waitForFirstFrame = true;

            var volume = Mathf.Clamp01(payload.audioVolume);

            if (audioSource != null)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                videoPlayer.controlledAudioTrackCount = 1;
                videoPlayer.EnableAudioTrack(0, payload.playAudio);
                videoPlayer.SetTargetAudioSource(0, audioSource);
                audioSource.volume = volume;
                audioSource.loop = false;
            }
            else
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                videoPlayer.controlledAudioTrackCount = 1;
                videoPlayer.EnableAudioTrack(0, payload.playAudio);
                videoPlayer.SetDirectAudioVolume(0, volume);
            }
        }

        private void ConfigureRenderTexture(VideoClip clip)
        {
            if (!createRuntimeRenderTexture)
            {
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                output.texture = videoPlayer.targetTexture;
                return;
            }

            ReleaseRuntimeTexture();

            var width = clip != null && clip.width > 0 ? (int)clip.width : fallbackTextureWidth;
            var height = clip != null && clip.height > 0 ? (int)clip.height : fallbackTextureHeight;

            width = Mathf.Max(16, width);
            height = Mathf.Max(16, height);

            _runtimeTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = $"VN_Cutscene_{width}x{height}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _runtimeTexture.Create();

            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = _runtimeTexture;
            output.texture = _runtimeTexture;
        }

        private void ConfigureAspect(VideoClip clip)
        {
            if (!updateAspectFromVideo || aspectRatioFitter == null || clip == null)
                return;

            var width = Mathf.Max(1f, clip.width);
            var height = Mathf.Max(1f, clip.height);

            aspectRatioFitter.aspectMode = aspectMode;
            aspectRatioFitter.aspectRatio = width / height;
        }

        private void ConfigureRaycast(bool blockInput)
        {
            if (output != null)
                output.raycastTarget = blockInput;
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (!_preparedPlayRequested || source == null)
                return;

            _preparedPlayRequested = false;
            source.Play();

            if (!hideOutputUntilFirstFrame)
                BeginFadeIn(_pendingFadeInSeconds);
        }

        private void OnFrameReady(VideoPlayer source, long frameIdx)
        {
            if (!_waitingForFirstFrame || source != videoPlayer)
                return;

            if (frameIdx >= 0)
                _firstFrameReady = true;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[VNCutscenePlayerUGUI] VideoPlayer error: {message}");
        }

        private IEnumerator RevealAfterFirstFrameRoutine(float fadeInSeconds)
        {
            var elapsed = 0f;
            var hasTimeout = firstFrameTimeoutSeconds > 0f;

            while (!_firstFrameReady)
            {
                if (IsVideoTextureReady())
                {
                    _firstFrameReady = true;
                    break;
                }

                if (hasTimeout && elapsed >= firstFrameTimeoutSeconds)
                    break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _waitingForFirstFrame = false;

            if (videoPlayer != null)
                videoPlayer.sendFrameReadyEvents = false;

            SetOutputAlpha(1f);
            yield return FadeCanvasTo(1f, fadeInSeconds);

            _routine = null;
        }

        private bool IsVideoTextureReady()
        {
            if (videoPlayer == null || videoPlayer.texture == null)
                return false;

            // На большинстве платформ frame становится 0 только после первого декодированного кадра.
            if (videoPlayer.frame >= 0)
                return true;

            // Fallback для платформ, где frame может не обновиться сразу, но время уже пошло.
            return videoPlayer.isPlaying && videoPlayer.time > 0.0;
        }

        private void BeginFadeIn(float fadeInSeconds)
        {
            StopRoutine();
            SetOutputAlpha(1f);

            if (_canvasGroup == null)
                return;

            if (fadeInSeconds > 0f)
                _routine = StartCoroutine(FadeCanvas(1f, fadeInSeconds, false));
            else
                _canvasGroup.alpha = 1f;
        }

        private IEnumerator FadeOutAndStop(float seconds)
        {
            yield return FadeCanvasTo(0f, seconds);
            HideImmediate();
        }

        private IEnumerator FadeCanvas(float targetAlpha, float seconds, bool hideAfter)
        {
            yield return FadeCanvasTo(targetAlpha, seconds);
            _routine = null;

            if (hideAfter)
                HideImmediate();
        }

        private IEnumerator FadeCanvasTo(float targetAlpha, float seconds)
        {
            if (_canvasGroup == null)
                yield break;

            if (seconds <= 0f)
            {
                _canvasGroup.alpha = targetAlpha;
                yield break;
            }

            var from = _canvasGroup.alpha;
            var duration = Mathf.Max(0.001f, seconds);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                _canvasGroup.alpha = Mathf.Lerp(from, targetAlpha, t);
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
        }

        private void StopRoutine()
        {
            if (_routine == null)
                return;

            StopCoroutine(_routine);
            _routine = null;
        }

        private void CaptureOutputBaseColor()
        {
            if (output == null || _hasOutputBaseColor)
                return;

            _outputBaseColor = output.color;
            _hasOutputBaseColor = true;
        }

        private void SetOutputAlpha(float alpha01)
        {
            if (output == null)
                return;

            if (!_hasOutputBaseColor)
                CaptureOutputBaseColor();

            var color = _hasOutputBaseColor ? _outputBaseColor : Color.white;
            color.a *= Mathf.Clamp01(alpha01);
            output.color = color;
        }

        private void ClearTargetTexture()
        {
            RenderTexture target = null;

            if (videoPlayer != null && videoPlayer.targetTexture != null)
                target = videoPlayer.targetTexture;
            else if (output != null)
                target = output.texture as RenderTexture;

            if (target == null)
                return;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;
        }

        private void ReleaseRuntimeTexture()
        {
            if (_runtimeTexture == null)
                return;

            if (videoPlayer != null && videoPlayer.targetTexture == _runtimeTexture)
                videoPlayer.targetTexture = null;

            _runtimeTexture.Release();
            Destroy(_runtimeTexture);
            _runtimeTexture = null;
        }
    }
}
