using System;
using HarmonyLib;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(UINPCJiaoHuPop), "OnJiaoTanBtnClick")]
public static class OnJiaoTanBtnClickPatch
{
	[HarmonyPostfix]
	public static void Postfix(UINPCJiaoHuPop __instance)
	{
		if (UINPCJiaoHu.Inst?.NowJiaoHuNPC == null)
		{
			return;
		}
		try
		{
			AIChatManager.HandleNpcInteractionDetected(UINPCJiaoHu.Inst.NowJiaoHuNPC.ID);
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogError((object)$"交互处理异常: {arg}");
		}
	}
}
