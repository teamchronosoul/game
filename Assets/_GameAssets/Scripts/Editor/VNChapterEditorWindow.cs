#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VN.Editor
{
    public class VNChapterEditorWindow : EditorWindow
    {
        private VNProjectDatabase _project;
        private VNChapter _chapter;

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        private int _selectedFilteredIndex = -1;
        private readonly List<int> _filtered = new();

        private string _search = "";
        private StepFilter _filter = StepFilter.All;

        private GUIStyle _item;
        private GUIStyle _itemSel;

        private enum StepFilter { All, Line, Choice, If, Command, Jump, End }

        [MenuItem("Tools/VN/Chapter Editor")]
        public static void Open()
        {
            var w = GetWindow<VNChapterEditorWindow>("VN Chapter Editor");
            w.minSize = new Vector2(980, 600);
            w.Show();
        }

        private void OnEnable()
        {
            if (_project == null)
            {
                var guids = AssetDatabase.FindAssets("t:VNProjectDatabase");
                if (guids != null && guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _project = AssetDatabase.LoadAssetAtPath<VNProjectDatabase>(path);
                }
            }
        }

        private void EnsureStyles()
        {
            if (_item != null) return;

            _item = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                padding = new RectOffset(6, 6, 4, 4)
            };

            _itemSel = new GUIStyle(_item);
            _itemSel.normal.textColor = Color.white;

            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, new Color(0.20f, 0.45f, 0.90f, 1f));
            tex.Apply();
            _itemSel.normal.background = tex;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawTop();
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeft();
                DrawRight();
            }
        }

        private void DrawTop()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _project = (VNProjectDatabase)EditorGUILayout.ObjectField("Project", _project, typeof(VNProjectDatabase), false, GUILayout.Width(480));
                _chapter = (VNChapter)EditorGUILayout.ObjectField("Chapter", _chapter, typeof(VNChapter), false, GUILayout.Width(480));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Gen IDs", EditorStyles.toolbarButton, GUILayout.Width(90)) && _chapter != null)
                {
                    _chapter.GenerateMissingStepIds();
                    EditorUtility.SetDirty(_chapter);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField("Search", _search);
                GUILayout.Space(10);
                _filter = (StepFilter)EditorGUILayout.EnumPopup("Filter", _filter, GUILayout.Width(280));
            }
        }

        private void DrawLeft()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(430)))
            {
                if (_chapter == null)
                {
                    EditorGUILayout.HelpBox("Assign VNChapter.", MessageType.Info);
                    return;
                }

                RebuildFiltered();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Line", GUILayout.Height(28))) AddStep(typeof(VNLineStep));
                    if (GUILayout.Button("+ Choice", GUILayout.Height(28))) AddStep(typeof(VNChoiceStep));
                    if (GUILayout.Button("+ If", GUILayout.Height(28))) AddStep(typeof(VNIfStep));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("+ Command", GUILayout.Height(28))) AddStep(typeof(VNCommandStep));
                    if (GUILayout.Button("+ Jump", GUILayout.Height(28))) AddStep(typeof(VNJumpStep));
                    if (GUILayout.Button("+ End", GUILayout.Height(28))) AddStep(typeof(VNEndStep));
                }

                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

                for (int fi = 0; fi < _filtered.Count; fi++)
                {
                    int si = _filtered[fi];
                    var s = _chapter.steps[si];
                    string title = BuildTitle(si, s);

                    var style = (fi == _selectedFilteredIndex) ? _itemSel : _item;
                    Rect r = EditorGUILayout.GetControlRect(false, 24);

                    if (GUI.Button(r, title, style))
                    {
                        _selectedFilteredIndex = fi;
                        GUI.FocusControl(null);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawRight()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_chapter == null) return;
                if (_selectedFilteredIndex < 0 || _selectedFilteredIndex >= _filtered.Count)
                {
                    EditorGUILayout.HelpBox("Select a step.", MessageType.Info);
                    return;
                }

                int stepIndex = _filtered[_selectedFilteredIndex];
                if (stepIndex < 0 || stepIndex >= _chapter.steps.Count || _chapter.steps[stepIndex] == null)
                    return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Up", GUILayout.Width(60))) Move(stepIndex, -1);
                    if (GUILayout.Button("Down", GUILayout.Width(60))) Move(stepIndex, +1);
                    if (GUILayout.Button("Delete", GUILayout.Width(80))) Delete(stepIndex);

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"Index: {stepIndex}", GUILayout.Width(120));
                }

                EditorGUILayout.Space(6);
                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

                DrawStepInspector(stepIndex);

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawStepInspector(int stepIndex)
        {
            var so = new SerializedObject(_chapter);
            var stepsProp = so.FindProperty("steps");
            var stepProp = stepsProp.GetArrayElementAtIndex(stepIndex);

            var runtime = _chapter.steps[stepIndex];

            EditorGUILayout.LabelField(runtime.GetType().Name, EditorStyles.boldLabel);

            DrawProp(stepProp, "id");
            DrawProp(stepProp, "label");
            DrawProp(stepProp, "disableSkip");
            DrawProp(stepProp, "stopAuto");

            EditorGUILayout.Space(8);

            if (runtime is VNLineStep)
            {
                DrawSpeakerDropdown(stepProp.FindPropertyRelative("speakerId"));
                DrawProp(stepProp, "pose");
                DrawProp(stepProp, "emotion");
                DrawSfxDropdown(stepProp.FindPropertyRelative("sfxId"));
                DrawProp(stepProp, "text");
                DrawProp(stepProp, "addToLog");
            }
            else if (runtime is VNChoiceStep)
            {
                DrawChoice(stepProp, runtime.id);
            }
            else if (runtime is VNIfStep)
            {
                DrawIf(stepProp, runtime.id);
            }
            else if (runtime is VNJumpStep)
            {
                DrawStepIdDropdown("Target", stepProp.FindPropertyRelative("targetStepId"), runtime.id, allowEmptyLinear: false);
            }
            else if (runtime is VNCommandStep)
            {
                DrawCommand(stepProp);
            }
            else if (runtime is VNEndStep)
            {
                EditorGUILayout.HelpBox("EndStep – конец главы.", MessageType.None);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_chapter);
        }

        private void DrawChoice(SerializedProperty stepProp, string selfId)
        {
            var optionsProp = stepProp.FindPropertyRelative("options");

            if (GUILayout.Button("+ Option", GUILayout.Width(110)))
                optionsProp.arraySize += 1;

            EditorGUILayout.Space(6);

            for (int i = 0; i < optionsProp.arraySize; i++)
            {
                var opt = optionsProp.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField($"Option #{i + 1}", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(opt.FindPropertyRelative("text"));

                    DrawStepIdDropdown("Next", opt.FindPropertyRelative("nextStepId"), selfId, allowEmptyLinear: false);

                    EditorGUILayout.PropertyField(opt.FindPropertyRelative("kind"));
                    EditorGUILayout.PropertyField(opt.FindPropertyRelative("premiumPrice"));
                    EditorGUILayout.PropertyField(opt.FindPropertyRelative("effects"), true);

                    if (GUILayout.Button("Delete Option", GUILayout.Width(130)))
                    {
                        optionsProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }
        }

        private void DrawIf(SerializedProperty stepProp, string selfId)
        {
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("requireAll"));

            var condProp = stepProp.FindPropertyRelative("conditions");
            if (GUILayout.Button("+ Condition", GUILayout.Width(120)))
                condProp.arraySize += 1;

            EditorGUILayout.Space(6);

            for (int i = 0; i < condProp.arraySize; i++)
            {
                var c = condProp.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField($"Condition #{i + 1}", EditorStyles.boldLabel);

                    EditorGUILayout.PropertyField(c.FindPropertyRelative("type"));
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("key"));
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("op"));

                    var type = (VNConditionValueType)c.FindPropertyRelative("type").enumValueIndex;
                    if (type == VNConditionValueType.Bool) EditorGUILayout.PropertyField(c.FindPropertyRelative("boolValue"), new GUIContent("Value"));
                    if (type == VNConditionValueType.Int) EditorGUILayout.PropertyField(c.FindPropertyRelative("intValue"), new GUIContent("Value"));
                    if (type == VNConditionValueType.String) EditorGUILayout.PropertyField(c.FindPropertyRelative("stringValue"), new GUIContent("Value"));

                    if (GUILayout.Button("Delete Condition", GUILayout.Width(140)))
                    {
                        condProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }

            EditorGUILayout.Space(8);
            DrawStepIdDropdown("True ->", stepProp.FindPropertyRelative("trueStepId"), selfId, allowEmptyLinear: true);
            DrawStepIdDropdown("False ->", stepProp.FindPropertyRelative("falseStepId"), selfId, allowEmptyLinear: true);
        }

        private void DrawCommand(SerializedProperty stepProp)
        {
            var cmdProp = stepProp.FindPropertyRelative("command");

            DrawCommandTypePicker(cmdProp);

            EditorGUILayout.Space(6);

            if (cmdProp.managedReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Pick a command type.", MessageType.Info);
                return;
            }

            // Рисуем поля команды, но для ID добавим dropdown ниже (если есть project)
            EditorGUILayout.PropertyField(cmdProp, true);

            if (_project == null || _project.assetDatabase == null || _project.characterDatabase == null) return;

            var runtime = _chapter.steps[_filtered[_selectedFilteredIndex]] as VNCommandStep;
            if (runtime == null || runtime.command == null) return;

            if (runtime.command is VNSetBackgroundCommand)
                DrawBackgroundDropdown(cmdProp.FindPropertyRelative("backgroundId"));

            if (runtime.command is VNPlayMusicCommand)
                DrawMusicDropdown(cmdProp.FindPropertyRelative("musicId"));

            if (runtime.command is VNPlaySfxCommand)
                DrawSfxDropdown(cmdProp.FindPropertyRelative("sfxId"));

            if (runtime.command is VNShowCharacterCommand)
                DrawCharacterDropdown(cmdProp.FindPropertyRelative("characterId"));
        }

        // -------- Dropdowns --------

        private void DrawSpeakerDropdown(SerializedProperty speakerIdProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(speakerIdProp, new GUIContent("SpeakerId"));

                if (_project?.characterDatabase == null) return;

                if (GUILayout.Button("▼", GUILayout.Width(28)))
                {
                    var ids = _project.characterDatabase.Characters
                        .Select(c => (c?.id ?? "").Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct().OrderBy(s => s).ToList();

                    ids.Insert(0, ""); // narrator

                    var m = new GenericMenu();
                    foreach (var id in ids)
                    {
                        string label = string.IsNullOrEmpty(id) ? "(Narrator)" : id;
                        m.AddItem(new GUIContent(label), speakerIdProp.stringValue == id, () =>
                        {
                            speakerIdProp.stringValue = id;
                            speakerIdProp.serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_chapter);
                        });
                    }
                    m.ShowAsContext();
                }
            }
        }

        private void DrawCharacterDropdown(SerializedProperty idProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp);

                if (_project?.characterDatabase == null) return;
                if (!GUILayout.Button("▼", GUILayout.Width(28))) return;

                var ids = _project.characterDatabase.Characters
                    .Select(c => (c?.id ?? "").Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct().OrderBy(s => s).ToList();

                var m = new GenericMenu();
                foreach (var id in ids)
                {
                    m.AddItem(new GUIContent(id), idProp.stringValue == id, () =>
                    {
                        idProp.stringValue = id;
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                }
                m.ShowAsContext();
            }
        }

        private void DrawBackgroundDropdown(SerializedProperty idProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent("backgroundId"));

                if (_project?.assetDatabase == null) return;
                if (!GUILayout.Button("▼", GUILayout.Width(28))) return;

                var ids = _project.assetDatabase.backgrounds
                    .Select(e => (e?.id ?? "").Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct().OrderBy(s => s).ToList();

                var m = new GenericMenu();
                foreach (var id in ids)
                {
                    m.AddItem(new GUIContent(id), idProp.stringValue == id, () =>
                    {
                        idProp.stringValue = id;
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                }
                m.ShowAsContext();
            }
        }

        private void DrawMusicDropdown(SerializedProperty idProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent("musicId"));

                if (_project?.assetDatabase == null) return;
                if (!GUILayout.Button("▼", GUILayout.Width(28))) return;

                var ids = _project.assetDatabase.music
                    .Select(e => (e?.id ?? "").Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct().OrderBy(s => s).ToList();

                var m = new GenericMenu();
                foreach (var id in ids)
                {
                    m.AddItem(new GUIContent(id), idProp.stringValue == id, () =>
                    {
                        idProp.stringValue = id;
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                }
                m.ShowAsContext();
            }
        }

        private void DrawSfxDropdown(SerializedProperty idProp)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent(idProp.displayName));

                if (_project?.assetDatabase == null) return;
                if (!GUILayout.Button("▼", GUILayout.Width(28))) return;

                var ids = _project.assetDatabase.sfx
                    .Select(e => (e?.id ?? "").Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct().OrderBy(s => s).ToList();

                ids.Insert(0, "");

                var m = new GenericMenu();
                foreach (var id in ids)
                {
                    string label = string.IsNullOrEmpty(id) ? "(None)" : id;
                    m.AddItem(new GUIContent(label), idProp.stringValue == id, () =>
                    {
                        idProp.stringValue = id;
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                }
                m.ShowAsContext();
            }
        }

        private void DrawStepIdDropdown(string caption, SerializedProperty idProp, string excludeSelfId, bool allowEmptyLinear)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(idProp, new GUIContent(caption));

                if (!GUILayout.Button("▼", GUILayout.Width(28))) return;

                var refs = BuildStepRefs(excludeSelfId);

                var m = new GenericMenu();

                if (allowEmptyLinear)
                {
                    m.AddItem(new GUIContent("(Next by order)"), string.IsNullOrEmpty(idProp.stringValue), () =>
                    {
                        idProp.stringValue = "";
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                    m.AddSeparator("");
                }

                foreach (var r in refs)
                {
                    m.AddItem(new GUIContent(r), idProp.stringValue == ExtractId(r), () =>
                    {
                        idProp.stringValue = ExtractId(r);
                        idProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });
                }

                m.ShowAsContext();
            }
        }

        private List<string> BuildStepRefs(string excludeSelfId)
        {
            var list = new List<string>();
            if (_chapter?.steps == null) return list;

            for (int i = 0; i < _chapter.steps.Count; i++)
            {
                var s = _chapter.steps[i];
                if (s == null || string.IsNullOrWhiteSpace(s.id)) continue;
                if (!string.IsNullOrEmpty(excludeSelfId) && s.id == excludeSelfId) continue;

                string label = string.IsNullOrWhiteSpace(s.label) ? ShortPreview(s) : s.label.Trim();
                string id6 = s.id.Length >= 6 ? s.id.Substring(0, 6) : s.id;
                list.Add($"{i:000}  {label}  ({id6}|{s.id})");
            }

            return list;
        }

        private static string ExtractId(string refLabel)
        {
            int p = refLabel.LastIndexOf('|');
            int e = refLabel.LastIndexOf(')');
            if (p < 0 || e < 0 || e <= p) return "";
            return refLabel.Substring(p + 1, e - p - 1);
        }

        private void DrawCommandTypePicker(SerializedProperty cmdProp)
        {
            Type current = cmdProp.managedReferenceValue?.GetType();

            var types = TypeCache.GetTypesDerivedFrom<VNCommand>()
                .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToList();

            string currentName = current != null ? current.Name : "(None)";

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Command Type", GUILayout.Width(110));
                EditorGUILayout.LabelField(currentName);

                if (GUILayout.Button("Change", GUILayout.Width(80)))
                {
                    var m = new GenericMenu();

                    m.AddItem(new GUIContent("(None)"), current == null, () =>
                    {
                        cmdProp.managedReferenceValue = null;
                        cmdProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_chapter);
                    });

                    m.AddSeparator("");

                    foreach (var t in types)
                    {
                        var captured = t;
                        m.AddItem(new GUIContent(captured.Name), current == captured, () =>
                        {
                            cmdProp.managedReferenceValue = Activator.CreateInstance(captured);
                            cmdProp.serializedObject.ApplyModifiedProperties();
                            EditorUtility.SetDirty(_chapter);
                        });
                    }

                    m.ShowAsContext();
                }
            }
        }

        // -------- Step list helpers --------

        private void RebuildFiltered()
        {
            _filtered.Clear();
            if (_chapter?.steps == null) return;

            string q = (_search ?? "").Trim().ToLowerInvariant();

            for (int i = 0; i < _chapter.steps.Count; i++)
            {
                var s = _chapter.steps[i];
                if (s == null) continue;

                if (!PassFilter(s)) continue;

                if (!string.IsNullOrEmpty(q))
                {
                    string hay = $"{s.id} {s.label} {ShortPreview(s)}".ToLowerInvariant();
                    if (!hay.Contains(q)) continue;
                }

                _filtered.Add(i);
            }

            if (_selectedFilteredIndex >= _filtered.Count)
                _selectedFilteredIndex = _filtered.Count - 1;
        }

        private bool PassFilter(VNChapterStep s)
        {
            return _filter switch
            {
                StepFilter.All => true,
                StepFilter.Line => s is VNLineStep,
                StepFilter.Choice => s is VNChoiceStep,
                StepFilter.If => s is VNIfStep,
                StepFilter.Command => s is VNCommandStep,
                StepFilter.Jump => s is VNJumpStep,
                StepFilter.End => s is VNEndStep,
                _ => true
            };
        }

        private string BuildTitle(int index, VNChapterStep s)
        {
            string type = s.GetType().Name.Replace("VN", "").Replace("Step", "");
            string label = string.IsNullOrWhiteSpace(s.label) ? "" : $" — <b>{Escape(s.label.Trim())}</b>";
            string preview = Escape(ShortPreview(s));
            string id6 = (!string.IsNullOrWhiteSpace(s.id) && s.id.Length >= 6) ? s.id.Substring(0, 6) : "------";
            return $"{index:000}  [{type}]  {preview}{label}   <color=#888>({id6})</color>";
        }

        private static string ShortPreview(VNChapterStep s)
        {
            switch (s)
            {
                case VNLineStep l:
                    return Trim($"{(string.IsNullOrWhiteSpace(l.speakerId) ? "Narrator" : l.speakerId)}: {l.text}");
                case VNChoiceStep c:
                    return $"Options: {c.options?.Count ?? 0}";
                case VNIfStep iff:
                    return $"If ({(iff.requireAll ? "AND" : "OR")}): {iff.conditions?.Count ?? 0}";
                case VNCommandStep cmd:
                    return cmd.command != null ? $"Cmd: {cmd.command.GetType().Name.Replace("VN", "").Replace("Command", "")}" : "Cmd: (null)";
                case VNJumpStep j:
                    return $"Jump -> {(string.IsNullOrWhiteSpace(j.targetStepId) ? "(empty)" : j.targetStepId.Substring(0, Mathf.Min(6, j.targetStepId.Length)))}";
                case VNEndStep:
                    return "END";
            }
            return s.GetType().Name;
        }

        private static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ").Replace("\r", " ");
            return s.Length > 70 ? s.Substring(0, 70) + "…" : s;
        }

        private static string Escape(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace("<", "‹").Replace(">", "›");

        private void AddStep(Type type)
        {
            if (_chapter == null) return;

            var so = new SerializedObject(_chapter);
            var stepsProp = so.FindProperty("steps");

            int idx = stepsProp.arraySize;
            stepsProp.arraySize += 1;

            var elem = stepsProp.GetArrayElementAtIndex(idx);
            elem.managedReferenceValue = Activator.CreateInstance(type);

            elem.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString("N");
            elem.FindPropertyRelative("label").stringValue = "";

            if (type == typeof(VNCommandStep))
                elem.FindPropertyRelative("command").managedReferenceValue = new VNSetBackgroundCommand();

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_chapter);

            RebuildFiltered();
            _selectedFilteredIndex = _filtered.IndexOf(idx);
        }

        private void Move(int stepIndex, int delta)
        {
            int t = stepIndex + delta;
            if (_chapter == null) return;
            if (t < 0 || t >= _chapter.steps.Count) return;

            Undo.RecordObject(_chapter, "Move VN Step");
            (_chapter.steps[stepIndex], _chapter.steps[t]) = (_chapter.steps[t], _chapter.steps[stepIndex]);
            EditorUtility.SetDirty(_chapter);

            RebuildFiltered();
        }

        private void Delete(int stepIndex)
        {
            if (_chapter == null) return;
            if (stepIndex < 0 || stepIndex >= _chapter.steps.Count) return;

            Undo.RecordObject(_chapter, "Delete VN Step");
            _chapter.steps.RemoveAt(stepIndex);
            EditorUtility.SetDirty(_chapter);

            _selectedFilteredIndex = -1;
            RebuildFiltered();
        }

        private static void DrawProp(SerializedProperty root, string name)
        {
            var p = root.FindPropertyRelative(name);
            if (p != null) EditorGUILayout.PropertyField(p, true);
        }
    }
}
#endif