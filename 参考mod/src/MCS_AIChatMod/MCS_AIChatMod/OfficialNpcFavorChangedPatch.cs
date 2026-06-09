using System;
using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCEx), "AddFavor", new Type[]
{
	typeof(int),
	typeof(int),
	typeof(bool),
	typeof(bool)
})]
internal static class OfficialNpcFavorChangedPatch
{
	private static void Prefix(int npcid, out int __state)
	{
		__state = ((npcid > 0) ? NPCEx.GetFavor(npcid) : 0);
	}

	private static void Postfix(int npcid, int __state)
	{
		if (npcid > 0)
		{
			int currentFavor = NPCEx.GetFavor(npcid);
			if (currentFavor != __state)
			{
				AIQuestManager.Instance?.NotifyNpcFavorChanged(npcid, __state, currentFavor);
			}
		}
	}
}
