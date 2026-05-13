using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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

        [Header("One-Line Scene Object")]
        [Tooltip("UI-объект на сцене, который нужно включать только на одной особой строке после победы в мини-игре с глазом. Это НЕ artifact и НЕ sprite из базы — просто ссылка на объект в сцене.")]
        [FormerlySerializedAs("oneLineImageRoot")]
        [SerializeField] private GameObject oneLineSceneObject;

        [Tooltip("Если включено, объект особой строки скрывается сразу при переходе на следующую строку.")]
        [FormerlySerializedAs("hideOneLineImageOnLineHidden")]
        [SerializeField] private bool hideOneLineSceneObjectOnLineHidden = true;

        [Header("Player")] [Tooltip("Имя, которое будет показано вместо speakerId = YOU")] [SerializeField]
        private string playerDisplayName = "Player";

        [Header("Background")] [SerializeField]
        private VNCrossfadeImageUGUI background;

        [Header("Character slots (full body)")] [SerializeField]
        private VNCrossfadeImageUGUI leftSlot;

        [SerializeField] private VNCrossfadeImageUGUI centerSlot;
        [SerializeField] private VNCrossfadeImageUGUI rightSlot;

        [Header("Spine character slots optional")]
        [Tooltip("Optional animated Spine layer for the Left slot. If empty, this slot keeps using sprites only.")]
        [SerializeField] private VNSpineCharacterSlotUGUI leftSpineSlot;
        [Tooltip("Optional animated Spine layer for the Center slot. If empty, this slot keeps using sprites only.")]
        [SerializeField] private VNSpineCharacterSlotUGUI centerSpineSlot;
        [Tooltip("Optional animated Spine layer for the Right slot. If empty, this slot keeps using sprites only.")]
        [SerializeField] private VNSpineCharacterSlotUGUI rightSpineSlot;

        [Header("Spine Slot Alignment")]
        [Tooltip("Если включено, перед показом Spine-персонажа обычный sprite помещается в Image_A/Image_B, получает SetNativeSize и становится прозрачным. Spine встает в центр этого Image.")]
        [SerializeField] private bool alignSpineSlotsToSpriteSlots = true;

        [Tooltip("Оставь включенным. Это новый стабильный режим позиционирования Spine: прозрачный Image_A становится эталоном размера и позиции.")]
        [SerializeField] private bool useTransparentSpriteProxyForSpineAlignment = true;

        [Tooltip("Если включено, Spine-slot дополнительно копирует sizeDelta прозрачного Image. Обычно выключено, чтобы не менять масштаб Spine-персонажа.")]
        [SerializeField] private bool copySpriteSlotSizeToSpineSlot = false;

        [Tooltip("Если включено и Spine-slot находится под тем же parent, что и Image_A/Image_B, будут скопированы anchors/pivot. Обычно выключено.")]
        [SerializeField] private bool copySpriteSlotLayoutToSpineSlotWhenSameParent = false;

        [Header("Character Switching")]
        [Tooltip("Если включено, при смене персонажа старый персонаж скрывается мгновенно, без fade-out. Это убирает заметное белое/полупрозрачное исчезновение при переключении персонажей.")]
        [SerializeField] private bool instantHideOldCharacterOnSwitch = true;

        [Tooltip("Если включено, новый персонаж при смене тоже появляется мгновенно. Эмоции текущего персонажа продолжают обновляться без этого форса.")]
        [SerializeField] private bool instantShowNewCharacterOnSwitch = true;

        [Tooltip("Если включено, UI дополнительно сам проверяет, изменился ли characterId в слоте. Это защищает от случаев, когда runner не пометил смену персонажа как isNewCharacter.")]
        [SerializeField] private bool detectCharacterSwitchInView = true;

        [Tooltip("Жесткий режим для Spine/PMA-экспортов: любые скрытия и переключения персонажей происходят без alpha-fade. Оставь включенным, чтобы не было белых краев/вспышек при исчезновении.")]
        [SerializeField] private bool forceInstantCharacterVisibility = true;

        [Header("Choice UI")] [SerializeField] private VNChoicePanelUGUI choicePanel;

        [Header("Currency UI")]
        [Tooltip("Если включено, счетчик будет показываться сразу при появлении premium choices. По умолчанию выключено: счетчик показывается только при оплате/нехватке валюты/начислении.")]
        [SerializeField] private bool showCurrencyCounterDuringPremiumChoices = false;
        [Tooltip("Откуда стартует полет кристаллов для VN-команд начисления, если отдельный источник не задан внутри prefab-счетчика.")]
        [SerializeField] private RectTransform defaultCurrencyRewardFxSource;
        [Tooltip("Куда переносить глобальный счетчик валюты при оплате premium choice. Обычно это anchor внутри активного игрового HUD.")]
        [SerializeField] private RectTransform premiumPaymentCounterAnchor;
        [Tooltip("Если premiumPaymentCounterAnchor пустой, счетчик будет временно переноситься к нажатой premium-кнопке.")]
        [SerializeField] private bool useClickedChoiceAsPaymentAnchor = true;

        [Header("Cutscene UI")]
        [SerializeField] private VNCutscenePlayerUGUI cutscenePlayer;
        [Tooltip("Дополнительные объекты, которые нужно скрывать на время катсцены. Например кнопки Auto/Skip/Log, если они не внутри dialogueRoot.")]
        [SerializeField] private GameObject[] extraHideDuringCutscene;

        [Header("Log UI")]
 [SerializeField] private VNLogPanelUGUI logPanel;

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

        private bool _cutsceneActive;
        private bool _cutsceneHideDialogue;
        private bool _cutsceneHideCharacters;
        private bool _cutsceneBlockInput;
        private bool _cutsceneVisualsHidden;
        private GameObject[] _cutsceneHiddenTargets;
        private bool[] _cutsceneHiddenTargetStates;
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

        private RectTransform _pendingChoiceButtonRect;
        private int _pendingChoiceIndex = -1;

        private string _leftVisibleCharacterId;
        private string _centerVisibleCharacterId;
        private string _rightVisibleCharacterId;
        private bool _leftVisibleAsSpine;
        private bool _centerVisibleAsSpine;
        private bool _rightVisibleAsSpine;

        private bool _isLogOpen;

        private void OnEnable()
        {
            if (runner == null) return;

            runner.OnLineStarted += OnLineStarted;
            runner.OnRequestInstantReveal += OnInstantReveal;
            runner.OnLineHidden += OnLineHidden;

            runner.OnChoicePresented += OnChoice;
            runner.OnChoiceHidden += OnChoiceHidden;
            runner.OnPremiumChoiceRejected += OnPremiumChoiceRejected;
            runner.OnPremiumChoicePaid += OnPremiumChoicePaid;
            runner.OnCrystalsRewardRequested += OnCrystalsRewardRequested;
            runner.OnCutsceneShown += OnCutsceneShown;
            runner.OnCutsceneHidden += OnCutsceneHidden;

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
                runner.OnPremiumChoiceRejected -= OnPremiumChoiceRejected;
                runner.OnPremiumChoicePaid -= OnPremiumChoicePaid;
                runner.OnCrystalsRewardRequested -= OnCrystalsRewardRequested;
                runner.OnCutsceneShown -= OnCutsceneShown;
                runner.OnCutsceneHidden -= OnCutsceneHidden;

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

            SetCutsceneVisualsHidden(false);
            _cutsceneActive = false;
            _cutsceneHideDialogue = false;
            _cutsceneHideCharacters = false;
            _cutsceneBlockInput = false;

            if (cutscenePlayer != null)
                cutscenePlayer.HideImmediate();

            if (logPanel != null)
                logPanel.HideImmediate();

            HideOneLineSceneObject();

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
            
            if (_truthEyeActive || _locationIntroActive || (_cutsceneActive && _cutsceneBlockInput))
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

            ApplyOneLineSceneObject(line);

            if (typewriter != null)
                typewriter.Begin(BuildShownLineText(line));

            ApplyCutsceneVisibilityConstraints();
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
            if (hideOneLineSceneObjectOnLineHidden)
                HideOneLineSceneObject();
        }

        private void OnChoice(VNRunner.VNChoicePayload payload)
        {
            HideOneLineSceneObject();

            if (dialogueRoot != null)
                dialogueRoot.SetActive(false);

            if (showCurrencyCounterDuringPremiumChoices && HasPremiumChoice(payload))
            {
                VNCurrencyCounterUGUI.SetRegisteredVisible(true);
                VNCurrencyCounterUGUI.RefreshRegistered();
            }

            _pendingChoiceButtonRect = null;
            _pendingChoiceIndex = -1;

            if (choicePanel != null)
                choicePanel.Show(payload, OnChoiceButtonClicked);

            ApplyCutsceneVisibilityConstraints();
        }

        private void OnChoiceButtonClicked(int optionIndex)
        {
            _pendingChoiceIndex = optionIndex;
            _pendingChoiceButtonRect = choicePanel != null ? choicePanel.GetChoiceButtonRectTransform(optionIndex) : null;
            runner.Choose(optionIndex);
        }

        private void OnChoiceHidden()
        {
            if (choicePanel != null)
                choicePanel.Hide();

            if (dialogueRoot != null)
                dialogueRoot.SetActive(true);

            _pendingChoiceButtonRect = null;
            _pendingChoiceIndex = -1;

            ApplyCutsceneVisibilityConstraints();
        }

        private void OnPremiumChoiceRejected(VNRunner.VNPremiumChoiceRejectedPayload payload)
        {
            if (choicePanel != null)
                choicePanel.RefreshCurrentChoices();

            var anchor = ResolvePremiumPaymentCounterAnchor();
            if (!VNCurrencyCounterUGUI.TryShowBalanceAt(anchor, true))
            {
                VNCurrencyCounterUGUI.SetRegisteredVisible(true);
                VNCurrencyCounterUGUI.RefreshRegistered();
                VNCurrencyCounterUGUI.PulseRegistered();
            }
        }

        private void OnPremiumChoicePaid(VNRunner.VNPremiumChoicePaidPayload payload)
        {
            var anchor = ResolvePremiumPaymentCounterAnchor();
            if (!VNCurrencyCounterUGUI.TryShowPremiumSpend(payload.price, anchor, _pendingChoiceButtonRect))
            {
                VNCurrencyCounterUGUI.SetRegisteredVisible(true);
                VNCurrencyCounterUGUI.RefreshRegistered();
                VNCurrencyCounterUGUI.PulseRegistered();
            }
        }

        private RectTransform ResolvePremiumPaymentCounterAnchor()
        {
            if (premiumPaymentCounterAnchor != null)
                return premiumPaymentCounterAnchor;

            if (useClickedChoiceAsPaymentAnchor)
                return _pendingChoiceButtonRect;

            return null;
        }

        private void OnCrystalsRewardRequested(VNRunner.VNCurrencyRewardPayload payload)
        {
            if (VNCurrencyCounterUGUI.TryPlayAddCrystals(payload.amount, defaultCurrencyRewardFxSource))
                return;

            VNCrystalWallet.Add(payload.amount);
        }

        private static bool HasPremiumChoice(VNRunner.VNChoicePayload payload)
        {
            if (payload.options == null)
                return false;

            for (int i = 0; i < payload.options.Length; i++)
            {
                var option = payload.options[i];
                if (option != null && option.kind == VNChoiceKind.Premium && option.premiumPrice > 0)
                    return true;
            }

            return false;
        }

        private void OnCutsceneShown(VNRunner.VNCutscenePayload payload)
        {
            if (_isLogOpen)
                SetLogOpen(false);

            if (typewriter != null && typewriter.IsPlaying)
                typewriter.RevealInstant();

            _cutsceneActive = true;
            _cutsceneHideDialogue = payload.hideDialogue;
            _cutsceneHideCharacters = payload.hideCharacters;
            _cutsceneBlockInput = payload.blockInput;

            SetCutsceneVisualsHidden(true);

            if (cutscenePlayer != null)
                cutscenePlayer.Show(payload);
            else
                Debug.LogWarning("[VNUIViewUGUI] Cutscene command received, but VNCutscenePlayerUGUI is not assigned.");

            RefreshButtons();
        }

        private void OnCutsceneHidden(VNRunner.VNCutsceneHidePayload payload)
        {
            if (cutscenePlayer != null)
                cutscenePlayer.Hide(payload.fadeOutSeconds);

            SetCutsceneVisualsHidden(false);

            _cutsceneActive = false;
            _cutsceneHideDialogue = false;
            _cutsceneHideCharacters = false;

            _cutsceneBlockInput = false;

            RefreshButtons();
        }

        private void ApplyCutsceneVisibilityConstraints()
        {
            if (!_cutsceneActive)
                return;

            if (_cutsceneHideDialogue)
            {
                if (dialogueRoot != null)
                    dialogueRoot.SetActive(false);

                if (choicePanel != null)
                    choicePanel.Hide();
            }

            if (_cutsceneHideCharacters)
            {
                if (leftSlot != null)
                    leftSlot.gameObject.SetActive(false);
                if (centerSlot != null)
                    centerSlot.gameObject.SetActive(false);
                if (rightSlot != null)
                    rightSlot.gameObject.SetActive(false);

                if (leftSpineSlot != null)
                    leftSpineSlot.gameObject.SetActive(false);
                if (centerSpineSlot != null)
                    centerSpineSlot.gameObject.SetActive(false);
                if (rightSpineSlot != null)
                    rightSpineSlot.gameObject.SetActive(false);
            }
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
            var spriteView = ResolveSpriteSlot(slot.slot);
            var spineView = ResolveSpineSlot(slot.slot);

            var visualsHidden = _locationIntroVisualsHidden || _truthEyeVisualsHidden || _cutsceneVisualsHidden;
            var canUseSpine = slot.visible
                              && slot.hasSpine
                              && slot.spineSkeletonDataAsset != null
                              && spineView != null;

            var wasVisible = TryGetDisplayedCharacter(slot.slot, out var previousCharacterId, out var previousWasSpine);
            var normalizedCharacterId = NormalizeCharacterId(slot.characterId);
            var changedCharacterInView = detectCharacterSwitchInView
                                         && slot.visible
                                         && wasVisible
                                         && !string.Equals(previousCharacterId, normalizedCharacterId, StringComparison.Ordinal);
            var changedRenderLayer = slot.visible && wasVisible && previousWasSpine != canUseSpine;

            var isCharacterSwitch = slot.visible
                                    && (slot.isNewCharacter || changedCharacterInView || changedRenderLayer);

            var showSeconds = forceInstantCharacterVisibility
                              || visualsHidden
                              || (isCharacterSwitch && instantShowNewCharacterOnSwitch)
                ? 0f
                : slot.crossfadeSeconds;

            if (!slot.visible)
            {
                HideSpriteSlot(spriteView, 0f, true);
                HideSpineSlot(spineView, 0f);
                SetDisplayedCharacter(slot.slot, null, false, false);
                return;
            }

            if (canUseSpine)
            {
                var sameVisibleSpineCharacter = wasVisible
                                               && previousWasSpine
                                               && !string.IsNullOrEmpty(normalizedCharacterId)
                                               && string.Equals(previousCharacterId, normalizedCharacterId, StringComparison.Ordinal);

                if (sameVisibleSpineCharacter && !isCharacterSwitch)
                {
                    // Same Spine character on the same slot: this is usually a consecutive line
                    // or only an emotion/skin change. Do NOT run hidden preparation here.
                    // Hidden preparation clears the renderer and causes the visible blink/blank frame.
                    // Also do not rebuild the transparent Image_A proxy: the character must keep
                    // standing where it already stands.
                    spineView.Show(
                        slot.spineSkeletonDataAsset,
                        slot.spineBaseSkinName,
                        slot.spineSkinName,
                        slot.spineAnimationName,
                        slot.spineLoop,
                        slot.spineEmotionSlotsToClear,
                        0f,
                        allowActivate: true);

                    if (!visualsHidden)
                        spineView.ForceVisibleInstant();

                    SetDisplayedCharacter(slot.slot, normalizedCharacterId, true, true);
                    return;
                }

                // New character / new render layer: do hidden preparation so the player never sees
                // the default Spine pose or the character jumping from the center into Image_A.
                // The transparent sprite proxy is refreshed only on this actual switch.
                var referenceRect = PrepareSpineSpriteProxy(spriteView, slot.sprite);

                if (instantHideOldCharacterOnSwitch && spineView != null)
                    spineView.SetInstantHidden();

                spineView.ShowHiddenForAlignment(
                    slot.spineSkeletonDataAsset,
                    slot.spineBaseSkinName,
                    slot.spineSkinName,
                    slot.spineAnimationName,
                    slot.spineLoop,
                    slot.spineEmotionSlotsToClear,
                    allowActivate: true);

                AlignSpineSlotToSpriteReference(spineView, referenceRect);

                if (!visualsHidden)
                    spineView.RevealAfterAlignment(showSeconds);

                SetDisplayedCharacter(slot.slot, normalizedCharacterId, true, true);
                return;
            }

            HideSpineSlot(spineView, 0f);
            ShowSpriteSlot(spriteView, slot, visualsHidden, forceInstantCharacterVisibility || (isCharacterSwitch && instantShowNewCharacterOnSwitch));
            SetDisplayedCharacter(slot.slot, normalizedCharacterId, true, false);
        }

        private RectTransform PrepareSpineSpriteProxy(VNCrossfadeImageUGUI spriteView, Sprite sprite)
        {
            if (spriteView == null)
                return null;

            if (useTransparentSpriteProxyForSpineAlignment)
                return spriteView.PrepareTransparentSpriteProxy(sprite, setNativeSize: true);

            return spriteView.GetReferenceRectTransform();
        }

        private void AlignSpineSlotToSpriteReference(VNSpineCharacterSlotUGUI spineView, RectTransform referenceRect)
        {
            if (!alignSpineSlotsToSpriteSlots || spineView == null || referenceRect == null)
                return;

            spineView.AlignToImageSlot(
                referenceRect,
                copySpriteSlotSizeToSpineSlot,
                copySpriteSlotLayoutToSpineSlotWhenSameParent);
        }

        private bool TryGetDisplayedCharacter(VNScreenSlot slot, out string characterId, out bool wasSpine)
        {
            switch (slot)
            {
                case VNScreenSlot.Left:
                    characterId = _leftVisibleCharacterId;
                    wasSpine = _leftVisibleAsSpine;
                    break;
                case VNScreenSlot.Center:
                    characterId = _centerVisibleCharacterId;
                    wasSpine = _centerVisibleAsSpine;
                    break;
                case VNScreenSlot.Right:
                    characterId = _rightVisibleCharacterId;
                    wasSpine = _rightVisibleAsSpine;
                    break;
                default:
                    characterId = null;
                    wasSpine = false;
                    return false;
            }

            return !string.IsNullOrEmpty(characterId);
        }

        private void SetDisplayedCharacter(VNScreenSlot slot, string characterId, bool visible, bool asSpine)
        {
            characterId = visible ? NormalizeCharacterId(characterId) : null;

            switch (slot)
            {
                case VNScreenSlot.Left:
                    _leftVisibleCharacterId = characterId;
                    _leftVisibleAsSpine = visible && asSpine;
                    break;
                case VNScreenSlot.Center:
                    _centerVisibleCharacterId = characterId;
                    _centerVisibleAsSpine = visible && asSpine;
                    break;
                case VNScreenSlot.Right:
                    _rightVisibleCharacterId = characterId;
                    _rightVisibleAsSpine = visible && asSpine;
                    break;
            }
        }

        private static string NormalizeCharacterId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private VNCrossfadeImageUGUI ResolveSpriteSlot(VNScreenSlot slot)
        {
            return slot switch
            {
                VNScreenSlot.Left => leftSlot,
                VNScreenSlot.Center => centerSlot,
                VNScreenSlot.Right => rightSlot,
                _ => null
            };
        }

        private VNSpineCharacterSlotUGUI ResolveSpineSlot(VNScreenSlot slot)
        {
            return slot switch
            {
                VNScreenSlot.Left => leftSpineSlot,
                VNScreenSlot.Center => centerSpineSlot,
                VNScreenSlot.Right => rightSpineSlot,
                _ => null
            };
        }

        private void ShowSpriteSlot(VNCrossfadeImageUGUI view, VNRunner.VNSlotPayload slot, bool visualsHidden, bool forceInstant = false)
        {
            if (view == null)
                return;

            var canAnimateSlot = view.gameObject.activeInHierarchy && !visualsHidden && !forceInstant;

            if (slot.sprite == null)
            {
                HideSpriteSlot(view, slot.crossfadeSeconds, visualsHidden);
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

        private void HideSpriteSlot(VNCrossfadeImageUGUI view, float fadeSeconds, bool visualsHidden)
        {
            if (view == null)
                return;

            var canAnimateSlot = view.gameObject.activeInHierarchy && !visualsHidden && !forceInstantCharacterVisibility;

            if (canAnimateSlot && fadeSeconds > 0f)
                view.Crossfade((Sprite)null, Mathf.Max(0f, fadeSeconds), false);
            else
                view.SetInstantHidden();
        }

        private void HideSpineSlot(VNSpineCharacterSlotUGUI view, float fadeSeconds)
        {
            if (view == null)
                return;

            var visualsHidden = _locationIntroVisualsHidden || _truthEyeVisualsHidden || _cutsceneVisualsHidden;
            view.Hide((visualsHidden || forceInstantCharacterVisibility) ? 0f : fadeSeconds);
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
                audioController.PlayMusic(m.clip, m.fadeInSeconds, m.loop, m.volume);
                return;
            }

            if (!string.IsNullOrWhiteSpace(m.musicId))
                audioController.PlayMusic(m.musicId, m.fadeInSeconds, m.loop, m.volume);
        }

        private void OnSfx(VNRunner.VNSfxPayload sfx)
        {
            if (audioController == null) return;

            if (sfx.clip != null)
            {
                audioController.PlaySfx(sfx.clip, sfx.volume);
                return;
            }

            if (!string.IsNullOrWhiteSpace(sfx.sfxId))
                audioController.PlaySfx(sfx.sfxId, sfx.volume);
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
                AddUniqueHideTarget(list, leftSpineSlot != null ? leftSpineSlot.gameObject : null);
                AddUniqueHideTarget(list, centerSpineSlot != null ? centerSpineSlot.gameObject : null);
                AddUniqueHideTarget(list, rightSpineSlot != null ? rightSpineSlot.gameObject : null);
            }

            if (extraHideDuringLocationIntro != null)
                for (var i = 0; i < extraHideDuringLocationIntro.Length; i++)
                    AddUniqueHideTarget(list, extraHideDuringLocationIntro[i]);

            return list.ToArray();
        }

        private string BuildShownLineText(VNRunner.VNLinePayload line)
        {
            var text = line.text ?? "";

            if (line.useTextColorOverride)
                text = WrapColor(text, line.textColorHex);

            if (IsPlayerThoughtsLine(line))
                return WrapItalic(text);

            return text;
        }

        private void ApplyOneLineSceneObject(VNRunner.VNLinePayload line)
        {
            if (!line.showOneLineSceneObject)
            {
                HideOneLineSceneObject();
                return;
            }

            if (oneLineSceneObject == null)
            {
                Debug.LogWarning("[VNUIViewUGUI] Truth Eye victory line requested a scene UI object, but One Line Scene Object is not assigned.");
                return;
            }

            oneLineSceneObject.SetActive(true);

            if (oneLineSceneObject.TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void HideOneLineSceneObject()
        {
            if (oneLineSceneObject != null)
                oneLineSceneObject.SetActive(false);
        }

        private static string WrapColor(string text, string colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
                colorHex = "#5E3F92";

            if (!colorHex.StartsWith("#", StringComparison.Ordinal))
                colorHex = "#" + colorHex;

            if (!ColorUtility.TryParseHtmlString(colorHex, out _))
                colorHex = "#5E3F92";

            return $"<color={colorHex}>{text}</color>";
        }

        private void SetTruthEyeActive(bool active)
        {
            _truthEyeActive = active;

            if (active)
            {
                HideOneLineSceneObject();

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
                AddUniqueHideTarget(list, leftSpineSlot != null ? leftSpineSlot.gameObject : null);
                AddUniqueHideTarget(list, centerSpineSlot != null ? centerSpineSlot.gameObject : null);
                AddUniqueHideTarget(list, rightSpineSlot != null ? rightSpineSlot.gameObject : null);
            }

            if (extraHideDuringTruthEye != null)
                for (var i = 0; i < extraHideDuringTruthEye.Length; i++)
                    AddUniqueHideTarget(list, extraHideDuringTruthEye[i]);

            return list.ToArray();
        }

        private void SetCutsceneVisualsHidden(bool hidden)
        {
            if (_cutsceneVisualsHidden == hidden)
                return;

            _cutsceneVisualsHidden = hidden;

            if (hidden)
            {
                _cutsceneHiddenTargets = BuildCutsceneHideTargets();
                _cutsceneHiddenTargetStates = new bool[_cutsceneHiddenTargets.Length];

                for (var i = 0; i < _cutsceneHiddenTargets.Length; i++)
                {
                    var target = _cutsceneHiddenTargets[i];
                    if (target == null)
                        continue;

                    _cutsceneHiddenTargetStates[i] = target.activeSelf;
                    target.SetActive(false);
                }
            }
            else
            {
                if (_cutsceneHiddenTargets != null && _cutsceneHiddenTargetStates != null)
                {
                    var count = Mathf.Min(_cutsceneHiddenTargets.Length, _cutsceneHiddenTargetStates.Length);

                    for (var i = 0; i < count; i++)
                    {
                        var target = _cutsceneHiddenTargets[i];
                        if (target == null)
                            continue;

                        target.SetActive(_cutsceneHiddenTargetStates[i]);
                    }
                }

                _cutsceneHiddenTargets = null;
                _cutsceneHiddenTargetStates = null;
            }
        }

        private GameObject[] BuildCutsceneHideTargets()
        {
            var list = new List<GameObject>();

            if (_cutsceneHideDialogue)
            {
                AddUniqueHideTarget(list, dialogueRoot);
                AddUniqueHideTarget(list, choicePanel != null ? choicePanel.gameObject : null);
            }

            if (_cutsceneHideCharacters)
            {
                AddUniqueHideTarget(list, leftSlot != null ? leftSlot.gameObject : null);
                AddUniqueHideTarget(list, centerSlot != null ? centerSlot.gameObject : null);
                AddUniqueHideTarget(list, rightSlot != null ? rightSlot.gameObject : null);
                AddUniqueHideTarget(list, leftSpineSlot != null ? leftSpineSlot.gameObject : null);
                AddUniqueHideTarget(list, centerSpineSlot != null ? centerSpineSlot.gameObject : null);
                AddUniqueHideTarget(list, rightSpineSlot != null ? rightSpineSlot.gameObject : null);
            }

            if (extraHideDuringCutscene != null)
                for (var i = 0; i < extraHideDuringCutscene.Length; i++)
                    AddUniqueHideTarget(list, extraHideDuringCutscene[i]);

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