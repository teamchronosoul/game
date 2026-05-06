using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNUIViewUGUI : MonoBehaviour
    {
        [Header("Runner")] [SerializeField] private VNRunner runner;

        [Header("Dialogue UI")] [SerializeField]
        private GameObject dialogueRoot;

        [Tooltip("Корневой объект плашки имени. Будет скрываться для Narrator.")] [SerializeField]
        private GameObject speakerNameRoot;

        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private VNTypewriterUGUI typewriter;

        [Header("Player")] [Tooltip("Имя, которое будет показано вместо speakerId = YOU")] [SerializeField]
        private string playerDisplayName = "Player";

        [Header("Background")] [SerializeField]
        private VNCrossfadeImageUGUI background;

        [Header("Character slots (full body)")] [SerializeField]
        private VNCrossfadeImageUGUI leftSlot;

        [SerializeField] private VNCrossfadeImageUGUI centerSlot;
        [SerializeField] private VNCrossfadeImageUGUI rightSlot;

        [Header("Choice UI")] [SerializeField] private VNChoicePanelUGUI choicePanel;

        [Header("Log UI")] [SerializeField] private VNLogPanelUGUI logPanel;

        [Header("Buttons")] [SerializeField] private Button autoButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button logButton;
        [SerializeField] private Button closeLogButton;
        [SerializeField] private Button resetAutosaveButton;

        [Header("Tap feedback")] [SerializeField]
        private VNTapFeedbackHeart tapFx;

        [Header("Audio")] [SerializeField] private VNAudioController audioController;

        [Header("Truth Eye Minigame Visibility")] [SerializeField]
        private bool hideDialogueDuringTruthEye = true;

        [SerializeField] private bool hideCharactersDuringTruthEye = true;

        [Tooltip(
            "Дополнительные объекты, которые нужно скрывать на время мини-игры. Например кнопки Auto/Skip/Log, если они не внутри dialogueRoot.")]
        [SerializeField]
        private GameObject[] extraHideDuringTruthEye;

        [Header("Location Intro Camera Slide")]
        [Tooltip("UI-анимация скольжения фона при смене локации. Раннер все равно делает паузу, даже если этот флаг выключен.")]
        [SerializeField] private bool playLocationIntroInView = true;

        [Tooltip("Что двигать во время интро. Если пусто, будет использован RectTransform объекта Background.")]
        [SerializeField] private RectTransform cameraSlideTarget;

        [SerializeField, Min(0f)] private float cameraSlideDistancePixels = 80f;
        [SerializeField] private AnimationCurve cameraSlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        // Location Intro Plate temporarily disabled.
        // Keep this block for quick restore when the plate is needed again.
        /*
        [Header("Location Intro Plate")]
        [SerializeField] private GameObject locationIntroPlateRoot;
        [SerializeField] private TextMeshProUGUI locationIntroLocationText;
        [SerializeField] private TextMeshProUGUI locationIntroTimeOfDayText;
        */

        [Header("Location Intro Visibility")]
        [SerializeField] private bool hideDialogueDuringLocationIntro = true;
        [SerializeField] private bool hideCharactersDuringLocationIntro = true;
        [SerializeField] private GameObject[] extraHideDuringLocationIntro;

        private Action<bool> _truthEyeActiveChangedHandler;

        private bool _truthEyeActive;
        private bool _truthEyeVisualsHidden;
        private GameObject[] _truthEyeHiddenTargets;
        private bool[] _truthEyeHiddenTargetStates;

        private bool _locationIntroActive;
        private bool _locationIntroVisualsHidden;
        private GameObject[] _locationIntroHiddenTargets;
        private bool[] _locationIntroHiddenTargetStates;
        private Coroutine _locationIntroCoroutine;
        private bool _hasPendingLocationIntroMusic;
        private VNRunner.VNMusicPayload _pendingLocationIntroMusic;
        private RectTransform _activeCameraSlideTarget;
        private Vector2 _cameraSlideBaseAnchoredPosition;
        private bool _hasCameraSlideBasePosition;

        private Action _typewriterFinishedHandler;
        private Action<Vector2> _tapFeedbackHandler;

        private Action<bool> _autoChangedHandler;
        private Action<bool> _skipChangedHandler;
        private Action<bool> _skipAllowedChangedHandler;

        private bool _isLogOpen;

        private void OnEnable()
        {
            if (runner == null) return;

            runner.OnLineStarted += OnLineStarted;
            runner.OnRequestInstantReveal += OnInstantReveal;
            runner.OnLineHidden += OnLineHidden;

            runner.OnChoicePresented += OnChoice;
            runner.OnChoiceHidden += OnChoiceHidden;

            runner.OnBackgroundChanged += OnBackground;
            runner.OnSlotChanged += OnSlot;


            _truthEyeActiveChangedHandler = SetTruthEyeActive;
            runner.OnTruthEyeMinigameActiveChanged += _truthEyeActiveChangedHandler;
            runner.OnMusicPlay += OnMusicPlay;
            runner.OnMusicStop += OnMusicStop;
            runner.OnSfxPlay += OnSfx;
            runner.OnLocationIntroStarted += OnLocationIntroStarted;
            runner.OnLocationIntroFinished += OnLocationIntroFinished;

            _autoChangedHandler = _ => RefreshButtons();
            _skipChangedHandler = _ => RefreshButtons();
            _skipAllowedChangedHandler = _ => RefreshButtons();

            runner.OnAutoChanged += _autoChangedHandler;
            runner.OnSkipChanged += _skipChangedHandler;
            runner.OnSkipAllowedChanged += _skipAllowedChangedHandler;

            runner.SetPresentedPlayerName(GetPlayerDisplayName());

            _tapFeedbackHandler = pos =>
            {
                if (tapFx != null && !_isLogOpen)
                    tapFx.Spawn(pos);
            };

            runner.OnTapFeedback += _tapFeedbackHandler;

            if (typewriter != null)
            {
                _typewriterFinishedHandler = () => runner.NotifyLineRevealFinished();
                typewriter.OnFinished += _typewriterFinishedHandler;
            }

            if (logPanel != null)
                logPanel.HideImmediate();

            if (choicePanel != null)
                choicePanel.Hide();

            _isLogOpen = false;
            runner.SetModalOpen(false);

            SetSpeakerPlateVisible(false);

            if (dialogueRoot != null)
                dialogueRoot.SetActive(true);

            WireButtons();
            RefreshButtons();
        }

        private void OnDisable()
        {
            if (runner != null)
            {
                runner.OnLineStarted -= OnLineStarted;
                runner.OnRequestInstantReveal -= OnInstantReveal;
                runner.OnLineHidden -= OnLineHidden;

                runner.OnChoicePresented -= OnChoice;
                runner.OnChoiceHidden -= OnChoiceHidden;

                runner.OnBackgroundChanged -= OnBackground;
                runner.OnSlotChanged -= OnSlot;

                runner.OnMusicPlay -= OnMusicPlay;
                runner.OnMusicStop -= OnMusicStop;
                runner.OnSfxPlay -= OnSfx;
                runner.OnLocationIntroStarted -= OnLocationIntroStarted;
                runner.OnLocationIntroFinished -= OnLocationIntroFinished;

                if (_truthEyeActiveChangedHandler != null)
                    runner.OnTruthEyeMinigameActiveChanged -= _truthEyeActiveChangedHandler;

                if (_tapFeedbackHandler != null)
                    runner.OnTapFeedback -= _tapFeedbackHandler;

                if (_autoChangedHandler != null)
                    runner.OnAutoChanged -= _autoChangedHandler;

                if (_skipChangedHandler != null)
                    runner.OnSkipChanged -= _skipChangedHandler;

                if (_skipAllowedChangedHandler != null)
                    runner.OnSkipAllowedChanged -= _skipAllowedChangedHandler;

                runner.SetModalOpen(false);
            }

            _tapFeedbackHandler = null;
            _autoChangedHandler = null;
            _skipChangedHandler = null;
            _skipAllowedChangedHandler = null;

            if (typewriter != null && _typewriterFinishedHandler != null)
                typewriter.OnFinished -= _typewriterFinishedHandler;

            _typewriterFinishedHandler = null;

            _isLogOpen = false;

            SetTruthEyeVisualsHidden(false);
            _truthEyeActive = false;
            StopLocationIntroCoroutine(true);
            // Location intro plate temporarily disabled.
            // SetLocationIntroPlateVisible(false);
            SetLocationIntroVisualsHidden(false);
            _locationIntroActive = false;
            _hasPendingLocationIntroMusic = false;
            _pendingLocationIntroMusic = default;

            if (logPanel != null)
                logPanel.HideImmediate();

            ResetButtonGraphic(autoButton);
            ResetButtonGraphic(skipButton);
        }

        public void SetPlayerDisplayName(string value)
        {
            playerDisplayName = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim();

            if (runner != null)
                runner.SetPresentedPlayerName(playerDisplayName);
        }

        public string GetPlayerDisplayName()
        {
            return string.IsNullOrWhiteSpace(playerDisplayName) ? "Player" : playerDisplayName;
        }

        private void WireButtons()
        {
            if (autoButton != null)
            {
                autoButton.onClick.RemoveAllListeners();
                autoButton.onClick.AddListener(() =>
                {
                    if (_isLogOpen)
                        return;

                    if (runner.AutoEnabled)
                        return;

                    runner.SuppressNextTap();

                    if (runner.SkipEnabled)
                        runner.SetSkip(false);

                    runner.SetAuto(true);
                    RefreshButtons();
                });
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() =>
                {
                    if (_isLogOpen)
                        return;

                    runner.SuppressNextTap();

                    if (runner.AutoEnabled)
                        runner.SetAuto(false);

                    runner.SetSkip(!runner.SkipEnabled);
                    RefreshButtons();
                });
            }

            if (logButton != null)
            {
                logButton.onClick.RemoveAllListeners();
                logButton.onClick.AddListener(() =>
                {
                    runner.SuppressNextTap();
                    SetLogOpen(true);
                });
            }

            if (closeLogButton != null)
            {
                closeLogButton.onClick.RemoveAllListeners();
                closeLogButton.onClick.AddListener(() =>
                {
                    runner.SuppressNextTap();
                    SetLogOpen(false);
                });
            }

            if (resetAutosaveButton != null)
            {
                resetAutosaveButton.onClick.RemoveAllListeners();
                resetAutosaveButton.onClick.AddListener(() =>
                {
                    runner.SuppressNextTap();
                    SetLogOpen(false);
                    runner.DeleteAutosaveAndRestart();
                });
            }
        }

        private void SetLogOpen(bool open)
        {
            if (runner == null || logPanel == null)
                return;

            if (_isLogOpen == open)
                return;

            if (open)
            {
                if (typewriter != null && typewriter.IsPlaying)
                    typewriter.RevealInstant();

                if (runner.AutoEnabled)
                    runner.SetAuto(false);

                if (runner.SkipEnabled)
                    runner.SetSkip(false);

                _isLogOpen = true;
                runner.SetModalOpen(true);
                logPanel.Show(runner.State.log);
            }
            else
            {
                _isLogOpen = false;
                logPanel.HideImmediate();
                runner.SetModalOpen(false);
            }

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            if (runner == null)
                return;

            var autoActive = runner.AutoEnabled && !_isLogOpen;
            var skipActive = runner.SkipEnabled && runner.SkipAllowed && !_isLogOpen;

            if (autoButton != null)
            {
                autoButton.interactable = !_isLogOpen && !runner.AutoEnabled;
                ApplyButtonGraphicState(autoButton, runner.AutoEnabled && !_isLogOpen);
            }

            if (skipButton != null)
            {
                skipButton.interactable = runner.SkipAllowed && !_isLogOpen && !runner.AutoEnabled;
                ApplyButtonGraphicState(skipButton, skipActive);
            }

            if (logButton != null)
                logButton.interactable = !_isLogOpen && !runner.AutoEnabled;

            if (closeLogButton != null)
                closeLogButton.interactable = _isLogOpen;
            
            if (_truthEyeActive || _locationIntroActive)
            {
                if (autoButton != null)
                {
                    autoButton.interactable = false;
                    ApplyButtonGraphicState(autoButton, false);
                }

                if (skipButton != null)
                {
                    skipButton.interactable = false;
                    ApplyButtonGraphicState(skipButton, false);
                }

                if (logButton != null)
                    logButton.interactable = false;

                if (closeLogButton != null)
                    closeLogButton.interactable = false;

                return;
            }
        }

        private void ApplyButtonGraphicState(Button button, bool active)
        {
            if (button == null || button.targetGraphic == null)
                return;

            var colors = button.colors;

            Color targetColor;

            if (active)
                targetColor = colors.pressedColor;
            else if (!button.interactable)
                targetColor = colors.disabledColor;
            else
                targetColor = colors.normalColor;

            button.targetGraphic.CrossFadeColor(
                targetColor,
                colors.fadeDuration,
                true,
                true
            );
        }

        private void ResetButtonGraphic(Button button)
        {
            if (button == null || button.targetGraphic == null)
                return;

            var colors = button.colors;

            button.targetGraphic.CrossFadeColor(
                colors.normalColor,
                0f,
                true,
                true
            );
        }

        private void OnLineStarted(VNRunner.VNLinePayload line)
        {
            if (dialogueRoot != null)
                dialogueRoot.SetActive(true);

            var shownSpeakerName = ResolveShownSpeakerName(line);
            var showSpeakerPlate = !line.isNarrator && !string.IsNullOrWhiteSpace(shownSpeakerName);

            SetSpeakerPlateVisible(showSpeakerPlate);

            if (speakerNameText != null)
                speakerNameText.text = showSpeakerPlate ? shownSpeakerName : "";

            if (typewriter != null)
                typewriter.Begin(BuildShownLineText(line));
        }

        private string ResolveShownSpeakerName(VNRunner.VNLinePayload line)
        {
            if (line.isNarrator)
                return "";

            if (!line.showSpeakerName)
                return "???";

            var speakerId = (line.speakerId ?? "").Trim();
            var speakerName = (line.speakerName ?? "").Trim();

            if (string.Equals(speakerId, "YOU", StringComparison.OrdinalIgnoreCase))
                return GetPlayerDisplayName();

            if (!string.IsNullOrWhiteSpace(speakerName))
                return speakerName;

            return speakerId;
        }

        private void SetSpeakerPlateVisible(bool visible)
        {
            if (speakerNameRoot != null)
                speakerNameRoot.SetActive(visible);
            else if (speakerNameText != null)
                speakerNameText.gameObject.SetActive(visible);
        }

        private void OnInstantReveal()
        {
            if (_isLogOpen)
                return;

            if (typewriter != null)
                typewriter.RevealInstant();
            else
                runner.NotifyLineRevealFinished();
        }

        private void OnLineHidden()
        {
        }

        private void OnChoice(VNRunner.VNChoicePayload payload)
        {
            if (dialogueRoot != null)
                dialogueRoot.SetActive(false);

            if (choicePanel != null)
                choicePanel.Show(payload, idx => runner.Choose(idx));
        }

        private void OnChoiceHidden()
        {
            if (choicePanel != null)
                choicePanel.Hide();

            if (dialogueRoot != null)
                dialogueRoot.SetActive(true);
        }

        private void OnBackground(VNRunner.VNBackgroundPayload bg)
        {
            if (background == null) return;

            if (bg.crossfadeSeconds <= 0f)
                background.SetInstant(bg.sprite, bg.sprite != null);
            else
                background.Crossfade(bg.sprite, bg.crossfadeSeconds, bg.sprite != null);
        }

        private void OnSlot(VNRunner.VNSlotPayload slot)
        {
            var view = slot.slot switch
            {
                VNScreenSlot.Left => leftSlot,
                VNScreenSlot.Center => centerSlot,
                VNScreenSlot.Right => rightSlot,
                _ => null
            };

            if (view == null) return;

            // Во время location intro character-slot может быть временно SetActive(false).
            // На неактивном объекте Crossfade запускает coroutine и может не выполниться,
            // поэтому команды hide/show персонажа применяем мгновенно, чтобы состояние не потерялось.
            var canAnimateSlot = view.gameObject.activeInHierarchy && !_locationIntroVisualsHidden;

            if (!slot.visible || slot.sprite == null)
            {
                if (canAnimateSlot && slot.crossfadeSeconds > 0f)
                    view.Crossfade((Sprite)null, Mathf.Max(0f, slot.crossfadeSeconds), false);
                else
                    view.SetInstant((Sprite)null, false);

                return;
            }

            if (!canAnimateSlot || slot.crossfadeSeconds <= 0f)
            {
                view.SetInstant(slot.sprite, true);
                ApplyNativeSize(view);
            }
            else
            {
                view.Crossfade(slot.sprite, slot.crossfadeSeconds, true);
                StartCoroutine(ApplyNativeSizeNextFrame(view));
            }
        }

        private void ApplyNativeSize(VNCrossfadeImageUGUI view)
        {
            if (view == null) return;

            var images = view.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
                if (images[i] != null && images[i].sprite != null)
                    images[i].SetNativeSize();
        }

        private IEnumerator ApplyNativeSizeNextFrame(VNCrossfadeImageUGUI view)
        {
            yield return null;
            ApplyNativeSize(view);
        }

        private void OnMusicPlay(VNRunner.VNMusicPayload m)
        {
            if (audioController == null) return;

            // Во время скольжения локации музыка не должна звучать.
            // Но сама команда смены музыки должна запомниться, чтобы после интро
            // заиграла уже новая музыка.
            if (_locationIntroActive)
            {
                _pendingLocationIntroMusic = m;
                _hasPendingLocationIntroMusic = true;
                audioController.StopMusic(0f);
                return;
            }

            PlayMusicPayload(m);
        }

        private void OnMusicStop(float fadeOut)
        {
            if (audioController == null) return;

            // Если во время скольжения пришла команда StopMusic, она отменяет
            // отложенную музыку и после интро музыка не включится.
            if (_locationIntroActive)
            {
                _hasPendingLocationIntroMusic = false;
                _pendingLocationIntroMusic = default;
                audioController.StopMusic(0f);
                return;
            }

            audioController.StopMusic(fadeOut);
        }

        private void PlayMusicPayload(VNRunner.VNMusicPayload m)
        {
            if (audioController == null) return;

            if (m.clip != null)
            {
                audioController.PlayMusic(m.clip, m.fadeInSeconds, m.loop);
                return;
            }

            if (!string.IsNullOrWhiteSpace(m.musicId))
                audioController.PlayMusic(m.musicId, m.fadeInSeconds, m.loop);
        }

        private void OnSfx(VNRunner.VNSfxPayload sfx)
        {
            if (audioController == null) return;

            if (sfx.clip != null)
            {
                audioController.PlaySfx(sfx.clip);
                return;
            }

            if (!string.IsNullOrWhiteSpace(sfx.sfxId))
                audioController.PlaySfx(sfx.sfxId);
        }

        private void OnLocationIntroStarted(VNRunner.VNLocationIntroPayload payload)
        {
            _locationIntroActive = true;
            _hasPendingLocationIntroMusic = false;
            _pendingLocationIntroMusic = default;

            // На время скольжения локации должна быть полная тишина.
            // Если следующая команда сменит музыку, OnMusicPlay запомнит её и включит после интро.
            if (audioController != null)
                audioController.StopMusic(0f);

            if (_isLogOpen)
                SetLogOpen(false);

            if (typewriter != null && typewriter.IsPlaying)
                typewriter.RevealInstant();

            SetLocationIntroVisualsHidden(true);
            // Location intro plate temporarily disabled.
            // SetLocationIntroPlate(payload);

            StopLocationIntroCoroutine(true);

            if (playLocationIntroInView)
                _locationIntroCoroutine = StartCoroutine(PlayLocationIntroSlide(payload.durationSeconds));

            RefreshButtons();
        }

        private void OnLocationIntroFinished()
        {
            StopLocationIntroCoroutine(true);
            // Location intro plate temporarily disabled.
            // SetLocationIntroPlateVisible(false);
            SetLocationIntroVisualsHidden(false);
            _locationIntroActive = false;

            if (_hasPendingLocationIntroMusic)
            {
                var pendingMusic = _pendingLocationIntroMusic;
                _hasPendingLocationIntroMusic = false;
                _pendingLocationIntroMusic = default;
                PlayMusicPayload(pendingMusic);
            }

            RefreshButtons();
        }

        private IEnumerator PlayLocationIntroSlide(float durationSeconds)
        {
            var target = ResolveCameraSlideTarget();
            if (target == null)
                yield break;

            _activeCameraSlideTarget = target;
            _cameraSlideBaseAnchoredPosition = target.anchoredPosition;
            _hasCameraSlideBasePosition = true;

            var duration = Mathf.Max(0.001f, durationSeconds);
            var distance = Mathf.Max(0f, cameraSlideDistancePixels);
            var from = _cameraSlideBaseAnchoredPosition + new Vector2(distance, 0f);
            var to = _cameraSlideBaseAnchoredPosition;

            target.anchoredPosition = from;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var k = cameraSlideCurve != null
                    ? cameraSlideCurve.Evaluate(normalized)
                    : Mathf.SmoothStep(0f, 1f, normalized);

                target.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
                yield return null;
            }

            target.anchoredPosition = to;
            _locationIntroCoroutine = null;
        }

        private RectTransform ResolveCameraSlideTarget()
        {
            if (cameraSlideTarget != null)
                return cameraSlideTarget;

            return background != null ? background.transform as RectTransform : null;
        }

        private void StopLocationIntroCoroutine(bool snapToBasePosition)
        {
            if (_locationIntroCoroutine != null)
            {
                StopCoroutine(_locationIntroCoroutine);
                _locationIntroCoroutine = null;
            }

            if (snapToBasePosition && _activeCameraSlideTarget != null && _hasCameraSlideBasePosition)
                _activeCameraSlideTarget.anchoredPosition = _cameraSlideBaseAnchoredPosition;

            _activeCameraSlideTarget = null;
            _hasCameraSlideBasePosition = false;
        }

        // Location intro plate temporarily disabled.
        // Keep these methods for quick restore when the plate is needed again.
        /*
        private void SetLocationIntroPlate(VNRunner.VNLocationIntroPayload payload)
        {
            var locationName = payload.locationName ?? "";
            var timeOfDay = payload.timeOfDay ?? "";

            if (locationIntroLocationText != null)
            {
                locationIntroLocationText.text = locationName;
                locationIntroLocationText.gameObject.SetActive(!string.IsNullOrWhiteSpace(locationName));
            }

            if (locationIntroTimeOfDayText != null)
            {
                locationIntroTimeOfDayText.text = timeOfDay;
                locationIntroTimeOfDayText.gameObject.SetActive(!string.IsNullOrWhiteSpace(timeOfDay));
            }

            var showPlate = !string.IsNullOrWhiteSpace(locationName) || !string.IsNullOrWhiteSpace(timeOfDay);
            SetLocationIntroPlateVisible(showPlate);
        }

        private void SetLocationIntroPlateVisible(bool visible)
        {
            if (locationIntroPlateRoot != null)
                locationIntroPlateRoot.SetActive(visible);
            else
            {
                if (locationIntroLocationText != null)
                    locationIntroLocationText.gameObject.SetActive(visible && !string.IsNullOrWhiteSpace(locationIntroLocationText.text));

                if (locationIntroTimeOfDayText != null)
                    locationIntroTimeOfDayText.gameObject.SetActive(visible && !string.IsNullOrWhiteSpace(locationIntroTimeOfDayText.text));
            }
        }
        */

        private void SetLocationIntroVisualsHidden(bool hidden)
        {
            if (_locationIntroVisualsHidden == hidden)
                return;

            _locationIntroVisualsHidden = hidden;

            if (hidden)
            {
                _locationIntroHiddenTargets = BuildLocationIntroHideTargets();
                _locationIntroHiddenTargetStates = new bool[_locationIntroHiddenTargets.Length];

                for (var i = 0; i < _locationIntroHiddenTargets.Length; i++)
                {
                    var target = _locationIntroHiddenTargets[i];
                    if (target == null)
                        continue;

                    _locationIntroHiddenTargetStates[i] = target.activeSelf;
                    target.SetActive(false);
                }
            }
            else
            {
                if (_locationIntroHiddenTargets != null && _locationIntroHiddenTargetStates != null)
                {
                    var count = Mathf.Min(_locationIntroHiddenTargets.Length, _locationIntroHiddenTargetStates.Length);
                    for (var i = 0; i < count; i++)
                    {
                        var target = _locationIntroHiddenTargets[i];
                        if (target == null)
                            continue;

                        target.SetActive(_locationIntroHiddenTargetStates[i]);
                    }
                }

                _locationIntroHiddenTargets = null;
                _locationIntroHiddenTargetStates = null;
            }
        }

        private GameObject[] BuildLocationIntroHideTargets()
        {
            var list = new List<GameObject>();

            if (hideDialogueDuringLocationIntro)
                AddUniqueHideTarget(list, dialogueRoot);

            if (hideCharactersDuringLocationIntro)
            {
                AddUniqueHideTarget(list, leftSlot != null ? leftSlot.gameObject : null);
                AddUniqueHideTarget(list, centerSlot != null ? centerSlot.gameObject : null);
                AddUniqueHideTarget(list, rightSlot != null ? rightSlot.gameObject : null);
            }

            if (extraHideDuringLocationIntro != null)
                for (var i = 0; i < extraHideDuringLocationIntro.Length; i++)
                    AddUniqueHideTarget(list, extraHideDuringLocationIntro[i]);

            return list.ToArray();
        }

        private string BuildShownLineText(VNRunner.VNLinePayload line)
        {
            var text = line.text ?? "";

            if (IsPlayerThoughtsLine(line))
                return WrapItalic(text);

            return text;
        }

        private void SetTruthEyeActive(bool active)
        {
            _truthEyeActive = active;

            if (active)
            {
                if (_isLogOpen)
                    SetLogOpen(false);

                if (runner != null)
                    runner.SetModalOpen(false);

                if (typewriter != null && typewriter.IsPlaying)
                    typewriter.RevealInstant();

                SetTruthEyeVisualsHidden(true);
            }
            else
            {
                SetTruthEyeVisualsHidden(false);
            }

            RefreshButtons();
        }

        private void SetTruthEyeVisualsHidden(bool hidden)
        {
            if (_truthEyeVisualsHidden == hidden)
                return;

            _truthEyeVisualsHidden = hidden;

            if (hidden)
            {
                _truthEyeHiddenTargets = BuildTruthEyeHideTargets();
                _truthEyeHiddenTargetStates = new bool[_truthEyeHiddenTargets.Length];

                for (var i = 0; i < _truthEyeHiddenTargets.Length; i++)
                {
                    var target = _truthEyeHiddenTargets[i];

                    if (target == null)
                        continue;

                    _truthEyeHiddenTargetStates[i] = target.activeSelf;
                    target.SetActive(false);
                }
            }
            else
            {
                if (_truthEyeHiddenTargets != null && _truthEyeHiddenTargetStates != null)
                {
                    var count = Mathf.Min(_truthEyeHiddenTargets.Length, _truthEyeHiddenTargetStates.Length);

                    for (var i = 0; i < count; i++)
                    {
                        var target = _truthEyeHiddenTargets[i];

                        if (target == null)
                            continue;

                        target.SetActive(_truthEyeHiddenTargetStates[i]);
                    }
                }

                _truthEyeHiddenTargets = null;
                _truthEyeHiddenTargetStates = null;
            }
        }

        private GameObject[] BuildTruthEyeHideTargets()
        {
            var list = new List<GameObject>();

            if (hideDialogueDuringTruthEye)
                AddUniqueHideTarget(list, dialogueRoot);

            if (hideCharactersDuringTruthEye)
            {
                AddUniqueHideTarget(list, leftSlot != null ? leftSlot.gameObject : null);
                AddUniqueHideTarget(list, centerSlot != null ? centerSlot.gameObject : null);
                AddUniqueHideTarget(list, rightSlot != null ? rightSlot.gameObject : null);
            }

            if (extraHideDuringTruthEye != null)
                for (var i = 0; i < extraHideDuringTruthEye.Length; i++)
                    AddUniqueHideTarget(list, extraHideDuringTruthEye[i]);

            return list.ToArray();
        }

        private void AddUniqueHideTarget(List<GameObject> list, GameObject target)
        {
            if (target == null)
                return;

            if (target == gameObject)
            {
                Debug.LogWarning("[VNUIViewUGUI] Do not add the VNUIViewUGUI root itself to Truth Eye hide targets.");
                return;
            }

            if (!list.Contains(target))
                list.Add(target);
        }

        private static bool IsPlayerThoughtsLine(VNRunner.VNLinePayload line)
        {
            return string.Equals(line.speakerId, "YOU", StringComparison.OrdinalIgnoreCase)
                   && line.emotion.ToString().Equals("Thoughts", StringComparison.OrdinalIgnoreCase);
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
    }
}