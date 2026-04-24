using UnityEngine;

namespace VN
{
    public class VNGameBootstrap : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private VNRunner runner;
        [SerializeField] private VNChapterSequence chapterSequence;

        [Header("Startup")]
        [SerializeField] private VNChapter testChapter;

        [Header("UI")]
        [SerializeField] private GameObject mainMenuRoot;

        [Header("Behaviour")]
        [SerializeField] private bool clearAutosaveBeforeTest = true;

        [Header("Main Menu Music")]
        [SerializeField] private string mainMenuMusicId = "home_page";
        [SerializeField] private bool loopMainMenuMusic = true;
        [SerializeField] [Min(0f)] private float mainMenuMusicFadeInSeconds = 0f;
        
        private bool _waitingForTestEnd;

        private void OnEnable()
        {
            if (runner != null)
            {
                runner.OnChapterEnded += OnChapterEnded;
                runner.OnMainMenuRequested += ShowMainMenu;
            }
        }

        private void OnDisable()
        {
            if (runner != null)
            {
                runner.OnChapterEnded -= OnChapterEnded;
                runner.OnMainMenuRequested -= ShowMainMenu;
            }
        }

        private void Start()
        {
            Boot();
        }

        public void Boot()
        {
            if (runner == null)
            {
                Debug.LogWarning("[VNGameBootstrap] Runner is not assigned.");
                ShowMainMenu();
                return;
            }

            if (!VNChapterProgress.IsTestCompleted)
            {
                StartTest();
                return;
            }

            ShowMainMenu();
        }

        private void StartTest()
        {
            if (testChapter == null)
            {
                Debug.LogWarning("[VNGameBootstrap] Test chapter is not assigned.");
                ShowMainMenu();
                return;
            }

            if (clearAutosaveBeforeTest)
                VNAutosave.Delete();

            HideMainMenu();

            _waitingForTestEnd = true;
            runner.StartNew(testChapter.chapterId);
        }

        private void OnChapterEnded(string endedChapterId)
        {
            if (!_waitingForTestEnd)
                return;

            if (testChapter == null)
                return;

            if (!string.Equals(endedChapterId, testChapter.chapterId, System.StringComparison.Ordinal))
                return;

            _waitingForTestEnd = false;

            string resultId = runner.Mbti != null ? runner.Mbti.ArchetypeId : "";
            VNChapterProgress.SaveTestCompleted(resultId);

            if (runner.State != null)
                VNAutosave.Save(runner.State);

            ShowMainMenu();
        }
        

        private void ShowMainMenu()
        {
            PlayMainMenuMusic();
            
            if (mainMenuRoot != null)
                mainMenuRoot.SetActive(true);
        }

        private void PlayMainMenuMusic()
        {
            if (string.IsNullOrWhiteSpace(mainMenuMusicId))
                return;

            Sound.PlayMusic(mainMenuMusicId, loopMainMenuMusic, mainMenuMusicFadeInSeconds);
        }

        private void HideMainMenu()
        {
            if (mainMenuRoot != null)
                mainMenuRoot.SetActive(false);
        }
    }
}