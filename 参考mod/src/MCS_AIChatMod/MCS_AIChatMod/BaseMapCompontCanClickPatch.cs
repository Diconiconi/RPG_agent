using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(BaseMapCompont), "CanClick")]
public static class BaseMapCompontCanClickPatch
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
