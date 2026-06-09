using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCStatus), "SetNpcStatus")]
internal static class OfficialNpcStatusChangedPatch
{
	private static void Postfix(int npcId, int status)
	{
		AIQuestManager.Instance?.NotifyNpcStatusChanged(npcId, status);
	}
}
