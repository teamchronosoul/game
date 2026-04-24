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

        [Header("Startup Control")]
        [SerializeField] private bool startAutomatically = true;

        [Header("Auto timing")]
        [SerializeField] private bool useFixedAutoReadDelay = true;
        [SerializeField] [Min(0f)] private float fixedAutoReadDelaySeconds = 1.2f;

        [Tooltip("Используется только если Use Fixed Auto Read Delay выключен.")]
        [SerializeField] [Min(0f)] private float autoBaseDelaySeconds = 0.8f;

        [Tooltip("Используется только если Use Fixed Auto Read Delay выключен.")]
        [SerializeField] [Min(0f)] private float autoPerCharacterSeconds = 0.03f;

        [Tooltip("Используется только если Use Fixed Auto Read Delay выключен.")]
        [SerializeField] [Min(0f)] private float autoPunctuationExtraSeconds = 0.25f;

        [Tooltip("Если включено, авточтение будет выключаться при появлении нового персонажа.")]
        [SerializeField] private bool stopAutoOnNewCharacter = false;

        [Header("Skip")]
        [SerializeField] [Min(0f)] private float skipStepFrameDelay = 0f;

        [Header("MBTI")]
        [SerializeField] private VNMbtiState mbti = new();
        public VNMbtiState Mbti => mbti;

        [Header("Character auto show")]
        [SerializeField] [Min(0f)] private float autoSpeakerCrossfadeSeconds = 0.2f;

        [Header("VFX")]
        [SerializeField] private VNVfxPlayer vfxPlayer;

        [Header("UI")]
        [SerializeField] private GameObject mainMenuRoot;

        public bool AutoEnabled { get; private set; }
        public bool SkipEnabled { get; private set; }
        public bool SkipAllowed { get; private set; } = true;

        public VNState State { get; private set; } = new();

        public event Action<VNLinePayload> OnLineStarted;
        public event Action OnRequestInstantReveal;
        public event Action OnLineHidden;
        public event Action OnMainMenuRequested;
        public event Action<VNChoicePayload> OnChoicePresented;
        public event Action OnChoiceHidden;

        public event Action<VNBackgroundPayload> OnBackgroundChanged;
        public event Action<VNSlotPayload> OnSlotChanged;

        public event Action<VNMusicPayload> OnMusicPlay;
        public event Action<float> OnMusicStop;
        public event Action<VNSfxPayload> OnSfxPlay;
        public event Action<VNArtifactPayload> OnArtifactShown;

        public event Action<bool> OnAutoChanged;
        public event Action<bool> OnSkipChanged;
        public event Action<bool> OnSkipAllowedChanged;

        public event Action<Vector2> OnTapFeedback;

        public event Action<string> OnChapterEnded;

        [Serializable]
        public struct VNLinePayload
        {
            public string speakerId;
            public string speakerName;
            public bool isNarrator;

            public bool showSpeakerName;

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
        public struct VNSfxPayload
        {
            public string sfxId;
            public AudioClip clip;
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

        private Coroutine _loop;

        private bool _lineRevealCompleted;
        private bool _advanceRequested;
        private bool _interruptWaitRequested;
        private bool _modalOpen;
        private bool _suppressNextTap;

        private string _presentedPlayerName = "Player";

        private bool _choiceWaiting;
        private VNChoiceStep _currentChoiceStep;

        private bool _autoStopDueToNewCharacterThisStep;

        private bool _artifactWaiting;

        private void Awake()
        {
            State.EnsureSlots();
        }

        private void Start()
        {
            if (!startAutomatically)
                return;

            if (project == null)
                return;

            if (autoLoadAutosaveOnStart && VNAutosave.TryLoad(out var loaded))
            {
                State = loaded;
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

            State.ResetAll();
            State.chapterId = chapterId;
            State.currentStepApplied = false;
            State.currentStepLogged = false;

            SetAuto(false);
            SetSkip(false);
            SetSkipAllowed(true);

            if (!TryResolveChapter(out var ch))
                return;

            if (ch.steps == null || ch.steps.Count == 0)
                return;

            var first = ch.steps[0];
            if (first == null || string.IsNullOrWhiteSpace(first.id))
                return;

            State.stepId = first.id;
            State.currentStepApplied = false;
            State.currentStepLogged = false;

            VNAutosave.Save(State);

            _loop = StartCoroutine(MainLoop());
        }

        public void ResumeFromState()
        {
            StopInternal();

            if (!TryResolveChapter(out _))
                return;

            if (string.IsNullOrWhiteSpace(State.stepId))
                return;

            EmitFullScreenState();
            _loop = StartCoroutine(MainLoop());
        }

        public void DeleteAutosaveAndRestart()
        {
            VNAutosave.Delete();
            StartNew(startChapterId);
        }

        public void SuppressNextTap()
        {
            _suppressNextTap = true;
        }

        public void Tap(Vector2 screenPosition)
        {
            if (_suppressNextTap)
            {
                _suppressNextTap = false;
                return;
            }

            if (_modalOpen)
                return;

            OnTapFeedback?.Invoke(screenPosition);

            // Auto выключается только тапом по экрану.
            // Этот же тап НЕ перелистывает строку.
            if (AutoEnabled)
            {
                SetAuto(false);
                return;
            }

            if (SkipEnabled)
            {
                SetSkip(false);
                return;
            }

            if (_choiceWaiting || _artifactWaiting)
                return;

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
            if (AutoEnabled == enabled)
                return;

            AutoEnabled = enabled;
            OnAutoChanged?.Invoke(AutoEnabled);
        }

        public void SetSkip(bool enabled)
        {
            if (enabled && !SkipAllowed)
                enabled = false;

            if (SkipEnabled == enabled)
                return;

            SkipEnabled = enabled;
            OnSkipChanged?.Invoke(SkipEnabled);
        }

        public void SetModalOpen(bool value)
        {
            _modalOpen = value;
        }

        public void NotifyLineRevealFinished()
        {
            _lineRevealCompleted = true;
        }

        public void NotifyArtifactPresentationFinished()
        {
            _artifactWaiting = false;
        }

        public void Choose(int optionIndex)
        {
            if (_modalOpen)
                return;

            if (!_choiceWaiting || _currentChoiceStep == null)
                return;

            if (_currentChoiceStep.options == null)
                return;

            if (optionIndex < 0 || optionIndex >= _currentChoiceStep.options.Count)
                return;

            var opt = _currentChoiceStep.options[optionIndex];

            AddChoiceToLog(opt.text);

            if (opt.effects != null)
            {
                for (int i = 0; i < opt.effects.Count; i++)
                    ApplyVarOp(opt.effects[i]);
            }

            var next = Norm(opt.nextStepId);
            if (string.IsNullOrEmpty(next))
                return;

            _choiceWaiting = false;
            _currentChoiceStep = null;

            OnChoiceHidden?.Invoke();

            State.stepId = next;
            State.currentStepApplied = false;
            State.currentStepLogged = false;

            VNAutosave.Save(State);
        }

        private IEnumerator MainLoop()
        {
            while (true)
            {
                if (!TryResolveChapter(out var chapter))
                    yield break;

                if (!chapter.TryGetStepIndex(State.stepId, out var index))
                    yield break;

                var step = chapter.GetStepAt(index);
                if (step == null)
                    yield break;

                ApplySkipAllowedForStep(step);

                if (SkipEnabled && !SkipAllowed)
                    SetSkip(false);

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
                {
                    OnChapterEnded?.Invoke(State.chapterId);
                    ShowMainMenu();
                }

                yield break;
            }
        }

        private IEnumerator HandleIfStep(VNChapter chapter, int index, VNIfStep iff)
        {
            var stopAutoHere = IsStopAutoHere(iff);
            StopAutoIfNeeded(stopAutoHere);

            if (stopAutoHere)
                yield return WaitManualAdvance();

            var result = EvaluateConditions(iff);
            var explicitTarget = result ? Norm(iff.trueStepId) : Norm(iff.falseStepId);

            TryAdvanceToExplicitOrFallback(chapter, index, explicitTarget);

            yield return null;
        }

        private IEnumerator HandleLineStep(VNChapter chapter, int index, VNLineStep line)
        {
            _advanceRequested = false;
            _interruptWaitRequested = false;
            _autoStopDueToNewCharacterThisStep = false;

            if (!State.currentStepApplied)
            {
                if (!string.IsNullOrWhiteSpace(line.speakerId))
                    AutoApplySpeakerPoseEmotion(Norm(line.speakerId), line.pose, line.emotion);

                if (!string.IsNullOrWhiteSpace(line.sfxId))
                    EmitSfx(Norm(line.sfxId));

                State.currentStepApplied = true;
                VNAutosave.Save(State);
            }

            // Важно: сначала false, потом OnLineStarted.
            // Иначе короткий typewriter может успеть завершиться, а мы потом снова поставим false.
            _lineRevealCompleted = false;

            var payload = BuildLinePayload(line);
            OnLineStarted?.Invoke(payload);

            yield return WaitUntilLineRevealed();

            AddToLogAfterReveal(line);

            bool stopAutoHere = IsStopAutoHere(line) || _autoStopDueToNewCharacterThisStep;
            StopAutoIfNeeded(stopAutoHere);

            if (SkipEnabled && SkipAllowed)
            {
                yield return WaitSkipStepDelay();

                AdvanceToNextStep(chapter, index, line.nextStepId);
                OnLineHidden?.Invoke();
                yield break;
            }

            while (!_advanceRequested)
            {
                if (_modalOpen)
                {
                    yield return null;
                    continue;
                }

                if (AutoEnabled && !stopAutoHere)
                {
                    float delay = ComputeAutoDelay(payload.text);
                    yield return WaitAutoOrUserInterrupt(delay);

                    if (AutoEnabled && !_modalOpen)
                    {
                        _advanceRequested = true;
                        break;
                    }

                    continue;
                }

                yield return null;
            }

            AdvanceToNextStep(chapter, index, line.nextStepId);
            OnLineHidden?.Invoke();
        }

        private IEnumerator HandleChoiceStep(VNChoiceStep choice)
        {
            if (AutoEnabled)
                SetAuto(false);

            if (SkipEnabled)
                SetSkip(false);

            _choiceWaiting = true;
            _currentChoiceStep = choice;

            State.currentStepApplied = true;
            VNAutosave.Save(State);

            var payload = new VNChoicePayload
            {
                stepId = State.stepId,
                options = choice.options != null ? choice.options.ToArray() : Array.Empty<VNChoiceOption>()
            };

            OnChoicePresented?.Invoke(payload);

            while (_choiceWaiting)
            {
                yield return null;
            }
        }

        private IEnumerator HandleCommandStep(VNChapter chapter, int index, VNCommandStep cmdStep)
        {
            _advanceRequested = false;
            _interruptWaitRequested = false;

            var stopAutoHere = IsStopAutoHere(cmdStep);

            if (!State.currentStepApplied)
            {
                if (cmdStep.command is VNVfxCommand vfxCommand)
                {
                    var shouldWaitForVfx = vfxCommand.waitUntilFinished && !(SkipEnabled && SkipAllowed);
                    yield return StartCoroutine(HandleVfxCommand(vfxCommand, shouldWaitForVfx));
                }
                else
                {
                    ApplyCommand(cmdStep.command, ref stopAutoHere);
                }

                State.currentStepApplied = true;
                VNAutosave.Save(State);
            }
            
            StopAutoIfNeeded(stopAutoHere);

            if (cmdStep.command is VNGiveArtifactCommand)
            {
                if (SkipEnabled)
                    SetSkip(false);

                while (_artifactWaiting)
                {
                    if (_modalOpen)
                    {
                        yield return null;
                        continue;
                    }

                    yield return null;
                }

                AdvanceToNextStep(chapter, index, cmdStep.nextStepId);
                yield break;
            }

            if (SkipEnabled && SkipAllowed)
            {
                yield return WaitSkipStepDelay();

                AdvanceToNextStep(chapter, index, cmdStep.nextStepId);
                yield break;
            }

            if (cmdStep.command is VNWaitCommand waitCmd)
            {
                yield return WaitCommandSeconds(waitCmd.seconds);

                AdvanceToNextStep(chapter, index, cmdStep.nextStepId);
                yield break;
            }

            if (stopAutoHere)
            {
                yield return WaitManualAdvance();

                AdvanceToNextStep(chapter, index, cmdStep.nextStepId);

                if (cmdStep.command is VNResolveMbtiCommand)
                    OnLineHidden?.Invoke();

                yield break;
            }

            AdvanceToNextStep(chapter, index, cmdStep.nextStepId);
            yield return null;
        }

        private IEnumerator HandleJumpStep(VNJumpStep jump)
        {
            StopAutoIfNeeded(IsStopAutoHere(jump));

            State.currentStepApplied = true;
            VNAutosave.Save(State);

            var target = Norm(jump.targetStepId);
            if (string.IsNullOrEmpty(target))
                yield break;

            State.stepId = target;
            State.currentStepApplied = false;
            State.currentStepLogged = false;

            VNAutosave.Save(State);

            yield return null;
        }

        private IEnumerator WaitUntilLineRevealed()
        {
            while (!_lineRevealCompleted)
            {
                if (_modalOpen)
                {
                    yield return null;
                    continue;
                }

                if (SkipEnabled && SkipAllowed)
                {
                    OnRequestInstantReveal?.Invoke();
                    _lineRevealCompleted = true;
                    break;
                }

                yield return null;
            }
        }

        private IEnumerator WaitManualAdvance()
        {
            while (!_advanceRequested)
            {
                if (_modalOpen)
                {
                    yield return null;
                    continue;
                }

                yield return null;
            }
        }

        private IEnumerator WaitCommandSeconds(float seconds)
        {
            float duration = Mathf.Max(0f, seconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (_modalOpen)
                {
                    yield return null;
                    continue;
                }

                if (_interruptWaitRequested)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitSkipStepDelay()
        {
            if (skipStepFrameDelay <= 0f)
                yield return null;
            else
                yield return new WaitForSeconds(skipStepFrameDelay);
        }

        private IEnumerator WaitAutoOrUserInterrupt(float seconds)
        {
            float duration = Mathf.Max(0f, seconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (_modalOpen)
                {
                    yield return null;
                    continue;
                }

                if (!AutoEnabled)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void ApplySkipAllowedForStep(VNChapterStep step)
        {
            var allowed = step == null || !step.disableSkip;

            if (step is VNCommandStep cs && cs.command is VNGateCommand gate && gate.gateDisablesSkip)
                allowed = false;

            SetSkipAllowed(allowed);
        }

        private void SetSkipAllowed(bool allowed)
        {
            if (SkipAllowed == allowed)
                return;

            SkipAllowed = allowed;
            OnSkipAllowedChanged?.Invoke(SkipAllowed);
        }

        private bool IsStopAutoHere(VNChapterStep step)
        {
            if (step == null)
                return false;

            var stop = step.stopAuto;

            if (step is VNChoiceStep)
                stop = true;

            if (step is VNCommandStep cs && cs.command is VNGateCommand gate && gate.gateStopsAuto)
                stop = true;

            if (step is VNCommandStep art && art.command is VNGiveArtifactCommand)
                stop = true;

            return stop;
        }

        private void StopAutoIfNeeded(bool stopAutoHere)
        {
            if (!stopAutoHere)
                return;

            if (AutoEnabled)
                SetAuto(false);
        }

        private void ApplyCommand(VNCommand command, ref bool stopAutoHere)
        {
            if (command == null)
                return;

            switch (command)
            {
                case VNSetBackgroundCommand bg:
                    State.backgroundId = Norm(bg.backgroundId);
                    EmitBackground(bg.backgroundId, bg.crossfadeSeconds);
                    break;

                case VNShowCharacterCommand show:
                    ShowOnlyCharacter(
                        show.slot,
                        show.characterId,
                        show.pose,
                        show.emotion,
                        show.crossfadeSeconds,
                        ref stopAutoHere);
                    break;

                case VNHideCharacterCommand hide:
                    HideCharacter(hide.slot, hide.fadeSeconds);
                    break;

                case VNPlayMusicCommand m:
                    State.musicId = Norm(m.musicId);
                    EmitMusic(m.musicId, m.fadeInSeconds, m.loop);
                    break;

                case VNStopMusicCommand stop:
                    State.musicId = null;
                    OnMusicStop?.Invoke(stop.fadeOutSeconds);
                    break;

                case VNPlaySfxCommand s:
                    EmitSfx(Norm(s.sfxId));
                    break;

                case VNMbtiAnswerCommand mbtiCmd:
                    ApplyMbtiAnswer(mbtiCmd.letter);
                    break;

                case VNResolveMbtiCommand:
                    ResolveMbtiResult();
                    ShowMbtiResultLine();

                    stopAutoHere = true;
                    State.mbti = mbti;
                    VNAutosave.Save(State);

                    if (SkipEnabled)
                        SetSkip(false);
                    break;

                case VNSetBoolVarCommand vb:
                    State.SetBool(vb.key, vb.value);
                    break;

                case VNSetIntVarCommand vi:
                    State.SetInt(vi.key, vi.value);
                    break;

                case VNAddIntVarCommand ai:
                    State.AddInt(ai.key, ai.delta);
                    break;

                case VNSetStringVarCommand vs:
                    State.SetString(vs.key, vs.value);
                    break;

                case VNGateCommand gate:
                    if (gate.gateStopsAuto)
                        stopAutoHere = true;
                    break;

                case VNWaitCommand:
                    break;

                case VNGiveArtifactCommand give:
                    EmitArtifact(give);
                    stopAutoHere = true;
                    break;
            }
        }

        private IEnumerator HandleVfxCommand(VNVfxCommand command, bool waitUntilFinished)
        {
            if (command == null)
                yield break;

            if (project == null)
            {
                Debug.LogWarning("[VN] Can't play VFX: project is null.");
                yield break;
            }

            if (vfxPlayer == null)
            {
                Debug.LogWarning("[VN] Can't play VFX: VNVfxPlayer is not assigned.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(command.vfxId))
            {
                Debug.LogWarning("[VN] Can't play VFX: empty vfxId.");
                yield break;
            }

            if (project.assetDatabase == null)
            {
                Debug.LogWarning("[VN] Can't play VFX: assetDatabase is null.");
                yield break;
            }

            if (!project.assetDatabase.TryGetVfx(command.vfxId, out var definition))
            {
                Debug.LogWarning($"[VN] Can't play VFX: id '{command.vfxId}' not found in project database.");
                yield break;
            }

            var handle = vfxPlayer.Play(
                definition,
                command.anchorId,
                command.localOffset,
                command.scale,
                command.lifetimeOverride,
                command.softStopSecondsOverride
            );

            if (waitUntilFinished && handle != null)
            {
                while (!handle.IsFinished)
                    yield return null;
            }
        }

        private void EmitArtifact(VNGiveArtifactCommand cmd)
        {
            var artifactId = Norm(cmd.artifactId);
            Sprite sprite = null;

            if (!string.IsNullOrWhiteSpace(artifactId) && project.assetDatabase != null)
                project.assetDatabase.TryGetArtifact(artifactId, out sprite);

            if (sprite == null || OnArtifactShown == null)
            {
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
        }

        private void AutoApplySpeakerPoseEmotion(string speakerId, VNPose pose, VNEmotion emotion)
        {
            if (project == null || project.characterDatabase == null)
                return;

            if (string.IsNullOrWhiteSpace(speakerId))
                return;

            if (!autoShowSpeakerIfMissing)
                return;

            speakerId = Norm(speakerId);
            State.EnsureSlots();

            var fade = Mathf.Max(0f, autoSpeakerCrossfadeSeconds);
            var targetSlot = FindPreferredSingleSlot(speakerId);

            for (var i = 0; i < State.slots.Count; i++)
            {
                var s = State.slots[i];

                if (!s.visible || string.IsNullOrWhiteSpace(s.characterId))
                    continue;

                if (!string.Equals(s.characterId, speakerId, StringComparison.Ordinal))
                    continue;

                s.pose = pose;
                s.emotion = emotion;

                EmitSlot(s.slot, speakerId, pose, emotion, true, 0f, false);
                return;
            }

            for (var i = 0; i < State.slots.Count; i++)
            {
                var s = State.slots[i];

                if (!s.visible && string.IsNullOrWhiteSpace(s.characterId))
                    continue;

                s.visible = false;
                s.characterId = null;
                s.pose = VNPose.Default;
                s.emotion = VNEmotion.Neutral;

                EmitSlot(s.slot, null, VNPose.Default, VNEmotion.Neutral, false, fade, false);
            }

            var targetState = State.GetSlot(targetSlot);

            targetState.visible = true;
            targetState.characterId = speakerId;
            targetState.pose = pose;
            targetState.emotion = emotion;

            EmitSlot(targetSlot, speakerId, pose, emotion, true, fade, true);

            if (AutoEnabled && stopAutoOnNewCharacter)
                _autoStopDueToNewCharacterThisStep = true;
        }

        private void ShowOnlyCharacter(
            VNScreenSlot preferredSlot,
            string characterId,
            VNPose pose,
            VNEmotion emotion,
            float crossfadeSeconds,
            ref bool stopAutoHere)
        {
            characterId = Norm(characterId);

            if (string.IsNullOrWhiteSpace(characterId))
                return;

            State.EnsureSlots();

            var fade = Mathf.Max(0f, crossfadeSeconds);
            var targetSlot = preferredSlot;

            for (var i = 0; i < State.slots.Count; i++)
            {
                var s = State.slots[i];

                if (!s.visible || string.IsNullOrWhiteSpace(s.characterId))
                    continue;

                if (string.Equals(s.characterId, characterId, StringComparison.Ordinal))
                {
                    targetSlot = s.slot;
                    break;
                }
            }

            for (var i = 0; i < State.slots.Count; i++)
            {
                var s = State.slots[i];
                bool isTarget = s.slot == targetSlot;

                if (isTarget || !s.visible)
                    continue;

                s.visible = false;
                s.characterId = null;
                s.pose = VNPose.Default;
                s.emotion = VNEmotion.Neutral;

                EmitSlot(s.slot, null, VNPose.Default, VNEmotion.Neutral, false, fade, false);
            }

            var targetState = State.GetSlot(targetSlot);

            bool wasSameCharacterAlreadyVisible =
                targetState.visible &&
                string.Equals(targetState.characterId, characterId, StringComparison.Ordinal);

            targetState.visible = true;
            targetState.characterId = characterId;
            targetState.pose = pose;
            targetState.emotion = emotion;

            float emitFade = wasSameCharacterAlreadyVisible ? 0f : fade;
            bool isNewCharacter = !wasSameCharacterAlreadyVisible;

            EmitSlot(targetSlot, characterId, pose, emotion, true, emitFade, isNewCharacter);

            if (isNewCharacter && AutoEnabled && stopAutoOnNewCharacter)
                stopAutoHere = true;
        }

        private void HideCharacter(VNScreenSlot slot, float fadeSeconds)
        {
            var slotState = State.GetSlot(slot);

            slotState.visible = false;
            slotState.characterId = null;
            slotState.pose = VNPose.Default;
            slotState.emotion = VNEmotion.Neutral;

            EmitSlot(slot, null, VNPose.Default, VNEmotion.Neutral, false, fadeSeconds, false);
        }

        private void HideAllCharacters(float fadeSeconds)
        {
            State.EnsureSlots();

            for (var i = 0; i < State.slots.Count; i++)
            {
                var s = State.slots[i];

                if (!s.visible && string.IsNullOrWhiteSpace(s.characterId))
                    continue;

                s.visible = false;
                s.characterId = null;
                s.pose = VNPose.Default;
                s.emotion = VNEmotion.Neutral;

                EmitSlot(s.slot, null, VNPose.Default, VNEmotion.Neutral, false, fadeSeconds, false);
            }
        }

        private void ShowMbtiResultLine()
        {
            _advanceRequested = false;
            _interruptWaitRequested = false;
            _lineRevealCompleted = false;

            OnLineStarted?.Invoke(new VNLinePayload
            {
                speakerId = null,
                speakerName = "",
                isNarrator = true,
                showSpeakerName = true,
                pose = VNPose.Default,
                emotion = VNEmotion.Neutral,
                sfxId = null,
                text = $"<color={mbti.ResultColorHex}>{mbti.ResultType}</color>"
            });
        }

        private VNScreenSlot FindPreferredSingleSlot(string characterId)
        {
            if (project != null &&
                project.characterDatabase != null &&
                project.characterDatabase.TryGetDefaultScreenSlot(characterId, out var slot))
                return slot;

            return VNScreenSlot.Center;
        }

        private bool EvaluateConditions(VNIfStep iff)
        {
            if (iff.conditions == null || iff.conditions.Count == 0)
                return true;

            var requireAll = iff.requireAll;
            var anyTrue = false;

            for (var i = 0; i < iff.conditions.Count; i++)
            {
                var c = iff.conditions[i];

                if (c == null)
                    continue;

                bool ok = EvaluateSingleCondition(c);

                if (requireAll && !ok)
                    return false;

                if (!requireAll && ok)
                    return true;

                anyTrue |= ok;
            }

            return requireAll || anyTrue;
        }

        private bool EvaluateSingleCondition(VNCondition c)
        {
            var key = Norm(c.key);

            switch (c.type)
            {
                case VNConditionValueType.Bool:
                {
                    bool v = State.GetBool(key);
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
                    int v = State.GetInt(key);
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
                    string v = State.GetString(key);
                    string t = c.stringValue ?? "";

                    return c.op switch
                    {
                        VNConditionOp.Equals => string.Equals(v, t, StringComparison.Ordinal),
                        VNConditionOp.NotEquals => !string.Equals(v, t, StringComparison.Ordinal),
                        VNConditionOp.Contains => !string.IsNullOrEmpty(t) &&
                                                  (v?.IndexOf(t, StringComparison.Ordinal) ?? -1) >= 0,
                        VNConditionOp.NotContains => string.IsNullOrEmpty(t) ||
                                                     (v?.IndexOf(t, StringComparison.Ordinal) ?? -1) < 0,
                        _ => false
                    };
                }
            }

            return false;
        }

        private void ApplyVarOp(VNVarOp op)
        {
            if (op == null)
                return;

            switch (op.type)
            {
                case VNVarOpType.SetBool:
                    State.SetBool(op.key, op.boolValue);
                    break;

                case VNVarOpType.SetInt:
                    State.SetInt(op.key, op.intValue);
                    break;

                case VNVarOpType.AddInt:
                    State.AddInt(op.key, op.intValue);
                    break;

                case VNVarOpType.SetString:
                    State.SetString(op.key, op.stringValue);
                    break;
            }
        }

        private VNLinePayload BuildLinePayload(VNLineStep line)
        {
            var speakerId = Norm(line.speakerId);
            var isNarrator = string.IsNullOrWhiteSpace(speakerId);

            var speakerName = "";

            if (!isNarrator && project.characterDatabase != null)
                project.characterDatabase.TryGetDisplayName(speakerId, out speakerName);

            return new VNLinePayload
            {
                speakerId = speakerId,
                speakerName = speakerName ?? "",
                isNarrator = isNarrator,
                showSpeakerName = line.showSpeakerName,
                pose = line.pose,
                emotion = line.emotion,
                sfxId = Norm(line.sfxId),
                text = line.text ?? ""
            };
        }

        private void EmitFullScreenState()
        {
            if (!string.IsNullOrWhiteSpace(State.backgroundId))
                EmitBackground(State.backgroundId, 0f);

            if (!string.IsNullOrWhiteSpace(State.musicId))
                EmitMusic(State.musicId, 0f, true);

            State.EnsureSlots();

            var alreadyOneVisible = false;

            for (var i = 0; i < State.slots.Count; i++)
            {
                var s = State.slots[i];
                var shouldShow = s.visible && !string.IsNullOrWhiteSpace(s.characterId);

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

        private void EmitSlot(
            VNScreenSlot slot,
            string characterId,
            VNPose pose,
            VNEmotion emotion,
            bool visible,
            float crossfade,
            bool isNewCharacter)
        {
            characterId = Norm(characterId);

            var name = "";
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

            if (string.IsNullOrWhiteSpace(sfxId))
                return;

            AudioClip clip = null;

            if (project.assetDatabase != null)
                project.assetDatabase.TryGetSfx(sfxId, out clip);

            OnSfxPlay?.Invoke(new VNSfxPayload
            {
                sfxId = sfxId,
                clip = clip
            });
        }

        private void AdvanceToNextStep(VNChapter chapter, int currentIndex, string explicitNextStepId)
        {
            if (TryResolveExplicitOrLinearNext(chapter, currentIndex, explicitNextStepId, out string nextId))
            {
                State.stepId = nextId;
                State.currentStepApplied = false;
                State.currentStepLogged = false;

                VNAutosave.Save(State);
            }

            _advanceRequested = false;
            _interruptWaitRequested = false;
            _lineRevealCompleted = false;
        }

        private bool TryAdvanceToExplicitOrFallback(VNChapter chapter, int currentIndex, string explicitTargetStepId)
        {
            if (TryResolveExplicitOrLinearNext(chapter, currentIndex, explicitTargetStepId, out string nextId))
            {
                State.stepId = nextId;
                State.currentStepApplied = false;
                State.currentStepLogged = false;

                VNAutosave.Save(State);

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

        private bool TryResolveExplicitOrLinearNext(
            VNChapter chapter,
            int currentIndex,
            string explicitTargetStepId,
            out string resolvedStepId)
        {
            resolvedStepId = null;

            if (chapter == null || chapter.steps == null)
                return false;

            var explicitId = Norm(explicitTargetStepId);

            if (!string.IsNullOrEmpty(explicitId) && chapter.TryGetStepIndex(explicitId, out _))
            {
                resolvedStepId = explicitId;
                return true;
            }

            var nextIndex = currentIndex + 1;

            if (nextIndex < 0 || nextIndex >= chapter.steps.Count)
                return false;

            var next = chapter.steps[nextIndex];

            if (next == null || string.IsNullOrWhiteSpace(next.id))
                return false;

            resolvedStepId = next.id.Trim();
            return true;
        }

        private float ComputeAutoDelay(string text)
        {
            if (useFixedAutoReadDelay)
                return Mathf.Max(0f, fixedAutoReadDelaySeconds);

            text ??= "";

            int chars = text.Length;
            int punct = 0;

            for (var i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '.' || c == '!' || c == '?' || c == ',' || c == ';' || c == ':')
                    punct++;
            }

            float delay =
                autoBaseDelaySeconds +
                chars * autoPerCharacterSeconds +
                punct * autoPunctuationExtraSeconds;

            return Mathf.Max(0f, delay);
        }

        private void ApplyMbtiAnswer(VNMbtiLetter letter)
        {
            switch (letter)
            {
                case VNMbtiLetter.E:
                    mbti.E++;
                    break;

                case VNMbtiLetter.I:
                    mbti.I++;
                    break;

                case VNMbtiLetter.S:
                    mbti.S++;
                    break;

                case VNMbtiLetter.N:
                    mbti.N++;
                    break;

                case VNMbtiLetter.T:
                    mbti.T++;
                    break;

                case VNMbtiLetter.F:
                    mbti.F++;
                    break;

                case VNMbtiLetter.J:
                    mbti.J++;
                    break;

                case VNMbtiLetter.P:
                    mbti.P++;
                    break;
            }
        }

        private void ResolveMbtiResult()
        {
            var ei = mbti.E >= mbti.I ? 'E' : 'I';
            var sn = mbti.S >= mbti.N ? 'S' : 'N';
            var tf = mbti.T >= mbti.F ? 'T' : 'F';
            var jp = mbti.J >= mbti.P ? 'J' : 'P';

            mbti.ResultType = $"{ei}{sn}{tf}{jp}";

            if (sn == 'N' && tf == 'T')
            {
                mbti.ArchetypeId = "Logics";
                mbti.ArchetypeName = "Логики";
                mbti.ResultColorHex = "#4A8BFF";
            }
            else if (sn == 'N' && tf == 'F')
            {
                mbti.ArchetypeId = "Diplomats";
                mbti.ArchetypeName = "Дипломаты";
                mbti.ResultColorHex = "#41B86A";
            }
            else if (sn == 'S' && jp == 'J')
            {
                mbti.ArchetypeId = "Defenders";
                mbti.ArchetypeName = "Защитники";
                mbti.ResultColorHex = "#E7C64A";
            }
            else
            {
                mbti.ArchetypeId = "Seekers";
                mbti.ArchetypeName = "Искатели";
                mbti.ResultColorHex = "#E25555";
            }
        }

        private bool TryResolveChapter(out VNChapter chapter)
        {
            chapter = null;

            if (project == null)
                return false;

            return project.TryGetChapter(State.chapterId, out chapter);
        }

        private void ShowMainMenu()
        {
            ClearVisualStateForMainMenu();

            StopInternal();

            SetAuto(false);
            SetSkip(false);

            OnMainMenuRequested?.Invoke();
        }

        private void StopInternal()
        {
            if (_loop != null)
                StopCoroutine(_loop);

            _loop = null;

            _choiceWaiting = false;
            _currentChoiceStep = null;
            _artifactWaiting = false;

            _advanceRequested = false;
            _interruptWaitRequested = false;
            _lineRevealCompleted = false;
            _suppressNextTap = false;

            OnChoiceHidden?.Invoke();
            OnLineHidden?.Invoke();
        }

        public void SetPresentedPlayerName(string value)
        {
            _presentedPlayerName = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();
        }

        private string GetPresentedPlayerName()
        {
            return string.IsNullOrWhiteSpace(_presentedPlayerName) ? "Player" : _presentedPlayerName;
        }

        private void AddChoiceToLog(string choiceText)
        {
            if (string.IsNullOrWhiteSpace(choiceText))
                return;

            State.log.Add(new VNState.LogEntry
            {
                speakerId = "YOU",
                speakerName = GetPresentedPlayerName(),
                text = choiceText
            });
        }

        private void AddToLogAfterReveal(VNLineStep line)
        {
            if (line == null || !line.addToLog || State.currentStepLogged)
                return;

            var speakerId = Norm(line.speakerId);
            var speakerName = "";

            if (!line.showSpeakerName && !string.IsNullOrWhiteSpace(speakerId))
            {
                speakerName = "???";
            }
            else if (string.Equals(speakerId, "YOU", StringComparison.OrdinalIgnoreCase))
            {
                speakerName = GetPresentedPlayerName();
            }
            else if (!string.IsNullOrWhiteSpace(speakerId) && project.characterDatabase != null)
            {
                project.characterDatabase.TryGetDisplayName(speakerId, out speakerName);
            }

            var text = line.text ?? "";

            if (IsPlayerThoughtsLine(speakerId, line.emotion))
                text = WrapItalic(text);

            State.log.Add(new VNState.LogEntry
            {
                speakerId = speakerId,
                speakerName = speakerName ?? "",
                text = text
            });

            State.currentStepLogged = true;
            VNAutosave.Save(State);
        }

        private static bool IsPlayerThoughtsLine(string speakerId, VNEmotion emotion)
        {
            return string.Equals(speakerId, "YOU", StringComparison.OrdinalIgnoreCase)
                   && emotion.ToString().Equals("Thoughts", StringComparison.OrdinalIgnoreCase);
        }

        private static string WrapItalic(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (text.StartsWith("<i>", StringComparison.OrdinalIgnoreCase) &&
                text.EndsWith("</i>", StringComparison.OrdinalIgnoreCase))
                return text;

            return $"<i>{text}</i>";
        }
        private void ClearVisualStateForMainMenu()
        {
            State.EnsureSlots();

            for (int i = 0; i < State.slots.Count; i++)
            {
                var slot = State.slots[i];

                slot.visible = false;
                slot.characterId = null;
                slot.pose = VNPose.Default;
                slot.emotion = VNEmotion.Neutral;

                EmitSlot(
                    slot.slot,
                    null,
                    VNPose.Default,
                    VNEmotion.Neutral,
                    false,
                    0f,
                    false
                );
            }

            State.backgroundId = null;

            OnBackgroundChanged?.Invoke(new VNBackgroundPayload
            {
                backgroundId = null,
                sprite = null,
                crossfadeSeconds = 0f
            });

            VNAutosave.Save(State);
        }
        private static string Norm(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
    }
}