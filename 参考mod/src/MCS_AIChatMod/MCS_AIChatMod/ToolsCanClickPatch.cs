using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(Tools), "canClick")]
public static class ToolsCanClickPatch
{
	[HarmonyPostfix]
	public static void Postfix(ref bool __result)
	{
		if (__result && !(UIManager.Instance == null) && UIManager.Instance.IsPointerOverModUI())
		{
			__result = false;
		}
	}
}
