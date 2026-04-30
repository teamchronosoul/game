using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VN.UI
{
    [DisallowMultipleComponent]
    public sealed class VNTruthEyeMinigameUGUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Serializable]
        public struct Result
        {
            public bool success;
            public bool skipped;
            public int fails;
            public float playTime;
        }

        [Header("Roots")]
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("UI")]
        [SerializeField] private RectTransform playArea;
        [SerializeField] private RectTransform safeZone;
        [SerializeField] private RectTransform eye;
        [SerializeField] private Image progressCircle;
        [SerializeField] private Image ringImage;
        [SerializeField] private Image eyeGlowImage;
        [SerializeField] private Button skipButton;

        [Header("Rules")]
        [SerializeField] [Min(1f)] private float requiredHoldSeconds = 15f;
        [SerializeField] private bool allowSkipAfterFails = true;
        [SerializeField] [Min(0)] private int failsBeforeSkip = 3;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool hideOnAwake = true;

        [Header("Safe Zone")]
        [Tooltip("Если 0, радиус берётся из размера safeZone.")]
        [SerializeField] [Min(0f)] private float manualSafeZoneRadius = 0f;

        [Header("Eye Control")]
        [SerializeField] [Min(1f)] private float dragFollowSpeed = 18f;

        [Header("Magic Drift")]
        [SerializeField] [Min(0f)] private float driftStrength = 70f;
        [SerializeField] [Range(0f, 1f)] private float outwardBias = 0.55f;
        [SerializeField] [Min(0f)] private float noiseStrength = 18f;
        [SerializeField] [Min(0.01f)] private float noiseSpeed = 1.25f;
        [SerializeField] [Min(0.05f)] private float directionChangeInterval = 1.1f;

        [Header("Risk Feedback")]
        [SerializeField] [Range(0f, 1f)] private float riskDistance01 = 0.72f;
        [SerializeField] [Min(0f)] private float riskShakeAmplitude = 5f;
        [SerializeField] [Min(0f)] private float riskPulseScale = 0.045f;
        [SerializeField] [Min(0.05f)] private float riskPulseEventInterval = 0.35f;

        [Header("Fail Feedback")]
        [SerializeField] [Min(0.01f)] private float failFlashSeconds = 0.22f;
        [SerializeField] [Min(0f)] private float failCooldownSeconds = 0.18f;
        [SerializeField] [Min(0f)] private float failPulseScale = 0.12f;

        [Header("Colors")]
        [SerializeField] private Color calmRingColor = Color.white;
        [SerializeField] private Color riskRingColor = new Color(0.35f, 0.75f, 1f, 1f);
        [SerializeField] private Color failRingColor = new Color(1f, 0.12f, 0.08f, 1f);
        [SerializeField] private Color progressColor = new Color(0.1f, 0.55f, 1f, 1f);
        [SerializeField] private Color calmEyeColor = new Color(0.35f, 0.85f, 1f, 1f);
        [SerializeField] private Color riskEyeColor = new Color(0.85f, 0.95f, 1f, 1f);

        [Header("Events")]
        [SerializeField] private UnityEvent onRiskPulse = new UnityEvent();
        [SerializeField] private UnityEvent onFail = new UnityEvent();
        [SerializeField] private UnityEvent onSuccess = new UnityEvent();
        [SerializeField] private UnityEvent onSkip = new UnityEvent();

        public float DefaultHoldSeconds => requiredHoldSeconds;
        public int DefaultFailsBeforeSkip => failsBeforeSkip;
        public bool DefaultAllowSkipAfterFails => allowSkipAfterFails;
        public float DefaultDriftStrength => driftStrength;

        private Action<Result> _onFinished;

        private bool _initialized;
        private bool _playing;
        private bool _dragging;
        private bool _finishing;

        private float _runtimeHoldSeconds;
        private float _runtimeDriftStrength;
        private bool _runtimeAllowSkip;
        private int _runtimeFailsBeforeSkip;

        private float _progress01;
        private float _playTime;
        private float _failCooldownTimer;
        private float _directionTimer;
        private float _riskEventTimer;

        private int _fails;

        private Vector2 _eyePos;
        private Vector2 _dragTarget;
        private Vector2 _targetDriftDirection;
        private Vector2 _currentDriftDirection;

        private float _noiseSeedX;
        private float _noiseSeedY;

        private Vector3 _safeZoneBaseScale;
        private Vector3 _eyeBaseScale;

        private Coroutine _feedbackRoutine;

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

            if (_failCooldownTimer > 0f)
            {
                _failCooldownTimer -= dt;
                return;
            }

            UpdateDriftDirection(dt);
            UpdateEyePosition(dt);

            SetEyeAnchoredPosition(_eyePos);

            float distance01 = GetCurrentDistance01();

            if (distance01 > 1f)
            {
                Fail();
                return;
            }

            _progress01 = Mathf.Clamp01(_progress01 + dt / Mathf.Max(0.01f, _runtimeHoldSeconds));
            SetProgress(_progress01);

            ApplyNormalFeedback(distance01);

            if (_progress01 >= 1f)
                CompleteSuccess();
        }

        public void Play(Action<Result> onFinished)
        {
            Play(requiredHoldSeconds, failsBeforeSkip, allowSkipAfterFails, -1f, onFinished);
        }

        public void Play(
            float holdSeconds,
            int failsToShowSkip,
            bool allowSkip,
            float driftOverride,
            Action<Result> onFinished)
        {
            EnsureInitialized();

            _onFinished = onFinished;

            _runtimeHoldSeconds = Mathf.Max(1f, holdSeconds);
            _runtimeFailsBeforeSkip = Mathf.Max(0, failsToShowSkip);
            _runtimeAllowSkip = allowSkip;
            _runtimeDriftStrength = driftOverride > 0f ? driftOverride : driftStrength;

            _playing = true;
            _finishing = false;
            _dragging = false;

            _progress01 = 0f;
            _playTime = 0f;
            _fails = 0;
            _failCooldownTimer = 0f;
            _riskEventTimer = 0f;

            _eyePos = GetSafeZoneCenterInPlayArea();
            _dragTarget = _eyePos;

            _noiseSeedX = UnityEngine.Random.Range(0f, 1000f);
            _noiseSeedY = UnityEngine.Random.Range(0f, 1000f);

            PickNewDriftDirection(true);

            SetVisible(true);
            SetProgress(0f);
            SetEyeAnchoredPosition(_eyePos);

            if (skipButton != null)
                skipButton.gameObject.SetActive(false);

            ResetVisuals();
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

            if (skipButton != null)
                skipButton.onClick.AddListener(Skip);

            if (progressCircle != null)
            {
                progressCircle.fillAmount = 0f;
                progressCircle.color = progressColor;
            }
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
                1f - Mathf.Exp(-3f * dt)
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

        private void UpdateEyePosition(float dt)
        {
            Vector2 center = GetSafeZoneCenterInPlayArea();

            if (_dragging)
            {
                _eyePos = Vector2.Lerp(
                    _eyePos,
                    _dragTarget,
                    1f - Mathf.Exp(-dragFollowSpeed * dt)
                );
            }

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

            Vector2 magicForce =
                driftDirection * _runtimeDriftStrength +
                noise * noiseStrength;

            _eyePos += magicForce * dt;
        }

        private void Fail()
        {
            _fails++;
            _progress01 = 0f;
            SetProgress(0f);

            _dragging = false;
            _failCooldownTimer = failCooldownSeconds;

            _eyePos = GetSafeZoneCenterInPlayArea();
            _dragTarget = _eyePos;
            SetEyeAnchoredPosition(_eyePos);

            PickNewDriftDirection(true);

            if (skipButton != null && _runtimeAllowSkip && _fails >= _runtimeFailsBeforeSkip)
                skipButton.gameObject.SetActive(true);

            onFail?.Invoke();

            if (_feedbackRoutine != null)
                StopCoroutine(_feedbackRoutine);

            _feedbackRoutine = StartCoroutine(FailFlashRoutine());
        }

        private IEnumerator FailFlashRoutine()
        {
            float timer = 0f;

            while (timer < failFlashSeconds)
            {
                float t = timer / Mathf.Max(0.01f, failFlashSeconds);
                float pulse = 1f + failPulseScale * (1f - t);

                if (ringImage != null)
                    ringImage.color = Color.Lerp(failRingColor, calmRingColor, t);

                if (safeZone != null)
                    safeZone.localScale = _safeZoneBaseScale * pulse;

                if (eyeGlowImage != null)
                    eyeGlowImage.color = Color.Lerp(failRingColor, calmEyeColor, t);

                timer += DeltaTime;
                yield return null;
            }

            ResetVisuals();
            _feedbackRoutine = null;
        }

        private void CompleteSuccess()
        {
            _progress01 = 1f;
            SetProgress(1f);

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

            SetVisible(false);
            callback?.Invoke(result);
        }

        private void ApplyNormalFeedback(float distance01)
        {
            float risk01 = Mathf.InverseLerp(riskDistance01, 1f, distance01);

            if (ringImage != null)
                ringImage.color = Color.Lerp(calmRingColor, riskRingColor, risk01);

            if (eyeGlowImage != null)
                eyeGlowImage.color = Color.Lerp(calmEyeColor, riskEyeColor, risk01);

            if (safeZone != null)
            {
                float pulse = 1f;

                if (risk01 > 0f)
                {
                    float sin = Mathf.Sin(CurrentTime * 16f) * 0.5f + 0.5f;
                    pulse += riskPulseScale * risk01 * sin;
                }

                safeZone.localScale = _safeZoneBaseScale * pulse;
            }

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
            }
            else
            {
                _riskEventTimer = 0f;
            }

            SetEyeAnchoredPosition(visualPos);
        }

        private void ResetVisuals()
        {
            if (ringImage != null)
                ringImage.color = calmRingColor;

            if (eyeGlowImage != null)
                eyeGlowImage.color = calmEyeColor;

            if (safeZone != null)
                safeZone.localScale = _safeZoneBaseScale;

            if (eye != null)
                eye.localScale = _eyeBaseScale;
        }

        private void SetProgress(float value)
        {
            if (progressCircle == null)
                return;

            progressCircle.fillAmount = Mathf.Clamp01(value);
            progressCircle.color = progressColor;
        }

        private void SetEyeAnchoredPosition(Vector2 position)
        {
            if (eye != null)
                eye.anchoredPosition = position;
        }

        private float GetCurrentDistance01()
        {
            if (eye == null || safeZone == null)
                return 0f;

            Vector3 eyeWorldCenter = eye.TransformPoint(eye.rect.center);
            Vector3 eyeLocalToSafeZone = safeZone.InverseTransformPoint(eyeWorldCenter);

            float radius = GetSafeZoneRadius();
            float distance = new Vector2(eyeLocalToSafeZone.x, eyeLocalToSafeZone.y).magnitude;

            return distance / Mathf.Max(1f, radius);
        }

        private float GetSafeZoneRadius()
        {
            if (manualSafeZoneRadius > 0f)
                return manualSafeZoneRadius;

            if (safeZone == null)
                return 1f;

            return Mathf.Min(safeZone.rect.width, safeZone.rect.height) * 0.5f;
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