using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN
{
    [Serializable]
    public class VNState
    {
        public string chapterId;
        public string stepId;

        public bool currentStepApplied;
        public VNMbtiState mbti = new VNMbtiState();
        public string backgroundId;
        public string musicId;

        public List<SlotState> slots = new()
        {
            new SlotState { slot = VNScreenSlot.Left },
            new SlotState { slot = VNScreenSlot.Center },
            new SlotState { slot = VNScreenSlot.Right },
        };

        public List<BoolVar> boolVars = new();
        public List<IntVar> intVars = new();
        public List<StringVar> stringVars = new();

        public List<LogEntry> log = new();

        [Serializable]
        public class SlotState
        {
            public VNScreenSlot slot;
            public bool visible;
            public string characterId;
            public VNPose pose;
            public VNEmotion emotion;
        }

        [Serializable] public class BoolVar { public string key; public bool value; }
        [Serializable] public class IntVar { public string key; public int value; }
        [Serializable] public class StringVar { public string key; public string value; }

        [Serializable]
        public class LogEntry
        {
            public string speakerId;
            public string speakerName;
            [TextArea(1, 6)] public string text;
        }

        public void ResetAll()
        {
            chapterId = null;
            stepId = null;
            currentStepApplied = false;
           
            backgroundId = null;
            musicId = null;

            EnsureSlots();
            foreach (var s in slots)
            {
                s.visible = false;
                s.characterId = null;
                s.pose = VNPose.Default;
                s.emotion = VNEmotion.Neutral;
            }

            boolVars.Clear();
            intVars.Clear();
            stringVars.Clear();
            log.Clear();
        }

        public void EnsureSlots()
        {
            if (slots == null) slots = new List<SlotState>();
            EnsureSlot(VNScreenSlot.Left);
            EnsureSlot(VNScreenSlot.Center);
            EnsureSlot(VNScreenSlot.Right);
        }

        private void EnsureSlot(VNScreenSlot slot)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].slot == slot) return;
            slots.Add(new SlotState { slot = slot });
        }

        public SlotState GetSlot(VNScreenSlot slot)
        {
            EnsureSlots();
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].slot == slot) return slots[i];
            var s = new SlotState { slot = slot };
            slots.Add(s);
            return s;
        }

        public bool GetBool(string key, bool def = false)
        {
            key = Norm(key);
            if (string.IsNullOrEmpty(key)) return def;
            for (int i = 0; i < boolVars.Count; i++)
                if (boolVars[i].key == key) return boolVars[i].value;
            return def;
        }

        public int GetInt(string key, int def = 0)
        {
            key = Norm(key);
            if (string.IsNullOrEmpty(key)) return def;
            for (int i = 0; i < intVars.Count; i++)
                if (intVars[i].key == key) return intVars[i].value;
            return def;
        }

        public string GetString(string key, string def = "")
        {
            key = Norm(key);
            if (string.IsNullOrEmpty(key)) return def;
            for (int i = 0; i < stringVars.Count; i++)
                if (stringVars[i].key == key) return stringVars[i].value ?? def;
            return def;
        }

        public void SetBool(string key, bool value)
        {
            key = Norm(key);
            if (string.IsNullOrEmpty(key)) return;
            for (int i = 0; i < boolVars.Count; i++)
                if (boolVars[i].key == key) { boolVars[i].value = value; return; }
            boolVars.Add(new BoolVar { key = key, value = value });
        }

        public void SetInt(string key, int value)
        {
            key = Norm(key);
            if (string.IsNullOrEmpty(key)) return;
            for (int i = 0; i < intVars.Count; i++)
                if (intVars[i].key == key) { intVars[i].value = value; return; }
            intVars.Add(new IntVar { key = key, value = value });
        }

        public void AddInt(string key, int delta)
        {
            key = Norm(key);
            if (string.IsNullOrEmpty(key)) return;
            for (int i = 0; i < intVars.Count; i++)
                if (intVars[i].key == key) { intVars[i].value += delta; return; }
            intVars.Add(new IntVar { key = key, value = delta });
        }

        public void SetString(string key, string value)
        {
            key = Norm(key);
            if (string.IsNullOrEmpty(key)) return;
            for (int i = 0; i < stringVars.Count; i++)
                if (stringVars[i].key == key) { stringVars[i].value = value; return; }
            stringVars.Add(new StringVar { key = key, value = value });
        }

        private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}