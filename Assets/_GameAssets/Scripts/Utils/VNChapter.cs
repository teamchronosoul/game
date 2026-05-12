using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

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

        [Tooltip("Если выключено, вместо имени говорящего будет показано ???")]
        public bool showSpeakerName = true;

        public string sfxId;

        [TextArea(3, 12)]
        public string text;

        public bool addToLog = true;
        public string nextStepId;
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
        public string nextStepId;

    }
    [Serializable]
    public sealed class VNVfxCommand : VNCommand
    {
        [Header("VFX")]
        public string vfxId;
        public string anchorId = "center";
        public Vector3 localOffset = Vector3.zero;

        [Min(0.01f)]
        public float scale = 1f;

        [Tooltip("Если > 0, время активной игры будет взято отсюда")]
        public float lifetimeOverride = -1f;

        [Tooltip("Если >= 0, переопределяет softStopSeconds из definition")]
        public float softStopSecondsOverride = -1f;

        [Tooltip("Если включено, VN будет ждать завершения эффекта")]
        public bool waitUntilFinished = false;
    }
    [Serializable]
    public sealed class VNTruthEyeCommand : VNCommand
    {
        [Header("Truth Eye Minigame")]

        [Tooltip("Сколько секунд нужно непрерывно удерживать глаз в зоне.")]
        [Min(1f)]
        public float holdSeconds = 15f;

        [Tooltip("После скольких ошибок показывать кнопку Skip. 0 = Skip доступен сразу.")]
        [Min(0)]
        public int failsBeforeSkip = 0;

        [Tooltip("Можно ли скипнуть мини-игру.")]
        public bool allowSkipAfterFails = true;

        [Tooltip("Если true, первый проигрыш завершает мини-игру с результатом failed.")]
        public bool finishOnFail = true;

        [Tooltip("Если <= 0, будет использовано значение из компонента мини-игры.")]
        public float driftStrength = -1f;

        [Header("Result Condition")]
        [Tooltip("Один bool-ключ для условий главы. true = победа, false = поражение или Skip.")]
        public string resultBoolKey = "truth_eye_win";
    }
    
    [Serializable]
    public class VNSetBackgroundCommand : VNCommand
    {
        public string backgroundId;
        [Min(0f)] public float crossfadeSeconds = 0.25f;

        [Header("Location Intro")]
        [Tooltip("Если включено, при первом показе или смене этого фона раннер делает короткое скольжение камеры и задерживает следующие шаги.")]
        public bool playLocationIntro = true;

        [Tooltip("Включить интро даже если backgroundId совпадает с текущим. Обычно выключено, чтобы не повторять скольжение на том же фоне.")]
        public bool forceLocationIntro = false;

        [Tooltip("0 = использовать дефолт из VNRunner. Если задано, значение будет зажато в диапазон 1-2 секунды.")]
        [Min(0f)] public float locationIntroDurationOverride = 0f;

        // Location intro plate temporarily disabled.
        // Keep this block for quick restore when the plate is needed again.
        /*
        [Tooltip("Опциональное название локации для плашки. Если пусто, берется из VNAssetDatabase.backgrounds или fallback = backgroundId.")]
        public string locationName;

        [Tooltip("Опциональное время суток для плашки. Если пусто, берется из VNAssetDatabase.backgrounds, если там заполнено.")]
        public string timeOfDay;
        */
    }
    
    [System.Serializable]
    public class VNMbtiAnswerCommand : VNCommand
    {
        public VNMbtiLetter letter;
    }
    [System.Serializable]
    public class VNResolveMbtiCommand : VNCommand
    {
    }
    [Serializable]
    public class VNGiveArtifactCommand : VNCommand
    {
        public string artifactId;

        [Header("Animation")]
        public float dimAlpha = 0.65f;
        public float fadeInSeconds = 0.2f;
        public float scaleUpSeconds = 0.2f;
        public float scaleSettleSeconds = 0.12f;
        public float holdSeconds = 0.8f;
        public float fadeOutSeconds = 0.2f;
    }
    [Serializable]
    public class VNGiveCrystalsCommand : VNCommand
    {
        [Min(1)] public int amount = 50;

        [Tooltip("Если включено, начисление идет через CoinFxManager полетом к счетчику валюты.")]
        public bool playFlyAnimation = true;
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

    [Serializable]
    public class VNShowCutsceneCommand : VNCommand
    {
        [Header("Cutscene Video")]
        [Tooltip("ID видео из VNAssetDatabase/Cutscenes. Можно оставить пустым, если задан Clip Override.")]
        public string cutsceneId;

        [Tooltip("Опционально: прямой клип. Удобно для теста, но для сохранения лучше использовать cutsceneId из базы.")]
        public VideoClip clipOverride;

        [Header("UI Visibility")]
        [Tooltip("Скрывать диалоговую плашку и выборы, пока катсцена видна.")]
        public bool hideDialogue = true;

        [Tooltip("Скрывать персонажей, пока катсцена видна.")]
        public bool hideCharacters = true;

        [Tooltip("Блокировать клики по VN, пока катсцена видна. Полезно для автокатсцен через Show -> Wait -> Hide.")]
        public bool blockInput = true;

        [Header("Playback")]
        [Min(0f)] public float fadeInSeconds = 0.15f;
        public bool playAudio = true;
        [Range(0f, 1f)] public float audioVolume = 1f;
    }

    [Serializable]
    public class VNHideCutsceneCommand : VNCommand
    {
        [Min(0f)] public float fadeOutSeconds = 0.15f;
    }

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