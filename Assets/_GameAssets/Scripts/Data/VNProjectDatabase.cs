using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN
{
    [CreateAssetMenu(fileName = "VNProjectDatabase", menuName = "VN/Project Database")]
    public class VNProjectDatabase : ScriptableObject
    {
        public VNCharacterDatabase characterDatabase;
        public VNAssetDatabase assetDatabase;
        public List<VNChapter> chapters = new();

        private readonly Dictionary<string, VNChapter> _byId = new(StringComparer.Ordinal);

        private void OnEnable() => Rebuild();
        private void OnValidate() => Rebuild();

        public bool TryGetChapter(string chapterId, out VNChapter chapter)
        {
            RebuildIfNeeded();
            chapter = null;
            chapterId = Norm(chapterId);
            if (string.IsNullOrEmpty(chapterId)) return false;
            return _byId.TryGetValue(chapterId, out chapter) && chapter != null;
        }

        private void RebuildIfNeeded()
        {
            if (_byId.Count == 0 && chapters != null && chapters.Count > 0)
                Rebuild();
        }

        private void Rebuild()
        {
            _byId.Clear();
            if (chapters == null) return;

            foreach (var ch in chapters)
            {
                if (ch == null) continue;
                var id = Norm(ch.chapterId);
                if (string.IsNullOrEmpty(id)) continue;
                if (!_byId.ContainsKey(id)) _byId[id] = ch;
            }
        }

        private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}