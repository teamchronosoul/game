using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VN;

namespace _GameAssets.Scripts.Gameplay.UI
{
    public class VNMainMenuUGUI : MonoBehaviour
    {
        [Header("Database")]
        [SerializeField] private ArchetypeDatabase database;

        [Header("VN")]
        [SerializeField] private VNRunner runner;
        [SerializeField] private VNChapterSequence chapterSequence;

        [Header("UI")]
        [SerializeField] private Image mentorImage;
        [SerializeField] private Image mentorIdImage;
        [SerializeField] private Image mirrorImage;

        [Header("Start / Continue Button")]
        [SerializeField] private Button continueButton;


        [Header("Behaviour")]
        [SerializeField] private bool hideMenuOnStart = true;
        [SerializeField] private bool autoSaveNextChapterWhenChapterEnds = true;

        private VNMbtiState mbti;
        private ArchetypeData archetype;

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
        }

        private void WireButtons()
        {
            if (continueButton == null)
                return;

            continueButton.onClick.RemoveListener(OnContinueClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
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
        }

        private void ApplyMenu()
        {
            if (archetype == null)
                return;

            if (mentorImage != null)
            {
                mentorImage.sprite = archetype.mentorSprite;

                if (mentorImage.sprite != null)
                    mentorImage.SetNativeSize();
            }

            if (mentorIdImage != null)
                mentorIdImage.sprite = archetype.mentorIdSprite;

            if (mirrorImage != null)
                mirrorImage.sprite = archetype.mirrorSprite;
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