using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YsoCorp.GameUtils;

namespace VN.UI
{
    /// <summary>
    /// Самостоятельный UI-счетчик кристаллов.
    /// Можно повесить на любой UI-объект / prefab с TextMeshProUGUI.
    /// Скрипт сам читает баланс из VNCrystalWallet, обновляется при изменении валюты
    /// и может быть целью полета кристаллов через CoinFxManager.
    ///
    /// Дополнительно умеет временно переезжать к активному UI-anchor в игре
    /// и показывать числовое списание при оплате premium choice.
    /// </summary>
    [DisallowMultipleComponent]
    public class VNCurrencyCounterUGUI : MonoBehaviour
    {
        private static readonly List<VNCurrencyCounterUGUI> RegisteredCounters = new();

        [Header("Registration")]
        [Tooltip("Если включено, счетчик регистрируется как глобальный. Это нужно, чтобы награды и premium choices могли сами найти активный счетчик.")]
        [SerializeField] private bool registerAsGlobalCounter = true;

        [Tooltip("Если включено, этот счетчик может быть выбран целью полета кристаллов через CoinFxManager.")]
        [SerializeField] private bool useAsRewardFxTarget = true;

        [Tooltip("Если включено, этот счетчик может быть использован для показа оплаты premium choice.")]
        [SerializeField] private bool useAsPremiumPaymentCounter = true;

        [Tooltip("Если активных счетчиков несколько, FX/оплата пойдут к счетчику с самым большим приоритетом. Например HUD = 100, MainMenu = 10.")]
        [SerializeField] private int rewardFxPriority = 0;

        [Tooltip("Если включено, VNUIView может показать этот счетчик автоматически по глобальному запросу.")]
        [SerializeField] private bool allowGlobalVisibilityRequests = true;

        [Header("View")]
        [Tooltip("Визуальный root счетчика. Можно оставить пустым. Если нужно скрывать только визуал, а не весь объект со скриптом, назначь сюда дочерний объект.")]
        [SerializeField] private GameObject root;

        [Tooltip("Показывать счетчик при OnEnable.")]
        [SerializeField] private bool showOnEnable = true;

        [Tooltip("Текст с текущим количеством кристаллов.")]
        [SerializeField] private TextMeshProUGUI amountText;

        [Header("Runtime Placement")]
        [Tooltip("Если включено, при оплате premium choice счетчик будет перепривязан к переданному активному UI-anchor.")]
        [SerializeField] private bool moveToPaymentAnchor = true;

        [Tooltip("Fallback anchor для оплаты, если VNUIView не передал конкретный anchor. Можно оставить пустым.")]
        [SerializeField] private RectTransform defaultPaymentAnchor;

        [Tooltip("После перепривязки поставить счетчик последним sibling, чтобы он был поверх UI.")]
        [SerializeField] private bool setAsLastSiblingWhenMoved = true;

        [Tooltip("Сбрасывать anchoredPosition в 0 при переезде к anchor.")]
        [SerializeField] private bool snapToPaymentAnchorCenter = true;

        [Header("FX")]
        [Tooltip("Точка, куда летят кристаллы. Обычно это RectTransform иконки кристалла или всего счетчика. Если пусто, используется RectTransform этого объекта.")]
        [SerializeField] private RectTransform fxTargetCounter;

        [Tooltip("Откуда стартует полет кристаллов, если источник не передан явно. Если пусто, старт будет из центра canvas.")]
        [SerializeField] private RectTransform defaultFxSource;

        [Tooltip("Если выключить, начисление будет мгновенным без CoinFxManager.")]
        [SerializeField] private bool useCoinFxManager = true;

        [Header("Spend Feedback")]
        [Tooltip("Показывать '-цена' около счетчика при оплате premium choice.")]
        [SerializeField] private bool showSpendPopup = true;

        [Tooltip("Опциональный prefab текста списания. Если пусто, текст создается автоматически и копирует стиль amountText.")]
        [SerializeField] private TextMeshProUGUI spendPopupTextPrefab;

        [Tooltip("Куда создавать popup списания. Если пусто, popup создается внутри счетчика.")]
        [SerializeField] private RectTransform spendPopupParent;

        [SerializeField] private string spendPopupFormat = "-{0}";
        [SerializeField] private Vector2 spendPopupStartOffset = new Vector2(0f, -42f);
        [SerializeField] private float spendPopupRisePixels = 46f;
        [SerializeField, Min(0.05f)] private float spendPopupSeconds = 0.7f;
        [SerializeField] private Color spendPopupColor = Color.white;

        [Header("Pulse")]
        [SerializeField, Min(1f)] private float pulseScale = 1.08f;
        [SerializeField, Min(0.01f)] private float pulseSeconds = 0.12f;

        private Coroutine _pulseRoutine;
        private Vector3 _baseScale = Vector3.one;
        private bool _hasBaseScale;

        public RectTransform FxTarget => fxTargetCounter != null ? fxTargetCounter : transform as RectTransform;
        public int Balance => VNCrystalWallet.Balance;
        public bool IsVisible => root != null ? root.activeSelf : gameObject.activeSelf;
        public int RewardFxPriority => rewardFxPriority;

        private void Reset()
        {
            AutoAssignRefs();
        }

        private void OnValidate()
        {
            if (amountText == null || fxTargetCounter == null)
                AutoAssignRefs();
        }

        private void Awake()
        {
            AutoAssignRefs();
            CaptureBaseScale();
            Refresh();
        }

        private void OnEnable()
        {
            RegisterIfNeeded();
            VNCrystalWallet.OnChanged += OnWalletChanged;

            Refresh();

            if (showOnEnable)
                SetVisible(true);
        }

        private void OnDisable()
        {
            VNCrystalWallet.OnChanged -= OnWalletChanged;
            Unregister();
            StopPulse();
        }

        public static VNCurrencyCounterUGUI FindBestRewardFxTarget()
        {
            CleanupRegistry();

            VNCurrencyCounterUGUI best = null;

            for (int i = 0; i < RegisteredCounters.Count; i++)
            {
                var counter = RegisteredCounters[i];
                if (!IsUsableForFx(counter))
                    continue;

                if (best == null || counter.rewardFxPriority > best.rewardFxPriority)
                    best = counter;
            }

            return best;
        }

        public static VNCurrencyCounterUGUI FindBestPremiumPaymentCounter()
        {
            CleanupRegistry();

            VNCurrencyCounterUGUI best = null;

            for (int i = 0; i < RegisteredCounters.Count; i++)
            {
                var counter = RegisteredCounters[i];
                if (!IsUsableForPremiumPayment(counter))
                    continue;

                if (best == null || counter.rewardFxPriority > best.rewardFxPriority)
                    best = counter;
            }

            return best;
        }

        public static bool TryPlayAddCrystals(int amount, RectTransform source = null, Action onComplete = null)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0)
            {
                onComplete?.Invoke();
                return true;
            }

            var counter = FindBestRewardFxTarget();
            if (counter == null)
                return false;

            counter.PlayAddCrystals(amount, source, onComplete);
            return true;
        }

        public static bool TryShowPremiumSpend(int amount, RectTransform paymentAnchor = null, RectTransform spendSource = null)
        {
            amount = Mathf.Max(0, amount);

            var counter = FindBestPremiumPaymentCounter();
            if (counter == null)
                return false;

            counter.ShowPremiumSpend(amount, paymentAnchor, spendSource);
            return true;
        }

        public static bool TryShowBalanceAt(RectTransform paymentAnchor = null, bool pulse = true)
        {
            var counter = FindBestPremiumPaymentCounter();
            if (counter == null)
                return false;

            counter.ShowBalance(paymentAnchor, pulse);
            return true;
        }

        public static void RefreshRegistered()
        {
            CleanupRegistry();

            for (int i = 0; i < RegisteredCounters.Count; i++)
            {
                if (RegisteredCounters[i] != null)
                    RegisteredCounters[i].Refresh();
            }
        }

        public static void SetRegisteredVisible(bool visible)
        {
            CleanupRegistry();

            for (int i = 0; i < RegisteredCounters.Count; i++)
            {
                var counter = RegisteredCounters[i];
                if (counter != null && counter.allowGlobalVisibilityRequests)
                    counter.SetVisible(visible);
            }
        }

        public static void PulseRegistered()
        {
            CleanupRegistry();

            for (int i = 0; i < RegisteredCounters.Count; i++)
            {
                if (RegisteredCounters[i] != null)
                    RegisteredCounters[i].Pulse();
            }
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
            else
                gameObject.SetActive(visible);
        }

        public void Refresh()
        {
            if (amountText != null)
                amountText.text = VNCrystalWallet.Balance.ToString();
        }

        public void MoveToPaymentAnchor(RectTransform paymentAnchor)
        {
            if (!moveToPaymentAnchor)
                return;

            if (paymentAnchor == null)
                paymentAnchor = defaultPaymentAnchor;

            var rect = transform as RectTransform;
            if (rect == null || paymentAnchor == null)
                return;

            rect.SetParent(paymentAnchor, false);

            if (snapToPaymentAnchorCenter)
                rect.anchoredPosition = Vector2.zero;

            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            if (setAsLastSiblingWhenMoved)
                rect.SetAsLastSibling();
        }

        public void ShowBalance(RectTransform paymentAnchor = null, bool pulse = true)
        {
            MoveToPaymentAnchor(paymentAnchor);
            SetVisible(true);
            Refresh();

            if (pulse)
                Pulse();
        }

        public void ShowPremiumSpend(int amount, RectTransform paymentAnchor = null, RectTransform spendSource = null)
        {
            amount = Mathf.Max(0, amount);

            MoveToPaymentAnchor(paymentAnchor != null ? paymentAnchor : spendSource);
            SetVisible(true);
            Refresh();
            Pulse();

            if (showSpendPopup && amount > 0)
                SpawnSpendPopup(amount);
        }

        public void PlayAddCrystals(int amount, RectTransform source = null, Action onComplete = null)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0)
            {
                onComplete?.Invoke();
                return;
            }

            SetVisible(true);
            Refresh();

            var target = FxTarget;
            var fxManager = useCoinFxManager ? CoinFxManager.Instance : null;

            if (fxManager == null || target == null)
            {
                CoinFxManager.PlayCrystalCurrencySfxGlobal();
                VNCrystalWallet.Add(amount);
                Refresh();
                Pulse();
                onComplete?.Invoke();
                return;
            }

            Vector2 from = ResolveCanvasLocalPosition(source != null ? source : defaultFxSource, target);
            fxManager.Play(from, amount, global::FlyIconType.Crystal, target, () =>
            {
                Refresh();
                Pulse();
                onComplete?.Invoke();
            });
        }

        public void Pulse()
        {
            if (!isActiveAndEnabled)
                return;

            StopPulse();
            _pulseRoutine = StartCoroutine(PulseRoutine());
        }

        private void SpawnSpendPopup(int amount)
        {
            var parent = spendPopupParent != null ? spendPopupParent : transform as RectTransform;
            if (parent == null)
                return;

            TextMeshProUGUI label;
            if (spendPopupTextPrefab != null)
            {
                label = Instantiate(spendPopupTextPrefab, parent);
            }
            else
            {
                var go = new GameObject("CrystalSpendPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                label = go.GetComponent<TextMeshProUGUI>();
                label.raycastTarget = false;
                label.alignment = TextAlignmentOptions.Center;

                if (amountText != null)
                {
                    label.font = amountText.font;
                    label.fontSharedMaterial = amountText.fontSharedMaterial;
                    label.fontSize = amountText.fontSize;
                    label.fontStyle = amountText.fontStyle;
                }
            }

            label.text = string.Format(spendPopupFormat, amount);
            label.color = spendPopupColor;
            label.gameObject.SetActive(true);

            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = spendPopupStartOffset;

            var canvasGroup = label.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = label.gameObject.AddComponent<CanvasGroup>();

            StartCoroutine(SpendPopupRoutine(label, canvasGroup, rect.anchoredPosition));
        }

        private IEnumerator SpendPopupRoutine(TextMeshProUGUI label, CanvasGroup canvasGroup, Vector2 startPosition)
        {
            var duration = Mathf.Max(0.05f, spendPopupSeconds);
            var elapsed = 0f;
            var endPosition = startPosition + Vector2.up * spendPopupRisePixels;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                if (label == null)
                    yield break;

                label.rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, t);

                if (canvasGroup != null)
                    canvasGroup.alpha = 1f - t;

                yield return null;
            }

            if (label != null)
                Destroy(label.gameObject);
        }

        private void AutoAssignRefs()
        {
            if (amountText == null)
                amountText = GetComponentInChildren<TextMeshProUGUI>(true);

            if (fxTargetCounter == null)
                fxTargetCounter = transform as RectTransform;
        }

        private void RegisterIfNeeded()
        {
            if (!registerAsGlobalCounter)
                return;

            if (!RegisteredCounters.Contains(this))
                RegisteredCounters.Add(this);
        }

        private void Unregister()
        {
            RegisteredCounters.Remove(this);
        }

        private static void CleanupRegistry()
        {
            for (int i = RegisteredCounters.Count - 1; i >= 0; i--)
            {
                if (RegisteredCounters[i] == null)
                    RegisteredCounters.RemoveAt(i);
            }
        }

        private static bool IsUsableForFx(VNCurrencyCounterUGUI counter)
        {
            return counter != null
                   && counter.registerAsGlobalCounter
                   && counter.useAsRewardFxTarget
                   && counter.isActiveAndEnabled
                   && counter.gameObject.activeInHierarchy
                   && counter.FxTarget != null;
        }

        private static bool IsUsableForPremiumPayment(VNCurrencyCounterUGUI counter)
        {
            return counter != null
                   && counter.registerAsGlobalCounter
                   && counter.useAsPremiumPaymentCounter
                   && counter.isActiveAndEnabled
                   && counter.gameObject.activeInHierarchy;
        }

        private void OnWalletChanged(int balance, int delta)
        {
            Refresh();

            if (delta != 0)
                Pulse();
        }

        private Vector2 ResolveCanvasLocalPosition(RectTransform source, RectTransform target)
        {
            var canvas = target != null ? target.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            if (canvas == null)
                return Vector2.zero;

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null || source == null)
                return Vector2.zero;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, source.position),
                canvas.worldCamera,
                out var localPoint);

            return localPoint;
        }

        private IEnumerator PulseRoutine()
        {
            CaptureBaseScale();

            var targetScale = _baseScale * Mathf.Max(1f, pulseScale);
            var duration = Mathf.Max(0.01f, pulseSeconds);

            yield return ScaleOverTime(_baseScale, targetScale, duration);
            yield return ScaleOverTime(targetScale, _baseScale, duration);

            _pulseRoutine = null;
        }

        private IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            transform.localScale = to;
        }

        private void StopPulse()
        {
            if (_pulseRoutine != null)
                StopCoroutine(_pulseRoutine);

            _pulseRoutine = null;

            if (_hasBaseScale)
                transform.localScale = _baseScale;
        }

        private void CaptureBaseScale()
        {
            if (_hasBaseScale)
                return;

            _baseScale = transform.localScale;
            _hasBaseScale = true;
        }
    }
}
