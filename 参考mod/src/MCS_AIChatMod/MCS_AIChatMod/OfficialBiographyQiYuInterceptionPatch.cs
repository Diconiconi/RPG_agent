using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCLiLian), "NPCYouLi", new Type[] { typeof(int) })]
internal static class OfficialBiographyQiYuInterceptionPatch
{
	private static bool Prefix(int npcId)
	{
		return !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.QiYu, npcId, "NPCLiLian.NPCYouLi", new JObject { ["npcId"] = npcId });
	}
}
