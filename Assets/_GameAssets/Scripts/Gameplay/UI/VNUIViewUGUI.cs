using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNUIViewUGUI : MonoBehaviour
    {
        [Header("Runner")]
        [SerializeField] private VN.VNRunner runner;

        [Header("Dialogue UI")]
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private Text speakerNameText;
        [SerializeField] private VNTypewriterUGUI typewriter;

        [Header("Background")]
        [SerializeField] private VNCrossfadeImageUGUI background;

        [Header("Character slots (full body)")]
        [SerializeField] private VNCrossfadeImageUGUI leftSlot;
        [SerializeField] private VNCrossfadeImageUGUI centerSlot;
        [SerializeField] private VNCrossfadeImageUGUI rightSlot;

        [Header("Choice UI")]
        [SerializeField] private VNChoicePanelUGUI choicePanel;

        [Header("Log UI")]
        [SerializeField] private VNLogPanelUGUI logPanel;

        [Header("Buttons")]
        [SerializeField] private Button autoButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button logButton;
        [SerializeField] private Button closeLogButton;
        [SerializeField] private Button resetAutosaveButton;

        [Header("Tap feedback")]
        [SerializeField] private VNTapFeedbackHeart tapFx;

        [Header("Audio")]
        [SerializeField] private VNAudioController audioController;

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

            runner.OnMusicPlay += OnMusicPlay;
            runner.OnMusicStop += OnMusicStop;
            runner.OnSfxPlay += OnSfx;

            runner.OnAutoChanged += _ => RefreshButtons();
            runner.OnSkipChanged += _ => RefreshButtons();
            runner.OnSkipAllowedChanged += _ => RefreshButtons();

            runner.OnTapFeedback += pos => { if (tapFx != null) tapFx.Spawn(pos); };

            if (typewriter != null)
                typewriter.OnFinished += () => runner.NotifyLineRevealFinished();

            WireButtons();
            RefreshButtons();

            if (logPanel != null) logPanel.Hide();
            if (choicePanel != null) choicePanel.Hide();
        }

        private void OnDisable()
        {
            if (runner == null) return;

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

            runner.OnTapFeedback -= pos => { if (tapFx != null) tapFx.Spawn(pos); };

            if (typewriter != null)
                typewriter.OnFinished -= () => runner.NotifyLineRevealFinished();
        }

        private void WireButtons()
        {
            if (autoButton != null)
            {
                autoButton.onClick.RemoveAllListeners();
                autoButton.onClick.AddListener(() =>
                {
                    // включение авто – действие игрока: выключаем skip
                    if (runner.SkipEnabled) runner.SetSkip(false);
                    runner.SetAuto(!runner.AutoEnabled);
                });
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() =>
                {
                    // включение skip – действие игрока: выключаем auto
                    if (runner.AutoEnabled) runner.SetAuto(false);
                    runner.SetSkip(!runner.SkipEnabled);
                });
            }

            if (logButton != null)
            {
                logButton.onClick.RemoveAllListeners();
                logButton.onClick.AddListener(() =>
                {
                    if (logPanel == null) return;
                    logPanel.Show(runner.State.log);
                });
            }

            if (closeLogButton != null)
            {
                closeLogButton.onClick.RemoveAllListeners();
                closeLogButton.onClick.AddListener(() =>
                {
                    if (logPanel != null) logPanel.Hide();
                });
            }

            if (resetAutosaveButton != null)
            {
                resetAutosaveButton.onClick.RemoveAllListeners();
                resetAutosaveButton.onClick.AddListener(() =>
                {
                    runner.DeleteAutosaveAndRestart();
                });
            }
        }

        private void RefreshButtons()
        {
            if (autoButton != null)
                SetButtonState(autoButton, runner.AutoEnabled);

            if (skipButton != null)
            {
                skipButton.interactable = runner.SkipAllowed;
                SetButtonState(skipButton, runner.SkipEnabled && runner.SkipAllowed);
            }
        }

        private static void SetButtonState(Button btn, bool on)
        {
            if (btn == null) return;
            var colors = btn.colors;
            colors.normalColor = on ? new Color(0.25f, 0.8f, 0.35f, 1f) : colors.normalColor;
            btn.colors = colors;
        }

        // -------- Runner events --------

        private void OnLineStarted(VN.VNRunner.VNLinePayload line)
        {
            if (dialogueRoot != null) dialogueRoot.SetActive(true);

            if (speakerNameText != null)
                speakerNameText.text = line.isNarrator ? "" : (string.IsNullOrWhiteSpace(line.speakerName) ? (line.speakerId ?? "") : line.speakerName);

            if (typewriter != null)
                typewriter.Begin(line.text ?? "");
        }

        private void OnInstantReveal()
        {
            if (typewriter != null)
                typewriter.RevealInstant();
            else
                runner.NotifyLineRevealFinished();
        }

        private void OnLineHidden()
        {
            // оставляем панель, чтобы UI не "прыгало"
        }

        private void OnChoice(VN.VNRunner.VNChoicePayload payload)
        {
            if (choicePanel != null)
                choicePanel.Show(payload, idx => runner.Choose(idx));
        }

        private void OnChoiceHidden()
        {
            if (choicePanel != null)
                choicePanel.Hide();
        }

        private void OnBackground(VN.VNRunner.VNBackgroundPayload bg)
        {
            if (background == null) return;

            if (bg.crossfadeSeconds <= 0f) background.SetInstant(bg.sprite, bg.sprite != null);
            else background.Crossfade(bg.sprite, bg.crossfadeSeconds, bg.sprite != null);
        }

        private void OnSlot(VN.VNRunner.VNSlotPayload slot)
        {
            var view = slot.slot switch
            {
                VN.VNScreenSlot.Left => leftSlot,
                VN.VNScreenSlot.Center => centerSlot,
                VN.VNScreenSlot.Right => rightSlot,
                _ => null
            };

            if (view == null) return;

            if (!slot.visible || slot.sprite == null)
            {
                view.Crossfade(null, Mathf.Max(0f, slot.crossfadeSeconds), false);
                return;
            }

            if (slot.crossfadeSeconds <= 0f) view.SetInstant(slot.sprite, true);
            else view.Crossfade(slot.sprite, slot.crossfadeSeconds, true);
        }

        private void OnMusicPlay(VN.VNRunner.VNMusicPayload m)
        {
            if (audioController == null) return;
            audioController.PlayMusic(m.clip, m.fadeInSeconds, m.loop);
        }

        private void OnMusicStop(float fadeOut)
        {
            if (audioController == null) return;
            audioController.StopMusic(fadeOut);
        }

        private void OnSfx(AudioClip clip)
        {
            if (audioController == null) return;
            audioController.PlaySfx(clip);
        }
    }
}