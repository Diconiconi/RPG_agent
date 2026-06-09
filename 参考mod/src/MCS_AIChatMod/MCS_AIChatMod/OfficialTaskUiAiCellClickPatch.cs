using System;
using HarmonyLib;
using UnityEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(TaskRenWuCell), "Click")]
internal static class OfficialTaskUiAiCellClickPatch
{
	private static bool Prefix(TaskRenWuCell __instance)
	{
		return !OfficialTaskUiBridge.TryHandleAiCellClick(__instance);
	}

	private static void Postfix(TaskRenWuCell __instance)
	{
		if (!(__instance == null) && !(__instance.GetComponent<AIOfficialTaskCellMarker>() != null))
		{
			Transform listRoot = __instance.transform.parent;
			if (listRoot == null)
			{
				throw new InvalidOperationException("官方任务条目没有父节点，无法清理 AI 主线选中态。");
			}
			OfficialTaskUiBridge.ClearInjectedSelectionAfterOfficialClick(listRoot);
		}
	}
}
