using Fight;
using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(FightResultMag), "ShowVictory")]
internal static class OfficialFightVictoryPatch
{
	private static void Postfix()
	{
		int npcId = Tools.instance?.MonstarID ?? 0;
		if (npcId > 0)
		{
			AIQuestManager.Instance?.NotifyBattleVictory(npcId);
		}
	}
}
