using UnityEngine;
using YP;

namespace _GameAssets.Scripts.UI
{
	public class OpenWindowButton : ButtonHandler
	{
		[SerializeField] private PopUpWindow windowToOpen;

		protected override void OnClick()
		{
			windowToOpen.Appear();
		}
	}
}