using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNChoicePanelUGUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private Transform buttonContainer;

        [Header("Prefabs")]
        [SerializeField] private Button buttonPrefab;
        [Tooltip("Опциональный отдельный prefab для платного выбора. Если пусто, используется обычный buttonPrefab.")]
        [SerializeField] private Button premiumButtonPrefab;

        [Header("Background Sprites")]
        [Tooltip("Опциональная подложка обычного выбора. Если пусто, sprite prefab не меняется.")]
        [SerializeField] private Sprite normalChoiceBackground;
        [Tooltip("Подложка платного выбора.")]
        [SerializeField] private Sprite premiumChoiceBackground;
        [Tooltip("Опциональная подложка платного выбора, если валюты не хватает.")]
        [SerializeField] private Sprite premiumLockedChoiceBackground;

        [Header("Text Binding")]
        [Tooltip("Имя объекта с основным текстом выбора в prefab. Если не найден, берется первый TextMeshProUGUI.")]
        [SerializeField] private string labelTextObjectName = "Label";
        [Tooltip("Имя объекта с ценой в prefab. Если не найден, берется второй TextMeshProUGUI, если он есть.")]
        [SerializeField] private string priceTextObjectName = "PriceText";
        [SerializeField] private string premiumPriceFormat = "💎 {0}";
        [Tooltip("Если в prefab нет отдельного текста цены, цена будет добавлена к основному тексту.")]
        [SerializeField] private bool appendPriceToLabelWhenPriceTextMissing = true;

        [Header("Behaviour")]
        [Tooltip("Если выключено, платные выборы без достаточного количества валюты будут disabled.")]
        [SerializeField] private bool allowClickWhenNotEnoughCurrency;

        private readonly List<ButtonBinding> _spawned = new();
        private VN.VNRunner.VNChoicePayload _currentPayload;
        private Action<int> _onChoose;

        private class ButtonBinding
        {
            public Button button;
            public Image background;
            public TextMeshProUGUI label;
            public TextMeshProUGUI priceLabel;
            public VNChoiceOption option;
            public int index;
            public string baseText;
        }

        private void OnEnable()
        {
            VNCrystalWallet.OnChanged += OnCurrencyChanged;
            RefreshButtonsState();
        }

        private void OnDisable()
        {
            VNCrystalWallet.OnChanged -= OnCurrencyChanged;
        }

        public void Show(VN.VNRunner.VNChoicePayload payload, Action<int> onChoose)
        {
            ClearButtons();

            _currentPayload = payload;
            _onChoose = onChoose;

            if (root != null)
                root.SetActive(true);
            else
                gameObject.SetActive(true);

            if (payload.options == null || payload.options.Length == 0 || buttonPrefab == null || buttonContainer == null)
                return;

            for (int i = 0; i < payload.options.Length; i++)
            {
                int capturedIndex = i;
                var option = payload.options[i];
                var isPremium = IsPremiumOption(option);

                var prefab = isPremium && premiumButtonPrefab != null ? premiumButtonPrefab : buttonPrefab;
                Button btn = Instantiate(prefab, buttonContainer);

                var binding = BuildBinding(btn, option, capturedIndex);
                _spawned.Add(binding);

                ApplyTexts(binding);
                ApplyBackground(binding);

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => _onChoose?.Invoke(capturedIndex));
            }

            RefreshButtonsState();
        }

        public void Hide()
        {
            ClearButtons();
            _currentPayload = default;
            _onChoose = null;

            if (root != null)
                root.SetActive(false);
            else
                gameObject.SetActive(false);
        }


        public RectTransform GetChoiceButtonRectTransform(int optionIndex)
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                var binding = _spawned[i];
                if (binding == null || binding.index != optionIndex || binding.button == null)
                    continue;

                return binding.button.transform as RectTransform;
            }

            return null;
        }

        public void RefreshCurrentChoices()
        {
            RefreshButtonsState();
        }

        private ButtonBinding BuildBinding(Button button, VNChoiceOption option, int index)
        {
            var textComponents = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            var label = FindTextByName(button.transform, labelTextObjectName);
            if (label == null && textComponents.Length > 0)
                label = textComponents[0];

            var price = FindTextByName(button.transform, priceTextObjectName);
            if (price == label)
                price = null;

            if (price == null && textComponents.Length > 1)
            {
                for (int i = 0; i < textComponents.Length; i++)
                {
                    if (textComponents[i] != null && textComponents[i] != label)
                    {
                        price = textComponents[i];
                        break;
                    }
                }
            }

            var background = button.targetGraphic as Image;
            if (background == null)
                background = button.GetComponent<Image>();

            return new ButtonBinding
            {
                button = button,
                background = background,
                label = label,
                priceLabel = price,
                option = option,
                index = index,
                baseText = ResolveChoiceText(option, index)
            };
        }

        private void ApplyTexts(ButtonBinding binding)
        {
            if (binding == null)
                return;

            var isPremium = IsPremiumOption(binding.option);
            var price = GetPremiumPrice(binding.option);
            var priceText = string.Format(premiumPriceFormat, price);

            if (binding.label != null)
            {
                binding.label.text = isPremium && binding.priceLabel == null && appendPriceToLabelWhenPriceTextMissing
                    ? $"{binding.baseText}\n<size=80%>{priceText}</size>"
                    : binding.baseText;
            }

            if (binding.priceLabel != null)
            {
                binding.priceLabel.gameObject.SetActive(isPremium);
                if (isPremium)
                    binding.priceLabel.text = priceText;
            }
        }

        private void ApplyBackground(ButtonBinding binding)
        {
            if (binding == null || binding.background == null)
                return;

            var isPremium = IsPremiumOption(binding.option);
            var enough = !isPremium || VNCrystalWallet.CanSpend(GetPremiumPrice(binding.option));

            Sprite sprite = null;
            if (isPremium)
                sprite = enough ? premiumChoiceBackground : premiumLockedChoiceBackground != null ? premiumLockedChoiceBackground : premiumChoiceBackground;
            else
                sprite = normalChoiceBackground;

            if (sprite != null)
                binding.background.sprite = sprite;
        }

        private void RefreshButtonsState()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                var binding = _spawned[i];
                if (binding == null || binding.button == null)
                    continue;

                var isPremium = IsPremiumOption(binding.option);
                var canAfford = !isPremium || VNCrystalWallet.CanSpend(GetPremiumPrice(binding.option));

                binding.button.interactable = !isPremium || canAfford || allowClickWhenNotEnoughCurrency;
                ApplyBackground(binding);
                ApplyTexts(binding);
            }
        }

        private void OnCurrencyChanged(int balance, int delta)
        {
            RefreshButtonsState();
        }

        private void ClearButtons()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i]?.button != null)
                    Destroy(_spawned[i].button.gameObject);
            }

            _spawned.Clear();
        }

        private static bool IsPremiumOption(VNChoiceOption option)
        {
            return option != null && option.kind == VNChoiceKind.Premium && option.premiumPrice > 0;
        }

        private static int GetPremiumPrice(VNChoiceOption option)
        {
            if (!IsPremiumOption(option))
                return 0;

            return Mathf.Max(0, option.premiumPrice);
        }

        private static string ResolveChoiceText(VNChoiceOption option, int index)
        {
            var text = option != null ? option.text : "";
            return string.IsNullOrWhiteSpace(text) ? $"Option {index + 1}" : text;
        }

        private static TextMeshProUGUI FindTextByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
                return null;

            var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && string.Equals(texts[i].gameObject.name, objectName, StringComparison.OrdinalIgnoreCase))
                    return texts[i];
            }

            return null;
        }
    }
}
