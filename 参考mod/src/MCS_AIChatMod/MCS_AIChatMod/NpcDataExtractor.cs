using System;
using System.Linq;
using JSONClass;

namespace MCS_AIChatMod;

public static class NpcDataExtractor
{
	public static NPCInfo ExtractFromGameMemory(int npcId)
	{
		NPCInfo npcInfo = new NPCInfo
		{
			NpcID = npcId
		};
		JSONObject NPCJsonData = npcInfo.NpcID.NPCJson();
		try
		{
			if (NPCJsonData == null)
			{
				AIChatManager.logger.LogError((object)$"NPC数据为空, ID: {npcId}");
				return npcInfo;
			}
			npcInfo.Name = jsonData.instance.AvatarRandomJsonData[npcInfo.NpcID.ToString()]["Name"].Str;
			npcInfo.Favor = jsonData.instance.AvatarRandomJsonData[npcInfo.NpcID.ToString()]["HaoGanDu"].I;
			npcInfo.HaoGanDu = MCS_Converter.GetFavorText(npcInfo.Favor);
			npcInfo.QingFen = NPCJsonData["QingFen"].I;
			npcInfo.Level = NPCJsonData["Level"].I;
			npcInfo.XiuWei = MCS_Converter.GetXiuWeiText(npcInfo.Level - 1);
			npcInfo.Status = NPCJsonData["Status"]["StatusId"].I;
			npcInfo.StatusStr = MCS_Converter.GetStatusText(npcInfo.Status);
			npcInfo.ShouYuan = NPCJsonData["shouYuan"].I;
			npcInfo.Age = NPCJsonData["age"].I / 12;
			npcInfo.ZiZhi = NPCJsonData["ziZhi"].I;
			npcInfo.WuXing = NPCJsonData["wuXin"].I;
			npcInfo.DunSu = NPCJsonData["dunSu"].I;
			npcInfo.ShenShi = NPCJsonData["shengShi"].I;
			int xingGe = NPCJsonData["XingGe"].I;
			npcInfo.XingGe = MCS_Converter.GetCharacterText(xingGe);
			string tagData = UINPCJiaoHu.Inst?.NowJiaoHuNPC?.Tag.ToString();
			if (!string.IsNullOrEmpty(tagData) && jsonData.instance.NPCTagDate.HasField(tagData))
			{
				npcInfo.ZhengXie = ((jsonData.instance.NPCTagDate[tagData]["zhengxie"].I == 1) ? "正道" : "魔道");
			}
			else
			{
				npcInfo.ZhengXie = "未知";
			}
			npcInfo.HP = NPCJsonData["HP"].I;
			int sexType = NPCJsonData["SexType"].I;
			npcInfo.Gender = ((sexType == 1) ? "男" : "女");
			npcInfo.Title = NPCJsonData["Title"].str;
			if (string.IsNullOrEmpty(npcInfo.Title))
			{
				npcInfo.Title = "无";
			}
			JSONObject backpackData = jsonData.instance.AvatarBackpackJsonData[npcInfo.NpcID.ToString()]["Backpack"];
			int result;
			if (backpackData != null)
			{
				npcInfo.BackPackNames = (from item in backpackData.list
					select (int.TryParse(item["ItemID"].ToString(), out result) && _ItemJsonData.DataDict.ContainsKey(result)) ? _ItemJsonData.DataDict[result].name : null into name
					where !string.IsNullOrEmpty(name)
					select name).ToList();
			}
			npcInfo.StaticSkillsNames = (from skillId in NPCJsonData["staticSkills"].ToList()
				select (int.TryParse(skillId.ToString(), out result) && StaticSkillJsonData.DataDict.ContainsKey(result)) ? StaticSkillJsonData.DataDict[result].name : null into name
				where !string.IsNullOrEmpty(name)
				select name).ToList();
			npcInfo.SkillsNames = (from skillId in NPCJsonData["skills"].ToList()
				select (int.TryParse(skillId.ToString(), out result) && _skillJsonData.DataDict.ContainsKey(result)) ? _skillJsonData.DataDict[result].name : null into name
				where !string.IsNullOrEmpty(name)
				select name).ToList();
			if (UINPCJiaoHu.Inst != null && UINPCJiaoHu.Inst.NowJiaoHuNPC != null)
			{
				npcInfo.Events = UINPCJiaoHu.Inst.NowJiaoHuNPC.Events;
			}
			return npcInfo;
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogError((object)$"读取NPC {npcId} 数据失败: {arg}");
			return npcInfo;
		}
	}
}
