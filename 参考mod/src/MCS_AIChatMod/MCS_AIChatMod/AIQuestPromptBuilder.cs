using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using KBEngine;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCS_AIChatMod;

public static class AIQuestPromptBuilder
{
	private sealed class MainlineDialogEntry
	{
		public string Speaker;

		public string Text;
	}

	private const int MainlineBiographyHistoryLimit = 6;

	private const int MainlineDialogHistoryLimit = 3;

	internal const int MainlineRawNarrativeTargetCharacters = 3000;

	internal const int MainlineRawNarrativeMinCharacters = 2500;

	private static void LogPromptBuilderWarning(string context, Exception ex)
	{
		ManualLogSource logger = AIChatManager.logger;
		if (logger != null)
		{
			logger.LogWarning((object)$"[AIQuestPromptBuilder] {context}: {ex}");
		}
	}

	public static List<ChatMessage> BuildMainlineStoryBeatMessages(NPCInfo npcInfo, QuestElementPicker.PickedElements elements, MainlineCampaign existingCampaign = null, PendingBiographyEventState pendingEvent = null, string storyHistorySummary = null)
	{
		return new List<ChatMessage>
		{
			new ChatMessage("system", BuildMainlineRawNarrativeSystemPrompt()),
			new ChatMessage("user", BuildMainlineRawNarrativeUserPrompt(npcInfo, elements, existingCampaign, pendingEvent, storyHistorySummary))
		};
	}

	public static List<ChatMessage> BuildMainlineStructuredBeatMessages(NPCInfo npcInfo, QuestElementPicker.PickedElements elements, PendingBiographyEventState pendingEvent, MainlineRawNarrativeDraft rawNarrativeDraft)
	{
		return new List<ChatMessage>
		{
			new ChatMessage("system", "你只返回一个 JSON 对象。\n字段只能来自 user 提供的 contract.output_template。\n不要解释，不要 markdown，不要返回合同本身。"),
			new ChatMessage("user", BuildMainlineStructuredBeatUserPrompt(npcInfo, elements, pendingEvent, rawNarrativeDraft))
		};
	}

	public static List<ChatMessage> BuildMainlineBeatGamePlanningMessages(NPCInfo npcInfo, MainlineBeat beat, QuestElementPicker.BeatExecutionContext executionContext)
	{
		return new List<ChatMessage>
		{
			new ChatMessage("system", "你只返回一个 JSON 对象。\n字段只能来自 user 提供的 contract.output_template。\n不要解释，不要 markdown，不要返回合同本身。"),
			new ChatMessage("user", BuildMainlineBeatGamePlanningUserPrompt(npcInfo, beat, executionContext))
		};
	}

	public static List<ChatMessage> BuildMainlineStoryBeatRepairMessages(NPCInfo npcInfo, QuestElementPicker.PickedElements elements, MainlineCampaign existingCampaign, PendingBiographyEventState pendingEvent, string storyHistorySummary, string previousResponse, string validationError)
	{
		List<ChatMessage> messages = BuildMainlineStoryBeatMessages(npcInfo, elements, existingCampaign, pendingEvent, storyHistorySummary);
		messages.Add(new ChatMessage("user", BuildMainlineRawNarrativeRepairPrompt(validationError)));
		return messages;
	}

	public static List<ChatMessage> BuildMainlineStructuredBeatRepairMessages(NPCInfo npcInfo, QuestElementPicker.PickedElements elements, PendingBiographyEventState pendingEvent, MainlineRawNarrativeDraft rawNarrativeDraft, string previousResponse, string validationError)
	{
		List<ChatMessage> messages = BuildMainlineStructuredBeatMessages(npcInfo, elements, pendingEvent, rawNarrativeDraft);
		messages.Add(new ChatMessage("assistant", previousResponse ?? string.Empty));
		messages.Add(new ChatMessage("user", BuildMainlineContractRepairEnvelope("stage2_story_contract_repair", validationError, MainlineJsonContractStage.Stage2Story, BuildStage2StoryInput(npcInfo, elements, pendingEvent, rawNarrativeDraft))));
		return messages;
	}

	public static List<ChatMessage> BuildMainlineBeatGamePlanningRepairMessages(NPCInfo npcInfo, MainlineBeat beat, QuestElementPicker.BeatExecutionContext executionContext, string previousResponse, string validationError)
	{
		List<ChatMessage> messages = BuildMainlineBeatGamePlanningMessages(npcInfo, beat, executionContext);
		messages.Add(new ChatMessage("assistant", previousResponse ?? string.Empty));
		messages.Add(new ChatMessage("user", BuildMainlineContractRepairEnvelope("stage3_semantic_contract_repair", validationError, MainlineJsonContractStage.Stage3Semantic, BuildStage3SemanticInput(npcInfo, beat, executionContext))));
		return messages;
	}

	private static string BuildMainlineContextPrompt(NPCInfo npcInfo, PendingBiographyEventState pendingEvent)
	{
		StringBuilder sb = new StringBuilder();
		Avatar player = Tools.instance.getPlayer();
		string playerName = jsonData.instance.AvatarRandomJsonData["1"]["Name"].Str;
		string playerSex = ((jsonData.instance.AvatarJsonData["1"]["SexType"].I == 1) ? "男" : "女");
		string playerXiuWei = MCS_Converter.GetXiuWeiText(player.level - 1);
		string playerFaction = Tools.getStr("menpai" + player.menPai);
		sb.AppendLine("=== 玩家摘要 ===");
		sb.AppendLine("名字: " + playerName + " 性别: " + playerSex + " 修为: " + playerXiuWei + " 门派: " + playerFaction);
		sb.AppendLine();
		sb.AppendLine("=== 当前时间 ===");
		sb.AppendLine(player.worldTimeMag.getNowTime().ToString("yyyy年MM月"));
		sb.AppendLine();
		sb.AppendLine(BuildRelationshipPriorityBlock(npcInfo));
		sb.AppendLine();
		sb.AppendLine("=== 锚点NPC核心信息 ===");
		sb.AppendLine("名字: " + npcInfo.Name + " 性别: " + npcInfo.Gender);
		sb.AppendLine("修为: " + npcInfo.XiuWei + " 门派: " + GetNpcFaction(npcInfo.NpcID) + " 性格: " + npcInfo.XingGe + " 正邪: " + npcInfo.ZhengXie);
		sb.AppendLine($"年龄: {npcInfo.Age} 称号: {npcInfo.Title}");
		sb.AppendLine($"好感度: {npcInfo.HaoGanDu}({npcInfo.Favor})");
		sb.AppendLine();
		sb.AppendLine("=== NPC 最近关键生平 ===");
		sb.AppendLine(BuildMainlineBiographySummary(npcInfo?.Events));
		sb.AppendLine();
		sb.AppendLine("=== 与该NPC的最近关键互动 ===");
		sb.AppendLine(BuildMainlineDialogSummary(npcInfo));
		string emailSummary = BuildMainlineRelevantEmailSummary(npcInfo, pendingEvent);
		if (!string.IsNullOrWhiteSpace(emailSummary))
		{
			sb.AppendLine();
			sb.AppendLine("=== 与该NPC直接相关的近期消息摘要 ===");
			sb.AppendLine(emailSummary);
		}
		return sb.ToString().TrimEnd();
	}

	private static string BuildMainlineBiographySummary(IEnumerable<UINPCEventData> events)
	{
		List<UINPCEventData> orderedEvents = (from uINPCEventData in events ?? Enumerable.Empty<UINPCEventData>()
			where uINPCEventData != null && !string.IsNullOrWhiteSpace(uINPCEventData.EventDesc)
			orderby uINPCEventData.EventTime
			select uINPCEventData).ToList();
		if (orderedEvents.Count == 0)
		{
			return "(暂无关键生平)";
		}
		int skipped = Math.Max(0, orderedEvents.Count - 6);
		StringBuilder sb = new StringBuilder();
		foreach (UINPCEventData evt in orderedEvents.Skip(skipped))
		{
			string time = ((!string.IsNullOrWhiteSpace(evt.EventTimeStr)) ? evt.EventTimeStr : evt.EventTime.ToString("yyyy年MM月"));
			sb.AppendLine("- [" + time + "] " + evt.EventDesc);
		}
		if (skipped > 0)
		{
			sb.AppendLine($"（更早还有 {skipped} 条生平，此处省略）");
		}
		return sb.ToString().TrimEnd();
	}

	private static string BuildMainlineDialogSummary(NPCInfo npcInfo)
	{
		string safeFileName = $"{npcInfo.NpcID}_{npcInfo.Name}".Replace("/", "_");
		string filePath = Path.Combine(Application.persistentDataPath, safeFileName + ".txt");
		if (!File.Exists(filePath))
		{
			return "(暂无关键互动)";
		}
		return BuildMainlineDialogSummaryFromRawLines(File.ReadAllLines(filePath));
	}

	private static string BuildMainlineDialogSummaryFromRawLines(IEnumerable<string> rawLines)
	{
		List<MainlineDialogEntry> entries = (from mainlineDialogEntry in (rawLines ?? Enumerable.Empty<string>()).Select(ParseMainlineDialogEntry)
			where mainlineDialogEntry != null && IsMainlineDialogEntrySubstantive(mainlineDialogEntry.Text)
			select mainlineDialogEntry).ToList();
		if (entries.Count == 0)
		{
			return "(暂无关键互动)";
		}
		int skipped = Math.Max(0, entries.Count - 3);
		StringBuilder sb = new StringBuilder();
		foreach (MainlineDialogEntry entry in entries.Skip(skipped))
		{
			sb.AppendLine("- " + entry.Speaker + ": " + entry.Text);
		}
		if (skipped > 0)
		{
			sb.AppendLine($"（更早还有 {skipped} 条互动，此处省略）");
		}
		return sb.ToString().TrimEnd();
	}

	private static MainlineDialogEntry ParseMainlineDialogEntry(string rawLine)
	{
		if (string.IsNullOrWhiteSpace(rawLine))
		{
			return null;
		}
		string[] parts = rawLine.Split(new string[1] { "|||" }, 2, StringSplitOptions.None);
		if (parts.Length != 2)
		{
			return null;
		}
		string speaker = parts[0]?.Trim();
		string text = parts[1]?.Trim();
		if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		return new MainlineDialogEntry
		{
			Speaker = speaker,
			Text = text
		};
	}

	private static bool IsMainlineDialogEntrySubstantive(string text)
	{
		string normalized = NormalizeDialogText(text);
		if (normalized.Length < 4)
		{
			return false;
		}
		switch (normalized)
		{
		case "你好":
		case "您好":
		case "在吗":
		case "嗯":
		case "哦":
		case "啊":
		case "好":
		case "收到":
		case "哈哈":
		case "哈":
		case "拜拜":
		case "回头见":
		case "先这样":
			return false;
		default:
			return true;
		}
	}

	private static string NormalizeDialogText(string text)
	{
		return (text ?? string.Empty).Trim().Replace("。", string.Empty).Replace("，", string.Empty)
			.Replace("！", string.Empty)
			.Replace("？", string.Empty)
			.Replace("…", string.Empty)
			.Replace(" ", string.Empty);
	}

	private static string BuildMainlineRelevantEmailSummary(NPCInfo npcInfo, PendingBiographyEventState pendingEvent)
	{
		if (!ShouldIncludeMainlineEmailSummary(pendingEvent))
		{
			return null;
		}
		Avatar player = Tools.instance.getPlayer();
		string npcId = npcInfo.NpcID.ToString();
		List<EmailData> allEmails = new List<EmailData>();
		if (player.emailDateMag.hasReadEmailDictionary != null && player.emailDateMag.hasReadEmailDictionary.ContainsKey(npcId))
		{
			allEmails.AddRange(player.emailDateMag.hasReadEmailDictionary[npcId]);
		}
		if (player.emailDateMag.newEmailDictionary != null && player.emailDateMag.newEmailDictionary.ContainsKey(npcId))
		{
			allEmails.AddRange(player.emailDateMag.newEmailDictionary[npcId]);
		}
		List<string> summaries = new List<string>();
		foreach (EmailData email in allEmails)
		{
			if (summaries.Count >= 3)
			{
				break;
			}
			string contentText = BuildEmailContentText(email);
			if (!string.IsNullOrWhiteSpace(contentText))
			{
				string time = (string.IsNullOrWhiteSpace(email.sendTime) ? "未知时间" : email.sendTime);
				string sender = (string.IsNullOrWhiteSpace(email.npcName) ? npcInfo.Name : email.npcName);
				summaries.Add("- [" + time + "] " + sender + ": " + contentText);
			}
		}
		if (summaries.Count == 0)
		{
			return null;
		}
		return string.Join("\n", summaries);
	}

	private static bool ShouldIncludeMainlineEmailSummary(PendingBiographyEventState pendingEvent)
	{
		string text = pendingEvent?.Summary + " " + pendingEvent?.PlayerFacingHint;
		return text.Contains("传音") || text.Contains("来信") || text.Contains("消息") || text.Contains("赴约") || text.Contains("回信");
	}

	private static string BuildEmailContentText(EmailData email)
	{
		if (email?.content == null || email.content.Count == 0)
		{
			return string.Empty;
		}
		List<string> textParts = new List<string>();
		foreach (int item in email.content)
		{
			string idStr = item.ToString();
			if (jsonData.instance.CyNpcDuiBaiData.HasField(idStr))
			{
				string text = jsonData.instance.CyNpcDuiBaiData[idStr]["neirong"]?.str ?? string.Empty;
				if (!string.IsNullOrWhiteSpace(text))
				{
					textParts.Add(text);
				}
			}
		}
		return string.Join(string.Empty, textParts);
	}

	private static string BuildMainlineCampaignSummary(MainlineCampaign existingCampaign, string storyHistorySummary)
	{
		StringBuilder sb = new StringBuilder();
		if (existingCampaign == null)
		{
			sb.AppendLine("- 这是该 NPC 当前事件链的第一拍。");
		}
		else
		{
			MainlineChapter currentChapter = existingCampaign.GetCurrentChapter();
			MainlineBeat currentBeat = currentChapter?.GetCurrentBeat();
			sb.AppendLine("- 主线标题: " + existingCampaign.Title);
			if (currentChapter != null)
			{
				sb.AppendLine("- 当前章节: " + currentChapter.Title);
			}
			if (currentBeat != null)
			{
				sb.AppendLine("- 当前拍: " + currentBeat.Title);
			}
			if (!string.IsNullOrWhiteSpace(existingCampaign.Continuity?.Summary))
			{
				sb.AppendLine("- 连续性摘要: " + existingCampaign.Continuity.Summary.Trim());
			}
			List<string> recentWorldChanges = existingCampaign.Continuity?.RecentWorldChanges?.Where((string value) => !string.IsNullOrWhiteSpace(value)).ToList() ?? new List<string>();
			int recentChangeStart = Math.Max(0, recentWorldChanges.Count - 3);
			foreach (string change in recentWorldChanges.Skip(recentChangeStart))
			{
				sb.AppendLine("- 最近变化: " + change);
			}
		}
		if (!string.IsNullOrWhiteSpace(storyHistorySummary))
		{
			sb.AppendLine("- 既往剧情小结: " + storyHistorySummary.Trim());
		}
		return sb.ToString().TrimEnd();
	}

	private static string BuildMainlineRawNarrativeSystemPrompt()
	{
		return "你是觅长生世界中的主线编剧。\n你当前只返回一段完整剧情正文。\n第一句就直接进入剧情，不要用“以下”“正文如下”“设定如下”之类的提示语起头。\n不要返回 JSON。\n不要使用大括号、方括号、代码围栏、字段名、键值对、标题、小节名、项目符号或编号列表。\n不要把背景改写成列表、资料卡、设定表、提纲或摘要。\n不要解释。";
	}

	private static string BuildMainlineRawNarrativeUserPrompt(NPCInfo npcInfo, QuestElementPicker.PickedElements elements, MainlineCampaign existingCampaign, PendingBiographyEventState pendingEvent, string storyHistorySummary)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("下面这些背景只供你吸收，不要把背景改写成列表、资料卡、字段表或 JSON。");
		sb.AppendLine("第一句就直接进入剧情，不要先写标题、前言、提纲、设定说明或“正文如下”。");
		sb.AppendLine();
		sb.AppendLine(BuildMainlineNarrativeContextBrief(npcInfo, pendingEvent));
		sb.AppendLine();
		sb.AppendLine(BuildMainlineNarrativeMaterialsBrief(elements));
		sb.AppendLine();
		sb.AppendLine(BuildMainlineCampaignNarrativeSummary(existingCampaign, storyHistorySummary));
		sb.AppendLine();
		sb.AppendLine("当前拦下的官方生平事件可作为背景线索之一。" + BuildPendingBiographyNarrativeSummary(pendingEvent));
		sb.AppendLine();
		sb.AppendLine("综合利用已经选中的 NPC、场景、具体物品与这条生平线索，写成一个完整单拍故事。");
		sb.AppendLine("不要把官方生平事件当作唯一主轴；它只是你组织这次故事时可以利用的一条线索。");
		sb.AppendLine("这是单生平单拍完结的剧情，不允许再写成等待下一拍的结构。");
		sb.AppendLine("只写剧情正文，不要写 JSON，不要写提纲，不要写列表。");
		sb.AppendLine($"正文去掉空白后的长度以 {3000} 字左右为目标，最低不少于 {2500} 字。");
		sb.AppendLine("NPC、场景、具体物品都只能使用素材库里已经给出的内容。");
		sb.AppendLine("即使你知道游戏里还存在别的官方场景或官方物品，只要这次素材库没给出，也不要写。");
		sb.AppendLine("写到具体物品时，优先参考候选池里的官方介绍，再结合它的实际用途组织情节。");
		sb.AppendLine("必须出现明确选择。");
		sb.AppendLine("每个分支都要写出独立后续。");
		sb.AppendLine("分支彼此独立，不汇合。");
		sb.AppendLine("每个分支都必须在这一拍内结束。");
		sb.AppendLine("如果当前拍需要见 NPC、回报 NPC 或交付 NPC，系统会负责让 NPC 到位；你只负责写戏和段落顺序。");
		sb.AppendLine("如果剧情需要物品，只能使用候选池里已有的具体物品名，不要发明新的物品名。");
		sb.AppendLine("时间只按短期 / 中期 / 长期的语义氛围把握，不要写具体日期。");
		sb.AppendLine("对话必须符合修仙界尊卑、门派、修为差距与暗流涌动的氛围。");
		sb.AppendLine("允许半文言，但不要堆古文腔。");
		sb.AppendLine("不要解释，不要总结，不要补充正文之外的内容。");
		return sb.ToString().TrimEnd();
	}

	private static string BuildMainlineNarrativeContextBrief(NPCInfo npcInfo, PendingBiographyEventState pendingEvent)
	{
		string playerName = "玩家";
		string playerSex = "未知";
		string playerXiuWei = "未知";
		string playerFaction = "散修";
		string currentTime = "未知时日";
		try
		{
			Avatar player = Tools.instance.getPlayer();
			playerName = jsonData.instance.AvatarRandomJsonData["1"]["Name"].Str;
			playerSex = ((jsonData.instance.AvatarJsonData["1"]["SexType"].I == 1) ? "男" : "女");
			playerXiuWei = MCS_Converter.GetXiuWeiText(player.level - 1);
			playerFaction = Tools.getStr("menpai" + player.menPai);
			currentTime = player.worldTimeMag.getNowTime().ToString("yyyy年MM月");
		}
		catch (Exception ex)
		{
			LogPromptBuilderWarning("BuildMainlineNarrativeContextBrief", ex);
		}
		string npcName = (string.IsNullOrWhiteSpace(npcInfo?.Name) ? "该NPC" : npcInfo.Name.Trim());
		string npcFaction = ((npcInfo == null) ? string.Empty : GetNpcFaction(npcInfo.NpcID));
		string relationshipBrief = FlattenNarrativePromptBlock(BuildRelationshipPriorityBlock(npcInfo));
		string biographyBrief = FlattenNarrativePromptBlock(BuildMainlineBiographySummary(npcInfo?.Events));
		string dialogBrief = FlattenNarrativePromptBlock(BuildMainlineDialogSummary(npcInfo));
		string emailBrief = FlattenNarrativePromptBlock(BuildMainlineRelevantEmailSummary(npcInfo, pendingEvent));
		List<string> list = new List<string>();
		list.Add("当前玩家名叫" + playerName + "，性别" + playerSex + "，修为" + playerXiuWei + "，门派" + playerFaction + "，当前时间在" + currentTime + "。");
		list.Add(npcName + "是这次主线的锚点人物，其性别为" + (npcInfo?.Gender ?? "未知") + "，修为为" + (npcInfo?.XiuWei ?? "未知") + "，门派为" + npcFaction + "，性格为" + (npcInfo?.XingGe ?? "未知") + "，正邪倾向为" + (npcInfo?.ZhengXie ?? "未知") + "，年龄约" + ((npcInfo == null) ? "未知" : npcInfo.Age.ToString()) + "，称号为" + (npcInfo?.Title ?? "无") + "，当前好感为" + (npcInfo?.HaoGanDu ?? "未知") + "（" + ((npcInfo == null) ? "未知" : npcInfo.Favor.ToString()) + "）。");
		List<string> paragraphs = list;
		if (!string.IsNullOrWhiteSpace(relationshipBrief))
		{
			paragraphs.Add("人物关系方面可直接参考这段既有判断：" + relationshipBrief + "。");
		}
		if (!string.IsNullOrWhiteSpace(biographyBrief))
		{
			paragraphs.Add(npcName + "最近的关键生平包括：" + biographyBrief + "。");
		}
		if (!string.IsNullOrWhiteSpace(dialogBrief))
		{
			paragraphs.Add("你与" + npcName + "最近的关键互动包括：" + dialogBrief + "。");
		}
		if (!string.IsNullOrWhiteSpace(emailBrief))
		{
			paragraphs.Add("与" + npcName + "直接相关的近期消息包括：" + emailBrief + "。");
		}
		return string.Join("\n", paragraphs.Where((string text) => !string.IsNullOrWhiteSpace(text)));
	}

	private static string BuildMainlineNarrativeMaterialsBrief(QuestElementPicker.PickedElements elements)
	{
		QuestElementPicker.NarrativeMaterialContext context = elements?.NarrativeContext ?? new QuestElementPicker.NarrativeMaterialContext();
		List<string> paragraphs = new List<string>();
		if (context.CandidateNpcs.Count > 0)
		{
			string npcText = string.Join("；", context.CandidateNpcs.Select(delegate(QuestElementPicker.PickedNpc npc)
			{
				string text = (string.IsNullOrWhiteSpace(npc.Relation) ? string.Empty : ("，与你的关系是" + npc.Relation));
				string text2 = (string.IsNullOrWhiteSpace(npc.Faction) ? string.Empty : ("，所属势力为" + npc.Faction));
				string text3 = (string.IsNullOrWhiteSpace(npc.XiuWei) ? string.Empty : ("，修为约为" + npc.XiuWei));
				return npc.Name + text + text2 + text3;
			}));
			paragraphs.Add("可调度进戏的 NPC 有这些：" + npcText + "。");
		}
		if (context.CandidateLocations.Count > 0)
		{
			string locationText = string.Join("、", from location in context.CandidateLocations
				select location.Name into name
				where !string.IsNullOrWhiteSpace(name)
				select name);
			if (!string.IsNullOrWhiteSpace(locationText))
			{
				paragraphs.Add("可用场景有这些：" + locationText + "。");
			}
		}
		if (context.ConcreteStoryItems.Count > 0)
		{
			string itemText = string.Join("；", context.ConcreteStoryItems.Select((QuestElementPicker.NarrativeConcreteItemCandidate item) => BuildNarrativeConcreteItemPromptText(item)));
			paragraphs.Add("如果剧情需要具体物品，只能从这些现成物品里选：" + itemText + "。写戏时优先参考它们的官方介绍，再结合实际用途。");
		}
		if (context.TimeCandidates.Count > 0)
		{
			string timeText = string.Join("；", context.TimeCandidates.Select((QuestElementPicker.TimeCandidate time) => time.Label + "（" + time.Summary + "）"));
			paragraphs.Add("时间氛围只从这些意图中取：" + timeText + "。");
		}
		paragraphs.Add("请综合利用这些 NPC、场景、具体物品与时间意图来组织故事。除了这里列出的素材，不要额外发明新的人名、地名或物品名。具体物品名只能从候选池中选择。");
		return string.Join("\n", paragraphs.Where((string text) => !string.IsNullOrWhiteSpace(text)));
	}

	private static string BuildNarrativeConcreteItemPromptText(QuestElementPicker.NarrativeConcreteItemCandidate item)
	{
		if (item == null)
		{
			return string.Empty;
		}
		List<string> fragments = new List<string>
		{
			$"品阶{item.Quality}",
			"稀缺度" + item.Scarcity
		};
		if (!string.IsNullOrWhiteSpace(item.OfficialDescription))
		{
			fragments.Add("官方介绍" + item.OfficialDescription.Trim());
		}
		if (!string.IsNullOrWhiteSpace(item.Effect))
		{
			fragments.Add("用途" + item.Effect.Trim());
		}
		return item.Name + "（" + string.Join("，", fragments) + "）";
	}

	private static string BuildMainlineCampaignNarrativeSummary(MainlineCampaign existingCampaign, string storyHistorySummary)
	{
		List<string> paragraphs = new List<string>();
		if (existingCampaign == null)
		{
			paragraphs.Add("这是该 NPC 当前事件链的第一拍。");
		}
		else
		{
			MainlineChapter currentChapter = existingCampaign.GetCurrentChapter();
			MainlineBeat currentBeat = currentChapter?.GetCurrentBeat();
			paragraphs.Add("当前事件链属于《" + existingCampaign.Title + "》。");
			if (currentChapter != null)
			{
				paragraphs.Add("当前章节是《" + currentChapter.Title + "》。");
			}
			if (currentBeat != null)
			{
				paragraphs.Add("当前拍是《" + currentBeat.Title + "》。");
			}
			if (!string.IsNullOrWhiteSpace(existingCampaign.Continuity?.Summary))
			{
				paragraphs.Add("连续性摘要是：" + existingCampaign.Continuity.Summary.Trim() + "。");
			}
			List<string> recentWorldChanges = existingCampaign.Continuity?.RecentWorldChanges?.Where((string change) => !string.IsNullOrWhiteSpace(change)).ToList() ?? new List<string>();
			int recentChangeStart = Math.Max(0, recentWorldChanges.Count - 3);
			if (recentWorldChanges.Count > 0)
			{
				paragraphs.Add("最近发生过这些变化：" + string.Join("；", from change in recentWorldChanges.Skip(recentChangeStart)
					select change.Trim()) + "。");
			}
		}
		if (!string.IsNullOrWhiteSpace(storyHistorySummary))
		{
			paragraphs.Add("既往剧情小结是：" + storyHistorySummary.Trim() + "。");
		}
		return string.Join("\n", paragraphs.Where((string text) => !string.IsNullOrWhiteSpace(text)));
	}

	private static string FlattenNarrativePromptBlock(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		List<string> fragments = (from line in (from line in text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
				select line.Trim() into line
				where !string.IsNullOrWhiteSpace(line)
				select line).Select(delegate(string line)
			{
				string text2 = line;
				if (text2.StartsWith("=== ", StringComparison.Ordinal) && text2.EndsWith(" ===", StringComparison.Ordinal))
				{
					text2 = text2.Substring(4, text2.Length - 8).Trim();
				}
				text2 = text2.TrimStart('-', '*', ' ');
				text2 = text2.Replace("【", string.Empty).Replace("】", string.Empty);
				return text2.Trim();
			})
			where !string.IsNullOrWhiteSpace(line)
			select line).ToList();
		return string.Join("；", fragments);
	}

	private static string BuildPendingBiographyNarrativeSummary(PendingBiographyEventState pendingEvent)
	{
		if (pendingEvent == null)
		{
			return "目前暂无被拦截的官方生平事件。";
		}
		List<string> fragments = new List<string>();
		if (!string.IsNullOrWhiteSpace(pendingEvent.NpcName))
		{
			fragments.Add("当事人是" + pendingEvent.NpcName.Trim());
		}
		string kindText = DescribePendingBiographyEventKind(pendingEvent.BiographyEventKind);
		if (!string.IsNullOrWhiteSpace(kindText))
		{
			fragments.Add("事件类型是" + kindText);
		}
		if (!string.IsNullOrWhiteSpace(pendingEvent.Summary))
		{
			fragments.Add("事件本身是：" + pendingEvent.Summary.Trim());
		}
		if (!string.IsNullOrWhiteSpace(pendingEvent.PlayerFacingHint))
		{
			fragments.Add("玩家可感知到的线索是：" + pendingEvent.PlayerFacingHint.Trim());
		}
		if (!string.IsNullOrWhiteSpace(pendingEvent.CapturedAtGameTime))
		{
			fragments.Add("触发时间在" + pendingEvent.CapturedAtGameTime.Trim());
		}
		return (fragments.Count == 0) ? "目前暂无被拦截的官方生平事件。" : (string.Join("；", fragments) + "。");
	}

	private static string DescribePendingBiographyEventKind(BiographyEventKind eventKind)
	{
		switch (eventKind)
		{
		case BiographyEventKind.QiYu:
			return "奇遇";
		case BiographyEventKind.ImprotantEvent:
			return "重要事件";
		case BiographyEventKind.UseDanYao:
			return "服丹";
		case BiographyEventKind.SmallTuPo:
			return "小突破";
		case BiographyEventKind.LianDan:
			return "炼丹";
		case BiographyEventKind.LianQi:
			return "炼器";
		case BiographyEventKind.ZhuJiSuccess:
			return "筑基";
		case BiographyEventKind.JinDanSuccess:
			return "结丹";
		case BiographyEventKind.YuanYingSuccess:
			return "元婴";
		case BiographyEventKind.HuaShenSuccess:
			return "化神";
		case BiographyEventKind.LunDaoSuccess:
			return "论道成功";
		case BiographyEventKind.LunDaoFail:
			return "论道失败";
		case BiographyEventKind.PaiMai:
			return "拍卖";
		case BiographyEventKind.JieShaSuccess:
			return "截杀成功";
		case BiographyEventKind.JieShaFail1:
		case BiographyEventKind.JieShaFail2:
			return "截杀失败";
		case BiographyEventKind.FanShaSuccess:
			return "反杀成功";
		case BiographyEventKind.FanShaFail1:
		case BiographyEventKind.FanShaFail2:
			return "反杀失败";
		case BiographyEventKind.TianJiDaBi:
			return "天机大比";
		default:
			return string.Empty;
		}
	}

	private static string BuildMainlineRawNarrativeRepairPrompt(string validationError)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("上一版出现了结构化输出，请完全忽略那种写法，重新写一版完整剧情正文。");
		sb.AppendLine("错误原因：" + validationError);
		sb.AppendLine("第一句就直接进入剧情，不要先写标题、前言、提纲、设定说明或“正文如下”。");
		sb.AppendLine("不要使用大括号、方括号、代码围栏、字段名、键值对、标题、小节名、项目符号或编号列表。");
		sb.AppendLine("不要把背景改写成列表、资料卡、字段表、提纲或摘要。");
		sb.AppendLine("仍然只返回纯剧情文本，不要 JSON，不要列表，不要解释。");
		sb.AppendLine($"正文去掉空白后的长度以 {3000} 字左右为目标，最低不少于 {2500} 字。");
		sb.AppendLine("只能使用素材库中已经给出的 NPC、场景、具体物品；不要自行补充素材库外的名字。");
		sb.AppendLine("具体物品要优先按候选池里的官方介绍来写，不要把官方介绍改写成功能标签列表。");
		sb.AppendLine("必须包含明确选择，分支独立，不汇合，每个分支都在本拍内结束。");
		return sb.ToString();
	}

	private static string BuildMainlineBeatGamePlanningUserPrompt(NPCInfo npcInfo, MainlineBeat beat, QuestElementPicker.BeatExecutionContext executionContext)
	{
		return BuildMainlineContractEnvelope("stage3_semantic_contract", "把结构化剧情拍整理成语义原语计划", MainlineJsonContractStage.Stage3Semantic, BuildStage3SemanticInput(npcInfo, beat, executionContext));
	}

	private static string BuildMainlineStructuredBeatUserPrompt(NPCInfo npcInfo, QuestElementPicker.PickedElements elements, PendingBiographyEventState pendingEvent, MainlineRawNarrativeDraft rawNarrativeDraft)
	{
		return BuildMainlineContractEnvelope("stage2_story_contract", "把纯剧情正文整理成结构化剧情拍", MainlineJsonContractStage.Stage2Story, BuildStage2StoryInput(npcInfo, elements, pendingEvent, rawNarrativeDraft));
	}

	private static string BuildMainlineContractEnvelope(string stageId, string goal, MainlineJsonContractStage stage, JObject input)
	{
		JObject envelope = new JObject
		{
			["stage"] = stageId,
			["goal"] = goal,
			["input"] = input ?? new JObject(),
			["contract"] = BuildPromptContractJson(BuildMainlineJsonContract(stage))
		};
		return envelope.ToString();
	}

	private static string BuildMainlineContractRepairEnvelope(string stageId, string validationError, MainlineJsonContractStage stage, JObject input)
	{
		JObject envelope = new JObject
		{
			["stage"] = stageId,
			["goal"] = "修正上一版输出，使之满足合同",
			["validationError"] = validationError ?? string.Empty,
			["repair_requirements"] = new JObject
			{
				["change_only_to_satisfy_contract"] = true,
				["preserve_valid_content"] = true,
				["forbid_extra_fields"] = true,
				["omit_unused_optional_fields"] = true,
				["forbid_empty_property_names"] = true
			},
			["input"] = input ?? new JObject(),
			["contract"] = BuildPromptContractJson(BuildMainlineJsonContract(stage))
		};
		return envelope.ToString();
	}

	private static JObject BuildStage2StoryInput(NPCInfo npcInfo, QuestElementPicker.PickedElements elements, PendingBiographyEventState pendingEvent, MainlineRawNarrativeDraft rawNarrativeDraft)
	{
		return new JObject
		{
			["contextText"] = BuildMainlineContextPrompt(npcInfo, pendingEvent),
			["pendingBiographyEvent"] = BuildPendingBiographyPromptInput(pendingEvent),
			["rawNarrative"] = rawNarrativeDraft?.RawText ?? string.Empty,
			["legalMaterials"] = BuildStage2LegalMaterialsPayload(npcInfo, elements)
		};
	}

	private static JObject BuildStage3SemanticInput(NPCInfo npcInfo, MainlineBeat beat, QuestElementPicker.BeatExecutionContext executionContext)
	{
		return new JObject
		{
			["contextText"] = BuildMainlineContextPrompt(npcInfo, null),
			["structuredBeat"] = BuildStructuredBeatPayload(beat),
			["legalMaterials"] = BuildStage3LegalMaterialsPayload(executionContext)
		};
	}

	private static JObject BuildStage3LegalMaterialsPayload(QuestElementPicker.BeatExecutionContext executionContext)
	{
		return new JObject
		{
			["npcNames"] = new JArray((from npc in executionContext?.AllowedNpcs ?? new List<QuestElementPicker.PickedNpc>()
				where npc != null && !string.IsNullOrWhiteSpace(npc.Name)
				select npc.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)),
			["sceneNames"] = new JArray((from location in executionContext?.AllowedLocations ?? new List<QuestElementPicker.PickedLocation>()
				where location != null && !string.IsNullOrWhiteSpace(location.Name)
				select location.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)),
			["items"] = new JArray(from item in executionContext?.ConcreteItems ?? new List<QuestElementPicker.ConcreteItemOption>()
				where item != null && !string.IsNullOrWhiteSpace(item.Name)
				select new JObject
				{
					["name"] = item.Name.Trim(),
					["effect"] = item.Effect ?? string.Empty,
					["quality"] = item.Quality
				})
		};
	}

	private static JObject BuildStage2LegalMaterialsPayload(NPCInfo anchorNpcInfo, QuestElementPicker.PickedElements elements)
	{
		HashSet<string> npcNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (!string.IsNullOrWhiteSpace(anchorNpcInfo?.Name))
		{
			npcNames.Add(anchorNpcInfo.Name.Trim());
		}
		foreach (string name in from npc in elements?.NarrativeContext?.CandidateNpcs ?? new List<QuestElementPicker.PickedNpc>()
			where npc != null && !string.IsNullOrWhiteSpace(npc.Name)
			select npc.Name.Trim())
		{
			npcNames.Add(name);
		}
		foreach (string name2 in from npc in elements?.Npcs ?? new List<QuestElementPicker.PickedNpc>()
			where npc != null && !string.IsNullOrWhiteSpace(npc.Name)
			select npc.Name.Trim())
		{
			npcNames.Add(name2);
		}
		HashSet<string> sceneNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (string name3 in from location in elements?.NarrativeContext?.CandidateLocations ?? new List<QuestElementPicker.PickedLocation>()
			where location != null && !string.IsNullOrWhiteSpace(location.Name)
			select location.Name.Trim())
		{
			sceneNames.Add(name3);
		}
		foreach (string name4 in from location in elements?.Locations ?? new List<QuestElementPicker.PickedLocation>()
			where location != null && !string.IsNullOrWhiteSpace(location.Name)
			select location.Name.Trim())
		{
			sceneNames.Add(name4);
		}
		return new JObject
		{
			["npcNames"] = new JArray(npcNames),
			["sceneNames"] = new JArray(sceneNames),
			["items"] = new JArray(from item in elements?.NarrativeContext?.ConcreteStoryItems ?? new List<QuestElementPicker.NarrativeConcreteItemCandidate>()
				where item != null && !string.IsNullOrWhiteSpace(item.Name)
				select new JObject
				{
					["name"] = item.Name.Trim(),
					["officialDescription"] = item.OfficialDescription ?? string.Empty,
					["effect"] = item.Effect ?? string.Empty,
					["quality"] = item.Quality
				})
		};
	}

	private static JObject BuildPendingBiographyPromptInput(PendingBiographyEventState pendingEvent)
	{
		if (pendingEvent == null)
		{
			return new JObject();
		}
		return new JObject
		{
			["npcName"] = pendingEvent.NpcName ?? string.Empty,
			["kind"] = pendingEvent.BiographyEventKind.ToString(),
			["summary"] = pendingEvent.Summary ?? string.Empty,
			["playerFacingHint"] = pendingEvent.PlayerFacingHint ?? string.Empty,
			["capturedAtGameTime"] = pendingEvent.CapturedAtGameTime ?? string.Empty
		};
	}

	private static JObject BuildStructuredBeatPayload(MainlineBeat beat)
	{
		if (beat == null)
		{
			return new JObject();
		}
		JObject jObject = new JObject();
		jObject["title"] = beat.Title ?? string.Empty;
		jObject["summary"] = beat.Summary ?? string.Empty;
		jObject["timeCategory"] = beat.TimeIntent?.TimeBand ?? string.Empty;
		jObject["scriptBlocks"] = ((beat.StoryDraft?.ScriptBlocks == null) ? new JArray() : new JArray(beat.StoryDraft.ScriptBlocks.Select(BuildPromptScriptBlockPayload)));
		return jObject;
	}

	private static JObject BuildPromptScriptBlockPayload(MainlineScriptBlock block)
	{
		if (block == null)
		{
			return new JObject();
		}
		JObject blockObj = new JObject
		{
			["blockRef"] = block.BlockRef ?? string.Empty,
			["kind"] = ToPromptScriptBlockKind(block.Kind),
			["sceneName"] = block.SceneName ?? string.Empty,
			["scenePurpose"] = block.ScenePurpose ?? string.Empty,
			["participantNpcNames"] = new JArray((block.ParticipantNpcNames ?? new List<string>()).Where((string name) => !string.IsNullOrWhiteSpace(name))),
			["lines"] = new JArray((block.Lines ?? new List<AIQuestStoryLine>()).Select((AIQuestStoryLine line) => BuildPromptStoryLinePayload(line, block)))
		};
		if (block.Kind == MainlineScriptBlockKind.Choice)
		{
			blockObj["choices"] = new JArray((block.Choices ?? new List<MainlineDialogueChoiceDraft>()).Select((MainlineDialogueChoiceDraft choice) => new JObject
			{
				["choiceRef"] = choice?.ChoiceRef ?? string.Empty,
				["text"] = choice?.Text ?? string.Empty,
				["consequenceDirection"] = choice?.ConsequenceDirection ?? string.Empty
			}));
		}
		if (!string.IsNullOrWhiteSpace(block.BranchRef))
		{
			blockObj["branchRef"] = block.BranchRef.Trim();
		}
		return blockObj;
	}

	private static string BuildPromptStoryLinePayload(AIQuestStoryLine line, MainlineScriptBlock block)
	{
		if (line == null)
		{
			return string.Empty;
		}
		string speakerType = ToPromptSpeakerType(line.SpeakerType);
		string speakerName = string.Empty;
		if (line.SpeakerType == QuestStorySpeakerType.Npc)
		{
			speakerName = ((line.SpeakerNpcId > 0) ? QuestElementPicker.GetNpcDisplayName(line.SpeakerNpcId) : (block?.ParticipantNpcNames?.FirstOrDefault((string name) => !string.IsNullOrWhiteSpace(name)) ?? string.Empty));
		}
		return EscapeCompactStoryLineSegment(speakerType) + "|" + EscapeCompactStoryLineSegment(speakerName) + "|" + EscapeCompactStoryLineSegment(line.Text ?? string.Empty);
	}

	private static string EscapeCompactStoryLineSegment(string value)
	{
		return (value ?? string.Empty).Replace("\\", "\\\\").Replace("|", "\\|");
	}

	private static string ToPromptSpeakerType(QuestStorySpeakerType speakerType)
	{
		return speakerType switch
		{
			QuestStorySpeakerType.Npc => "npc",
			QuestStorySpeakerType.Player => "player",
			QuestStorySpeakerType.Narrator => "narrator",
			_ => throw new InvalidOperationException($"未实现的剧情说话人类型: {speakerType}"),
		};
	}

	private static string ToPromptScriptBlockKind(MainlineScriptBlockKind kind)
	{
		return kind switch
		{
			MainlineScriptBlockKind.Encounter => "encounter",
			MainlineScriptBlockKind.Dialogue => "dialogue",
			MainlineScriptBlockKind.Choice => "choice",
			MainlineScriptBlockKind.Fallout => "fallout",
			MainlineScriptBlockKind.DeliveryPrompt => "delivery_prompt",
			MainlineScriptBlockKind.CombatPrelude => "combat_prelude",
			_ => throw new InvalidOperationException($"未实现的剧情块类型: {kind}"),
		};
	}

	private static MainlineJsonContract BuildMainlineJsonContract(MainlineJsonContractStage stage)
	{
		return stage switch
		{
			MainlineJsonContractStage.Stage2Story => BuildStage2StoryContract(),
			MainlineJsonContractStage.Stage3Semantic => BuildStage3SemanticContract(),
			_ => throw new InvalidOperationException($"未实现的主线合同阶段: {stage}"),
		};
	}

	internal static MainlineJsonContract BuildMainlineJsonContractForTest(MainlineJsonContractStage stage)
	{
		return BuildMainlineJsonContract(stage);
	}

	private static MainlineJsonContract BuildStage2StoryContract()
	{
		MainlineJsonContract mainlineJsonContract = new MainlineJsonContract();
		mainlineJsonContract.ContractId = "stage2_story_contract";
		mainlineJsonContract.Goal = "从纯剧情正文提取结构化剧情拍；不补写剧情，不扩写分支，不允许汇合";
		mainlineJsonContract.FieldContracts = new List<MainlineJsonFieldContract>
		{
			CreateStringField("title", required: true, "填写剧情标题，只概括 input.rawNarrative 中已经写出的故事"),
			CreateStringField("summary", required: true, "填写剧情概要，只概括 input.rawNarrative 中已经写出的故事"),
			CreateEnumField("timeCategory", true, "填写时间类别", "短期", "中期", "长期"),
			CreateScriptBlocksField()
		};
		mainlineJsonContract.Rules = new List<MainlineJsonRuleContract>
		{
			new MainlineJsonRuleContract
			{
				RuleId = "single_main_choice_block",
				Scope = "scriptBlocks",
				Requirement = "只能有一个主 choice block",
				ErrorMessage = "scriptBlocks 只能有一个主 choice block"
			},
			new MainlineJsonRuleContract
			{
				RuleId = "branch_blocks_must_have_choice_ids",
				Scope = "scriptBlocks",
				Condition = "after_choice",
				Requirement = "choice 之后的所有 scriptBlocks 都必须带 branchRef",
				ErrorMessage = "choice 之后的所有 scriptBlocks 都必须带 branchRef"
			},
			new MainlineJsonRuleContract
			{
				RuleId = "no_public_blocks_after_choice",
				Scope = "scriptBlocks",
				Condition = "after_choice",
				Requirement = "choice 之后不允许公共 block",
				ErrorMessage = "choice 之后不允许公共 block"
			},
			new MainlineJsonRuleContract
			{
				RuleId = "non_choice_blocks_forbid_choices",
				Scope = "scriptBlocks",
				Requirement = "只有 kind=choice 的 script block 才允许 choices",
				ErrorMessage = "只有 kind=choice 的 script block 才允许 choices"
			},
			new MainlineJsonRuleContract
			{
				RuleId = "names_must_come_from_legal_materials",
				Scope = "scriptBlocks",
				Requirement = "sceneName、participantNpcNames 与 lines 中出现的具体物品名只能从 input.legalMaterials 中选择",
				ErrorMessage = "sceneName、participantNpcNames 与 lines 中出现的具体物品名只能从 input.legalMaterials 中选择"
			}
		};
		mainlineJsonContract.OutputShell = new JObject
		{
			["title"] = "<剧情标题>",
			["summary"] = "<剧情概要>",
			["timeCategory"] = "<短期|中期|长期>",
			["scriptBlocks"] = new JArray
			{
				new JObject
				{
					["blockRef"] = "<choice 之前的剧情块引用名，由模型自定>",
					["kind"] = "<从允许的剧情块类型中选择一种；这是主干剧情块>",
					["sceneName"] = "<从剧情中提取的场景名称>",
					["scenePurpose"] = "<这一段的戏剧作用>",
					["participantNpcNames"] = new JArray("<参与此段的NPC名称，可省略>"),
					["lines"] = new JArray { "narrator||<主干叙述>", "npc|<NPC名称>|<主干对白>" }
				},
				new JObject
				{
					["blockRef"] = "<主 choice 剧情块引用名，由模型自定>",
					["kind"] = "choice",
					["sceneName"] = "<choice 发生的场景名称>",
					["scenePurpose"] = "<choice 这一段的戏剧作用>",
					["participantNpcNames"] = new JArray("<参与此段的NPC名称，可省略>"),
					["lines"] = new JArray { "narrator||<choice 出现前的叙述>", "npc|<NPC名称>|<choice 出现前的对白>" },
					["choices"] = new JArray
					{
						new JObject
						{
							["choiceRef"] = "<仅 kind=choice 时填写唯一分支引用名，由模型自定>",
							["text"] = "<仅 kind=choice 时填写分支选择文本>",
							["consequenceDirection"] = "<仅 kind=choice 时填写该分支的后续方向，可省略>"
						}
					}
				},
				new JObject
				{
					["blockRef"] = "<choice 之后的分支剧情块引用名，由模型自定>",
					["kind"] = "<从允许的剧情块类型中选择一种；这是分支后续块>",
					["sceneName"] = "<该分支发生的场景名称>",
					["scenePurpose"] = "<这一分支后续块的戏剧作用>",
					["participantNpcNames"] = new JArray("<参与此段的NPC名称，可省略>"),
					["lines"] = new JArray { "narrator||<该分支后续的叙述>", "npc|<NPC名称>|<该分支后续的对白>" },
					["branchRef"] = "<该分支所属的 choiceRef>"
				}
			}
		};
		return mainlineJsonContract;
	}

	private static MainlineJsonContract BuildStage3SemanticContract()
	{
		return new MainlineJsonContract
		{
			ContractId = "stage3_semantic_contract",
			Goal = "从结构化剧情拍提取语义原语计划；不改剧情，不发明 script block，不让分支汇合",
			FieldContracts = new List<MainlineJsonFieldContract> { CreateFlowStepsField() },
			Rules = new List<MainlineJsonRuleContract>
			{
				new MainlineJsonRuleContract
				{
					RuleId = "present_choice_requires_branch_only_followups",
					Scope = "flowSteps",
					Condition = "after_present_choice",
					Requirement = "present_choice 之后所有 flowSteps 都必须带 branchRef",
					ErrorMessage = "present_choice 之后所有 flowSteps 都必须带 branchRef"
				},
				new MainlineJsonRuleContract
				{
					RuleId = "each_choice_requires_own_followup_chain",
					Scope = "flowSteps",
					Requirement = "每个 choice 都必须有独立后续链",
					ErrorMessage = "每个 choice 都必须有独立后续链"
				},
				new MainlineJsonRuleContract
				{
					RuleId = "no_public_flow_after_choice",
					Scope = "flowSteps",
					Condition = "after_present_choice",
					Requirement = "choice 之后不允许公共主干步骤",
					ErrorMessage = "choice 之后不允许公共主干步骤"
				},
				new MainlineJsonRuleContract
				{
					RuleId = "script_block_references_must_exist",
					Scope = "flowSteps",
					Requirement = "引用的 sourceBlockRef 必须存在，present_choice 必须引用 choice block，play_script_block 不能引用 choice block",
					ErrorMessage = "引用的 sourceBlockRef 必须存在，present_choice 必须引用 choice block，play_script_block 不能引用 choice block"
				},
				new MainlineJsonRuleContract
				{
					RuleId = "names_must_come_from_legal_materials",
					Scope = "flowSteps",
					Requirement = "sceneName、targetNpcName 与物品相关 payload.itemName 只能从 input.legalMaterials 中选择",
					ErrorMessage = "sceneName、targetNpcName 与物品相关 payload.itemName 只能从 input.legalMaterials 中选择"
				}
			},
			OutputShell = new JObject { ["flowSteps"] = new JArray
			{
				new JObject
				{
					["stepRef"] = "<choice 之前的步骤引用名，由模型自定>",
					["kind"] = "<从允许的语义原语类型中选择一种；这是主干步骤>",
					["sourceBlockRef"] = "<需要引用剧情块时填写对应 blockRef，可省略>",
					["sceneName"] = "<需要场景时填写场景名称，可省略>",
					["targetNpcName"] = "<需要目标 NPC 时填写目标 NPC 名称，可省略>"
				},
				new JObject
				{
					["stepRef"] = "<present_choice 步骤引用名，由模型自定>",
					["kind"] = "present_choice",
					["sourceBlockRef"] = "<主 choice 剧情块对应的 blockRef>"
				},
				new JObject
				{
					["stepRef"] = "<choice 之后的分支步骤引用名，由模型自定>",
					["kind"] = "<从允许的语义原语类型中选择一种；这是分支后续步骤>",
					["sourceBlockRef"] = "<需要引用剧情块时填写对应 blockRef，可省略>",
					["sceneName"] = "<需要场景时填写场景名称，可省略>",
					["targetNpcName"] = "<需要目标 NPC 时填写目标 NPC 名称，可省略>",
					["branchRef"] = "<该步骤所属的 choiceRef>",
					["payload"] = new JObject
					{
						["itemName"] = "<仅物品相关步骤填写具体物品名，可省略>",
						["count"] = "<仅物品相关步骤填写数量，可省略>"
					},
					["effects"] = new JArray
					{
						new JObject
						{
							["effectRef"] = "<该分支步骤完成后产生的效果引用名，由模型自定>",
							["kind"] = "<favor_change|grant_item|grant_money>",
							["targetNpcName"] = "<需要目标 NPC 时填写目标 NPC 名称，可省略>",
							["payload"] = new JObject
							{
								["amount"] = "<仅好感变化时填写数值，可省略>",
								["itemName"] = "<仅发放物品时填写具体物品名，可省略>",
								["count"] = "<仅发放物品时填写数量，可省略>",
								["money"] = "<仅发放灵石时填写数值，可省略>"
							}
						}
					}
				}
			} }
		};
	}

	private static MainlineJsonFieldContract CreateStringField(string name, bool required, string requirement)
	{
		return new MainlineJsonFieldContract
		{
			Name = name,
			Type = "string",
			Required = required,
			ContentRequirement = requirement
		};
	}

	private static MainlineJsonFieldContract CreateEnumField(string name, bool required, string requirement, params string[] allowedValues)
	{
		return new MainlineJsonFieldContract
		{
			Name = name,
			Type = "string",
			Required = required,
			ContentRequirement = requirement,
			AllowedValues = (allowedValues?.ToList() ?? new List<string>())
		};
	}

	private static MainlineJsonFieldContract CreateIntField(string name, bool required, string requirement)
	{
		return new MainlineJsonFieldContract
		{
			Name = name,
			Type = "integer",
			Required = required,
			ContentRequirement = requirement
		};
	}

	private static MainlineJsonFieldContract CreateStringArrayField(string name, bool required, string requirement)
	{
		return new MainlineJsonFieldContract
		{
			Name = name,
			Type = "array",
			Required = required,
			Multiple = true,
			ContentRequirement = requirement,
			ItemContract = new MainlineJsonFieldContract
			{
				Type = "string"
			}
		};
	}

	private static MainlineJsonFieldContract CreateIntArrayField(string name, bool required, string requirement)
	{
		return new MainlineJsonFieldContract
		{
			Name = name,
			Type = "array",
			Required = required,
			Multiple = true,
			ContentRequirement = requirement,
			ItemContract = new MainlineJsonFieldContract
			{
				Type = "integer"
			}
		};
	}

	private static MainlineJsonFieldContract CreateCompactStoryLinesField()
	{
		return new MainlineJsonFieldContract
		{
			Name = "lines",
			Type = "array",
			Required = true,
			Multiple = true,
			ContentRequirement = "填写当前 block 的正文对白，每一行都必须是 <speakerType>|<speakerName>|<text>；speakerType 内部按小写归一，推荐直接使用 npc / player / narrator；未涉及的 speakerName 留空，不要填 null 或空对象；如果 text 中出现具体物品名，只能使用 input.legalMaterials.items 中列出的名字",
			ItemContract = new MainlineJsonFieldContract
			{
				Type = "string"
			}
		};
	}

	private static MainlineJsonFieldContract CreateChoiceField()
	{
		return new MainlineJsonFieldContract
		{
			Type = "object",
			FieldContracts = new List<MainlineJsonFieldContract>
			{
				ConfigureField(CreateStringField("choiceRef", required: true, "填写唯一 choiceRef"), null, null, null, unique: true),
				CreateStringField("text", required: true, "填写选择文本"),
				CreateStringField("consequenceDirection", required: false, "填写这一分支的后续方向；合法但未用到可省略")
			}
		};
	}

	private static MainlineJsonFieldContract CreateScriptBlocksField()
	{
		MainlineJsonFieldContract mainlineJsonFieldContract = new MainlineJsonFieldContract();
		mainlineJsonFieldContract.Name = "scriptBlocks";
		mainlineJsonFieldContract.Type = "array";
		mainlineJsonFieldContract.Required = true;
		mainlineJsonFieldContract.Multiple = true;
		mainlineJsonFieldContract.ContentRequirement = "按正文顺序整理剧情块";
		mainlineJsonFieldContract.ItemContract = new MainlineJsonFieldContract
		{
			Type = "object",
			FieldContracts = new List<MainlineJsonFieldContract>
			{
				ConfigureField(CreateStringField("blockRef", required: true, "填写唯一 blockRef"), null, null, null, unique: true),
				CreateEnumField("kind", true, "填写 encounter / dialogue / choice / fallout / delivery_prompt / combat_prelude", "encounter", "dialogue", "choice", "fallout", "delivery_prompt", "combat_prelude"),
				CreateStringField("sceneName", required: true, "只能填写 input.legalMaterials.sceneNames 中列出的场景名称"),
				CreateStringField("scenePurpose", required: true, "填写这一段的戏剧作用，但不要发明 input.rawNarrative 中不存在的新事件"),
				CreateStringArrayField("participantNpcNames", required: false, "只能填写 input.legalMaterials.npcNames 中列出的 NPC 名称；合法但未用到可省略"),
				ConfigureField(CreateCompactStoryLinesField()),
				new MainlineJsonFieldContract
				{
					Name = "choices",
					Type = "array",
					RequiredWhen = new List<string> { "kind=choice" },
					ForbiddenWhen = new List<string> { "kind!=choice" },
					Multiple = true,
					ContentRequirement = "仅 kind=choice 时填写非空 choices",
					ItemContract = CreateChoiceField()
				},
				CreateStringField("branchRef", required: false, "仅 choice 之后的分支 block 填写所属 choiceRef；合法但未用到可省略")
			}
		};
		return mainlineJsonFieldContract;
	}

	private static MainlineJsonFieldContract CreatePayloadField(string name, string requirement)
	{
		return new MainlineJsonFieldContract
		{
			Name = name,
			Type = "object",
			Required = false,
			ContentRequirement = requirement,
			FieldContracts = new List<MainlineJsonFieldContract>
			{
				CreateStringField("itemName", required: false, "仅物品相关步骤或效果填写具体物品名；只能填写 input.legalMaterials.items 中列出的物品名"),
				ConfigureField(CreateIntField("count", required: false, "仅物品相关步骤或效果填写数量；合法但未填时默认 1"), null, null, "1"),
				CreateIntField("amount", required: false, "仅好感变化时填写数值"),
				CreateIntField("money", required: false, "仅发放灵石时填写数值")
			}
		};
	}

	private static MainlineJsonFieldContract CreateInlineEffectsField()
	{
		MainlineJsonFieldContract mainlineJsonFieldContract = new MainlineJsonFieldContract();
		mainlineJsonFieldContract.Name = "effects";
		mainlineJsonFieldContract.Type = "array";
		mainlineJsonFieldContract.Required = false;
		mainlineJsonFieldContract.Multiple = true;
		mainlineJsonFieldContract.ContentRequirement = "仅当前步骤完成后确有后果时填写；合法但未用到可省略";
		mainlineJsonFieldContract.ItemContract = new MainlineJsonFieldContract
		{
			Type = "object",
			FieldContracts = new List<MainlineJsonFieldContract>
			{
				ConfigureField(CreateStringField("effectRef", required: true, "填写唯一 effectRef"), null, null, null, unique: true),
				CreateEnumField("kind", true, "填写 effect kind", "favor_change", "grant_item", "grant_money"),
				ConfigureField(CreateStringField("targetNpcName", required: false, "需要目标 NPC 时填写目标 NPC 名称"), new string[1] { "kind=favor_change" }),
				CreatePayloadField("payload", "仅该 effect 涉及参数时填写；未涉及就完全省略")
			}
		};
		return mainlineJsonFieldContract;
	}

	private static MainlineJsonFieldContract CreateFlowStepsField()
	{
		MainlineJsonFieldContract mainlineJsonFieldContract = new MainlineJsonFieldContract();
		mainlineJsonFieldContract.Name = "flowSteps";
		mainlineJsonFieldContract.Type = "array";
		mainlineJsonFieldContract.Required = true;
		mainlineJsonFieldContract.Multiple = true;
		mainlineJsonFieldContract.ContentRequirement = "按实际执行顺序填写语义原语步骤";
		mainlineJsonFieldContract.ItemContract = new MainlineJsonFieldContract
		{
			Type = "object",
			FieldContracts = new List<MainlineJsonFieldContract>
			{
				ConfigureField(CreateStringField("stepRef", required: true, "填写唯一 stepRef"), null, null, null, unique: true),
				CreateEnumField("kind", true, "填写语义原语 kind", "encounter_at_scene", "visit_scene_gate", "formal_talk_gate", "report_back_in_person", "play_script_block", "present_choice", "acquire_item_gate", "deliver_item_in_person", "combat_gate", "scene_transition"),
				ConfigureField(CreateStringField("sourceBlockRef", required: false, "kind=play_script_block 或 present_choice 时填写；只能引用 input.structuredBeat 里已有的 blockRef"), new string[2] { "kind=play_script_block", "kind=present_choice" }),
				ConfigureField(CreateStringField("sceneName", required: false, "需要场景时填写场景名称；只能填写 input.legalMaterials.sceneNames 中列出的场景名称"), new string[7] { "kind=encounter_at_scene", "kind=visit_scene_gate", "kind=formal_talk_gate", "kind=report_back_in_person", "kind=deliver_item_in_person", "kind=combat_gate", "kind=scene_transition" }),
				ConfigureField(CreateStringField("targetNpcName", required: false, "需要目标 NPC 时填写目标 NPC 名称；只能填写 input.legalMaterials.npcNames 中列出的 NPC 名称"), new string[5] { "kind=encounter_at_scene", "kind=formal_talk_gate", "kind=report_back_in_person", "kind=deliver_item_in_person", "kind=combat_gate" }),
				CreateStringField("branchRef", required: false, "choice 分支后续步骤填写所属 choiceRef；合法但未用到可省略"),
				CreatePayloadField("payload", "仅该步骤涉及资源类参数时填写；未涉及就完全省略"),
				CreateInlineEffectsField()
			}
		};
		return mainlineJsonFieldContract;
	}

	private static MainlineJsonFieldContract ConfigureField(MainlineJsonFieldContract field, IEnumerable<string> requiredWhen = null, IEnumerable<string> forbiddenWhen = null, string defaultWhenOmitted = null, bool unique = false)
	{
		if (field == null)
		{
			return null;
		}
		field.Unique = unique;
		if (requiredWhen != null)
		{
			field.RequiredWhen = requiredWhen.ToList();
		}
		if (forbiddenWhen != null)
		{
			field.ForbiddenWhen = forbiddenWhen.ToList();
		}
		if (!string.IsNullOrWhiteSpace(defaultWhenOmitted))
		{
			field.DefaultWhenOmitted = defaultWhenOmitted;
		}
		return field;
	}

	private static JObject BuildPromptContractJson(MainlineJsonContract contract)
	{
		if (contract == null)
		{
			return new JObject();
		}
		JObject jObject = new JObject();
		jObject["contract_id"] = contract.ContractId ?? string.Empty;
		jObject["goal"] = contract.Goal ?? string.Empty;
		jObject["response_requirements"] = new JObject
		{
			["return_single_json_object"] = true,
			["use_only_output_shell_fields"] = true,
			["use_only_input_materials"] = true,
			["omit_unused_optional_fields"] = true,
			["forbid_null_values"] = true,
			["forbid_empty_string_placeholders"] = true,
			["forbid_placeholder_empty_arrays"] = true,
			["forbid_empty_property_names"] = true
		};
		jObject["field_contracts"] = new JArray((contract.FieldContracts ?? new List<MainlineJsonFieldContract>()).Select(BuildPromptFieldContractJson));
		jObject["rules"] = new JArray((contract.Rules ?? new List<MainlineJsonRuleContract>()).Select(BuildPromptRuleContractJson));
		jObject["output_shell"] = contract.OutputShell?.DeepClone() ?? new JObject();
		return jObject;
	}

	private static JObject BuildPromptFieldContractJson(MainlineJsonFieldContract field)
	{
		if (field == null)
		{
			return new JObject();
		}
		JObject obj = new JObject
		{
			["name"] = field.Name ?? string.Empty,
			["type"] = field.Type ?? string.Empty,
			["required"] = field.Required,
			["multiple"] = field.Multiple,
			["unique"] = field.Unique,
			["content_requirement"] = field.ContentRequirement ?? string.Empty
		};
		if (field.AllowedValues != null && field.AllowedValues.Count > 0)
		{
			obj["allowed_values"] = new JArray(field.AllowedValues);
		}
		if (field.RequiredWhen != null && field.RequiredWhen.Count > 0)
		{
			obj["required_when"] = new JArray(field.RequiredWhen);
		}
		if (field.ForbiddenWhen != null && field.ForbiddenWhen.Count > 0)
		{
			obj["forbidden_when"] = new JArray(field.ForbiddenWhen);
		}
		if (!string.IsNullOrWhiteSpace(field.DefaultWhenOmitted))
		{
			obj["default_when_omitted"] = field.DefaultWhenOmitted;
		}
		if (field.FieldContracts != null && field.FieldContracts.Count > 0)
		{
			obj["field_contracts"] = new JArray(field.FieldContracts.Select(BuildPromptFieldContractJson));
		}
		if (field.ItemContract != null)
		{
			obj["item_contract"] = BuildPromptFieldContractJson(field.ItemContract);
		}
		return obj;
	}

	private static JObject BuildPromptRuleContractJson(MainlineJsonRuleContract rule)
	{
		if (rule == null)
		{
			return new JObject();
		}
		return new JObject
		{
			["rule_id"] = rule.RuleId ?? string.Empty,
			["scope"] = rule.Scope ?? string.Empty,
			["condition"] = rule.Condition ?? string.Empty,
			["requirement"] = rule.Requirement ?? string.Empty,
			["error_message"] = rule.ErrorMessage ?? string.Empty
		};
	}

	internal static string BuildMainlineBiographySummaryForTest(IEnumerable<UINPCEventData> events)
	{
		return BuildMainlineBiographySummary(events);
	}

	internal static string BuildMainlineDialogSummaryForTest(IEnumerable<string> rawLines)
	{
		return BuildMainlineDialogSummaryFromRawLines(rawLines);
	}

	private static string BuildRelationshipPriorityBlock(NPCInfo npcInfo)
	{
		string playerName = "玩家";
		string playerSex = "未知";
		string playerXiuWei = "未知";
		string playerFaction = "散修";
		string playerIdentity = "散修修士";
		string visibilitySummary = "你目前仅能稳定确认玩家的姓名、性别与门派背景；真实修为与正邪立场未显式掌握。";
		try
		{
			Avatar player = Tools.instance?.getPlayer();
			int playerShenShi = player?.GetBaseShenShi() ?? 0;
			playerName = jsonData.instance.AvatarRandomJsonData["1"]["Name"].Str;
			playerSex = ((jsonData.instance.AvatarJsonData["1"]["SexType"].I == 1) ? "男" : "女");
			playerXiuWei = ((player != null) ? MCS_Converter.GetXiuWeiText(player.level - 1) : "未知");
			playerFaction = ((player != null) ? Tools.getStr("menpai" + player.menPai) : "散修");
			if (string.IsNullOrWhiteSpace(playerFaction))
			{
				playerFaction = "散修";
			}
			playerIdentity = ((playerFaction == "散修") ? "散修修士" : (playerFaction + "修士"));
			visibilitySummary = ((npcInfo != null && npcInfo.ShenShi > 0 && npcInfo.ShenShi > playerShenShi) ? ("你已知晓玩家的门派与修为，修为约为" + playerXiuWei + "；正邪立场暂未显式掌握。") : "你已知晓玩家的姓名、性别与门派背景；真实修为与正邪立场暂未显式掌握。");
		}
		catch (Exception ex)
		{
			LogPromptBuilderWarning("BuildRelationshipPriorityBlock", ex);
		}
		return PromptBuilder.BuildRelationshipPriorityBlock(playerName, playerSex, playerXiuWei, playerFaction, playerIdentity, ResolveNpcPlayerRelationshipForPrompt(npcInfo?.NpcID ?? 0), visibilitySummary);
	}

	internal static string BuildRelationshipPriorityBlockForTest(string playerName, string playerSex, string playerCultivation, string playerFaction, string playerIdentity, string npcRelationship, string visibilitySummary)
	{
		return PromptBuilder.BuildRelationshipPriorityBlock(playerName, playerSex, playerCultivation, playerFaction, playerIdentity, npcRelationship, visibilitySummary);
	}

	internal static string ResolveNpcPlayerRelationshipForPrompt(int npcId)
	{
		return GetNpcPlayerRelationship(npcId);
	}

	private static string GetNpcFaction(int npcId)
	{
		try
		{
			JSONObject npcData = NpcJieSuanManager.inst?.GetNpcData(npcId);
			if (npcData != null && npcData.HasField("MenPai"))
			{
				int menPaiId = npcData["MenPai"].I;
				if (menPaiId > 0)
				{
					return Tools.getStr("menpai" + menPaiId);
				}
			}
		}
		catch (Exception ex)
		{
			LogPromptBuilderWarning("ResolveNpcFaction", ex);
		}
		return "散修";
	}

	private static string GetNpcPlayerRelationship(int npcId)
	{
		try
		{
			JSONObject npcData = NpcJieSuanManager.inst?.GetNpcData(npcId);
			if (npcData == null)
			{
				return "";
			}
			if (npcData.HasField("ShiFu") && npcData["ShiFu"]?.Str == "1")
			{
				return "NPC是玩家的徒弟";
			}
			if (npcData.HasField("TuDi") && npcData["TuDi"] != null && npcData["TuDi"].IsArray)
			{
				foreach (JSONObject item in npcData["TuDi"].list)
				{
					if (item?.Str == "1")
					{
						return "NPC是玩家的师傅";
					}
				}
			}
			if (npcData.HasField("DaoLvID") && npcData["DaoLvID"]?.Str == "1")
			{
				return "NPC是玩家的道侣";
			}
			Avatar player = Tools.instance?.getPlayer();
			if (player != null && npcData.HasField("MenPai") && npcData["MenPai"].I == player.menPai && player.menPai > 0)
			{
				return "同门";
			}
		}
		catch (Exception ex)
		{
			LogPromptBuilderWarning("ResolveSceneName", ex);
		}
		return "";
	}
}
