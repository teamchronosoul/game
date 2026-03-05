using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace _GameAssets.Scripts.UI
{
    public class PopUpWindow : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] protected Image blackout;
        [SerializeField] protected RectTransform window;
        [SerializeField] private Button[] buttons;
        [Header("Etc.")]
        [SerializeField] private string soundKey;
        [SerializeField] private GameObject effect;
        
        

        public Action OnWindowOpen;
        public Action OnWindowClose;

        public virtual void Initialize()
        {
            Close();
        }

        public void Close()
        {
            blackout.color = Color.clear;
            window.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }

        public virtual void Appear()
        {
            BeforeAppear();
            gameObject.SetActive(true);

            if (soundKey != "") Sound.PlaySFX(soundKey);

            blackout.DOFade(.85f, .6f).SetUpdate(true);
            window.DOScale(Vector3.one * 1.1f, .35f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    window.DOScale(Vector3.one, .25f)
                        .SetUpdate(true)
                        .OnComplete(AfterAppear);
                });
        }

        protected virtual void AfterAppear()
        {
            foreach (var button in buttons) button.interactable = true;
            OnWindowOpen?.Invoke();
        }

        protected virtual void BeforeAppear()
        {
            foreach (var button in buttons) button.interactable = false;
            Time.timeScale = 0;
            if (effect) effect.SetActive(true);
        }

        public virtual void ResetAll()
        {
            Close();
            Time.timeScale = 1;
            if (effect) effect.SetActive(false);
            OnWindowClose?.Invoke();
        }
    }
}