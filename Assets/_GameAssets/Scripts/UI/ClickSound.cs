using YP;

namespace _GameAssets.Scripts.UI
{
	public class ClickSound : ButtonHandler
	{
		protected override void OnClick()
		{
			Sound.PlaySFX("click");
		}
	}
}