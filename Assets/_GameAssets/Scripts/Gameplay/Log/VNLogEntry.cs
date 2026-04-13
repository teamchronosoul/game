using System;
using UnityEngine;

namespace VN
{
    [Serializable]
    public class VNLogEntry
    {
        [TextArea(2, 8)]
        public string text;

        public string speaker;
        public bool hasSpeaker;

        public VNLogEntry() { }

        public VNLogEntry(string text, string speaker = "", bool hasSpeaker = false)
        {
            this.text = text ?? string.Empty;
            this.speaker = speaker ?? string.Empty;
            this.hasSpeaker = hasSpeaker && !string.IsNullOrWhiteSpace(this.speaker);
        }
    }
}