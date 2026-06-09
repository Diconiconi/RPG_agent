using System;
using HarmonyLib;
using KBEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(Avatar), "addItem", new Type[]
{
	typeof(int),
	typeof(JSONObject),
	typeof(int)
})]
internal static class OfficialPlayerItemAddedPatch
{
	private static void Prefix(Avatar __instance, int itemID, out int __state)
	{
		__state = ((__instance != null && itemID > 0) ? Math.Max(0, __instance.getItemNum(itemID)) : 0);
	}

	private static void Postfix(Avatar __instance, int itemID, int __state)
	{
		Avatar player = Tools.instance?.getPlayer();
		if (__instance != null && player == __instance && itemID > 0)
		{
			int currentCount = Math.Max(0, __instance.getItemNum(itemID));
			if (currentCount != __state)
			{
				AIQuestManager.Instance?.NotifyPlayerItemChanged(itemID, __state, currentCount);
			}
		}
	}
}
