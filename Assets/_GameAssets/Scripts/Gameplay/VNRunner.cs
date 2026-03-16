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
        [SerializeField] private float skipStepFrameDelay = 0f;

        public bool AutoEnabled { get; private set; }
        public bool SkipEnabled { get; private set; }
        public bool SkipAllowed { get; private set; } = true;

        public VNState State => _state;

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

        public event Action<VNArtifactPayload> OnArtifactShown;

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

        [Serializable]
        public struct VNArtifactPayload
        {
            public string artifactId;
            public Sprite sprite;

            public float dimAlpha;
            public float fadeInSeconds;
            public float scaleUpSeconds;
            public float scaleSettleSeconds;
            public float holdSeconds;
            public float fadeOutSeconds;
        }

        private VNState _state = new();

        private Coroutine _loop;

        private bool _lineRevealCompleted;
        private bool _advanceRequested;
        private bool _interruptWaitRequested;

        private bool _choiceWaiting;
        private VNChoiceStep _currentChoiceStep;

        private bool _autoStopDueToNewCharacterThisStep;

        private bool _artifactWaiting;

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

            if (AutoEnabled) SetAuto(false);
            if (SkipEnabled) SetSkip(false);

            if (_choiceWaiting || _artifactWaiting) return;

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

        public void NotifyArtifactPresentationFinished() => _artifactWaiting = false;

        public void Choose(int optionIndex)
        {
            if (!_choiceWaiting || _currentChoiceStep == null) return;
            if (_currentChoiceStep.options == null) return;
            if (optionIndex < 0 || optionIndex >= _currentChoiceStep.options.Count) return;

            var opt = _currentChoiceStep.options[optionIndex];

            if (opt.effects != null)
            {
                for (int i = 0; i < opt.effects.Count; i++)
                    ApplyVarOp(opt.effects[i]);
            }

            var next = Norm(opt.nextStepId);
            if (string.IsNullOrEmpty(next)) return;

            _choiceWaiting = false;
            _currentChoiceStep = null;
            OnChoiceHidden?.Invoke();

            _state.stepId = next;
            _state.currentStepApplied = false;
            VNAutosave.Save(_state);
        }

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
                    yield return HandleIfStep(chapter, index, iff);
                    continue;
                }

                if (step is VNLineStep line)
                {
                    yield return HandleLineStep(chapter, index, line);
                    continue;
                }

                if (step is VNChoiceStep choice)
                {
                    yield return HandleChoiceStep(choice);
                    continue;
                }

                if (step is VNCommandStep cmdStep)
                {
                    yield return HandleCommandStep(chapter, index, cmdStep);
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

        private IEnumerator HandleIfStep(VNChapter chapter, int index, VNIfStep iff)
        {
            bool result = EvaluateConditions(iff);
            string explicitTarget = result ? Norm(iff.trueStepId) : Norm(iff.falseStepId);

            if (TryAdvanceToExplicitOrFallback(chapter, index, explicitTarget))
            {
                yield return null;
                yield break;
            }

            yield break;
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

            return requireAll || anyTrue;
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

        private IEnumerator HandleLineStep(VNChapter chapter, int index, VNLineStep line)
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

            if (SkipEnabled && SkipAllowed)
            {
                OnRequestInstantReveal?.Invoke();
                _lineRevealCompleted = true;

                if (skipStepFrameDelay <= 0f) yield return null;
                else yield return new WaitForSeconds(skipStepFrameDelay);

                AdvanceToNextStep(chapter, index, line.nextStepId);
                OnLineHidden?.Invoke();
                yield break;
            }

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

            AdvanceToNextStep(chapter, index, line.nextStepId);
            OnLineHidden?.Invoke();
        }

        private IEnumerator HandleChoiceStep(VNChoiceStep choice)
        {
            if (SkipEnabled) SetSkip(false);

            _choiceWaiting = true;
            _currentChoiceStep = choice;

            _state.currentStepApplied = true;
            VNAutosave.Save(_state);

            var payload = new VNChoicePayload
            {
                stepId = _state.stepId,
                options = choice.options != null ? choice.options.ToArray() : Array.Empty<VNChoiceOption>()
            };

            OnChoicePresented?.Invoke(payload);

            while (_choiceWaiting)
                yield return null;
        }

        private IEnumerator HandleCommandStep(VNChapter chapter, int index, VNCommandStep cmdStep)
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

            if (cmdStep.command is VNGiveArtifactCommand)
            {
                if (SkipEnabled) SetSkip(false);

                while (_artifactWaiting)
                    yield return null;

                AdvanceToNextStep(chapter, index, cmdStep.nextStepId);
                yield break;
            }

            if (SkipEnabled && SkipAllowed)
            {
                if (skipStepFrameDelay <= 0f) yield return null;
                else yield return new WaitForSeconds(skipStepFrameDelay);

                AdvanceToNextStep(chapter, index, cmdStep.nextStepId);
                yield break;
            }

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

                AdvanceToNextStep(chapter, index, cmdStep.nextStepId);
                yield break;
            }

            if (stopAutoHere)
            {
                while (!_advanceRequested)
                    yield return null;

                AdvanceToNextStep(chapter, index, cmdStep.nextStepId);
                yield break;
            }

            AdvanceToNextStep(chapter, index, cmdStep.nextStepId);
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

            if (step is VNCommandStep art && art.command is VNGiveArtifactCommand)
                stop = true;

            return stop;
        }

        private void ApplyCommand(VNCommand command, ref bool stopAutoHere)
        {
            if (command == null) return;
            Debug.Log(command == null ? "[VN] command NULL" : "[VN] command type = " + command.GetType().Name);
            switch (command)
            {
                case VNSetBackgroundCommand bg:
                    _state.backgroundId = Norm(bg.backgroundId);
                    EmitBackground(bg.backgroundId, bg.crossfadeSeconds);
                    break;

                case VNShowCharacterCommand show:
                {
                    ShowOnlyCharacter(show.slot, show.characterId, show.pose, show.emotion, show.crossfadeSeconds, ref stopAutoHere);
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

                case VNGiveArtifactCommand give:
                    Debug.Log("[VN] VNGiveArtifactCommand CASE HIT");
                    EmitArtifact(give);
                    stopAutoHere = true;
                    break;
            }
        }

        private void EmitArtifact(VNGiveArtifactCommand cmd)
        {
            string artifactId = Norm(cmd.artifactId);
            Sprite sprite = null;

            Debug.Log("[VN] EmitArtifact called");
            Debug.Log("[VN] artifactId = " + artifactId);
            Debug.Log("[VN] project null = " + (project == null));
            Debug.Log("[VN] assetDatabase null = " + (project == null || project.assetDatabase == null));

            if (!string.IsNullOrWhiteSpace(artifactId) && project.assetDatabase != null)
                project.assetDatabase.TryGetArtifact(artifactId, out sprite);

            Debug.Log("[VN] sprite found = " + (sprite != null));
            Debug.Log("[VN] OnArtifactShown listeners = " + (OnArtifactShown != null));

            if (sprite == null || OnArtifactShown == null)
            {
                Debug.LogWarning("[VN] Artifact show aborted");
                _artifactWaiting = false;
                return;
            }

            _artifactWaiting = true;

            OnArtifactShown?.Invoke(new VNArtifactPayload
            {
                artifactId = artifactId,
                sprite = sprite,
                dimAlpha = Mathf.Clamp01(cmd.dimAlpha),
                fadeInSeconds = Mathf.Max(0f, cmd.fadeInSeconds),
                scaleUpSeconds = Mathf.Max(0f, cmd.scaleUpSeconds),
                scaleSettleSeconds = Mathf.Max(0f, cmd.scaleSettleSeconds),
                holdSeconds = Mathf.Max(0f, cmd.holdSeconds),
                fadeOutSeconds = Mathf.Max(0f, cmd.fadeOutSeconds)
            });

            Debug.Log("[VN] OnArtifactShown invoked");
        }

        private void AutoApplySpeakerPoseEmotion(string speakerId, VNPose pose, VNEmotion emotion)
        {
            if (project == null || project.characterDatabase == null) return;
            if (string.IsNullOrWhiteSpace(speakerId)) return;
            if (!autoShowSpeakerIfMissing) return;

            _state.EnsureSlots();

            int visibleCount = 0;
            int visibleIndex = -1;

            for (int i = 0; i < _state.slots.Count; i++)
            {
                var s = _state.slots[i];
                if (!s.visible || string.IsNullOrWhiteSpace(s.characterId))
                    continue;

                visibleCount++;
                visibleIndex = i;
            }

            if (visibleCount == 1)
            {
                var current = _state.slots[visibleIndex];

                if (string.Equals(current.characterId, speakerId, StringComparison.Ordinal))
                {
                    current.pose = pose;
                    current.emotion = emotion;

                    EmitSlot(current.slot, speakerId, pose, emotion, true, 0f, false);
                    return;
                }

                HideAllCharacters(0f);

                var slotState = _state.GetSlot(current.slot);
                slotState.visible = true;
                slotState.characterId = speakerId;
                slotState.pose = pose;
                slotState.emotion = emotion;

                EmitSlot(current.slot, speakerId, pose, emotion, true, 0.2f, true);

                if (AutoEnabled)
                    _autoStopDueToNewCharacterThisStep = true;

                return;
            }

            if (visibleCount > 1)
            {
                var target = FindPreferredSingleSlot();
                HideAllCharacters(0f);

                var slotState = _state.GetSlot(target);
                slotState.visible = true;
                slotState.characterId = speakerId;
                slotState.pose = pose;
                slotState.emotion = emotion;

                EmitSlot(target, speakerId, pose, emotion, true, 0.2f, true);

                if (AutoEnabled)
                    _autoStopDueToNewCharacterThisStep = true;

                return;
            }

            {
                var target = FindPreferredSingleSlot();

                var slotState = _state.GetSlot(target);
                slotState.visible = true;
                slotState.characterId = speakerId;
                slotState.pose = pose;
                slotState.emotion = emotion;

                EmitSlot(target, speakerId, pose, emotion, true, 0.2f, true);

                if (AutoEnabled)
                    _autoStopDueToNewCharacterThisStep = true;
            }
        }

        private void ShowOnlyCharacter(VNScreenSlot preferredSlot, string characterId, VNPose pose, VNEmotion emotion, float crossfadeSeconds, ref bool stopAutoHere)
        {
            characterId = Norm(characterId);
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            _state.EnsureSlots();

            VNScreenSlot targetSlot = preferredSlot;
            bool foundSameCharacter = false;

            for (int i = 0; i < _state.slots.Count; i++)
            {
                var s = _state.slots[i];
                if (!s.visible || string.IsNullOrWhiteSpace(s.characterId))
                    continue;

                if (string.Equals(s.characterId, characterId, StringComparison.Ordinal))
                {
                    targetSlot = s.slot;
                    foundSameCharacter = true;
                    break;
                }
            }

            for (int i = 0; i < _state.slots.Count; i++)
            {
                var s = _state.slots[i];
                bool isTarget = s.slot == targetSlot;

                if (!isTarget && s.visible)
                {
                    s.visible = false;
                    s.characterId = null;
                    s.pose = VNPose.Default;
                    s.emotion = VNEmotion.Neutral;

                    EmitSlot(s.slot, null, VNPose.Default, VNEmotion.Neutral, false, 0f, false);
                }
            }

            var targetState = _state.GetSlot(targetSlot);
            bool wasSameCharacterAlreadyVisible =
                targetState.visible &&
                string.Equals(targetState.characterId, characterId, StringComparison.Ordinal);

            targetState.visible = true;
            targetState.characterId = characterId;
            targetState.pose = pose;
            targetState.emotion = emotion;

            float emitFade = wasSameCharacterAlreadyVisible ? 0f : Mathf.Max(0f, crossfadeSeconds);
            bool isNewCharacter = !wasSameCharacterAlreadyVisible;

            EmitSlot(targetSlot, characterId, pose, emotion, true, emitFade, isNewCharacter);

            if (isNewCharacter && AutoEnabled)
                stopAutoHere = true;
        }

        private void HideAllCharacters(float fadeSeconds)
        {
            _state.EnsureSlots();

            for (int i = 0; i < _state.slots.Count; i++)
            {
                var s = _state.slots[i];
                if (!s.visible && string.IsNullOrWhiteSpace(s.characterId))
                    continue;

                s.visible = false;
                s.characterId = null;
                s.pose = VNPose.Default;
                s.emotion = VNEmotion.Neutral;

                EmitSlot(s.slot, null, VNPose.Default, VNEmotion.Neutral, false, fadeSeconds, false);
            }
        }

        private VNScreenSlot FindPreferredSingleSlot()
        {
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

            bool alreadyOneVisible = false;
            for (int i = 0; i < _state.slots.Count; i++)
            {
                var s = _state.slots[i];
                bool shouldShow = s.visible && !string.IsNullOrWhiteSpace(s.characterId);

                if (shouldShow && !alreadyOneVisible)
                {
                    alreadyOneVisible = true;
                    EmitSlot(s.slot, s.characterId, s.pose, s.emotion, true, 0f, false);
                }
                else
                {
                    EmitSlot(s.slot, null, VNPose.Default, VNEmotion.Neutral, false, 0f, false);
                }
            }
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

        private void AdvanceToNextStep(VNChapter chapter, int currentIndex, string explicitNextStepId)
        {
            if (TryResolveExplicitOrLinearNext(chapter, currentIndex, explicitNextStepId, out string nextId))
            {
                _state.stepId = nextId;
                _state.currentStepApplied = false;
                VNAutosave.Save(_state);
            }

            _advanceRequested = false;
            _interruptWaitRequested = false;
            _lineRevealCompleted = false;
        }

        private bool TryAdvanceToExplicitOrFallback(VNChapter chapter, int currentIndex, string explicitTargetStepId)
        {
            if (TryResolveExplicitOrLinearNext(chapter, currentIndex, explicitTargetStepId, out string nextId))
            {
                _state.stepId = nextId;
                _state.currentStepApplied = false;
                VNAutosave.Save(_state);

                _advanceRequested = false;
                _interruptWaitRequested = false;
                _lineRevealCompleted = false;
                return true;
            }

            _advanceRequested = false;
            _interruptWaitRequested = false;
            _lineRevealCompleted = false;
            return false;
        }

        private bool TryResolveExplicitOrLinearNext(VNChapter chapter, int currentIndex, string explicitTargetStepId, out string resolvedStepId)
        {
            resolvedStepId = null;
            if (chapter == null || chapter.steps == null) return false;

            string explicitId = Norm(explicitTargetStepId);
            if (!string.IsNullOrEmpty(explicitId))
            {
                if (chapter.TryGetStepIndex(explicitId, out _))
                {
                    resolvedStepId = explicitId;
                    return true;
                }
            }

            int nextIndex = currentIndex + 1;
            if (nextIndex < 0 || nextIndex >= chapter.steps.Count) return false;

            var next = chapter.steps[nextIndex];
            if (next == null || string.IsNullOrWhiteSpace(next.id)) return false;

            resolvedStepId = next.id.Trim();
            return true;
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
                if (!AutoEnabled) break;
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
            _artifactWaiting = false;

            OnChoiceHidden?.Invoke();
            OnLineHidden?.Invoke();
        }

        private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}