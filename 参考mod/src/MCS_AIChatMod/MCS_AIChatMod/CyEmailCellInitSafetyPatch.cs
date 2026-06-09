using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(CyEmailCell), "Init")]
internal static class CyEmailCellInitSafetyPatch
{
	private static bool Prefix(CyEmailCell __instance, EmailData emailData, bool isDeath)
	{
		JSONObject chuanYingList = PlayerEx.Player?.NewChuanYingList;
		if (QuestCyMailHelper.IsCyEmailRenderable(emailData, chuanYingList, out var reason))
		{
			return true;
		}
		QuestCyMailHelper.ConfigureInvalidCyEmailCell(__instance, emailData, reason);
		return false;
	}
}
