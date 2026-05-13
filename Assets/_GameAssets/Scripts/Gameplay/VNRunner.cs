using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using Spine.Unity;
using VN.UI;
using YsoCorp.GameUtils;

namespace VN
{
    public class VNRunner : MonoBehaviour
    {
        [Header("Project")] [SerializeField] private VNProjectDatabase project;

        [Header("Startup")] [SerializeField] private string startChapterId = "chapter_01";
        [SerializeField] private bool autoLoadAutosaveOnStart = true;
        [SerializeField] private bool autoShowSpeakerIfMissing = true;

        [Header("Startup Control")] [SerializeField]
        private bool startAutomatically = true;

        [Header("Auto timing")] [SerializeField]
        private bool useFixedAutoReadDelay = true;

        [SerializeField] [Min(0f)] private float fixedAutoReadDelaySeconds = 1.2f;

        [Tooltip("Используется только если Use Fixed Auto Read Delay выключен.")] [SerializeField] [Min(0f)]
        private float autoBaseDelaySeconds = 0.8f;

        [Tooltip("Используется только если Use Fixed Auto Read Delay выключен.")] [SerializeField] [Min(0f)]
        private float autoPerCharacterSeconds = 0.03f;

        [Tooltip("Используется только если Use Fixed Auto Read Delay выключен.")] [SerializeField] [Min(0f)]
        private float autoPunctuationExtraSeconds = 0.25f;

        [Tooltip("Если включено, авточтение будет выключаться при появлении нового персонажа.")] [SerializeField]
        private bool stopAutoOnNewCharacter;

        [Header("Skip")] [SerializeField] [Min(0f)]
        private float skipStepFrameDelay;

        [Header("MBTI")] [SerializeField] private VNMbtiState mbti = new();
        public VNMbtiState Mbti => mbti;

        [Header("MBTI Intro Video")]
        [Tooltip("Если включено, VNResolveMbtiCommand вместо строки с типом личности запускает интро-видео наставника по выпавшему архетипу.")]
        [SerializeField] private bool playMbtiIntroVideoAfterTest = true;

        [Tooltip("Скрывать персонажей поверх интро-видео. Диалоговая плашка остается видимой, чтобы текст шел обычным typewriter-чтением.")]
        [SerializeField] private bool hideCharactersDuringMbtiIntro = true;

        [Tooltip("Включать звук, встроенный в сам VideoClip. Обычно выключено, потому что музыка/SFX идут отдельными ключами из SOUND.")]
        [SerializeField] private bool playMbtiIntroVideoAudio = false;

        [SerializeField, Min(0f)] private float mbtiIntroVideoFadeInSeconds = 0.15f;
        [SerializeField, Min(0f)] private float mbtiIntroVideoFadeOutSeconds = 0.15f;
        [SerializeField, Min(0f)] private float mbtiIntroMusicFadeInSeconds = 0.35f;
        [SerializeField, Min(0f)] private float mbtiIntroMusicFadeOutSeconds = 0.35f;
        [SerializeField] private bool stopMbtiIntroMusicOnFinish = true;

        [Header("MBTI Intro Volume")]
        [Tooltip("Обычная громкость музыки интро. Если текущий SOUND не поддерживает громкость на отдельном PlayMusic, значение будет проигнорировано и звук сыграет как раньше.")]
        [SerializeField, Range(0f, 1f)] private float normalMbtiIntroMusicVolume = 1f;

        [Tooltip("Тихая громкость для интро Kensui/Hinato.")]
        [SerializeField, Range(0f, 1f)] private float quietMbtiIntroMusicVolume = 0.45f;

        [Tooltip("Обычная громкость SFX интро. Если текущий SOUND не поддерживает громкость на отдельном PlaySFX, значение будет проигнорировано.")]
        [SerializeField, Range(0f, 1f)] private float normalMbtiIntroSfxVolume = 1f;

        [Tooltip("Тихая громкость для Amb_Wind и других шумных SFX.")]
        [SerializeField, Range(0f, 1f)] private float quietMbtiIntroSfxVolume = 0.35f;

        [Header("Character auto show")] [SerializeField] [Min(0f)]
        private float autoSpeakerCrossfadeSeconds = 0.2f;

        [Header("VFX")] [SerializeField] private VNVfxPlayer vfxPlayer;

        [Header("UI")] [SerializeField] private GameObject mainMenuRoot;
        [SerializeField] private VNTruthEyeMinigameUGUI truthEyeMinigame;

        [Header("Location Intro")]
        [Tooltip("При первом показе нового backgroundId или смене backgroundId делает короткое скольжение камеры.")]
        [SerializeField] private bool playLocationIntroOnBackgroundChange = true;

        [Tooltip("Длительность скольжения по умолчанию. По ТЗ держим 1-2 секунды.")]
        [SerializeField, Range(1f, 2f)] private float defaultLocationIntroDurationSeconds = 1.5f;

        [Tooltip("Скрывать всех персонажей перед скольжением, чтобы локация показывалась пустой.")]
        [SerializeField] private bool clearCharactersBeforeLocationIntro = true;

        [Tooltip("Полностью выключать любую текущую музыку перед скольжением. Следующая VNPlayMusicCommand сработает уже после интро.")]
        [SerializeField] private bool stopMusicBeforeLocationIntro = true;


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
        public event Action<VNPremiumChoiceRejectedPayload> OnPremiumChoiceRejected;
        public event Action<VNPremiumChoicePaidPayload> OnPremiumChoicePaid;
        public event Action<VNCurrencyRewardPayload> OnCrystalsRewardRequested;

        public event Action<VNBackgroundPayload> OnBackgroundChanged;
        public event Action<VNLocationIntroPayload> OnLocationIntroStarted;
        public event Action OnLocationIntroFinished;
        public event Action<VNSlotPayload> OnSlotChanged;

        public event Action<VNMusicPayload> OnMusicPlay;
        public event Action<float> OnMusicStop;
        public event Action<VNSfxPayload> OnSfxPlay;
        public event Action<VNArtifactPayload> OnArtifactShown;
        public event Action<VNCutscenePayload> OnCutsceneShown;
        public event Action<VNCutsceneHidePayload> OnCutsceneHidden;

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
        public struct VNPremiumChoiceRejectedPayload
        {
            public string stepId;
            public int optionIndex;
            public int price;
            public int balance;
        }

        [Serializable]
        public struct VNPremiumChoicePaidPayload
        {
            public string stepId;
            public int optionIndex;
            public int price;
            public int balanceBefore;
            public int balanceAfter;
        }

        [Serializable]
        public struct VNCurrencyRewardPayload
        {
            public int amount;
        }

        [Serializable]
        public struct VNBackgroundPayload
        {
            public string backgroundId;
            public Sprite sprite;
            public float crossfadeSeconds;
        }

        [Serializable]
        public struct VNLocationIntroPayload
        {
            public string backgroundId;
            // Location intro plate temporarily disabled.
            // Keep these fields for quick restore when the plate is needed again.
            // public string locationName;
            // public string timeOfDay;
            public float durationSeconds;
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

            public bool hasSpine;
            public SkeletonDataAsset spineSkeletonDataAsset;
            public string spineBaseSkinName;
            public string spineSkinName;
            public string spineAnimationName;
            public bool spineLoop;
            public IReadOnlyList<string> spineEmotionSlotsToClear;

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
            public float volume;
        }

        [Serializable]
        public struct VNSfxPayload
        {
            public string sfxId;
            public AudioClip clip;
            public float volume;
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

        [Serializable]
        public struct VNCutscenePayload
        {
            public string cutsceneId;
            public VideoClip clip;
            public bool hideDialogue;
            public bool hideCharacters;
            public bool blockInput;
            public float fadeInSeconds;
            public bool playAudio;
            public float audioVolume;
        }

        [Serializable]
        public struct VNCutsceneHidePayload
        {
            public float fadeOutSeconds;
        }

        private Coroutine _loop;

        private bool _lineRevealCompleted;
        private bool _advanceRequested;
        private bool _interruptWaitRequested;
        private bool _modalOpen;
        private bool _suppressNextTap;
        public VNTruthEyeMinigameUGUI.Result LastTruthEyeResult { get; private set; }
        public event Action<bool> OnTruthEyeMinigameActiveChanged;
        private bool _truthEyeMinigameActive;
        private string _presentedPlayerName = "Player";

        private bool _choiceWaiting;
        private VNChoiceStep _currentChoiceStep;

        private bool _autoStopDueToNewCharacterThisStep;

        private bool _artifactWaiting;
        private bool _locationIntroActive;
        private Coroutine _locationIntroCoroutine;
        private bool _locationIntroPreviousSkipAllowed = true;
        private bool _mbtiIntroActive;

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

            if (_locationIntroActive)
                return;

            if (State.cutsceneVisible && State.cutsceneBlockInput)
                return;

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
            if ((_locationIntroActive || _mbtiIntroActive) && enabled)
                enabled = false;

            if (AutoEnabled == enabled)
                return;

            AutoEnabled = enabled;
            OnAutoChanged?.Invoke(AutoEnabled);
        }

        public void SetSkip(bool enabled)
        {
            if ((_locationIntroActive || _mbtiIntroActive) && enabled)
                enabled = false;

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
            if (opt == null)
                return;

            var next = Norm(opt.nextStepId);
            if (string.IsNullOrEmpty(next))
                return;

            var price = GetPremiumPrice(opt);
            if (price > 0)
            {
                var balanceBefore = VNCrystalWallet.Balance;

                if (!VNCrystalWallet.TrySpend(price))
                {
                    OnPremiumChoiceRejected?.Invoke(new VNPremiumChoiceRejectedPayload
                    {
                        stepId = State.stepId,
                        optionIndex = optionIndex,
                        price = price,
                        balance = VNCrystalWallet.Balance
                    });
                    return;
                }

                var balanceAfter = VNCrystalWallet.Balance;

                CoinFxManager.PlayCrystalCurrencySfxGlobal();

                OnPremiumChoicePaid?.Invoke(new VNPremiumChoicePaidPayload
                {
                    stepId = State.stepId,
                    optionIndex = optionIndex,
                    price = price,
                    balanceBefore = balanceBefore,
                    balanceAfter = balanceAfter
                });
            }

            AddChoiceToLog(opt.text);

            if (opt.effects != null)
                for (var i = 0; i < opt.effects.Count; i++)
                    ApplyVarOp(opt.effects[i]);

            _choiceWaiting = false;
            _currentChoiceStep = null;

            OnChoiceHidden?.Invoke();

            State.stepId = next;
            State.currentStepApplied = false;
            State.currentStepLogged = false;

            VNAutosave.Save(State);
        }

        public bool CanChooseCurrentOption(int optionIndex)
        {
            if (!_choiceWaiting || _currentChoiceStep == null || _currentChoiceStep.options == null)
                return false;

            if (optionIndex < 0 || optionIndex >= _currentChoiceStep.options.Count)
                return false;

            var opt = _currentChoiceStep.options[optionIndex];
            return opt != null && VNCrystalWallet.CanSpend(GetPremiumPrice(opt));
        }

        private static int GetPremiumPrice(VNChoiceOption option)
        {
            if (option == null || option.kind != VNChoiceKind.Premium)
                return 0;

            return Mathf.Max(0, option.premiumPrice);
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
                    if (_locationIntroActive)
                        yield return WaitForLocationIntroToFinish();

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
            if (_locationIntroActive)
            {
                yield return WaitForLocationIntroToFinish();
                ApplySkipAllowedForStep(line);
            }

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

            var stopAutoHere = IsStopAutoHere(line) || _autoStopDueToNewCharacterThisStep;
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
                    var delay = ComputeAutoDelay(payload.text);
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
            if (_locationIntroActive)
            {
                yield return WaitForLocationIntroToFinish();
                ApplySkipAllowedForStep(choice);
            }

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

            while (_choiceWaiting) yield return null;
        }

        private IEnumerator HandleCommandStep(VNChapter chapter, int index, VNCommandStep cmdStep)
        {
            _advanceRequested = false;
            _interruptWaitRequested = false;

            var stopAutoHere = IsStopAutoHere(cmdStep);

            if (!State.currentStepApplied)
            {
                if (cmdStep.command is VNSetBackgroundCommand backgroundCommand)
                {
                    yield return HandleSetBackgroundCommand(backgroundCommand);
                }
                else if (cmdStep.command is VNVfxCommand vfxCommand)
                {
                    var shouldWaitForVfx = vfxCommand.waitUntilFinished && !(SkipEnabled && SkipAllowed);
                    yield return StartCoroutine(HandleVfxCommand(vfxCommand, shouldWaitForVfx));
                }
                else if (cmdStep.command is VNTruthEyeCommand truthEyeCommand)
                {
                    if (AutoEnabled)
                        SetAuto(false);

                    if (SkipEnabled)
                        SetSkip(false);

                    stopAutoHere = true;

                    yield return StartCoroutine(HandleTruthEyeCommand(truthEyeCommand));
                }
                else if (cmdStep.command is VNResolveMbtiCommand resolveMbtiCommand)
                {
                    yield return StartCoroutine(HandleResolveMbtiCommand(resolveMbtiCommand));

                    // Интро уже само показало все строки и дождалось клика после последней.
                    // Дополнительный WaitManualAdvance здесь не нужен.
                    stopAutoHere = false;
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
                if (cmdStep.command is not VNTruthEyeCommand)
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
            var duration = Mathf.Max(0f, seconds);
            var elapsed = 0f;

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
            var duration = Mathf.Max(0f, seconds);
            var elapsed = 0f;

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
            if (_locationIntroActive || _mbtiIntroActive)
                allowed = false;

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

            if (step is VNCommandStep truthEye && truthEye.command is VNTruthEyeCommand)
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

        private IEnumerator HandleSetBackgroundCommand(VNSetBackgroundCommand command)
        {
            if (command == null)
                yield break;

            var previousBackgroundId = State.backgroundId;
            var nextBackgroundId = Norm(command.backgroundId);
            var shouldPlayIntro = ShouldPlayLocationIntro(command, previousBackgroundId, nextBackgroundId);

            if (shouldPlayIntro)
            {
                if (AutoEnabled)
                    SetAuto(false);

                if (SkipEnabled)
                    SetSkip(false);

                if (clearCharactersBeforeLocationIntro)
                    HideAllCharacters(0f);

                if (stopMusicBeforeLocationIntro)
                {
                    // Важно: останавливаем музыку без проверки State.musicId.
                    // В Sound могла играть музыка, запущенная не через VNRunner (например меню/предыдущая сцена),
                    // поэтому перед интро локации всегда шлем жесткий stop с нулевым fade.
                    State.musicId = null;
                    OnMusicStop?.Invoke(0f);
                }
            }

            State.backgroundId = nextBackgroundId;
            EmitBackground(command.backgroundId, command.crossfadeSeconds);

            // Важно: интро локации больше НЕ блокирует выполнение следующих command-step.
            // Так команды музыки/скрытия/показа персонажей, которые идут сразу после background,
            // успевают примениться во время скольжения, а текст/выбор дождутся конца интро отдельно.
            if (shouldPlayIntro)
                StartLocationIntro(command, nextBackgroundId);

            yield return null;
        }

        private bool ShouldPlayLocationIntro(VNSetBackgroundCommand command, string previousBackgroundId, string nextBackgroundId)
        {
            if (!playLocationIntroOnBackgroundChange || command == null || !command.playLocationIntro)
                return false;

            if (string.IsNullOrWhiteSpace(nextBackgroundId))
                return false;

            if (command.forceLocationIntro)
                return true;

            return !string.Equals(Norm(previousBackgroundId), Norm(nextBackgroundId), StringComparison.Ordinal);
        }

        private void StartLocationIntro(VNSetBackgroundCommand command, string backgroundId)
        {
            StopLocationIntro(sendFinishedEvent: true, restoreSkipAllowed: true);

            var duration = ResolveLocationIntroDuration(command);
            // Location intro plate temporarily disabled.
            // ResolveLocationIntroText(command, backgroundId, out var locationName, out var timeOfDay);

            _locationIntroPreviousSkipAllowed = SkipAllowed;
            _locationIntroActive = true;
            SetSkipAllowed(false);

            OnLocationIntroStarted?.Invoke(new VNLocationIntroPayload
            {
                backgroundId = backgroundId,
                // Location intro plate temporarily disabled.
                // locationName = locationName,
                // timeOfDay = timeOfDay,
                durationSeconds = duration
            });

            _locationIntroCoroutine = StartCoroutine(LocationIntroRoutine(duration));
        }

        private IEnumerator LocationIntroRoutine(float duration)
        {
            var elapsed = 0f;
            var waitSeconds = Mathf.Max(0f, duration);

            while (elapsed < waitSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            _locationIntroCoroutine = null;
            StopLocationIntro(sendFinishedEvent: true, restoreSkipAllowed: true);
        }

        private void StopLocationIntro(bool sendFinishedEvent, bool restoreSkipAllowed)
        {
            if (_locationIntroCoroutine != null)
            {
                StopCoroutine(_locationIntroCoroutine);
                _locationIntroCoroutine = null;
            }

            if (!_locationIntroActive)
                return;

            _locationIntroActive = false;

            if (sendFinishedEvent)
                OnLocationIntroFinished?.Invoke();

            if (restoreSkipAllowed)
                SetSkipAllowed(_locationIntroPreviousSkipAllowed);
        }

        private IEnumerator WaitForLocationIntroToFinish()
        {
            while (_locationIntroActive)
                yield return null;
        }

        private float ResolveLocationIntroDuration(VNSetBackgroundCommand command)
        {
            var duration = defaultLocationIntroDurationSeconds;

            if (command != null && command.locationIntroDurationOverride > 0f)
                duration = command.locationIntroDurationOverride;

            return Mathf.Clamp(duration, 1f, 2f);
        }

        // Location intro plate temporarily disabled.
        // Keep this method for quick restore when the plate is needed again.
        /*
        private void ResolveLocationIntroText(
            VNSetBackgroundCommand command,
            string backgroundId,
            out string locationName,
            out string timeOfDay)
        {
            locationName = Norm(command != null ? command.locationName : null);
            timeOfDay = Norm(command != null ? command.timeOfDay : null);

            if (project != null && project.assetDatabase != null)
            {
                if (project.assetDatabase.TryGetBackgroundLocationInfo(backgroundId, out var dbLocationName, out var dbTimeOfDay))
                {
                    if (string.IsNullOrWhiteSpace(locationName))
                        locationName = dbLocationName;

                    if (string.IsNullOrWhiteSpace(timeOfDay))
                        timeOfDay = dbTimeOfDay;
                }
            }

            if (string.IsNullOrWhiteSpace(locationName))
                locationName = backgroundId;
        }
        */

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
                    // VNResolveMbtiCommand обрабатывается корутиной HandleResolveMbtiCommand,
                    // потому что интро-видео содержит несколько typewriter-строк и ожидание тапов.
                    stopAutoHere = true;
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

                case VNGiveCrystalsCommand giveCrystals:
                    GiveCrystals(giveCrystals.amount, giveCrystals.playFlyAnimation);
                    break;

                case VNVibrationCommand vibration:
                    PlayVibrationCommand(vibration);
                    break;

                case VNShowCutsceneCommand showCutscene:
                    ShowCutscene(showCutscene);
                    break;

                case VNHideCutsceneCommand hideCutscene:
                    HideCutscene(hideCutscene);
                    break;
            }
        }

        private void PlayVibrationCommand(VNVibrationCommand command)
        {
            if (command == null)
                return;

            int pulseCount = Mathf.Max(1, command.pulseCount);

            if (pulseCount <= 1 || !isActiveAndEnabled)
            {
                VNVibration.Play(command.feedbackType);
                return;
            }

            StartCoroutine(PlayVibrationRoutine(command.feedbackType, pulseCount, command.pulseIntervalSeconds));
        }

        private IEnumerator PlayVibrationRoutine(VNHapticFeedbackType feedbackType, int pulseCount, float pulseIntervalSeconds)
        {
            pulseCount = Mathf.Max(1, pulseCount);
            pulseIntervalSeconds = Mathf.Max(0f, pulseIntervalSeconds);

            for (int i = 0; i < pulseCount; i++)
            {
                VNVibration.Play(feedbackType);

                if (i < pulseCount - 1 && pulseIntervalSeconds > 0f)
                    yield return new WaitForSecondsRealtime(pulseIntervalSeconds);
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
                while (!handle.IsFinished)
                    yield return null;
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

            // Narrator/player/unknown speakers must not clear the last visible character.
            // Auto character switching is only allowed for real characters that have a sprite or Spine visual.
            if (!CanAutoApplySpeakerVisual(speakerId, pose, emotion))
                return;

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

        private bool CanAutoApplySpeakerVisual(string speakerId, VNPose pose, VNEmotion emotion)
        {
            speakerId = Norm(speakerId);

            if (string.IsNullOrWhiteSpace(speakerId))
                return false;

            if (IsPlayerSpeakerId(speakerId))
                return false;

            if (project == null || project.characterDatabase == null)
                return false;

            if (!project.characterDatabase.TryGetCharacter(speakerId, out _))
                return false;

            if (project.characterDatabase.TryGetSprite(speakerId, pose, emotion, out var sprite) && sprite != null)
                return true;

            if (project.characterDatabase.TryGetSpineAnimation(speakerId, pose, emotion, out var spine) && spine.skeletonDataAsset != null)
                return true;

            return false;
        }

        private static bool IsPlayerSpeakerId(string speakerId)
        {
            return string.Equals(speakerId, "YOU", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(speakerId, "PLAYER", StringComparison.OrdinalIgnoreCase);
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
                var isTarget = s.slot == targetSlot;

                if (isTarget || !s.visible)
                    continue;

                s.visible = false;
                s.characterId = null;
                s.pose = VNPose.Default;
                s.emotion = VNEmotion.Neutral;

                EmitSlot(s.slot, null, VNPose.Default, VNEmotion.Neutral, false, fade, false);
            }

            var targetState = State.GetSlot(targetSlot);

            var wasSameCharacterAlreadyVisible =
                targetState.visible &&
                string.Equals(targetState.characterId, characterId, StringComparison.Ordinal);

            targetState.visible = true;
            targetState.characterId = characterId;
            targetState.pose = pose;
            targetState.emotion = emotion;

            var emitFade = wasSameCharacterAlreadyVisible ? 0f : fade;
            var isNewCharacter = !wasSameCharacterAlreadyVisible;

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

        private IEnumerator HandleResolveMbtiCommand(VNResolveMbtiCommand command)
        {
            ResolveMbtiResult();

            State.mbti = mbti;
            VNAutosave.Save(State);

            if (AutoEnabled)
                SetAuto(false);

            if (SkipEnabled)
                SetSkip(false);

            var previousSkipAllowed = SkipAllowed;
            _mbtiIntroActive = true;
            SetSkipAllowed(false);

            if (playMbtiIntroVideoAfterTest && TryBuildMbtiIntroDefinition(out var intro))
                yield return PlayMbtiIntroRoutine(intro);
            else
                yield return ShowMbtiResultLineRoutine();

            _mbtiIntroActive = false;
            SetSkipAllowed(previousSkipAllowed);
        }

        private IEnumerator PlayMbtiIntroRoutine(VNMbtiIntroDefinition intro)
        {
            if (intro == null)
                yield break;

            if (hideCharactersDuringMbtiIntro)
                HideAllCharacters(0f);

            if (!string.IsNullOrWhiteSpace(intro.musicId))
            {
                State.musicId = Norm(intro.musicId);
                EmitMusic(intro.musicId, mbtiIntroMusicFadeInSeconds, true, intro.musicVolume);
            }

            if (!string.IsNullOrWhiteSpace(intro.cutsceneId))
            {
                ShowCutscene(new VNShowCutsceneCommand
                {
                    cutsceneId = intro.cutsceneId,
                    hideDialogue = false,
                    hideCharacters = hideCharactersDuringMbtiIntro,
                    blockInput = false,
                    fadeInSeconds = mbtiIntroVideoFadeInSeconds,
                    playAudio = playMbtiIntroVideoAudio,
                    audioVolume = 1f
                });
            }

            if (intro.sfxIds != null)
            {
                for (var i = 0; i < intro.sfxIds.Length; i++)
                {
                    var sfxId = intro.sfxIds[i];
                    if (string.IsNullOrWhiteSpace(sfxId))
                        continue;

                    var volume = intro.sfxVolumes != null && i < intro.sfxVolumes.Length
                        ? intro.sfxVolumes[i]
                        : normalMbtiIntroSfxVolume;

                    EmitSfx(sfxId, volume);
                }
            }

            if (intro.lines != null)
            {
                for (var i = 0; i < intro.lines.Length; i++)
                    yield return PlayRuntimeDialogueLine(
                        intro.speakerId,
                        intro.speakerName,
                        intro.lines[i],
                        addToLog: true);
            }

            HideCutscene(new VNHideCutsceneCommand { fadeOutSeconds = mbtiIntroVideoFadeOutSeconds });

            if (stopMbtiIntroMusicOnFinish && !string.IsNullOrWhiteSpace(intro.musicId))
            {
                State.musicId = null;
                OnMusicStop?.Invoke(Mathf.Max(0f, mbtiIntroMusicFadeOutSeconds));
            }
        }

        private IEnumerator ShowMbtiResultLineRoutine()
        {
            yield return PlayRuntimeDialogueLine(
                null,
                "",
                $"<color={mbti.ResultColorHex}>{mbti.ResultType}</color>",
                addToLog: false,
                narrator: true);
        }

        private IEnumerator PlayRuntimeDialogueLine(
            string speakerId,
            string speakerName,
            string text,
            bool addToLog,
            bool narrator = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            _advanceRequested = false;
            _interruptWaitRequested = false;
            _lineRevealCompleted = false;

            var payload = new VNLinePayload
            {
                speakerId = Norm(speakerId),
                speakerName = speakerName ?? "",
                isNarrator = narrator,
                showSpeakerName = true,
                pose = VNPose.Default,
                emotion = VNEmotion.Neutral,
                sfxId = null,
                text = text
            };

            OnLineStarted?.Invoke(payload);

            yield return WaitUntilLineRevealed();

            if (addToLog)
                AddRuntimeLineToLog(payload);

            yield return WaitManualAdvance();

            OnLineHidden?.Invoke();
        }

        private bool TryBuildMbtiIntroDefinition(out VNMbtiIntroDefinition intro)
        {
            var archetypeId = Norm(mbti.ArchetypeId);

            if (string.Equals(archetypeId, "Logics", StringComparison.OrdinalIgnoreCase))
            {
                intro = new VNMbtiIntroDefinition
                {
                    speakerId = "Shinrai",
                    speakerName = "Shinrai",
                    cutsceneId = "Shinrai_intro",
                    musicId = "Shinrai_intro_3",
                    musicVolume = normalMbtiIntroMusicVolume,
                    sfxIds = new[] { "Mgc_Water_Throw_02" },
                    sfxVolumes = new[] { normalMbtiIntroSfxVolume },
                    lines = new[]
                    {
                        "Welcome to the Order of the Labyrinth, esteemed one.",
                        "I invite you to assist me in shaping the future of the academy and the world beyond, for this is our purpose, and this is who we are."
                    }
                };
                return true;
            }

            if (string.Equals(archetypeId, "Defenders", StringComparison.OrdinalIgnoreCase))
            {
                intro = new VNMbtiIntroDefinition
                {
                    speakerId = "Kaitora",
                    speakerName = "Kaitora",
                    cutsceneId = "Kaitora_intro",
                    musicId = "Kaitora_1",
                    musicVolume = normalMbtiIntroMusicVolume,
                    sfxIds = Array.Empty<string>(),
                    sfxVolumes = Array.Empty<float>(),
                    lines = new[]
                    {
                        "Welcome to Legion of Sentinels. Together, we will honor and protect the Academy and our ancient magic.",
                        "Together we stand, free in heart, strong in mind."
                    }
                };
                return true;
            }

            if (string.Equals(archetypeId, "Diplomats", StringComparison.OrdinalIgnoreCase))
            {
                intro = new VNMbtiIntroDefinition
                {
                    speakerId = "Kensui",
                    speakerName = "Kensui",
                    cutsceneId = "Kensui_intro",
                    musicId = "Kensui_1",
                    musicVolume = normalMbtiIntroMusicVolume,
                    sfxIds = new[] { "Amb_Wind" },
                    sfxVolumes = new[] { quietMbtiIntroSfxVolume },
                    lines = new[]
                    {
                        "I welcome you to the Temple of the Everveil, dear heart.",
                        "Here you will unlock the potential of your soul and help your fellow scholars do the same.",
                        "Your gentle nature is your greatest gift, gracious one."
                    }
                };
                return true;
            }

            if (string.Equals(archetypeId, "Seekers", StringComparison.OrdinalIgnoreCase))
            {
                intro = new VNMbtiIntroDefinition
                {
                    speakerId = "Hinato",
                    speakerName = "Hinato",
                    cutsceneId = "Hinato_intro",
                    musicId = "Hinato_2",
                    musicVolume = quietMbtiIntroMusicVolume,
                    // Берем один огненный SFX, чтобы интро не шумело слишком сильно.
                    sfxIds = new[] { "Mgc_Fire_Hold_01" },
                    sfxVolumes = new[] { normalMbtiIntroSfxVolume },
                    lines = new[]
                    {
                        "Congratulations! You're part of the Guild of the Flow. Honestly, I knew you would be. I could see it in your eyes.",
                        "Your thoughts and feelings are now one with the Flow, and a tremendous source of power will open its doors to you... as long as you're not scared."
                    }
                };
                return true;
            }

            intro = null;
            Debug.LogWarning($"[VNRunner] MBTI intro not found for archetype '{archetypeId ?? "<empty>"}'. Falling back to plain MBTI result line.");
            return false;
        }

        private sealed class VNMbtiIntroDefinition
        {
            public string speakerId;
            public string speakerName;
            public string cutsceneId;
            public string musicId;
            public float musicVolume = 1f;
            public string[] sfxIds;
            public float[] sfxVolumes;
            public string[] lines;
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

                var ok = EvaluateSingleCondition(c);

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
                    var v = State.GetBool(key);
                    var t = c.boolValue;

                    return c.op switch
                    {
                        VNConditionOp.Equals => v == t,
                        VNConditionOp.NotEquals => v != t,
                        _ => false
                    };
                }

                case VNConditionValueType.Int:
                {
                    var v = State.GetInt(key);
                    var t = c.intValue;

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
                    var v = State.GetString(key);
                    var t = c.stringValue ?? "";

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

            if (State.cutsceneVisible && !string.IsNullOrWhiteSpace(State.cutsceneId))
                EmitCutsceneFromState(0f);
            else
                OnCutsceneHidden?.Invoke(new VNCutsceneHidePayload { fadeOutSeconds = 0f });
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
            var hasSpine = false;
            SkeletonDataAsset spineSkeletonDataAsset = null;
            var spineBaseSkinName = "";
            var spineSkinName = "";
            var spineAnimationName = "";
            var spineLoop = true;
            IReadOnlyList<string> spineEmotionSlotsToClear = null;

            if (visible && !string.IsNullOrWhiteSpace(characterId) && project.characterDatabase != null)
            {
                project.characterDatabase.TryGetDisplayName(characterId, out name);
                project.characterDatabase.TryGetSprite(characterId, pose, emotion, out sprite);

                if (project.characterDatabase.TryGetSpineAnimation(characterId, pose, emotion, out var spine))
                {
                    hasSpine = spine.skeletonDataAsset != null;
                    spineSkeletonDataAsset = spine.skeletonDataAsset;
                    spineBaseSkinName = spine.baseSkinName ?? "";
                    spineSkinName = spine.skinName ?? "";
                    spineAnimationName = spine.animationName ?? "";
                    spineLoop = spine.loop;
                    spineEmotionSlotsToClear = spine.emotionSlotsToClear;
                }
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
                hasSpine = hasSpine,
                spineSkeletonDataAsset = spineSkeletonDataAsset,
                spineBaseSkinName = spineBaseSkinName,
                spineSkinName = spineSkinName,
                spineAnimationName = spineAnimationName,
                spineLoop = spineLoop,
                spineEmotionSlotsToClear = spineEmotionSlotsToClear,
                crossfadeSeconds = Mathf.Max(0f, crossfade),
                isNewCharacter = isNewCharacter
            });
        }

        private void EmitMusic(string musicId, float fadeIn, bool loop, float volume = 1f)
        {
            musicId = Norm(musicId);
            AudioClip clip = null;

            if (!string.IsNullOrWhiteSpace(musicId) && project != null && project.assetDatabase != null)
                project.assetDatabase.TryGetMusic(musicId, out clip);

            OnMusicPlay?.Invoke(new VNMusicPayload
            {
                musicId = musicId,
                clip = clip,
                fadeInSeconds = Mathf.Max(0f, fadeIn),
                loop = loop,
                volume = Mathf.Clamp01(volume)
            });
        }

        private void ShowCutscene(VNShowCutsceneCommand command)
        {
            if (command == null)
                return;

            var cutsceneId = Norm(command.cutsceneId);
            var clip = command.clipOverride;

            if (clip == null && !string.IsNullOrWhiteSpace(cutsceneId) && project != null && project.assetDatabase != null)
                project.assetDatabase.TryGetCutscene(cutsceneId, out clip);

            if (clip == null)
            {
                Debug.LogWarning($"[VNRunner] Cutscene video not found. ID: '{cutsceneId ?? "<empty>"}'.");
                return;
            }

            State.cutsceneVisible = true;
            State.cutsceneId = cutsceneId;
            State.cutsceneHideDialogue = command.hideDialogue;
            State.cutsceneHideCharacters = command.hideCharacters;
            State.cutsceneBlockInput = command.blockInput;
            State.cutscenePlayAudio = command.playAudio;
            State.cutsceneAudioVolume = Mathf.Clamp01(command.audioVolume);

            if (string.IsNullOrWhiteSpace(cutsceneId) && command.clipOverride != null)
            {
                Debug.LogWarning(
                    "[VNRunner] Cutscene uses Clip Override without cutsceneId. It will play now, but it cannot be restored from autosave after reload.");
            }

            OnCutsceneShown?.Invoke(new VNCutscenePayload
            {
                cutsceneId = cutsceneId,
                clip = clip,
                hideDialogue = command.hideDialogue,
                hideCharacters = command.hideCharacters,
                blockInput = command.blockInput,
                fadeInSeconds = Mathf.Max(0f, command.fadeInSeconds),
                playAudio = command.playAudio,
                audioVolume = Mathf.Clamp01(command.audioVolume)
            });
        }

        private void HideCutscene(VNHideCutsceneCommand command)
        {
            State.cutsceneVisible = false;
            State.cutsceneId = null;
            State.cutsceneHideDialogue = false;
            State.cutsceneHideCharacters = false;
            State.cutsceneBlockInput = false;
            State.cutscenePlayAudio = true;
            State.cutsceneAudioVolume = 1f;

            OnCutsceneHidden?.Invoke(new VNCutsceneHidePayload
            {
                fadeOutSeconds = Mathf.Max(0f, command != null ? command.fadeOutSeconds : 0f)
            });
        }

        private void EmitCutsceneFromState(float fadeInSeconds)
        {
            var cutsceneId = Norm(State.cutsceneId);
            if (string.IsNullOrWhiteSpace(cutsceneId) || project == null || project.assetDatabase == null)
                return;

            if (!project.assetDatabase.TryGetCutscene(cutsceneId, out var clip) || clip == null)
            {
                Debug.LogWarning($"[VNRunner] Saved cutscene video not found. ID: '{cutsceneId}'.");
                return;
            }

            OnCutsceneShown?.Invoke(new VNCutscenePayload
            {
                cutsceneId = cutsceneId,
                clip = clip,
                hideDialogue = State.cutsceneHideDialogue,
                hideCharacters = State.cutsceneHideCharacters,
                blockInput = State.cutsceneBlockInput,
                fadeInSeconds = Mathf.Max(0f, fadeInSeconds),
                playAudio = State.cutscenePlayAudio,
                audioVolume = Mathf.Clamp01(State.cutsceneAudioVolume)
            });
        }

        private void GiveCrystals(int amount, bool playFlyAnimation)
        {
            amount = Mathf.Max(0, amount);
            if (amount <= 0)
                return;

            var handler = OnCrystalsRewardRequested;
            if (playFlyAnimation && handler != null)
            {
                handler.Invoke(new VNCurrencyRewardPayload { amount = amount });
                return;
            }

            VNCrystalWallet.Add(amount);
            CoinFxManager.PlayCrystalCurrencySfxGlobal();
        }

        private void EmitSfx(string sfxId, float volume = 1f)
        {
            sfxId = Norm(sfxId);

            if (string.IsNullOrWhiteSpace(sfxId))
                return;

            AudioClip clip = null;

            if (project != null && project.assetDatabase != null)
                project.assetDatabase.TryGetSfx(sfxId, out clip);

            OnSfxPlay?.Invoke(new VNSfxPayload
            {
                sfxId = sfxId,
                clip = clip,
                volume = Mathf.Clamp01(volume)
            });
        }

        private void AdvanceToNextStep(VNChapter chapter, int currentIndex, string explicitNextStepId)
        {
            if (TryResolveExplicitOrLinearNext(chapter, currentIndex, explicitNextStepId, out var nextId))
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
            if (TryResolveExplicitOrLinearNext(chapter, currentIndex, explicitTargetStepId, out var nextId))
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

            var chars = text.Length;
            var punct = 0;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (c == '.' || c == '!' || c == '?' || c == ',' || c == ';' || c == ':')
                    punct++;
            }

            var delay =
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

            StopLocationIntro(sendFinishedEvent: true, restoreSkipAllowed: false);

            SetTruthEyeMinigameActive(false);
            
            OnChoiceHidden?.Invoke();
            OnLineHidden?.Invoke();
            OnCutsceneHidden?.Invoke(new VNCutsceneHidePayload { fadeOutSeconds = 0f });
        }
        private void SetTruthEyeMinigameActive(bool active)
        {
            if (_truthEyeMinigameActive == active)
                return;

            _truthEyeMinigameActive = active;
            OnTruthEyeMinigameActiveChanged?.Invoke(active);
        }
        private IEnumerator HandleTruthEyeCommand(VNTruthEyeCommand command)
        {
            if (command == null)
                yield break;

            if (truthEyeMinigame == null)
            {
                Debug.LogWarning("[VNRunner] Truth Eye minigame is not assigned. Command skipped.");
                yield break;
            }

            VNTruthEyeMinigameUGUI.Result result = default;
            var done = false;

            if (AutoEnabled)
                SetAuto(false);

            if (SkipEnabled)
                SetSkip(false);

            SetTruthEyeMinigameActive(true);

            truthEyeMinigame.Play(
                command.holdSeconds,
                command.failsBeforeSkip,
                command.allowSkipAfterFails,
                command.driftStrength,
                command.finishOnFail,
                r =>
                {
                    result = r;
                    LastTruthEyeResult = r;
                    done = true;
                }
            );

            while (!done)
            {
                yield return null;
            }

            SetTruthEyeMinigameActive(false);

            var resultBoolKey = string.IsNullOrWhiteSpace(command.resultBoolKey)
                ? "truth_eye_win"
                : command.resultBoolKey.Trim();

            // Один bool для conditions главы:
            // true = победа, false = поражение или Skip.
            State.SetBool(resultBoolKey, result.success);

            VNAutosave.Save(State);
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

        private void AddRuntimeLineToLog(VNLinePayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.text))
                return;

            State.log.Add(new VNState.LogEntry
            {
                speakerId = Norm(payload.speakerId),
                speakerName = payload.speakerName ?? "",
                text = payload.text ?? ""
            });

            VNAutosave.Save(State);
        }

        private void AddToLogAfterReveal(VNLineStep line)
        {
            if (line == null || !line.addToLog || State.currentStepLogged)
                return;

            var speakerId = Norm(line.speakerId);
            var speakerName = "";

            if (!line.showSpeakerName && !string.IsNullOrWhiteSpace(speakerId))
                speakerName = "???";
            else if (string.Equals(speakerId, "YOU", StringComparison.OrdinalIgnoreCase))
                speakerName = GetPresentedPlayerName();
            else if (!string.IsNullOrWhiteSpace(speakerId) && project.characterDatabase != null)
                project.characterDatabase.TryGetDisplayName(speakerId, out speakerName);

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

            for (var i = 0; i < State.slots.Count; i++)
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

            State.cutsceneVisible = false;
            State.cutsceneId = null;
            State.cutsceneHideDialogue = false;
            State.cutsceneHideCharacters = false;
            State.cutsceneBlockInput = false;
            State.cutscenePlayAudio = true;
            State.cutsceneAudioVolume = 1f;

            OnCutsceneHidden?.Invoke(new VNCutsceneHidePayload { fadeOutSeconds = 0f });

            VNAutosave.Save(State);
        }

        private static string Norm(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
    }
}