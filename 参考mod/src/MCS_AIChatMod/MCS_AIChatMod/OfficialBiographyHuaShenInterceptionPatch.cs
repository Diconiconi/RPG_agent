using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCTuPo), "NpcTuPoHuaShen", new Type[]
{
	typeof(int),
	typeof(bool)
})]
internal static class OfficialBiographyHuaShenInterceptionPatch
{
	private static bool Prefix(int npcId, bool isKuaiSu)
	{
		return !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.HuaShenSuccess, npcId, "NPCTuPo.NpcTuPoHuaShen", new JObject
		{
			["npcId"] = npcId,
			["isKuaiSu"] = isKuaiSu
		});
	}
}
