using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN
{
    [CreateAssetMenu(fileName = "VNAssetDatabase", menuName = "VN/Asset Database")]
    public class VNAssetDatabase : ScriptableObject
    {
        [Serializable]
        public class SpriteEntry
        {
            public string id;
            public Sprite asset;
        }

        [Serializable]
        public class AudioEntry
        {
            public string id;
            public AudioClip asset;
        }

        [Header("Backgrounds")]
        public List<SpriteEntry> backgrounds = new();

        [Header("Artifacts")]
        public List<SpriteEntry> artifacts = new();

        [Header("Audio")]
        public List<AudioEntry> music = new();
        public List<AudioEntry> sfx = new();

        private readonly Dictionary<string, Sprite> _bg = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _artifacts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> _msc = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> _sfx = new(StringComparer.Ordinal);

        private void OnEnable() => Rebuild();
        private void OnValidate() => Rebuild();

        public bool TryGetBackground(string id, out Sprite sprite)
        {
            RebuildIfNeeded();
            sprite = null;
            id = Normalize(id);
            if (string.IsNullOrEmpty(id)) return false;
            return _bg.TryGetValue(id, out sprite) && sprite != null;
        }

        public bool TryGetArtifact(string id, out Sprite sprite)
        {
            RebuildIfNeeded();
            sprite = null;
            id = Normalize(id);
            if (string.IsNullOrEmpty(id)) return false;
            return _artifacts.TryGetValue(id, out sprite) && sprite != null;
        }

        public bool TryGetMusic(string id, out AudioClip clip)
        {
            RebuildIfNeeded();
            clip = null;
            id = Normalize(id);
            if (string.IsNullOrEmpty(id)) return false;
            return _msc.TryGetValue(id, out clip) && clip != null;
        }

        public bool TryGetSfx(string id, out AudioClip clip)
        {
            RebuildIfNeeded();
            clip = null;
            id = Normalize(id);
            if (string.IsNullOrEmpty(id)) return false;
            return _sfx.TryGetValue(id, out clip) && clip != null;
        }

        private void RebuildIfNeeded()
        {
            if (_bg.Count == 0 && (backgrounds.Count > 0 || artifacts.Count > 0 || music.Count > 0 || sfx.Count > 0))
                Rebuild();
        }

        private void Rebuild()
        {
            _bg.Clear();
            _artifacts.Clear();
            _msc.Clear();
            _sfx.Clear();

            Build(backgrounds, _bg);
            Build(artifacts, _artifacts);
            Build(music, _msc);
            Build(sfx, _sfx);
        }

        private static void Build(List<SpriteEntry> list, Dictionary<string, Sprite> map)
        {
            if (list == null) return;
            foreach (var e in list)
            {
                if (e == null) continue;
                var id = Normalize(e.id);
                if (string.IsNullOrEmpty(id)) continue;
                if (!map.ContainsKey(id)) map[id] = e.asset;
            }
        }

        private static void Build(List<AudioEntry> list, Dictionary<string, AudioClip> map)
        {
            if (list == null) return;
            foreach (var e in list)
            {
                if (e == null) continue;
                var id = Normalize(e.id);
                if (string.IsNullOrEmpty(id)) continue;
                if (!map.ContainsKey(id)) map[id] = e.asset;
            }
        }

        private static string Normalize(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}