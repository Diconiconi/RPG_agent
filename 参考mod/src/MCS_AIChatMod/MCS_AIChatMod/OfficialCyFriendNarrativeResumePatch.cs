using HarmonyLib;
using KBEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(Avatar), "AddFriend")]
internal static class OfficialCyFriendNarrativeResumePatch
{
	private static void Postfix(int npcId)
	{
		AIQuestManager.Instance?.NotifyCyFriendRelationChanged(npcId);
	}
}
