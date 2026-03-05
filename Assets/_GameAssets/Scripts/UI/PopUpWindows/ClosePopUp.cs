using UnityEngine;
using YP;

namespace _GameAssets.Scripts.UI
{
    public class ClosePopUp : ButtonHandler
    {
        [SerializeField] PopUpWindow popUpWindowToClose;
        protected override void OnClick()
        {
            popUpWindowToClose.ResetAll();
        }
    }
}