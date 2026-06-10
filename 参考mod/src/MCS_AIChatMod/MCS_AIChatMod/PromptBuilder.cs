using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KBEngine;
using UnityEngine;

namespace MCS_AIChatMod;

public static class PromptBuilder
{
	private static readonly Dictionary<string, string> BundledCharacterCardAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "星铃儿", Path.Combine("special", "xing_linger_xingning.character.txt") },
		{ "星凝", Path.Combine("special", "xing_linger_xingning.character.txt") },
		{ "魏无极", Path.Combine("special", "wei_wuji.character.txt") },
		{ "吞云大圣", Path.Combine("special", "tunyun_dasheng.character.txt") },
		{ "火麒麟", Path.Combine("special", "huoqilin.character.txt") },
		{ "冲虚", Path.Combine("special", "chongxu.character.txt") },
		{ "白帝", Path.Combine("special", "baidi.character.txt") }
	};

	public static string LoadCharacterCard(string targetFilePath)
	{
		return LoadCharacterCard(targetFilePath, null);
	}

	private static string LoadCharacterCard(string targetFilePath, NPCInfo npcInfo)
	{
		try
		{
			if (File.Exists(targetFilePath))
			{
				return File.ReadAllText(targetFilePath);
			}
			if (npcInfo != null && TryLoadBundledCharacterCard(npcInfo.NpcID, npcInfo.Name, out var characterContent))
			{
				File.WriteAllText(targetFilePath, characterContent, Encoding.UTF8);
				AIChatManager.logger?.LogInfo((object)("[PromptBuilder] 已从内置角色卡初始化: " + targetFilePath));
				return characterContent;
			}
			File.WriteAllText(targetFilePath, "空白角色卡", Encoding.UTF8);
			AIChatManager.logger?.LogInfo((object)"目标角色卡文件不存在，已创建空白卡！");
			return "";
		}
		catch (Exception ex)
		{
			AIChatManager.logger?.LogError((object)("加载角色卡时出错: " + ex.Message));
			return null;
		}
	}

	private static bool TryLoadBundledCharacterCard(int npcId, string npcName, out string content)
	{
		content = string.Empty;
		string bundledPath = ResolveBundledCharacterCardPath(ResolveBundledCharacterCardRoot(), npcId, npcName);
		if (string.IsNullOrEmpty(bundledPath))
		{
			return false;
		}
		try
		{
			content = TextEncodingUtil.ReadAllTextWithFallback(bundledPath);
			return !string.IsNullOrWhiteSpace(content);
		}
		catch (Exception ex)
		{
			AIChatManager.logger?.LogWarning((object)("[PromptBuilder] 读取内置角色卡失败: " + bundledPath + ", " + ex.Message));
			content = string.Empty;
			return false;
		}
	}

	private static string ResolveBundledCharacterCardRoot()
	{
		string pluginDirectory = ConfigManager.pluginDirectory;
		if (string.IsNullOrWhiteSpace(pluginDirectory))
		{
			pluginDirectory = Path.GetDirectoryName(typeof(PromptBuilder).Assembly.Location);
		}
		if (string.IsNullOrWhiteSpace(pluginDirectory))
		{
			return string.Empty;
		}
		return Path.Combine(pluginDirectory, "RPGSkillKit", "character_cards_minimal");
	}

	private static string ResolveBundledCharacterCardPath(string bundledRoot, int npcId, string npcName)
	{
		if (string.IsNullOrWhiteSpace(bundledRoot) || !Directory.Exists(bundledRoot))
		{
			return null;
		}
		if (npcId > 0)
		{
			string idPrefix = npcId.ToString() + "_";
			string idMatch = Directory.GetFiles(bundledRoot, "*.character.txt", SearchOption.AllDirectories)
				.OrderBy((string path) => path, StringComparer.OrdinalIgnoreCase)
				.FirstOrDefault((string path) => Path.GetFileName(path).StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase));
			if (!string.IsNullOrEmpty(idMatch))
			{
				return idMatch;
			}
		}
		if (!string.IsNullOrWhiteSpace(npcName) && BundledCharacterCardAliases.TryGetValue(npcName.Trim(), out var relativePath))
		{
			string aliasPath = Path.Combine(bundledRoot, relativePath);
			if (File.Exists(aliasPath))
			{
				return aliasPath;
			}
		}
		return null;
	}

	internal static string ResolveBundledCharacterCardPathForTest(string bundledRoot, int npcId, string npcName)
	{
		return ResolveBundledCharacterCardPath(bundledRoot, npcId, npcName);
	}

	private static PromptPlayerProfile BuildPlayerPromptProfile(int observerShenShi)
	{
		PromptPlayerProfile profile = new PromptPlayerProfile
		{
			Name = "玩家",
			Gender = "未知",
			Cultivation = "未知",
			Faction = "散修",
			Identity = "散修修士",
			VisibilitySummary = "你目前仅能稳定确认玩家的姓名、性别与门派背景；真实修为与正邪立场未显式掌握。"
		};
		try
		{
			Avatar player = Tools.instance?.getPlayer();
			int playerShenShi = player?.GetBaseShenShi() ?? 0;
			int playerLevel = player?.level ?? 0;
			profile.Name = jsonData.instance.AvatarRandomJsonData["1"]["Name"].Str;
			profile.Gender = ((jsonData.instance.AvatarJsonData["1"]["SexType"].I == 1) ? "男" : "女");
			profile.Cultivation = ((player != null) ? MCS_Converter.GetXiuWeiText(playerLevel - 1) : "未知");
			string faction = ((player != null) ? Tools.getStr("menpai" + player.menPai) : "散修");
			if (string.IsNullOrWhiteSpace(faction))
			{
				faction = "散修";
			}
			profile.Faction = faction;
			profile.Identity = ((faction == "散修") ? "散修修士" : (faction + "修士"));
			bool knowsCultivation = observerShenShi > 0 && observerShenShi > playerShenShi;
			profile.VisibilitySummary = (knowsCultivation ? ("你已能判断玩家修为约为" + profile.Cultivation + "；门派与性别通常可见；正邪立场暂未显式掌握。") : "你目前并不确定玩家的真实修为；门派与性别通常可见；正邪立场暂未显式掌握。");
		}
		catch (Exception ex)
		{
			AIChatManager.logger?.LogWarning((object)("[PromptBuilder] 构建玩家身份信息失败: " + ex.Message));
		}
		return profile;
	}

	internal static string BuildRelationshipPriorityBlock(string playerName, string playerSex, string playerCultivation, string playerFaction, string playerIdentity, string npcRelationship, string visibilitySummary)
	{
		string resolvedRelationship = (string.IsNullOrWhiteSpace(npcRelationship) ? "当前没有明确身份关系" : npcRelationship);
		string resolvedName = (string.IsNullOrWhiteSpace(playerName) ? "玩家" : playerName);
		string resolvedSex = (string.IsNullOrWhiteSpace(playerSex) ? "未知" : playerSex);
		string resolvedCultivation = (string.IsNullOrWhiteSpace(playerCultivation) ? "未知" : playerCultivation);
		string resolvedFaction = (string.IsNullOrWhiteSpace(playerFaction) ? "散修" : playerFaction);
		string resolvedIdentity = (string.IsNullOrWhiteSpace(playerIdentity) ? "修士" : playerIdentity);
		string resolvedVisibility = (string.IsNullOrWhiteSpace(visibilitySummary) ? "你需要先依据彼此关系决定称呼、语气、亲疏与态度。" : visibilitySummary);
		return string.Join("\n", "=== 你与玩家的关系定位（最高优先） ===", "优先依据这段关系定位决定称呼、语气、亲疏与态度。", "身份关系: " + resolvedRelationship, "玩家姓名: " + resolvedName, "玩家性别: " + resolvedSex, "玩家门派: " + resolvedFaction, "玩家修为: " + resolvedCultivation, "当前身份: " + resolvedIdentity, "你目前掌握的玩家可见信息: " + resolvedVisibility);
	}

	private static string BuildRelationshipPriorityBlock(NPCInfo npcInfo)
	{
		PromptPlayerProfile playerProfile = BuildPlayerPromptProfile(npcInfo?.ShenShi ?? 0);
		string npcRelationship = AIQuestPromptBuilder.ResolveNpcPlayerRelationshipForPrompt(npcInfo?.NpcID ?? 0);
		return BuildRelationshipPriorityBlock(playerProfile.Name, playerProfile.Gender, playerProfile.Cultivation, playerProfile.Faction, playerProfile.Identity, npcRelationship, playerProfile.VisibilitySummary);
	}

	private static string BuildRoleplayAttributePrompt(NPCInfo npcInfo, string characterContent)
	{
		PromptPlayerProfile playerProfile = BuildPlayerPromptProfile(npcInfo?.ShenShi ?? 0);
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("请你扮演觅长生世界中的npc,每次回复控制在50字以内");
		sb.AppendLine();
		sb.AppendLine("=== 最高优先：角色、关系与境界礼法 ===");
		sb.AppendLine("以下内容优先级高于普通闲聊、最近对话和杂项数值。回答时必须先符合角色卡、双方关系和境界礼法。");
		sb.AppendLine("NPC角色卡:");
		sb.AppendLine(string.IsNullOrWhiteSpace(characterContent) ? "空白角色卡" : characterContent.Trim());
		sb.AppendLine();
		sb.AppendLine(BuildRelationshipPriorityBlock(playerProfile.Name, playerProfile.Gender, playerProfile.Cultivation, playerProfile.Faction, playerProfile.Identity, AIQuestPromptBuilder.ResolveNpcPlayerRelationshipForPrompt(npcInfo?.NpcID ?? 0), playerProfile.VisibilitySummary));
		sb.AppendLine();
		sb.AppendLine("双方境界:");
		sb.AppendLine("玩家境界: " + playerProfile.Cultivation);
		sb.AppendLine("NPC境界: " + npcInfo.XiuWei);
		sb.AppendLine("境界礼法: 若对方境界明显高于你，称呼和语气必须更克制、敬重、谨慎，不要轻易挑衅或猖狂；若你无法确认玩家真实境界，也应保留修士间的基本分寸，先试探而不是冒犯。若你的角色卡明确设定为高位、狂傲或敌对，也要让冒犯建立在角色动机和实力判断上，而不是无意义发疯。");
		sb.AppendLine();
		sb.AppendLine("以下为玩家的基础信息:");
		sb.AppendLine("玩家的名字:" + playerProfile.Name);
		sb.AppendLine("玩家的性别:" + playerProfile.Gender + ",");
		sb.AppendLine("玩家的修为:" + playerProfile.Cultivation + ",");
		sb.AppendLine("玩家的门派:" + playerProfile.Faction + ",");
		sb.AppendLine("玩家的当前身份:" + playerProfile.Identity + ",");
		sb.AppendLine();
		sb.AppendLine("以下为你要扮演的npc的基础信息:");
		sb.AppendLine($"npc的id:{npcInfo.NpcID},");
		sb.AppendLine("名字: " + npcInfo.Name + ", ");
		sb.AppendLine("性别: " + npcInfo.Gender + ",");
		sb.AppendLine("性格: " + npcInfo.XingGe + ", ");
		sb.AppendLine("正魔立场: " + npcInfo.ZhengXie + ", ");
		sb.AppendLine("修为: " + npcInfo.XiuWei + ", ");
		sb.AppendLine("与玩家的关系: " + npcInfo.HaoGanDu + ", ");
		sb.AppendLine($"情分: {npcInfo.QingFen}, ");
		sb.AppendLine("状态: " + npcInfo.StatusStr + ", ");
		sb.AppendLine($"寿命: {npcInfo.ShouYuan}, ");
		sb.AppendLine($"年龄: {npcInfo.Age}, ");
		sb.AppendLine($"资质: {npcInfo.ZiZhi}, ");
		sb.AppendLine($"悟性: {npcInfo.WuXing}, ");
		sb.AppendLine($"遁速: {npcInfo.DunSu}, ");
		sb.AppendLine($"神识: {npcInfo.ShenShi}, ");
		sb.AppendLine("称号: " + npcInfo.Title);
		if (ConfigManager.includeNpcInventoryInPrompt?.Value == true)
		{
			sb.AppendLine("背包物品(最多20项): " + JoinLimited(npcInfo.BackPackNames, 20));
		}
		if (ConfigManager.includeNpcSkillsInPrompt?.Value == true)
		{
			sb.AppendLine("功法(最多12项): " + JoinLimited(npcInfo.StaticSkillsNames, 12));
			sb.AppendLine("神通(最多12项): " + JoinLimited(npcInfo.SkillsNames, 12));
		}
		return sb.ToString().TrimEnd();
	}

	public static List<ChatMessage> ResolveInitialContext(NPCInfo npcInfo)
	{
		List<ChatMessage> initMessages = new List<ChatMessage>();
		string saveFileName = $"{npcInfo.NpcID}_{npcInfo.Name}_character".Replace("/", "_");
		string characterContent = LoadCharacterCard(AIChatManager.targetFilePath = Path.Combine(Application.persistentDataPath, saveFileName + ".txt"), npcInfo);
		initMessages.Add(new ChatMessage("system", ConfigManager.npcPromptConfig.Value));
		string attributePrompt = BuildRoleplayAttributePrompt(npcInfo, characterContent);
		initMessages.Add(new ChatMessage("user", attributePrompt));
		initMessages.Add(new ChatMessage("assistant", "好的"));
		string taskMemoryContext = BuildQuestTaskMemoryContext(npcInfo);
		if (!string.IsNullOrEmpty(taskMemoryContext))
		{
			initMessages.Add(new ChatMessage("user", taskMemoryContext));
			initMessages.Add(new ChatMessage("assistant", "已读取任务往来摘要"));
		}
		int recentDialogCount = Mathf.Clamp(ConfigManager.recentDialogCount.Value, 4, 30);
		int longTermSummaryCount = Mathf.Clamp(ConfigManager.longTermSummaryCount.Value, 2, 30);
		int maxCharsPerEntry = Mathf.Clamp(ConfigManager.longTermMaxCharsPerEntry.Value, 20, 200);
		if (NPCDialog.TryBuildPromptHistory(npcInfo.NpcID, npcInfo.Name, recentDialogCount, longTermSummaryCount, maxCharsPerEntry, out var longTermSummary, out var recentDialog))
		{
			if (!string.IsNullOrEmpty(longTermSummary))
			{
				initMessages.Add(new ChatMessage("user", "以下是你与玩家早期互动的长期记忆摘要，仅供保持关系与事件一致性：\n" + longTermSummary));
				initMessages.Add(new ChatMessage("assistant", "已读取长期记忆摘要"));
			}
			if (recentDialog.Count > 0)
			{
				string recentDialogText = string.Join("\n", recentDialog);
				initMessages.Add(new ChatMessage("user", "以下为你跟玩家最近的对话，请优先参考：\n" + recentDialogText));
				initMessages.Add(new ChatMessage("assistant", "已加载最近对话,继续吧"));
			}
		}
		AIChatManager.logger.LogInfo((object)(npcInfo.Name + " 信息提取并组装Prompt完成"));
		return initMessages;
	}

	private static string BuildQuestTaskMemoryContext(NPCInfo npcInfo)
	{
		if (npcInfo == null || npcInfo.NpcID <= 0)
		{
			return string.Empty;
		}
		string taskSummary = AIQuestManager.Instance?.BuildQuestTaskMemorySummaryForPrompt(npcInfo.NpcID, npcInfo.Name);
		if (string.IsNullOrWhiteSpace(taskSummary))
		{
			return string.Empty;
		}
		return "以下是你与玩家之间近期的任务往来摘要，仅供保持委托进展与口径一致：\n" + taskSummary;
	}

	private static string JoinLimited(List<string> values, int maxCount)
	{
		if (values == null || values.Count == 0)
		{
			return "无";
		}
		int count = Mathf.Max(1, maxCount);
		List<string> selected = values.Where((string v) => !string.IsNullOrWhiteSpace(v)).Take(count).ToList();
		if (selected.Count == 0)
		{
			return "无";
		}
		if (values.Count > selected.Count)
		{
			return string.Join(", ", selected) + $" ...共{values.Count}项";
		}
		return string.Join(", ", selected);
	}
}
