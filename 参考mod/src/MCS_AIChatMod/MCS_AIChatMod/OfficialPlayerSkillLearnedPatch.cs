using System;
using HarmonyLib;
using KBEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(Avatar), "addHasSkillList", new Type[] { typeof(int) })]
internal static class OfficialPlayerSkillLearnedPatch
{
	private static void Prefix(int SkillId, out bool __state)
	{
		__state = SkillId > 0 && PlayerEx.HasSkill(SkillId);
	}

	private static void Postfix(int SkillId, bool __state)
	{
		if (SkillId > 0)
		{
			bool currentHasSkill = PlayerEx.HasSkill(SkillId);
			if (currentHasSkill != __state)
			{
				AIQuestManager.Instance?.NotifyPlayerSkillChanged(SkillId, __state, currentHasSkill);
			}
		}
	}
}
