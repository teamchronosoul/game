using System;
using System.Collections;
using UnityEngine;

namespace VN
{
    public class VNRunner : MonoBehaviour
    {
        [Header("Project")]
        [SerializeField] private VNProjectDatabase project;

        [Header("Startup")]
        [SerializeField] private string startChapterId = "chapter_01";
        [SerializeField] private bool autoLoadAutosaveOnStart = true;
        [SerializeField] private bool autoShowSpeakerIfMissing = true;

        [Header("Auto timing (delay after line reveal)")]
        [SerializeField] private float autoBaseDelaySeconds = 0.8f;
        [SerializeField] private float autoPerCharacterSeconds = 0.03f;
        [SerializeField] private float autoPunctuationExtraSeconds = 0.25f;

        [Header("Skip")]
        [SerializeField] private float skipStepFrameDelay = 0f; // 0 = следующий кадр, можно 0.01f для визуального "пульса"

        public bool AutoEnabled { get; private set; }
        public bool SkipEnabled { get; private set; }
        public bool SkipAllowed { get; private set; } = true;

        public VNState State => _state;

        // --- Events (под uGUI) ---
        public event Action<VNLinePayload> OnLineStarted;
        public event Action OnRequestInstantReveal;
        public event Action OnLineHidden;

        public event Action<VNChoicePayload> OnChoicePresented;
        public event Action OnChoiceHidden;

        public event Action<VNBackgroundPayload> OnBackgroundChanged;
        public event Action<VNSlotPayload> OnSlotChanged;

        public event Action<VNMusicPayload> OnMusicPlay;
        public event Action<float> OnMusicStop;
        public event Action<AudioClip> OnSfxPlay;

        public event Action<bool> OnAutoChanged;
        public event Action<bool> OnSkipChanged;
        public event Action<bool> OnSkipAllowedChanged;

        public event Action<Vector2> OnTapFeedback;

        [Serializable]
        public struct VNLinePayload
        {
            public string speakerId;
            public string speakerName;
            public bool isNarrator;

            public VNPose pose;
            public VNEmotion emotion;

            public string sfxId;
            public string text;
        }

        [Serializable]
        public struct VNChoicePayload
        {
            public string stepId;
            public VNChoiceOption[] options;
        }

        [Serializable]
        public struct VNBackgroundPayload
        {
            public string backgroundId;
            public Sprite sprite;
            public float crossfadeSeconds;
        }

        [Serializable]
        public struct VNSlotPayload
        {
            public VNScreenSlot slot;
            public bool visible;

            public string characterId;
            public string characterName;

            public VNPose pose;
            public VNEmotion emotion;

            public Sprite sprite;
            public float crossfadeSeconds;

            public bool isNewCharacter;
        }

        [Serializable]
        public struct VNMusicPayload
        {
            public string musicId;
            public AudioClip clip;
            public float fadeInSeconds;
            public bool loop;
        }

        private VNState _state = new();

        private Coroutine _loop;

        private bool _lineRevealCompleted;
        private bool _advanceRequested;
        private bool _interruptWaitRequested;

        private bool _choiceWaiting;
        private VNChoiceStep _currentChoiceStep;

        private bool _autoStopDueToNewCharacterThisStep;

        private void Awake()
        {
            _state.EnsureSlots();
        }

        private void Start()
        {
            if (project == null) return;

            if (autoLoadAutosaveOnStart && VNAutosave.TryLoad(out var loaded))
            {
                _state = loaded;
                ResumeFromState();
            }
            else
            {
                StartNew(startChapterId);
            }
        }

        // -------- Public API --------

        public void StartNew(string chapterId)
        {
            StopInternal();

            _state.ResetAll();
            _state.chapterId = chapterId;
            _state.currentStepApplied = false;

            AutoEnabled = false;
            SkipEnabled = false;
            SkipAllowed = true;

            if (!TryResolveChapter(out var ch)) return;
            if (ch.steps == null || ch.steps.Count == 0) return;

            var first = ch.steps[0];
            if (first == null || string.IsNullOrWhiteSpace(first.id)) return;

            _state.stepId = first.id;
            _state.currentStepApplied = false;
            VNAutosave.Save(_state);

            _loop = StartCoroutine(MainLoop());
        }

        public void ResumeFromState()
        {
            StopInternal();

            if (!TryResolveChapter(out _)) return;
            if (string.IsNullOrWhiteSpace(_state.stepId)) return;

            EmitFullScreenState();
            _loop = StartCoroutine(MainLoop());
        }

        public void DeleteAutosaveAndRestart()
        {
            VNAutosave.Delete();
            StartNew(startChapterId);
        }

        public void Tap(Vector2 screenPosition)
        {
            OnTapFeedback?.Invoke(screenPosition);

            // Любое взаимодействие игрока выключает Auto/Skip
            if (AutoEnabled) SetAuto(false);
            if (SkipEnabled) SetSkip(false);

            if (_choiceWaiting) return;

            if (!_lineRevealCompleted)
            {
                OnRequestInstantReveal?.Invoke();
                return;
            }

            _advanceRequested = true;
            _interruptWaitRequested = true;
        }

        public void SetAuto(bool enabled)
        {
            if (AutoEnabled == enabled) return;
            AutoEnabled = enabled;
            OnAutoChanged?.Invoke(AutoEnabled);
        }

        public void SetSkip(bool enabled)
        {
            if (enabled && !SkipAllowed) enabled = false;
            if (SkipEnabled == enabled) return;
            SkipEnabled = enabled;
            OnSkipChanged?.Invoke(SkipEnabled);
        }

        public void NotifyLineRevealFinished() => _lineRevealCompleted = true;

        public void Choose(int optionIndex)
        {
            if (!_choiceWaiting || _currentChoiceStep == null) return;
            if (_currentChoiceStep.options == null) return;
            if (optionIndex < 0 || optionIndex >= _currentChoiceStep.options.Count) return;

            var opt = _currentChoiceStep.options[optionIndex];

            if (opt.effects != null)
                for (int i = 0; i < opt.effects.Count; i++)
                    ApplyVarOp(opt.effects[i]);

            var next = Norm(opt.nextStepId);
            if (string.IsNullOrEmpty(next)) return;

            _choiceWaiting = false;
            _currentChoiceStep = null;
            OnChoiceHidden?.Invoke();

            _state.stepId = next;
            _state.currentStepApplied = false;
            VNAutosave.Save(_state);
        }

        // -------- Loop --------

        private IEnumerator MainLoop()
        {
            while (true)
            {
                if (!TryResolveChapter(out var chapter)) yield break;
                if (!chapter.TryGetStepIndex(_state.stepId, out int index)) yield break;

                var step = chapter.GetStepAt(index);
                if (step == null) yield break;

                ApplySkipAllowedForStep(step);
                if (SkipEnabled && !SkipAllowed) SetSkip(false);

                if (step is VNIfStep iff)
                {
                    yield return HandleIfStep(iff);
                    continue;
                }

                if (step is VNLineStep line)
                {
                    yield return HandleLineStep(line);
                    continue;
                }

                if (step is VNChoiceStep choice)
                {
                    yield return HandleChoiceStep(choice);
                    continue;
                }

                if (step is VNCommandStep cmdStep)
                {
                    yield return HandleCommandStep(cmdStep);
                    continue;
                }

                if (step is VNJumpStep jump)
                {
                    yield return HandleJumpStep(jump);
                    continue;
                }

                if (step is VNEndStep)
                    yield break;

                yield break;
            }
        }

        private IEnumerator HandleIfStep(VNIfStep iff)
        {
            bool result = EvaluateConditions(iff);
            string target = result ? Norm(iff.trueStepId) : Norm(iff.falseStepId);

            if (!string.IsNullOrWhiteSpace(target))
            {
                _state.stepId = target;
                _state.currentStepApplied = false;
                VNAutosave.Save(_state);
                yield return null;
                yield break;
            }

            AdvanceToNextStep();
            yield return null;
        }

        private bool EvaluateConditions(VNIfStep iff)
        {
            if (iff.conditions == null || iff.conditions.Count == 0) return true;

            bool requireAll = iff.requireAll;

            bool anyTrue = false;
            for (int i = 0; i < iff.conditions.Count; i++)
            {
                var c = iff.conditions[i];
                if (c == null) continue;

                bool ok = EvaluateSingleCondition(c);

                if (requireAll && !ok) return false;
                if (!requireAll && ok) return true;

                anyTrue |= ok;
            }

            return requireAll ? true : anyTrue;
        }

        private bool EvaluateSingleCondition(VNCondition c)
        {
            string key = Norm(c.key);

            switch (c.type)
            {
                case VNConditionValueType.Bool:
                {
                    bool v = _state.GetBool(key, false);
                    bool t = c.boolValue;
                    return c.op switch
                    {
                        VNConditionOp.Equals => v == t,
                        VNConditionOp.NotEquals => v != t,
                        _ => false
                    };
                }
                case VNConditionValueType.Int:
                {
                    int v = _state.GetInt(key, 0);
                    int t = c.intValue;
                    return c.op switch
                    {
                        VNConditionOp.Equals => v == t,
                        VNConditionOp.NotEquals => v != t,
                        VNConditionOp.Greater => v > t,
                        VNConditionOp.GreaterOrEqual => v >= t,
                        VNConditionOp.Less => v < t,
                        VNConditionOp.LessOrEqual => v <= t,
                        _ => false
                    };
                }
                case VNConditionValueType.String:
                {
                    string v = _state.GetString(key, "");
                    string t = c.stringValue ?? "";
                    return c.op switch
                    {
                        VNConditionOp.Equals => string.Equals(v, t, StringComparison.Ordinal),
                        VNConditionOp.NotEquals => !string.Equals(v, t, StringComparison.Ordinal),
                        VNConditionOp.Contains => !string.IsNullOrEmpty(t) && (v?.IndexOf(t, StringComparison.Ordinal) ?? -1) >= 0,
                        VNConditionOp.NotContains => string.IsNullOrEmpty(t) || (v?.IndexOf(t, StringComparison.Ordinal) ?? -1) < 0,
                        _ => false
                    };
                }
            }

            return false;
        }

        private IEnumerator HandleLineStep(VNLineStep line)
        {
            _advanceRequested = false;
            _interruptWaitRequested = false;
            _autoStopDueToNewCharacterThisStep = false;

            if (!_state.currentStepApplied)
            {
                if (!string.IsNullOrWhiteSpace(line.speakerId))
                    AutoApplySpeakerPoseEmotion(Norm(line.speakerId), line.pose, line.emotion);

                if (line.addToLog)
                    AddToLog(line);

                if (!string.IsNullOrWhiteSpace(line.sfxId))
                    EmitSfx(Norm(line.sfxId));

                _state.currentStepApplied = true;
                VNAutosave.Save(_state);
            }

            var payload = BuildLinePayload(line);
            OnLineStarted?.Invoke(payload);

            _lineRevealCompleted = false;

            // Skip: сам прогоняет, без кликов
            if (SkipEnabled && SkipAllowed)
            {
                OnRequestInstantReveal?.Invoke();
                _lineRevealCompleted = true;

                if (skipStepFrameDelay <= 0f) yield return null;
                else yield return new WaitForSeconds(skipStepFrameDelay);

                AdvanceToNextStep();
                OnLineHidden?.Invoke();
                yield break;
            }

            // Manual / Auto
            bool stopAutoHere = IsStopAutoHere(line) || _autoStopDueToNewCharacterThisStep;

            if (AutoEnabled && !stopAutoHere)
            {
                yield return new WaitUntil(() => _lineRevealCompleted);

                float delay = ComputeAutoDelay(payload.text);
                yield return WaitAutoOrUserInterrupt(delay);

                if (AutoEnabled) _advanceRequested = true;
            }
            else
            {
                while (!_advanceRequested)
                    yield return null;
            }

            if (_advanceRequested && !_lineRevealCompleted)
            {
                OnRequestInstantReveal?.Invoke();
                yield return new WaitUntil(() => _lineRevealCompleted);
            }

            AdvanceToNextStep();
            OnLineHidden?.Invoke();
        }

        private IEnumerator HandleChoiceStep(VNChoiceStep choice)
        {
            // Выбор всегда останавливает skip
            if (SkipEnabled) SetSkip(false);

            _choiceWaiting = true;
            _currentChoiceStep = choice;

            _state.currentStepApplied = true;
            VNAutosave.Save(_state);

            var payload = new VNChoicePayload
            {
                stepId = _state.stepId,
                options = (choice.options != null) ? choice.options.ToArray() : Array.Empty<VNChoiceOption>()
            };

            OnChoicePresented?.Invoke(payload);

            while (_choiceWaiting)
                yield return null;
        }

        private IEnumerator HandleCommandStep(VNCommandStep cmdStep)
        {
            _advanceRequested = false;
            _interruptWaitRequested = false;

            bool stopAutoHere = IsStopAutoHere(cmdStep);

            if (!_state.currentStepApplied)
            {
                ApplyCommand(cmdStep.command, ref stopAutoHere);
                _state.currentStepApplied = true;
                VNAutosave.Save(_state);
            }

            // Skip: команды выполняются и сразу дальше, Wait – прерывается
            if (SkipEnabled && SkipAllowed)
            {
                if (skipStepFrameDelay <= 0f) yield return null;
                else yield return new WaitForSeconds(skipStepFrameDelay);

                AdvanceToNextStep();
                yield break;
            }

            // Wait
            if (cmdStep.command is VNWaitCommand waitCmd)
            {
                float t = Mathf.Max(0f, waitCmd.seconds);
                float elapsed = 0f;

                while (elapsed < t)
                {
                    if (_interruptWaitRequested) break;
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                AdvanceToNextStep();
                yield break;
            }

            // Gate/stopAuto: ждём клика
            if (stopAutoHere)
            {
                while (!_advanceRequested)
                    yield return null;

                AdvanceToNextStep();
                yield break;
            }

            // Остальные команды – мгновенно
            AdvanceToNextStep();
            yield return null;
        }

        private IEnumerator HandleJumpStep(VNJumpStep jump)
        {
            _state.currentStepApplied = true;
            VNAutosave.Save(_state);

            var target = Norm(jump.targetStepId);
            if (string.IsNullOrEmpty(target)) yield break;

            _state.stepId = target;
            _state.currentStepApplied = false;
            VNAutosave.Save(_state);

            yield return null;
        }

        // -------- Apply helpers --------

        private void ApplySkipAllowedForStep(VNChapterStep step)
        {
            bool allowed = true;

            if (step.disableSkip)
                allowed = false;

            if (step is VNCommandStep cs && cs.command is VNGateCommand gate && gate.gateDisablesSkip)
                allowed = false;

            if (SkipAllowed != allowed)
            {
                SkipAllowed = allowed;
                OnSkipAllowedChanged?.Invoke(SkipAllowed);
            }
        }

        private bool IsStopAutoHere(VNChapterStep step)
        {
            bool stop = step.stopAuto;

            if (step is VNChoiceStep) stop = true;

            if (step is VNCommandStep cs && cs.command is VNGateCommand gate && gate.gateStopsAuto)
                stop = true;

            return stop;
        }

        private void ApplyCommand(VNCommand command, ref bool stopAutoHere)
        {
            if (command == null) return;

            switch (command)
            {
                case VNSetBackgroundCommand bg:
                    _state.backgroundId = Norm(bg.backgroundId);
                    EmitBackground(bg.backgroundId, bg.crossfadeSeconds);
                    break;

                case VNShowCharacterCommand show:
                {
                    var slotState = _state.GetSlot(show.slot);
                    bool wasEmpty = !slotState.visible || string.IsNullOrWhiteSpace(slotState.characterId);

                    slotState.visible = true;
                    slotState.characterId = Norm(show.characterId);
                    slotState.pose = show.pose;
                    slotState.emotion = show.emotion;

                    EmitSlot(show.slot, show.characterId, show.pose, show.emotion, true, show.crossfadeSeconds, wasEmpty);

                    // Auto – обязательная остановка на новом персонаже
                    if (wasEmpty && AutoEnabled)
                        stopAutoHere = true;

                    break;
                }

                case VNHideCharacterCommand hide:
                {
                    var slotState = _state.GetSlot(hide.slot);
                    slotState.visible = false;
                    slotState.characterId = null;
                    slotState.pose = VNPose.Default;
                    slotState.emotion = VNEmotion.Neutral;

                    EmitSlot(hide.slot, null, VNPose.Default, VNEmotion.Neutral, false, hide.fadeSeconds, false);
                    break;
                }

                case VNPlayMusicCommand m:
                    _state.musicId = Norm(m.musicId);
                    EmitMusic(m.musicId, m.fadeInSeconds, m.loop);
                    break;

                case VNStopMusicCommand stop:
                    _state.musicId = null;
                    OnMusicStop?.Invoke(stop.fadeOutSeconds);
                    break;

                case VNPlaySfxCommand s:
                    EmitSfx(s.sfxId);
                    break;

                case VNSetBoolVarCommand vb:
                    _state.SetBool(vb.key, vb.value);
                    break;

                case VNSetIntVarCommand vi:
                    _state.SetInt(vi.key, vi.value);
                    break;

                case VNAddIntVarCommand ai:
                    _state.AddInt(ai.key, ai.delta);
                    break;

                case VNSetStringVarCommand vs:
                    _state.SetString(vs.key, vs.value);
                    break;

                case VNGateCommand gate:
                    if (gate.gateStopsAuto) stopAutoHere = true;
                    break;

                case VNWaitCommand:
                    break;
            }
        }

        private void AutoApplySpeakerPoseEmotion(string speakerId, VNPose pose, VNEmotion emotion)
        {
            if (project == null || project.characterDatabase == null) return;
            if (string.IsNullOrWhiteSpace(speakerId)) return;

            for (int i = 0; i < _state.slots.Count; i++)
            {
                var s = _state.slots[i];
                if (!s.visible) continue;
                if (string.Equals(s.characterId, speakerId, StringComparison.Ordinal))
                {
                    s.pose = pose;
                    s.emotion = emotion;
                    EmitSlot(s.slot, speakerId, pose, emotion, true, 0.2f, false);
                    return;
                }
            }

            if (!autoShowSpeakerIfMissing) return;

            var target = FindFirstEmptySlot(out bool hasEmpty);
            if (!hasEmpty) return;

            var slotState = _state.GetSlot(target);
            slotState.visible = true;
            slotState.characterId = speakerId;
            slotState.pose = pose;
            slotState.emotion = emotion;

            EmitSlot(target, speakerId, pose, emotion, true, 0.2f, true);

            if (AutoEnabled)
                _autoStopDueToNewCharacterThisStep = true;
        }

        private VNScreenSlot FindFirstEmptySlot(out bool found)
        {
            var order = new[] { VNScreenSlot.Center, VNScreenSlot.Left, VNScreenSlot.Right };
            for (int i = 0; i < order.Length; i++)
            {
                var s = _state.GetSlot(order[i]);
                if (!s.visible || string.IsNullOrWhiteSpace(s.characterId))
                {
                    found = true;
                    return order[i];
                }
            }
            found = false;
            return VNScreenSlot.Center;
        }

        private void AddToLog(VNLineStep line)
        {
            string speakerId = Norm(line.speakerId);
            string speakerName = "";

            if (!string.IsNullOrWhiteSpace(speakerId) && project.characterDatabase != null)
                project.characterDatabase.TryGetDisplayName(speakerId, out speakerName);

            _state.log.Add(new VNState.LogEntry
            {
                speakerId = speakerId,
                speakerName = speakerName ?? "",
                text = line.text ?? ""
            });
        }

        private void ApplyVarOp(VNVarOp op)
        {
            if (op == null) return;

            switch (op.type)
            {
                case VNVarOpType.SetBool:
                    _state.SetBool(op.key, op.boolValue);
                    break;
                case VNVarOpType.SetInt:
                    _state.SetInt(op.key, op.intValue);
                    break;
                case VNVarOpType.AddInt:
                    _state.AddInt(op.key, op.intValue);
                    break;
                case VNVarOpType.SetString:
                    _state.SetString(op.key, op.stringValue);
                    break;
            }
        }

        private VNLinePayload BuildLinePayload(VNLineStep line)
        {
            var speakerId = Norm(line.speakerId);
            bool isNarrator = string.IsNullOrWhiteSpace(speakerId);

            string speakerName = "";
            if (!isNarrator && project.characterDatabase != null)
                project.characterDatabase.TryGetDisplayName(speakerId, out speakerName);

            return new VNLinePayload
            {
                speakerId = speakerId,
                speakerName = speakerName ?? "",
                isNarrator = isNarrator,
                pose = line.pose,
                emotion = line.emotion,
                sfxId = Norm(line.sfxId),
                text = line.text ?? ""
            };
        }

        private void EmitFullScreenState()
        {
            if (!string.IsNullOrWhiteSpace(_state.backgroundId))
                EmitBackground(_state.backgroundId, 0f);

            if (!string.IsNullOrWhiteSpace(_state.musicId))
                EmitMusic(_state.musicId, 0f, true);

            _state.EnsureSlots();
            foreach (var s in _state.slots)
                EmitSlot(s.slot, s.characterId, s.pose, s.emotion, s.visible, 0f, false);
        }

        private void EmitBackground(string backgroundId, float crossfade)
        {
            backgroundId = Norm(backgroundId);
            Sprite sprite = null;

            if (!string.IsNullOrWhiteSpace(backgroundId) && project.assetDatabase != null)
                project.assetDatabase.TryGetBackground(backgroundId, out sprite);

            OnBackgroundChanged?.Invoke(new VNBackgroundPayload
            {
                backgroundId = backgroundId,
                sprite = sprite,
                crossfadeSeconds = Mathf.Max(0f, crossfade)
            });
        }

        private void EmitSlot(VNScreenSlot slot, string characterId, VNPose pose, VNEmotion emotion, bool visible, float crossfade, bool isNewCharacter)
        {
            characterId = Norm(characterId);

            string name = "";
            Sprite sprite = null;

            if (visible && !string.IsNullOrWhiteSpace(characterId) && project.characterDatabase != null)
            {
                project.characterDatabase.TryGetDisplayName(characterId, out name);
                project.characterDatabase.TryGetSprite(characterId, pose, emotion, out sprite);
            }

            OnSlotChanged?.Invoke(new VNSlotPayload
            {
                slot = slot,
                visible = visible,
                characterId = characterId,
                characterName = name ?? "",
                pose = pose,
                emotion = emotion,
                sprite = sprite,
                crossfadeSeconds = Mathf.Max(0f, crossfade),
                isNewCharacter = isNewCharacter
            });
        }

        private void EmitMusic(string musicId, float fadeIn, bool loop)
        {
            musicId = Norm(musicId);
            AudioClip clip = null;

            if (!string.IsNullOrWhiteSpace(musicId) && project.assetDatabase != null)
                project.assetDatabase.TryGetMusic(musicId, out clip);

            OnMusicPlay?.Invoke(new VNMusicPayload
            {
                musicId = musicId,
                clip = clip,
                fadeInSeconds = Mathf.Max(0f, fadeIn),
                loop = loop
            });
        }

        private void EmitSfx(string sfxId)
        {
            sfxId = Norm(sfxId);
            if (string.IsNullOrWhiteSpace(sfxId)) return;

            if (project.assetDatabase != null && project.assetDatabase.TryGetSfx(sfxId, out var clip))
                OnSfxPlay?.Invoke(clip);
        }

        private void AdvanceToNextStep()
        {
            if (!TryResolveChapter(out var chapter)) return;
            if (!chapter.TryGetStepIndex(_state.stepId, out int index)) return;

            int nextIndex = index + 1;
            if (nextIndex >= chapter.steps.Count) return;

            var next = chapter.steps[nextIndex];
            if (next == null || string.IsNullOrWhiteSpace(next.id)) return;

            _state.stepId = next.id;
            _state.currentStepApplied = false;
            VNAutosave.Save(_state);

            _advanceRequested = false;
            _interruptWaitRequested = false;
            _lineRevealCompleted = false;
        }

        private float ComputeAutoDelay(string text)
        {
            text ??= "";
            int chars = text.Length;
            int punct = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '.' || c == '!' || c == '?' || c == ',' || c == ';' || c == ':')
                    punct++;
            }

            float delay = autoBaseDelaySeconds + chars * autoPerCharacterSeconds + punct * autoPunctuationExtraSeconds;
            return Mathf.Max(0f, delay);
        }

        private IEnumerator WaitAutoOrUserInterrupt(float seconds)
        {
            float t = Mathf.Max(0f, seconds);
            float elapsed = 0f;

            while (elapsed < t)
            {
                if (!AutoEnabled) break; // пользователь взаимодействовал – auto отключён
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private bool TryResolveChapter(out VNChapter chapter)
        {
            chapter = null;
            if (project == null) return false;
            return project.TryGetChapter(_state.chapterId, out chapter);
        }

        private void StopInternal()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;

            _choiceWaiting = false;
            _currentChoiceStep = null;
            OnChoiceHidden?.Invoke();
            OnLineHidden?.Invoke();
        }

        private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}