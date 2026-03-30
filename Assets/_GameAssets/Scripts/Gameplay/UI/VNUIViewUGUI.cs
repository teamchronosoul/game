using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace VN.UI
{
    public class VNUIViewUGUI : MonoBehaviour
    {
        [Header("Runner")]
        [SerializeField] private VN.VNRunner runner;

        [Header("Dialogue UI")]
        [SerializeField] private GameObject dialogueRoot;

        [Tooltip("Корневой объект плашки имени. Будет скрываться для Narrator.")]
        [SerializeField] private GameObject speakerNameRoot;

        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private VNTypewriterUGUI typewriter;

        [Header("Player")]
        [Tooltip("Имя, которое будет показано вместо speakerId = YOU")]
        [SerializeField] private string playerDisplayName = "Player";

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

        private System.Action _typewriterFinishedHandler;
        private System.Action<Vector2> _tapFeedbackHandler;

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

            _tapFeedbackHandler = pos =>
            {
                if (tapFx != null)
                    tapFx.Spawn(pos);
            };
            runner.OnTapFeedback += _tapFeedbackHandler;

            if (typewriter != null)
            {
                _typewriterFinishedHandler = () => runner.NotifyLineRevealFinished();
                typewriter.OnFinished += _typewriterFinishedHandler;
            }

            WireButtons();
            RefreshButtons();

            if (logPanel != null) logPanel.Hide();
            if (choicePanel != null) choicePanel.Hide();

            SetSpeakerPlateVisible(false);

            if (dialogueRoot != null)
                dialogueRoot.SetActive(true);
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

            if (_tapFeedbackHandler != null)
                runner.OnTapFeedback -= _tapFeedbackHandler;

            if (typewriter != null && _typewriterFinishedHandler != null)
                typewriter.OnFinished -= _typewriterFinishedHandler;
        }

        private void WireButtons()
        {
            if (autoButton != null)
            {
                autoButton.onClick.RemoveAllListeners();
                autoButton.onClick.AddListener(() =>
                {
                    if (runner.SkipEnabled) runner.SetSkip(false);
                    runner.SetAuto(!runner.AutoEnabled);
                });
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() =>
                {
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
            colors.normalColor = on ? new Color(0.25f, 0.8f, 0.35f, 1f) : Color.white;
            btn.colors = colors;
        }

        private void OnLineStarted(VN.VNRunner.VNLinePayload line)
        {
            if (dialogueRoot != null)
                dialogueRoot.SetActive(true);

            string shownSpeakerName = ResolveShownSpeakerName(line);
            bool showSpeakerPlate = !line.isNarrator && !string.IsNullOrWhiteSpace(shownSpeakerName);

            SetSpeakerPlateVisible(showSpeakerPlate);

            if (speakerNameText != null)
                speakerNameText.text = showSpeakerPlate ? shownSpeakerName : "";

            if (typewriter != null)
                typewriter.Begin(line.text ?? "");
        }

        private string ResolveShownSpeakerName(VN.VNRunner.VNLinePayload line)
        {
            if (line.isNarrator)
                return "";

            string speakerId = (line.speakerId ?? "").Trim();
            string speakerName = (line.speakerName ?? "").Trim();

            if (string.Equals(speakerId, "YOU", System.StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(playerDisplayName) ? "You" : playerDisplayName;

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
            if (typewriter != null)
                typewriter.RevealInstant();
            else
                runner.NotifyLineRevealFinished();
        }

        private void OnLineHidden()
        {
            // Панель диалога оставляем, чтобы UI не прыгал
        }

        private void OnChoice(VN.VNRunner.VNChoicePayload payload)
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

            if (slot.crossfadeSeconds <= 0f)
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
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].sprite != null)
                    images[i].SetNativeSize();
            }
        }

        private IEnumerator ApplyNativeSizeNextFrame(VNCrossfadeImageUGUI view)
        {
            yield return null;
            ApplyNativeSize(view);
        }

        private void OnMusicPlay(VN.VNRunner.VNMusicPayload m)
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

        private void OnMusicStop(float fadeOut)
        {
            if (audioController == null) return;
            audioController.StopMusic(fadeOut);
        }

        private void OnSfx(VN.VNRunner.VNSfxPayload sfx)
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
    }
}