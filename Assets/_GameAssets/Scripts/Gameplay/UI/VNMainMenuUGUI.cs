using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VN;

namespace _GameAssets.Scripts.Gameplay.UI
{
    public class VNMainMenuUGUI : MonoBehaviour
    {
        [Header("Database")]
        [SerializeField] private ArchetypeDatabase database;

        [Header("UI")]
        [SerializeField] private Image mentorImage;
        [SerializeField] private Image mentorIdImage;
        [SerializeField] private Image mirrorImage;

        private VNMbtiState mbti;
        private ArchetypeData archetype;

        private void OnEnable()
        {
            if (VNAutosave.TryLoad(out var state))
            {
                mbti = state.mbti;
                archetype = database.GetById(mbti.ArchetypeId);

                if (archetype != null)
                    ApplyMenu();
            }
        }

        private void ApplyMenu()
        {
            mentorImage.sprite = archetype.mentorSprite;
            mentorImage.SetNativeSize();
            mentorIdImage.sprite = archetype.mentorIdSprite;
            mirrorImage.sprite = archetype.mirrorSprite;
        }

    }
}