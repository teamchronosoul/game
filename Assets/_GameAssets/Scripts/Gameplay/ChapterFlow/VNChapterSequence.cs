using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN
{
    [CreateAssetMenu(fileName = "VNChapterSequence", menuName = "VN/Chapter Sequence")]
    public class VNChapterSequence : ScriptableObject
    {
        [Serializable]
        public class FirstChapterVariant
        {
            [Tooltip("Например: Logics, Diplomats, Defenders, Seekers")]
            public string resultId;

            [Tooltip("Первая глава для этого результата")]
            public VNChapter chapter;
        }

        [Serializable]
        public class ChapterNextOverride
        {
            [Tooltip("Текущая глава")]
            public VNChapter chapter;

            [Tooltip("Какая глава должна идти после неё")]
            public VNChapter nextChapter;
        }

        [Header("First chapter by test result")]
        [SerializeField] private List<FirstChapterVariant> firstChapterVariants = new();

        [Header("Linear chapter order")]
        [SerializeField] private List<VNChapter> orderedChapters = new();

        [Header("Custom next chapter overrides")]
        [Tooltip("Используй это, чтобы 4 разные первые главы вели в одну общую вторую главу.")]
        [SerializeField] private List<ChapterNextOverride> nextOverrides = new();

        public bool TryGetFirstChapterByResult(string resultId, out VNChapter chapter)
        {
            chapter = null;

            if (string.IsNullOrWhiteSpace(resultId))
                return false;

            for (int i = 0; i < firstChapterVariants.Count; i++)
            {
                var variant = firstChapterVariants[i];
                if (variant == null || variant.chapter == null)
                    continue;

                if (string.Equals(variant.resultId?.Trim(), resultId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    chapter = variant.chapter;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetFirstOrderedChapter(out VNChapter chapter)
        {
            chapter = null;

            if (orderedChapters == null || orderedChapters.Count == 0)
                return false;

            for (int i = 0; i < orderedChapters.Count; i++)
            {
                if (orderedChapters[i] == null)
                    continue;

                chapter = orderedChapters[i];
                return true;
            }

            return false;
        }

        public bool TryGetNextChapter(string currentChapterId, out VNChapter nextChapter)
        {
            nextChapter = null;

            if (string.IsNullOrWhiteSpace(currentChapterId))
                return false;

            // Сначала проверяем ручные переходы.
            if (nextOverrides != null)
            {
                for (int i = 0; i < nextOverrides.Count; i++)
                {
                    var entry = nextOverrides[i];
                    if (entry == null || entry.chapter == null || entry.nextChapter == null)
                        continue;

                    if (string.Equals(entry.chapter.chapterId, currentChapterId, StringComparison.Ordinal))
                    {
                        nextChapter = entry.nextChapter;
                        return true;
                    }
                }
            }

            // Если ручного перехода нет — идём по обычному списку.
            int index = IndexOf(currentChapterId);
            if (index < 0)
                return false;

            int nextIndex = index + 1;
            if (nextIndex < 0 || nextIndex >= orderedChapters.Count)
                return false;

            nextChapter = orderedChapters[nextIndex];
            return nextChapter != null;
        }

        public int IndexOf(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId) || orderedChapters == null)
                return -1;

            for (int i = 0; i < orderedChapters.Count; i++)
            {
                var chapter = orderedChapters[i];
                if (chapter == null)
                    continue;

                if (string.Equals(chapter.chapterId, chapterId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }
}