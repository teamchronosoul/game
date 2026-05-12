using UnityEngine;
using UnityEngine.EventSystems;

namespace _GameAssets.Scripts.Gameplay.UI
{
    /// <summary>
    /// Повесь на Image/прозрачную tap-area поверх наставника, чтобы тап по нему показывал следующую intro-фразу.
    /// Важно: на объекте должен быть UI Graphic с Raycast Target = true, иначе UI-событие не придет.
    /// </summary>
    public class VNMainMenuMentorTapAreaUGUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private VNMainMenuUGUI mainMenu;

        private void Awake()
        {
            if (mainMenu == null)
                mainMenu = GetComponentInParent<VNMainMenuUGUI>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (mainMenu == null)
                mainMenu = GetComponentInParent<VNMainMenuUGUI>();

            if (mainMenu != null)
                mainMenu.OnMentorTapped();
        }
    }
}
