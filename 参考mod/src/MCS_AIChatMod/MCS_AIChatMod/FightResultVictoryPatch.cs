using System;
using Fight;
using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(FightResultMag), "ShowVictory")]
public static class FightResultVictoryPatch
{
	[HarmonyPostfix]
	public static void Postfix()
	{
		try
		{
			int targetNpcId = ((Tools.instance != null) ? Tools.instance.MonstarID : 0);
			if (targetNpcId > 0)
			{
				AIQuestManager.Instance?.NotifyBattleVictory(targetNpcId);
			}
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogError((object)$"[AIChatManager] FightResultVictoryPatch exception: {arg}");
		}
	}
}
