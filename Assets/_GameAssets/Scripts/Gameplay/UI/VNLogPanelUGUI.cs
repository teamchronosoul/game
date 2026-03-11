using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNLogPanelUGUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI logText;

        public bool IsOpen => root != null && root.activeSelf;

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        public void Show(List<VN.VNState.LogEntry> entries)
        {
            if (root != null) root.SetActive(true);

            if (logText == null) return;

            var sb = new StringBuilder(2048);
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e == null) continue;

                    if (!string.IsNullOrWhiteSpace(e.speakerName))
                        sb.Append(e.speakerName).Append(": ");

                    sb.Append(e.text ?? "").Append("\n\n");
                }
            }

            logText.text = sb.ToString();
        }
    }
}