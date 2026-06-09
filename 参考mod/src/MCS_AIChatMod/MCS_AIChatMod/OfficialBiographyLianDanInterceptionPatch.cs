using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCFuYe), "NpcLianDan", new Type[] { typeof(int) })]
internal static class OfficialBiographyLianDanInterceptionPatch
{
	private static bool Prefix(int npcId)
	{
		return !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.LianDan, npcId, "NPCFuYe.NpcLianDan", new JObject { ["npcId"] = npcId });
	}
}
