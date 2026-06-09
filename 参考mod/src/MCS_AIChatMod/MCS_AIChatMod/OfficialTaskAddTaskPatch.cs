using System;
using HarmonyLib;
using KBEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(TaskMag), "addTask", new Type[] { typeof(int) })]
internal static class OfficialTaskAddTaskPatch
{
	private static void Postfix(TaskMag __instance, int taskId)
	{
		if (taskId > 0 && (Tools.instance?.getPlayer())?.taskMag == __instance)
		{
			AIQuestManager.Instance?.NotifyOfficialTaskProgressChanged(taskId);
		}
	}
}
