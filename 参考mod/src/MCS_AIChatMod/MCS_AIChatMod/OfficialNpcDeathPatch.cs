using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCDeath), "SetNpcDeath")]
internal static class OfficialNpcDeathPatch
{
	private static void Postfix(int deathType, int npcId, int killNpcId, bool after)
	{
		if (!after)
		{
			AIQuestManager.Instance?.NotifyNpcDeath(npcId);
		}
	}
}
