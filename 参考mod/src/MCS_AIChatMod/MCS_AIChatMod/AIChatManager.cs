using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MCS_AIChatMod;

[BepInPlugin("com.baiyua.npcchat", "NPC AI Chat", "2.6.0")]
public class AIChatManager : BaseUnityPlugin
{
	internal static ManualLogSource logger;

	public static AIChatManager Instance;

	public static BaseAIClient client;

	public static NPCInfo npcInfo = new NPCInfo();

	public static string targetFilePath;

	public static List<ChatMessage> Messages = new List<ChatMessage>();

	public static Dictionary<int, List<ChatMessage>> requests = new Dictionary<int, List<ChatMessage>>();

	private static readonly object _tokenUsageLock = new object();

	private static TokenUsage _sessionTokenUsage = new TokenUsage();

	private static TokenUsage _lastChatRoundTokenUsage = new TokenUsage();

	private static int _previousNpcId = -1;

	public static TokenUsage GetSessionTokenUsageSnapshot()
	{
		lock (_tokenUsageLock)
		{
			return _sessionTokenUsage.Clone();
		}
	}

	public static TokenUsage GetLastChatRoundTokenUsageSnapshot()
	{
		lock (_tokenUsageLock)
		{
			return _lastChatRoundTokenUsage.Clone();
		}
	}

	public static void RecordTokenUsage(TokenUsage usage, string source)
	{
		if (usage != null && usage.HasUsage)
		{
			TokenUsage sessionSnapshot;
			lock (_tokenUsageLock)
			{
				_sessionTokenUsage.Add(usage);
				sessionSnapshot = _sessionTokenUsage.Clone();
			}
			ManualLogSource obj = logger;
			if (obj != null)
			{
				obj.LogInfo((object)$"[TokenUsage] source={source}, prompt={usage.PromptTokens}, completion={usage.CompletionTokens}, total={usage.ResolvedTotalTokens}, sessionTotal={sessionSnapshot.ResolvedTotalTokens}");
			}
		}
	}

	private static void SetLastChatRoundTokenUsage(TokenUsage usage)
	{
		lock (_tokenUsageLock)
		{
			_lastChatRoundTokenUsage = usage?.Clone() ?? new TokenUsage();
		}
	}

	public static string BuildTokenUsageDisplayText(TokenUsage currentUsage, TokenUsage cumulativeUsage, string currentLabel)
	{
		if (currentUsage == null || !currentUsage.HasUsage)
		{
			if (cumulativeUsage == null || !cumulativeUsage.HasUsage)
			{
				return "Token：当前服务未返回";
			}
			return $"Token：当前服务未返回 | 累计 {cumulativeUsage.ResolvedTotalTokens}";
		}
		string currentText = $"{currentLabel} {currentUsage.ResolvedTotalTokens}（输入{currentUsage.PromptTokens} / 输出{currentUsage.CompletionTokens}）";
		if (cumulativeUsage == null || !cumulativeUsage.HasUsage)
		{
			return "Token：" + currentText;
		}
		return $"Token：{currentText} | 累计 {cumulativeUsage.ResolvedTotalTokens}";
	}

	public static string BuildLastChatRoundTokenUsageText()
	{
		TokenUsage current = GetLastChatRoundTokenUsageSnapshot();
		TokenUsage cumulative = GetSessionTokenUsageSnapshot();
		return BuildTokenUsageDisplayText(current, cumulative, "本次");
	}

	public static LegacyDataWipeSummary ClearLegacyModDataPreserveCharacterCards()
	{
		return ClearLegacyModDataPreserveCharacterCardsInternal(Application.persistentDataPath, AIQuestManager.Instance, NPCDialog.Instance);
	}

	internal static LegacyDataWipeSummary ClearLegacyModDataPreserveCharacterCardsForTest(string persistentDataRoot, AIQuestManager questManager, NPCDialog dialog)
	{
		return ClearLegacyModDataPreserveCharacterCardsInternal(persistentDataRoot, questManager, dialog);
	}

	private static LegacyDataWipeSummary ClearLegacyModDataPreserveCharacterCardsInternal(string persistentDataRoot, AIQuestManager questManager, NPCDialog dialog)
	{
		LegacyDataWipeSummary summary = NPCDialog.DeleteDialogHistoryFilesPreserveCharacterCards(persistentDataRoot, new LegacyDataWipeSummary());
		if (requests == null)
		{
			requests = new Dictionary<int, List<ChatMessage>>();
		}
		else
		{
			requests.Clear();
		}
		targetFilePath = null;
		Messages = new List<ChatMessage>();
		try
		{
			dialog?.ResetConversationAfterGlobalHistoryWipe();
		}
		catch (Exception ex)
		{
			ManualLogSource obj = logger;
			if (obj != null)
			{
				obj.LogWarning((object)$"[AIChatManager] Reset conversation after legacy wipe failed: {ex}");
			}
			summary.AddError("重置当前对话上下文失败: " + ex.Message);
		}
		try
		{
			if (questManager != null)
			{
				questManager.ClearAllQuestDataForLegacyWipe(persistentDataRoot, summary);
			}
			else
			{
				AIQuestManager.DeleteQuestPersistenceFilesForLegacyWipe(persistentDataRoot, summary);
			}
		}
		catch (Exception ex2)
		{
			ManualLogSource obj2 = logger;
			if (obj2 != null)
			{
				obj2.LogWarning((object)$"[AIChatManager] Clear quest data during legacy wipe failed: {ex2}");
			}
			summary.AddError("清空任务数据失败: " + ex2.Message);
		}
		ManualLogSource obj3 = logger;
		if (obj3 != null)
		{
			obj3.LogInfo((object)$"[AIChatManager] Legacy wipe completed: dialogDeleted={summary.DeletedDialogHistoryFiles}, characterCards={summary.PreservedCharacterCardFiles}, questFiles={summary.DeletedQuestPersistenceFiles}, questDirDeleted={summary.QuestSavesDirectoryDeleted}, errors={summary.Errors?.Count ?? 0}");
		}
		return summary;
	}

	public static void RefreshWorldPrompt(string promptText)
	{
		if (string.IsNullOrWhiteSpace(promptText))
		{
			return;
		}
		int updatedContextCount = 0;
		HashSet<int> visited = new HashSet<int>();
		if (TryApplyWorldPrompt(Messages, promptText, visited))
		{
			updatedContextCount++;
		}
		foreach (List<ChatMessage> request in requests.Values)
		{
			if (TryApplyWorldPrompt(request, promptText, visited))
			{
				updatedContextCount++;
			}
		}
		ManualLogSource obj = logger;
		if (obj != null)
		{
			obj.LogInfo((object)$"[AIChatManager] 世界观 Prompt 已热更新到 {updatedContextCount} 个对话上下文");
		}
	}

	private static bool TryApplyWorldPrompt(List<ChatMessage> messageList, string promptText, HashSet<int> visited)
	{
		if (messageList == null)
		{
			return false;
		}
		int identity = RuntimeHelpers.GetHashCode(messageList);
		if (visited != null && !visited.Add(identity))
		{
			return false;
		}
		ChatMessage systemMessage = null;
		foreach (ChatMessage message in messageList)
		{
			if (message != null && string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
			{
				systemMessage = message;
				break;
			}
		}
		if (systemMessage != null)
		{
			systemMessage.Content = promptText;
		}
		else
		{
			messageList.Insert(0, new ChatMessage("system", promptText));
		}
		return true;
	}

	private void Awake()
	{
		Instance = this;
		logger = Logger;
		GameObject managerObject = new GameObject("AIChatMod_Manager");
		UnityEngine.Object.DontDestroyOnLoad(managerObject);
		ConfigManager.Initialize(((BaseUnityPlugin)this).Config);
		managerObject.AddComponent<NPCDialog>();
		managerObject.AddComponent<UIManager>();
		managerObject.AddComponent<AIQuestManager>();
		Harmony.CreateAndPatchAll(typeof(NpcInfoPatch), (string)null);
		Harmony.CreateAndPatchAll(typeof(OnJiaoTanBtnClickPatch), (string)null);
		Harmony.CreateAndPatchAll(typeof(ToolsCanClickPatch), (string)null);
		Harmony.CreateAndPatchAll(typeof(BaseMapCompontCanClickPatch), (string)null);
		try
		{
			Harmony.CreateAndPatchAll(typeof(QuestSaveContextPatches), (string)null);
		}
		catch (Exception arg)
		{
			Logger.LogWarning((object)$"[AIChatManager] QuestSaveContextPatches patch failed: {arg}");
		}
		try
		{
			Harmony.CreateAndPatchAll(typeof(QuestCyMailCellInitPatch), (string)null);
		}
		catch (Exception arg2)
		{
			Logger.LogWarning((object)$"[AIChatManager] QuestCyMailCellInitPatch patch failed: {arg2}");
		}
		try
		{
			Harmony.CreateAndPatchAll(typeof(QuestTimeAdvancePatches), (string)null);
		}
		catch (Exception arg3)
		{
			Logger.LogWarning((object)$"[AIChatManager] QuestTimeAdvancePatches patch failed: {arg3}");
		}
		try
		{
			Harmony.CreateAndPatchAll(typeof(OfficialCyFriendNarrativeResumePatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialNpcActionChangedPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialNpcFavorChangedPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialPlayerMoneyChangedPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialPlayerItemAddedPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialPlayerItemRemovedPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialPlayerSkillLearnedPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialPlayerStaticSkillLearnedPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialPlayerLevelUpPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialTaskAddTaskPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialTaskAddTaskWithTimePatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialTaskCompletionProgressPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialTaskUiInjectAiMainlinePatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialTaskUiAiCellClickPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialNpcStatusChangedPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialNpcDeathPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialFightVictoryPatch), (string)null);
		}
		catch (Exception arg4)
		{
			Logger.LogWarning((object)$"[AIChatManager] Quest runtime world-event patches failed: {arg4}");
		}
		try
		{
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyUseDanYaoInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographySmallTuPoInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyLianDanInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyLianQiInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyQiYuInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyImportantEventInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyPaiMaiInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyTianJiDaBiInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyZhuJiInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyJinDanInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyYuanYingInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyHuaShenInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyJieShaInterceptionPatch), (string)null);
			Harmony.CreateAndPatchAll(typeof(OfficialBiographyLunDaoInterceptionPatch), (string)null);
		}
		catch (Exception arg5)
		{
			Logger.LogWarning((object)$"[AIChatManager] Biography interception patches failed: {arg5}");
		}
		SceneManager.sceneLoaded += OnSceneLoaded;
		Logger.LogInfo((object)"[AIChatManager] NPC AI 聊天插件已加载");
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if ((scene.name == "AllMaps" || scene.name.StartsWith("S")) && ConfigManager.IsConfigReady() && client == null)
		{
			client = AIModelFactory.CreateFromConfig();
			Logger.LogInfo((object)"[AIChatManager] 场景加载完成，已预初始化 AI 客户端");
		}
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	public static bool RebuildClientFromConfig()
	{
		try
		{
			if (!ConfigManager.IsConfigReady())
			{
				client = null;
				logger.LogWarning((object)"[AIChatManager] Config not ready, AI client reset.");
				return false;
			}
			client = AIModelFactory.CreateFromConfig();
			bool ready = client != null;
			if (ready)
			{
				logger.LogInfo((object)"[AIChatManager] AI client rebuilt from latest config.");
			}
			else
			{
				logger.LogError((object)"[AIChatManager] Failed to rebuild AI client.");
			}
			return ready;
		}
		catch (Exception arg)
		{
			client = null;
			logger.LogError((object)$"[AIChatManager] Rebuild client exception: {arg}");
			return false;
		}
	}

	public IEnumerator getAllNpcInfo()
	{
		yield return new WaitUntil(() => NpcJieSuanManager.inst != null);
		if (UINPCJiaoHu.Inst?.NowJiaoHuNPC != null)
		{
			int currentNpcId = UINPCJiaoHu.Inst.NowJiaoHuNPC.ID;
			StartChatWithNpc(currentNpcId);
		}
	}

	internal static void HandleNpcInteractionDetected(int npcId)
	{
		if (npcId > 0)
		{
			ManualLogSource obj = logger;
			if (obj != null)
			{
				obj.LogInfo((object)$"检测到NPC交互，ID: {npcId}");
			}
			if ((UnityEngine.Object)(object)Instance != null)
			{
				((MonoBehaviour)(object)Instance).StartCoroutine(Instance.getAllNpcInfo());
			}
		}
	}

	internal static void HandleNpcInteractionDetectedForTest(int npcId)
	{
		HandleNpcInteractionDetected(npcId);
	}

	public void StartChatWithNpc(int targetNpcId)
	{
		npcInfo = NpcDataExtractor.ExtractFromGameMemory(targetNpcId);
		if (!ConfigManager.IsConfigReady())
		{
			Logger.LogInfo((object)"[StartChatWithNpc] 配置未就绪，弹出设置面板");
			if (UIManager.Instance != null && UIManager.Instance.IsChatPanelVisible())
			{
				UIManager.Instance.ShowChatPanel(npcInfo.Name ?? "NPC");
			}
			return;
		}
		if (client == null)
		{
			client = AIModelFactory.CreateFromConfig();
			if (client == null)
			{
				Logger.LogError((object)"[StartChatWithNpc] 客户端创建失败");
				return;
			}
		}
		int newNpcId = npcInfo.NpcID;
		if (_previousNpcId > 0 && _previousNpcId != newNpcId && Messages != null && Messages.Count > 0)
		{
			requests[_previousNpcId] = Messages;
			Logger.LogInfo((object)$"[StartChatWithNpc] 保存NPC {_previousNpcId} 的对话历史 ({Messages.Count}条)");
		}
		if (requests.TryGetValue(newNpcId, out var existingMessages) && existingMessages != null && existingMessages.Count > 0)
		{
			Messages = existingMessages;
			Logger.LogInfo((object)$"[StartChatWithNpc] 恢复NPC {newNpcId}({npcInfo.Name}) 的对话历史 ({Messages.Count}条)");
		}
		else
		{
			Messages = PromptBuilder.ResolveInitialContext(npcInfo) ?? new List<ChatMessage>();
			requests[newNpcId] = Messages;
			Logger.LogInfo((object)$"[StartChatWithNpc] 创建NPC {newNpcId}({npcInfo.Name}) 的新对话上下文");
		}
		NPCDialog.Instance.StartDialogWithNPC(npcInfo.NpcID.ToString(), npcInfo.Name);
		_previousNpcId = newNpcId;
		Logger.LogInfo((object)("[StartChatWithNpc] 对话已启动: " + npcInfo.Name));
		UIManager.Instance?.ShowChatPanel(npcInfo.Name ?? "NPC");
	}

	public static async Task<string> GetAIResponseAsync(string input)
	{
		if (npcInfo == null || npcInfo.NpcID <= 0)
		{
			logger.LogError((object)"npcInfo is null or invalid.");
			return "Error: npcInfo is not initialized.";
		}
		if (requests.TryGetValue(npcInfo.NpcID, out var existingMessages) && existingMessages != null && existingMessages.Count > 0)
		{
			Messages = existingMessages;
		}
		else
		{
			logger.LogInfo((object)"[GetAIResponseAsync] 初始化新的对话上下文");
			Messages = PromptBuilder.ResolveInitialContext(npcInfo) ?? new List<ChatMessage>();
			requests[npcInfo.NpcID] = Messages;
		}
		if (client == null)
		{
			logger.LogError((object)"client is null.");
			return "Error: AI 客户端未初始化。请检查 API 配置。";
		}
		try
		{
			SetLastChatRoundTokenUsage(null);
			TokenUsage roundTokenUsage = new TokenUsage();
			NPCChatTools.RemoveTransientToolInstructions(Messages);
			NPCChatTools.EnsureToolInstruction(Messages);
			Messages.Add(new ChatMessage("user", input));
			ChatRequest request = new ChatRequest
			{
				Messages = Messages,
				Tools = NPCChatTools.GetToolsForCurrentContext()
			};
			OpenAIResponse apiResponse = await client.GenerateResponseAsync(request);
			if (apiResponse?.Usage != null && apiResponse.Usage.HasUsage)
			{
				roundTokenUsage.Add(apiResponse.Usage);
				RecordTokenUsage(apiResponse.Usage, "chat");
			}
			if (apiResponse.Choices == null || apiResponse.Choices.Count == 0)
			{
				SetLastChatRoundTokenUsage(roundTokenUsage);
				return "Error: AI未返回有效内容。";
			}
			ResponseMessage responseMessage = apiResponse.Choices[0].Message;
			while (responseMessage.ToolCalls != null && responseMessage.ToolCalls.Count > 0)
			{
				Messages.Add(new ChatMessage
				{
					Role = "assistant",
					Content = responseMessage.Content,
					ToolCalls = responseMessage.ToolCalls
				});
				foreach (ToolCall toolCall in responseMessage.ToolCalls)
				{
					string toolResult;
					try
					{
						toolResult = NPCChatTools.ExecuteTool(toolCall.Function.Name, toolCall.Function.Arguments);
					}
					catch (Exception ex)
					{
						toolResult = "Error executing tool: " + ex.Message;
						logger.LogError((object)toolResult);
					}
					Messages.Add(new ChatMessage("tool", toolResult, toolCall.Id));
				}
				NPCChatTools.EnsureToolInstruction(Messages);
				NPCChatTools.EnsureToolFollowupInstruction(Messages);
				request.Messages = Messages;
				request.Tools = NPCChatTools.GetToolsForCurrentContext();
				apiResponse = await client.GenerateResponseAsync(request);
				if (apiResponse?.Usage != null && apiResponse.Usage.HasUsage)
				{
					roundTokenUsage.Add(apiResponse.Usage);
					RecordTokenUsage(apiResponse.Usage, "chat_tool_followup");
				}
				if (apiResponse.Choices == null || apiResponse.Choices.Count == 0)
				{
					SetLastChatRoundTokenUsage(roundTokenUsage);
					return "Error: 工具执行后获取二次响应为空。";
				}
				responseMessage = apiResponse.Choices[0].Message;
			}
			string content = responseMessage.Content;
			Messages.Add(new ChatMessage("assistant", content));
			SetLastChatRoundTokenUsage(roundTokenUsage);
			return content;
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			SetLastChatRoundTokenUsage(null);
			logger.LogError((object)ex3);
			return "Error: " + ex3.Message;
		}
		finally
		{
			NPCChatTools.RemoveTransientToolInstructions(Messages);
		}
	}
}
