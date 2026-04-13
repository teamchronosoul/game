using TMPro;
using UnityEngine;

namespace VN.UI
{
    public class VNLogEntryItemUGUI : MonoBehaviour
    {
        [Header("Speaker")]
        [SerializeField] private GameObject speakerRoot;
        [SerializeField] private TextMeshProUGUI speakerText;

        [Header("Body")]
        [SerializeField] private TextMeshProUGUI bodyText;

        public void Bind(VN.VNState.LogEntry entry)
        {
            bool hasSpeaker = !string.IsNullOrWhiteSpace(entry.speakerName);

            if (speakerRoot != null)
                speakerRoot.SetActive(hasSpeaker);

            if (speakerText != null)
                speakerText.text = hasSpeaker ? entry.speakerName : string.Empty;

            if (bodyText != null)
                bodyText.text = entry.text ?? string.Empty;
        }
    }
}