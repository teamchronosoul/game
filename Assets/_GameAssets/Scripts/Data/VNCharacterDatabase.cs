using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN
{
    [CreateAssetMenu(fileName = "VNCharacterDatabase", menuName = "VN/Character Database")]
    public class VNCharacterDatabase : ScriptableObject
    {
        [Serializable]
        public class PoseEmotionSprite
        {
            public VNPose pose = VNPose.Default;
            public VNEmotion emotion = VNEmotion.Neutral;
            public Sprite sprite;
        }

        [Serializable]
        public class Character
        {
            [Tooltip("Уникальный ID. Используется в сценарии. Пример: 'anna'")]
            public string id;

            [Tooltip("Отображаемое имя в UI. Пример: 'Анна'")]
            public string displayName;

            [Tooltip("Таблица: Pose×Emotion -> Sprite")]
            public List<PoseEmotionSprite> sprites = new List<PoseEmotionSprite>();
        }

        [SerializeField] private List<Character> characters = new List<Character>();
        public IReadOnlyList<Character> Characters => characters;

        private readonly Dictionary<string, Character> _byId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<PoseEmotionKey, Sprite>> _spriteMap = new(StringComparer.Ordinal);

        [Serializable]
        private struct PoseEmotionKey : IEquatable<PoseEmotionKey>
        {
            public VNPose pose;
            public VNEmotion emotion;

            public PoseEmotionKey(VNPose pose, VNEmotion emotion)
            {
                this.pose = pose;
                this.emotion = emotion;
            }

            public bool Equals(PoseEmotionKey other) => pose == other.pose && emotion == other.emotion;
            public override bool Equals(object obj) => obj is PoseEmotionKey other && Equals(other);
            public override int GetHashCode() => ((int)pose * 397) ^ (int)emotion;
        }

        private void OnEnable() => RebuildCache();
        private void OnValidate() => RebuildCache();

        public bool TryGetCharacter(string characterId, out Character character)
        {
            RebuildCacheIfNeeded();
            character = null;
            characterId = NormalizeId(characterId);
            if (string.IsNullOrEmpty(characterId)) return false;
            return _byId.TryGetValue(characterId, out character) && character != null;
        }

        public bool TryGetDisplayName(string characterId, out string displayName)
        {
            displayName = "";
            if (!TryGetCharacter(characterId, out var ch)) return false;
            displayName = ch.displayName ?? "";
            return true;
        }

        public bool TryGetSprite(string characterId, VNPose pose, VNEmotion emotion, out Sprite sprite)
        {
            RebuildCacheIfNeeded();
            sprite = null;

            characterId = NormalizeId(characterId);
            if (string.IsNullOrEmpty(characterId)) return false;

            if (_spriteMap.TryGetValue(characterId, out var map))
            {
                map.TryGetValue(new PoseEmotionKey(pose, emotion), out sprite);
                return sprite != null;
            }

            return false;
        }

        private void RebuildCacheIfNeeded()
        {
            if (_byId.Count == 0 && characters != null && characters.Count > 0)
                RebuildCache();
        }

        private void RebuildCache()
        {
            _byId.Clear();
            _spriteMap.Clear();

            if (characters == null) return;

            foreach (var ch in characters)
            {
                if (ch == null) continue;

                var id = NormalizeId(ch.id);
                if (string.IsNullOrEmpty(id)) continue;

                if (_byId.ContainsKey(id)) continue; // первый выигрывает

                _byId[id] = ch;

                var map = new Dictionary<PoseEmotionKey, Sprite>();
                if (ch.sprites != null)
                {
                    foreach (var e in ch.sprites)
                    {
                        if (e == null) continue;
                        var key = new PoseEmotionKey(e.pose, e.emotion);
                        if (!map.ContainsKey(key))
                            map[key] = e.sprite;
                    }
                }

                _spriteMap[id] = map;
            }
        }

        private static string NormalizeId(string id) => string.IsNullOrWhiteSpace(id) ? null : id.Trim();
    }
}