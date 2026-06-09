using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCTuPo), "NpcSmallToPo", new Type[]
{
	typeof(JSONObject),
	typeof(int)
})]
internal static class OfficialBiographySmallTuPoInterceptionPatch
{
	private static bool Prefix(ref JSONObject __result, JSONObject npcDate, int nextLevel)
	{
		int npcId = ((npcDate != null && npcDate.HasField("id")) ? npcDate["id"].I : 0);
		if (!OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.SmallTuPo, npcId, "NPCTuPo.NpcSmallToPo", new JObject
		{
			["npcId"] = npcId,
			["nextLevel"] = nextLevel
		}))
		{
			return true;
		}
		__result = npcDate;
		return false;
	}
}
