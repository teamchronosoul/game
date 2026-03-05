using System.Collections.Generic;
using _GameAssets.Scripts.Main;
using UnityEngine;

namespace _GameAssets.Scripts.UI
{
	public class WindowsInitializer : Controller
	{
		[SerializeField] List<PopUpWindow> popUpWindows = new();
		public override void Initialize()
		{
			for (var i = 0; i < transform.childCount; i++)
			{
				popUpWindows.Add(transform.GetChild(i).GetComponent<PopUpWindow>());
			}

			foreach (var popUpWindow in popUpWindows)
			{
				popUpWindow.gameObject.SetActive(true);
				popUpWindow.Initialize();
			}
		}
	}
}