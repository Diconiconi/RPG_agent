using System;
using HarmonyLib;
using KBEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(Avatar), "addHasStaticSkillList", new Type[]
{
	typeof(int),
	typeof(int)
})]
internal static class OfficialPlayerStaticSkillLearnedPatch
{
	private static void Prefix(int SkillId, out bool __state)
	{
		__state = SkillId > 0 && PlayerEx.HasStaticSkill(SkillId);
	}

	private static void Postfix(int SkillId, bool __state)
	{
		if (SkillId > 0)
		{
			bool currentHasSkill = PlayerEx.HasStaticSkill(SkillId);
			if (currentHasSkill != __state)
			{
				AIQuestManager.Instance?.NotifyPlayerStaticSkillChanged(SkillId, __state, currentHasSkill);
			}
		}
	}
}
