using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using KBEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace MCS_AIChatMod;

public class NPCDialog : MonoBehaviour
{
	private class DialogEntry
	{
		public string speaker;

		public string message;

		public string gameTimeText;

		public string tokenUsageText;

		[JsonConverter(typeof(StringEnumConverter))]
		public QuestStorySpeakerType? speakerType;
	}

	public class DialogHistoryEntryView
	{
		public string Speaker;

		public string Message;

		public string GameTimeText;

		public string TokenUsageText;

		public QuestStorySpeakerType SpeakerType;
	}

	internal sealed class DialogHistorySummary
	{
		public int NpcId;

		public string NpcName;

		public int MessageCount;

		public DateTime LastGameDate;

		public string LastGameTimeText;
	}

	public const string LegacyDefaultGameTimeText = "0001年01月01日";

	private List<DialogEntry> dialogHistory = new List<DialogEntry>();

	public NPCInfo npcInfo;

	private List<ChatMessage> Messages;

	private Dictionary<int, List<ChatMessage>> requests;

	public static NPCDialog Instance { get; private set; }

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			AIChatManager.logger.LogInfo((object)"NPCDialog 单例已创建");
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		else
		{
			AIChatManager.logger.LogInfo((object)"NPCDialog 单例已存在，销毁当前实例！");
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	public void StartDialogWithNPC(string npcId, string npcName)
	{
		if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(npcName))
		{
			AIChatManager.logger.LogError((object)"NPC ID 或名称为空，无法启动对话！");
			return;
		}
		npcInfo = AIChatManager.npcInfo;
		Messages = AIChatManager.Messages;
		requests = AIChatManager.requests;
		if (npcInfo != null)
		{
			npcInfo.Name = npcName;
			if (int.TryParse(npcId, out var parsedId))
			{
				npcInfo.NpcID = parsedId;
			}
		}
		EnsureDialogHistoryFile();
		if (UIManager.Instance != null && UIManager.Instance.IsChatPanelVisible())
		{
			UIManager.Instance.ShowChatPanel(npcName);
		}
		if (npcInfo != null)
		{
			AIQuestManager.Instance?.NotifyTalkToNpc(npcInfo.NpcID);
		}
	}

	public async void OnUISendMessage(string inputText)
	{
		if (string.IsNullOrEmpty(inputText))
		{
			return;
		}
		DialogEntry playerEntry = AddDialogEntry("你", inputText, null, QuestStorySpeakerType.Player);
		UIManager.Instance?.AppendMessage("你", inputText, isPlayer: true, playerEntry.gameTimeText, playerEntry.tokenUsageText, QuestStorySpeakerType.Player);
		UIManager.Instance?.SetInputText("正在思考...");
		UIManager.Instance?.SetInputInteractable(interactable: false);
		try
		{
			string aiResponse = await AIChatManager.GetAIResponseAsync(inputText);
			string speakerName = npcInfo?.Name ?? "NPC";
			int currentNpcId = npcInfo?.NpcID ?? 0;
			DialogEffectMvp.ParsedReply parsedReply = DialogEffectMvp.ParseReply(aiResponse);
			aiResponse = parsedReply.VisibleText;
			string tokenUsageText = AIChatManager.BuildLastChatRoundTokenUsageText();
			DialogEntry npcEntry = AddDialogEntry(speakerName, aiResponse, tokenUsageText, QuestStorySpeakerType.Npc);
			UIManager.Instance?.AppendMessage(speakerName, aiResponse, isPlayer: false, npcEntry.gameTimeText, npcEntry.tokenUsageText, QuestStorySpeakerType.Npc);
			DialogEffectMvp.Effect effect = DialogEffectMvp.ResolveEffectForApplication(inputText, aiResponse, parsedReply.Effect);
			bool shouldTriggerBattle = DialogEffectMvp.ShouldTriggerBattleAfterDialogForDialog(currentNpcId, aiResponse, effect);
			int appliedDelta;
			bool applied = shouldTriggerBattle
				? DialogEffectMvp.TryApplyWithoutFeedback(currentNpcId, speakerName, effect, out appliedDelta)
				: DialogEffectMvp.TryApply(currentNpcId, speakerName, effect, out appliedDelta);
			if (applied && shouldTriggerBattle)
			{
				DialogEffectMvp.TryTriggerBattleAfterDialog(currentNpcId, speakerName, aiResponse, effect, appliedDelta);
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			string errorMsg = "出错了: " + ex2.Message;
			UIManager.Instance?.AppendMessage("系统", errorMsg, isPlayer: false);
			AIChatManager.logger.LogError((object)$"[NPCDialog] SendMessage error: {ex2}");
		}
		finally
		{
			UIManager.Instance?.SetInputText("");
			UIManager.Instance?.SetInputInteractable(interactable: true);
		}
	}

	public void ClearHistory()
	{
		if (npcInfo == null)
		{
			return;
		}
		foreach (string filePath in GetDialogHistoryCandidatePaths(Application.persistentDataPath, npcInfo.NpcID))
		{
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
				AIChatManager.logger.LogInfo((object)("已删除历史记录文件: " + filePath));
			}
		}
		dialogHistory.Clear();
		if (requests != null && requests.ContainsKey(npcInfo.NpcID))
		{
			requests.Remove(npcInfo.NpcID);
		}
		List<ChatMessage> rebuiltMessages = PromptBuilder.ResolveInitialContext(npcInfo) ?? new List<ChatMessage>();
		if (requests != null)
		{
			requests[npcInfo.NpcID] = rebuiltMessages;
		}
		AIChatManager.Messages = rebuiltMessages;
		Messages = rebuiltMessages;
		AIChatManager.logger.LogInfo((object)"已清空对话历史，并重建当前 NPC 的基础上下文");
	}

	internal static bool IsCharacterCardFilePath(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			return false;
		}
		string fileName = Path.GetFileNameWithoutExtension(filePath);
		return fileName.EndsWith("_character", StringComparison.OrdinalIgnoreCase);
	}

	internal static bool IsDialogHistoryFilePath(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			return false;
		}
		if (!string.Equals(Path.GetExtension(filePath), ".txt", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (IsCharacterCardFilePath(filePath))
		{
			return false;
		}
		string fileName = Path.GetFileNameWithoutExtension(filePath);
		int firstUnderscore = fileName.IndexOf('_');
		if (firstUnderscore <= 0 || firstUnderscore >= fileName.Length - 1)
		{
			return false;
		}
		int npcId;
		return int.TryParse(fileName.Substring(0, firstUnderscore), out npcId) && npcId > 0;
	}

	internal static string NormalizeDialogHistoryFilesForTest(string persistentDataRoot, int npcId, string npcName)
	{
		return NormalizeDialogHistoryFiles(persistentDataRoot, npcId, npcName, null);
	}

	internal static IReadOnlyList<DialogHistorySummary> BuildDialogHistorySummariesForTest(string persistentDataRoot, Func<int, string> nameResolver = null)
	{
		return BuildDialogHistorySummaries(persistentDataRoot, nameResolver);
	}

	internal static IReadOnlyList<DialogHistorySummary> BuildDialogHistorySummaries(string persistentDataRoot, Func<int, string> nameResolver = null)
	{
		List<DialogHistorySummary> summaries = new List<DialogHistorySummary>();
		if (string.IsNullOrWhiteSpace(persistentDataRoot) || !Directory.Exists(persistentDataRoot))
		{
			return summaries;
		}
		int npcId2;
		string npcName;
		List<int> npcIds = (from filePath in Directory.GetFiles(persistentDataRoot, "*.txt", SearchOption.TopDirectoryOnly).Where(IsDialogHistoryFilePath)
			select TryParseDialogHistoryFilePath(filePath, out npcId2, out npcName) ? npcId2 : (-1) into num
			where num > 0
			select num).Distinct().ToList();
		foreach (int npcId in npcIds)
		{
			List<string> candidatePaths = GetDialogHistoryCandidatePaths(persistentDataRoot, npcId);
			if (candidatePaths.Count == 0)
			{
				continue;
			}
			string resolvedName = ResolveCanonicalNpcName(npcId, candidatePaths.Select((string path) => TryParseDialogHistoryFilePath(path, out npcId2, out npcName) ? npcName : string.Empty), nameResolver);
			string canonicalPath = NormalizeDialogHistoryFiles(persistentDataRoot, npcId, resolvedName, nameResolver);
			string[] historyLines = ReadDialogHistoryLines(canonicalPath);
			if (historyLines.Length != 0)
			{
				string lastGameTimeText = GetLastDialogGameTimeText(historyLines);
				if (!TryParseGameDate(lastGameTimeText, out var lastGameDate))
				{
					lastGameDate = new DateTime(1, 1, 1);
				}
				summaries.Add(new DialogHistorySummary
				{
					NpcId = npcId,
					NpcName = resolvedName,
					MessageCount = historyLines.Length,
					LastGameDate = lastGameDate,
					LastGameTimeText = lastGameTimeText
				});
			}
		}
		return summaries;
	}

	internal static LegacyDataWipeSummary DeleteDialogHistoryFilesPreserveCharacterCards(string persistentDataRoot, LegacyDataWipeSummary summary = null)
	{
		summary = summary ?? new LegacyDataWipeSummary();
		if (string.IsNullOrWhiteSpace(persistentDataRoot) || !Directory.Exists(persistentDataRoot))
		{
			return summary;
		}
		string[] rootTxtFiles = Directory.GetFiles(persistentDataRoot, "*.txt", SearchOption.TopDirectoryOnly);
		summary.PreservedCharacterCardFiles = rootTxtFiles.Count(IsCharacterCardFilePath);
		foreach (string filePath in rootTxtFiles.Where(IsDialogHistoryFilePath))
		{
			try
			{
				File.Delete(filePath);
				summary.DeletedDialogHistoryFiles++;
			}
			catch (Exception arg)
			{
				ManualLogSource logger = AIChatManager.logger;
				if (logger != null)
				{
					logger.LogWarning((object)$"[NPCDialog] Delete dialog history failed: {filePath} | {arg}");
				}
				summary.AddError("删除对话历史失败: " + Path.GetFileName(filePath));
			}
		}
		return summary;
	}

	internal void ResetConversationAfterGlobalHistoryWipe()
	{
		dialogHistory.Clear();
		requests = AIChatManager.requests ?? new Dictionary<int, List<ChatMessage>>();
		AIChatManager.requests = requests;
		List<ChatMessage> rebuiltMessages = new List<ChatMessage>();
		if (npcInfo != null && npcInfo.NpcID > 0 && !string.IsNullOrWhiteSpace(npcInfo.Name))
		{
			rebuiltMessages = PromptBuilder.ResolveInitialContext(npcInfo) ?? new List<ChatMessage>();
			requests[npcInfo.NpcID] = rebuiltMessages;
		}
		AIChatManager.Messages = rebuiltMessages;
		Messages = rebuiltMessages;
	}

	public void OpenCharacterCard()
	{
		try
		{
			string filePath = GetCharacterCardPath();
			if (string.IsNullOrEmpty(filePath))
			{
				AIChatManager.logger.LogWarning((object)"[角色卡] 无法生成路径：尚未与 NPC 对话");
				return;
			}
			if (!File.Exists(filePath))
			{
				TextEncodingUtil.WriteAllTextUtf8(filePath, "空白角色卡");
				AIChatManager.logger.LogInfo((object)("[角色卡] 已创建新文件: " + filePath));
			}
			AIChatManager.targetFilePath = filePath;
			Process.Start(new ProcessStartInfo
			{
				FileName = filePath,
				UseShellExecute = true
			});
			AIChatManager.logger.LogInfo((object)("[角色卡] 已打开: " + filePath));
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogError((object)("[角色卡] 出错: " + ex.Message));
		}
	}

	public string GetCharacterCardPath()
	{
		if (!string.IsNullOrEmpty(AIChatManager.targetFilePath))
		{
			return AIChatManager.targetFilePath;
		}
		if (npcInfo != null && !string.IsNullOrEmpty(npcInfo.Name))
		{
			string safeName = $"{npcInfo.NpcID}_{npcInfo.Name}_character".Replace("/", "_");
			return Path.Combine(Application.persistentDataPath, safeName + ".txt");
		}
		return null;
	}

	private void EnsureDialogHistoryFile()
	{
		if (npcInfo != null && !string.IsNullOrEmpty(npcInfo.Name))
		{
			EnsureDialogHistoryFile(npcInfo.NpcID, npcInfo.Name);
		}
	}

	private void EnsureDialogHistoryFile(int npcId, string npcName)
	{
		if (npcId <= 0 || string.IsNullOrEmpty(npcName))
		{
			return;
		}
		try
		{
			string filePath = NormalizeDialogHistoryFiles(Application.persistentDataPath, npcId, npcName, null);
			string directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}
			if (!File.Exists(filePath))
			{
				TextEncodingUtil.WriteAllTextUtf8(filePath, string.Empty);
			}
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogWarning((object)("[NPCDialog] EnsureDialogHistoryFile failed: " + ex.Message));
		}
	}

	private string GetDialogHistoryPath(int npcId, string npcName)
	{
		return GetDialogHistoryPath(Application.persistentDataPath, npcId, npcName);
	}

	public void AppendMessageToNpcHistory(int npcId, string npcName, string speaker, string message, bool isPlayer, bool showInUi = true, string tokenUsageText = null, QuestStorySpeakerType? speakerType = null)
	{
		if (npcId <= 0 || string.IsNullOrEmpty(npcName) || string.IsNullOrEmpty(speaker) || string.IsNullOrEmpty(message))
		{
			return;
		}
		try
		{
			EnsureDialogHistoryFile(npcId, npcName);
			DialogEntry entry = CreateDialogEntry(speaker, message, tokenUsageText, speakerType, isPlayer);
			SaveDialogEntry(npcId, npcName, entry);
			SyncNpcRequestHistory(npcId, npcName, message, isPlayer);
			if (AIChatManager.npcInfo != null && AIChatManager.npcInfo.NpcID == npcId)
			{
				dialogHistory.Add(entry);
				if (showInUi)
				{
					UIManager.Instance?.AppendMessage(speaker, message, isPlayer, entry.gameTimeText, entry.tokenUsageText, GetSpeakerType(entry, speaker, isPlayer));
				}
			}
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogWarning((object)("[NPCDialog] AppendMessageToNpcHistory failed: " + ex.Message));
		}
	}

	private void SyncNpcRequestHistory(int npcId, string npcName, string message, bool isPlayer)
	{
		try
		{
			if (!AIChatManager.requests.TryGetValue(npcId, out var npcMessages) || npcMessages == null)
			{
				NPCInfo targetNpcInfo = NpcDataExtractor.ExtractFromGameMemory(npcId);
				if (targetNpcInfo == null)
				{
					targetNpcInfo = new NPCInfo();
				}
				targetNpcInfo.NpcID = npcId;
				if (string.IsNullOrEmpty(targetNpcInfo.Name))
				{
					targetNpcInfo.Name = npcName;
				}
				npcMessages = PromptBuilder.ResolveInitialContext(targetNpcInfo) ?? new List<ChatMessage>();
				AIChatManager.requests[npcId] = npcMessages;
			}
			npcMessages.Add(new ChatMessage(isPlayer ? "user" : "assistant", message));
			if (AIChatManager.npcInfo != null && AIChatManager.npcInfo.NpcID == npcId)
			{
				AIChatManager.Messages = npcMessages;
			}
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogWarning((object)("[NPCDialog] SyncNpcRequestHistory failed: " + ex.Message));
		}
	}

	private DialogEntry AddDialogEntry(string speaker, string message, string tokenUsageText = null, QuestStorySpeakerType? speakerType = null, bool isPlayer = false)
	{
		DialogEntry entry = CreateDialogEntry(speaker, message, tokenUsageText, speakerType, isPlayer);
		dialogHistory.Add(entry);
		SaveDialogEntry(entry);
		return entry;
	}

	public bool LoadDialogHistory()
	{
		if (npcInfo == null)
		{
			return false;
		}
		string filePath = NormalizeDialogHistoryFiles(Application.persistentDataPath, npcInfo.NpcID, npcInfo.Name, null);
		AIChatManager.logger.LogInfo((object)("尝试加载对话历史文件: " + filePath));
		if (File.Exists(filePath))
		{
			dialogHistory = LoadDialogHistoryEntries(Application.persistentDataPath, npcInfo.NpcID, npcInfo.Name);
			AIChatManager.logger.LogInfo((object)$"成功加载对话历史，共 {dialogHistory.Count} 条记录");
			return dialogHistory.Count > 0;
		}
		AIChatManager.logger.LogInfo((object)("对话历史文件不存在，正在创建新文件: " + filePath));
		TextEncodingUtil.WriteAllTextUtf8(filePath, string.Empty);
		return false;
	}

	private void SaveDialogEntry(DialogEntry entry)
	{
		if (npcInfo != null)
		{
			SaveDialogEntry(npcInfo.NpcID, npcInfo.Name, entry);
		}
	}

	public List<string> GetDialogHistory()
	{
		return dialogHistory.Select((DialogEntry entry) => entry.speaker + ": " + entry.message).ToList();
	}

	public List<string> GetRecentDialogHistory(int count)
	{
		return GetRecentDialogHistory(dialogHistory, count);
	}

	public string BuildLongTermMemorySummary(int keepRecentCount, int maxSummaryEntries, int maxCharsPerEntry = 60)
	{
		return BuildLongTermMemorySummary(dialogHistory, keepRecentCount, maxSummaryEntries, maxCharsPerEntry);
	}

	internal static bool TryBuildPromptHistory(int npcId, string npcName, int recentDialogCount, int longTermSummaryCount, int maxCharsPerEntry, out string longTermSummary, out List<string> recentDialog)
	{
		List<DialogEntry> historyEntries = LoadDialogHistoryEntries(Application.persistentDataPath, npcId, npcName);
		longTermSummary = BuildLongTermMemorySummary(historyEntries, recentDialogCount, longTermSummaryCount, maxCharsPerEntry);
		recentDialog = GetRecentDialogHistory(historyEntries, recentDialogCount);
		return historyEntries.Count > 0;
	}

	public List<DialogHistoryEntryView> GetDialogHistoryEntries()
	{
		return dialogHistory.Select((DialogEntry entry) => new DialogHistoryEntryView
		{
			Speaker = entry.speaker,
			Message = entry.message,
			GameTimeText = entry.gameTimeText,
			TokenUsageText = entry.tokenUsageText,
			SpeakerType = GetSpeakerType(entry, entry.speaker, entry.speaker == "你")
		}).ToList();
	}

	private DialogEntry CreateDialogEntry(string speaker, string message, string tokenUsageText = null, QuestStorySpeakerType? speakerType = null, bool isPlayer = false)
	{
		return new DialogEntry
		{
			speaker = speaker,
			message = message,
			gameTimeText = NormalizeGameTimeTextValue(GetCurrentGameTimeTextOrDefault()),
			tokenUsageText = tokenUsageText,
			speakerType = ResolveSpeakerType(speaker, isPlayer, speakerType)
		};
	}

	private static List<DialogEntry> LoadDialogHistoryEntries(string persistentDataRoot, int npcId, string npcName)
	{
		List<DialogEntry> historyEntries = new List<DialogEntry>();
		if (npcId <= 0 || string.IsNullOrWhiteSpace(npcName))
		{
			return historyEntries;
		}
		string filePath = NormalizeDialogHistoryFiles(persistentDataRoot, npcId, npcName, null);
		if (!File.Exists(filePath))
		{
			TextEncodingUtil.WriteAllTextUtf8(filePath, string.Empty);
			return historyEntries;
		}
		string[] array = ReadDialogHistoryLines(filePath);
		foreach (string line in array)
		{
			if (TryParseDialogEntry(line, out var entry))
			{
				historyEntries.Add(entry);
			}
		}
		return historyEntries;
	}

	private static List<string> GetRecentDialogHistory(List<DialogEntry> historyEntries, int count)
	{
		if (count <= 0 || historyEntries == null || historyEntries.Count == 0)
		{
			return new List<string>();
		}
		return (from entry in historyEntries.Skip(Math.Max(0, historyEntries.Count - count))
			select entry.speaker + ": " + entry.message).ToList();
	}

	private static string BuildLongTermMemorySummary(List<DialogEntry> historyEntries, int keepRecentCount, int maxSummaryEntries, int maxCharsPerEntry)
	{
		if (historyEntries == null || historyEntries.Count <= keepRecentCount || maxSummaryEntries <= 0)
		{
			return string.Empty;
		}
		int availableLongTermCount = historyEntries.Count - keepRecentCount;
		int summaryCount = Math.Min(maxSummaryEntries, availableLongTermCount);
		int startIndex = availableLongTermCount - summaryCount;
		IEnumerable<string> longTermEntries = historyEntries.Skip(startIndex).Take(summaryCount).Select(delegate(DialogEntry entry)
		{
			string text = entry.message ?? string.Empty;
			if (text.Length > maxCharsPerEntry)
			{
				text = text.Substring(0, maxCharsPerEntry) + "…";
			}
			return "- " + entry.speaker + ": " + text;
		});
		return string.Join("\n", longTermEntries);
	}

	private void SaveDialogEntry(int npcId, string npcName, DialogEntry entry)
	{
		if (npcId > 0 && !string.IsNullOrEmpty(npcName) && entry != null)
		{
			string filePath = NormalizeDialogHistoryFiles(Application.persistentDataPath, npcId, npcName, null);
			string logLine = JsonConvert.SerializeObject(entry, Formatting.None) + "\n";
			TextEncodingUtil.AppendAllTextUtf8(filePath, logLine);
		}
	}

	private static string NormalizeDialogHistoryFiles(string persistentDataRoot, int npcId, string npcName, Func<int, string> nameResolver)
	{
		if (string.IsNullOrWhiteSpace(persistentDataRoot))
		{
			persistentDataRoot = Application.persistentDataPath;
		}
		if (npcId <= 0)
		{
			return GetDialogHistoryPath(persistentDataRoot, npcId, npcName);
		}
		if (!Directory.Exists(persistentDataRoot))
		{
			Directory.CreateDirectory(persistentDataRoot);
		}
		List<string> candidatePaths = GetDialogHistoryCandidatePaths(persistentDataRoot, npcId);
		int npcId2;
		string npcName2;
		string canonicalName = ResolveCanonicalNpcName(npcId, candidatePaths.Select((string path) => TryParseDialogHistoryFilePath(path, out npcId2, out npcName2) ? npcName2 : string.Empty).Concat(new string[1] { npcName }), nameResolver);
		string canonicalPath = GetDialogHistoryPath(persistentDataRoot, npcId, canonicalName);
		List<string> canonicalLines = new List<string>();
		if (File.Exists(canonicalPath))
		{
			canonicalLines.AddRange(ReadDialogHistoryLines(canonicalPath));
		}
		HashSet<string> existingLines = new HashSet<string>(canonicalLines, StringComparer.Ordinal);
		foreach (string candidatePath in candidatePaths)
		{
			if (string.Equals(candidatePath, canonicalPath, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			string[] array = ReadDialogHistoryLines(candidatePath);
			foreach (string line in array)
			{
				if (existingLines.Add(line))
				{
					canonicalLines.Add(line);
				}
			}
			try
			{
				File.Delete(candidatePath);
			}
			catch (Exception ex)
			{
				ManualLogSource logger = AIChatManager.logger;
				if (logger != null)
				{
					logger.LogWarning((object)("[NPCDialog] Failed to delete stale history file '" + candidatePath + "': " + ex.Message));
				}
			}
		}
		if (canonicalLines.Count > 0)
		{
			string mergedText = string.Join("\n", canonicalLines) + "\n";
			TextEncodingUtil.WriteAllTextUtf8(canonicalPath, mergedText);
		}
		else if (!File.Exists(canonicalPath))
		{
			TextEncodingUtil.WriteAllTextUtf8(canonicalPath, string.Empty);
		}
		return canonicalPath;
	}

	private static string GetDialogHistoryPath(string persistentDataRoot, int npcId, string npcName)
	{
		string safeNpcName = (string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName.Trim());
		string safeFileName = $"{npcId}_{safeNpcName}".Replace("/", "_");
		return Path.Combine(persistentDataRoot, safeFileName + ".txt");
	}

	private static List<string> GetDialogHistoryCandidatePaths(string persistentDataRoot, int npcId)
	{
		if (string.IsNullOrWhiteSpace(persistentDataRoot) || !Directory.Exists(persistentDataRoot) || npcId <= 0)
		{
			return new List<string>();
		}
		return Directory.GetFiles(persistentDataRoot, $"{npcId}_*.txt", SearchOption.TopDirectoryOnly).Where(IsDialogHistoryFilePath).OrderBy((string path) => path, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static bool TryParseDialogHistoryFilePath(string filePath, out int npcId, out string npcName)
	{
		npcId = 0;
		npcName = string.Empty;
		if (!IsDialogHistoryFilePath(filePath))
		{
			return false;
		}
		string fileName = Path.GetFileNameWithoutExtension(filePath);
		int firstUnderscore = fileName.IndexOf('_');
		if (firstUnderscore <= 0 || firstUnderscore >= fileName.Length - 1)
		{
			return false;
		}
		if (!int.TryParse(fileName.Substring(0, firstUnderscore), out npcId) || npcId <= 0)
		{
			return false;
		}
		npcName = fileName.Substring(firstUnderscore + 1);
		return !string.IsNullOrWhiteSpace(npcName);
	}

	private static string ResolveCanonicalNpcName(int npcId, IEnumerable<string> candidateNames, Func<int, string> nameResolver)
	{
		string resolvedByCaller = nameResolver?.Invoke(npcId);
		if (!string.IsNullOrWhiteSpace(resolvedByCaller))
		{
			return resolvedByCaller.Trim();
		}
		try
		{
			NPCInfo npcInfo = NpcDataExtractor.ExtractFromGameMemory(npcId);
			if (npcInfo != null && !string.IsNullOrWhiteSpace(npcInfo.Name))
			{
				return npcInfo.Name.Trim();
			}
		}
		catch (Exception ex)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogWarning((object)$"[NPCDialog] Resolve canonical npc name failed for {npcId}: {ex.Message}");
			}
		}
		string candidateName = (from name in candidateNames?.Where((string name) => !string.IsNullOrWhiteSpace(name))
			select name.Trim()).FirstOrDefault();
		return string.IsNullOrWhiteSpace(candidateName) ? "NPC" : candidateName;
	}

	private static string[] ReadDialogHistoryLines(string filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
		{
			return Array.Empty<string>();
		}
		return (from line in TextEncodingUtil.ReadAllLinesWithFallback(filePath)
			where !string.IsNullOrWhiteSpace(line)
			select line).ToArray();
	}

	private static string GetLastDialogGameTimeText(string[] historyLines)
	{
		if (historyLines == null || historyLines.Length == 0)
		{
			return "0001年01月01日";
		}
		for (int i = historyLines.Length - 1; i >= 0; i--)
		{
			if (TryGetGameTimeTextFromHistoryLine(historyLines[i], out var gameTimeText))
			{
				return gameTimeText;
			}
		}
		return "0001年01月01日";
	}

	public static bool TryGetGameTimeTextFromHistoryLine(string line, out string gameTimeText)
	{
		gameTimeText = "0001年01月01日";
		if (!TryParseDialogEntry(line, out var entry))
		{
			return false;
		}
		gameTimeText = NormalizeGameTimeTextValue(entry.gameTimeText);
		return true;
	}

	public static bool TryParseGameDate(string gameTimeText, out DateTime gameDate)
	{
		gameDate = DateTime.MinValue;
		string normalized = NormalizeGameTimeTextValue(gameTimeText);
		string[] parts = normalized.Replace("年", " ").Replace("月", " ").Replace("日", " ")
			.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 3)
		{
			return false;
		}
		if (!int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month) || !int.TryParse(parts[2], out var day))
		{
			return false;
		}
		try
		{
			gameDate = new DateTime(Math.Max(1, year), month, day);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool TryParseDialogEntry(string line, out DialogEntry entry)
	{
		entry = null;
		if (string.IsNullOrWhiteSpace(line))
		{
			return false;
		}
		string trimmed = line.Trim();
		if (trimmed.StartsWith("{", StringComparison.Ordinal))
		{
			try
			{
				entry = JsonConvert.DeserializeObject<DialogEntry>(trimmed);
				if (entry != null && !string.IsNullOrEmpty(entry.speaker) && !string.IsNullOrEmpty(entry.message))
				{
					entry.gameTimeText = NormalizeGameTimeTextValue(entry.gameTimeText);
					entry.speakerType = ResolveSpeakerType(entry.speaker, entry.speaker == "你", entry.speakerType);
					return true;
				}
			}
			catch (Exception ex)
			{
				AIChatManager.logger.LogWarning((object)("[NPCDialog] 解析 JSON 对话历史失败，将尝试旧格式: " + ex.Message));
			}
		}
		string[] parts = line.Split(new string[1] { "|||" }, 2, StringSplitOptions.None);
		if (parts.Length != 2)
		{
			return false;
		}
		entry = new DialogEntry
		{
			speaker = parts[0],
			message = parts[1],
			gameTimeText = "0001年01月01日",
			speakerType = ResolveSpeakerType(parts[0], parts[0] == "你", null)
		};
		return true;
	}

	private static QuestStorySpeakerType ResolveSpeakerType(string speaker, bool isPlayer, QuestStorySpeakerType? explicitType)
	{
		if (explicitType.HasValue)
		{
			return explicitType.Value;
		}
		if (isPlayer || speaker == "你")
		{
			return QuestStorySpeakerType.Player;
		}
		if (speaker == "旁白")
		{
			return QuestStorySpeakerType.Narrator;
		}
		return QuestStorySpeakerType.Npc;
	}

	private static QuestStorySpeakerType GetSpeakerType(DialogEntry entry, string speaker, bool isPlayer)
	{
		return ResolveSpeakerType(speaker, isPlayer, entry?.speakerType);
	}

	public static string GetCurrentGameTimeTextOrDefault()
	{
		try
		{
			Avatar player = Tools.instance?.getPlayer();
			if (player != null)
			{
				return player.worldTimeMag.getNowTime().ToString("yyyy年MM月dd日");
			}
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogWarning((object)("[NPCDialog] 获取游戏时间失败: " + ex.Message));
		}
		return "0001年01月01日";
	}

	public static string NormalizeGameTimeTextValue(string gameTimeText)
	{
		if (string.IsNullOrWhiteSpace(gameTimeText))
		{
			return "0001年01月01日";
		}
		string normalized = gameTimeText.Trim();
		int dayIndex = normalized.IndexOf('日');
		if (dayIndex >= 0)
		{
			normalized = normalized.Substring(0, dayIndex + 1);
		}
		int spaceIndex = normalized.IndexOf(' ');
		if (spaceIndex > 0)
		{
			normalized = normalized.Substring(0, spaceIndex);
		}
		string[] parts = normalized.Replace("年", " ").Replace("月", " ").Replace("日", " ")
			.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length >= 3 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var month) && int.TryParse(parts[2], out var day))
		{
			try
			{
				return new DateTime(Math.Max(1, year), month, day).ToString("yyyy年MM月dd日");
			}
			catch
			{
			}
		}
		return string.IsNullOrWhiteSpace(normalized) ? "0001年01月01日" : normalized;
	}
}
