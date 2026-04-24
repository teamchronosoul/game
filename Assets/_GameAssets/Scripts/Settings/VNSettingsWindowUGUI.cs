using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YP;

namespace _GameAssets.Scripts.Gameplay.UI
{
    public class VNSettingsWindowUGUI : MonoBehaviour
    {
        [System.Serializable]
        private class ToggleView
        {
            [Header("Button")]
            public Button button;

            [Header("Dot")]
            public RectTransform dot;
            public Vector2 enabledPosition = new Vector2(45f, 0f);
            public Vector2 disabledPosition = new Vector2(-45f, 0f);

            [Header("State Text Objects")]
            [Tooltip("Объект с текстом ВКЛ")]
            public GameObject enabledTextObject;

            [Tooltip("Объект с текстом ВЫКЛ")]
            public GameObject disabledTextObject;

            [Header("Animation")]
            [Min(0f)] public float moveDuration = 0.2f;
            public Ease moveEase = Ease.OutBack;

            private Tween _moveTween;

            public void Apply(bool enabled, bool animated)
            {
                if (enabledTextObject != null)
                    enabledTextObject.SetActive(enabled);

                if (disabledTextObject != null)
                    disabledTextObject.SetActive(!enabled);

                if (dot == null)
                    return;

                Vector2 target = enabled ? enabledPosition : disabledPosition;

                _moveTween?.Kill();

                if (!animated || moveDuration <= 0f)
                {
                    dot.anchoredPosition = target;
                    return;
                }

                _moveTween = dot
                    .DOAnchorPos(target, moveDuration)
                    .SetEase(moveEase)
                    .SetUpdate(true);
            }

            public void Kill()
            {
                _moveTween?.Kill();
                _moveTween = null;
            }
        }

        [Header("Root")]
        [SerializeField] private GameObject root;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [Header("Music")]
        [SerializeField] private ToggleView musicToggle;

        [Header("Sound")]
        [SerializeField] private ToggleView soundToggle;

        [Header("Vibration")]
        [SerializeField] private ToggleView vibrationToggle;

        [Header("Behaviour")]
        [SerializeField] private bool hideOnStart = true;
        [SerializeField] private bool playClickSound = true;
        [SerializeField] private string clickSfxKey = "click";

        private bool _musicEnabled;
        private bool _soundEnabled;
        private bool _vibrationEnabled;

        public static bool VibrationEnabled
        {
            get => PlayerPrefs.GetInt(Key_Save.vibration, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(Key_Save.vibration, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            LoadSettings();
            ApplyAll(animated: false);

            if (hideOnStart)
                HideImmediate();
        }

        private void OnEnable()
        {
            WireButtons();
            LoadSettings();
            ApplyAll(animated: false);
        }

        private void OnDisable()
        {
            UnwireButtons();

            musicToggle?.Kill();
            soundToggle?.Kill();
            vibrationToggle?.Kill();
        }

        private void WireButtons()
        {
            if (musicToggle != null && musicToggle.button != null)
            {
                musicToggle.button.onClick.RemoveListener(ToggleMusic);
                musicToggle.button.onClick.AddListener(ToggleMusic);
            }

            if (soundToggle != null && soundToggle.button != null)
            {
                soundToggle.button.onClick.RemoveListener(ToggleSound);
                soundToggle.button.onClick.AddListener(ToggleSound);
            }

            if (vibrationToggle != null && vibrationToggle.button != null)
            {
                vibrationToggle.button.onClick.RemoveListener(ToggleVibration);
                vibrationToggle.button.onClick.AddListener(ToggleVibration);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Hide);
                closeButton.onClick.AddListener(Hide);
            }
        }

        private void UnwireButtons()
        {
            if (musicToggle != null && musicToggle.button != null)
                musicToggle.button.onClick.RemoveListener(ToggleMusic);

            if (soundToggle != null && soundToggle.button != null)
                soundToggle.button.onClick.RemoveListener(ToggleSound);

            if (vibrationToggle != null && vibrationToggle.button != null)
                vibrationToggle.button.onClick.RemoveListener(ToggleVibration);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(Hide);
        }

        public void Show()
        {
            if (root != null)
                root.SetActive(true);
            else
                gameObject.SetActive(true);

            LoadSettings();
            ApplyAll(animated: false);
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        public void HideImmediate()
        {
            if (root != null)
                root.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void ToggleMusic()
        {
            _musicEnabled = !_musicEnabled;
            SaveBool(Key_Save.music, _musicEnabled);

            Sound.EnableMusic(_musicEnabled);

            musicToggle?.Apply(_musicEnabled, animated: true);
            PlayClick();
        }

        private void ToggleSound()
        {
            _soundEnabled = !_soundEnabled;
            SaveBool(Key_Save.sound, _soundEnabled);

            Sound.EnableSound(_soundEnabled);

            soundToggle?.Apply(_soundEnabled, animated: true);
            PlayClick();
        }

        private void ToggleVibration()
        {
            _vibrationEnabled = !_vibrationEnabled;
            SaveBool(Key_Save.vibration, _vibrationEnabled);

            vibrationToggle?.Apply(_vibrationEnabled, animated: true);
            PlayClick();
        }

        private void LoadSettings()
        {
            _musicEnabled = LoadBool(Key_Save.music, true);
            _soundEnabled = LoadBool(Key_Save.sound, true);
            _vibrationEnabled = LoadBool(Key_Save.vibration, true);
        }

        private void ApplyAll(bool animated)
        {
            Sound.EnableMusic(_musicEnabled);
            Sound.EnableSound(_soundEnabled);

            musicToggle?.Apply(_musicEnabled, animated);
            soundToggle?.Apply(_soundEnabled, animated);
            vibrationToggle?.Apply(_vibrationEnabled, animated);
        }

        private void PlayClick()
        {
            if (!playClickSound)
                return;

            if (!_soundEnabled)
                return;

            if (!string.IsNullOrWhiteSpace(clickSfxKey))
                Sound.PlaySFX(clickSfxKey);
        }

        private static bool LoadBool(string key, bool defaultValue)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        }

        private static void SaveBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}