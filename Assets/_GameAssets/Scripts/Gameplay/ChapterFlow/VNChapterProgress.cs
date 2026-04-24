using UnityEngine;

namespace VN
{
    public static class VNChapterProgress
    {
        private const string CurrentChapterIdKey = "VN.CurrentChapterId";
        private const string CurrentChapterIndexKey = "VN.CurrentChapterIndex";
        private const string HasProgressKey = "VN.HasChapterProgress";

        private const string TestCompletedKey = "VN.TestCompleted";
        private const string TestResultIdKey = "VN.TestResultId";

        public static bool HasProgress => PlayerPrefs.GetInt(HasProgressKey, 0) == 1;
        public static bool IsTestCompleted => PlayerPrefs.GetInt(TestCompletedKey, 0) == 1;

        public static void Save(string chapterId, int chapterIndex)
        {
            PlayerPrefs.SetInt(HasProgressKey, 1);
            PlayerPrefs.SetString(CurrentChapterIdKey, chapterId ?? "");
            PlayerPrefs.SetInt(CurrentChapterIndexKey, Mathf.Max(0, chapterIndex));
            PlayerPrefs.Save();
        }

        public static string LoadChapterId()
        {
            return PlayerPrefs.GetString(CurrentChapterIdKey, "");
        }

        public static int LoadChapterIndex()
        {
            return PlayerPrefs.GetInt(CurrentChapterIndexKey, 0);
        }

        public static void SaveTestCompleted(string resultId)
        {
            PlayerPrefs.SetInt(TestCompletedKey, 1);
            PlayerPrefs.SetString(TestResultIdKey, resultId ?? "");
            PlayerPrefs.Save();
        }

        public static string LoadTestResultId()
        {
            return PlayerPrefs.GetString(TestResultIdKey, "");
        }

        public static void ClearChapterProgress()
        {
            PlayerPrefs.DeleteKey(CurrentChapterIdKey);
            PlayerPrefs.DeleteKey(CurrentChapterIndexKey);
            PlayerPrefs.DeleteKey(HasProgressKey);
            PlayerPrefs.Save();
        }

        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(CurrentChapterIdKey);
            PlayerPrefs.DeleteKey(CurrentChapterIndexKey);
            PlayerPrefs.DeleteKey(HasProgressKey);
            PlayerPrefs.DeleteKey(TestCompletedKey);
            PlayerPrefs.DeleteKey(TestResultIdKey);
            PlayerPrefs.Save();
        }
    }
}