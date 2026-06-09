using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

[HarmonyPatch(typeof(NPCTeShu), "NextJieSha", new Type[] { })]
internal static class OfficialBiographyJieShaInterceptionPatch
{
	private static bool Prefix()
	{
		if (NpcJieSuanManager.inst.JieShaNpcList.Count < 1 || NpcJieSuanManager.inst.allBigMapNpcList.Count < 1)
		{
			return false;
		}
		foreach (int npcId in NpcJieSuanManager.inst.JieShaNpcList)
		{
			if (NpcJieSuanManager.inst.IsDeath(npcId) || NpcJieSuanManager.inst.getRandomInt(0, 1000) > 15)
			{
				continue;
			}
			JSONObject npcData = NpcJieSuanManager.inst.GetNpcData(npcId);
			for (int i = NpcJieSuanManager.inst.allBigMapNpcList.Count - 1; i >= 0; i--)
			{
				int targetNpcId = NpcJieSuanManager.inst.allBigMapNpcList[i];
				if (NpcJieSuanManager.inst.IsDeath(targetNpcId))
				{
					NpcJieSuanManager.inst.allBigMapNpcList.RemoveAt(i);
				}
				else
				{
					JSONObject targetNpcData = NpcJieSuanManager.inst.GetNpcData(targetNpcId);
					if (NpcJieSuanManager.inst.GetNpcBigLevel(npcId) == NpcJieSuanManager.inst.GetNpcBigLevel(targetNpcId))
					{
						string npcName = jsonData.instance.AvatarRandomJsonData[npcId.ToString()]["Name"].str;
						string targetName = jsonData.instance.AvatarRandomJsonData[targetNpcId.ToString()]["Name"].str;
						bool intercepted = false;
						if (NpcJieSuanManager.inst.getRandomInt(0, 100) > 50)
						{
							if (NpcJieSuanManager.inst.getRandomInt(0, 1000) > jsonData.instance.NpcLevelShouYiDate[targetNpcData["Level"].I.ToString()]["siwangjilv"].I || targetNpcData["isImportant"].b)
							{
								if (!OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.JieShaFail1, npcId, "NPCTeShu.NextJieSha", new JObject
								{
									["npcId"] = npcId,
									["targetNpcId"] = targetNpcId,
									["targetName"] = targetName,
									["actorName"] = npcName,
									["resultKind"] = "JieShaFail1"
								}) && !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.FanShaFail1, targetNpcId, "NPCTeShu.NextJieSha", new JObject
								{
									["npcId"] = npcId,
									["targetNpcId"] = targetNpcId,
									["targetName"] = targetName,
									["actorName"] = npcName,
									["resultKind"] = "JieShaFail1"
								}))
								{
									NpcJieSuanManager.inst.npcNoteBook.NoteJieShaFail1(npcId, targetName);
									NpcJieSuanManager.inst.npcNoteBook.NoteFanShaFail1(targetNpcId, npcName);
								}
							}
							else
							{
								int moneyDelta = jsonData.instance.NpcLevelShouYiDate[npcData["Level"].I.ToString()]["money"].I * 10;
								if (!OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.JieShaSuccess, npcId, "NPCTeShu.NextJieSha", new JObject
								{
									["npcId"] = npcId,
									["targetNpcId"] = targetNpcId,
									["targetName"] = targetName,
									["deathType"] = 5,
									["killNpcId"] = npcId,
									["moneyDelta"] = moneyDelta,
									["resultKind"] = "JieShaSuccess"
								}))
								{
									NpcJieSuanManager.inst.npcNoteBook.NoteJieShaSuccess(npcId, targetName);
									if (Tools.instance.getPlayer().emailDateMag.IsFriend(targetNpcId))
									{
										int randomInt = NpcJieSuanManager.inst.getRandomInt(1, 3);
										int duiBaiId = NpcJieSuanManager.inst.GetDuiBaiId(jsonData.instance.AvatarJsonData[targetNpcId.ToString()]["XingGe"].I, 999);
										Tools.instance.getPlayer().emailDateMag.SendToPlayerNoPd(targetNpcId, duiBaiId, randomInt, npcName, NpcJieSuanManager.inst.JieSuanTime);
									}
									NpcJieSuanManager.inst.npcDeath.SetNpcDeath(5, targetNpcId, npcId);
									NpcJieSuanManager.inst.npcSetField.AddNpcMoney(npcId, moneyDelta);
								}
							}
						}
						else if (NpcJieSuanManager.inst.getRandomInt(0, 1000) > jsonData.instance.NpcLevelShouYiDate[npcData["Level"].I.ToString()]["siwangjilv"].I || npcData["isImportant"].b)
						{
							if (!OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.JieShaFail2, npcId, "NPCTeShu.NextJieSha", new JObject
							{
								["npcId"] = npcId,
								["targetNpcId"] = targetNpcId,
								["targetName"] = targetName,
								["actorName"] = npcName,
								["resultKind"] = "JieShaFail2"
							}) && !OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.FanShaFail2, targetNpcId, "NPCTeShu.NextJieSha", new JObject
							{
								["npcId"] = npcId,
								["targetNpcId"] = targetNpcId,
								["targetName"] = targetName,
								["actorName"] = npcName,
								["resultKind"] = "JieShaFail2"
							}))
							{
								NpcJieSuanManager.inst.npcNoteBook.NoteJieShaFail2(npcId, targetName);
								NpcJieSuanManager.inst.npcNoteBook.NoteFanShaFail2(targetNpcId, npcName);
							}
						}
						else
						{
							int moneyDelta2 = jsonData.instance.NpcLevelShouYiDate[targetNpcData["Level"].I.ToString()]["money"].I * 10;
							if (!OfficialBiographyInterceptionHelper.TryCapture(BiographyEventKind.FanShaSuccess, targetNpcId, "NPCTeShu.NextJieSha", new JObject
							{
								["npcId"] = targetNpcId,
								["targetNpcId"] = npcId,
								["targetName"] = npcName,
								["deathType"] = 11,
								["killNpcId"] = targetNpcId,
								["moneyDelta"] = moneyDelta2,
								["resultKind"] = "FanShaSuccess"
							}))
							{
								NpcJieSuanManager.inst.npcNoteBook.NoteFanShaSuccess(targetNpcId, npcName);
								NpcJieSuanManager.inst.npcDeath.SetNpcDeath(11, npcId, targetNpcId);
								NpcJieSuanManager.inst.npcSetField.AddNpcMoney(targetNpcId, moneyDelta2);
							}
						}
						NpcJieSuanManager.inst.allBigMapNpcList.RemoveAt(i);
						break;
					}
				}
			}
		}
		NpcJieSuanManager.inst.JieShaNpcList = new List<int>();
		NpcJieSuanManager.inst.allBigMapNpcList = new List<int>();
		return false;
	}
}
