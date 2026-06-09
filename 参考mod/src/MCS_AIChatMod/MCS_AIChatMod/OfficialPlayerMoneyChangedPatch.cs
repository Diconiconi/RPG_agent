using HarmonyLib;
using KBEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(Avatar), "AddMoney")]
internal static class OfficialPlayerMoneyChangedPatch
{
	private static void Prefix(Avatar __instance, out long __state)
	{
		__state = (long)(__instance?.money ?? 0);
	}

	private static void Postfix(Avatar __instance, long __state)
	{
		Avatar player = Tools.instance?.getPlayer();
		if (__instance != null && player == __instance)
		{
			long currentMoney = (long)__instance.money;
			if (currentMoney != __state)
			{
				AIQuestManager.Instance?.NotifyPlayerMoneyChanged(__state, currentMoney);
			}
		}
	}
}
