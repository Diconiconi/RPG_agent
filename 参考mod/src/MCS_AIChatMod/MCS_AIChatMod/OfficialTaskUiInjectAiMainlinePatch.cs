using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(TaskUIManager), "initTaskList")]
internal static class OfficialTaskUiInjectAiMainlinePatch
{
	private static void Postfix(TaskUIManager __instance)
	{
		OfficialTaskUiBridge.InjectAiMainlineEntries(__instance);
	}
}
