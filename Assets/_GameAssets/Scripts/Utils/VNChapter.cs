using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN
{
    public enum VNChoiceKind { Cosmetic = 0, Important = 1, Premium = 2 }
    public enum VNGateKind { Mechanic = 0, Bonus = 1, ImportantScene = 2 }

    public enum VNVarOpType { SetBool = 0, SetInt = 1, AddInt = 2, SetString = 3 }

    [Serializable]
    public class VNVarOp
    {
        public VNVarOpType type;
        public string key;

        public bool boolValue;
        public int intValue;
        public string stringValue;
    }

    [Serializable]
    public class VNChoiceOption
    {
        [TextArea(2, 6)] public string text;
        public string nextStepId;

        public VNChoiceKind kind = VNChoiceKind.Cosmetic;
        public int premiumPrice = 0;

        public List<VNVarOp> effects = new();
    }

    public enum VNConditionValueType { Bool = 0, Int = 1, String = 2 }

    public enum VNConditionOp
    {
        Equals = 0, NotEquals = 1,
        Greater = 2, GreaterOrEqual = 3, Less = 4, LessOrEqual = 5,
        Contains = 6, NotContains = 7,
    }

    [Serializable]
    public class VNCondition
    {
        public VNConditionValueType type = VNConditionValueType.Bool;
        public string key;
        public VNConditionOp op = VNConditionOp.Equals;

        public bool boolValue;
        public int intValue;
        public string stringValue;
    }

    [Serializable]
    public abstract class VNChapterStep
    {
        public string id;      // стабильный для сейва
        public string label;   // удобная метка для геймдиза
        public bool disableSkip = false;
        public bool stopAuto = false;
    }

    [Serializable]
    public class VNLineStep : VNChapterStep
    {
        public string speakerId; // пусто = Narrator
        public VNPose pose = VNPose.Default;
        public VNEmotion emotion = VNEmotion.Neutral;

        public string sfxId;

        [TextArea(3, 12)]
        public string text;

        public bool addToLog = true;
    }

    [Serializable]
    public class VNChoiceStep : VNChapterStep
    {
        public List<VNChoiceOption> options = new();
    }

    [Serializable]
    public class VNIfStep : VNChapterStep
    {
        public bool requireAll = true;
        public List<VNCondition> conditions = new();

        public string trueStepId;  // пусто = следующий по порядку
        public string falseStepId; // пусто = следующий по порядку
    }

    [Serializable] public class VNJumpStep : VNChapterStep { public string targetStepId; }
    [Serializable] public class VNEndStep : VNChapterStep { }

    [Serializable] public abstract class VNCommand { }

    [Serializable]
    public class VNCommandStep : VNChapterStep
    {
        [SerializeReference] public VNCommand command;
    }

    [Serializable]
    public class VNSetBackgroundCommand : VNCommand
    {
        public string backgroundId;
        [Min(0f)] public float crossfadeSeconds = 0.25f;
    }

    [Serializable]
    public class VNShowCharacterCommand : VNCommand
    {
        public string characterId;
        public VNScreenSlot slot = VNScreenSlot.Left;
        public VNPose pose = VNPose.Default;
        public VNEmotion emotion = VNEmotion.Neutral;
        [Min(0f)] public float crossfadeSeconds = 0.2f;
    }

    [Serializable]
    public class VNHideCharacterCommand : VNCommand
    {
        public VNScreenSlot slot = VNScreenSlot.Left;
        [Min(0f)] public float fadeSeconds = 0.2f;
    }

    [Serializable]
    public class VNPlayMusicCommand : VNCommand
    {
        public string musicId;
        [Min(0f)] public float fadeInSeconds = 0.5f;
        public bool loop = true;
    }

    [Serializable]
    public class VNStopMusicCommand : VNCommand
    {
        [Min(0f)] public float fadeOutSeconds = 0.5f;
    }

    [Serializable] public class VNPlaySfxCommand : VNCommand { public string sfxId; }

    [Serializable] public class VNWaitCommand : VNCommand { [Min(0f)] public float seconds = 0.5f; }

    [Serializable] public class VNSetBoolVarCommand : VNCommand { public string key; public bool value; }
    [Serializable] public class VNSetIntVarCommand : VNCommand { public string key; public int value; }
    [Serializable] public class VNAddIntVarCommand : VNCommand { public string key; public int delta; }
    [Serializable] public class VNSetStringVarCommand : VNCommand { public string key; public string value; }

    [Serializable]
    public class VNGateCommand : VNCommand
    {
        public VNGateKind kind = VNGateKind.Mechanic;
        public bool gateStopsAuto = true;
        public bool gateDisablesSkip = false;
    }

    [CreateAssetMenu(fileName = "VNChapter", menuName = "VN/Chapter")]
    public class VNChapter : ScriptableObject
    {
        public string chapterId = "chapter_01";

        [SerializeReference]
        public List<VNChapterStep> steps = new();

        private readonly Dictionary<string, int> _indexById = new(StringComparer.Ordinal);

        private void OnEnable() => RebuildIndex();
        private void OnValidate() => RebuildIndex();

        public void RebuildIndex()
        {
            _indexById.Clear();
            if (steps == null) return;

            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (s == null) continue;
                var id = Norm(s.id);
                if (string.IsNullOrEmpty(id)) continue;
                if (!_indexById.ContainsKey(id)) _indexById[id] = i;
            }
        }

        public bool TryGetStepIndex(string stepId, out int index)
        {
            if (_indexById.Count == 0) RebuildIndex();
            index = -1;
            stepId = Norm(stepId);
            if (string.IsNullOrEmpty(stepId)) return false;
            return _indexById.TryGetValue(stepId, out index);
        }

        public VNChapterStep GetStepAt(int index) => (index < 0 || index >= steps.Count) ? null : steps[index];

        [ContextMenu("Generate Missing Step IDs")]
        public void GenerateMissingStepIds()
        {
            for (int i = 0; i < steps.Count; i++)
                if (steps[i] != null && string.IsNullOrWhiteSpace(steps[i].id))
                    steps[i].id = Guid.NewGuid().ToString("N");

            RebuildIndex();
        }

        private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}