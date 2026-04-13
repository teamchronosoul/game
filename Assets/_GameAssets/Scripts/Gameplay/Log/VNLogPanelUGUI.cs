using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VN.UI
{
    public class VNLogPanelUGUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private VNLogEntryItemUGUI entryPrefab;
        [SerializeField] private bool scrollToLatestOnShow = true;
        [SerializeField] private float extraHeightPerEntry = 300f;

        private readonly List<VNLogEntryItemUGUI> _spawned = new();
        private Coroutine _scrollRoutine;
        private float _baseContentHeight;

        public bool IsVisible => root != null ? root.activeSelf : gameObject.activeSelf;

        private void Awake()
        {
            if (contentRoot != null)
                _baseContentHeight = contentRoot.rect.height;
        }

        public void Show(IReadOnlyList<VN.VNState.LogEntry> entries)
        {
            if (root != null)
                root.SetActive(true);
            else
                gameObject.SetActive(true);

            Rebuild(entries);

            if (scrollToLatestOnShow)
                ScrollToBottomDeferred();
        }

        public void HideImmediate()
        {
            if (_scrollRoutine != null)
            {
                StopCoroutine(_scrollRoutine);
                _scrollRoutine = null;
            }

            if (root != null)
                root.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void Rebuild(IReadOnlyList<VN.VNState.LogEntry> entries)
        {
            Clear();

            if (entries == null || contentRoot == null || entryPrefab == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var item = Instantiate(entryPrefab, contentRoot);
                item.Bind(entries[i]);
                _spawned.Add(item);
            }
            
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }
        
        private void Clear()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Destroy(_spawned[i].gameObject);
            }

            _spawned.Clear();

            if (contentRoot != null)
                contentRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _baseContentHeight);
        }

        private void ScrollToBottomDeferred()
        {
            if (_scrollRoutine != null)
                StopCoroutine(_scrollRoutine);

            _scrollRoutine = StartCoroutine(CoScrollToBottom());
        }

        private IEnumerator CoScrollToBottom()
        {
            yield return null;
            yield return null;

            Canvas.ForceUpdateCanvases();

            if (contentRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;

            _scrollRoutine = null;
        }
    }
}