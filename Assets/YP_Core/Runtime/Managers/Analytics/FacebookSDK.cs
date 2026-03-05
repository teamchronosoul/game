using UnityEngine;

public class FacebookSDK : MonoBehaviour
{
	private void Awake()
	{
		//if (!FB.IsInitialized) FB.Init(InitCallback);
		//else FB.ActivateApp();
	}

	private void InitCallback()
	{
	//	if (FB.IsInitialized) FB.ActivateApp();
	//	else Debug.Log("Failed to Initialize the Facebook SDK");
	}
}