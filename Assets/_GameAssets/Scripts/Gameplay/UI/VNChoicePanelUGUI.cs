using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNChoicePanelUGUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private Button buttonPrefab;

        private readonly List<Button> _spawned = new();

        public void Show(VN.VNRunner.VNChoicePayload payload, Action<int> onChoose)
        {
            ClearButtons();

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

                Button btn = Instantiate(buttonPrefab, buttonContainer);
                _spawned.Add(btn);

                var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    string text = option != null ? option.text : "";
                    label.text = string.IsNullOrWhiteSpace(text)
                        ? $"Option {capturedIndex + 1}"
                        : text;
                }

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onChoose?.Invoke(capturedIndex));
            }
        }

        public void Hide()
        {
            ClearButtons();

            if (root != null)
                root.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void ClearButtons()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Destroy(_spawned[i].gameObject);
            }

            _spawned.Clear();
        }
    }
}