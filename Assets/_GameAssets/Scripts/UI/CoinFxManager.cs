using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using VN;
using Random = UnityEngine.Random;

namespace YsoCorp.GameUtils
{
    /// <summary>
    /// FX-manager only for crystal currency.
    /// Old project currencies (Coin / Dollar / Ticket / Tape / Scotch) are intentionally removed.
    /// Currency is added only through VNCrystalWallet.
    /// Sound is played through the current project SOUND system: Sound.PlaySFX(key).
    /// </summary>
    public class CoinFxManager : MonoBehaviour
    {
        private const string FallbackCrystalCurrencySfxKey = "crystal";

        [Header("Scene refs")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform defaultTargetCounter;
        [SerializeField] private Image crystalPrefab;

        [Header("Animation")]
        [SerializeField] private float flyTime = 0.6f;
        [SerializeField] private float overshoot = 1.2f;
        [SerializeField] private float spawnRadiusPx = 60f;
        [SerializeField] private float delayBetween = 0.05f;

        [Header("Sound / SOUND system")]
        [Tooltip("Проигрывать звук начисления/траты кристаллов через Sound.PlaySFX(key).")]
        [SerializeField] private bool playCrystalCurrencySfx = true;

        [Tooltip("Ключ SFX в текущей системе Sound. Этот же звук используется при начислении валюты и при успешном нажатии на платный выбор.")]
        [SerializeField] private string crystalCurrencySfxKey = FallbackCrystalCurrencySfxKey;

        [Header("Settings")]
        [SerializeField] private int prewarm = 20;

        private readonly Queue<Image> _pool = new();

        public static CoinFxManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                Debug.LogWarning("[CoinFxManager] Canvas is not assigned. Crystals will be added instantly without fly animation.");
                return;
            }

            for (int i = 0; i < Mathf.Max(0, prewarm); i++)
                _pool.Enqueue(Create());
        }

        /// <summary>
        /// Plays the configured crystal currency sound through the current project Sound system.
        /// Use this for premium choices too, so reward and spend feedback stay consistent.
        /// </summary>
        public static void PlayCrystalCurrencySfxGlobal()
        {
            if (Instance != null)
            {
                Instance.PlayCrystalCurrencySfx();
                return;
            }

            PlaySfxByKey(FallbackCrystalCurrencySfxKey);
        }

        public void PlayCrystalCurrencySfx()
        {
            if (!playCrystalCurrencySfx)
                return;

            PlaySfxByKey(crystalCurrencySfxKey);
        }

        /// <summary>
        /// Backward-compatible entry point for old calls with FlyIconType.
        /// Only FlyIconType.Crystal is supported now.
        /// </summary>
        public void Play(Vector3 worldPos, int amount, FlyIconType type, RectTransform targetCounter = null, Action onComplete = null)
        {
            PlayCrystals(worldPos, amount, targetCounter, onComplete);
        }

        public void PlayCrystals(Vector3 worldPos, int amount, RectTransform targetCounter = null, Action onComplete = null)
        {
            amount = Mathf.Max(0, amount);

            if (amount > 0)
                PlayCrystalCurrencySfx();

            if (targetCounter == null)
                targetCounter = defaultTargetCounter;

            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();

            if (targetCounter == null || canvas == null)
            {
                AddCrystalsInstant(amount);
                onComplete?.Invoke();
                return;
            }

            var targetFxRect = Instantiate(targetCounter, canvas.transform, true);
            targetFxRect.name = targetCounter.name + "_CrystalFX";
            targetFxRect.SetAsLastSibling();

            Sequence targetSeq = DOTween.Sequence()
                .SetUpdate(true)
                .Append(targetFxRect.DOScale(1.15f, 0.25f).From(1f).SetEase(Ease.OutBack));

            void FinishTarget()
            {
                targetSeq?.Kill();
                targetSeq = DOTween.Sequence()
                    .SetUpdate(true)
                    .Append(targetFxRect.DOScale(1f, 0.25f).SetEase(Ease.InBack))
                    .OnComplete(() =>
                    {
                        if (targetFxRect != null)
                            Destroy(targetFxRect.gameObject);

                        onComplete?.Invoke();
                    });
            }

            if (amount <= 0)
            {
                FinishTarget();
                return;
            }

            var targetDuration = 1f;
            var safeDelay = Mathf.Max(0.01f, delayBetween);
            var estimatedObjects = Mathf.Max(1, Mathf.FloorToInt((targetDuration - flyTime) / safeDelay));
            var valuePerObject = Mathf.Max(1, amount / estimatedObjects);
            var objectsToSpawn = Mathf.Max(1, amount / valuePerObject);
            var remainder = amount % valuePerObject;

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                AddCrystalsInstant(amount);
                FinishTarget();
                return;
            }

            Vector2 from = worldPos;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, targetCounter.position),
                canvas.worldCamera,
                out var to);

            var completed = 0;

            for (var i = 0; i < objectsToSpawn; i++)
            {
                var icon = Get();
                icon.transform.SetAsLastSibling();
                icon.rectTransform.anchoredPosition = from + Random.insideUnitCircle * spawnRadiusPx;
                icon.gameObject.SetActive(true);

                var delay = i * safeDelay;
                var addValue = valuePerObject;

                icon.rectTransform.DOScale(1f, flyTime * 0.4f)
                    .From(0f)
                    .SetUpdate(true)
                    .SetDelay(delay)
                    .SetEase(Ease.OutBack, overshoot);

                icon.rectTransform.DORotate(new Vector3(0f, 0f, 360f), flyTime, RotateMode.FastBeyond360)
                    .SetUpdate(true)
                    .SetDelay(delay)
                    .SetEase(Ease.Linear);

                icon.rectTransform.DOAnchorPos(to, flyTime)
                    .SetUpdate(true)
                    .SetDelay(delay)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        icon.gameObject.SetActive(false);
                        ReturnToPool(icon);

                        AddCrystalsInstant(addValue);

                        completed++;
                        if (completed != objectsToSpawn)
                            return;

                        if (remainder > 0)
                            AddCrystalsInstant(remainder);

                        FinishTarget();
                    });
            }
        }

        private static void AddCrystalsInstant(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0)
                return;

            VNCrystalWallet.Add(amount);
        }

        private static void PlaySfxByKey(string sfxKey)
        {
            if (string.IsNullOrWhiteSpace(sfxKey))
                return;

            Sound.PlaySFX(sfxKey);
        }

        private Image Get()
        {
            if (_pool.Count > 0)
                return _pool.Dequeue();

            return Create();
        }

        private void ReturnToPool(Image icon)
        {
            if (icon == null)
                return;

            _pool.Enqueue(icon);
        }

        private Image Create()
        {
            Image image;

            if (crystalPrefab != null)
            {
                image = Instantiate(crystalPrefab, canvas.transform, false);
            }
            else
            {
                var go = new GameObject("CrystalFxIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(canvas.transform, false);
                image = go.GetComponent<Image>();
            }

            image.gameObject.SetActive(false);
            return image;
        }
    }
}

public enum FlyIconType
{
    Crystal
}
