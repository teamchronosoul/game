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
        public sealed class VNVfxDefinition
        {
            public string id;
            public GameObject prefab;

            [Tooltip("Если > 0, VFX активно играет это время. Потом эмиссия останавливается и начинается мягкое затухание.")]
            public float lifetime = -1f;

            [Tooltip("Сколько секунд дать VFX на мягкое затухание после остановки эмиссии")]
            public float softStopSeconds = 0.8f;

            public float ResolveLifetime()
            {
                if (lifetime > 0f)
                    return lifetime;

                if (prefab == null)
                    return -1f;

                var particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                float maxLifetime = -1f;

                for (int i = 0; i < particleSystems.Length; i++)
                {
                    var ps = particleSystems[i];
                    if (ps == null)
                        continue;

                    var main = ps.main;
                    if (main.loop)
                        continue;

                    float delay = GetMax(main.startDelay);
                    float startLifetime = GetMax(main.startLifetime);
                    float candidate = delay + main.duration + startLifetime;

                    if (candidate > maxLifetime)
                        maxLifetime = candidate;
                }

                return maxLifetime;
            }

            private static float GetMax(ParticleSystem.MinMaxCurve curve)
            {
                switch (curve.mode)
                {
                    case ParticleSystemCurveMode.Constant:
                        return curve.constant;

                    case ParticleSystemCurveMode.TwoConstants:
                        return curve.constantMax;

                    case ParticleSystemCurveMode.Curve:
                    case ParticleSystemCurveMode.TwoCurves:
                        return Mathf.Max(0f, curve.curveMultiplier);

                    default:
                        return 0f;
                }
            }
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
        [Header("VFX")]
        [SerializeField] private List<VNVfxDefinition> vfx = new();
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
        public bool TryGetVfx(string id, out VNVfxDefinition result)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                for (int i = 0; i < vfx.Count; i++)
                {
                    var item = vfx[i];
                    if (item != null && string.Equals(item.id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        result = item;
                        return true;
                    }
                }
            }

            result = null;
            return false;
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