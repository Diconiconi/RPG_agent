using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NpcJieSuanManager), "AddNpcEquip", new Type[]
{
	typeof(int),
	typeof(int),
	typeof(bool)
})]
internal static class OfficialBiographyLianQiInterceptionPatch
{
	private static bool Prefix(int npcId, int equipType, bool isLianQi)
	{
		if (!isLianQi)
		{
			return true;
		}
		return !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.LianQi, npcId, "NpcJieSuanManager.AddNpcEquip", new JObject
		{
			["npcId"] = npcId,
			["equipType"] = equipType,
			["isLianQi"] = true
		});
	}
}
