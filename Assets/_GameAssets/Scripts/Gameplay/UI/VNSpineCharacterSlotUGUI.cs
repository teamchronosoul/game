using System;
using System.Collections;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace VN.UI
{
    // Optional UI slot for animated Spine characters.
    // This version intentionally keeps positioning simple and predictable:
    // it aligns the Spine RectTransform to the real Image buffer rect instead of trying
    // to calculate Spine mesh bounds. This avoids center jumps caused by oversized UI roots.
    public class VNSpineCharacterSlotUGUI : MonoBehaviour
    {
        [Header("Spine")]
        [SerializeField] private SkeletonGraphic skeletonGraphic;

        [Header("Visibility")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool deactivateWhenHidden = true;

        [Tooltip("If true, character show/hide never uses alpha fade. Keep this enabled for PMA/bleed Spine exports to avoid white edges while disappearing.")]
        [SerializeField] private bool forceInstantVisibility = true;

        [Tooltip("If true, the old rendered frame is cleared before applying another skeleton/skin. This removes white/default one-frame flashes during switches.")]
        [SerializeField] private bool clearRendererBeforeShow = true;

        [Header("Skin / Emotion")]
        [Tooltip("Used if the requested animation is missing. Artist exported technical static animations, so static is a safe fallback.")]
        [SerializeField] private string fallbackAnimationName = "static";

        [Tooltip("If true, Emotion Slot Name is first checked as a Spine skin name. Use this for exports where emotions are skins: man_smile, girl_happy, lumis_sad, etc.")]
        [SerializeField] private bool useEmotionNameAsSkinFirst = true;

        [Tooltip("Fallback only. If the requested emotion is not a Skin, it will be treated as a slot name and the first attachment in that slot will be enabled.")]
        [SerializeField] private bool autoUseFirstAttachmentInEmotionSlot = true;

        [SerializeField] private bool warnAboutMissingSkins = true;
        [SerializeField] private bool warnAboutMissingAnimations = true;
        [SerializeField] private bool warnAboutMissingEmotionSlots = true;

        [Header("Alignment")]
        [Tooltip("Optional local offset after the Spine RectTransform pivot is placed into the center of Image_A/Image_B. Use only for tiny manual correction.")]
        [SerializeField] private Vector2 alignmentOffset = Vector2.zero;

        [Tooltip("Usually keep enabled. Spine keeps its own anchors/pivot and only its position changes to match the transparent sprite proxy Image.")]
        [SerializeField] private bool preserveOwnAnchorsAndPivot = true;

        private Coroutine _fadeRoutine;
        private RectTransform _rectTransform;
        private SkeletonDataAsset _currentSkeletonDataAsset;
        private string _currentBaseSkinName;
        private string _currentEmotionName;
        private string _currentAttachmentName;
        private string _currentAnimationName;
        private bool _currentLoop;

        private void Reset()
        {
            skeletonGraphic = GetComponentInChildren<SkeletonGraphic>(true);
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            StopFadeRoutine();
        }

        public void Show(
            SkeletonDataAsset skeletonDataAsset,
            string baseSkinName,
            string emotionSlotName,
            string animationName,
            bool loop,
            System.Collections.Generic.IReadOnlyList<string> emotionSlotsToClearOverride,
            float fadeSeconds,
            bool allowActivate = true)
        {
            ShowInternal(
                skeletonDataAsset,
                baseSkinName,
                emotionSlotName,
                animationName,
                loop,
                emotionSlotsToClearOverride,
                fadeSeconds,
                allowActivate,
                revealAfterSetup: true,
                hideBeforeSetup: false);
        }

        // Used when the view needs to switch to another, not-yet-prepared Spine character.
        // The skeleton/skin/animation are applied while the renderer is fully invisible,
        // then VNUIViewUGUI aligns the character to Image_A/Image_B, and only after that
        // RevealAfterAlignment() makes it visible. This removes the visible "standing up" / center jump.
        public void ShowHiddenForAlignment(
            SkeletonDataAsset skeletonDataAsset,
            string baseSkinName,
            string emotionSlotName,
            string animationName,
            bool loop,
            System.Collections.Generic.IReadOnlyList<string> emotionSlotsToClearOverride,
            bool allowActivate = true)
        {
            ShowInternal(
                skeletonDataAsset,
                baseSkinName,
                emotionSlotName,
                animationName,
                loop,
                emotionSlotsToClearOverride,
                fadeSeconds: 0f,
                allowActivate: allowActivate,
                revealAfterSetup: false,
                hideBeforeSetup: true);
        }

        public void RevealAfterAlignment(float fadeSeconds)
        {
            ResolveReferences();

            if (skeletonGraphic != null)
            {
                skeletonGraphic.enabled = true;
                skeletonGraphic.SetVerticesDirty();
                Canvas.ForceUpdateCanvases();
                skeletonGraphic.canvasRenderer.SetAlpha(1f);
            }

            if (canvasGroup == null)
                return;

            if (forceInstantVisibility || fadeSeconds <= 0f || !gameObject.activeInHierarchy)
            {
                StopFadeRoutine();
                canvasGroup.alpha = 1f;
            }
            else
            {
                FadeTo(1f, fadeSeconds);
            }
        }

        public void ForceVisibleInstant()
        {
            ResolveReferences();

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            StopFadeRoutine();

            if (skeletonGraphic != null)
            {
                skeletonGraphic.enabled = true;
                skeletonGraphic.SetVerticesDirty();
                skeletonGraphic.canvasRenderer.SetAlpha(1f);
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        private void ShowInternal(
            SkeletonDataAsset skeletonDataAsset,
            string baseSkinName,
            string emotionSlotName,
            string animationName,
            bool loop,
            System.Collections.Generic.IReadOnlyList<string> emotionSlotsToClearOverride,
            float fadeSeconds,
            bool allowActivate,
            bool revealAfterSetup,
            bool hideBeforeSetup)
        {
            if (skeletonDataAsset == null)
            {
                SetInstantHidden();
                return;
            }

            ResolveReferences();

            if (skeletonGraphic == null)
            {
                Debug.LogWarning("[VNSpineCharacterSlotUGUI] SkeletonGraphic is not assigned.", this);
                return;
            }

            if (allowActivate && !gameObject.activeSelf)
                gameObject.SetActive(true);

            // Critical for a NEW / not-yet-prepared character: hide before SkeletonGraphic receives
            // a new SkeletonDataAsset/skin. For the SAME already-visible character we deliberately
            // do NOT hide here, otherwise consecutive lines or emotion changes can leave one blank frame.
            if (hideBeforeSetup)
                MakeInvisibleWithoutDeactivating(clearRendererBeforeShow);

            SplitSlotAndAttachment(emotionSlotName, out var parsedEmotionName, out var parsedAttachmentName);

            var setupChanged = _currentSkeletonDataAsset != skeletonDataAsset
                               || !string.Equals(_currentBaseSkinName, Normalize(baseSkinName), StringComparison.Ordinal)
                               || !string.Equals(_currentEmotionName, parsedEmotionName, StringComparison.Ordinal)
                               || !string.Equals(_currentAttachmentName, parsedAttachmentName, StringComparison.Ordinal);

            if (_currentSkeletonDataAsset != skeletonDataAsset)
            {
                skeletonGraphic.skeletonDataAsset = skeletonDataAsset;
                skeletonGraphic.Initialize(true);
                _currentSkeletonDataAsset = skeletonDataAsset;
                _currentAnimationName = null;
            }
            else if (skeletonGraphic.Skeleton == null)
            {
                skeletonGraphic.Initialize(false);
            }

            if (skeletonGraphic.Skeleton == null)
            {
                Debug.LogWarning("[VNSpineCharacterSlotUGUI] SkeletonGraphic failed to initialize.", this);
                SetInstantHidden();
                return;
            }

            if (setupChanged)
            {
                ApplyCharacterSetup(baseSkinName, parsedEmotionName, parsedAttachmentName);
                _currentBaseSkinName = Normalize(baseSkinName) ?? string.Empty;
                _currentEmotionName = parsedEmotionName ?? string.Empty;
                _currentAttachmentName = parsedAttachmentName ?? string.Empty;
            }

            PlayAnimation(animationName, loop);

            skeletonGraphic.enabled = true;
            skeletonGraphic.SetVerticesDirty();

            if (!revealAfterSetup)
            {
                // Keep the prepared character completely invisible until alignment is done.
                skeletonGraphic.canvasRenderer.SetAlpha(0f);
                if (canvasGroup != null)
                    canvasGroup.alpha = 0f;
                return;
            }

            skeletonGraphic.canvasRenderer.SetAlpha(1f);

            if (canvasGroup != null)
            {
                if (forceInstantVisibility || fadeSeconds <= 0f || !gameObject.activeInHierarchy)
                {
                    StopFadeRoutine();
                    canvasGroup.alpha = allowActivate ? 1f : 0f;
                }
                else
                {
                    FadeTo(1f, fadeSeconds);
                }
            }
        }

        public void Hide(float fadeSeconds)
        {
            ResolveReferences();

            if (forceInstantVisibility || fadeSeconds <= 0f || !gameObject.activeInHierarchy)
            {
                SetInstantHidden();
                return;
            }

            FadeTo(0f, fadeSeconds, SetInstantHidden);
        }

        public void SetInstantHidden()
        {
            ResolveReferences();
            StopFadeRoutine();

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (skeletonGraphic != null)
            {
                skeletonGraphic.canvasRenderer.SetAlpha(0f);
                skeletonGraphic.canvasRenderer.Clear();
                skeletonGraphic.enabled = false;
            }

            if (deactivateWhenHidden)
                gameObject.SetActive(false);
        }

        private void ResolveReferences()
        {
            if (skeletonGraphic == null)
                skeletonGraphic = GetComponentInChildren<SkeletonGraphic>(true);

            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void ClearRenderedFrame()
        {
            MakeInvisibleWithoutDeactivating(clearMesh: true);
        }

        private void MakeInvisibleWithoutDeactivating(bool clearMesh)
        {
            StopFadeRoutine();

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (skeletonGraphic != null)
            {
                skeletonGraphic.canvasRenderer.SetAlpha(0f);
                if (clearMesh)
                    skeletonGraphic.canvasRenderer.Clear();
            }
        }

        public void AlignCenterToImageSlot(RectTransform referenceRect, bool copySize, bool copyLayoutWhenSameParent)
        {
            AlignToImageSlot(referenceRect, copySize, copyLayoutWhenSameParent);
        }

        public void AlignToImageSlot(RectTransform referenceRect, bool copySize, bool copyLayoutWhenSameParent)
        {
            ResolveReferences();

            if (_rectTransform == null || referenceRect == null)
                return;

            if (copyLayoutWhenSameParent && _rectTransform.parent == referenceRect.parent)
            {
                _rectTransform.anchorMin = referenceRect.anchorMin;
                _rectTransform.anchorMax = referenceRect.anchorMax;
                if (!preserveOwnAnchorsAndPivot)
                    _rectTransform.pivot = referenceRect.pivot;
            }
            else if (!preserveOwnAnchorsAndPivot)
            {
                _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            if (copySize)
                _rectTransform.sizeDelta = referenceRect.rect.size;

            // Deliberately simple and stable: the transparent sprite proxy already has
            // the correct sprite and Native Size. Put the Spine RectTransform pivot into
            // the center of that Image. No Spine bounds, no oversized root-rect alignment.
            var targetWorld = referenceRect.TransformPoint(referenceRect.rect.center);
            var currentPivotWorld = _rectTransform.TransformPoint(Vector3.zero);
            var worldDelta = targetWorld - currentPivotWorld;
            _rectTransform.position += worldDelta;

            if (alignmentOffset != Vector2.zero)
                _rectTransform.anchoredPosition += alignmentOffset;
        }

        public void RefreshVisualAlignment(RectTransform referenceRect)
        {
            AlignToImageSlot(referenceRect, false, false);
        }

        private void ApplyCharacterSetup(string baseSkinName, string emotionNameOrSlotName, string attachmentNameOverride)
        {
            var skeleton = skeletonGraphic.Skeleton;
            var data = skeleton?.Data;
            if (skeleton == null || data == null)
                return;

            baseSkinName = Normalize(baseSkinName);
            emotionNameOrSlotName = Normalize(emotionNameOrSlotName);
            attachmentNameOverride = Normalize(attachmentNameOverride);

            var baseSkin = FindSkin(data, baseSkinName);
            if (!string.IsNullOrEmpty(baseSkinName) && baseSkin == null && warnAboutMissingSkins)
                Debug.LogWarning($"[VNSpineCharacterSlotUGUI] Base skin '{baseSkinName}' not found in '{skeletonGraphic.skeletonDataAsset.name}'.", this);

            var emotionSkin = useEmotionNameAsSkinFirst && string.IsNullOrEmpty(attachmentNameOverride)
                ? FindSkin(data, emotionNameOrSlotName)
                : null;

            if (emotionSkin != null)
            {
                ApplyCombinedSkin(baseSkin, emotionSkin, baseSkinName, emotionNameOrSlotName);
                return;
            }

            skeleton.SetSkin(baseSkin);
            skeleton.SetSlotsToSetupPose();
            skeletonGraphic.AnimationState?.Apply(skeleton);
            ApplyEmotionSlot(emotionNameOrSlotName, attachmentNameOverride);
            skeletonGraphic.SetVerticesDirty();
        }

        private void ApplyCombinedSkin(Skin baseSkin, Skin emotionSkin, string baseSkinName, string emotionSkinName)
        {
            var skeleton = skeletonGraphic.Skeleton;
            if (skeleton == null)
                return;

            Skin skinToApply;
            if (baseSkin != null && emotionSkin != baseSkin)
            {
                var combinedSkin = new Skin($"vn_{baseSkinName}_{emotionSkinName}");
                combinedSkin.AddSkin(baseSkin);
                combinedSkin.AddSkin(emotionSkin);
                skinToApply = combinedSkin;
            }
            else
            {
                skinToApply = emotionSkin ?? baseSkin;
            }

            skeleton.SetSkin(skinToApply);
            skeleton.SetSlotsToSetupPose();
            skeletonGraphic.AnimationState?.Apply(skeleton);
            skeletonGraphic.SetVerticesDirty();
        }

        private void ApplyEmotionSlot(string emotionSlotName, string attachmentNameOverride)
        {
            var skeleton = skeletonGraphic.Skeleton;
            if (skeleton == null)
                return;

            emotionSlotName = Normalize(emotionSlotName);
            attachmentNameOverride = Normalize(attachmentNameOverride);

            if (string.IsNullOrEmpty(emotionSlotName))
                return;

            var targetSlot = skeleton.FindSlot(emotionSlotName);
            if (targetSlot == null)
            {
                if (warnAboutMissingEmotionSlots)
                    Debug.LogWarning($"[VNSpineCharacterSlotUGUI] Emotion slot '{emotionSlotName}' not found in '{skeletonGraphic.skeletonDataAsset.name}'.", this);
                return;
            }

            var targetAttachment = ResolveAttachment(targetSlot, attachmentNameOverride);
            if (targetAttachment == null)
            {
                if (warnAboutMissingEmotionSlots)
                    Debug.LogWarning($"[VNSpineCharacterSlotUGUI] Attachment for emotion slot '{emotionSlotName}' not found in '{skeletonGraphic.skeletonDataAsset.name}'.", this);
                return;
            }

            targetSlot.Attachment = targetAttachment;
        }

        private Attachment ResolveAttachment(Slot slot, string attachmentNameOverride)
        {
            if (slot == null || skeletonGraphic == null || skeletonGraphic.Skeleton == null)
                return null;

            if (!string.IsNullOrEmpty(attachmentNameOverride))
            {
                var byOverride = skeletonGraphic.Skeleton.GetAttachment(slot.Data.Index, attachmentNameOverride);
                if (byOverride != null)
                    return byOverride;
            }

            var setupAttachmentName = Normalize(slot.Data.AttachmentName);
            if (!string.IsNullOrEmpty(setupAttachmentName))
            {
                var bySetupName = skeletonGraphic.Skeleton.GetAttachment(slot.Data.Index, setupAttachmentName);
                if (bySetupName != null)
                    return bySetupName;
            }

            var bySlotName = skeletonGraphic.Skeleton.GetAttachment(slot.Data.Index, slot.Data.Name);
            if (bySlotName != null)
                return bySlotName;

            return autoUseFirstAttachmentInEmotionSlot ? FindFirstAttachmentInSlot(slot.Data.Index) : null;
        }

        private Attachment FindFirstAttachmentInSlot(int slotIndex)
        {
            var skeleton = skeletonGraphic != null ? skeletonGraphic.Skeleton : null;
            var data = skeleton?.Data;
            if (data == null)
                return null;

            Attachment attachment;
            if (TryFindFirstAttachmentInSkin(skeleton.Skin, slotIndex, out attachment))
                return attachment;

            if (TryFindFirstAttachmentInSkin(data.DefaultSkin, slotIndex, out attachment))
                return attachment;

            var skins = data.Skins;
            if (skins != null)
            {
                for (var i = 0; i < skins.Count; i++)
                {
                    if (TryFindFirstAttachmentInSkin(skins.Items[i], slotIndex, out attachment))
                        return attachment;
                }
            }

            return null;
        }

        private static bool TryFindFirstAttachmentInSkin(Skin skin, int slotIndex, out Attachment attachment)
        {
            attachment = null;
            if (skin == null)
                return false;

            var entries = new System.Collections.Generic.List<Skin.SkinEntry>();
            skin.GetAttachments(slotIndex, entries);
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Attachment == null)
                    continue;

                attachment = entries[i].Attachment;
                return true;
            }

            return false;
        }

        private void PlayAnimation(string animationName, bool loop)
        {
            var skeleton = skeletonGraphic.Skeleton;
            var data = skeleton?.Data;
            var state = skeletonGraphic.AnimationState;
            if (data == null || state == null)
                return;

            var resolvedName = ResolveAnimationName(data, animationName, fallbackAnimationName);
            if (string.IsNullOrEmpty(resolvedName))
                return;

            if (string.Equals(_currentAnimationName, resolvedName, StringComparison.Ordinal) && _currentLoop == loop)
                return;

            state.SetAnimation(0, resolvedName, loop);
            state.Apply(skeleton);
            _currentAnimationName = resolvedName;
            _currentLoop = loop;
        }

        private string ResolveAnimationName(SkeletonData data, string animationName, string fallbackName)
        {
            animationName = Normalize(animationName);
            fallbackName = Normalize(fallbackName);

            if (!string.IsNullOrEmpty(animationName) && data.FindAnimation(animationName) != null)
                return animationName;

            if (!string.IsNullOrEmpty(animationName) && warnAboutMissingAnimations)
                Debug.LogWarning($"[VNSpineCharacterSlotUGUI] Animation '{animationName}' not found in '{skeletonGraphic.skeletonDataAsset.name}'.", this);

            if (!string.IsNullOrEmpty(fallbackName) && data.FindAnimation(fallbackName) != null)
                return fallbackName;

            if (data.Animations != null && data.Animations.Count > 0)
                return data.Animations.Items[0].Name;

            return null;
        }

        private static Skin FindSkin(SkeletonData data, string skinName)
        {
            skinName = Normalize(skinName);
            return string.IsNullOrEmpty(skinName) ? null : data.FindSkin(skinName);
        }

        private static void SplitSlotAndAttachment(string value, out string slotName, out string attachmentName)
        {
            slotName = Normalize(value);
            attachmentName = null;

            if (string.IsNullOrEmpty(slotName))
                return;

            var separatorIndex = slotName.IndexOf('|');
            if (separatorIndex < 0)
                separatorIndex = slotName.IndexOf(':');

            if (separatorIndex < 0)
                return;

            attachmentName = Normalize(slotName.Substring(separatorIndex + 1));
            slotName = Normalize(slotName.Substring(0, separatorIndex));
        }

        private void FadeTo(float targetAlpha, float seconds, Action onComplete = null)
        {
            if (canvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopFadeRoutine();

            seconds = Mathf.Max(0f, seconds);
            if (seconds <= 0f || !gameObject.activeInHierarchy)
            {
                canvasGroup.alpha = targetAlpha;
                onComplete?.Invoke();
                return;
            }

            _fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, seconds, onComplete));
        }

        private IEnumerator FadeRoutine(float targetAlpha, float seconds, Action onComplete)
        {
            var startAlpha = canvasGroup.alpha;
            var elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            _fadeRoutine = null;
            onComplete?.Invoke();
        }

        private void StopFadeRoutine()
        {
            if (_fadeRoutine == null)
                return;

            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
