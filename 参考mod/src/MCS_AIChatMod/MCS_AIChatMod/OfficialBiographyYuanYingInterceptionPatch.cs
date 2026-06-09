using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCTuPo), "NpcTuPoYuanYing", new Type[]
{
	typeof(int),
	typeof(bool)
})]
internal static class OfficialBiographyYuanYingInterceptionPatch
{
	private static bool Prefix(int npcId, bool isKuaiSu)
	{
		return !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.YuanYingSuccess, npcId, "NPCTuPo.NpcTuPoYuanYing", new JObject
		{
			["npcId"] = npcId,
			["isKuaiSu"] = isKuaiSu
		});
	}
}
