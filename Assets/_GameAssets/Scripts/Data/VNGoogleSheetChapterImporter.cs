using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using YP;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VN
{
    [CreateAssetMenu(
        fileName = "VNGoogleSheetChapterImporter",
        menuName = "VN/Google Sheet Chapter Importer")]
    public class VNGoogleSheetChapterImporter : LoadableFromTable
    {
        [Serializable]
        public class SpeakerAlias
        {
            public string tableValue;
            public string speakerId;
        }

        public enum ChapterImportMode
        {
            FullRebuild = 0,
            SkipCompletely = 1,
            TextOnlyExistingLines = 2
        }

        public enum TextOnlyLineMatchMode
        {
            StableIdOnly = 0,
            StableIdThenLineOrder = 1,
            LineOrderOnly = 2
        }

        [Serializable]
        public class ChapterBinding
        {
            [Header("Source")]
            public string tableKey;

            [Header("Target")]
            public VNChapter chapter;
            public string chapterIdOverride;

            [Header("Import Mode")]
            public ChapterImportMode importMode = ChapterImportMode.FullRebuild;

            [Header("Sheet Layout")]
            [Min(1)] public int firstDataRow = 2;
            public Column speakerColumn = Column.A;
            public Column emotionColumn = Column.B;
            public Column flowColumn = Column.C;
            public Column firstChoiceColumn = Column.D;

            [Header("Build")]
            public bool appendEndStep = true;

            [Header("Text Only Mode")]
            public TextOnlyLineMatchMode textOnlyLineMatchMode = TextOnlyLineMatchMode.StableIdThenLineOrder;
        }

        [Header("Import Bindings")]
        [SerializeField] private List<ChapterBinding> chapters = new();

        [Header("Speaker Mapping")]
        [SerializeField] private List<SpeakerAlias> speakerAliases = new();

        [Header("Narrator aliases -> empty speakerId")]
        [SerializeField]
        private List<string> narratorAliases = new()
        {
            "Narrator",
            "narrator",
            "Нарратор",
            "Автор",
            "-"
        };

        [Header("Character Database")]
        [SerializeField] private VNCharacterDatabase characterDatabase;

#if UNITY_EDITOR
        [SerializeField] private bool autoFindCharacterDatabasesInProject = true;
#endif

        [SerializeField] private bool matchSpeakerByCharacterId = true;
        [SerializeField] private bool matchSpeakerByDisplayName = true;

        [Header("Debug")]
        [SerializeField] private bool logWarnings = true;

        private readonly Dictionary<string, string> _speakerIdLookup = new(StringComparer.OrdinalIgnoreCase);
        private bool _speakerLookupBuilt;

        public override void LoadData(Dictionary<string, Table> allTables)
        {
            if (chapters == null || chapters.Count == 0)
                return;

            foreach (var binding in chapters)
            {
                if (binding == null)
                    continue;

                if (binding.importMode == ChapterImportMode.SkipCompletely)
                {
                    Debug.Log($"VN Google Sheet Importer: chapter binding '{binding.tableKey}' skipped completely.");
                    continue;
                }

                if (binding.chapter == null)
                {
                    Warn($"Importer '{name}': target chapter is null for table '{binding.tableKey}'.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(binding.tableKey))
                {
                    Warn($"Importer '{name}': tableKey is empty for chapter '{binding.chapter.name}'.");
                    continue;
                }

                if (!allTables.TryGetValue(binding.tableKey, out var table) || table == null)
                {
                    Warn($"Importer '{name}': table '{binding.tableKey}' not found.");
                    continue;
                }

                EnsureSpeakerLookup();

                var builder = new Builder(this, binding, table);

                switch (binding.importMode)
                {
                    case ChapterImportMode.FullRebuild:
                        builder.BuildInto(binding.chapter);
                        break;

                    case ChapterImportMode.TextOnlyExistingLines:
                        builder.UpdateOnlyLineText(binding.chapter);
                        break;

                    case ChapterImportMode.SkipCompletely:
                        break;
                }
            }
        }

        private void EnsureSpeakerLookup()
        {
            if (_speakerLookupBuilt)
                return;

            _speakerLookupBuilt = true;
            _speakerIdLookup.Clear();

            AddCharacterDatabaseToLookup(characterDatabase);

#if UNITY_EDITOR
            if (autoFindCharacterDatabasesInProject)
            {
                var guids = AssetDatabase.FindAssets("t:VNCharacterDatabase");

                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var db = AssetDatabase.LoadAssetAtPath<VNCharacterDatabase>(path);
                    AddCharacterDatabaseToLookup(db);
                }
            }
#endif
        }

        private void AddCharacterDatabaseToLookup(VNCharacterDatabase db)
        {
            if (db == null || db.Characters == null)
                return;

            foreach (var ch in db.Characters)
            {
                if (ch == null)
                    continue;

                var id = NormalizeLookupKey(ch.id);
                var displayName = NormalizeLookupKey(ch.displayName);

                if (matchSpeakerByCharacterId && !string.IsNullOrWhiteSpace(id))
                    RegisterSpeakerLookup(id, ch.id?.Trim());

                if (matchSpeakerByDisplayName && !string.IsNullOrWhiteSpace(displayName))
                    RegisterSpeakerLookup(displayName, ch.id?.Trim());
            }
        }

        private void RegisterSpeakerLookup(string sourceValue, string speakerId)
        {
            sourceValue = NormalizeLookupKey(sourceValue);
            speakerId = speakerId?.Trim();

            if (string.IsNullOrWhiteSpace(sourceValue) || string.IsNullOrWhiteSpace(speakerId))
                return;

            if (!_speakerIdLookup.ContainsKey(sourceValue))
                _speakerIdLookup[sourceValue] = speakerId;
        }

        private bool TryResolveSpeakerFromDatabase(string rawValue, out string speakerId)
        {
            EnsureSpeakerLookup();

            speakerId = string.Empty;

            var key = NormalizeLookupKey(rawValue);

            if (string.IsNullOrWhiteSpace(key))
                return false;

            return _speakerIdLookup.TryGetValue(key, out speakerId) &&
                   !string.IsNullOrWhiteSpace(speakerId);
        }

        private bool TryMapSpeakerAlias(string rawValue, out string mapped)
        {
            mapped = rawValue;

            if (speakerAliases == null)
                return false;

            for (var i = 0; i < speakerAliases.Count; i++)
            {
                var alias = speakerAliases[i];

                if (alias == null || string.IsNullOrWhiteSpace(alias.tableValue))
                    continue;

                if (string.Equals(alias.tableValue.Trim(), rawValue, StringComparison.OrdinalIgnoreCase))
                {
                    mapped = alias.speakerId?.Trim() ?? string.Empty;
                    return true;
                }
            }

            return false;
        }

        private bool IsNarrator(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return true;

            if (narratorAliases == null)
                return false;

            for (var i = 0; i < narratorAliases.Count; i++)
            {
                var alias = narratorAliases[i];

                if (string.IsNullOrWhiteSpace(alias))
                    continue;

                if (string.Equals(alias.Trim(), rawValue, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void Warn(string message)
        {
            if (logWarnings)
                Debug.LogWarning(message);
        }

        private static string NormalizeLookupKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToLowerInvariant();
        }

        private sealed class Builder
        {
            private sealed class ParseResult
            {
                public string firstStepId;
                public readonly List<VNChapterStep> terminals = new();
                public readonly List<VNChoiceOption> pendingDirectOptions = new();
            }

            private readonly VNGoogleSheetChapterImporter owner;
            private readonly ChapterBinding binding;
            private readonly Table table;
            private readonly List<VNChapterStep> builtSteps = new();

            private readonly string idPrefix;

            public Builder(VNGoogleSheetChapterImporter owner, ChapterBinding binding, Table table)
            {
                this.owner = owner;
                this.binding = binding;
                this.table = table;

                idPrefix = SanitizeIdPart(binding.tableKey);
            }

            public void BuildInto(VNChapter chapter)
            {
                builtSteps.Clear();

                var startRow = Mathf.Max(0, binding.firstDataRow - 1);
                var flowCol = (int)binding.flowColumn;
                var choiceStartCol = (int)binding.firstChoiceColumn;

                var root = ParseFlow(
                    flowCol,
                    startRow,
                    table.rows,
                    -1,
                    0,
                    choiceStartCol);

                var needEnd =
                    binding.appendEndStep ||
                    string.IsNullOrWhiteSpace(root.firstStepId) ||
                    root.terminals.Count > 0 ||
                    root.pendingDirectOptions.Count > 0;

                if (needEnd)
                {
                    var end = new VNEndStep
                    {
                        id = $"{idPrefix}_end",
                        label = "END"
                    };

                    AddStep(end);
                    ConnectResultToTarget(root, end.id);
                }

                if (builtSteps.Count == 0)
                {
                    builtSteps.Add(new VNEndStep
                    {
                        id = $"{idPrefix}_end",
                        label = "END"
                    });
                }

                chapter.steps = new List<VNChapterStep>(builtSteps);

                if (!string.IsNullOrWhiteSpace(binding.chapterIdOverride))
                    chapter.chapterId = binding.chapterIdOverride.Trim();
                else if (string.IsNullOrWhiteSpace(chapter.chapterId))
                    chapter.chapterId = binding.tableKey.Trim();

                chapter.RebuildIndex();

#if UNITY_EDITOR
                EditorUtility.SetDirty(chapter);
#endif
            }

            public void UpdateOnlyLineText(VNChapter chapter)
            {
                builtSteps.Clear();

                if (chapter == null || chapter.steps == null)
                    return;

                var startRow = Mathf.Max(0, binding.firstDataRow - 1);
                var flowCol = (int)binding.flowColumn;
                var choiceStartCol = (int)binding.firstChoiceColumn;

                ParseFlow(
                    flowCol,
                    startRow,
                    table.rows,
                    -1,
                    0,
                    choiceStartCol);

                var sourceLines = builtSteps
                    .OfType<VNLineStep>()
                    .ToList();

                var sourceLinesById = new Dictionary<string, VNLineStep>(StringComparer.Ordinal);

                for (var i = 0; i < sourceLines.Count; i++)
                {
                    var id = NormalizeId(sourceLines[i].id);

                    if (string.IsNullOrEmpty(id))
                        continue;

                    if (!sourceLinesById.ContainsKey(id))
                        sourceLinesById.Add(id, sourceLines[i]);
                }

                var targetLines = chapter.steps
                    .OfType<VNLineStep>()
                    .ToList();

                var matchedById = 0;
                var matchedByOrder = 0;
                var changed = 0;

                // Важно: не прятать под #if UNITY_EDITOR.
                // Иначе в билде переменная не существует, но передаётся в ApplyTextIfChanged.
                var undoRecorded = false;

                var sourceUsed = new bool[sourceLines.Count];
                var targetUpdated = new bool[targetLines.Count];

                var allowIdMatch =
                    binding.textOnlyLineMatchMode == TextOnlyLineMatchMode.StableIdOnly ||
                    binding.textOnlyLineMatchMode == TextOnlyLineMatchMode.StableIdThenLineOrder;

                var allowOrderMatch =
                    binding.textOnlyLineMatchMode == TextOnlyLineMatchMode.LineOrderOnly ||
                    binding.textOnlyLineMatchMode == TextOnlyLineMatchMode.StableIdThenLineOrder;

                if (allowIdMatch)
                {
                    for (var targetIndex = 0; targetIndex < targetLines.Count; targetIndex++)
                    {
                        var targetLine = targetLines[targetIndex];

                        if (targetLine == null)
                            continue;

                        var targetId = NormalizeId(targetLine.id);

                        if (string.IsNullOrEmpty(targetId))
                            continue;

                        if (!sourceLinesById.TryGetValue(targetId, out var sourceLine))
                            continue;

                        var sourceIndex = sourceLines.IndexOf(sourceLine);

                        if (sourceIndex >= 0)
                            sourceUsed[sourceIndex] = true;

                        targetUpdated[targetIndex] = true;
                        matchedById++;

                        ApplyTextIfChanged(
                            chapter,
                            targetLine,
                            sourceLine.text,
                            ref changed,
                            ref undoRecorded);
                    }
                }

                if (allowOrderMatch)
                {
                    var sourceCursor = 0;

                    for (var targetIndex = 0; targetIndex < targetLines.Count; targetIndex++)
                    {
                        if (targetUpdated[targetIndex])
                            continue;

                        while (sourceCursor < sourceLines.Count && sourceUsed[sourceCursor])
                            sourceCursor++;

                        if (sourceCursor >= sourceLines.Count)
                            break;

                        var targetLine = targetLines[targetIndex];
                        var sourceLine = sourceLines[sourceCursor];

                        sourceUsed[sourceCursor] = true;
                        targetUpdated[targetIndex] = true;
                        matchedByOrder++;

                        ApplyTextIfChanged(
                            chapter,
                            targetLine,
                            sourceLine.text,
                            ref changed,
                            ref undoRecorded);

                        sourceCursor++;
                    }
                }

                if (changed > 0)
                {
#if UNITY_EDITOR
                    EditorUtility.SetDirty(chapter);
#endif
                }

                Debug.Log(
                    $"VN Google Sheet Importer: text-only update for '{chapter.name}'. " +
                    $"Source lines: {sourceLines.Count}. Existing lines: {targetLines.Count}. " +
                    $"Matched by id: {matchedById}. Matched by line order: {matchedByOrder}. Changed texts: {changed}. " +
                    "Commands and non-line steps were ignored.");

                builtSteps.Clear();
            }

            private ParseResult ParseFlow(
                int flowCol,
                int startRow,
                int endRowExclusive,
                int variantIndex,
                int variantCount,
                int choiceStartCol)
            {
                var result = new ParseResult();
                var row = startRow;

                while (row < endRowExclusive)
                {
                    var flowText = GetCell(row, flowCol);

                    if (IsIgnoredRowMarker(flowText))
                    {
                        row++;
                        continue;
                    }

                    if (TryCreateCommandStep(row, flowCol, out var commandStep))
                    {
                        AppendTerminalStep(result, commandStep);
                        row++;
                        continue;
                    }

                    var choiceColumns = CollectChoiceColumns(row, choiceStartCol);

                    var hasText = !string.IsNullOrWhiteSpace(flowText);
                    var hasChoice = choiceColumns.Count > 0;

                    if (!hasText && !hasChoice)
                    {
                        row++;
                        continue;
                    }

                    if (hasText)
                    {
                        var line = CreateLineStep(row, flowCol, flowText, variantIndex, variantCount);
                        AppendTerminalStep(result, line);
                    }

                    if (hasChoice)
                    {
                        var choice = CreateChoiceStep(row, flowCol, choiceColumns);
                        AppendNonTerminalStep(result, choice);

                        var branchStartRow = row + 1;
                        var returnRow = FindReturnRow(flowCol, branchStartRow, endRowExclusive);
                        var nestedChoiceStartCol = choiceStartCol + choiceColumns.Count;

                        var continuation = ParseFlow(
                            flowCol,
                            returnRow,
                            endRowExclusive,
                            variantIndex,
                            variantCount,
                            choiceStartCol);

                        var unresolvedAfterChoice = new ParseResult();

                        for (var optionIndex = 0; optionIndex < choiceColumns.Count; optionIndex++)
                        {
                            var branchCol = choiceColumns[optionIndex];
                            var option = choice.options[optionIndex];

                            var branch = ParseFlow(
                                branchCol,
                                branchStartRow,
                                returnRow,
                                optionIndex,
                                choiceColumns.Count,
                                nestedChoiceStartCol);

                            if (!string.IsNullOrWhiteSpace(branch.firstStepId))
                                option.nextStepId = branch.firstStepId;
                            else if (!string.IsNullOrWhiteSpace(continuation.firstStepId))
                                option.nextStepId = continuation.firstStepId;
                            else
                                unresolvedAfterChoice.pendingDirectOptions.Add(option);

                            if (!string.IsNullOrWhiteSpace(continuation.firstStepId))
                            {
                                ConnectResultToTarget(branch, continuation.firstStepId);
                            }
                            else
                            {
                                unresolvedAfterChoice.terminals.AddRange(branch.terminals);
                                unresolvedAfterChoice.pendingDirectOptions.AddRange(branch.pendingDirectOptions);
                            }
                        }

                        result.terminals.Clear();
                        result.pendingDirectOptions.Clear();

                        if (!string.IsNullOrWhiteSpace(continuation.firstStepId))
                        {
                            result.terminals.AddRange(continuation.terminals);
                            result.pendingDirectOptions.AddRange(continuation.pendingDirectOptions);
                        }
                        else
                        {
                            result.terminals.AddRange(unresolvedAfterChoice.terminals);
                            result.pendingDirectOptions.AddRange(unresolvedAfterChoice.pendingDirectOptions);
                        }

                        return result;
                    }

                    row++;
                }

                return result;
            }

            private static bool IsIgnoredRowMarker(string value)
            {
                return string.Equals(NormalizeText(value), "-", StringComparison.Ordinal);
            }

            private VNLineStep CreateLineStep(int row, int flowCol, string text, int variantIndex, int variantCount)
            {
                var rawSpeaker = GetVariantCell(row, (int)binding.speakerColumn, variantIndex, variantCount);
                var rawEmotion = GetVariantCell(row, (int)binding.emotionColumn, variantIndex, variantCount);

                var speakerId = NormalizeSpeaker(rawSpeaker);
                var emotion = ParseEmotion(rawEmotion, speakerId, row);

                return new VNLineStep
                {
                    id = MakeStepId("line", row, flowCol),
                    label = BuildLineLabel(row, flowCol, speakerId, text),
                    speakerId = speakerId,
                    pose = VNPose.Default,
                    emotion = emotion,
                    sfxId = string.Empty,
                    text = NormalizeText(text),
                    addToLog = true,
                    nextStepId = string.Empty
                };
            }

            private VNChoiceStep CreateChoiceStep(int row, int flowCol, List<int> choiceColumns)
            {
                var step = new VNChoiceStep
                {
                    id = MakeStepId("choice", row, flowCol),
                    label = $"Choice R{row + 1} C{flowCol + 1}"
                };

                for (var i = 0; i < choiceColumns.Count; i++)
                {
                    var col = choiceColumns[i];
                    var optionText = NormalizeText(GetCell(row, col));

                    step.options.Add(CreateChoiceOption(optionText));
                }

                return step;
            }


            private bool TryCreateCommandStep(int row, int flowCol, out VNCommandStep step)
            {
                step = null;

                // Командные строки относятся к основному потоку.
                // Иначе во время разбора веток выбора одна строка из A/B могла бы случайно продублироваться в каждой ветке.
                if (flowCol != (int)binding.flowColumn)
                    return false;

                // Команды задаются для геймдизайнера в первых двух столбцах:
                // A = название команды без префикса VN и без суффикса Command, B = основной параметр.
                // Обычные строки продолжают использовать A как speaker, B как emotion, C как текст.
                var rawCommandName = GetCell(row, (int)binding.speakerColumn);
                var rawArgument = GetCell(row, (int)binding.emotionColumn);

                if (!TryCreateCommand(rawCommandName, rawArgument, row, out var command, out var label))
                    return false;

                step = new VNCommandStep
                {
                    id = MakeStepId("cmd", row, flowCol),
                    label = label,
                    command = command,
                    nextStepId = string.Empty
                };

                return true;
            }

            private bool TryCreateCommand(
                string rawCommandName,
                string rawArgument,
                int row,
                out VNCommand command,
                out string label)
            {
                command = null;
                label = string.Empty;

                rawCommandName = NormalizeText(rawCommandName);
                rawArgument = NormalizeText(rawArgument);

                if (string.IsNullOrWhiteSpace(rawCommandName))
                    return false;

                var token = NormalizeCommandToken(rawCommandName);

                switch (token)
                {
                    case "setbackground":
                    case "background":
                    case "location":
                    case "setlocation":
                        command = new VNSetBackgroundCommand
                        {
                            backgroundId = rawArgument,
                            crossfadeSeconds = 0.25f,
                            playLocationIntro = true,
                            forceLocationIntro = false,
                            locationIntroDurationOverride = 0f
                        };
                        label = BuildCommandLabel(row, "SetBackground", rawArgument);
                        return true;

                    case "showcharacter":
                    case "character":
                    case "showchar":
                        command = new VNShowCharacterCommand
                        {
                            characterId = rawArgument,
                            slot = VNScreenSlot.Left,
                            pose = VNPose.Default,
                            emotion = VNEmotion.Neutral,
                            crossfadeSeconds = 0.2f
                        };
                        label = BuildCommandLabel(row, "ShowCharacter", rawArgument);
                        return true;

                    case "hidecharacter":
                    case "hidechar":
                        command = new VNHideCharacterCommand
                        {
                            slot = ParseEnumOrDefault(rawArgument, VNScreenSlot.Left),
                            fadeSeconds = 0.2f
                        };
                        label = BuildCommandLabel(row, "HideCharacter", rawArgument);
                        return true;

                    case "playmusic":
                    case "music":
                        command = new VNPlayMusicCommand
                        {
                            musicId = rawArgument,
                            fadeInSeconds = 0.5f,
                            loop = true
                        };
                        label = BuildCommandLabel(row, "PlayMusic", rawArgument);
                        return true;

                    case "stopmusic":
                        command = new VNStopMusicCommand
                        {
                            fadeOutSeconds = 0.5f
                        };
                        label = BuildCommandLabel(row, "StopMusic", rawArgument);
                        return true;

                    case "playsfx":
                    case "sfx":
                    case "sound":
                        command = new VNPlaySfxCommand
                        {
                            sfxId = rawArgument
                        };
                        label = BuildCommandLabel(row, "PlaySfx", rawArgument);
                        return true;

                    case "showcutscene":
                    case "cutscene":
                        command = new VNShowCutsceneCommand
                        {
                            cutsceneId = rawArgument,
                            clipOverride = null,
                            hideDialogue = true,
                            hideCharacters = true,
                            blockInput = true,
                            fadeInSeconds = 0.15f,
                            playAudio = true,
                            audioVolume = 1f
                        };
                        label = BuildCommandLabel(row, "ShowCutscene", rawArgument);
                        return true;

                    case "hidecutscene":
                        command = new VNHideCutsceneCommand
                        {
                            fadeOutSeconds = 0.15f
                        };
                        label = BuildCommandLabel(row, "HideCutscene", rawArgument);
                        return true;

                    case "wait":
                        command = new VNWaitCommand
                        {
                            seconds = ParseFloatOrDefault(rawArgument, 0.5f)
                        };
                        label = BuildCommandLabel(row, "Wait", rawArgument);
                        return true;

                    case "giveartifact":
                    case "artifact":
                        command = new VNGiveArtifactCommand
                        {
                            artifactId = rawArgument,
                            dimAlpha = 0.65f,
                            fadeInSeconds = 0.2f,
                            scaleUpSeconds = 0.2f,
                            scaleSettleSeconds = 0.12f,
                            holdSeconds = 0.8f,
                            fadeOutSeconds = 0.2f
                        };
                        label = BuildCommandLabel(row, "GiveArtifact", rawArgument);
                        return true;

                    case "givecrystals":
                    case "crystals":
                    case "givecrystal":
                        command = new VNGiveCrystalsCommand
                        {
                            amount = Mathf.Max(1, ParseIntOrDefault(rawArgument, 50)),
                            playFlyAnimation = true
                        };
                        label = BuildCommandLabel(row, "GiveCrystals", rawArgument);
                        return true;

                    case "vfx":
                        command = new VNVfxCommand
                        {
                            vfxId = rawArgument,
                            anchorId = "center",
                            localOffset = Vector3.zero,
                            scale = 1f,
                            lifetimeOverride = -1f,
                            softStopSecondsOverride = -1f,
                            waitUntilFinished = false
                        };
                        label = BuildCommandLabel(row, "Vfx", rawArgument);
                        return true;

                    case "trutheye":
                    case "eye":
                        command = new VNTruthEyeCommand
                        {
                            holdSeconds = 15f,
                            failsBeforeSkip = 0,
                            allowSkipAfterFails = true,
                            finishOnFail = true,
                            driftStrength = -1f,
                            resultBoolKey = string.IsNullOrWhiteSpace(rawArgument) ? "truth_eye_win" : rawArgument
                        };
                        label = BuildCommandLabel(row, "TruthEye", rawArgument);
                        return true;

                    case "mbtianswer":
                    case "mbti":
                        command = new VNMbtiAnswerCommand
                        {
                            letter = ParseEnumOrDefault(rawArgument, VNMbtiLetter.E)
                        };
                        label = BuildCommandLabel(row, "MbtiAnswer", rawArgument);
                        return true;

                    case "resolvembti":
                        command = new VNResolveMbtiCommand();
                        label = BuildCommandLabel(row, "ResolveMbti", rawArgument);
                        return true;

                    case "setboolvar":
                    case "setbool":
                        ParseKeyValue(rawArgument, out var boolKey, out var boolRawValue);
                        command = new VNSetBoolVarCommand
                        {
                            key = boolKey,
                            value = ParseBoolOrDefault(boolRawValue, true)
                        };
                        label = BuildCommandLabel(row, "SetBoolVar", rawArgument);
                        return true;

                    case "setintvar":
                    case "setint":
                        ParseKeyValue(rawArgument, out var intKey, out var intRawValue);
                        command = new VNSetIntVarCommand
                        {
                            key = intKey,
                            value = ParseIntOrDefault(intRawValue, 0)
                        };
                        label = BuildCommandLabel(row, "SetIntVar", rawArgument);
                        return true;

                    case "addintvar":
                    case "addint":
                        ParseKeyValue(rawArgument, out var addIntKey, out var addIntRawDelta);
                        command = new VNAddIntVarCommand
                        {
                            key = addIntKey,
                            delta = ParseIntOrDefault(addIntRawDelta, 1)
                        };
                        label = BuildCommandLabel(row, "AddIntVar", rawArgument);
                        return true;

                    case "setstringvar":
                    case "setstring":
                        ParseKeyValue(rawArgument, out var stringKey, out var stringValue);
                        command = new VNSetStringVarCommand
                        {
                            key = stringKey,
                            value = stringValue
                        };
                        label = BuildCommandLabel(row, "SetStringVar", rawArgument);
                        return true;

                    case "gate":
                        command = new VNGateCommand
                        {
                            kind = ParseEnumOrDefault(rawArgument, VNGateKind.Mechanic),
                            gateStopsAuto = true,
                            gateDisablesSkip = false
                        };
                        label = BuildCommandLabel(row, "Gate", rawArgument);
                        return true;

                    case "vibration":
                    case "haptic":
                        command = new VNVibrationCommand
                        {
                            feedbackType = ParseEnumOrDefault(rawArgument, VNHapticFeedbackType.Heavy),
                            pulseCount = 1,
                            pulseIntervalSeconds = 0.08f
                        };
                        label = BuildCommandLabel(row, "Vibration", rawArgument);
                        return true;
                }

                return false;
            }

            private static VNChoiceOption CreateChoiceOption(string optionText)
            {
                var option = new VNChoiceOption
                {
                    text = optionText,
                    nextStepId = string.Empty,
                    kind = VNChoiceKind.Cosmetic,
                    premiumPrice = 0,
                    effects = new List<VNVarOp>()
                };

                TryExtractPremiumChoiceMarker(option);
                return option;
            }

            private static void TryExtractPremiumChoiceMarker(VNChoiceOption option)
            {
                if (option == null || string.IsNullOrWhiteSpace(option.text))
                    return;

                var text = option.text.Trim();
                if (!text.StartsWith("[", StringComparison.Ordinal) || !text.Contains("]"))
                    return;

                var closeIndex = text.IndexOf(']');
                if (closeIndex <= 1)
                    return;

                var marker = text.Substring(1, closeIndex - 1).Trim();
                var parts = marker.Split(':');
                if (parts.Length != 2)
                    return;

                var key = parts[0].Trim();
                if (!string.Equals(key, "premium", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(key, "paid", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(key, "crystal", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(key, "crystals", StringComparison.OrdinalIgnoreCase))
                    return;

                if (!int.TryParse(parts[1].Trim(), out var price) || price <= 0)
                    return;

                option.kind = VNChoiceKind.Premium;
                option.premiumPrice = price;
                option.text = text.Substring(closeIndex + 1).Trim();
            }

            private void AppendTerminalStep(ParseResult result, VNChapterStep step)
            {
                AddStep(step);

                if (string.IsNullOrWhiteSpace(result.firstStepId))
                    result.firstStepId = step.id;

                ConnectResultToTarget(result, step.id);

                result.terminals.Clear();
                result.pendingDirectOptions.Clear();
                result.terminals.Add(step);
            }

            private void AppendNonTerminalStep(ParseResult result, VNChapterStep step)
            {
                AddStep(step);

                if (string.IsNullOrWhiteSpace(result.firstStepId))
                    result.firstStepId = step.id;

                ConnectResultToTarget(result, step.id);

                result.terminals.Clear();
                result.pendingDirectOptions.Clear();
            }

            private void ConnectResultToTarget(ParseResult result, string targetStepId)
            {
                if (string.IsNullOrWhiteSpace(targetStepId) || result == null)
                    return;

                for (var i = 0; i < result.terminals.Count; i++)
                    SetNext(result.terminals[i], targetStepId);

                for (var i = 0; i < result.pendingDirectOptions.Count; i++)
                    result.pendingDirectOptions[i].nextStepId = targetStepId;
            }

            private static void SetNext(VNChapterStep step, string nextStepId)
            {
                if (step == null)
                    return;

                switch (step)
                {
                    case VNLineStep line:
                        line.nextStepId = nextStepId;
                        break;

                    case VNCommandStep command:
                        command.nextStepId = nextStepId;
                        break;

                    case VNJumpStep jump:
                        jump.targetStepId = nextStepId;
                        break;
                }
            }

            private void AddStep(VNChapterStep step)
            {
                if (step != null)
                    builtSteps.Add(step);
            }

            private int FindReturnRow(int parentFlowCol, int startRow, int endRowExclusive)
            {
                for (var row = startRow; row < endRowExclusive; row++)
                {
                    if (!string.IsNullOrWhiteSpace(GetCell(row, parentFlowCol)))
                        return row;
                }

                return endRowExclusive;
            }

            private List<int> CollectChoiceColumns(int row, int startCol)
            {
                var result = new List<int>();

                if (startCol < 0 || startCol >= table.columns)
                    return result;

                for (var col = startCol; col < table.columns; col++)
                {
                    var value = GetCell(row, col);

                    if (string.IsNullOrWhiteSpace(value))
                        break;

                    result.Add(col);
                }

                return result;
            }


            private string BuildCommandLabel(int row, string commandName, string argument)
            {
                var suffix = string.IsNullOrWhiteSpace(argument) ? string.Empty : $": {argument}";
                return $"R{row + 1} {commandName}{suffix}";
            }

            private static string NormalizeCommandToken(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return string.Empty;

                var normalized = NormalizeEnumToken(value);

                if (normalized.StartsWith("vn", StringComparison.Ordinal))
                    normalized = normalized.Substring(2);

                if (normalized.EndsWith("command", StringComparison.Ordinal))
                    normalized = normalized.Substring(0, normalized.Length - "command".Length);

                return normalized;
            }

            private static TEnum ParseEnumOrDefault<TEnum>(string raw, TEnum fallback) where TEnum : struct
            {
                if (TryParseEnumFlexible(raw, out TEnum parsed))
                    return parsed;

                return fallback;
            }

            private static int ParseIntOrDefault(string raw, int fallback)
            {
                raw = NormalizeText(raw);

                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                    return result;

                return fallback;
            }

            private static float ParseFloatOrDefault(string raw, float fallback)
            {
                raw = NormalizeText(raw);

                if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                    return Mathf.Max(0f, result);

                raw = raw.Replace(',', '.');

                if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                    return Mathf.Max(0f, result);

                return fallback;
            }

            private static bool ParseBoolOrDefault(string raw, bool fallback)
            {
                raw = NormalizeText(raw);

                if (bool.TryParse(raw, out var parsed))
                    return parsed;

                switch (NormalizeEnumToken(raw))
                {
                    case "1":
                    case "yes":
                    case "y":
                    case "true":
                    case "on":
                    case "да":
                    case "истина":
                        return true;

                    case "0":
                    case "no":
                    case "n":
                    case "false":
                    case "off":
                    case "нет":
                    case "ложь":
                        return false;

                    default:
                        return fallback;
                }
            }

            private static void ParseKeyValue(string raw, out string key, out string value)
            {
                raw = NormalizeText(raw);
                key = raw;
                value = string.Empty;

                if (string.IsNullOrWhiteSpace(raw))
                    return;

                var separators = new[] { '=', ':', ';', ',' };
                var separatorIndex = -1;

                for (var i = 0; i < separators.Length; i++)
                {
                    var index = raw.IndexOf(separators[i]);

                    if (index < 0)
                        continue;

                    if (separatorIndex < 0 || index < separatorIndex)
                        separatorIndex = index;
                }

                if (separatorIndex < 0)
                    return;

                key = raw.Substring(0, separatorIndex).Trim();
                value = raw.Substring(separatorIndex + 1).Trim();
            }

            private string NormalizeSpeaker(string rawSpeaker)
            {
                rawSpeaker = NormalizeText(rawSpeaker);

                if (string.IsNullOrWhiteSpace(rawSpeaker))
                    return string.Empty;

                if (owner.TryMapSpeakerAlias(rawSpeaker, out var aliasMapped))
                    rawSpeaker = aliasMapped;

                if (owner.IsNarrator(rawSpeaker))
                    return string.Empty;

                if (owner.TryResolveSpeakerFromDatabase(rawSpeaker, out var dbSpeakerId))
                    return dbSpeakerId;

                return rawSpeaker;
            }

            private VNEmotion ParseEmotion(string rawEmotion, string speakerId, int row)
            {
                rawEmotion = NormalizeText(rawEmotion);

                if (string.IsNullOrWhiteSpace(rawEmotion))
                    return VNEmotion.Neutral;

                if (IsPlayerThoughtsEmotion(rawEmotion, speakerId))
                    return VNEmotion.Thoughts;

                if (TryParseEnumFlexible(rawEmotion, out VNEmotion parsed))
                    return parsed;

                owner.Warn(
                    $"[{binding.tableKey}] Unknown emotion '{rawEmotion}' at row {row + 1}. Fallback to Neutral.");

                return VNEmotion.Neutral;
            }

            private static bool IsPlayerThoughtsEmotion(string rawEmotion, string speakerId)
            {
                return string.Equals(speakerId, "YOU", StringComparison.OrdinalIgnoreCase)
                       && NormalizeEnumToken(rawEmotion) == "thoughts";
            }

            private string GetVariantCell(int row, int col, int variantIndex, int variantCount)
            {
                var raw = GetCell(row, col);

                if (string.IsNullOrWhiteSpace(raw))
                    return string.Empty;

                var parts = raw
                    .Split(',')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (parts.Count == 0)
                    return string.Empty;

                if (variantIndex < 0 || variantCount <= 1)
                    return parts[0];

                if (parts.Count == 1)
                    return parts[0];

                if (variantIndex < parts.Count)
                    return parts[variantIndex];

                return parts[parts.Count - 1];
            }

            private string GetCell(int row, int col)
            {
                if (row < 0 || row >= table.rows)
                    return string.Empty;

                if (col < 0 || col >= table.columns)
                    return string.Empty;

                return table.Get(row, (Column)col)?.Trim() ?? string.Empty;
            }

            private string BuildLineLabel(int row, int flowCol, string speakerId, string text)
            {
                var speaker = string.IsNullOrWhiteSpace(speakerId) ? "Narrator" : speakerId;
                var preview = NormalizeText(text).Replace("\n", " ");

                if (preview.Length > 32)
                    preview = preview.Substring(0, 32) + "…";

                return $"R{row + 1} C{flowCol + 1} {speaker}: {preview}";
            }

            private string MakeStepId(string kind, int row, int col)
            {
                return $"{idPrefix}_{kind}_r{row + 1:000}_c{col + 1:00}";
            }

            private static string NormalizeText(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return string.Empty;

                return value
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n")
                    .Trim();
            }

            private static string SanitizeIdPart(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return "chapter";

                var chars = value
                    .Trim()
                    .ToLowerInvariant()
                    .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                    .ToArray();

                var result = new string(chars);

                while (result.Contains("__"))
                    result = result.Replace("__", "_");

                return result.Trim('_');
            }

            private static bool TryParseEnumFlexible<TEnum>(string raw, out TEnum value) where TEnum : struct
            {
                if (Enum.TryParse(raw, true, out value))
                    return true;

                var normalizedRaw = NormalizeEnumToken(raw);
                var names = Enum.GetNames(typeof(TEnum));

                for (var i = 0; i < names.Length; i++)
                {
                    if (NormalizeEnumToken(names[i]) == normalizedRaw)
                    {
                        value = (TEnum)Enum.Parse(typeof(TEnum), names[i], true);
                        return true;
                    }
                }

                value = default;
                return false;
            }

            private static string NormalizeId(string value)
            {
                return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            }

            private static bool ApplyTextIfChanged(
                VNChapter chapter,
                VNLineStep targetLine,
                string newText,
                ref int changed,
                ref bool undoRecorded)
            {
                if (targetLine == null)
                    return false;

                var oldText = targetLine.text ?? string.Empty;
                newText ??= string.Empty;

                if (string.Equals(oldText, newText, StringComparison.Ordinal))
                    return false;

#if UNITY_EDITOR
                if (!undoRecorded)
                {
                    Undo.RecordObject(chapter, "Update VN Line Texts From Google Sheet");
                    undoRecorded = true;
                }
#endif

                targetLine.text = newText;
                changed++;

                return true;
            }

            private static string NormalizeEnumToken(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return string.Empty;

                var chars = value
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray();

                return new string(chars);
            }
        }
    }
}