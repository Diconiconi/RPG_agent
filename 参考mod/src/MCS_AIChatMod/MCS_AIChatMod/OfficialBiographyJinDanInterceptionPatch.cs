using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCTuPo), "NpcTuPoJinDan", new Type[]
{
	typeof(int),
	typeof(bool)
})]
internal static class OfficialBiographyJinDanInterceptionPatch
{
	private static bool Prefix(int npcId, bool isKuaiSu)
	{
		return !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.JinDanSuccess, npcId, "NPCTuPo.NpcTuPoJinDan", new JObject
		{
			["npcId"] = npcId,
			["isKuaiSu"] = isKuaiSu
		});
	}
}
