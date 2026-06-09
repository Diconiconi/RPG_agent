using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using GUIPackage;
using Newtonsoft.Json;

namespace MCS_AIChatMod;

public static class NPCChatTools
{
	private sealed class FriendlyRequestToolResult
	{
		[JsonProperty("status")]
		public string Status { get; set; }

		[JsonProperty("resolved_item_name", NullValueHandling = NullValueHandling.Ignore)]
		public string ResolvedItemName { get; set; }

		[JsonProperty("count")]
		public int Count { get; set; }

		[JsonProperty("available_count", NullValueHandling = NullValueHandling.Ignore)]
		public int? AvailableCount { get; set; }

		[JsonProperty("roleplay_hint")]
		public string RoleplayHint { get; set; }
	}

	private const string ToolInstructionMarker = "[ToolRules]";

	private const string ToolFollowupInstructionMarker = "[ToolFollowupRules]";

	private const string FriendlyRequestItemToolName = "FriendlyRequestItemTool";

	private static readonly string FriendlyRequestToolInstruction = "[ToolRules]\n只有当玩家明确索要、借用或讨要某件你当前持有的具体物品时，才允许调用 FriendlyRequestItemTool。\n如果玩家表达模糊、没有明确物品名、或者只是闲聊，不要调用工具。\n如果物品名称可能对应多个物品，不要猜测，调用工具后根据返回结果要求玩家说清楚。\n工具只负责内部结算；最终回复必须继续以当前NPC的身份、语气、立场与玩家关系来自然说话。\n不要在最终回复里直接复述 status、reason、tool、json、字段名、系统提示或任何工具调用痕迹。";

	private static readonly string FriendlyRequestToolFollowupInstruction = "[ToolFollowupRules]\n你刚刚获得了一次内部工具结算结果。现在必须继续扮演当前NPC，自然回应玩家。\n不要提及工具、系统、json、状态码、字段名，也不要复述 status 或 reason。\n如果索取成功，可以自然答应、递交、叮嘱或打趣；如果拒绝，只能模糊表达交情未到、此物不便相让或暂时拿不出。\n绝对不要说出具体好感、情分或任何数值成本。";

	public static List<Tool> GetToolsForCurrentContext()
	{
		if (!TryGetCurrentRequestNpc(out var _))
		{
			return null;
		}
		return new List<Tool> { BuildFriendlyRequestItemTool() };
	}

	public static void EnsureToolInstruction(List<ChatMessage> messages)
	{
		if (messages != null)
		{
			if (GetToolsForCurrentContext() == null)
			{
				RemoveSystemInstruction(messages, "[ToolRules]");
			}
			else
			{
				UpsertSystemInstruction(messages, "[ToolRules]", FriendlyRequestToolInstruction, null);
			}
		}
	}

	public static void EnsureToolFollowupInstruction(List<ChatMessage> messages)
	{
		if (messages != null)
		{
			UpsertSystemInstruction(messages, "[ToolFollowupRules]", FriendlyRequestToolFollowupInstruction, "[ToolRules]");
		}
	}

	public static void RemoveTransientToolInstructions(List<ChatMessage> messages)
	{
		if (messages != null)
		{
			RemoveSystemInstruction(messages, "[ToolFollowupRules]");
		}
	}

	public static string ExecuteTool(string toolName, string toolArguments)
	{
		ManualLogSource logger = AIChatManager.logger;
		if (logger != null)
		{
			logger.LogInfo((object)("[NPCChatTools] ExecuteTool name=" + toolName + ", args=" + toolArguments));
		}
		Dictionary<string, object> args = ParseArguments(toolArguments);
		if (toolName == "FriendlyRequestItemTool")
		{
			return ExecuteFriendlyRequestItemTool(args);
		}
		throw new ArgumentException("Unknown tool: " + toolName);
	}

	private static Tool BuildFriendlyRequestItemTool()
	{
		Tool tool = new Tool();
		tool.Type = "function";
		tool.Function = new FunctionDefinition
		{
			Name = "FriendlyRequestItemTool",
			Description = "当玩家明确向当前交互中的NPC索要某件具体物品时调用。工具会在本地检查NPC背包、情分和好感是否足够，并返回结构化结果。",
			Parameters = new
			{
				type = "object",
				properties = new
				{
					item_name = new
					{
						type = "string",
						description = "玩家明确索要的物品名称。必须直接来自玩家的要求，不要猜测。"
					},
					count = new
					{
						type = "integer",
						description = "玩家索要的数量。省略时默认为1。",
						@default = 1,
						minimum = 1
					}
				},
				required = new string[1] { "item_name" }
			}
		};
		return tool;
	}

	private static string ExecuteFriendlyRequestItemTool(Dictionary<string, object> args)
	{
		string requestedItemName = GetStringArg(args, "item_name");
		int requestedCount = Math.Max(1, GetIntArg(args, "count", 1));
		if (!TryGetCurrentRequestNpc(out var npc))
		{
			return ReturnToolResult(null, requestedItemName, null, requestedCount, "invalid_context", "npc_interaction_not_active");
		}
		if (string.IsNullOrWhiteSpace(requestedItemName))
		{
			return ReturnToolResult(npc, requestedItemName, null, requestedCount, "ambiguous_or_missing", "missing_item_name");
		}
		try
		{
			npc.RefreshData();
		}
		catch (Exception arg)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogError((object)$"[NPCChatTools] RefreshData failed for npc {npc.ID}/{npc.Name}: {arg}");
			}
			return ReturnToolResult(npc, requestedItemName, null, requestedCount, "invalid_context", "refresh_npc_failed");
		}
		List<item> matches = FindMatchingItems(npc, requestedItemName);
		if (matches.Count != 1)
		{
			return ReturnToolResult(npc, requestedItemName, null, requestedCount, "ambiguous_or_missing", (matches.Count == 0) ? "item_not_found" : "item_name_not_unique");
		}
		item matchedItem = matches[0];
		string resolvedItemName = GetPrimaryItemName(matchedItem);
		if (matchedItem.itemNum < requestedCount)
		{
			return ReturnToolResult(npc, requestedItemName, resolvedItemName, requestedCount, "ambiguous_or_missing", "insufficient_quantity", matchedItem.itemNum);
		}
		int qingfenCost = CalculateQingFenCost(npc, matchedItem, requestedCount);
		int favorDivisor = Math.Max(1, NPCEx.CalcZengLiX(npc));
		int favorCost = Math.Max(0, qingfenCost / favorDivisor);
		int currentQingFen = NPCEx.GetQingFen(npc.ID);
		int currentFavor = NPCEx.GetFavor(npc.ID);
		int? currentQingFen2;
		int? currentFavor2;
		int? qingFenCost;
		int? favorCost2;
		if (currentQingFen < qingfenCost || currentFavor < favorCost)
		{
			UINPCData npc2 = npc;
			string detailCode = ((currentQingFen < qingfenCost) ? "qingfen_not_enough" : "favor_not_enough");
			favorCost2 = favorCost;
			qingFenCost = qingfenCost;
			currentFavor2 = currentFavor;
			currentQingFen2 = currentQingFen;
			return ReturnToolResult(npc2, requestedItemName, resolvedItemName, requestedCount, "rejected_insufficient_cost", detailCode, null, favorCost2, qingFenCost, currentFavor2, currentQingFen2);
		}
		NPCEx.AddFavor(npc.ID, -favorCost, addQingFen: false);
		NPCEx.AddQingFen(npc.ID, -qingfenCost, showTip: true);
		NPCEx.ItemToPlayerFromNPC(npc, matchedItem, requestedCount);
		try
		{
			npc.RefreshData();
			if (UINPCJiaoHu.Inst?.NowJiaoHuNPC != null && UINPCJiaoHu.Inst.NowJiaoHuNPC.ID == npc.ID)
			{
				UINPCJiaoHu.Inst.NowJiaoHuNPC.RefreshData();
			}
		}
		catch (Exception arg2)
		{
			ManualLogSource logger2 = AIChatManager.logger;
			if (logger2 != null)
			{
				logger2.LogError((object)$"[NPCChatTools] NPC refresh after transfer failed for npc {npc.ID}/{npc.Name}: {arg2}");
			}
		}
		try
		{
			NpcJieSuanManager.inst.isUpDateNpcList = true;
		}
		catch (Exception arg3)
		{
			ManualLogSource logger3 = AIChatManager.logger;
			if (logger3 != null)
			{
				logger3.LogError((object)$"[NPCChatTools] Failed to flag NPC list refresh: {arg3}");
			}
		}
		UINPCData npc3 = npc;
		currentQingFen2 = favorCost;
		currentFavor2 = qingfenCost;
		qingFenCost = currentFavor;
		favorCost2 = currentQingFen;
		return ReturnToolResult(npc3, requestedItemName, resolvedItemName, requestedCount, "success", "ok", null, currentQingFen2, currentFavor2, qingFenCost, favorCost2);
	}

	private static string ReturnToolResult(UINPCData npc, string requestedItemName, string resolvedItemName, int requestedCount, string status, string detailCode, int? availableCount = null, int? favorCost = null, int? qingFenCost = null, int? currentFavor = null, int? currentQingFen = null)
	{
		LogFriendlyRequestOutcome(npc, requestedItemName, resolvedItemName, requestedCount, status, detailCode, availableCount, favorCost, qingFenCost, currentFavor, currentQingFen);
		return SerializeResult(new FriendlyRequestToolResult
		{
			Status = status,
			ResolvedItemName = (string.IsNullOrWhiteSpace(resolvedItemName) ? null : resolvedItemName),
			Count = requestedCount,
			AvailableCount = availableCount,
			RoleplayHint = BuildRoleplayHint(status, detailCode, resolvedItemName, requestedCount, availableCount)
		});
	}

	private static string BuildRoleplayHint(string status, string detailCode, string resolvedItemName, int requestedCount, int? availableCount)
	{
		string itemLabel = (string.IsNullOrWhiteSpace(resolvedItemName) ? "这件东西" : resolvedItemName);
		switch (status)
		{
		case "success":
			return "你愿意把" + itemLabel + "交给玩家。请继续保持当前NPC的人设与语气，自然地答应、递交、叮嘱或打趣，但绝不要提及任何具体数值成本。";
		case "rejected_insufficient_cost":
			return "你不愿轻易把" + itemLabel + "相让。请只用模糊的关系语言表达交情未到、此物难舍或暂不便相让，不要说出任何具体情分或好感数值。";
		case "ambiguous_or_missing":
			switch (detailCode)
			{
			case "missing_item_name":
			case "item_name_not_unique":
				return "自然追问玩家说的是哪一件，保持角色口吻，不要像报错。";
			case "item_not_found":
				return "自然说明自己身上并没有" + itemLabel + "，不要提及内部状态。";
			case "insufficient_quantity":
				return availableCount.HasValue ? $"自然说明自己手头没有那么多{itemLabel}，只能拿得出{availableCount.Value}个左右，但不要把这句话说得像系统提示。" : ("自然说明自己拿不出那么多" + itemLabel + "。");
			}
			break;
		case "invalid_context":
			return "不要提及工具、系统或异常；如果必须回应，就像平常交谈一样自然带过，不要破坏角色扮演。";
		}
		return "继续保持角色扮演，用自然口吻回应玩家，不要暴露任何工具或系统痕迹。";
	}

	private static void LogFriendlyRequestOutcome(UINPCData npc, string requestedItemName, string resolvedItemName, int requestedCount, string status, string detailCode, int? availableCount, int? favorCost, int? qingFenCost, int? currentFavor, int? currentQingFen)
	{
		ManualLogSource logger = AIChatManager.logger;
		if (logger != null)
		{
			logger.LogInfo((object)string.Format("[NPCChatTools] FriendlyRequest outcome status={0}, detail={1}, npcId={2}, npcName={3}, requestedItem={4}, resolvedItem={5}, count={6}, available={7}, favorCost={8}, qingFenCost={9}, currentFavor={10}, currentQingFen={11}", status, detailCode, npc?.ID ?? 0, npc?.Name ?? "<null>", requestedItemName ?? string.Empty, resolvedItemName ?? string.Empty, requestedCount, availableCount?.ToString() ?? string.Empty, favorCost?.ToString() ?? string.Empty, qingFenCost?.ToString() ?? string.Empty, currentFavor?.ToString() ?? string.Empty, currentQingFen?.ToString() ?? string.Empty));
		}
	}

	private static int CalculateQingFenCost(UINPCData npc, item matchedItem, int requestedCount)
	{
		bool isLaJi;
		bool isJiXu;
		int jieGuo;
		bool highLevel;
		bool isXiHao;
		int qingfenCost = NPCEx.CalcQingFen(npc, matchedItem, requestedCount, out isLaJi, out isJiXu, out jieGuo, out highLevel, out isXiHao);
		return Math.Max(0, qingfenCost);
	}

	private static List<item> FindMatchingItems(UINPCData npc, string requestedItemName)
	{
		List<item> inventory = npc?.Inventory ?? new List<item>();
		string rawName = (requestedItemName ?? string.Empty).Trim();
		if (string.IsNullOrEmpty(rawName))
		{
			return new List<item>();
		}
		List<item> exactMatches = inventory.Where((item candidate) => GetCandidateNames(candidate).Any((string name) => string.Equals(name, rawName, StringComparison.Ordinal))).ToList();
		if (exactMatches.Count > 0)
		{
			return exactMatches;
		}
		string normalizedRequestedName = NormalizeItemName(rawName);
		if (string.IsNullOrEmpty(normalizedRequestedName))
		{
			return new List<item>();
		}
		return inventory.Where((item candidate) => GetCandidateNames(candidate).Any((string name) => NormalizeItemName(name) == normalizedRequestedName)).ToList();
	}

	private static IEnumerable<string> GetCandidateNames(item itemInfo)
	{
		if (itemInfo != null)
		{
			string primaryName = GetPrimaryItemName(itemInfo);
			if (!string.IsNullOrWhiteSpace(primaryName))
			{
				yield return primaryName;
			}
			string canonicalName = itemInfo.itemNameCN;
			if (!string.IsNullOrWhiteSpace(canonicalName) && !string.Equals(primaryName, canonicalName, StringComparison.Ordinal))
			{
				yield return canonicalName.Trim();
			}
		}
	}

	private static string GetPrimaryItemName(item itemInfo)
	{
		if (itemInfo == null)
		{
			return string.Empty;
		}
		string primaryName = itemInfo.itemName;
		if (!string.IsNullOrWhiteSpace(primaryName))
		{
			return primaryName.Trim();
		}
		return string.IsNullOrWhiteSpace(itemInfo.itemNameCN) ? string.Empty : itemInfo.itemNameCN.Trim();
	}

	private static string NormalizeItemName(string itemName)
	{
		if (string.IsNullOrWhiteSpace(itemName))
		{
			return string.Empty;
		}
		StringBuilder builder = new StringBuilder(itemName.Length);
		string text = itemName.Trim();
		foreach (char ch in text)
		{
			if (char.IsLetterOrDigit(ch))
			{
				builder.Append(char.ToLowerInvariant(ch));
			}
		}
		return builder.ToString();
	}

	private static string GetStringArg(Dictionary<string, object> args, string key)
	{
		if (args == null || string.IsNullOrEmpty(key) || !args.TryGetValue(key, out var rawValue) || rawValue == null)
		{
			return string.Empty;
		}
		return rawValue.ToString()?.Trim() ?? string.Empty;
	}

	private static int GetIntArg(Dictionary<string, object> args, string key, int defaultValue)
	{
		if (args == null || string.IsNullOrEmpty(key) || !args.TryGetValue(key, out var rawValue) || rawValue == null)
		{
			return defaultValue;
		}
		try
		{
			return Convert.ToInt32(rawValue);
		}
		catch (Exception arg)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogError((object)$"[NPCChatTools] Failed to parse int arg '{key}'='{rawValue}': {arg}");
			}
			return defaultValue;
		}
	}

	private static Dictionary<string, object> ParseArguments(string toolArguments)
	{
		if (string.IsNullOrWhiteSpace(toolArguments))
		{
			return new Dictionary<string, object>();
		}
		Dictionary<string, object> args = JsonConvert.DeserializeObject<Dictionary<string, object>>(toolArguments);
		return args ?? new Dictionary<string, object>();
	}

	private static bool TryGetCurrentRequestNpc(out UINPCData npc)
	{
		npc = null;
		if (UINPCJiaoHu.Inst?.NowJiaoHuNPC == null)
		{
			return false;
		}
		UINPCData currentNpc = UINPCJiaoHu.Inst.NowJiaoHuNPC;
		if (currentNpc == null || currentNpc.ID <= 1)
		{
			return false;
		}
		if (AIChatManager.npcInfo == null || AIChatManager.npcInfo.NpcID != currentNpc.ID)
		{
			return false;
		}
		try
		{
			if (NPCEx.IsDeath(currentNpc.ID))
			{
				return false;
			}
		}
		catch (Exception arg)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogError((object)$"[NPCChatTools] Failed to check NPC death state for {currentNpc.ID}: {arg}");
			}
			return false;
		}
		npc = currentNpc;
		return true;
	}

	private static void UpsertSystemInstruction(List<ChatMessage> messages, string marker, string content, string anchorMarker)
	{
		int existingIndex = messages.FindIndex((ChatMessage message) => message != null && string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(message.Content) && message.Content.StartsWith(marker, StringComparison.Ordinal));
		if (existingIndex >= 0)
		{
			messages[existingIndex].Content = content;
			return;
		}
		int anchorIndex = -1;
		if (!string.IsNullOrEmpty(anchorMarker))
		{
			anchorIndex = messages.FindIndex((ChatMessage message) => message != null && string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(message.Content) && message.Content.StartsWith(anchorMarker, StringComparison.Ordinal));
		}
		if (anchorIndex >= 0)
		{
			messages.Insert(anchorIndex + 1, new ChatMessage("system", content));
			return;
		}
		int firstSystemIndex = messages.FindIndex((ChatMessage message) => message != null && string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase));
		if (firstSystemIndex >= 0)
		{
			messages.Insert(firstSystemIndex + 1, new ChatMessage("system", content));
		}
		else
		{
			messages.Insert(0, new ChatMessage("system", content));
		}
	}

	private static void RemoveSystemInstruction(List<ChatMessage> messages, string marker)
	{
		for (int i = messages.Count - 1; i >= 0; i--)
		{
			ChatMessage message = messages[i];
			if (message != null && string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(message.Content) && message.Content.StartsWith(marker, StringComparison.Ordinal))
			{
				messages.RemoveAt(i);
			}
		}
	}

	private static string SerializeResult(FriendlyRequestToolResult result)
	{
		string json = JsonConvert.SerializeObject(result);
		ManualLogSource logger = AIChatManager.logger;
		if (logger != null)
		{
			logger.LogInfo((object)("[NPCChatTools] Tool result: " + json));
		}
		return json;
	}
}
