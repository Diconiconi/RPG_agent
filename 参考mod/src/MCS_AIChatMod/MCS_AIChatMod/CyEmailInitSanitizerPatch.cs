using BepInEx.Logging;
using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(CyEmail), "Init")]
internal static class CyEmailInitSanitizerPatch
{
	private static void Prefix(int npcId)
	{
		string report;
		int removed = QuestCyMailHelper.SanitizeCyEmailBucketsForNpc(npcId, out report);
		if (removed > 0)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogWarning((object)$"[QuestCyMail] Sanitized {removed} invalid cy emails before opening npc={npcId}. {report}");
			}
		}
	}
}
