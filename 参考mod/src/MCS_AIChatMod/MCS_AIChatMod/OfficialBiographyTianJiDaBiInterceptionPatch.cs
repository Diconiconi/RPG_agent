using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using YSGame.TianJiDaBi;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(TianJiDaBiManager), "AddMatchPlayerEvent", new Type[]
{
	typeof(Match),
	typeof(bool)
})]
internal static class OfficialBiographyTianJiDaBiInterceptionPatch
{
	private static bool Prefix(Match match, bool isNowTime)
	{
		int playerCount = match.PlayerCount;
		for (int i = 0; i < playerCount; i++)
		{
			DaBiPlayer daBiPlayer = match.PlayerList[i];
			if (!daBiPlayer.IsWanJia)
			{
				string eventTime = (isNowTime ? PlayerEx.Player.worldTimeMag.nowTime : (match.MatchYear.ToString("D4") + "/12/31 0:00:00"));
				if (!OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.TianJiDaBi, daBiPlayer.ID, "TianJiDaBiManager.AddMatchPlayerEvent", new JObject
				{
					["npcId"] = daBiPlayer.ID,
					["rank"] = i + 1,
					["eventTime"] = eventTime
				}, eventTime))
				{
					NpcJieSuanManager.inst.npcNoteBook.NoteTianJiDaBi(daBiPlayer.ID, i + 1, eventTime);
				}
			}
		}
		return false;
	}
}
