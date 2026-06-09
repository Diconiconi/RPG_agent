using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCTuPo), "NpcTuPoZhuJi", new Type[]
{
	typeof(int),
	typeof(bool)
})]
internal static class OfficialBiographyZhuJiInterceptionPatch
{
	private static bool Prefix(int npcId, bool isKuaiSu)
	{
		return !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.ZhuJiSuccess, npcId, "NPCTuPo.NpcTuPoZhuJi", new JObject
		{
			["npcId"] = npcId,
			["isKuaiSu"] = isKuaiSu
		});
	}
}
