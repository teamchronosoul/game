using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNChoicePanelUGUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Transform container;
        [SerializeField] private Button optionButtonPrefab;

        private readonly List<Button> _buttons = new();

        public void Hide()
        {
            ClearButtons();
            if (root != null) root.SetActive(false);
        }

        public void Show(VN.VNRunner.VNChoicePayload payload, Action<int> onChoose)
        {
            if (root != null) root.SetActive(true);
            ClearButtons();

            var opts = payload.options ?? Array.Empty<VN.VNChoiceOption>();

            for (int i = 0; i < opts.Length; i++)
            {
                int idx = i;
                var btn = Instantiate(optionButtonPrefab, container);
                _buttons.Add(btn);

                var txt = btn.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    string label = opts[i].text ?? "";
                    if (opts[i].kind == VN.VNChoiceKind.Premium && opts[i].premiumPrice > 0)
                        label += $"  (💎{opts[i].premiumPrice})";
                    txt.text = label;
                }

                btn.onClick.AddListener(() => onChoose?.Invoke(idx));
            }
        }

        private void ClearButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i] != null) Destroy(_buttons[i].gameObject);
            _buttons.Clear();
        }
    }
}