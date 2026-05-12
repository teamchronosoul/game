using System;
using _GameAssets.Scripts.Gameplay.UI;
using CandyCoded.HapticFeedback;
using UnityEngine;

namespace VN
{
    public enum VNHapticFeedbackType
    {
        Light = 0,
        Heavy = 1
    }

    [Serializable]
    public class VNVibrationCommand : VNCommand
    {
        [Header("Haptic Feedback")]
        [Tooltip("Тип haptic feedback из импортированного package CandyCoded.HapticFeedback.")]
        public VNHapticFeedbackType feedbackType = VNHapticFeedbackType.Heavy;

        [Tooltip("Количество импульсов. Обычно 1. Для серии ударов можно поставить больше.")]
        [Min(1)] public int pulseCount = 1;

        [Tooltip("Пауза между импульсами, если Pulse Count больше 1.")]
        [Min(0f)] public float pulseIntervalSeconds = 0.08f;
    }

    public static class VNVibration
    {
        public static void Play(VNHapticFeedbackType feedbackType = VNHapticFeedbackType.Heavy)
        {
            if (!VNSettingsWindowUGUI.VibrationEnabled)
                return;

#if UNITY_IOS || UNITY_ANDROID
            switch (feedbackType)
            {
                case VNHapticFeedbackType.Light:
                    HapticFeedback.LightFeedback();
                    break;

                case VNHapticFeedbackType.Heavy:
                default:
                    HapticFeedback.HeavyFeedback();
                    break;
            }
#endif
        }
    }
}
