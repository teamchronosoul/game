using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Spine.Unity;

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
        public class PoseEmotionSpineAnimation
        {
            public VNPose pose = VNPose.Default;
            public VNEmotion emotion = VNEmotion.Neutral;

            [Tooltip("Опционально. Если задано, заменяет Default Base Skin Name для этой эмоции/позы. Например: man_stand, girl_stand, man_sit.")]
            public string baseSkinNameOverride;

            [FormerlySerializedAs("skinName")]
            [Tooltip("Слот лица/эмоции из Spine, который нужно включить. Пример: man_smile, man_sad, man_angry. Если attachment называется иначе, можно указать slot:attachment или slot|attachment.")]
            public string emotionSlotName;

            [Tooltip("Название Spine-анимации, которая будет включена для этой эмоции и зациклена, если Loop включен. В вашем экспорте обычно idle или static.")]
            public string animationName = "idle";

            public bool loop = true;
        }

        public readonly struct SpinePoseEmotionResult
        {
            public readonly SkeletonDataAsset skeletonDataAsset;
            public readonly string baseSkinName;
            public readonly string skinName;
            public readonly string animationName;
            public readonly bool loop;
            public readonly string[] emotionSlotsToClear;

            public SpinePoseEmotionResult(
                SkeletonDataAsset skeletonDataAsset,
                string baseSkinName,
                string skinName,
                string animationName,
                bool loop,
                string[] emotionSlotsToClear)
            {
                this.skeletonDataAsset = skeletonDataAsset;
                this.baseSkinName = baseSkinName;
                this.skinName = skinName;
                this.animationName = animationName;
                this.loop = loop;
                this.emotionSlotsToClear = emotionSlotsToClear ?? Array.Empty<string>();
            }
        }

        [Serializable]
        public class Character
        {
            [Tooltip("Уникальный ID. Используется в сценарии. Пример: 'anna'")]
            public string id;

            [Tooltip("Отображаемое имя в UI. Пример: 'Анна'")]
            public string displayName;

            [Tooltip("Дефолтная позиция появления персонажа при автоматическом показе во время реплики")]
            public VNScreenSlot defaultScreenSlot = VNScreenSlot.Center;

            [Tooltip("Таблица: Pose×Emotion -> Sprite. Используется как fallback, если у персонажа нет Spine-анимации или в сцене не назначен Spine slot.")]
            public List<PoseEmotionSprite> sprites = new List<PoseEmotionSprite>();

            [Header("Spine Animation Optional")]
            [Tooltip("Если заполнено, персонаж может отображаться через Spine SkeletonGraphic вместо обычного спрайта.")]
            public SkeletonDataAsset spineSkeletonData;

            [Tooltip("Базовый скин тела из Spine. По инструкции художника обычно man_stand или girl_stand.")]
            public string defaultBaseSkinName = "man_stand";

            [FormerlySerializedAs("defaultSkinName")]
            [Tooltip("Слот лица/эмоции по умолчанию. Например: man_smile или man. Можно оставить пустым, если дефолтное лицо уже настроено в setup pose.")]
            public string defaultEmotionSlotName;

            [Tooltip("Список Spine slots, которые относятся именно к лицам/эмоциям этого персонажа и должны очищаться перед включением новой эмоции. Заполняется отдельно для каждого персонажа: для мальчика man, man_smile, man_sad; для девочки girl, girl_smile, girl_sad и т.д. Не добавляй сюда одежду/аксессуары.")]
            public List<string> spineEmotionSlotsToClear = new List<string>();

            [Tooltip("Анимация по умолчанию, если для Pose×Emotion нет отдельной настройки. Можно поставить idle или static.")]
            public string defaultAnimationName = "idle";

            public bool defaultLoop = true;

            [Tooltip("Таблица: Pose×Emotion -> Spine emotion slot + animation.")]
            public List<PoseEmotionSpineAnimation> spineAnimations = new List<PoseEmotionSpineAnimation>();
        }

        [SerializeField] private List<Character> characters = new List<Character>();
        public IReadOnlyList<Character> Characters => characters;

        private readonly Dictionary<string, Character> _byId = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<PoseEmotionKey, Sprite>> _spriteMap = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<PoseEmotionKey, PoseEmotionSpineAnimation>> _spineMap = new(StringComparer.Ordinal);

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

        public bool TryGetDefaultScreenSlot(string characterId, out VNScreenSlot slot)
        {
            slot = VNScreenSlot.Center;

            if (!TryGetCharacter(characterId, out var ch))
                return false;

            slot = ch.defaultScreenSlot;
            return true;
        }

        public bool TryGetSprite(string characterId, VNPose pose, VNEmotion emotion, out Sprite sprite)
        {
            RebuildCacheIfNeeded();
            sprite = null;

            characterId = NormalizeId(characterId);
            if (string.IsNullOrEmpty(characterId)) return false;

            if (!_spriteMap.TryGetValue(characterId, out var map))
                return false;

            if (map.TryGetValue(new PoseEmotionKey(pose, emotion), out sprite) && sprite != null)
                return true;

            if (map.TryGetValue(new PoseEmotionKey(pose, VNEmotion.Neutral), out sprite) && sprite != null)
                return true;

            if (map.TryGetValue(new PoseEmotionKey(VNPose.Default, emotion), out sprite) && sprite != null)
                return true;

            if (map.TryGetValue(new PoseEmotionKey(VNPose.Default, VNEmotion.Neutral), out sprite) && sprite != null)
                return true;

            return false;
        }

        public bool TryGetSpineAnimation(
            string characterId,
            VNPose pose,
            VNEmotion emotion,
            out SpinePoseEmotionResult result)
        {
            RebuildCacheIfNeeded();
            result = default;

            characterId = NormalizeId(characterId);
            if (string.IsNullOrEmpty(characterId)) return false;

            if (!_byId.TryGetValue(characterId, out var ch) || ch == null || ch.spineSkeletonData == null)
                return false;

            PoseEmotionSpineAnimation entry = null;
            if (_spineMap.TryGetValue(characterId, out var map))
            {
                if (!map.TryGetValue(new PoseEmotionKey(pose, emotion), out entry))
                    if (!map.TryGetValue(new PoseEmotionKey(pose, VNEmotion.Neutral), out entry))
                        if (!map.TryGetValue(new PoseEmotionKey(VNPose.Default, emotion), out entry))
                            map.TryGetValue(new PoseEmotionKey(VNPose.Default, VNEmotion.Neutral), out entry);
            }

            var baseSkinName = entry != null && !string.IsNullOrWhiteSpace(entry.baseSkinNameOverride)
                ? entry.baseSkinNameOverride.Trim()
                : NormalizeId(ch.defaultBaseSkinName);

            var skinName = entry != null && !string.IsNullOrWhiteSpace(entry.emotionSlotName)
                ? entry.emotionSlotName.Trim()
                : NormalizeId(ch.defaultEmotionSlotName);

            var animationName = entry != null && !string.IsNullOrWhiteSpace(entry.animationName)
                ? entry.animationName.Trim()
                : NormalizeId(ch.defaultAnimationName);

            var loop = entry?.loop ?? ch.defaultLoop;
            var emotionSlotsToClear = BuildEmotionSlotsToClear(ch);

            result = new SpinePoseEmotionResult(
                ch.spineSkeletonData,
                baseSkinName,
                skinName,
                animationName,
                loop,
                emotionSlotsToClear);

            return true;
        }

        private static string[] BuildEmotionSlotsToClear(Character ch)
        {
            if (ch == null || ch.spineEmotionSlotsToClear == null || ch.spineEmotionSlotsToClear.Count == 0)
                return Array.Empty<string>();

            var result = new List<string>(ch.spineEmotionSlotsToClear.Count);
            for (var i = 0; i < ch.spineEmotionSlotsToClear.Count; i++)
            {
                var slotName = NormalizeId(ch.spineEmotionSlotsToClear[i]);
                if (string.IsNullOrEmpty(slotName))
                    continue;

                var exists = false;
                for (var j = 0; j < result.Count; j++)
                {
                    if (string.Equals(result[j], slotName, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    result.Add(slotName);
            }

            return result.Count == 0 ? Array.Empty<string>() : result.ToArray();
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
            _spineMap.Clear();

            if (characters == null) return;

            foreach (var ch in characters)
            {
                if (ch == null) continue;

                var id = NormalizeId(ch.id);
                if (string.IsNullOrEmpty(id)) continue;

                if (_byId.ContainsKey(id)) continue;

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

                var spineMap = new Dictionary<PoseEmotionKey, PoseEmotionSpineAnimation>();
                if (ch.spineAnimations != null)
                {
                    foreach (var e in ch.spineAnimations)
                    {
                        if (e == null) continue;
                        var key = new PoseEmotionKey(e.pose, e.emotion);
                        if (!spineMap.ContainsKey(key))
                            spineMap[key] = e;
                    }
                }

                _spineMap[id] = spineMap;
            }
        }

        private static string NormalizeId(string id) => string.IsNullOrWhiteSpace(id) ? null : id.Trim();
    }
}