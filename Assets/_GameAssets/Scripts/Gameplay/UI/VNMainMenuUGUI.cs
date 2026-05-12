using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VN;
using VN.UI;

namespace _GameAssets.Scripts.Gameplay.UI
{
    public class VNMainMenuUGUI : MonoBehaviour
    {
        [Header("Database")]
        [SerializeField] private ArchetypeDatabase database;

        [Header("VN")]
        [SerializeField] private VNRunner runner;
        [SerializeField] private VNChapterSequence chapterSequence;

        [Header("Animated Mentor Optional")]
        [Tooltip("База персонажей VN. Нужна, чтобы в главном меню показать назначенного наставника через Spine idle-анимацию.")]
        [SerializeField] private VNCharacterDatabase characterDatabase;

        [Tooltip("UI-slot с SkeletonGraphic + VNSpineCharacterSlotUGUI для наставника в главном меню. Если не назначен или у наставника нет Spine, используется старый mentorImage.")]
        [SerializeField] private VNSpineCharacterSlotUGUI mentorSpineSlot;

        [Tooltip("Если включено, главное меню пытается показать наставника через Spine. Если не получится, автоматически вернется к mentorImage.")]
        [SerializeField] private bool useAnimatedMentorIfAvailable = true;

        [Tooltip("Если включено, обычный mentorImage скрывается, когда наставник успешно показан через Spine.")]
        [SerializeField] private bool hideSpriteMentorWhenAnimated = true;

        [Tooltip("Если включено, Spine-наставник в меню автоматически встанет центром туда же, где стоит старый mentorImage.")]
        [SerializeField] private bool alignAnimatedMentorToMentorImage = true;

        [Tooltip("Если включено, Spine-slot наставника дополнительно копирует sizeDelta mentorImage, когда они находятся под одним parent. Обычно выключено, чтобы не менять масштаб Spine.")]
        [SerializeField] private bool copyMentorImageSizeToSpineSlot = false;

        [Tooltip("Если включено и Spine-slot находится под тем же parent, что и mentorImage, будут скопированы anchors/pivot/anchoredPosition.")]
        [SerializeField] private bool copyMentorImageLayoutToSpineSlotWhenSameParent = false;

        [Tooltip("Fallback ID персонажа из VNCharacterDatabase, если в ArchetypeData не заполнен Mentor Character Id.")]
        [SerializeField] private string fallbackMentorCharacterId;

        [Tooltip("Pose для наставника в главном меню, если в ArchetypeData не задано другое значение.")]
        [SerializeField] private VNPose fallbackMentorPose = VNPose.Default;

        [Tooltip("Emotion для наставника в главном меню, если в ArchetypeData не задано другое значение.")]
        [SerializeField] private VNEmotion fallbackMentorEmotion = VNEmotion.Neutral;

        [SerializeField] [Min(0f)] private float animatedMentorFadeSeconds = 0f;

        [Header("UI")]
        [SerializeField] private Image mentorImage;
        [SerializeField] private Image mentorIdImage;
        [SerializeField] private Image mirrorImage;

        [Header("Start / Continue Button")]
        [SerializeField] private Button continueButton;

        [Header("Behaviour")]
        [SerializeField] private bool hideMenuOnStart = true;
        [SerializeField] private bool autoSaveNextChapterWhenChapterEnds = true;

        [Header("Mentor Intro Badge")]
        [Tooltip("Корневой объект плашки с интро-фразой наставника. Поставь его в нужное место на Canvas, как на макете.")]
        [SerializeField] private GameObject mentorIntroBadgeRoot;

        [Tooltip("Текст внутри плашки. Используется как fallback, если не назначен отдельный VNTypewriterUGUI.")]
        [SerializeField] private TextMeshProUGUI mentorIntroText;

        [Tooltip("Typewriter для текста плашки. Можно использовать тот же компонент VNTypewriterUGUI, что и в сюжетных репликах, но с отдельным TextMeshProUGUI.")]
        [SerializeField] private VNTypewriterUGUI mentorIntroTypewriter;

        [Tooltip("Кнопка/невидимая tap-area поверх наставника. По нажатию показывает следующую фразу. Если пусто, скрипт попробует найти Button на mentorImage или mentorSpineSlot.")]
        [SerializeField] private Button mentorTapButton;

        [Tooltip("Если Mentor Tap Button не назначен, попробовать автоматически взять Button с объекта mentorImage или mentorSpineSlot.")]
        [SerializeField] private bool autoFindMentorTapButton = true;

        [Tooltip("Если включено, первая фраза наставника показывается автоматически при каждом открытии главного меню.")]
        [SerializeField] private bool autoShowFirstMentorIntroOnMenuOpen = true;

        [Tooltip("Если тапнуть по наставнику, пока фраза еще печатается, текущая фраза сначала раскрывается мгновенно. Следующая фраза пойдет уже следующим тапом.")]
        [SerializeField] private bool revealCurrentIntroOnTapWhileTyping = true;

        [Tooltip("Если у выбранного архетипа нет фраз, плашка будет скрыта.")]
        [SerializeField] private bool hideMentorIntroBadgeWhenEmpty = true;

        private VNMbtiState mbti;
        private ArchetypeData archetype;
        private Button _resolvedMentorTapButton;
        private int _nextMentorIntroPhraseIndex;

        private void OnEnable()
        {
            if (runner != null)
                runner.OnChapterEnded += OnChapterEnded;

            WireButtons();
            LoadMbtiFromSave();
            RefreshContinueButton();
        }

        private void OnDisable()
        {
            if (runner != null)
                runner.OnChapterEnded -= OnChapterEnded;

            if (continueButton != null)
                continueButton.onClick.RemoveListener(OnContinueClicked);

            UnwireMentorTapButton();
            StopMentorIntroTyping();
        }

        private void WireButtons()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
                continueButton.onClick.AddListener(OnContinueClicked);
            }

            WireMentorTapButton();
        }

        private void LoadMbtiFromSave()
        {
            mbti = null;
            archetype = null;

            if (VNAutosave.TryLoad(out var state))
            {
                mbti = state.mbti;

                if (database != null && mbti != null && !string.IsNullOrWhiteSpace(mbti.ArchetypeId))
                    archetype = database.GetById(mbti.ArchetypeId);
            }

            if (archetype == null)
            {
                string savedResultId = VNChapterProgress.LoadTestResultId();

                if (database != null && !string.IsNullOrWhiteSpace(savedResultId))
                    archetype = database.GetById(savedResultId);
            }

            if (archetype != null)
                ApplyMenu();
            else
            {
                HideAnimatedMentor();
                HideMentorIntroBadge();
            }
        }

        private void ApplyMenu()
        {
            if (archetype == null)
                return;

            var animatedMentorShown = TryApplyAnimatedMentor();

            if (mentorImage != null)
            {
                if (animatedMentorShown && hideSpriteMentorWhenAnimated)
                {
                    mentorImage.gameObject.SetActive(false);
                }
                else
                {
                    mentorImage.gameObject.SetActive(true);
                    mentorImage.sprite = archetype.mentorSprite;

                    if (mentorImage.sprite != null)
                        mentorImage.SetNativeSize();
                }
            }

            if (!animatedMentorShown)
                HideAnimatedMentor();

            if (mentorIdImage != null)
                mentorIdImage.sprite = archetype.mentorIdSprite;

            if (mirrorImage != null)
                mirrorImage.sprite = archetype.mirrorSprite;

            ResetMentorIntroPhraseCycle();

            if (autoShowFirstMentorIntroOnMenuOpen)
                ShowNextMentorIntroPhrase();
            else
                HideMentorIntroBadge();
        }

        private bool TryApplyAnimatedMentor()
        {
            if (!useAnimatedMentorIfAvailable || mentorSpineSlot == null || characterDatabase == null || archetype == null)
                return false;

            var characterId = ResolveMentorCharacterId();
            if (string.IsNullOrWhiteSpace(characterId))
                return false;

            var pose = archetype.overrideMainMenuMentorPose ? archetype.mainMenuMentorPose : fallbackMentorPose;
            var emotion = archetype.overrideMainMenuMentorEmotion ? archetype.mainMenuMentorEmotion : fallbackMentorEmotion;

            if (!characterDatabase.TryGetSpineAnimation(characterId, pose, emotion, out var spine))
            {
                Debug.LogWarning($"[VNMainMenuUGUI] Animated mentor '{characterId}' is not configured in VNCharacterDatabase or has no Spine data. Fallback to mentor sprite.", this);
                return false;
            }

            if (alignAnimatedMentorToMentorImage && mentorImage != null)
            {
                mentorSpineSlot.AlignCenterToImageSlot(
                    mentorImage.rectTransform,
                    copyMentorImageSizeToSpineSlot,
                    copyMentorImageLayoutToSpineSlotWhenSameParent);
            }

            mentorSpineSlot.Show(
                spine.skeletonDataAsset,
                spine.baseSkinName,
                spine.skinName,
                spine.animationName,
                true,
                spine.emotionSlotsToClear,
                animatedMentorFadeSeconds,
                true);

            return true;
        }

        private string ResolveMentorCharacterId()
        {
            if (archetype != null && !string.IsNullOrWhiteSpace(archetype.mentorCharacterId))
                return archetype.mentorCharacterId.Trim();

            return string.IsNullOrWhiteSpace(fallbackMentorCharacterId)
                ? ""
                : fallbackMentorCharacterId.Trim();
        }

        private void HideAnimatedMentor()
        {
            if (mentorSpineSlot != null)
                mentorSpineSlot.SetInstantHidden();

            if (mentorImage != null)
                mentorImage.gameObject.SetActive(true);
        }

        private void WireMentorTapButton()
        {
            UnwireMentorTapButton();

            _resolvedMentorTapButton = ResolveMentorTapButton();
            if (_resolvedMentorTapButton == null)
                return;

            _resolvedMentorTapButton.onClick.RemoveListener(OnMentorTapped);
            _resolvedMentorTapButton.onClick.AddListener(OnMentorTapped);
        }

        private void UnwireMentorTapButton()
        {
            if (_resolvedMentorTapButton != null)
                _resolvedMentorTapButton.onClick.RemoveListener(OnMentorTapped);

            _resolvedMentorTapButton = null;
        }

        private Button ResolveMentorTapButton()
        {
            if (mentorTapButton != null)
                return mentorTapButton;

            if (!autoFindMentorTapButton)
                return null;

            if (mentorImage != null && mentorImage.TryGetComponent<Button>(out var imageButton))
                return imageButton;

            if (mentorSpineSlot != null && mentorSpineSlot.TryGetComponent<Button>(out var spineButton))
                return spineButton;

            return null;
        }

        public void OnMentorTapped()
        {
            if (revealCurrentIntroOnTapWhileTyping && mentorIntroTypewriter != null && mentorIntroTypewriter.IsPlaying)
            {
                mentorIntroTypewriter.RevealInstant();
                return;
            }

            ShowNextMentorIntroPhrase();
        }

        public void ShowNextMentorIntroPhrase()
        {
            if (archetype == null)
            {
                HideMentorIntroBadge();
                return;
            }

            var phrases = archetype.GetMentorIntroPhrases();
            if (phrases == null || phrases.Length == 0)
            {
                if (hideMentorIntroBadgeWhenEmpty)
                    HideMentorIntroBadge();
                return;
            }

            if (_nextMentorIntroPhraseIndex < 0 || _nextMentorIntroPhraseIndex >= phrases.Length)
                _nextMentorIntroPhraseIndex = 0;

            var phrase = phrases[_nextMentorIntroPhraseIndex];
            _nextMentorIntroPhraseIndex = (_nextMentorIntroPhraseIndex + 1) % phrases.Length;

            ShowMentorIntroPhrase(phrase);
        }

        private void ResetMentorIntroPhraseCycle()
        {
            _nextMentorIntroPhraseIndex = 0;
        }

        private void ShowMentorIntroPhrase(string phrase)
        {
            phrase = string.IsNullOrWhiteSpace(phrase) ? "" : phrase.Trim();

            if (string.IsNullOrEmpty(phrase))
            {
                if (hideMentorIntroBadgeWhenEmpty)
                    HideMentorIntroBadge();
                return;
            }

            SetMentorIntroBadgeVisible(true);

            if (mentorIntroTypewriter != null)
            {
                mentorIntroTypewriter.Begin(phrase);
            }
            else if (mentorIntroText != null)
            {
                mentorIntroText.text = phrase;
            }
        }

        private void HideMentorIntroBadge()
        {
            StopMentorIntroTyping();

            if (mentorIntroText != null)
                mentorIntroText.text = "";

            SetMentorIntroBadgeVisible(false);
        }

        private void StopMentorIntroTyping()
        {
            if (mentorIntroTypewriter != null)
                mentorIntroTypewriter.StopTyping();
        }

        private void SetMentorIntroBadgeVisible(bool visible)
        {
            if (mentorIntroBadgeRoot != null)
            {
                mentorIntroBadgeRoot.SetActive(visible);
                return;
            }

            if (mentorIntroText != null)
                mentorIntroText.gameObject.SetActive(visible);
        }

        private void RefreshContinueButton()
        {
            if (continueButton != null)
                continueButton.interactable = runner != null && chapterSequence != null;
        }

        private void OnContinueClicked()
        {
            if (runner == null || chapterSequence == null)
                return;

            if (VNChapterProgress.HasProgress)
                ContinueSavedChapter();
            else
                StartFirstStoryChapterByTestResult();
        }

        private void ContinueSavedChapter()
        {
            string chapterId = VNChapterProgress.LoadChapterId();

            if (string.IsNullOrWhiteSpace(chapterId))
            {
                VNChapterProgress.ClearChapterProgress();
                RefreshContinueButton();
                StartFirstStoryChapterByTestResult();
                return;
            }

            HideMenu();
            runner.StartNew(chapterId);
        }

        private void StartFirstStoryChapterByTestResult()
        {
            string resultId = "";

            if (mbti != null && !string.IsNullOrWhiteSpace(mbti.ArchetypeId))
                resultId = mbti.ArchetypeId;

            if (string.IsNullOrWhiteSpace(resultId))
                resultId = VNChapterProgress.LoadTestResultId();

            VNChapter firstChapter = null;

            if (!string.IsNullOrWhiteSpace(resultId))
                chapterSequence.TryGetFirstChapterByResult(resultId, out firstChapter);

            if (firstChapter == null)
            {
                Debug.LogWarning($"[VNMainMenuUGUI] First chapter for result '{resultId}' not found. Trying first ordered chapter.");

                if (!chapterSequence.TryGetFirstOrderedChapter(out firstChapter) || firstChapter == null)
                {
                    Debug.LogWarning("[VNMainMenuUGUI] Can't start first story chapter.");
                    return;
                }
            }

            SaveChapterProgress(firstChapter.chapterId);

            HideMenu();
            runner.StartNew(firstChapter.chapterId);

            RefreshContinueButton();
        }

        private void OnChapterEnded(string endedChapterId)
        {
            if (chapterSequence == null)
            {
                RefreshContinueButton();
                return;
            }

            if (!chapterSequence.TryGetNextChapter(endedChapterId, out var nextChapter) || nextChapter == null)
            {
                RefreshContinueButton();
                return;
            }

            if (autoSaveNextChapterWhenChapterEnds)
                SaveChapterProgress(nextChapter.chapterId);

            RefreshContinueButton();
        }

        private void SaveChapterProgress(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
                return;

            int index = chapterSequence != null ? chapterSequence.IndexOf(chapterId) : 0;
            VNChapterProgress.Save(chapterId, index);
        }

        private void HideMenu()
        {
            if (!hideMenuOnStart)
                return;

            gameObject.SetActive(false);
        }
    }
}
