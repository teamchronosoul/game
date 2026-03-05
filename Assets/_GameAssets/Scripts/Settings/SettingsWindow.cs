using _GameAssets.Scripts.UI;
using UnityEngine;

public class SettingsWindow : PopUpWindow
{
	[SerializeField] private Canvas startScreenCanvas;
	[SerializeField] private GameObject home_button;
	[SerializeField] GameObject windowLast;
		
	protected override void BeforeAppear()
	{
		home_button.SetActive(startScreenCanvas.enabled == false);
	}

	public override void ResetAll()
	{
		base.ResetAll();
		windowLast.SetActive(false);
	}
}