using HarmonyLib;
using KBEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(Avatar), "levelUp")]
internal static class OfficialPlayerLevelUpPatch
{
	private static void Prefix(Avatar __instance, out int __state)
	{
		__state = __instance?.level ?? 0;
	}

	private static void Postfix(Avatar __instance, int __state)
	{
		int currentLevel = __instance?.level ?? 0;
		if (currentLevel != __state)
		{
			AIQuestManager.Instance?.NotifyPlayerLevelChanged(__state, currentLevel);
		}
	}
}
