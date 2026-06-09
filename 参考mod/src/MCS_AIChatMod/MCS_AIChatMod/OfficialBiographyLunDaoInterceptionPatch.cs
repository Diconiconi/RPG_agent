using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCXiuLian), "NextNpcLunDao", new Type[] { })]
internal static class OfficialBiographyLunDaoInterceptionPatch
{
	private static bool Prefix()
	{
		for (int i = NpcJieSuanManager.inst.lunDaoNpcList.Count - 1; i >= 0; i--)
		{
			int npcId = NpcJieSuanManager.inst.lunDaoNpcList[i];
			if (NpcJieSuanManager.inst.npcDeath.npcDeathJson.HasField(npcId.ToString()) || !jsonData.instance.AvatarJsonData.HasField(npcId.ToString()))
			{
				NpcJieSuanManager.inst.lunDaoNpcList.RemoveAt(i);
			}
		}
		if (NpcJieSuanManager.inst.lunDaoNpcList.Count < 2)
		{
			return false;
		}
		while (NpcJieSuanManager.inst.lunDaoNpcList.Count >= 2)
		{
			int firstIndex = NpcJieSuanManager.inst.getRandomInt(0, NpcJieSuanManager.inst.lunDaoNpcList.Count - 1);
			int firstNpcId = NpcJieSuanManager.inst.lunDaoNpcList[firstIndex];
			NpcJieSuanManager.inst.lunDaoNpcList.RemoveAt(firstIndex);
			int secondIndex = NpcJieSuanManager.inst.getRandomInt(0, NpcJieSuanManager.inst.lunDaoNpcList.Count - 1);
			int secondNpcId = NpcJieSuanManager.inst.lunDaoNpcList[secondIndex];
			NpcJieSuanManager.inst.lunDaoNpcList.RemoveAt(secondIndex);
			string firstName = jsonData.instance.AvatarRandomJsonData[firstNpcId.ToString()]["Name"].str;
			string secondName = jsonData.instance.AvatarRandomJsonData[secondNpcId.ToString()]["Name"].str;
			if (NpcJieSuanManager.inst.getRandomInt(1, 100) < 10)
			{
				if (!OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.LunDaoFail, firstNpcId, "NPCXiuLian.NextNpcLunDao", new JObject
				{
					["npcId"] = firstNpcId,
					["otherNpcId"] = secondNpcId,
					["otherNpcName"] = secondName,
					["selfName"] = firstName,
					["resultKind"] = "LunDaoFail"
				}) && !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.LunDaoFail, secondNpcId, "NPCXiuLian.NextNpcLunDao", new JObject
				{
					["npcId"] = secondNpcId,
					["otherNpcId"] = firstNpcId,
					["otherNpcName"] = firstName,
					["selfName"] = secondName,
					["resultKind"] = "LunDaoFail"
				}))
				{
					NpcJieSuanManager.inst.npcNoteBook.NoteLunDaoFail(firstNpcId, secondName);
					NpcJieSuanManager.inst.npcNoteBook.NoteLunDaoFail(secondNpcId, firstName);
				}
				continue;
			}
			try
			{
				float firstWuXin = jsonData.instance.AvatarJsonData[firstNpcId.ToString()]["wuXin"].f / 100f;
				float secondWuXin = jsonData.instance.AvatarJsonData[secondNpcId.ToString()]["wuXin"].f / 100f;
				JSONObject firstWuDaoLevelData = jsonData.instance.WuDaoZhiData[jsonData.instance.AvatarJsonData[firstNpcId.ToString()]["WuDaoValueLevel"].I.ToString()];
				JSONObject secondWuDaoLevelData = jsonData.instance.WuDaoZhiData[jsonData.instance.AvatarJsonData[secondNpcId.ToString()]["WuDaoValueLevel"].I.ToString()];
				int firstWuDaoZhiDelta = firstWuDaoLevelData["LevelUpExp"].I / firstWuDaoLevelData["LevelUpNum"].I;
				int secondWuDaoZhiDelta = secondWuDaoLevelData["LevelUpExp"].I / secondWuDaoLevelData["LevelUpNum"].I;
				int firstWuDaoExp = (int)((float)jsonData.instance.NpcLevelShouYiDate[jsonData.instance.AvatarJsonData[firstNpcId.ToString()]["Level"].I.ToString()]["wudaoexp"].I * (1f + firstWuXin));
				int secondWuDaoExp = (int)((float)jsonData.instance.NpcLevelShouYiDate[jsonData.instance.AvatarJsonData[secondNpcId.ToString()]["Level"].I.ToString()]["wudaoexp"].I * (1f + secondWuXin));
				int wuDaoId = NpcJieSuanManager.inst.getRandomInt(1, 10);
				if (!OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.LunDaoSuccess, firstNpcId, "NPCXiuLian.NextNpcLunDao", new JObject
				{
					["npcId"] = firstNpcId,
					["otherNpcId"] = secondNpcId,
					["otherNpcName"] = secondName,
					["selfName"] = firstName,
					["resultKind"] = "LunDaoSuccess",
					["selfWuDaoZhi"] = firstWuDaoZhiDelta,
					["otherWuDaoZhi"] = secondWuDaoZhiDelta,
					["wuDaoId"] = wuDaoId,
					["selfWuDaoExp"] = firstWuDaoExp,
					["otherWuDaoExp"] = secondWuDaoExp
				}) && !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.LunDaoSuccess, secondNpcId, "NPCXiuLian.NextNpcLunDao", new JObject
				{
					["npcId"] = secondNpcId,
					["otherNpcId"] = firstNpcId,
					["otherNpcName"] = firstName,
					["selfName"] = secondName,
					["resultKind"] = "LunDaoSuccess",
					["selfWuDaoZhi"] = secondWuDaoZhiDelta,
					["otherWuDaoZhi"] = firstWuDaoZhiDelta,
					["wuDaoId"] = wuDaoId,
					["selfWuDaoExp"] = secondWuDaoExp,
					["otherWuDaoExp"] = firstWuDaoExp
				}))
				{
					NpcJieSuanManager.inst.npcSetField.AddNpcWuDaoZhi(firstNpcId, firstWuDaoZhiDelta);
					NpcJieSuanManager.inst.npcSetField.AddNpcWuDaoZhi(secondNpcId, secondWuDaoZhiDelta);
					NpcJieSuanManager.inst.npcSetField.AddNpcWuDaoExp(firstNpcId, wuDaoId, firstWuDaoExp);
					NpcJieSuanManager.inst.npcSetField.AddNpcWuDaoExp(secondNpcId, wuDaoId, secondWuDaoExp);
					NpcJieSuanManager.inst.npcNoteBook.NoteLunDaoSuccess(firstNpcId, secondName);
					NpcJieSuanManager.inst.npcNoteBook.NoteLunDaoSuccess(secondNpcId, firstName);
				}
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
			}
		}
		NpcJieSuanManager.inst.lunDaoNpcList = new List<int>();
		return false;
	}
}
