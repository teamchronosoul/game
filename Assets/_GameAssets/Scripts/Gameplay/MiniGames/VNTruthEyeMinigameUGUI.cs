using System;
using System.Collections;
using _GameAssets.Scripts.Gameplay.UI;
using CandyCoded.HapticFeedback;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YP;

namespace VN.UI
{
    [DisallowMultipleComponent]
    public sealed class VNTruthEyeMinigameUGUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public enum VisualState
        {
            Normal,
            Risk,
            Fail
        }

        [Serializable]
        public struct Result
        {
            public bool success;
            public bool skipped;
            public int fails;
            public float playTime;

            public bool failed => !success && !skipped;
        }

        [Serializable]
        public sealed class SpriteStateTarget
        {
            public Image image;

            [Header("Sprites")]
            public Sprite normalSprite;
            public Sprite riskSprite;
            public Sprite failSprite;

            [Tooltip("Если включено, после смены спрайта будет вызван SetNativeSize.")]
            public bool setNativeSize;

            public void Apply(VisualState state)
            {
                if (image == null)
                    return;

                Sprite target = state switch
                {
                    VisualState.Normal => normalSprite,
                    VisualState.Risk => riskSprite != null ? riskSprite : normalSprite,
                    VisualState.Fail => failSprite != null ? failSprite : riskSprite != null ? riskSprite : normalSprite,
                    _ => normalSprite
                };

                if (target == null)
                    return;

                image.sprite = target;

                if (setNativeSize)
                    image.SetNativeSize();
            }
        }

        [Header("Roots")]
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("UI")]
        [SerializeField] private RectTransform playArea;
        [SerializeField] private RectTransform safeZone;
        [SerializeField] private RectTransform eye;
        [SerializeField] private Image progressCircle;
        [SerializeField] private Button skipButton;

        [Header("Sprite States")]
        [Tooltip("Сюда добавь все Image, которым нужно менять спрайт: кольцо, глаз, фон, прогресс и т.д.")]
        [SerializeField] private SpriteStateTarget[] spriteStateTargets;

        [Header("Rules")]
        [SerializeField] [Min(1f)] private float requiredHoldSeconds = 15f;

        [Tooltip("0 = кнопка Skip доступна сразу.")]
        [SerializeField] [Min(0)] private int failsBeforeSkip = 0;

        [SerializeField] private bool allowSkipAfterFails = true;

        [Tooltip("Если true, первый проигрыш завершает мини-игру как failed.")]
        [SerializeField] private bool finishOnFail = true;

        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool hideOnAwake = true;

        [Header("Safe Zone")]
        [Tooltip("Если 0, радиус берётся из размера safeZone.")]
        [SerializeField] [Min(0f)] private float manualSafeZoneRadius = 0f;

        [Header("Player Counter Force")]
        [Tooltip("Сила, с которой палец тянет глаз к точке касания. Это НЕ телепорт, а компенсирующая сила.")]
        [SerializeField] [Min(0f)] private float playerPullStrength = 7.5f;

        [Tooltip("Максимальная скорость компенсации игрока. Если ниже магической тяги, глаз будет прорываться сильнее.")]
        [SerializeField] [Min(0f)] private float maxPlayerPullSpeed = 230f;

        [Tooltip("Максимальная дистанция от глаза до пальца, которая учитывается для управления.")]
        [SerializeField] [Min(1f)] private float maxPointerControlDistance = 380f;

        [Tooltip("Процент магической тяги, который невозможно полностью отменить даже движением в противоположную сторону.")]
        [SerializeField] [Range(0f, 0.8f)] private float unavoidableDriftPercent = 0.25f;

        [Header("Magic Drift")]
        [Tooltip("Основная сила, которая тянет глаз. Чем выше, тем сложнее.")]
        [SerializeField] [Min(0f)] private float driftStrength = 125f;

        [Tooltip("Насколько сильно глаз тянет именно наружу. Ниже = больше хаотичного движения в разные стороны.")]
        [SerializeField] [Range(0f, 1f)] private float outwardBias = 0.25f;

        [Tooltip("Сила постоянного магического шума.")]
        [SerializeField] [Min(0f)] private float noiseStrength = 45f;

        [SerializeField] [Min(0.01f)] private float noiseSpeed = 2.2f;

        [Tooltip("Как часто меняется основное направление тяги.")]
        [SerializeField] [Min(0.05f)] private float directionChangeInterval = 0.45f;

        [Header("Random Pull Bursts")]
        [Tooltip("Сила коротких рывков в случайные стороны.")]
        [SerializeField] [Min(0f)] private float randomPullBurstStrength = 95f;

        [SerializeField] [Min(0.05f)] private float randomPullBurstMinInterval = 0.35f;
        [SerializeField] [Min(0.05f)] private float randomPullBurstMaxInterval = 0.9f;

        [Tooltip("Как быстро затухают рывки.")]
        [SerializeField] [Min(0.1f)] private float randomPullDamping = 4.5f;

        [Header("Risk Feedback")]
        [Tooltip("С какой дистанции от центра начинается состояние риска.")]
        [SerializeField] [Range(0f, 1f)] private float riskDistance01 = 0.72f;

        [SerializeField] [Min(0f)] private float riskShakeAmplitude = 6f;
        [SerializeField] [Min(0f)] private float riskPulseScale = 0.045f;
        [SerializeField] [Min(0.05f)] private float riskPulseEventInterval = 0.35f;

        [Header("Progress Drain")]
        [Tooltip("С какой дистанции от центра прогресс начинает уменьшаться. 1 = только у самой границы.")]
        [SerializeField] [Range(0f, 1f)] private float progressDrainDistance01 = 0.86f;

        [Tooltip("Если включено, скорость сброса равна скорости заполнения.")]
        [SerializeField] private bool drainProgressAtFillSpeed = true;

        [Tooltip("Используется только если Drain Progress At Fill Speed выключен.")]
        [SerializeField] [Min(0.01f)] private float customProgressDrainSeconds = 15f;

        [Header("Haptic Feedback")]
        [SerializeField] private bool useRiskHaptic = true;

        [Tooltip("С какой близости к границе начинается вибрация. Обычно равно Risk Distance или чуть выше.")]
        [SerializeField] [Range(0f, 1f)] private float hapticDistance01 = 0.78f;

        [Tooltip("Минимальная пауза между вибрациями, чтобы телефон не вибрировал постоянно.")]
        [SerializeField] [Min(0.05f)] private float hapticCooldownSeconds = 0.35f;

        [SerializeField] private bool useFailHaptic = true;

        [Header("Sound Feedback")]
        [Tooltip("Звук, когда глаз входит в состояние риска / граница краснеет.")]
        [SerializeField] private string riskSfxKey;

        [Tooltip("Минимальная пауза между звуками риска.")]
        [SerializeField] [Min(0.05f)] private float riskSfxCooldownSeconds = 0.45f;

        [Tooltip("Звук победы.")]
        [SerializeField] private string successSfxKey;

        [Header("Fail Feedback")]
        [SerializeField] [Min(0.01f)] private float failFeedbackSeconds = 0.35f;
        [SerializeField] [Min(0f)] private float failCooldownSeconds = 0.18f;
        [SerializeField] [Min(0f)] private float failPulseScale = 0.12f;

        [Header("Events")]
        [SerializeField] private UnityEvent onRiskPulse = new UnityEvent();
        [SerializeField] private UnityEvent onFail = new UnityEvent();
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onSkip = new UnityEvent();

        public float DefaultHoldSeconds => requiredHoldSeconds;
        public int DefaultFailsBeforeSkip => failsBeforeSkip;
        public bool DefaultAllowSkipAfterFails => allowSkipAfterFails;
        public float DefaultDriftStrength => driftStrength;
        public bool DefaultFinishOnFail => finishOnFail;

        private Action<Result> _onFinished;

        private bool _initialized;
        private bool _playing;
        private bool _dragging;
        private bool _finishing;

        private float _runtimeHoldSeconds;
        private float _runtimeDriftStrength;
        private bool _runtimeAllowSkip;
        private int _runtimeFailsBeforeSkip;
        private bool _runtimeFinishOnFail;

        private float _progress01;
        private float _playTime;
        private float _failCooldownTimer;
        private float _directionTimer;
        private float _riskEventTimer;
        private float _randomPullTimer;
        private float _hapticCooldownTimer;
        private float _riskSfxCooldownTimer;

        private int _fails;

        private Vector2 _eyePos;
        private Vector2 _dragTarget;
        private Vector2 _targetDriftDirection;
        private Vector2 _currentDriftDirection;
        private Vector2 _burstVelocity;

        private float _noiseSeedX;
        private float _noiseSeedY;

        private Vector3 _safeZoneBaseScale;
        private Vector3 _eyeBaseScale;
        private Vector3 _progressCircleBaseScale;

        private Coroutine _feedbackRoutine;

        private VisualState _visualState;
        private bool _visualStateInitialized;

        private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        private float CurrentTime => useUnscaledTime ? Time.unscaledTime : Time.time;

        private void Awake()
        {
            EnsureInitialized();

            if (hideOnAwake)
                SetVisible(false);
        }

        private void OnDestroy()
        {
            if (skipButton != null)
                skipButton.onClick.RemoveListener(Skip);
        }

        private void Update()
        {
            if (!_playing || _finishing)
                return;

            float dt = DeltaTime;
            _playTime += dt;

            if (_hapticCooldownTimer > 0f)
                _hapticCooldownTimer -= dt;

            if (_riskSfxCooldownTimer > 0f)
                _riskSfxCooldownTimer -= dt;

            if (_failCooldownTimer > 0f)
            {
                _failCooldownTimer -= dt;
                return;
            }

            UpdateDriftDirection(dt);
            UpdateRandomPullBurst(dt);
            UpdateEyePosition(dt);

            SetEyeAnchoredPosition(_eyePos);

            float distance01 = GetCurrentDistance01();

            if (UpdateProgressByDistance(distance01, dt))
                return;

            SetProgress(_progress01);
            ApplyLiveFeedback(distance01);

            if (_progress01 >= 1f)
                CompleteSuccess();
        }

        public void Play(Action<Result> onFinished)
        {
            Play(
                requiredHoldSeconds,
                failsBeforeSkip,
                allowSkipAfterFails,
                -1f,
                finishOnFail,
                onFinished
            );
        }

        public void Play(
            float holdSeconds,
            int failsToShowSkip,
            bool allowSkip,
            float driftOverride,
            Action<Result> onFinished)
        {
            Play(
                holdSeconds,
                failsToShowSkip,
                allowSkip,
                driftOverride,
                finishOnFail,
                onFinished
            );
        }

        public void Play(
            float holdSeconds,
            int failsToShowSkip,
            bool allowSkip,
            float driftOverride,
            bool finishOnFailValue,
            Action<Result> onFinished)
        {
            EnsureInitialized();

            _onFinished = onFinished;

            _runtimeHoldSeconds = Mathf.Max(1f, holdSeconds);
            _runtimeFailsBeforeSkip = Mathf.Max(0, failsToShowSkip);
            _runtimeAllowSkip = allowSkip;
            _runtimeDriftStrength = driftOverride > 0f ? driftOverride : driftStrength;
            _runtimeFinishOnFail = finishOnFailValue;

            _playing = true;
            _finishing = false;
            _dragging = false;

            _progress01 = 0f;
            _playTime = 0f;
            _fails = 0;
            _failCooldownTimer = 0f;
            _riskEventTimer = 0f;
            _randomPullTimer = 0f;
            _hapticCooldownTimer = 0f;
            _riskSfxCooldownTimer = 0f;
            _burstVelocity = Vector2.zero;

            _eyePos = GetSafeZoneCenterInPlayArea();
            _dragTarget = _eyePos;

            _noiseSeedX = UnityEngine.Random.Range(0f, 1000f);
            _noiseSeedY = UnityEngine.Random.Range(0f, 1000f);

            PickNewDriftDirection(true);
            ResetRandomPullTimer();

            SetVisible(true);
            SetProgress(0f);
            SetEyeAnchoredPosition(_eyePos);
            ApplyVisualState(VisualState.Normal, true);
            RefreshSkipButton();
            ResetTransforms();
        }

        public void Skip()
        {
            if (!_playing)
                return;

            if (!_runtimeAllowSkip)
                return;

            if (_fails < _runtimeFailsBeforeSkip)
                return;

            onSkip?.Invoke();
            Finish(false, true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_playing || _finishing || _failCooldownTimer > 0f)
                return;

            _dragging = true;
            UpdateDragTarget(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_playing || !_dragging || _finishing || _failCooldownTimer > 0f)
                return;

            UpdateDragTarget(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _dragging = false;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (root == null)
                root = gameObject;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (playArea == null)
                playArea = transform as RectTransform;

            if (safeZone != null)
                _safeZoneBaseScale = safeZone.localScale;

            if (eye != null)
                _eyeBaseScale = eye.localScale;

            if (progressCircle != null)
            {
                progressCircle.fillAmount = 0f;
                _progressCircleBaseScale = progressCircle.rectTransform.localScale;
            }

            if (skipButton != null)
                skipButton.onClick.AddListener(Skip);
        }

        private void SetVisible(bool visible)
        {
            if (root != null)
                root.SetActive(visible);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }

        private void RefreshSkipButton()
        {
            if (skipButton == null)
                return;

            bool visible = _runtimeAllowSkip && _fails >= _runtimeFailsBeforeSkip;
            skipButton.gameObject.SetActive(visible);
        }

        private void UpdateDragTarget(PointerEventData eventData)
        {
            if (playArea == null)
                return;

            Camera eventCamera = eventData.pressEventCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    playArea,
                    eventData.position,
                    eventCamera,
                    out Vector2 localPoint))
            {
                _dragTarget = localPoint;
            }
        }

        private void UpdateDriftDirection(float dt)
        {
            _directionTimer -= dt;

            if (_directionTimer <= 0f)
                PickNewDriftDirection(false);

            _currentDriftDirection = Vector2.Lerp(
                _currentDriftDirection,
                _targetDriftDirection,
                1f - Mathf.Exp(-4.5f * dt)
            );

            if (_currentDriftDirection.sqrMagnitude < 0.001f)
                _currentDriftDirection = Vector2.right;

            _currentDriftDirection.Normalize();
        }

        private void PickNewDriftDirection(bool instant)
        {
            _directionTimer = directionChangeInterval * UnityEngine.Random.Range(0.75f, 1.35f);

            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle;

            if (randomDirection.sqrMagnitude < 0.001f)
                randomDirection = Vector2.right;

            randomDirection.Normalize();

            _targetDriftDirection = randomDirection;

            if (instant)
                _currentDriftDirection = _targetDriftDirection;
        }

        private void UpdateRandomPullBurst(float dt)
        {
            _randomPullTimer -= dt;

            if (_randomPullTimer <= 0f)
            {
                Vector2 dir = UnityEngine.Random.insideUnitCircle;

                if (dir.sqrMagnitude < 0.001f)
                    dir = Vector2.right;

                dir.Normalize();

                _burstVelocity += dir * randomPullBurstStrength;
                ResetRandomPullTimer();
            }

            _burstVelocity = Vector2.Lerp(
                _burstVelocity,
                Vector2.zero,
                1f - Mathf.Exp(-randomPullDamping * dt)
            );
        }

        private void ResetRandomPullTimer()
        {
            float min = Mathf.Max(0.05f, randomPullBurstMinInterval);
            float max = Mathf.Max(min, randomPullBurstMaxInterval);

            _randomPullTimer = UnityEngine.Random.Range(min, max);
        }

        private void UpdateEyePosition(float dt)
        {
            Vector2 center = GetSafeZoneCenterInPlayArea();

            Vector2 fromCenter = _eyePos - center;
            Vector2 outward = fromCenter.sqrMagnitude > 0.001f
                ? fromCenter.normalized
                : _currentDriftDirection;

            Vector2 driftDirection = Vector2.Lerp(
                _currentDriftDirection,
                outward,
                outwardBias
            );

            if (driftDirection.sqrMagnitude < 0.001f)
                driftDirection = Vector2.right;

            driftDirection.Normalize();

            float noiseTime = CurrentTime * noiseSpeed;

            Vector2 noise = new Vector2(
                Mathf.PerlinNoise(_noiseSeedX, noiseTime) - 0.5f,
                Mathf.PerlinNoise(_noiseSeedY, noiseTime) - 0.5f
            ) * 2f;

            Vector2 magicVelocity =
                driftDirection * _runtimeDriftStrength +
                noise * noiseStrength +
                _burstVelocity;

            Vector2 playerVelocity = Vector2.zero;

            if (_dragging)
            {
                Vector2 toPointer = _dragTarget - _eyePos;

                if (toPointer.magnitude > maxPointerControlDistance)
                    toPointer = toPointer.normalized * maxPointerControlDistance;

                playerVelocity = toPointer * playerPullStrength;

                if (playerVelocity.magnitude > maxPlayerPullSpeed)
                    playerVelocity = playerVelocity.normalized * maxPlayerPullSpeed;
            }

            Vector2 totalVelocity = playerVelocity + magicVelocity;

            if (magicVelocity.sqrMagnitude > 0.001f && unavoidableDriftPercent > 0f)
            {
                Vector2 magicDir = magicVelocity.normalized;
                float magicSpeed = magicVelocity.magnitude;

                float minUnavoidableSpeed = magicSpeed * unavoidableDriftPercent;
                float currentSpeedAlongMagic = Vector2.Dot(totalVelocity, magicDir);

                if (currentSpeedAlongMagic < minUnavoidableSpeed)
                {
                    float missingSpeed = minUnavoidableSpeed - currentSpeedAlongMagic;
                    totalVelocity += magicDir * missingSpeed;
                }
            }

            _eyePos += totalVelocity * dt;
        }

        private bool UpdateProgressByDistance(float distance01, float dt)
        {
            float fillSpeed = 1f / Mathf.Max(0.01f, _runtimeHoldSeconds);

            bool shouldDrain = distance01 >= progressDrainDistance01;

            if (shouldDrain)
            {
                float drainSeconds = drainProgressAtFillSpeed
                    ? _runtimeHoldSeconds
                    : customProgressDrainSeconds;

                float drainSpeed = 1f / Mathf.Max(0.01f, drainSeconds);

                _progress01 = Mathf.Clamp01(_progress01 - drainSpeed * dt);
                SetProgress(_progress01);

                if (_progress01 <= 0f)
                {
                    Fail();
                    return true;
                }

                return false;
            }

            _progress01 = Mathf.Clamp01(_progress01 + fillSpeed * dt);
            return false;
        }

        private void Fail()
        {
            if (_feedbackRoutine != null)
                StopCoroutine(_feedbackRoutine);

            _fails++;
            _progress01 = 0f;
            SetProgress(0f);

            _dragging = false;
            _playing = false;

            RefreshSkipButton();
            onFail?.Invoke();

            if (useFailHaptic)
                TriggerHapticPulse();

            _feedbackRoutine = StartCoroutine(FailRoutine());
        }

        private IEnumerator FailRoutine()
        {
            ApplyVisualState(VisualState.Fail, true);

            float timer = 0f;

            while (timer < failFeedbackSeconds)
            {
                float t = timer / Mathf.Max(0.01f, failFeedbackSeconds);
                float pulse = 1f + failPulseScale * (1f - t);

                if (safeZone != null)
                    safeZone.localScale = _safeZoneBaseScale * pulse;

                if (progressCircle != null)
                    progressCircle.rectTransform.localScale = _progressCircleBaseScale * pulse;

                timer += DeltaTime;
                yield return null;
            }

            ResetTransforms();

            if (_runtimeFinishOnFail)
            {
                Finish(false, false);
                yield break;
            }

            _eyePos = GetSafeZoneCenterInPlayArea();
            _dragTarget = _eyePos;
            SetEyeAnchoredPosition(_eyePos);

            _failCooldownTimer = failCooldownSeconds;
            _hapticCooldownTimer = 0f;
            _riskSfxCooldownTimer = 0f;
            _burstVelocity = Vector2.zero;

            PickNewDriftDirection(true);
            ResetRandomPullTimer();

            ApplyVisualState(VisualState.Normal, true);

            _playing = true;
            _feedbackRoutine = null;
        }

        private void CompleteSuccess()
        {
            _progress01 = 1f;
            SetProgress(1f);

            PlaySfx(successSfxKey);
            onSuccess?.Invoke();

            Finish(true, false);
        }

        private void Finish(bool success, bool skipped)
        {
            if (_finishing)
                return;

            _finishing = true;
            _playing = false;
            _dragging = false;

            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                _feedbackRoutine = null;
            }

            Result result = new Result
            {
                success = success,
                skipped = skipped,
                fails = _fails,
                playTime = _playTime
            };

            Action<Result> callback = _onFinished;
            _onFinished = null;

            ResetTransforms();
            SetVisible(false);

            callback?.Invoke(result);
        }

        private void ApplyLiveFeedback(float distance01)
        {
            bool risk = distance01 >= riskDistance01;
            ApplyVisualState(risk ? VisualState.Risk : VisualState.Normal);

            float risk01 = Mathf.InverseLerp(riskDistance01, 1f, distance01);

            float pulse = 1f;

            if (risk01 > 0f)
            {
                float sin = Mathf.Sin(CurrentTime * 16f) * 0.5f + 0.5f;
                pulse += riskPulseScale * risk01 * sin;
            }

            if (safeZone != null)
                safeZone.localScale = _safeZoneBaseScale * pulse;

            if (progressCircle != null)
                progressCircle.rectTransform.localScale = _progressCircleBaseScale * pulse;

            Vector2 visualPos = _eyePos;

            if (risk01 > 0f)
            {
                visualPos += UnityEngine.Random.insideUnitCircle * riskShakeAmplitude * risk01;

                _riskEventTimer -= DeltaTime;

                if (_riskEventTimer <= 0f)
                {
                    _riskEventTimer = riskPulseEventInterval;
                    onRiskPulse?.Invoke();
                }

                TryTriggerRiskHaptic(distance01);
            }
            else
            {
                _riskEventTimer = 0f;
            }

            SetEyeAnchoredPosition(visualPos);
        }

        private void TryTriggerRiskHaptic(float distance01)
        {
            if (!useRiskHaptic)
                return;

            if (distance01 < hapticDistance01)
                return;

            if (_hapticCooldownTimer > 0f)
                return;

            _hapticCooldownTimer = hapticCooldownSeconds;
            TriggerHapticPulse();
        }

        private void TriggerHapticPulse()
        {
            if (!VNSettingsWindowUGUI.VibrationEnabled)
                return;

            HapticFeedback.HeavyFeedback();
        }

        private void ApplyVisualState(VisualState state, bool force = false)
        {
            if (!force && _visualStateInitialized && _visualState == state)
                return;

            bool wasInitialized = _visualStateInitialized;
            VisualState previousState = _visualState;

            _visualState = state;
            _visualStateInitialized = true;

            if (spriteStateTargets != null)
            {
                for (int i = 0; i < spriteStateTargets.Length; i++)
                    spriteStateTargets[i]?.Apply(state);
            }

            bool enteredRisk =
                state == VisualState.Risk &&
                (!wasInitialized || previousState != VisualState.Risk);

            if (enteredRisk && !force)
                TryPlayRiskSfx();
        }

        private void TryPlayRiskSfx()
        {
            if (_riskSfxCooldownTimer > 0f)
                return;

            _riskSfxCooldownTimer = riskSfxCooldownSeconds;
            PlaySfx(riskSfxKey);
        }

        private static void PlaySfx(string sfxKey)
        {
            if (string.IsNullOrWhiteSpace(sfxKey))
                return;

            Sound.PlaySFX(sfxKey);
        }

        private void ResetTransforms()
        {
            if (safeZone != null)
                safeZone.localScale = _safeZoneBaseScale;

            if (progressCircle != null)
                progressCircle.rectTransform.localScale = _progressCircleBaseScale;

            if (eye != null)
                eye.localScale = _eyeBaseScale;
        }

        private void SetProgress(float value)
        {
            if (progressCircle == null)
                return;

            progressCircle.fillAmount = Mathf.Clamp01(value);
        }

        private void SetEyeAnchoredPosition(Vector2 position)
        {
            if (eye != null)
                eye.anchoredPosition = position;
        }

        private float GetCurrentDistance01()
        {
            Vector2 center = GetSafeZoneCenterInPlayArea();
            float radius = GetSafeZoneRadiusInPlayArea();
            float distance = (_eyePos - center).magnitude;

            return distance / Mathf.Max(1f, radius);
        }

        private float GetSafeZoneRadiusInPlayArea()
        {
            if (manualSafeZoneRadius > 0f)
                return manualSafeZoneRadius;

            if (safeZone == null || playArea == null)
                return 1f;

            Vector3 centerWorld = safeZone.TransformPoint(safeZone.rect.center);

            Vector3 rightWorld = safeZone.TransformPoint(
                safeZone.rect.center + Vector2.right * safeZone.rect.width * 0.5f
            );

            Vector3 topWorld = safeZone.TransformPoint(
                safeZone.rect.center + Vector2.up * safeZone.rect.height * 0.5f
            );

            Vector2 centerLocal = playArea.InverseTransformPoint(centerWorld);
            Vector2 rightLocal = playArea.InverseTransformPoint(rightWorld);
            Vector2 topLocal = playArea.InverseTransformPoint(topWorld);

            float radiusX = Vector2.Distance(centerLocal, rightLocal);
            float radiusY = Vector2.Distance(centerLocal, topLocal);

            return Mathf.Min(radiusX, radiusY);
        }

        private Vector2 GetSafeZoneCenterInPlayArea()
        {
            if (safeZone == null || playArea == null)
                return Vector2.zero;

            Vector3 worldCenter = safeZone.TransformPoint(safeZone.rect.center);
            Vector3 localCenter = playArea.InverseTransformPoint(worldCenter);

            return new Vector2(localCenter.x, localCenter.y);
        }
    }
}