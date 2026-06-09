using System;
using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCEx), "SetNPCAction", new Type[]
{
	typeof(int),
	typeof(int)
})]
internal static class OfficialNpcActionChangedPatch
{
	private static void Prefix(int npcid, out int __state)
	{
		__state = 0;
		int normalizedNpcId = NPCEx.NPCIDToNew(npcid);
		JSONObject npcData = NpcJieSuanManager.inst?.GetNpcData(normalizedNpcId);
		if (npcData != null)
		{
			__state = npcData["ActionId"].I;
		}
	}

	private static void Postfix(int npcid, int actionID, int __state)
	{
		int normalizedNpcId = NPCEx.NPCIDToNew(npcid);
		int currentActionId = (NpcJieSuanManager.inst?.GetNpcData(normalizedNpcId))?["ActionId"].I ?? actionID;
		if (currentActionId != __state)
		{
			AIQuestManager.Instance?.NotifyNpcActionChanged(normalizedNpcId, __state, currentActionId);
		}
	}
}
