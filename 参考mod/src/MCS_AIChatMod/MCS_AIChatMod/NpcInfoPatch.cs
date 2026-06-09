using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NpcJieSuanManager), "InitCyData")]
public static class NpcInfoPatch
{
	[HarmonyPostfix]
	private static void OnNpcDataLoaded()
	{
		AIChatManager.logger.LogInfo((object)"NPC数据加载完成");
	}
}
