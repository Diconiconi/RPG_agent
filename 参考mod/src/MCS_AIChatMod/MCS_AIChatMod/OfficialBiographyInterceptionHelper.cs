using System;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

internal static class OfficialBiographyInterceptionHelper
{
	internal static bool TryCapture(BiographyEventKind eventKind, int npcId, string sourceMethodKey, JObject payload, string originalOfficialTime = null)
	{
		return AIQuestManager.Instance?.TryInterceptOfficialBiographyEvent(eventKind, npcId, sourceMethodKey, payload, originalOfficialTime) ?? false;
	}

	internal static string ResolveOfficialTime(string explicitTime = null)
	{
		if (!string.IsNullOrWhiteSpace(explicitTime))
		{
			return explicitTime.Trim();
		}
		string settlementTime = NpcJieSuanManager.inst?.JieSuanTime;
		if (!string.IsNullOrWhiteSpace(settlementTime))
		{
			return settlementTime;
		}
		string currentTime = Tools.instance?.getPlayer()?.worldTimeMag?.nowTime;
		if (!string.IsNullOrWhiteSpace(currentTime))
		{
			return currentTime;
		}
		return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
	}
}
