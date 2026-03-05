using UnityEngine;
using UnityEngine.EventSystems;

namespace VN.UI
{
    public class VNInputRouterUGUI : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private VN.VNRunner runner;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (runner == null) return;
            runner.Tap(eventData.position);
        }
    }
}