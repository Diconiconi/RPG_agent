using System;
using BepInEx.Logging;
using HarmonyLib;
using KBEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(Avatar), "AddTime")]
internal static class QuestTimeAdvancePatches
{
	private static void Postfix(Avatar __instance, int addday, int addMonth = 0, int Addyear = 0)
	{
		try
		{
			if (__instance != null && PlayerEx.Player != null && __instance == PlayerEx.Player)
			{
				AIQuestManager.Instance?.OnPlayerTimeAdvanced(addday, addMonth, Addyear);
			}
		}
		catch (Exception ex)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogWarning((object)("[QuestTimeAdvancePatches] Avatar.AddTime postfix failed: " + ex.Message));
			}
		}
	}
}
