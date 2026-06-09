using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCUseItem), "UseDanYao", new Type[]
{
	typeof(int),
	typeof(JSONObject),
	typeof(int)
})]
internal static class OfficialBiographyUseDanYaoInterceptionPatch
{
	private static bool Prefix(int npcID, JSONObject item, int seid)
	{
		int itemId = ((item != null && item.HasField("ItemID")) ? item["ItemID"].I : 0);
		return !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.UseDanYao, npcID, "NPCUseItem.UseDanYao", new JObject
		{
			["npcId"] = npcID,
			["itemId"] = itemId,
			["seid"] = seid
		});
	}
}
