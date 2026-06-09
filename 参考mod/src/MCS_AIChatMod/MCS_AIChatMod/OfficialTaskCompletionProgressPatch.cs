using Fungus;
using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(SetTaskIndexFinish), "Do")]
internal static class OfficialTaskCompletionProgressPatch
{
	private static void Postfix(int TaskID, int TaskIndex)
	{
		AIQuestManager.Instance?.NotifyOfficialTaskProgressChanged(TaskID, TaskIndex);
	}
}
