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
			if (UINPCJiaoHu.Inst.IsLiaoTianClicked)
			{
				UIManager.Instance?.ScheduleFreeInputOptionForNpcTalk();
			}
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogError((object)$"交互处理异常: {arg}");
		}
	}
}

[HarmonyPatch(typeof(OnLiaoTianClick), "Update")]
public static class OnLiaoTianClickPatch
{
	[HarmonyPrefix]
	public static void Prefix(out bool __state)
	{
		__state = UINPCJiaoHu.Inst?.IsLiaoTianClicked == true;
	}

	[HarmonyPostfix]
	public static void Postfix(bool __state)
	{
		if (!__state || UINPCJiaoHu.Inst?.NowJiaoHuNPC == null)
		{
			return;
		}
		try
		{
			UIManager.Instance?.ScheduleFreeInputOptionForNpcTalk();
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogWarning((object)$"[OnLiaoTianClickPatch] 注入自由输入入口失败: {arg}");
		}
	}
}
