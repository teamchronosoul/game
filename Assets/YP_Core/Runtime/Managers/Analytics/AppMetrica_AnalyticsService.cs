using System;
using System.Collections.Generic;
using UnityEngine;
using YP;

public class AppMetrica_AnalyticsService : AnalyticsService
{
	[SerializeField] private List<string> _sendBufferEventKeys;
	[SerializeField] private bool bLogEventsOnDevice;

	public override bool supported => true;


	public override void Initialize() => InitCompleted();

	public override void SendEvent(string key_analytics, Dictionary<string, object> parameters)
	{
		bool sendBuffer = _sendBufferEventKeys.Contains(key_analytics);
		string firstLaunchDateString = Saves.String[Key_Save.first_time].Value;
		if (DateTime.TryParse(firstLaunchDateString, out DateTime firstLaunchDate))
		{
			int daysSinceReg = (DateTime.Now - firstLaunchDate).Days;
			parameters.Add("days since reg", daysSinceReg);
		}
		SendCustomEvent(key_analytics, parameters, sendBuffer);
	}

	public void SendCustomEvent(string eventName, Dictionary<string, object> parameters, bool bSendEventsBuffer = false)
	{
		if (parameters == null)
		{
			parameters = new Dictionary<string, object>();
		}

		bool debugLog = bLogEventsOnDevice;

#if !UNITY_EDITOR

		if (AppMetrica.Instance != null)
		{
			AppMetrica.Instance.ReportEvent(eventName, parameters);

			if (bSendEventsBuffer)
			{
				AppMetrica.Instance.SendEventsBuffer();
			}
		}
		else Debug.LogError("YandexMetrica instance is null");
#endif
		if (debugLog)
		{
			string eventParams = "";
			foreach (string key in parameters.Keys)
			{
				eventParams = eventParams + "\n" + key + ": " + parameters[key].ToString();
			}

			Debug.Log($"Event: {eventName} and params: {eventParams}");
		}
	}
}