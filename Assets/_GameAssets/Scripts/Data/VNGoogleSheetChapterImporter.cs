using System;
using System.Collections.Generic;
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

        [Serializable]
        public class ChapterBinding
        {
            [Header("Source")]
            public string tableKey;

            [Header("Target")]
            public VNChapter chapter;
            public string chapterIdOverride;

            [Header("Sheet Layout")]
            [Min(1)] public int firstDataRow = 2;
            public Column speakerColumn = Column.A;
            public Column emotionColumn = Column.B;
            public Column flowColumn = Column.C;
            public Column firstChoiceColumn = Column.D;

            [Header("Build")]
            public bool appendEndStep = true;
        }

        [Header("Import Bindings")]
        [SerializeField] private List<ChapterBinding> chapters = new();

        [Header("Speaker Mapping")]
        [SerializeField] private List<SpeakerAlias> speakerAliases = new();

        [Header("Narrator aliases -> empty speakerId")]
        [SerializeField] private List<string> narratorAliases = new()
        {
            "Narrator",
            "narrator",
            "Нарратор",
            "Автор",
            "-"
        };

        [Header("Character Database")]
        [SerializeField] private VNCharacterDatabase characterDatabase;
        [SerializeField] private bool autoFindCharacterDatabasesInProject = true;
        [SerializeField] private bool matchSpeakerByCharacterId = true;
        [SerializeField] private bool matchSpeakerByDisplayName = true;

        [Header("Debug")]
        [SerializeField] private bool logWarnings = true;

        private readonly Dictionary<string, string> _speakerIdLookup = new(StringComparer.OrdinalIgnoreCase);
        private bool _speakerLookupBuilt;

        public override void LoadData(Dictionary<string, Table> allTables)
        {
            EnsureSpeakerLookup();

            if (chapters == null || chapters.Count == 0)
                return;

            foreach (var binding in chapters)
            {
                if (binding == null)
                    continue;

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

                var builder = new Builder(this, binding, table);
                builder.BuildInto(binding.chapter);
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
                string[] guids = AssetDatabase.FindAssets("t:VNCharacterDatabase");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
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

                string id = NormalizeLookupKey(ch.id);
                string displayName = NormalizeLookupKey(ch.displayName);

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

            string key = NormalizeLookupKey(rawValue);
            if (string.IsNullOrWhiteSpace(key))
                return false;

            return _speakerIdLookup.TryGetValue(key, out speakerId) && !string.IsNullOrWhiteSpace(speakerId);
        }

        private bool TryMapSpeakerAlias(string rawValue, out string mapped)
        {
            mapped = rawValue;

            if (speakerAliases == null)
                return false;

            for (int i = 0; i < speakerAliases.Count; i++)
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

            for (int i = 0; i < narratorAliases.Count; i++)
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

                int startRow = Mathf.Max(0, binding.firstDataRow - 1);
                int flowCol = (int)binding.flowColumn;
                int choiceStartCol = (int)binding.firstChoiceColumn;

                var root = ParseFlow(
                    flowCol: flowCol,
                    startRow: startRow,
                    endRowExclusive: table.rows,
                    variantIndex: -1,
                    variantCount: 0,
                    choiceStartCol: choiceStartCol);

                bool needEnd =
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

            private ParseResult ParseFlow(
                int flowCol,
                int startRow,
                int endRowExclusive,
                int variantIndex,
                int variantCount,
                int choiceStartCol)
            {
                var result = new ParseResult();
                int row = startRow;

                while (row < endRowExclusive)
                {
                    string flowText = GetCell(row, flowCol);
                    List<int> choiceColumns = CollectChoiceColumns(row, choiceStartCol);

                    bool hasText = !string.IsNullOrWhiteSpace(flowText);
                    bool hasChoice = choiceColumns.Count > 0;

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

                        int branchStartRow = row + 1;
                        int returnRow = FindReturnRow(flowCol, branchStartRow, endRowExclusive);
                        int nestedChoiceStartCol = choiceStartCol + choiceColumns.Count;

                        var continuation = ParseFlow(
                            flowCol: flowCol,
                            startRow: returnRow,
                            endRowExclusive: endRowExclusive,
                            variantIndex: variantIndex,
                            variantCount: variantCount,
                            choiceStartCol: choiceStartCol);

                        var unresolvedAfterChoice = new ParseResult();

                        for (int optionIndex = 0; optionIndex < choiceColumns.Count; optionIndex++)
                        {
                            int branchCol = choiceColumns[optionIndex];
                            var option = choice.options[optionIndex];

                            var branch = ParseFlow(
                                flowCol: branchCol,
                                startRow: branchStartRow,
                                endRowExclusive: returnRow,
                                variantIndex: optionIndex,
                                variantCount: choiceColumns.Count,
                                choiceStartCol: nestedChoiceStartCol);

                            if (!string.IsNullOrWhiteSpace(branch.firstStepId))
                            {
                                option.nextStepId = branch.firstStepId;
                            }
                            else if (!string.IsNullOrWhiteSpace(continuation.firstStepId))
                            {
                                option.nextStepId = continuation.firstStepId;
                            }
                            else
                            {
                                unresolvedAfterChoice.pendingDirectOptions.Add(option);
                            }

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

            private VNLineStep CreateLineStep(int row, int flowCol, string text, int variantIndex, int variantCount)
            {
                string rawSpeaker = GetVariantCell(row, (int)binding.speakerColumn, variantIndex, variantCount);
                string rawEmotion = GetVariantCell(row, (int)binding.emotionColumn, variantIndex, variantCount);

                string speakerId = NormalizeSpeaker(rawSpeaker);
                VNEmotion emotion = ParseEmotion(rawEmotion, row);

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

                for (int i = 0; i < choiceColumns.Count; i++)
                {
                    int col = choiceColumns[i];
                    string optionText = NormalizeText(GetCell(row, col));

                    step.options.Add(new VNChoiceOption
                    {
                        text = optionText,
                        nextStepId = string.Empty,
                        kind = VNChoiceKind.Cosmetic,
                        premiumPrice = 0,
                        effects = new List<VNVarOp>()
                    });
                }

                return step;
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

                for (int i = 0; i < result.terminals.Count; i++)
                    SetNext(result.terminals[i], targetStepId);

                for (int i = 0; i < result.pendingDirectOptions.Count; i++)
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
                for (int row = startRow; row < endRowExclusive; row++)
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

                for (int col = startCol; col < table.columns; col++)
                {
                    string value = GetCell(row, col);

                    if (string.IsNullOrWhiteSpace(value))
                        break;

                    result.Add(col);
                }

                return result;
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

            private VNEmotion ParseEmotion(string rawEmotion, int row)
            {
                rawEmotion = NormalizeText(rawEmotion);

                if (string.IsNullOrWhiteSpace(rawEmotion))
                    return VNEmotion.Neutral;

                if (TryParseEnumFlexible(rawEmotion, out VNEmotion parsed))
                    return parsed;

                owner.Warn(
                    $"[{binding.tableKey}] Unknown emotion '{rawEmotion}' at row {row + 1}. Fallback to Neutral.");

                return VNEmotion.Neutral;
            }

            private string GetVariantCell(int row, int col, int variantIndex, int variantCount)
            {
                string raw = GetCell(row, col);
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
                string speaker = string.IsNullOrWhiteSpace(speakerId) ? "Narrator" : speakerId;
                string preview = NormalizeText(text).Replace("\n", " ");

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

                string result = new string(chars);

                while (result.Contains("__"))
                    result = result.Replace("__", "_");

                return result.Trim('_');
            }

            private static bool TryParseEnumFlexible<TEnum>(string raw, out TEnum value) where TEnum : struct
            {
                if (Enum.TryParse(raw, true, out value))
                    return true;

                string normalizedRaw = NormalizeEnumToken(raw);
                var names = Enum.GetNames(typeof(TEnum));

                for (int i = 0; i < names.Length; i++)
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