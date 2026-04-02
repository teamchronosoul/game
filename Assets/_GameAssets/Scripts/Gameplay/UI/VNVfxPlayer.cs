using System;
using System.Collections.Generic;
using System.Reflection;
using Coffee.UIExtensions;
using UnityEngine;

namespace VN
{
    public class VNVfxPlayer : MonoBehaviour
    {
        [Serializable]
        public struct AnchorBinding
        {
            public string id;
            public Transform anchor;
        }

        [Header("Roots")]
        [SerializeField] private Transform defaultAnchor;
        [SerializeField] private Transform instancesRoot;

        [Header("Named anchors")]
        [SerializeField] private List<AnchorBinding> anchors = new();

        private readonly Dictionary<string, Transform> _anchorMap =
            new(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            RebuildCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildCache();
        }
#endif

        public VNVfxRuntimeHandle Play(
            VNAssetDatabase.VNVfxDefinition definition,
            string anchorId,
            Vector3 localOffset,
            float scale = 1f,
            float lifetimeOverride = -1f)
        {
            if (definition == null || definition.prefab == null)
            {
                Debug.LogWarning("[VN] VFX definition or prefab is missing.");
                return null;
            }

            Transform anchor = ResolveAnchor(anchorId);
            Transform parent = anchor != null
                ? anchor
                : (instancesRoot != null ? instancesRoot : transform);

            GameObject instance = Instantiate(definition.prefab, parent);
            instance.transform.localPosition = localOffset;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale *= Mathf.Max(0.01f, scale);
            
            instancesRoot.GetComponent<UIParticle>().RefreshParticles();
            RestartEffects(instance);

            float resolvedLifetime = lifetimeOverride > 0f
                ? lifetimeOverride
                : definition.ResolveLifetime();

            var handle = instance.GetComponent<VNVfxRuntimeHandle>();
            if (handle == null)
                handle = instance.AddComponent<VNVfxRuntimeHandle>();

            handle.Initialize(resolvedLifetime, true);
            return handle;
        }

        private Transform ResolveAnchor(string anchorId)
        {
            if (!string.IsNullOrWhiteSpace(anchorId) &&
                _anchorMap.TryGetValue(anchorId, out var anchor) &&
                anchor != null)
            {
                return anchor;
            }

            if (defaultAnchor != null)
                return defaultAnchor;

            return instancesRoot != null ? instancesRoot : transform;
        }

        private void RebuildCache()
        {
            _anchorMap.Clear();

            for (int i = 0; i < anchors.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(anchors[i].id) || anchors[i].anchor == null)
                    continue;

                _anchorMap[anchors[i].id] = anchors[i].anchor;
            }
        }

        private static void RestartEffects(GameObject root)
        {
            if (root == null)
                return;

            var particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                particleSystems[i].Clear(true);
                particleSystems[i].Play(true);
            }

            // Мягкая поддержка VFX Graph без прямой зависимости от пакета
            var components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                    continue;

                var type = component.GetType();
                if (type.Name != "VisualEffect")
                    continue;

                MethodInfo reinit = type.GetMethod("Reinit", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo play = type.GetMethod("Play", BindingFlags.Instance | BindingFlags.Public);

                reinit?.Invoke(component, null);
                play?.Invoke(component, null);
            }
        }
    }
}