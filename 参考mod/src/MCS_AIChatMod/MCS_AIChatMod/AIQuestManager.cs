using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BepInEx.Logging;
using Fungus;
using HarmonyLib;
using JSONClass;
using KBEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MCS_AIChatMod;

public class AIQuestManager : MonoBehaviour
{
	private sealed class QuestProgramTestNotificationRecord
	{
		public int NpcId;

		public string NpcName;

		public string Message;

		public string HistoryMessage;

		public bool WasQueued;
	}

	private sealed class QuestProgramTestCombatRecord
	{
		public int NpcId;

		public string FightMusic;
	}

	private sealed class QuestProgramRecentDeliveryMailSubmission
	{
		public int NpcId;

		public int ItemId;

		public int MailId;

		public string QuestId;

		public string StepId;

		public float SubmittedAt;
	}

	private sealed class QuestProgramMutationScope : IDisposable
	{
		private Action _onDispose;

		public QuestProgramMutationScope(Action onDispose)
		{
			_onDispose = onDispose;
		}

		public void Dispose()
		{
			Action callback = _onDispose;
			_onDispose = null;
			callback?.Invoke();
		}
	}

	private class QuestSaveData
	{
		public int SaveFormatVersion { get; set; } = 3;

		public DateTime SavedAt { get; set; }

		public List<QuestMailEnvelope> PendingQuestMails { get; set; } = new List<QuestMailEnvelope>();

		public Dictionary<int, List<QuestTaskMemoryEntry>> QuestTaskMemories { get; set; } = new Dictionary<int, List<QuestTaskMemoryEntry>>();

		public MainlineCampaign MainlineCampaign { get; set; }

		public QuestProgramState ProgramState { get; set; } = QuestProgramPersistence.CreateEmptyState();
	}

	[Serializable]
	private class QuestTaskMemoryEntry
	{
		public int NpcId;

		public string NpcName;

		public string QuestId;

		public string StepId;

		public string Summary;

		public string GameTimeText;
	}

	[Serializable]
	private class QuestMailEnvelope
	{
		public int MailId;

		public int NpcId;

		public string NpcName;

		public string QuestId;

		public string StepId;

		public string MailType;

		public int ItemId;

		public int ItemCount;

		public int FavorAmount;

		public string Message;

		public string HistoryMessage;

		public string SendTime;

		public bool MemoryRecorded;
	}

	private sealed class AITextCallResult
	{
		public string Content;

		public TokenUsage Usage;
	}

	private sealed class MainlineStageParseResult<T>
	{
		public T Value;

		public string Error;
	}

	private sealed class MainlineStageExecutionResult<T>
	{
		public T Value;

		public string ResponseText;

		public bool UsedRepair;

		public TokenUsage Usage = new TokenUsage();
	}

	private sealed class MainlineRepairableStageException : InvalidOperationException
	{
		public string ResponseText { get; }

		public MainlineRepairableStageException(string message, string responseText)
			: base(message)
		{
			ResponseText = responseText;
		}
	}

	private sealed class CompiledChoiceBranchContext
	{
		public HashSet<string> BranchRefs = new HashSet<string>(StringComparer.Ordinal);

		public Dictionary<string, QuestNode> BranchTails = new Dictionary<string, QuestNode>(StringComparer.Ordinal);
	}

	private List<QuestMailEnvelope> _pendingQuestMails = new List<QuestMailEnvelope>();

	private Dictionary<int, List<QuestTaskMemoryEntry>> _questTaskMemories = new Dictionary<int, List<QuestTaskMemoryEntry>>();

	private MainlineCampaign _mainlineCampaign;

	private QuestProgramState _questProgramState = QuestProgramPersistence.CreateEmptyState();

	private readonly QuestEventBus _questEventBus = new QuestEventBus();

	private readonly QuestPredicateEngine _questPredicateEngine = new QuestPredicateEngine();

	private readonly QuestCommandExecutor _questCommandExecutor = new QuestCommandExecutor();

	private readonly QuestNarrativeDirector _questNarrativeDirector = new QuestNarrativeDirector();

	private bool _questProgramRuntimeInitialized;

	private int _questProgramOfficialTaskMutationDepth;

	private int _questProgramNpcActionMutationDepth;

	private static readonly Regex QuestPlayerFacingNpcIdPattern = new Regex("NPC_(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex QuestPlayerFacingNpcLabelPattern = new Regex("NPC[_\\s]*ID\\s*[:：]?\\s*(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private int _questProgramNpcFavorMutationDepth;

	private int _questProgramNpcStatusMutationDepth;

	private int _questProgramNpcDeathMutationDepth;

	private int _questProgramPlayerItemMutationDepth;

	private int _questProgramPlayerMoneyMutationDepth;

	private int _questProgramPlayerSkillMutationDepth;

	private DateTime? _questProgramTestGameTime;

	private HashSet<string> _questProgramTestLocationIds;

	private Dictionary<int, bool> _questProgramTestNpcAliveStates = new Dictionary<int, bool>();

	private Dictionary<int, bool> _questProgramTestCyFriendStates = new Dictionary<int, bool>();

	private Dictionary<int, int> _questProgramTestNpcFavorValues = new Dictionary<int, int>();

	private Dictionary<int, int> _questProgramTestPlayerItemCounts = new Dictionary<int, int>();

	private HashSet<int> _questProgramTestPlayerSkillIds = new HashSet<int>();

	private HashSet<int> _questProgramTestPlayerStaticSkillIds = new HashSet<int>();

	private int? _questProgramTestPlayerMoney;

	private int? _questProgramTestPlayerLevel;

	private int? _questProgramTestFuBenRemainingDays;

	private int? _questProgramTestSceneType;

	private Dictionary<int, bool> _questProgramTestNpcPatrolPresence = new Dictionary<int, bool>();

	private Dictionary<int, bool> _questProgramTestOfficialTaskOutdatedStates = new Dictionary<int, bool>();

	private long? _lastQuestProgramObservedMoney;

	private int? _lastQuestProgramObservedPlayerLevel;

	private int? _lastQuestProgramObservedFuBenRemainingDays;

	private Dictionary<int, int> _lastQuestProgramObservedItemCounts = new Dictionary<int, int>();

	private Dictionary<int, bool> _lastQuestProgramObservedNpcAliveStates = new Dictionary<int, bool>();

	private Dictionary<int, int> _lastQuestProgramObservedNpcFavorValues = new Dictionary<int, int>();

	private Dictionary<int, int> _lastQuestProgramObservedNpcActions = new Dictionary<int, int>();

	private HashSet<int> _questProgramTestOfficialStartedTaskIds = new HashSet<int>();

	private Dictionary<int, HashSet<int>> _questProgramTestOfficialFinishedTaskSteps = new Dictionary<int, HashSet<int>>();

	private Dictionary<int, int> _questProgramTestOfficialRequiredTaskStepCounts = new Dictionary<int, int>();

	private Dictionary<int, string> _questProgramTestNpcLocations = new Dictionary<int, string>();

	private Dictionary<int, int> _questProgramTestNpcActions = new Dictionary<int, int>();

	private Dictionary<int, int> _questProgramTestNpcStatuses = new Dictionary<int, int>();

	private Dictionary<int, int> _questProgramTestNpcLockActions = new Dictionary<int, int>();

	private List<QuestProgramTestNotificationRecord> _questProgramTestNotificationRecords = new List<QuestProgramTestNotificationRecord>();

	private List<QuestProgramTestCombatRecord> _questProgramTestCombatRecords = new List<QuestProgramTestCombatRecord>();

	private readonly List<QuestProgramRecentDeliveryMailSubmission> _recentQuestProgramDeliveryMailSubmissions = new List<QuestProgramRecentDeliveryMailSubmission>();

	private Dictionary<string, string> _questProgramTestChoiceSelections = new Dictionary<string, string>(StringComparer.Ordinal);

	private readonly Queue<AITextCallResult> _questProgramTestAiResponses = new Queue<AITextCallResult>();

	private Coroutine _questProgramNarrativeCoroutine;

	private string _activeQuestProgramNarrativeProgramId = string.Empty;

	private string _activeQuestProgramNarrativeNodeId = string.Empty;

	private const int MaxQuestTaskMemoryEntriesPerNpc = 12;

	private const int MaxQuestTaskMemorySummaryEntries = 6;

	private const int MaxQuestTaskMemorySummaryChars = 80;

	private const int MaxOfficialContactMemoryEntriesPerNpc = 12;

	private const int MaxOfficialContactSummaryEntries = 6;

	private const int MaxOfficialContactSummaryChars = 80;

	private const float QUEST_PROGRAM_DELIVERY_MAIL_TTL_SECONDS = 30f;

	private const int CurrentQuestSaveFormatVersion = 3;

	private const int MaxQuestNpcValidationAttempts = 3;

	private bool _isGenerating = false;

	private bool _hasLoadedPersistence = false;

	private bool _questPersistenceDirty = false;

	private string _currentSaveScopeKey = "legacy_default";

	private int _currentAvatarIndex = -1;

	private int _currentSlotIndex = -1;

	private bool _currentUseNewSaveSystem = true;

	private int _saveContextSaveDepth = 0;

	private int _saveContextLoadDepth = 0;

	private int _saveContextSelectDepth = 0;

	private int _saveContextSwitchDepth = 0;

	private int _lastCompletedSaveContextFrame = -1;

	private QuestSaveContextEventType _lastCompletedSaveContextEventType = QuestSaveContextEventType.Unknown;

	private int _lastCompletedSaveContextAvatar = -1;

	private int _lastCompletedSaveContextSlot = -1;

	private bool _lastCompletedSaveContextUseNewSaveSystem = true;

	private static int _pendingAvatarIndex = -1;

	private static int _pendingSlotIndex = -1;

	private static bool _pendingUseNewSaveSystem = true;

	private static QuestSaveContextEventType _pendingSaveContextEventType = QuestSaveContextEventType.Unknown;

	private static bool _pendingSaveContextIsPostfix = false;

	private static bool _hasPendingSaveContext = false;

	private static string _saveFilePathOverride;

	private static string _historyFilePathOverride;

	private static string _newSaveRootOverride;

	private static string _oldSaveRootOverride;

	private static string _legacyQuestPersistenceRootOverride;

	private Dictionary<int, bool> _talkFlags = new Dictionary<int, bool>();

	private Dictionary<int, float> _recentBattleVictories = new Dictionary<int, float>();

	private const float BATTLE_VICTORY_TTL_SECONDS = 15f;

	private float _checkInterval = 3f;

	private float _checkTimer = 0f;

	private bool _pendingBiographyStoryGenerationRequested = false;

	private bool _suppressBiographyEventInterception = false;

	private HashSet<string> _visitedSceneIds = new HashSet<string>();

	private string _lastNarrativeLocationSignature = string.Empty;

	private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
	{
		Formatting = Formatting.Indented,
		NullValueHandling = NullValueHandling.Ignore,
		Converters = { (JsonConverter)new StringEnumConverter() }
	};

	public static AIQuestManager Instance { get; private set; }

	public bool IsGenerating => _isGenerating;

	private static string EffectiveLegacyQuestPersistenceRoot => string.IsNullOrWhiteSpace(_legacyQuestPersistenceRootOverride) ? Application.persistentDataPath : _legacyQuestPersistenceRootOverride;

	private static string EffectiveNewSaveRoot => string.IsNullOrWhiteSpace(_newSaveRootOverride) ? Paths.GetNewSavePath() : _newSaveRootOverride;

	private static string EffectiveOldSaveRoot => string.IsNullOrWhiteSpace(_oldSaveRootOverride) ? Paths.GetSavePath() : _oldSaveRootOverride;

	private string SaveFilePath
	{
		get
		{
			if (!string.IsNullOrEmpty(_saveFilePathOverride))
			{
				return _saveFilePathOverride;
			}
			return BuildQuestSaveFilePath(_currentAvatarIndex, _currentSlotIndex, _currentUseNewSaveSystem);
		}
	}

	private string HistoryFilePath
	{
		get
		{
			if (!string.IsNullOrEmpty(_historyFilePathOverride))
			{
				return _historyFilePathOverride;
			}
			return BuildQuestHistoryFilePath(_currentAvatarIndex, _currentSlotIndex, _currentUseNewSaveSystem);
		}
	}

	private string LegacyRootSaveFilePath => Path.Combine(EffectiveLegacyQuestPersistenceRoot, "AIQuestData.json");

	private string LegacyRootHistoryFilePath => Path.Combine(EffectiveLegacyQuestPersistenceRoot, "AIQuestHistory.json");

	internal static void SetPersistencePathOverrides(string saveFilePath, string historyFilePath)
	{
		_saveFilePathOverride = saveFilePath;
		_historyFilePathOverride = historyFilePath;
	}

	internal static void SetPersistenceRootOverrides(string newSaveRoot, string oldSaveRoot)
	{
		_newSaveRootOverride = newSaveRoot;
		_oldSaveRootOverride = oldSaveRoot;
	}

	internal static void SetLegacyQuestPersistenceRootForTest(string legacyPersistentRoot)
	{
		_legacyQuestPersistenceRootOverride = legacyPersistentRoot;
	}

	internal static string BuildQuestSaveFilePathForTest(int avatarIndex, int slotIndex, bool useNewSaveSystem)
	{
		return BuildQuestSaveFilePath(avatarIndex, slotIndex, useNewSaveSystem);
	}

	internal static string SerializeQuestSaveDataForTest(DateTime savedAt, MainlineCampaign campaign, QuestProgramState programState)
	{
		QuestSaveData saveData = new QuestSaveData
		{
			SaveFormatVersion = 3,
			SavedAt = savedAt,
			MainlineCampaign = campaign,
			ProgramState = QuestProgramPersistence.NormalizeState(programState)
		};
		return JsonConvert.SerializeObject(saveData, _jsonSettings);
	}

	internal static void NotifyGameSaveContext(int avatarIndex, int slotIndex, bool useNewSaveSystem, QuestSaveContextEventType eventType, bool isPostfix)
	{
		_pendingAvatarIndex = avatarIndex;
		_pendingSlotIndex = slotIndex;
		_pendingUseNewSaveSystem = useNewSaveSystem;
		_pendingSaveContextEventType = eventType;
		_pendingSaveContextIsPostfix = isPostfix;
		_hasPendingSaveContext = true;
		if (Instance != null)
		{
			Instance.HandleSaveContextEvent(avatarIndex, slotIndex, useNewSaveSystem, eventType, isPostfix);
			_hasPendingSaveContext = false;
		}
	}

	internal static void NotifyGameSaveContextForTest(int avatarIndex, int slotIndex, bool useNewSaveSystem, QuestSaveContextEventType eventType, bool isPostfix)
	{
		NotifyGameSaveContext(avatarIndex, slotIndex, useNewSaveSystem, eventType, isPostfix);
	}

	public bool HasPendingQuest(int npcId)
	{
		TryLoadPersistenceIfReady();
		return HasQuestProgramForAnchorNpc(npcId, QuestProgramStatus.Draft);
	}

	public bool IsNpcReservedByAIQuest(int npcId)
	{
		if (npcId <= 0)
		{
			return false;
		}
		return HasQuestProgramForAnchorNpc(npcId, QuestProgramStatus.Active) || HasQuestProgramForAnchorNpc(npcId, QuestProgramStatus.Draft);
	}

	private void AddPendingQuestProgram(QuestProgram program)
	{
		if (program != null)
		{
			program.Status = QuestProgramStatus.Draft;
			program.Priority = GetMainlineQuestPriority(program?.Priority ?? 0);
			program.Runtime = program.Runtime ?? new QuestRuntimeState();
			program.Nodes = program.Nodes ?? new List<QuestNode>();
			UpsertQuestProgram(program);
		}
	}

	private bool HasQuestProgramForAnchorNpc(int npcId, QuestProgramStatus status)
	{
		if (npcId <= 0)
		{
			return false;
		}
		return EnsureQuestProgramState().Programs.Any((QuestProgram program) => program != null && program.AnchorNpcId == npcId && program.Status == status);
	}

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			UnityEngine.Object.Destroy(this);
			return;
		}
		Instance = this;
		EnsureQuestProgramRuntimeInitialized();
	}

	private void TryLoadPersistenceIfReady()
	{
		EnsureSaveContextInitialized();
		if (!_hasLoadedPersistence && TryGetGameTime(out var _))
		{
			AIChatManager.logger.LogInfo((object)("[AIQuestManager] Start() - persistentDataPath: " + Application.persistentDataPath + ", questScope=" + _currentSaveScopeKey));
			LoadQuestSnapshotForCurrentScope();
		}
	}

	private void EnsureSaveContextInitialized()
	{
		if (_hasPendingSaveContext)
		{
			HandleSaveContextEvent(_pendingAvatarIndex, _pendingSlotIndex, _pendingUseNewSaveSystem, _pendingSaveContextEventType, _pendingSaveContextIsPostfix);
			_hasPendingSaveContext = false;
			return;
		}
		int avatarIndex = -1;
		int slotIndex = -1;
		bool useNewSave = false;
		try
		{
			if (!string.IsNullOrEmpty(YSNewSaveSystem.NowAvatarPathPre))
			{
				avatarIndex = YSNewSaveSystem.NowUsingAvatarIndex;
				slotIndex = YSNewSaveSystem.NowUsingSlot;
				useNewSave = true;
			}
		}
		catch (Exception ex)
		{
			LogQuestWarning("EnsureSaveContextInitialized", ex);
		}
		if (avatarIndex < 0)
		{
			avatarIndex = PlayerPrefs.GetInt("NowPlayerFileAvatar", -1);
			if (slotIndex < 0)
			{
				slotIndex = 0;
			}
			useNewSave = false;
		}
		if (avatarIndex >= 0)
		{
			HandleSaveContextEvent(avatarIndex, slotIndex, useNewSave, QuestSaveContextEventType.Unknown, isPostfix: true);
		}
	}

	private void HandleSaveContextEvent(int avatarIndex, int slotIndex, bool useNewSaveSystem, QuestSaveContextEventType eventType, bool isPostfix)
	{
		if (avatarIndex < 0)
		{
			return;
		}
		switch (eventType)
		{
		case QuestSaveContextEventType.Save:
			ApplySaveScopeMetadata(avatarIndex, slotIndex, useNewSaveSystem);
			if (TryCompleteSaveContextEventBoundary(avatarIndex, slotIndex, useNewSaveSystem, eventType, isPostfix))
			{
				if (!_hasLoadedPersistence)
				{
					LoadQuestSnapshotForCurrentScope();
				}
				if (_questPersistenceDirty)
				{
					SaveQuests();
				}
			}
			break;
		case QuestSaveContextEventType.Load:
		case QuestSaveContextEventType.Select:
		case QuestSaveContextEventType.Switch:
			if (TryCompleteSaveContextEventBoundary(avatarIndex, slotIndex, useNewSaveSystem, eventType, isPostfix))
			{
				bool scopeChanged = ApplySaveScopeMetadata(avatarIndex, slotIndex, useNewSaveSystem);
				if (!ShouldSkipRedundantLoadLikeSaveContext(eventType, scopeChanged))
				{
					ResetQuestPersistenceRuntimeState();
					LoadQuestSnapshotForCurrentScope();
				}
			}
			break;
		default:
			if (ApplySaveScopeMetadata(avatarIndex, slotIndex, useNewSaveSystem))
			{
				ResetQuestPersistenceRuntimeState();
			}
			break;
		}
	}

	private bool TryCompleteSaveContextEventBoundary(int avatarIndex, int slotIndex, bool useNewSaveSystem, QuestSaveContextEventType eventType, bool isPostfix)
	{
		if (eventType == QuestSaveContextEventType.Unknown)
		{
			return true;
		}
		if (!isPostfix)
		{
			IncrementSaveContextDepth(eventType);
			return false;
		}
		DecrementSaveContextDepth(eventType);
		if (GetSaveContextDepth(eventType) > 0)
		{
			return false;
		}
		int currentFrame = Time.frameCount;
		if (_lastCompletedSaveContextFrame == currentFrame && _lastCompletedSaveContextAvatar == avatarIndex && _lastCompletedSaveContextSlot == slotIndex && _lastCompletedSaveContextUseNewSaveSystem == useNewSaveSystem)
		{
			if (_lastCompletedSaveContextEventType == eventType)
			{
				return false;
			}
			if (IsLoadLikeSaveContextEvent(_lastCompletedSaveContextEventType) && IsLoadLikeSaveContextEvent(eventType))
			{
				return false;
			}
		}
		_lastCompletedSaveContextFrame = currentFrame;
		_lastCompletedSaveContextEventType = eventType;
		_lastCompletedSaveContextAvatar = avatarIndex;
		_lastCompletedSaveContextSlot = slotIndex;
		_lastCompletedSaveContextUseNewSaveSystem = useNewSaveSystem;
		return true;
	}

	private static bool IsLoadLikeSaveContextEvent(QuestSaveContextEventType eventType)
	{
		return eventType == QuestSaveContextEventType.Load || eventType == QuestSaveContextEventType.Select || eventType == QuestSaveContextEventType.Switch;
	}

	private bool ShouldSkipRedundantLoadLikeSaveContext(QuestSaveContextEventType eventType, bool scopeChanged)
	{
		if (scopeChanged || !_hasLoadedPersistence)
		{
			return false;
		}
		switch (eventType)
		{
		case QuestSaveContextEventType.Select:
		case QuestSaveContextEventType.Switch:
			return true;
		case QuestSaveContextEventType.Load:
			return !_questPersistenceDirty;
		default:
			return false;
		}
	}

	private void IncrementSaveContextDepth(QuestSaveContextEventType eventType)
	{
		switch (eventType)
		{
		case QuestSaveContextEventType.Save:
			_saveContextSaveDepth++;
			break;
		case QuestSaveContextEventType.Load:
			_saveContextLoadDepth++;
			break;
		case QuestSaveContextEventType.Select:
			_saveContextSelectDepth++;
			break;
		case QuestSaveContextEventType.Switch:
			_saveContextSwitchDepth++;
			break;
		}
	}

	private void DecrementSaveContextDepth(QuestSaveContextEventType eventType)
	{
		switch (eventType)
		{
		case QuestSaveContextEventType.Save:
			_saveContextSaveDepth--;
			break;
		case QuestSaveContextEventType.Load:
			_saveContextLoadDepth--;
			break;
		case QuestSaveContextEventType.Select:
			_saveContextSelectDepth--;
			break;
		case QuestSaveContextEventType.Switch:
			_saveContextSwitchDepth--;
			break;
		}
	}

	private int GetSaveContextDepth(QuestSaveContextEventType eventType)
	{
		return eventType switch
		{
			QuestSaveContextEventType.Save => _saveContextSaveDepth,
			QuestSaveContextEventType.Load => _saveContextLoadDepth,
			QuestSaveContextEventType.Select => _saveContextSelectDepth,
			QuestSaveContextEventType.Switch => _saveContextSwitchDepth,
			_ => 0,
		};
	}

	private bool ApplySaveScopeMetadata(int avatarIndex, int slotIndex, bool useNewSaveSystem)
	{
		string newScope = BuildQuestScopeKey(avatarIndex, slotIndex, useNewSaveSystem);
		bool changed = !string.Equals(newScope, _currentSaveScopeKey, StringComparison.Ordinal);
		_currentAvatarIndex = avatarIndex;
		_currentSlotIndex = slotIndex;
		_currentUseNewSaveSystem = useNewSaveSystem;
		_currentSaveScopeKey = newScope;
		if (changed)
		{
			AIChatManager.logger.LogInfo((object)("[AIQuestManager] 切换任务存档作用域: " + _currentSaveScopeKey));
		}
		return changed;
	}

	private void ResetQuestPersistenceRuntimeState()
	{
		_pendingQuestMails.Clear();
		_questTaskMemories.Clear();
		_mainlineCampaign = null;
		_questProgramState = QuestProgramPersistence.CreateEmptyState();
		_pendingBiographyStoryGenerationRequested = false;
		_hasLoadedPersistence = false;
		_questPersistenceDirty = false;
		_saveContextSaveDepth = 0;
		_saveContextLoadDepth = 0;
		_saveContextSelectDepth = 0;
		_saveContextSwitchDepth = 0;
		_lastCompletedSaveContextFrame = -1;
		_lastCompletedSaveContextEventType = QuestSaveContextEventType.Unknown;
		_lastCompletedSaveContextAvatar = -1;
		_lastCompletedSaveContextSlot = -1;
		_lastCompletedSaveContextUseNewSaveSystem = true;
	}

	private void LoadQuestSnapshotForCurrentScope()
	{
		TryAutoMigrateLegacyPersistence();
		LoadQuests();
		MainlineChapter chapter = _mainlineCampaign?.GetCurrentChapter();
		PendingBiographyEventState pendingEvent = chapter?.PendingBiographyEvent;
		_pendingBiographyStoryGenerationRequested = ShouldRequestPendingBiographyGeneration(chapter, pendingEvent);
		_hasLoadedPersistence = true;
		_questPersistenceDirty = false;
	}

	private static string BuildQuestScopeKey(int avatarIndex, int slotIndex, bool useNewSaveSystem)
	{
		int safeAvatar = Mathf.Max(0, avatarIndex);
		int safeSlot = Mathf.Max(0, slotIndex);
		string prefix = (useNewSaveSystem ? "new" : "old");
		return $"{prefix}_avatar{safeAvatar}_slot{safeSlot}";
	}

	internal static string FormatMainlineGenerationFailureForTest(string stageName, int currentStep, int totalSteps, string responseText, Exception ex)
	{
		return FormatMainlineGenerationFailure(stageName, currentStep, totalSteps, responseText, ex);
	}

	private async Task<AITextCallResult> CallAIAsync(BaseAIClient client, List<ChatMessage> messages, string stageName)
	{
		if (_questProgramTestAiResponses.Count > 0)
		{
			AITextCallResult queuedResult = _questProgramTestAiResponses.Dequeue();
			if (queuedResult?.Usage != null && queuedResult.Usage.HasUsage)
			{
				AIChatManager.RecordTokenUsage(queuedResult.Usage, "quest_generation");
			}
			AIChatManager.logger.LogInfo((object)("[AIQuestManager] " + stageName + " 测试返回预览: " + SafeLogPreview(queuedResult?.Content, 2000)));
			return queuedResult;
		}
		ChatRequest request = new ChatRequest
		{
			Messages = messages
		};
		AIChatManager.logger.LogInfo((object)$"[AIQuestManager] {stageName} 请求摘要: messageCount={messages?.Count ?? 0}, promptPreview={BuildQuestGenerationMessagePreview(messages)}");
		OpenAIResponse response;
		try
		{
			response = await client.GenerateResponseAsync(request);
		}
		catch (Exception ex)
		{
			AIChatManager.logger.LogError((object)(FormatMainlineGenerationFailure(stageName, 0, 0, null, ex) + ", promptPreview=" + BuildQuestGenerationMessagePreview(messages)));
			throw;
		}
		if (response?.Choices == null || response.Choices.Count == 0)
		{
			throw new Exception("AI 未返回有效内容。");
		}
		if (response.Usage != null && response.Usage.HasUsage)
		{
			AIChatManager.RecordTokenUsage(response.Usage, "quest_generation");
		}
		return new AITextCallResult
		{
			Content = response.Choices[0].Message.Content,
			Usage = response.Usage?.Clone()
		};
	}

	private async Task<MainlineStageExecutionResult<T>> RunRepairableMainlineStageAsync<T>(BaseAIClient client, List<ChatMessage> initialMessages, string stageName, int currentStep, int totalSteps, float repairProgress, string repairProgressMessage, Action<string, int, int, float> onProgress, Func<string, MainlineStageParseResult<T>> tryParse, Func<string, string, List<ChatMessage>> buildRepairMessages, int rawPreviewLength = 4000)
	{
		AITextCallResult firstResult = await CallAIAsync(client, initialMessages, stageName);
		MainlineStageExecutionResult<T> executionResult = new MainlineStageExecutionResult<T>
		{
			ResponseText = firstResult?.Content
		};
		if (firstResult?.Usage != null)
		{
			executionResult.Usage.Add(firstResult.Usage);
		}
		AIChatManager.logger.LogInfo((object)("[AIQuestManager] " + stageName + " 原始返回: " + SafeLogPreview(executionResult.ResponseText, rawPreviewLength)));
		MainlineStageParseResult<T> parsed = tryParse(executionResult.ResponseText);
		if (parsed != null && string.IsNullOrWhiteSpace(parsed.Error))
		{
			executionResult.Value = parsed.Value;
			return executionResult;
		}
		string firstError = parsed?.Error ?? (stageName + " 输出不合法");
		AIChatManager.logger.LogWarning((object)("[AIQuestManager] " + stageName + " 输出不合法，正在请求修正: error=" + firstError + ", responsePreview=" + SafeLogPreview(executionResult.ResponseText, 3000)));
		ReportQuestGenerationProgress(onProgress, repairProgressMessage, currentStep, totalSteps, repairProgress);
		AITextCallResult repairResult = await CallAIAsync(client, buildRepairMessages(executionResult.ResponseText, firstError), stageName + " 修正");
		executionResult.UsedRepair = true;
		executionResult.ResponseText = repairResult?.Content;
		if (repairResult?.Usage != null)
		{
			executionResult.Usage.Add(repairResult.Usage);
		}
		AIChatManager.logger.LogInfo((object)("[AIQuestManager] " + stageName + " 修正后原始返回: " + SafeLogPreview(executionResult.ResponseText, rawPreviewLength)));
		parsed = tryParse(executionResult.ResponseText);
		if (parsed != null && string.IsNullOrWhiteSpace(parsed.Error))
		{
			executionResult.Value = parsed.Value;
			return executionResult;
		}
		throw new MainlineRepairableStageException(parsed?.Error ?? (stageName + " 修正后输出仍不合法"), executionResult.ResponseText);
	}

	private static MainlineStageParseResult<T> SucceedMainlineStageParse<T>(T value)
	{
		return new MainlineStageParseResult<T>
		{
			Value = value
		};
	}

	private static MainlineStageParseResult<T> FailMainlineStageParse<T>(string error)
	{
		return new MainlineStageParseResult<T>
		{
			Error = (error ?? "输出不合法")
		};
	}

	private bool HasQueuedQuestProgramAiResponsesForTest()
	{
		return _questProgramTestAiResponses.Count > 0;
	}

	private void StartManagedCoroutine(IEnumerator coroutine)
	{
		if (coroutine == null)
		{
			return;
		}
		if (HasQueuedQuestProgramAiResponsesForTest())
		{
			while (coroutine.MoveNext())
			{
			}
		}
		else
		{
			StartCoroutine(coroutine);
		}
	}

	private JObject ParseJsonObjectResponse(string responseText, string stageName)
	{
		string json = ExtractFirstJsonObject(responseText);
		if (string.IsNullOrWhiteSpace(json))
		{
			throw new Exception(stageName + " 未返回有效 JSON。");
		}
		return JObject.Parse(json);
	}

	private bool TryParseJsonObjectResponse(string responseText, string stageName, out JObject obj, out string error)
	{
		obj = null;
		error = null;
		try
		{
			obj = ParseJsonObjectResponse(responseText, stageName);
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	private string ExtractFirstJsonObject(string responseText)
	{
		if (string.IsNullOrWhiteSpace(responseText))
		{
			return null;
		}
		string text = responseText.Trim();
		if (text.StartsWith("```"))
		{
			int firstNewLine = text.IndexOf('\n');
			if (firstNewLine >= 0)
			{
				text = text.Substring(firstNewLine + 1);
			}
			if (text.EndsWith("```"))
			{
				text = text.Substring(0, text.Length - 3);
			}
			text = text.Trim();
		}
		int start = text.IndexOf('{');
		if (start < 0)
		{
			return null;
		}
		int depth = 0;
		bool inString = false;
		bool escaped = false;
		for (int i = start; i < text.Length; i++)
		{
			char ch = text[i];
			if (inString)
			{
				if (escaped)
				{
					escaped = false;
					continue;
				}
				switch (ch)
				{
				case '\\':
					escaped = true;
					break;
				case '"':
					inString = false;
					break;
				}
				continue;
			}
			switch (ch)
			{
			case '"':
				inString = true;
				break;
			case '{':
				depth++;
				break;
			case '}':
				depth--;
				if (depth == 0)
				{
					return text.Substring(start, i - start + 1);
				}
				break;
			}
		}
		return null;
	}

	public bool AcceptQuestProgram(string programId)
	{
		QuestProgram program = GetQuestProgramById(programId);
		if (program == null)
		{
			return false;
		}
		if (program.Status == QuestProgramStatus.Active)
		{
			return true;
		}
		if (program.Status != QuestProgramStatus.Draft && program.Status != QuestProgramStatus.Suspended)
		{
			return false;
		}
		program.Status = QuestProgramStatus.Active;
		program.Priority = GetMainlineQuestPriority(program?.Priority ?? 0);
		program.Runtime = program.Runtime ?? new QuestRuntimeState();
		program.Runtime.ActivatedNodeIds = program.Runtime.ActivatedNodeIds ?? new List<string>();
		TryInitializeQuestProgramTiming(program);
		QuestNode firstNode = program.Nodes?.FirstOrDefault((QuestNode node) => node != null && !string.IsNullOrWhiteSpace(node.NodeId));
		if (firstNode != null)
		{
			program.Runtime.CurrentNodeId = firstNode.NodeId;
			if (!program.Runtime.ActivatedNodeIds.Contains(firstNode.NodeId))
			{
				program.Runtime.ActivatedNodeIds.Add(firstNode.NodeId);
			}
		}
		UpsertQuestProgram(program);
		ProcessQuestProgramRuntimeEvent(new QuestRuntimeEvent
		{
			EventType = "program.accepted",
			PrimaryId = program.ProgramId,
			Payload = new Dictionary<string, string>(),
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out var now) ? now : DateTime.Now)
		}, persistChanges: false);
		MarkQuestPersistenceDirty();
		return true;
	}

	public bool RejectQuestProgram(string programId)
	{
		bool removed = RemoveQuestProgram(programId);
		if (removed)
		{
			MarkQuestPersistenceDirty();
		}
		return removed;
	}

	public List<QuestProgram> GetPendingQuestPrograms()
	{
		TryLoadPersistenceIfReady();
		return (from program in EnsureQuestProgramState().Programs
			where program != null && program.TrackType == QuestTrackType.Mainline && program.Status == QuestProgramStatus.Draft
			orderby program.Priority descending
			select program).ThenBy((QuestProgram program) => program.Title ?? string.Empty, StringComparer.Ordinal).ToList();
	}

	public bool HasActiveQuest(int npcId)
	{
		TryLoadPersistenceIfReady();
		return HasQuestProgramForAnchorNpc(npcId, QuestProgramStatus.Active);
	}

	internal MainlineGenerationStartResult StartGenerateMainline(NPCInfo npcInfo, Action<QuestProgram> onComplete, Action<string> onError, Action<string, int, int, float> onProgress, out string statusMessage)
	{
		statusMessage = null;
		if (_isGenerating)
		{
			statusMessage = "正在生成中，请稍等...";
			onError?.Invoke(statusMessage);
			return MainlineGenerationStartResult.Rejected;
		}
		if (npcInfo == null || npcInfo.NpcID <= 0)
		{
			statusMessage = "无法生成主线：NPC 信息为空。";
			onError?.Invoke(statusMessage);
			return MainlineGenerationStartResult.Rejected;
		}
		string validationError;
		MainlineGenerationStartResult validationResult = TryValidateMainlineProgramGeneration(npcInfo, out validationError);
		if (validationResult != MainlineGenerationStartResult.Started)
		{
			statusMessage = validationError;
			if (validationResult == MainlineGenerationStartResult.Rejected)
			{
				onError?.Invoke(validationError);
			}
			return validationResult;
		}
		StartManagedCoroutine(GenerateOrResumeMainlineCoroutine(npcInfo, onComplete, onError, onProgress));
		return MainlineGenerationStartResult.Started;
	}

	internal MainlineGenerationStartResult StartGenerateMainline(NPCInfo npcInfo, Action<QuestProgram> onComplete, Action<string> onError, Action<string, int, int, float> onProgress = null)
	{
		string statusMessage;
		return StartGenerateMainline(npcInfo, onComplete, onError, onProgress, out statusMessage);
	}

	private MainlineGenerationStartResult TryValidateMainlineProgramGeneration(NPCInfo npcInfo, out string error)
	{
		if (_mainlineCampaign != null && _mainlineCampaign.Status == CampaignStatus.Active && _mainlineCampaign.SeedNpcId > 0 && _mainlineCampaign.SeedNpcId != npcInfo?.NpcID)
		{
			error = "当前主线已绑定 " + _mainlineCampaign.SeedNpcName + "，请先推进或结束当前主线。";
			return MainlineGenerationStartResult.Rejected;
		}
		if (HasMainlineQuestProgram(QuestProgramStatus.Active))
		{
			error = "已有进行中的主线 beat，请先完成当前主线。";
			return MainlineGenerationStartResult.Rejected;
		}
		if (HasMainlineQuestProgram(QuestProgramStatus.Draft))
		{
			error = "已有待接取的主线 beat，请先接受或拒绝。";
			return MainlineGenerationStartResult.Rejected;
		}
		string status;
		bool alreadyWaitingForBiographyEvent = IsWaitingMainlineAnchorForNpc(npcInfo?.NpcID ?? 0, out status);
		MainlineCampaign campaign = EnsureMainlineCampaignShell(npcInfo);
		EnsureCurrentMainlineChapter(campaign, npcInfo);
		PendingBiographyEventState pendingEvent = campaign?.GetCurrentChapter()?.PendingBiographyEvent;
		if (pendingEvent == null || pendingEvent.NpcId != npcInfo?.NpcID)
		{
			if (!alreadyWaitingForBiographyEvent)
			{
				MarkQuestPersistenceDirty();
				SaveQuests();
			}
			error = BuildWaitingMainlineAnchorStatusText(campaign, npcInfo);
			return MainlineGenerationStartResult.WaitingForBiographyEvent;
		}
		error = null;
		return MainlineGenerationStartResult.Started;
	}

	internal MainlineGenerationStartResult TryValidateMainlineProgramGenerationForTest(NPCInfo npcInfo, out string error)
	{
		return TryValidateMainlineProgramGeneration(npcInfo, out error);
	}

	internal bool IsWaitingMainlineAnchorForNpcForTest(int npcId, out string status)
	{
		return IsWaitingMainlineAnchorForNpc(npcId, out status);
	}

	internal bool IsWaitingMainlineAnchorForNpc(int npcId, out string status)
	{
		TryLoadPersistenceIfReady();
		status = string.Empty;
		if (npcId <= 0)
		{
			return false;
		}
		MainlineCampaign campaign = _mainlineCampaign;
		if (campaign == null || campaign.Status == CampaignStatus.Completed || campaign.Status == CampaignStatus.Failed || campaign.SeedNpcId != npcId)
		{
			return false;
		}
		if (HasMainlineQuestProgram(QuestProgramStatus.Active) || HasMainlineQuestProgram(QuestProgramStatus.Draft))
		{
			return false;
		}
		MainlineChapter chapter = campaign.GetCurrentChapter();
		if (chapter == null || chapter.Status == MainlineChapterStatus.Completed || chapter.Status == MainlineChapterStatus.Failed || chapter.PendingBiographyEvent != null)
		{
			return false;
		}
		status = BuildWaitingMainlineAnchorStatusText(campaign, null);
		return !string.IsNullOrWhiteSpace(status);
	}

	internal void QueueQuestProgramAiResponsesForTest(params string[] responseContents)
	{
		if (responseContents != null)
		{
			foreach (string responseContent in responseContents)
			{
				_questProgramTestAiResponses.Enqueue(new AITextCallResult
				{
					Content = (responseContent ?? string.Empty)
				});
			}
		}
	}

	internal void ClearQuestProgramAiResponsesForTest()
	{
		_questProgramTestAiResponses.Clear();
	}

	public void NotifyTalkToNpc(int npcId)
	{
		if (!_talkFlags.ContainsKey(npcId))
		{
			_talkFlags[npcId] = true;
		}
		else
		{
			_talkFlags[npcId] = true;
		}
		PublishQuestProgramRuntimeEvent("npc.talked", npcId.ToString());
	}

	internal void NotifyCyFriendRelationChanged(int npcId)
	{
		if (npcId > 0)
		{
			PublishQuestProgramRuntimeEvent("npc.cy_friend_changed", npcId.ToString());
		}
	}

	public void NotifyBattleVictory(int npcId)
	{
		if (npcId > 0)
		{
			_recentBattleVictories[npcId] = Time.realtimeSinceStartup;
			AIChatManager.logger.LogInfo((object)$"[AIQuestManager] 战斗胜利已记录: Target={npcId}");
			PublishQuestProgramRuntimeEvent("combat.victory", npcId.ToString(), new Dictionary<string, string> { ["npcId"] = npcId.ToString() });
		}
	}

	public List<QuestProgram> GetActiveQuestPrograms()
	{
		TryLoadPersistenceIfReady();
		return (from program in EnsureQuestProgramState().Programs
			where program != null && program.TrackType == QuestTrackType.Mainline && program.Status == QuestProgramStatus.Active
			orderby GetMainlineQuestPriority(program?.Priority ?? 0) descending
			select program).ToList();
	}

	public List<QuestProgram> GetQuestProgramHistory()
	{
		TryLoadPersistenceIfReady();
		return EnsureQuestProgramState().Programs.Where((QuestProgram program) => program != null && program.TrackType == QuestTrackType.Mainline && (program.Status == QuestProgramStatus.Completed || program.Status == QuestProgramStatus.Failed || program.Status == QuestProgramStatus.Suspended)).ToList();
	}

	internal string BuildCurrentProgramGuideText(QuestProgram program)
	{
		if (program == null)
		{
			return string.Empty;
		}
		QuestNode node = ResolveCurrentQuestProgramNode(program);
		if (node == null)
		{
			return SanitizePlayerFacingQuestText(string.IsNullOrWhiteSpace(program.Summary) ? "等待节点激活" : program.Summary.Trim());
		}
		if (!string.IsNullOrWhiteSpace(node.Summary))
		{
			return SanitizePlayerFacingQuestText(node.Summary);
		}
		if (!string.IsNullOrWhiteSpace(node.Title))
		{
			return SanitizePlayerFacingQuestText(node.Title);
		}
		return SanitizePlayerFacingQuestText(string.IsNullOrWhiteSpace(program.Summary) ? "等待节点激活" : program.Summary.Trim());
	}

	internal string BuildCurrentProgramStatusText(QuestProgram program)
	{
		if (program == null)
		{
			return string.Empty;
		}
		switch (program.Status)
		{
		case QuestProgramStatus.Completed:
			return SanitizePlayerFacingQuestText("已完成");
		case QuestProgramStatus.Failed:
			return SanitizePlayerFacingQuestText(string.IsNullOrWhiteSpace(program.Runtime?.FailedReason) ? "已失败" : ("已失败：" + program.Runtime.FailedReason.Trim()));
		case QuestProgramStatus.Suspended:
			return SanitizePlayerFacingQuestText(string.IsNullOrWhiteSpace(program.Runtime?.FailedReason) ? "已挂起" : ("已挂起：" + program.Runtime.FailedReason.Trim()));
		default:
		{
			QuestNode node = ResolveCurrentQuestProgramNode(program);
			if (node == null)
			{
				return SanitizePlayerFacingQuestText("等待节点激活");
			}
			NormalizeQuestProgramRuntimeState(program);
			if (!EvaluateQuestProgramPredicates(node.ActivationPredicates))
			{
				string activationHint = BuildQuestProgramPredicateCoreHint(program, node.ActivationPredicates);
				return SanitizePlayerFacingQuestText(string.IsNullOrWhiteSpace(activationHint) ? "待满足前置条件" : ("待满足：" + activationHint));
			}
			if (program.Runtime.PendingNarrativeNodeIds.Contains(node.NodeId))
			{
				return SanitizePlayerFacingQuestText("待播放演出");
			}
			if (node.Type == QuestNodeType.Narrative || node.Type == QuestNodeType.Choice)
			{
				if (!EvaluateQuestProgramPredicates(node.Narrative?.Gates))
				{
					string gateHint = BuildQuestProgramPredicateCoreHint(program, node.Narrative?.Gates);
					return SanitizePlayerFacingQuestText(string.IsNullOrWhiteSpace(gateHint) ? "待触发演出" : ("待触发演出：" + gateHint));
				}
				return SanitizePlayerFacingQuestText((node.Narrative?.Choices != null && node.Narrative.Choices.Count > 0) ? "等待选择分支" : "演出进行中");
			}
			if (!EvaluateQuestProgramPredicates(node.CompletionPredicates))
			{
				string completionHint = BuildQuestProgramPredicateCoreHint(program, node.CompletionPredicates);
				return SanitizePlayerFacingQuestText(string.IsNullOrWhiteSpace(completionHint) ? "进行中" : ("待完成：" + completionHint));
			}
			if (node.Type == QuestNodeType.Terminal)
			{
				return SanitizePlayerFacingQuestText("节点完成，等待收束");
			}
			return SanitizePlayerFacingQuestText(string.IsNullOrWhiteSpace(node.NextNodeId) ? "节点完成" : "节点完成，等待进入下一节点");
		}
		}
	}

	internal string BuildCurrentProgramRemainingTimeText(QuestProgram program)
	{
		if (!TryGetQuestProgramRemainingTime(program, out var years, out var months, out var days))
		{
			return string.Empty;
		}
		return $"剩余时间:{years}年{months}月{days}日";
	}

	internal PendingBiographyEventState GetPendingMainlineBiographyEvent()
	{
		TryLoadPersistenceIfReady();
		return _mainlineCampaign?.GetCurrentChapter()?.PendingBiographyEvent;
	}

	internal string BuildPendingMainlineBiographyStatusText()
	{
		PendingBiographyEventState pendingEvent = GetPendingMainlineBiographyEvent();
		if (pendingEvent == null)
		{
			return string.Empty;
		}
		if (!string.IsNullOrWhiteSpace(pendingEvent.LastGenerationError))
		{
			return "当前剧情生成失败：" + pendingEvent.LastGenerationError.Trim();
		}
		if (pendingEvent.GenerationAttempted)
		{
			return "正在改写当前生平事件";
		}
		return (pendingEvent.Status == PendingBiographyEventStatus.ActiveStory) ? "当前剧情正在进行" : "等待拦截的官方生平事件改写为剧情";
	}

	internal string BuildPendingMainlineBiographyGuideText()
	{
		PendingBiographyEventState pendingEvent = GetPendingMainlineBiographyEvent();
		if (pendingEvent == null)
		{
			return string.Empty;
		}
		if (!string.IsNullOrWhiteSpace(pendingEvent.PlayerFacingHint))
		{
			return pendingEvent.PlayerFacingHint.Trim();
		}
		return pendingEvent.Summary?.Trim() ?? string.Empty;
	}

	internal List<OfficialTaskUiEntry> BuildOfficialTaskUiEntriesForTest()
	{
		return BuildOfficialTaskUiEntries();
	}

	internal List<OfficialTaskUiEntry> BuildOfficialTaskUiEntries()
	{
		TryLoadPersistenceIfReady();
		List<OfficialTaskUiEntry> entries = new List<OfficialTaskUiEntry>();
		foreach (QuestProgram program in from questProgram in GetActiveQuestPrograms()
			where questProgram != null
			select questProgram)
		{
			entries.Add(BuildOfficialTaskUiEntry(program));
		}
		if (entries.Count == 0)
		{
			MainlineCampaign campaign = GetMainlineCampaign();
			MainlineChapter chapter = campaign?.GetCurrentChapter();
			PendingBiographyEventState pendingEvent = chapter?.PendingBiographyEvent;
			if (pendingEvent != null)
			{
				entries.Add(BuildWaitingPendingBiographyOfficialTaskUiEntry(campaign, chapter, pendingEvent));
			}
		}
		return entries;
	}

	private OfficialTaskUiEntry BuildOfficialTaskUiEntry(QuestProgram program)
	{
		string guideText = SanitizePlayerFacingQuestText(BuildCurrentProgramGuideText(program));
		string statusText = SanitizePlayerFacingQuestText(BuildCurrentProgramStatusText(program));
		string remainingTimeText = SanitizePlayerFacingQuestText(BuildCurrentProgramRemainingTimeText(program));
		string summaryText = SanitizePlayerFacingQuestText(program?.Summary?.Trim() ?? string.Empty);
		return new OfficialTaskUiEntry
		{
			EntryId = "mainline:program:" + program?.ProgramId,
			Kind = OfficialTaskUiEntryKind.ActiveProgram,
			Title = SanitizePlayerFacingQuestText(string.IsNullOrWhiteSpace(program?.Title) ? "主线" : program.Title.Trim()),
			StatusText = statusText,
			GuideText = guideText,
			RemainingTimeText = remainingTimeText,
			DescriptionText = BuildOfficialTaskUiDescription(summaryText, guideText, statusText, remainingTimeText),
			ProgramId = (program?.ProgramId ?? string.Empty)
		};
	}

	private OfficialTaskUiEntry BuildWaitingPendingBiographyOfficialTaskUiEntry(MainlineCampaign campaign, MainlineChapter chapter, PendingBiographyEventState pendingEvent)
	{
		string guideText = SanitizePlayerFacingQuestText(BuildPendingMainlineBiographyGuideText());
		string statusText = SanitizePlayerFacingQuestText(BuildPendingMainlineBiographyStatusText());
		string title = ((!string.IsNullOrWhiteSpace(chapter?.Title)) ? ("主线 · " + chapter.Title.Trim()) : ((!string.IsNullOrWhiteSpace(campaign?.Title)) ? campaign.Title.Trim() : "主线"));
		return new OfficialTaskUiEntry
		{
			EntryId = "mainline:waiting:" + pendingEvent?.EventId,
			Kind = OfficialTaskUiEntryKind.WaitingPendingBiography,
			Title = SanitizePlayerFacingQuestText(title),
			StatusText = statusText,
			GuideText = guideText,
			RemainingTimeText = string.Empty,
			DescriptionText = BuildOfficialTaskUiDescription(SanitizePlayerFacingQuestText(pendingEvent?.Summary?.Trim() ?? string.Empty), guideText, statusText, string.Empty),
			ProgramId = string.Empty
		};
	}

	private static string BuildOfficialTaskUiDescription(string summaryText, string guideText, string statusText, string remainingTimeText)
	{
		if (!string.IsNullOrWhiteSpace(summaryText))
		{
			return SanitizePlayerFacingQuestText(summaryText);
		}
		if (!string.IsNullOrWhiteSpace(guideText))
		{
			return SanitizePlayerFacingQuestText(guideText);
		}
		if (!string.IsNullOrWhiteSpace(statusText))
		{
			return SanitizePlayerFacingQuestText(statusText);
		}
		return SanitizePlayerFacingQuestText(remainingTimeText);
	}

	private static string BuildWaitingMainlineAnchorStatusText(MainlineCampaign campaign, NPCInfo npcInfo)
	{
		string anchorName = ((!string.IsNullOrWhiteSpace(campaign?.SeedNpcName)) ? campaign.SeedNpcName.Trim() : npcInfo?.Name?.Trim());
		if (string.IsNullOrWhiteSpace(anchorName))
		{
			anchorName = "该NPC";
		}
		return "已将" + anchorName + "设为当前主线锚点，等待其下一条官方生平事件被改写。";
	}

	internal MainlineCampaign GetMainlineCampaign()
	{
		TryLoadPersistenceIfReady();
		return _mainlineCampaign;
	}

	private bool HasPendingMainlineBiographyEvent()
	{
		return _mainlineCampaign?.GetCurrentChapter()?.PendingBiographyEvent != null;
	}

	private bool HasMainlineRuntimeWork()
	{
		return HasActiveQuestPrograms() || HasPendingMainlineBiographyEvent();
	}

	private QuestNode ResolveCurrentQuestProgramNode(QuestProgram program)
	{
		if (program?.Nodes == null || program.Nodes.Count == 0)
		{
			return null;
		}
		if (!string.IsNullOrWhiteSpace(program.Runtime?.CurrentNodeId))
		{
			QuestNode currentNode = program.Nodes.FirstOrDefault((QuestNode node) => node != null && string.Equals(node.NodeId, program.Runtime.CurrentNodeId, StringComparison.Ordinal));
			if (currentNode != null)
			{
				return currentNode;
			}
		}
		return program.Nodes.FirstOrDefault((QuestNode node) => node != null && !string.IsNullOrWhiteSpace(node.NodeId));
	}

	private string BuildQuestProgramPredicateHint(QuestProgram program, IEnumerable<QuestPredicateInvocation> predicates)
	{
		string coreHint = BuildQuestProgramPredicateCoreHint(program, predicates);
		if (string.IsNullOrWhiteSpace(coreHint))
		{
			return string.Empty;
		}
		return "当前前置：" + coreHint;
	}

	private string BuildQuestProgramPredicateCoreHint(QuestProgram program, IEnumerable<QuestPredicateInvocation> predicates)
	{
		if (predicates == null)
		{
			return string.Empty;
		}
		List<string> hints = new List<string>();
		foreach (QuestPredicateInvocation predicate in predicates)
		{
			string hint = DescribeQuestProgramPredicate(program, predicate);
			if (!string.IsNullOrWhiteSpace(hint))
			{
				hints.Add(hint);
			}
		}
		return (hints.Count == 0) ? string.Empty : string.Join("；", hints.Distinct());
	}

	private string DescribeQuestProgramPredicate(QuestProgram program, QuestPredicateInvocation predicate)
	{
		if (predicate == null || string.IsNullOrWhiteSpace(predicate.Opcode))
		{
			return string.Empty;
		}
		Dictionary<string, string> parameters = predicate.Params ?? new Dictionary<string, string>();
		switch (predicate.Opcode.Trim())
		{
		case "logic.all":
			return DescribeQuestProgramCompositePredicate(program, predicate.Children, "且");
		case "logic.any":
			return DescribeQuestProgramCompositePredicate(program, predicate.Children, "或");
		case "logic.not":
		{
			string negatedHint = DescribeQuestProgramCompositePredicate(program, predicate.Children, "且");
			return string.IsNullOrWhiteSpace(negatedHint) ? string.Empty : ("不得满足：" + negatedHint);
		}
		case "scene.is":
		{
			string resolvedSceneName = ResolveQuestProgramPredicateSceneName(parameters);
			if (!string.IsNullOrWhiteSpace(resolvedSceneName))
			{
				return "前往" + resolvedSceneName;
			}
			return string.Empty;
		}
		case "time.compare":
		{
			string opText;
			string op = (parameters.TryGetValue("operator", out opText) ? opText : string.Empty);
			string timeValue;
			string value = (parameters.TryGetValue("value", out timeValue) ? timeValue : string.Empty);
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}
			return string.IsNullOrWhiteSpace(op) ? ("等待时间达到 " + value.Trim()) : ("时间需 " + op.Trim() + " " + value.Trim());
		}
		case "time.window":
		{
			string rawStartText;
			string startText = ((!parameters.TryGetValue("start", out rawStartText)) ? string.Empty : rawStartText?.Trim());
			string rawEndText;
			string endText = ((!parameters.TryGetValue("end", out rawEndText)) ? string.Empty : rawEndText?.Trim());
			if (!string.IsNullOrWhiteSpace(startText) && !string.IsNullOrWhiteSpace(endText))
			{
				return "时间需处于 " + startText + " 至 " + endText;
			}
			if (!string.IsNullOrWhiteSpace(startText))
			{
				return "时间需不早于 " + startText;
			}
			if (!string.IsNullOrWhiteSpace(endText))
			{
				return "时间需不晚于 " + endText;
			}
			return string.Empty;
		}
		case "fuben.remaining_days":
		{
			string rawRemainOperator;
			string remainOperator = (parameters.TryGetValue("operator", out rawRemainOperator) ? rawRemainOperator : ">=");
			string rawRemainDays;
			string remainDays = (parameters.TryGetValue("days", out rawRemainDays) ? rawRemainDays : string.Empty);
			return string.IsNullOrWhiteSpace(remainDays) ? string.Empty : ("副本剩余天数需 " + remainOperator.Trim() + " " + remainDays.Trim());
		}
		case "scene.type_is":
		{
			string rawSceneTypeName;
			string sceneTypeName = ((!parameters.TryGetValue("sceneTypeName", out rawSceneTypeName)) ? string.Empty : rawSceneTypeName?.Trim());
			string rawSceneTypeValue;
			string sceneTypeValue = ((!parameters.TryGetValue("sceneType", out rawSceneTypeValue)) ? string.Empty : rawSceneTypeValue?.Trim());
			if (!string.IsNullOrWhiteSpace(sceneTypeName))
			{
				return "当前场景类型需为" + sceneTypeName;
			}
			return string.IsNullOrWhiteSpace(sceneTypeValue) ? string.Empty : ("当前场景类型需为 " + sceneTypeValue);
		}
		case "npc.alive":
		{
			string aliveNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(aliveNpcName))
			{
				return aliveNpcName + "必须存活";
			}
			return string.Empty;
		}
		case "npc.dead":
		{
			string deadNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(deadNpcName))
			{
				return deadNpcName + "必须死亡";
			}
			return string.Empty;
		}
		case "npc.status_is":
		{
			string rawStatusId;
			string statusText = ((!parameters.TryGetValue("statusId", out rawStatusId)) ? string.Empty : rawStatusId?.Trim());
			string statusNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(statusNpcName))
			{
				return string.IsNullOrWhiteSpace(statusText) ? (statusNpcName + "必须进入指定状态") : (statusNpcName + "必须进入状态 " + statusText);
			}
			return string.Empty;
		}
		case "npc.action_is":
		{
			string rawActionId;
			string actionText = ((!parameters.TryGetValue("actionId", out rawActionId)) ? string.Empty : rawActionId?.Trim());
			string actionNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(actionNpcName))
			{
				return string.IsNullOrWhiteSpace(actionText) ? (actionNpcName + "必须切换到指定行为") : (actionNpcName + "必须切换到行为 " + actionText);
			}
			return string.Empty;
		}
		case "npc.talked_to_player":
		{
			string talkedNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(talkedNpcName))
			{
				return "与" + talkedNpcName + "交谈";
			}
			return string.Empty;
		}
		case "npc.cy_friend":
		{
			string cyNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(cyNpcName))
			{
				return "先与" + cyNpcName + "结为传音好友";
			}
			return string.Empty;
		}
		case "npc.present_in_current_scene":
		{
			string presentNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(presentNpcName))
			{
				return presentNpcName + "必须在当前地点";
			}
			return string.Empty;
		}
		case "npc.patrol_present":
		{
			string patrolNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(patrolNpcName))
			{
				return patrolNpcName + "必须在当前副本节点巡逻到场";
			}
			return string.Empty;
		}
		case "scene.npc_present":
		{
			string targetSceneName = ResolveQuestProgramPredicateSceneName(parameters);
			string sceneNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(sceneNpcName))
			{
				return string.IsNullOrWhiteSpace(targetSceneName) ? (sceneNpcName + "必须在指定地点") : (sceneNpcName + "必须在" + targetSceneName);
			}
			return string.Empty;
		}
		case "combat.victory_over_npc":
		{
			string battleNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(battleNpcName))
			{
				return "击败" + battleNpcName;
			}
			return string.Empty;
		}
		case "mail.delivery_submitted":
		{
			string rawDeliveryItemName;
			string deliveryItemName = (parameters.TryGetValue("itemName", out rawDeliveryItemName) ? rawDeliveryItemName : string.Empty);
			string deliveryNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(deliveryNpcName))
			{
				return string.IsNullOrWhiteSpace(deliveryItemName) ? ("通过传音向" + deliveryNpcName + "提交任务物品") : ("通过传音向" + deliveryNpcName + "提交" + deliveryItemName.Trim());
			}
			if (!string.IsNullOrWhiteSpace(deliveryItemName))
			{
				return "通过传音提交" + deliveryItemName.Trim();
			}
			return "通过传音提交任务物品";
		}
		case "player.has_item":
		{
			string rawItemName;
			string itemName = (parameters.TryGetValue("itemName", out rawItemName) ? rawItemName : string.Empty);
			string rawItemCount;
			string itemCount = (parameters.TryGetValue("count", out rawItemCount) ? rawItemCount : "1");
			if (!string.IsNullOrWhiteSpace(itemName))
			{
				return "持有" + itemCount.Trim() + "个" + itemName.Trim();
			}
			if (parameters.TryGetValue("itemId", out var itemId) && !string.IsNullOrWhiteSpace(itemId))
			{
				return "持有" + itemCount.Trim() + "个物品 " + itemId.Trim();
			}
			return string.Empty;
		}
		case "player.money_compare":
		{
			string rawMoneyOperator;
			string moneyOperator = (parameters.TryGetValue("operator", out rawMoneyOperator) ? rawMoneyOperator : ">=");
			string rawAmount;
			string amount = (parameters.TryGetValue("amount", out rawAmount) ? rawAmount : string.Empty);
			if (string.IsNullOrWhiteSpace(amount))
			{
				return string.Empty;
			}
			return "灵石需 " + moneyOperator.Trim() + " " + amount.Trim();
		}
		case "player.level_compare":
		{
			string rawLevelOperator;
			string levelOperator = (parameters.TryGetValue("operator", out rawLevelOperator) ? rawLevelOperator : ">=");
			string rawLevelValue;
			string levelValue = (parameters.TryGetValue("level", out rawLevelValue) ? rawLevelValue : string.Empty);
			return string.IsNullOrWhiteSpace(levelValue) ? string.Empty : ("修为需 " + levelOperator.Trim() + " " + levelValue.Trim() + " 级");
		}
		case "player.has_skill":
		{
			if (parameters.TryGetValue("skillName", out var skillName) && !string.IsNullOrWhiteSpace(skillName))
			{
				return "掌握神通《" + skillName.Trim() + "》";
			}
			if (parameters.TryGetValue("skillId", out var skillId) && !string.IsNullOrWhiteSpace(skillId))
			{
				return "掌握神通 " + skillId.Trim();
			}
			return string.Empty;
		}
		case "player.has_static_skill":
		{
			if (parameters.TryGetValue("skillName", out var staticSkillName) && !string.IsNullOrWhiteSpace(staticSkillName))
			{
				return "掌握功法《" + staticSkillName.Trim() + "》";
			}
			if (parameters.TryGetValue("skillId", out var staticSkillId) && !string.IsNullOrWhiteSpace(staticSkillId))
			{
				return "掌握功法 " + staticSkillId.Trim();
			}
			return string.Empty;
		}
		case "player.favor_compare":
		{
			string rawFavorOperator;
			string favorOperator = (parameters.TryGetValue("operator", out rawFavorOperator) ? rawFavorOperator : ">=");
			string rawFavorValue;
			string favorValue = (parameters.TryGetValue("value", out rawFavorValue) ? rawFavorValue : string.Empty);
			if (string.IsNullOrWhiteSpace(favorValue))
			{
				return string.Empty;
			}
			string favorNpcName = ResolveQuestProgramPredicateNpcName(program, parameters);
			if (!string.IsNullOrWhiteSpace(favorNpcName))
			{
				return "与" + favorNpcName + "的好感需 " + favorOperator.Trim() + " " + favorValue.Trim();
			}
			return "目标好感需 " + favorOperator.Trim() + " " + favorValue.Trim();
		}
		case "task.official_not_outdated":
		{
			if (parameters.TryGetValue("taskName", out var taskName) && !string.IsNullOrWhiteSpace(taskName))
			{
				return taskName.Trim() + "必须仍未过时";
			}
			if (parameters.TryGetValue("taskId", out var taskId) && !string.IsNullOrWhiteSpace(taskId))
			{
				return "官方任务 " + taskId.Trim() + " 必须仍未过时";
			}
			return "绑定的官方任务必须仍未过时";
		}
		default:
			return string.Empty;
		}
	}

	private string DescribeQuestProgramCompositePredicate(QuestProgram program, IEnumerable<QuestPredicateInvocation> predicates, string separator)
	{
		if (predicates == null)
		{
			return string.Empty;
		}
		List<string> hints = (from predicate in predicates
			select DescribeQuestProgramPredicate(program, predicate) into hint
			where !string.IsNullOrWhiteSpace(hint)
			select hint).Distinct().ToList();
		if (hints.Count == 0)
		{
			return string.Empty;
		}
		return string.Join(string.IsNullOrWhiteSpace(separator) ? "且" : separator.Trim(), hints);
	}

	private string ResolveQuestProgramPredicateNpcName(QuestProgram program, Dictionary<string, string> parameters)
	{
		if (TryGetQuestProgramTextParam(parameters, "npcName", out var npcName))
		{
			return npcName;
		}
		if (!TryParseQuestProgramNpcId(parameters, out var npcId))
		{
			return string.Empty;
		}
		if (program != null && npcId == program.AnchorNpcId && !string.IsNullOrWhiteSpace(program.AnchorNpcName))
		{
			return program.AnchorNpcName.Trim();
		}
		string resolvedName = ResolveQuestProgramNpcName(npcId);
		return (string.IsNullOrWhiteSpace(resolvedName) || resolvedName.StartsWith("NPC_", StringComparison.Ordinal)) ? string.Empty : resolvedName;
	}

	private string ResolveQuestProgramPredicateSceneName(Dictionary<string, string> parameters)
	{
		if (TryGetQuestProgramTextParam(parameters, "sceneName", out var sceneName))
		{
			return sceneName;
		}
		if (!TryGetQuestProgramTextParam(parameters, "sceneId", out var sceneId))
		{
			return string.Empty;
		}
		string normalizedSceneId = sceneId.Trim();
		string hierarchy = QuestElementPicker.BuildSceneHierarchy(normalizedSceneId);
		if (!string.IsNullOrWhiteSpace(hierarchy) && !string.Equals(hierarchy, normalizedSceneId, StringComparison.Ordinal))
		{
			return hierarchy.Trim();
		}
		if (QuestElementPicker.TryGetOfficialLocationName(normalizedSceneId, out var officialSceneName) && !string.IsNullOrWhiteSpace(officialSceneName))
		{
			return officialSceneName.Trim();
		}
		return string.Empty;
	}

	private bool TryInitializeQuestProgramTiming(QuestProgram program)
	{
		if (program == null || !TryGetQuestProgramRuntimeGameTime(out var acceptedAt))
		{
			return false;
		}
		NormalizeQuestProgramRuntimeState(program);
		if (program.Runtime.AcceptedAtGameTime == DateTime.MinValue)
		{
			program.Runtime.AcceptedAtGameTime = acceptedAt;
		}
		if (program.Runtime.DeadlineGameTime != DateTime.MinValue)
		{
			return true;
		}
		PendingBiographyEventState pendingEvent = ResolvePendingBiographyEvent(program);
		if (pendingEvent != null && pendingEvent.DeadlineGameTime != DateTime.MinValue)
		{
			program.Runtime.DeadlineGameTime = pendingEvent.DeadlineGameTime;
			return true;
		}
		int maxSpanMonths = (ResolveQuestBeat(_mainlineCampaign, program.ChapterId, program.BeatId)?.TimeIntent?.MaxSpanMonths).GetValueOrDefault();
		if (maxSpanMonths <= 0)
		{
			return false;
		}
		program.Runtime.DeadlineGameTime = AddGameMonths(program.Runtime.AcceptedAtGameTime, maxSpanMonths);
		return true;
	}

	private bool TryGetQuestProgramRemainingTime(QuestProgram program, out int years, out int months, out int days)
	{
		years = 0;
		months = 0;
		days = 0;
		if (program == null || !TryGetQuestProgramRuntimeGameTime(out var now))
		{
			return false;
		}
		DateTime deadline = ResolveQuestProgramDeadline(program);
		if (deadline == DateTime.MinValue)
		{
			return false;
		}
		if (deadline <= now)
		{
			return true;
		}
		DateTime cursor = now;
		while (cursor.AddYears(1) <= deadline)
		{
			cursor = cursor.AddYears(1);
			years++;
		}
		while (cursor.AddMonths(1) <= deadline)
		{
			cursor = cursor.AddMonths(1);
			months++;
		}
		while (cursor.AddDays(1.0) <= deadline)
		{
			cursor = cursor.AddDays(1.0);
			days++;
		}
		return true;
	}

	private DateTime ResolveQuestProgramDeadline(QuestProgram program)
	{
		if (program != null && (program.Runtime?.DeadlineGameTime).HasValue && program.Runtime.DeadlineGameTime != DateTime.MinValue)
		{
			return program.Runtime.DeadlineGameTime;
		}
		PendingBiographyEventState pendingEvent = ResolvePendingBiographyEvent(program);
		if (pendingEvent != null && pendingEvent.DeadlineGameTime != DateTime.MinValue)
		{
			return pendingEvent.DeadlineGameTime;
		}
		int maxSpanMonths = (ResolveQuestBeat(_mainlineCampaign, program?.ChapterId, program?.BeatId)?.TimeIntent?.MaxSpanMonths).GetValueOrDefault();
		if (maxSpanMonths <= 0)
		{
			return DateTime.MinValue;
		}
		DateTime acceptedAt = program?.Runtime?.AcceptedAtGameTime ?? DateTime.MinValue;
		if (acceptedAt == DateTime.MinValue)
		{
			acceptedAt = _mainlineCampaign?.CreatedAt ?? DateTime.MinValue;
		}
		if (acceptedAt == DateTime.MinValue)
		{
			return DateTime.MinValue;
		}
		return AddGameMonths(acceptedAt, maxSpanMonths);
	}

	private PendingBiographyEventState ResolvePendingBiographyEvent(QuestProgram program)
	{
		if (program == null || _mainlineCampaign == null)
		{
			return null;
		}
		return ResolveQuestChapter(_mainlineCampaign, program.ChapterId)?.PendingBiographyEvent;
	}

	private bool PrepareQuestProgramCurrentNodeWorldState(QuestProgram program, QuestNode node)
	{
		if (program == null || node == null)
		{
			return false;
		}
		return PrepareQuestProgramNodeWorldControl(program, node);
	}

	private bool PrepareQuestProgramNodeWorldControl(QuestProgram program, QuestNode node)
	{
		if (program == null || node?.WorldControl == null)
		{
			return false;
		}
		string sceneId = node.WorldControl.SceneId?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(sceneId))
		{
			throw new InvalidOperationException("节点 " + node.NodeId + " 的 worldControl 缺少 sceneId");
		}
		List<QuestNpcPresenceRequirement> requirements = node.WorldControl.NpcRequirements ?? new List<QuestNpcPresenceRequirement>();
		if (requirements.Count == 0)
		{
			return false;
		}
		bool changed = false;
		foreach (QuestNpcPresenceRequirement requirement in requirements)
		{
			if (PrepareQuestProgramNpcPresenceRequirement(program, requirement, sceneId))
			{
				changed = true;
			}
		}
		return changed;
	}

	private bool PrepareQuestProgramNpcPresenceRequirement(QuestProgram program, QuestNpcPresenceRequirement requirement, string sceneId)
	{
		if (program == null || requirement == null)
		{
			return false;
		}
		bool changed = false;
		if (TryStageQuestProgramNpcToScene(program, requirement.NpcId, sceneId))
		{
			changed = true;
		}
		if (TrySetQuestProgramNpcPresenceMode(program, requirement.NpcId, requirement.Mode, sceneId))
		{
			changed = true;
		}
		return changed;
	}

	private bool TryStageQuestProgramNpcToScene(QuestProgram program, int npcId, string sceneId)
	{
		if (program == null || npcId <= 0 || string.IsNullOrWhiteSpace(sceneId))
		{
			return false;
		}
		string normalizedSceneId = sceneId.Trim();
		Dictionary<string, string> parameters = new Dictionary<string, string>
		{
			["npcId"] = npcId.ToString(),
			["sceneId"] = normalizedSceneId
		};
		string npcName = ResolveQuestProgramNpcName(npcId);
		if (!string.IsNullOrWhiteSpace(npcName) && !npcName.StartsWith("NPC_", StringComparison.Ordinal))
		{
			parameters["npcName"] = npcName;
		}
		if (!EnsureQuestProgramForcedNpcState(program, npcId, parameters))
		{
			return false;
		}
		if (IsQuestProgramNpcPresentInScene(npcId, normalizedSceneId, normalizedSceneId))
		{
			return false;
		}
		if (HasQuestProgramRuntimeTestNpcWorldState(npcId))
		{
			_questProgramTestNpcLocations[npcId] = normalizedSceneId;
			return true;
		}
		bool moved = TryRelocateNpcToLocation(npcId, normalizedSceneId);
		if (moved && NpcJieSuanManager.inst != null)
		{
			NpcJieSuanManager.inst.isUpDateNpcList = true;
		}
		return moved;
	}

	private bool TrySetQuestProgramNpcPresenceMode(QuestProgram program, int npcId, QuestNpcPresenceMode mode, string sceneId)
	{
		if (program == null || npcId <= 0 || string.IsNullOrWhiteSpace(sceneId))
		{
			return false;
		}
		int actionId = ResolveQuestProgramPresenceActionId(mode, sceneId);
		if (actionId <= 0)
		{
			throw new InvalidOperationException($"presence mode {mode} 无法解析有效 actionId: sceneId={sceneId}");
		}
		Dictionary<string, string> parameters = new Dictionary<string, string>
		{
			["npcId"] = npcId.ToString(),
			["sceneId"] = sceneId.Trim()
		};
		string npcName = ResolveQuestProgramNpcName(npcId);
		if (!string.IsNullOrWhiteSpace(npcName) && !npcName.StartsWith("NPC_", StringComparison.Ordinal))
		{
			parameters["npcName"] = npcName;
		}
		if (!EnsureQuestProgramForcedNpcState(program, npcId, parameters))
		{
			return false;
		}
		if (HasQuestProgramRuntimeTestNpcWorldState(npcId))
		{
			bool changed = false;
			if (_questProgramTestNpcActions.TryGetValue(npcId, out var currentAction) && currentAction != actionId)
			{
				_questProgramTestNpcActions[npcId] = actionId;
				changed = true;
			}
			if (_questProgramTestNpcLockActions.TryGetValue(npcId, out var lockActionId) && lockActionId != 0)
			{
				_questProgramTestNpcLockActions[npcId] = 0;
				changed = true;
			}
			return changed;
		}
		int normalizedNpcId = NPCEx.NPCIDToNew(npcId);
		JSONObject npcData = NpcJieSuanManager.inst?.GetNpcData(normalizedNpcId);
		if (npcData == null)
		{
			return false;
		}
		bool changedReal = false;
		if (npcData["ActionId"].I != actionId)
		{
			using (BeginQuestProgramNpcActionMutationScope())
			{
				NPCEx.SetNPCAction(normalizedNpcId, actionId);
			}
			changedReal = true;
		}
		if (npcData.HasField("LockAction"))
		{
			npcData.RemoveField("LockAction");
			changedReal = true;
		}
		if (changedReal && NpcJieSuanManager.inst != null)
		{
			NpcJieSuanManager.inst.isUpDateNpcList = true;
		}
		return changedReal;
	}

	private static int ResolveQuestProgramPresenceActionId(QuestNpcPresenceMode mode, string sceneId)
	{
		string normalizedSceneId = sceneId?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(normalizedSceneId))
		{
			return 0;
		}
		int result;
		if ((uint)mode <= 4u)
		{
			return int.TryParse(normalizedSceneId, out result) ? 33 : 113;
		}
		throw new InvalidOperationException($"未支持的 QuestNpcPresenceMode: {mode}");
	}

	private int GetMainlineQuestPriority(int explicitPriority = 0)
	{
		if (explicitPriority > 0)
		{
			return explicitPriority;
		}
		return 300;
	}

	private QuestProgramState EnsureQuestProgramState()
	{
		_questProgramState = QuestProgramPersistence.NormalizeState(_questProgramState);
		return _questProgramState;
	}

	internal void SetQuestProgramRuntimeTestSnapshotForTest(DateTime? gameTime, IEnumerable<string> locationIds, Dictionary<int, bool> npcAliveStates, Dictionary<int, bool> cyFriendStates = null)
	{
		_questProgramTestGameTime = gameTime;
		_questProgramTestLocationIds = ((locationIds != null) ? new HashSet<string>(locationIds.Where((string id) => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal) : null);
		_questProgramTestNpcAliveStates = ((npcAliveStates != null) ? new Dictionary<int, bool>(npcAliveStates) : new Dictionary<int, bool>());
		_questProgramTestCyFriendStates = ((cyFriendStates != null) ? new Dictionary<int, bool>(cyFriendStates) : new Dictionary<int, bool>());
	}

	internal void SetQuestProgramRuntimeTestNpcWorldStateForTest(int npcId, string locationId, int actionId, int statusId, int lockActionId = 0)
	{
		if (npcId > 0)
		{
			_questProgramTestNpcLocations[npcId] = locationId ?? string.Empty;
			_questProgramTestNpcActions[npcId] = actionId;
			_questProgramTestNpcStatuses[npcId] = statusId;
			_questProgramTestNpcLockActions[npcId] = lockActionId;
		}
	}

	internal void SetQuestProgramRuntimeTestPlayerStateForTest(int? money, Dictionary<int, int> itemCounts, Dictionary<int, int> npcFavorValues)
	{
		_questProgramTestPlayerMoney = money;
		_questProgramTestPlayerItemCounts = ((itemCounts != null) ? new Dictionary<int, int>(itemCounts) : new Dictionary<int, int>());
		_questProgramTestNpcFavorValues = ((npcFavorValues != null) ? new Dictionary<int, int>(npcFavorValues) : new Dictionary<int, int>());
	}

	internal void SetQuestProgramRuntimeTestPlayerSkillStateForTest(IEnumerable<int> skillIds, IEnumerable<int> staticSkillIds)
	{
		_questProgramTestPlayerSkillIds = ((skillIds != null) ? new HashSet<int>(skillIds.Where((int skillId) => skillId > 0)) : new HashSet<int>());
		_questProgramTestPlayerStaticSkillIds = ((staticSkillIds != null) ? new HashSet<int>(staticSkillIds.Where((int skillId) => skillId > 0)) : new HashSet<int>());
	}

	internal void SetQuestProgramRuntimeTestAdvancedStateForTest(int? playerLevel, int? fuBenRemainingDays, int? sceneType = null)
	{
		_questProgramTestPlayerLevel = playerLevel;
		_questProgramTestFuBenRemainingDays = fuBenRemainingDays;
		_questProgramTestSceneType = sceneType;
	}

	internal void SetQuestProgramRuntimeTestNpcPatrolPresenceForTest(Dictionary<int, bool> npcPatrolPresence)
	{
		_questProgramTestNpcPatrolPresence = ((npcPatrolPresence != null) ? new Dictionary<int, bool>(npcPatrolPresence) : new Dictionary<int, bool>());
	}

	internal void SetQuestProgramRuntimeTestOfficialTaskOutdatedStateForTest(Dictionary<int, bool> outdatedStates)
	{
		_questProgramTestOfficialTaskOutdatedStates = ((outdatedStates != null) ? new Dictionary<int, bool>(outdatedStates) : new Dictionary<int, bool>());
	}

	internal void SetQuestProgramRuntimeTestCyFriendStateForTest(int npcId, bool isFriend)
	{
		if (npcId > 0)
		{
			_questProgramTestCyFriendStates[npcId] = isFriend;
		}
	}

	internal void SetQuestProgramRuntimeTestOfficialTaskStateForTest(IEnumerable<int> startedTaskIds, Dictionary<int, IEnumerable<int>> finishedTaskSteps, Dictionary<int, int> requiredStepCounts = null)
	{
		_questProgramTestOfficialStartedTaskIds = ((startedTaskIds != null) ? new HashSet<int>(startedTaskIds.Where((int taskId) => taskId > 0)) : new HashSet<int>());
		_questProgramTestOfficialFinishedTaskSteps = new Dictionary<int, HashSet<int>>();
		_questProgramTestOfficialRequiredTaskStepCounts = new Dictionary<int, int>();
		if (finishedTaskSteps == null)
		{
			finishedTaskSteps = new Dictionary<int, IEnumerable<int>>();
		}
		foreach (KeyValuePair<int, IEnumerable<int>> kvp in finishedTaskSteps)
		{
			if (kvp.Key > 0)
			{
				_questProgramTestOfficialFinishedTaskSteps[kvp.Key] = ((kvp.Value != null) ? new HashSet<int>(kvp.Value.Where((int stepIndex) => stepIndex > 0)) : new HashSet<int>());
			}
		}
		if (requiredStepCounts == null)
		{
			return;
		}
		foreach (KeyValuePair<int, int> kvp2 in requiredStepCounts)
		{
			if (kvp2.Key > 0 && kvp2.Value > 0)
			{
				_questProgramTestOfficialRequiredTaskStepCounts[kvp2.Key] = kvp2.Value;
			}
		}
	}

	internal bool TryGetQuestProgramRuntimeTestNpcStateForTest(int npcId, out string locationId, out int actionId, out int statusId, out int lockActionId)
	{
		locationId = null;
		actionId = 0;
		statusId = 0;
		lockActionId = 0;
		if (npcId <= 0 || !_questProgramTestNpcLocations.TryGetValue(npcId, out locationId) || !_questProgramTestNpcActions.TryGetValue(npcId, out actionId) || !_questProgramTestNpcStatuses.TryGetValue(npcId, out statusId))
		{
			return false;
		}
		_questProgramTestNpcLockActions.TryGetValue(npcId, out lockActionId);
		return true;
	}

	internal bool TryGetQuestProgramRuntimeTestCyFriendStateForTest(int npcId, out bool isFriend)
	{
		isFriend = false;
		return npcId > 0 && _questProgramTestCyFriendStates.TryGetValue(npcId, out isFriend);
	}

	internal bool TryGetQuestProgramRuntimeTestNpcFavorForTest(int npcId, out int favorValue)
	{
		favorValue = 0;
		return npcId > 0 && _questProgramTestNpcFavorValues.TryGetValue(npcId, out favorValue);
	}

	internal bool TryGetQuestProgramRuntimeTestPlayerItemCountForTest(int itemId, out int count)
	{
		count = 0;
		return itemId > 0 && _questProgramTestPlayerItemCounts.TryGetValue(itemId, out count);
	}

	internal bool TryGetQuestProgramRuntimeTestPlayerMoneyForTest(out int money)
	{
		money = _questProgramTestPlayerMoney.GetValueOrDefault();
		return _questProgramTestPlayerMoney.HasValue;
	}

	internal bool TryGetQuestProgramRuntimeTestPlayerSkillForTest(int skillId, out bool hasSkill)
	{
		hasSkill = skillId > 0 && _questProgramTestPlayerSkillIds.Contains(skillId);
		return skillId > 0;
	}

	internal bool TryGetQuestProgramRuntimeTestPlayerStaticSkillForTest(int skillId, out bool hasSkill)
	{
		hasSkill = skillId > 0 && _questProgramTestPlayerStaticSkillIds.Contains(skillId);
		return skillId > 0;
	}

	internal bool TryGetQuestProgramRuntimeTestOfficialTaskStartedForTest(int taskId, out bool started)
	{
		started = taskId > 0 && _questProgramTestOfficialStartedTaskIds.Contains(taskId);
		return taskId > 0;
	}

	internal bool TryGetQuestProgramRuntimeTestOfficialTaskStepFinishedForTest(int taskId, int taskIndex, out bool finished)
	{
		finished = false;
		if (taskId <= 0 || taskIndex <= 0)
		{
			return false;
		}
		if (!_questProgramTestOfficialFinishedTaskSteps.TryGetValue(taskId, out var finishedSteps))
		{
			return false;
		}
		finished = finishedSteps.Contains(taskIndex);
		return true;
	}

	internal bool TryGetQuestProgramRuntimeTestLastNotificationForTest(out int npcId, out string message, out string historyMessage, out bool wasQueued)
	{
		npcId = 0;
		message = string.Empty;
		historyMessage = string.Empty;
		wasQueued = false;
		if (_questProgramTestNotificationRecords == null || _questProgramTestNotificationRecords.Count == 0)
		{
			return false;
		}
		QuestProgramTestNotificationRecord record = _questProgramTestNotificationRecords[_questProgramTestNotificationRecords.Count - 1];
		if (record == null)
		{
			return false;
		}
		npcId = record.NpcId;
		message = record.Message ?? string.Empty;
		historyMessage = record.HistoryMessage ?? string.Empty;
		wasQueued = record.WasQueued;
		return true;
	}

	internal bool TryGetQuestProgramRuntimeTestLastCombatForTest(out int npcId, out string fightMusic)
	{
		npcId = 0;
		fightMusic = string.Empty;
		if (_questProgramTestCombatRecords == null || _questProgramTestCombatRecords.Count == 0)
		{
			return false;
		}
		QuestProgramTestCombatRecord record = _questProgramTestCombatRecords[_questProgramTestCombatRecords.Count - 1];
		if (record == null)
		{
			return false;
		}
		npcId = record.NpcId;
		fightMusic = record.FightMusic ?? string.Empty;
		return true;
	}

	internal void ClearQuestProgramRuntimeTestNpcWorldStateForTest()
	{
		_questProgramTestNpcLocations.Clear();
		_questProgramTestNpcActions.Clear();
		_questProgramTestNpcStatuses.Clear();
		_questProgramTestNpcLockActions.Clear();
		_questProgramTestNpcFavorValues.Clear();
		_questProgramTestPlayerItemCounts.Clear();
		_questProgramTestPlayerSkillIds.Clear();
		_questProgramTestPlayerStaticSkillIds.Clear();
		_questProgramTestPlayerMoney = null;
		_questProgramTestPlayerLevel = null;
		_questProgramTestFuBenRemainingDays = null;
		_questProgramTestSceneType = null;
		_questProgramTestNpcPatrolPresence.Clear();
		_questProgramTestOfficialStartedTaskIds.Clear();
		_questProgramTestOfficialFinishedTaskSteps.Clear();
		_questProgramTestOfficialRequiredTaskStepCounts.Clear();
		_questProgramTestOfficialTaskOutdatedStates.Clear();
		_questProgramTestNotificationRecords.Clear();
		_questProgramTestCombatRecords.Clear();
		_recentQuestProgramDeliveryMailSubmissions.Clear();
		_lastQuestProgramObservedMoney = null;
		_lastQuestProgramObservedPlayerLevel = null;
		_lastQuestProgramObservedFuBenRemainingDays = null;
		_lastQuestProgramObservedItemCounts.Clear();
		_lastQuestProgramObservedNpcAliveStates.Clear();
		_lastQuestProgramObservedNpcFavorValues.Clear();
		_lastQuestProgramObservedNpcActions.Clear();
	}

	internal void SetQuestProgramRuntimeTestChoiceSelectionForTest(string programId, string nodeId, string choiceId)
	{
		if (!string.IsNullOrWhiteSpace(programId) && !string.IsNullOrWhiteSpace(nodeId))
		{
			_questProgramTestChoiceSelections[programId.Trim() + "::" + nodeId.Trim()] = choiceId?.Trim() ?? string.Empty;
		}
	}

	internal void ClearQuestProgramRuntimeTestChoiceSelectionsForTest()
	{
		_questProgramTestChoiceSelections.Clear();
	}

	private bool HasQuestProgramRuntimeTestNpcWorldState(int npcId)
	{
		return npcId > 0 && _questProgramTestNpcLocations.ContainsKey(npcId) && _questProgramTestNpcActions.ContainsKey(npcId) && _questProgramTestNpcStatuses.ContainsKey(npcId);
	}

	internal bool PublishQuestProgramEventForTest(string eventType, string primaryId = null, Dictionary<string, string> payload = null)
	{
		return ProcessQuestProgramRuntimeEvent(new QuestRuntimeEvent
		{
			EventType = eventType,
			PrimaryId = primaryId,
			Payload = (payload ?? new Dictionary<string, string>()),
			RaisedAt = (_questProgramTestGameTime ?? DateTime.Now)
		}, persistChanges: false);
	}

	internal bool NotifyOfficialTaskProgressChangedForTest(int taskId, int taskIndex = 0)
	{
		if (taskId <= 0)
		{
			return false;
		}
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramOfficialTaskProgressEvent(taskId, taskIndex), persistChanges: false);
	}

	internal bool NotifyNpcStatusChangedForTest(int npcId, int statusId)
	{
		if (npcId <= 0 || statusId <= 0)
		{
			return false;
		}
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramNpcStatusChangedEvent(npcId, statusId), persistChanges: false);
	}

	internal bool NotifyNpcActionChangedForTest(int npcId, int previousActionId, int currentActionId)
	{
		if (npcId <= 0 || previousActionId == currentActionId)
		{
			return false;
		}
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramNpcActionChangedEvent(npcId, previousActionId, currentActionId), persistChanges: false);
	}

	internal bool NotifyNpcFavorChangedForTest(int npcId, int previousFavor, int currentFavor)
	{
		if (npcId <= 0 || previousFavor == currentFavor)
		{
			return false;
		}
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramNpcFavorChangedEvent(npcId, previousFavor, currentFavor), persistChanges: false);
	}

	internal bool NotifyNpcDeathForTest(int npcId)
	{
		if (npcId <= 0)
		{
			return false;
		}
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramNpcAliveChangedEvent(npcId, currentAlive: false, previousAlive: true), persistChanges: false);
	}

	internal bool NotifyQuestProgramPlayerMoneyChangedForTest(long previousAmount, long currentAmount)
	{
		if (previousAmount == currentAmount)
		{
			return false;
		}
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramPlayerMoneyChangedEvent(previousAmount, currentAmount), persistChanges: false);
	}

	internal bool NotifyQuestProgramPlayerItemChangedForTest(int itemId, int previousCount, int currentCount)
	{
		if (itemId <= 0 || previousCount == currentCount)
		{
			return false;
		}
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramPlayerItemChangedEvent(itemId, previousCount, currentCount), persistChanges: false);
	}

	internal bool NotifyQuestProgramPlayerSkillLearnedForTest(int skillId)
	{
		if (skillId <= 0)
		{
			return false;
		}
		_questProgramTestPlayerSkillIds.Add(skillId);
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramPlayerSkillChangedEvent(skillId, previousHasSkill: false, currentHasSkill: true), persistChanges: false);
	}

	internal bool NotifyQuestProgramPlayerStaticSkillLearnedForTest(int skillId)
	{
		if (skillId <= 0)
		{
			return false;
		}
		_questProgramTestPlayerStaticSkillIds.Add(skillId);
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramPlayerStaticSkillChangedEvent(skillId, previousHasSkill: false, currentHasSkill: true), persistChanges: false);
	}

	internal bool NotifyQuestProgramPlayerLevelChangedForTest(int previousLevel, int currentLevel)
	{
		if (previousLevel == currentLevel)
		{
			return false;
		}
		_questProgramTestPlayerLevel = currentLevel;
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramPlayerLevelChangedEvent(previousLevel, currentLevel), persistChanges: false);
	}

	internal bool NotifyQuestProgramFuBenRemainingDaysChangedForTest(int previousDays, int currentDays)
	{
		if (previousDays == currentDays)
		{
			return false;
		}
		_questProgramTestFuBenRemainingDays = currentDays;
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramFuBenRemainingDaysChangedEvent(previousDays, currentDays), persistChanges: false);
	}

	internal bool NotifyQuestProgramDeliveryMailSubmittedForTest(int npcId, int itemId, int mailId, string questId = "", string stepId = "")
	{
		if (npcId <= 0 || itemId <= 0 || mailId == 0)
		{
			return false;
		}
		RecordQuestProgramDeliveryMailSubmission(npcId, itemId, mailId, questId, stepId);
		return ProcessQuestProgramRuntimeEvent(BuildQuestProgramDeliveryMailSubmittedEvent(npcId, itemId, mailId, questId, stepId), persistChanges: false);
	}

	internal void NotifyOfficialTaskProgressChanged(int taskId, int taskIndex = 0)
	{
		if (taskId > 0 && _questProgramOfficialTaskMutationDepth <= 0)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramOfficialTaskProgressEvent(taskId, taskIndex);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal void NotifyNpcStatusChanged(int npcId, int statusId)
	{
		if (npcId > 0 && statusId > 0 && _questProgramNpcStatusMutationDepth <= 0)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramNpcStatusChangedEvent(npcId, statusId);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal void NotifyNpcActionChanged(int npcId, int previousActionId, int currentActionId)
	{
		if (npcId > 0 && previousActionId != currentActionId && _questProgramNpcActionMutationDepth <= 0)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramNpcActionChangedEvent(npcId, previousActionId, currentActionId);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal void NotifyNpcFavorChanged(int npcId, int previousFavor, int currentFavor)
	{
		if (npcId > 0 && previousFavor != currentFavor && _questProgramNpcFavorMutationDepth <= 0)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramNpcFavorChangedEvent(npcId, previousFavor, currentFavor);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal void NotifyNpcDeath(int npcId)
	{
		if (npcId > 0 && _questProgramNpcDeathMutationDepth <= 0)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramNpcAliveChangedEvent(npcId, currentAlive: false, previousAlive: true);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal void NotifyPlayerMoneyChanged(long previousAmount, long currentAmount)
	{
		if (_questProgramPlayerMoneyMutationDepth <= 0 && previousAmount != currentAmount)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramPlayerMoneyChangedEvent(previousAmount, currentAmount);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal void NotifyPlayerItemChanged(int itemId, int previousCount, int currentCount)
	{
		if (itemId > 0 && _questProgramPlayerItemMutationDepth <= 0 && previousCount != currentCount)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramPlayerItemChangedEvent(itemId, previousCount, currentCount);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal void NotifyPlayerSkillChanged(int skillId, bool previousHasSkill, bool currentHasSkill)
	{
		if (skillId > 0 && _questProgramPlayerSkillMutationDepth <= 0 && previousHasSkill != currentHasSkill)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramPlayerSkillChangedEvent(skillId, previousHasSkill, currentHasSkill);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal void NotifyPlayerStaticSkillChanged(int skillId, bool previousHasSkill, bool currentHasSkill)
	{
		if (skillId > 0 && _questProgramPlayerSkillMutationDepth <= 0 && previousHasSkill != currentHasSkill)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramPlayerStaticSkillChangedEvent(skillId, previousHasSkill, currentHasSkill);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal void NotifyPlayerLevelChanged(int previousLevel, int currentLevel)
	{
		if (previousLevel != currentLevel)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramPlayerLevelChangedEvent(previousLevel, currentLevel);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal void NotifyFuBenRemainingDaysChanged(int previousDays, int currentDays)
	{
		if (previousDays != currentDays)
		{
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramFuBenRemainingDaysChangedEvent(previousDays, currentDays);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	internal bool PumpQuestProgramObservedStateForTest()
	{
		return PumpQuestProgramObservedStateChanges(persistChanges: false);
	}

	private void EnsureQuestProgramRuntimeInitialized()
	{
		if (!_questProgramRuntimeInitialized)
		{
			_questEventBus.EventRaised += OnQuestProgramEventRaised;
			_questProgramRuntimeInitialized = true;
		}
	}

	private void OnQuestProgramEventRaised(QuestRuntimeEvent runtimeEvent)
	{
		ProcessQuestProgramRuntimeEvent(runtimeEvent, persistChanges: true);
	}

	private void PublishQuestProgramRuntimeEvent(string eventType, string primaryId = null, Dictionary<string, string> payload = null)
	{
		if (!string.IsNullOrWhiteSpace(eventType))
		{
			EnsureQuestProgramRuntimeInitialized();
			_questEventBus.Publish(new QuestRuntimeEvent
			{
				EventType = eventType.Trim(),
				PrimaryId = primaryId,
				Payload = (payload ?? new Dictionary<string, string>()),
				RaisedAt = (TryGetQuestProgramRuntimeGameTime(out var now) ? now : DateTime.Now)
			});
		}
	}

	private QuestRuntimeEvent BuildQuestProgramOfficialTaskProgressEvent(int taskId, int taskIndex)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "official_task.progress_changed",
			PrimaryId = taskId.ToString(),
			Payload = new Dictionary<string, string>
			{
				["taskId"] = taskId.ToString(),
				["taskIndex"] = Math.Max(0, taskIndex).ToString()
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramPlayerMoneyChangedEvent(long previousAmount, long currentAmount)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "player.money_changed",
			PrimaryId = currentAmount.ToString(),
			Payload = new Dictionary<string, string>
			{
				["amount"] = currentAmount.ToString(),
				["previousAmount"] = previousAmount.ToString()
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramPlayerItemChangedEvent(int itemId, int previousCount, int currentCount)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "player.item_changed",
			PrimaryId = itemId.ToString(),
			Payload = new Dictionary<string, string>
			{
				["itemId"] = itemId.ToString(),
				["count"] = currentCount.ToString(),
				["previousCount"] = previousCount.ToString()
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramPlayerSkillChangedEvent(int skillId, bool previousHasSkill, bool currentHasSkill)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "player.skill_changed",
			PrimaryId = skillId.ToString(),
			Payload = new Dictionary<string, string>
			{
				["skillId"] = skillId.ToString(),
				["hasSkill"] = currentHasSkill.ToString(),
				["previousHasSkill"] = previousHasSkill.ToString()
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramPlayerStaticSkillChangedEvent(int skillId, bool previousHasSkill, bool currentHasSkill)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "player.static_skill_changed",
			PrimaryId = skillId.ToString(),
			Payload = new Dictionary<string, string>
			{
				["skillId"] = skillId.ToString(),
				["hasSkill"] = currentHasSkill.ToString(),
				["previousHasSkill"] = previousHasSkill.ToString()
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramPlayerLevelChangedEvent(int previousLevel, int currentLevel)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "player.level_changed",
			PrimaryId = currentLevel.ToString(),
			Payload = new Dictionary<string, string>
			{
				["level"] = currentLevel.ToString(),
				["previousLevel"] = previousLevel.ToString()
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramFuBenRemainingDaysChangedEvent(int previousDays, int currentDays)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "fuben.remaining_days_changed",
			PrimaryId = currentDays.ToString(),
			Payload = new Dictionary<string, string>
			{
				["days"] = currentDays.ToString(),
				["previousDays"] = previousDays.ToString()
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramNpcAliveChangedEvent(int npcId, bool currentAlive, bool previousAlive)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "npc.alive_changed",
			PrimaryId = npcId.ToString(),
			Payload = new Dictionary<string, string>
			{
				["npcId"] = npcId.ToString(),
				["alive"] = (currentAlive ? "true" : "false"),
				["previousAlive"] = (previousAlive ? "true" : "false")
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramNpcActionChangedEvent(int npcId, int previousActionId, int currentActionId)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "npc.action_changed",
			PrimaryId = npcId.ToString(),
			Payload = new Dictionary<string, string>
			{
				["npcId"] = npcId.ToString(),
				["actionId"] = currentActionId.ToString(),
				["previousActionId"] = previousActionId.ToString()
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramNpcStatusChangedEvent(int npcId, int statusId)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "npc.status_changed",
			PrimaryId = npcId.ToString(),
			Payload = new Dictionary<string, string>
			{
				["npcId"] = npcId.ToString(),
				["statusId"] = statusId.ToString()
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramNpcFavorChangedEvent(int npcId, int previousFavor, int currentFavor)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "npc.favor_changed",
			PrimaryId = npcId.ToString(),
			Payload = new Dictionary<string, string>
			{
				["npcId"] = npcId.ToString(),
				["favor"] = currentFavor.ToString(),
				["previousFavor"] = previousFavor.ToString()
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private QuestRuntimeEvent BuildQuestProgramDeliveryMailSubmittedEvent(int npcId, int itemId, int mailId, string questId, string stepId)
	{
		DateTime now;
		return new QuestRuntimeEvent
		{
			EventType = "mail.delivery_submitted",
			PrimaryId = npcId.ToString(),
			Payload = new Dictionary<string, string>
			{
				["npcId"] = npcId.ToString(),
				["itemId"] = itemId.ToString(),
				["mailId"] = mailId.ToString(),
				["questId"] = questId ?? string.Empty,
				["stepId"] = stepId ?? string.Empty
			},
			RaisedAt = (TryGetQuestProgramRuntimeGameTime(out now) ? now : DateTime.Now)
		};
	}

	private IDisposable BeginQuestProgramOfficialTaskMutationScope()
	{
		_questProgramOfficialTaskMutationDepth++;
		return new QuestProgramMutationScope(delegate
		{
			_questProgramOfficialTaskMutationDepth = Math.Max(0, _questProgramOfficialTaskMutationDepth - 1);
		});
	}

	private IDisposable BeginQuestProgramNpcActionMutationScope()
	{
		_questProgramNpcActionMutationDepth++;
		return new QuestProgramMutationScope(delegate
		{
			_questProgramNpcActionMutationDepth = Math.Max(0, _questProgramNpcActionMutationDepth - 1);
		});
	}

	private IDisposable BeginQuestProgramNpcFavorMutationScope()
	{
		_questProgramNpcFavorMutationDepth++;
		return new QuestProgramMutationScope(delegate
		{
			_questProgramNpcFavorMutationDepth = Math.Max(0, _questProgramNpcFavorMutationDepth - 1);
		});
	}

	private IDisposable BeginQuestProgramNpcStatusMutationScope()
	{
		_questProgramNpcStatusMutationDepth++;
		return new QuestProgramMutationScope(delegate
		{
			_questProgramNpcStatusMutationDepth = Math.Max(0, _questProgramNpcStatusMutationDepth - 1);
		});
	}

	private IDisposable BeginQuestProgramNpcDeathMutationScope()
	{
		_questProgramNpcDeathMutationDepth++;
		return new QuestProgramMutationScope(delegate
		{
			_questProgramNpcDeathMutationDepth = Math.Max(0, _questProgramNpcDeathMutationDepth - 1);
		});
	}

	private IDisposable BeginQuestProgramPlayerItemMutationScope()
	{
		_questProgramPlayerItemMutationDepth++;
		return new QuestProgramMutationScope(delegate
		{
			_questProgramPlayerItemMutationDepth = Math.Max(0, _questProgramPlayerItemMutationDepth - 1);
		});
	}

	private IDisposable BeginQuestProgramPlayerMoneyMutationScope()
	{
		_questProgramPlayerMoneyMutationDepth++;
		return new QuestProgramMutationScope(delegate
		{
			_questProgramPlayerMoneyMutationDepth = Math.Max(0, _questProgramPlayerMoneyMutationDepth - 1);
		});
	}

	private IDisposable BeginQuestProgramPlayerSkillMutationScope()
	{
		_questProgramPlayerSkillMutationDepth++;
		return new QuestProgramMutationScope(delegate
		{
			_questProgramPlayerSkillMutationDepth = Math.Max(0, _questProgramPlayerSkillMutationDepth - 1);
		});
	}

	private bool HasActiveQuestPrograms()
	{
		return EnsureQuestProgramState().Programs.Any((QuestProgram program) => program != null && program.Status == QuestProgramStatus.Active);
	}

	private bool ProcessQuestProgramRuntimeEvent(QuestRuntimeEvent runtimeEvent, bool persistChanges)
	{
		if (runtimeEvent == null)
		{
			return false;
		}
		EnsureQuestProgramRuntimeInitialized();
		TryLoadPersistenceIfReady();
		bool changed = false;
		foreach (QuestProgram program in (from candidate in EnsureQuestProgramState().Programs
			where candidate != null && candidate.Status == QuestProgramStatus.Active
			orderby GetMainlineQuestPriority(candidate?.Priority ?? 0) descending
			select candidate).ToList())
		{
			if (TryProgressQuestProgram(program))
			{
				changed = true;
			}
		}
		if (TryKickoffQuestProgramNarrativePlayback())
		{
			changed = true;
		}
		if (changed && persistChanges)
		{
			MarkQuestPersistenceDirty();
		}
		return changed;
	}

	private bool PumpQuestProgramObservedStateChanges(bool persistChanges)
	{
		List<QuestProgram> activePrograms = EnsureQuestProgramState().Programs.Where((QuestProgram questProgram) => questProgram != null && questProgram.Status == QuestProgramStatus.Active).ToList();
		if (activePrograms.Count == 0)
		{
			_lastQuestProgramObservedMoney = null;
			_lastQuestProgramObservedPlayerLevel = null;
			_lastQuestProgramObservedFuBenRemainingDays = null;
			_lastQuestProgramObservedItemCounts.Clear();
			_lastQuestProgramObservedNpcAliveStates.Clear();
			_lastQuestProgramObservedNpcFavorValues.Clear();
			_lastQuestProgramObservedNpcActions.Clear();
			return false;
		}
		bool changed = false;
		bool observedAnyMoney = false;
		bool observedAnyPlayerLevel = false;
		bool observedAnyFuBenRemainingDays = false;
		HashSet<int> observedItemIds = new HashSet<int>();
		HashSet<int> observedNpcIds = new HashSet<int>();
		HashSet<int> observedNpcFavorIds = new HashSet<int>();
		HashSet<int> observedNpcActionIds = new HashSet<int>();
		foreach (QuestProgram program in activePrograms)
		{
			QuestNode currentNode = ResolveCurrentQuestProgramNode(program);
			if (currentNode == null)
			{
				continue;
			}
			foreach (QuestPredicateInvocation predicate in EnumerateQuestProgramObservedPredicates(currentNode))
			{
				if (predicate == null)
				{
					continue;
				}
				switch (predicate.Opcode?.Trim())
				{
				case "player.money_compare":
					observedAnyMoney = true;
					break;
				case "player.level_compare":
					observedAnyPlayerLevel = true;
					break;
				case "fuben.remaining_days":
					observedAnyFuBenRemainingDays = true;
					break;
				case "player.has_item":
				{
					if (TryGetQuestProgramObservedItemId(predicate.Params, out var itemId))
					{
						observedItemIds.Add(itemId);
					}
					break;
				}
				case "npc.alive":
				case "npc.dead":
				{
					if (TryGetQuestProgramObservedNpcId(predicate.Params, out var aliveNpcId))
					{
						observedNpcIds.Add(aliveNpcId);
					}
					break;
				}
				case "player.favor_compare":
				{
					if (TryGetQuestProgramObservedNpcId(predicate.Params, out var favorNpcId))
					{
						observedNpcFavorIds.Add(favorNpcId);
					}
					break;
				}
				case "npc.action_is":
				{
					if (TryGetQuestProgramObservedNpcId(predicate.Params, out var actionNpcId))
					{
						observedNpcActionIds.Add(actionNpcId);
					}
					break;
				}
				}
			}
		}
		if (observedAnyMoney)
		{
			long currentMoney = GetQuestProgramRuntimePlayerMoney();
			if (_lastQuestProgramObservedMoney.HasValue && _lastQuestProgramObservedMoney.Value != currentMoney)
			{
				changed |= ProcessQuestProgramRuntimeEvent(BuildQuestProgramPlayerMoneyChangedEvent(_lastQuestProgramObservedMoney.Value, currentMoney), persistChanges);
			}
			_lastQuestProgramObservedMoney = currentMoney;
		}
		else
		{
			_lastQuestProgramObservedMoney = null;
		}
		if (observedAnyPlayerLevel)
		{
			int currentLevel = GetQuestProgramRuntimePlayerLevel();
			if (_lastQuestProgramObservedPlayerLevel.HasValue && _lastQuestProgramObservedPlayerLevel.Value != currentLevel)
			{
				changed |= ProcessQuestProgramRuntimeEvent(BuildQuestProgramPlayerLevelChangedEvent(_lastQuestProgramObservedPlayerLevel.Value, currentLevel), persistChanges);
			}
			_lastQuestProgramObservedPlayerLevel = currentLevel;
		}
		else
		{
			_lastQuestProgramObservedPlayerLevel = null;
		}
		if (observedAnyFuBenRemainingDays)
		{
			int? currentDays = GetQuestProgramRuntimeFuBenRemainingDays();
			if (_lastQuestProgramObservedFuBenRemainingDays.HasValue && currentDays.HasValue && _lastQuestProgramObservedFuBenRemainingDays.Value != currentDays.Value)
			{
				changed |= ProcessQuestProgramRuntimeEvent(BuildQuestProgramFuBenRemainingDaysChangedEvent(_lastQuestProgramObservedFuBenRemainingDays.Value, currentDays.Value), persistChanges);
			}
			_lastQuestProgramObservedFuBenRemainingDays = currentDays;
		}
		else
		{
			_lastQuestProgramObservedFuBenRemainingDays = null;
		}
		foreach (int staleItemId in _lastQuestProgramObservedItemCounts.Keys.Except(observedItemIds).ToList())
		{
			_lastQuestProgramObservedItemCounts.Remove(staleItemId);
		}
		foreach (int itemId2 in observedItemIds)
		{
			int currentCount = GetQuestProgramRuntimePlayerItemCount(new Dictionary<string, string> { ["itemId"] = itemId2.ToString() });
			if (_lastQuestProgramObservedItemCounts.TryGetValue(itemId2, out var previousCount) && previousCount != currentCount)
			{
				changed |= ProcessQuestProgramRuntimeEvent(BuildQuestProgramPlayerItemChangedEvent(itemId2, previousCount, currentCount), persistChanges);
			}
			_lastQuestProgramObservedItemCounts[itemId2] = currentCount;
		}
		foreach (int staleNpcId in _lastQuestProgramObservedNpcAliveStates.Keys.Except(observedNpcIds).ToList())
		{
			_lastQuestProgramObservedNpcAliveStates.Remove(staleNpcId);
		}
		foreach (int npcId in observedNpcIds)
		{
			bool currentAlive = IsQuestProgramNpcAlive(npcId);
			if (_lastQuestProgramObservedNpcAliveStates.TryGetValue(npcId, out var previousAlive) && previousAlive != currentAlive)
			{
				changed |= ProcessQuestProgramRuntimeEvent(BuildQuestProgramNpcAliveChangedEvent(npcId, currentAlive, previousAlive), persistChanges);
			}
			_lastQuestProgramObservedNpcAliveStates[npcId] = currentAlive;
		}
		foreach (int staleNpcId2 in _lastQuestProgramObservedNpcFavorValues.Keys.Except(observedNpcFavorIds).ToList())
		{
			_lastQuestProgramObservedNpcFavorValues.Remove(staleNpcId2);
		}
		foreach (int npcId2 in observedNpcFavorIds)
		{
			int currentFavor = GetQuestProgramRuntimeNpcFavor(npcId2);
			if (_lastQuestProgramObservedNpcFavorValues.TryGetValue(npcId2, out var previousFavor) && previousFavor != currentFavor)
			{
				changed |= ProcessQuestProgramRuntimeEvent(BuildQuestProgramNpcFavorChangedEvent(npcId2, previousFavor, currentFavor), persistChanges);
			}
			_lastQuestProgramObservedNpcFavorValues[npcId2] = currentFavor;
		}
		foreach (int staleNpcId3 in _lastQuestProgramObservedNpcActions.Keys.Except(observedNpcActionIds).ToList())
		{
			_lastQuestProgramObservedNpcActions.Remove(staleNpcId3);
		}
		foreach (int npcId3 in observedNpcActionIds)
		{
			int currentAction = GetQuestProgramRuntimeNpcAction(npcId3);
			if (_lastQuestProgramObservedNpcActions.TryGetValue(npcId3, out var previousAction) && previousAction != currentAction)
			{
				changed |= ProcessQuestProgramRuntimeEvent(BuildQuestProgramNpcActionChangedEvent(npcId3, previousAction, currentAction), persistChanges);
			}
			_lastQuestProgramObservedNpcActions[npcId3] = currentAction;
		}
		return changed;
	}

	private IEnumerable<QuestPredicateInvocation> EnumerateQuestProgramObservedPredicates(QuestNode node)
	{
		if (node == null)
		{
			yield break;
		}
		if (node.ActivationPredicates != null)
		{
			foreach (QuestPredicateInvocation predicate in node.ActivationPredicates)
			{
				foreach (QuestPredicateInvocation item in EnumerateQuestProgramObservedPredicatesRecursive(predicate))
				{
					yield return item;
				}
			}
		}
		if (node.CompletionPredicates != null)
		{
			foreach (QuestPredicateInvocation predicate2 in node.CompletionPredicates)
			{
				foreach (QuestPredicateInvocation item2 in EnumerateQuestProgramObservedPredicatesRecursive(predicate2))
				{
					yield return item2;
				}
			}
		}
		if (node.Narrative?.Gates == null)
		{
			yield break;
		}
		foreach (QuestPredicateInvocation predicate3 in node.Narrative.Gates)
		{
			foreach (QuestPredicateInvocation item3 in EnumerateQuestProgramObservedPredicatesRecursive(predicate3))
			{
				yield return item3;
			}
		}
	}

	private IEnumerable<QuestPredicateInvocation> EnumerateQuestProgramObservedPredicatesRecursive(QuestPredicateInvocation predicate)
	{
		if (predicate == null)
		{
			yield break;
		}
		if (predicate.Children != null && predicate.Children.Count > 0)
		{
			foreach (QuestPredicateInvocation child in predicate.Children)
			{
				foreach (QuestPredicateInvocation item in EnumerateQuestProgramObservedPredicatesRecursive(child))
				{
					yield return item;
				}
			}
		}
		else
		{
			yield return predicate;
		}
	}

	private bool TryGetQuestProgramObservedItemId(Dictionary<string, string> parameters, out int itemId)
	{
		itemId = 0;
		if (parameters == null)
		{
			return false;
		}
		string rawItemId;
		return parameters.TryGetValue("itemId", out rawItemId) && int.TryParse(rawItemId, out itemId) && itemId > 0;
	}

	private bool TryGetQuestProgramObservedNpcId(Dictionary<string, string> parameters, out int npcId)
	{
		npcId = 0;
		if (parameters == null)
		{
			return false;
		}
		string rawNpcId;
		return parameters.TryGetValue("npcId", out rawNpcId) && int.TryParse(rawNpcId, out npcId) && npcId > 0;
	}

	private bool TryProgressQuestProgram(QuestProgram program)
	{
		if (program == null || program.Status != QuestProgramStatus.Active)
		{
			return false;
		}
		NormalizeQuestProgramRuntimeState(program);
		bool changed = false;
		int safety = Math.Max(1, program.Nodes?.Count ?? 0) + 2;
		while (program.Status == QuestProgramStatus.Active && safety-- > 0)
		{
			QuestNode node = ResolveCurrentQuestProgramNode(program);
			if (node == null)
			{
				if (program.Status != QuestProgramStatus.Completed)
				{
					program.Status = QuestProgramStatus.Completed;
					changed = true;
				}
				break;
			}
			changed |= PrepareQuestProgramCurrentNodeWorldState(program, node);
			if (!EnsureQuestProgramNodeActivated(program, node, ref changed))
			{
				break;
			}
			string previousNodeId = node.NodeId;
			bool nodeChanged = TryResolveQuestProgramNode(program, node);
			changed = changed || nodeChanged;
			if (!nodeChanged || program.Status != QuestProgramStatus.Active || string.Equals(previousNodeId, program.Runtime.CurrentNodeId, StringComparison.Ordinal))
			{
				break;
			}
		}
		return changed;
	}

	private void NormalizeQuestProgramRuntimeState(QuestProgram program)
	{
		if (program != null)
		{
			program.Runtime = program.Runtime ?? new QuestRuntimeState();
			program.Runtime.ActivatedNodeIds = program.Runtime.ActivatedNodeIds ?? new List<string>();
			program.Runtime.CompletedNodeIds = program.Runtime.CompletedNodeIds ?? new List<string>();
			program.Runtime.ExecutedCommandKeys = program.Runtime.ExecutedCommandKeys ?? new List<string>();
			program.Runtime.PendingNarrativeNodeIds = program.Runtime.PendingNarrativeNodeIds ?? new List<string>();
			program.Runtime.ActiveForcedNpcStates = program.Runtime.ActiveForcedNpcStates ?? new List<QuestForcedNpcState>();
		}
	}

	private bool EnsureQuestProgramNodeActivated(QuestProgram program, QuestNode node, ref bool changed)
	{
		if (program == null || node == null)
		{
			return false;
		}
		if (!EvaluateQuestProgramPredicates(node.ActivationPredicates))
		{
			return false;
		}
		if (!program.Runtime.ActivatedNodeIds.Contains(node.NodeId))
		{
			program.Runtime.ActivatedNodeIds.Add(node.NodeId);
			changed = true;
		}
		return TryExecuteQuestProgramCommandBlock(program, node.OnActivated, node.NodeId + ":onActivated", ref changed);
	}

	private bool TryResolveQuestProgramNode(QuestProgram program, QuestNode node)
	{
		if (program == null || node == null)
		{
			return false;
		}
		switch (node.Type)
		{
		case QuestNodeType.Terminal:
			return CompleteQuestProgramNode(program, node, completesProgram: true);
		case QuestNodeType.Objective:
		case QuestNodeType.Combat:
		case QuestNodeType.WorldState:
			if (node.CompletionPredicates == null || node.CompletionPredicates.Count == 0)
			{
				program.Status = QuestProgramStatus.Suspended;
				program.Runtime.FailedReason = "节点 " + (node.Title ?? node.NodeId) + " 缺少 completionPredicates";
				return true;
			}
			if (string.IsNullOrWhiteSpace(node.NextNodeId))
			{
				program.Status = QuestProgramStatus.Suspended;
				program.Runtime.FailedReason = "节点 " + (node.Title ?? node.NodeId) + " 缺少 nextNodeId";
				return true;
			}
			if (EvaluateQuestProgramPredicates(node.CompletionPredicates))
			{
				return CompleteQuestProgramNode(program, node, completesProgram: false);
			}
			return false;
		case QuestNodeType.Narrative:
		case QuestNodeType.Choice:
			return TryMarkQuestProgramNarrativePending(program, node);
		default:
			return false;
		}
	}

	private bool TryMarkQuestProgramNarrativePending(QuestProgram program, QuestNode node)
	{
		if (program == null || node == null || !_questNarrativeDirector.CanStartNarrative(program, node))
		{
			return false;
		}
		if (!EvaluateQuestProgramPredicates(node.Narrative?.Gates))
		{
			return false;
		}
		if (program.Runtime.PendingNarrativeNodeIds.Contains(node.NodeId))
		{
			return false;
		}
		program.Runtime.PendingNarrativeNodeIds.Add(node.NodeId);
		return true;
	}

	private bool TryKickoffQuestProgramNarrativePlayback()
	{
		if (_questProgramNarrativeCoroutine != null)
		{
			return false;
		}
		foreach (QuestProgram program in from candidate in EnsureQuestProgramState().Programs
			where candidate != null && candidate.Status == QuestProgramStatus.Active
			orderby GetMainlineQuestPriority(candidate?.Priority ?? 0) descending
			select candidate)
		{
			NormalizeQuestProgramRuntimeState(program);
			QuestNode node = ResolveCurrentQuestProgramNode(program);
			if (program.Runtime?.PendingNarrativeNodeIds == null || node == null || !program.Runtime.PendingNarrativeNodeIds.Contains(node.NodeId) || !_questNarrativeDirector.CanStartNarrative(program, node))
			{
				continue;
			}
			if (IsQuestProgramRuntimeTestMode())
			{
				return TryResolveQuestProgramNarrativeImmediatelyForTest(program, node);
			}
			_activeQuestProgramNarrativeProgramId = program.ProgramId ?? string.Empty;
			_activeQuestProgramNarrativeNodeId = node.NodeId ?? string.Empty;
			_questProgramNarrativeCoroutine = StartCoroutine(PlayQuestProgramNarrativeNodeCoroutine(program.ProgramId, node.NodeId));
			return false;
		}
		return false;
	}

	private bool IsQuestProgramRuntimeTestMode()
	{
		return _questProgramTestGameTime.HasValue || _questProgramTestLocationIds != null || (_questProgramTestNpcAliveStates != null && _questProgramTestNpcAliveStates.Count > 0) || (_questProgramTestCyFriendStates != null && _questProgramTestCyFriendStates.Count > 0) || (_questProgramTestNpcFavorValues != null && _questProgramTestNpcFavorValues.Count > 0) || (_questProgramTestPlayerItemCounts != null && _questProgramTestPlayerItemCounts.Count > 0) || (_questProgramTestPlayerSkillIds != null && _questProgramTestPlayerSkillIds.Count > 0) || (_questProgramTestPlayerStaticSkillIds != null && _questProgramTestPlayerStaticSkillIds.Count > 0) || _questProgramTestPlayerMoney.HasValue || _questProgramTestPlayerLevel.HasValue || _questProgramTestFuBenRemainingDays.HasValue || _questProgramTestSceneType.HasValue || (_questProgramTestNpcPatrolPresence != null && _questProgramTestNpcPatrolPresence.Count > 0) || (_questProgramTestOfficialStartedTaskIds != null && _questProgramTestOfficialStartedTaskIds.Count > 0) || (_questProgramTestOfficialFinishedTaskSteps != null && _questProgramTestOfficialFinishedTaskSteps.Count > 0) || (_questProgramTestOfficialRequiredTaskStepCounts != null && _questProgramTestOfficialRequiredTaskStepCounts.Count > 0) || (_questProgramTestOfficialTaskOutdatedStates != null && _questProgramTestOfficialTaskOutdatedStates.Count > 0) || (_questProgramTestNotificationRecords != null && _questProgramTestNotificationRecords.Count > 0) || (_questProgramTestCombatRecords != null && _questProgramTestCombatRecords.Count > 0) || _questProgramTestNpcLocations.Count > 0 || _questProgramTestNpcActions.Count > 0 || _questProgramTestNpcStatuses.Count > 0 || _questProgramTestNpcLockActions.Count > 0 || _questProgramTestChoiceSelections.Count > 0;
	}

	private bool TryResolveQuestProgramNarrativeImmediatelyForTest(QuestProgram program, QuestNode node)
	{
		bool changed = false;
		if (!TryBeginQuestProgramNarrativeNode(program, node, ref changed))
		{
			return changed;
		}
		List<QuestChoiceOption> availableChoices = BuildAvailableQuestProgramChoices(program, node);
		QuestChoiceOption selectedChoice = ResolveQuestProgramTestChoice(program, node, availableChoices);
		if (!TryFinishQuestProgramNarrativeNode(program, node, selectedChoice, ref changed))
		{
			return changed;
		}
		if (program.Status == QuestProgramStatus.Active && TryProgressQuestProgram(program))
		{
			changed = true;
		}
		if (program.Status == QuestProgramStatus.Active && TryKickoffQuestProgramNarrativePlayback())
		{
			changed = true;
		}
		return changed;
	}

	private IEnumerator PlayQuestProgramNarrativeNodeCoroutine(string programId, string nodeId)
	{
		try
		{
			QuestProgram program = GetQuestProgramById(programId);
			QuestNode node = ResolveQuestProgramNodeById(program, nodeId);
			bool changed = false;
			if (program == null || node == null || !TryBeginQuestProgramNarrativeNode(program, node, ref changed))
			{
				if (changed)
				{
					MarkQuestPersistenceDirty();
				}
				yield break;
			}
			if (node.Narrative?.Lines != null)
			{
				foreach (AIQuestStoryLine line in node.Narrative.Lines)
				{
					if (HasStoryLineText(line))
					{
						yield return StartCoroutine(PlayQuestProgramStoryLineCoroutine(program, line));
					}
				}
			}
			List<QuestChoiceOption> availableChoices = BuildAvailableQuestProgramChoices(program, node);
			QuestChoiceOption selectedChoice = null;
			if (availableChoices.Count > 0)
			{
				yield return StartCoroutine(ShowQuestProgramChoicesCoroutine(availableChoices, delegate(QuestChoiceOption choice)
				{
					selectedChoice = choice;
				}));
			}
			if (TryFinishQuestProgramNarrativeNode(program, node, selectedChoice, ref changed) && program.Status == QuestProgramStatus.Active && TryProgressQuestProgram(program))
			{
				changed = true;
			}
			if (changed)
			{
				MarkQuestPersistenceDirty();
			}
		}
		finally
		{
			CleanupStoryDialogs();
			ClearActiveQuestProgramNarrativeState();
			TryKickoffQuestProgramNarrativePlayback();
		}
	}

	private bool TryBeginQuestProgramNarrativeNode(QuestProgram program, QuestNode node, ref bool changed)
	{
		if (program == null || node == null || !_questNarrativeDirector.CanStartNarrative(program, node))
		{
			return false;
		}
		if (!EvaluateQuestProgramPredicates(node.Narrative?.Gates))
		{
			return false;
		}
		if (!TryExecuteQuestProgramCommandBlock(program, node.Narrative?.OnStarted, node.NodeId + ":narrative:onStarted", ref changed))
		{
			return false;
		}
		return true;
	}

	private bool TryFinishQuestProgramNarrativeNode(QuestProgram program, QuestNode node, QuestChoiceOption selectedChoice, ref bool changed)
	{
		if (program == null || node == null)
		{
			return false;
		}
		List<QuestChoiceOption> availableChoices = BuildAvailableQuestProgramChoices(program, node);
		if (node.Type == QuestNodeType.Choice || availableChoices.Count > 0)
		{
			if (selectedChoice == null)
			{
				program.Status = QuestProgramStatus.Suspended;
				program.Runtime.FailedReason = "剧情节点 " + (node.Title ?? node.NodeId) + " 没有可执行的分支选择";
				changed = true;
				return false;
			}
			if (!TryExecuteQuestProgramCommandBlock(program, selectedChoice.Consequences, node.NodeId + ":choice:" + selectedChoice.ChoiceId, ref changed))
			{
				return false;
			}
		}
		string nextNodeId = ((!string.IsNullOrWhiteSpace(selectedChoice?.NextNodeId)) ? selectedChoice.NextNodeId : node.NextNodeId);
		if (string.IsNullOrWhiteSpace(nextNodeId))
		{
			program.Status = QuestProgramStatus.Suspended;
			program.Runtime.FailedReason = "剧情节点 " + (node.Title ?? node.NodeId) + " 缺少 nextNodeId";
			changed = true;
			return false;
		}
		return CompleteQuestProgramNode(program, node, completesProgram: false, nextNodeId) | changed;
	}

	private List<QuestChoiceOption> BuildAvailableQuestProgramChoices(QuestProgram program, QuestNode node)
	{
		if (program == null || node?.Narrative?.Choices == null)
		{
			return new List<QuestChoiceOption>();
		}
		return node.Narrative.Choices.Where((QuestChoiceOption choice) => choice != null && EvaluateQuestProgramPredicates(choice.Requirements)).ToList();
	}

	private QuestChoiceOption ResolveQuestProgramTestChoice(QuestProgram program, QuestNode node, List<QuestChoiceOption> availableChoices)
	{
		if (availableChoices == null || availableChoices.Count == 0)
		{
			return null;
		}
		string selectionKey = (program?.ProgramId ?? string.Empty) + "::" + (node?.NodeId ?? string.Empty);
		if (_questProgramTestChoiceSelections.TryGetValue(selectionKey, out var requestedChoiceId) && !string.IsNullOrWhiteSpace(requestedChoiceId))
		{
			QuestChoiceOption selected = availableChoices.FirstOrDefault((QuestChoiceOption choice) => string.Equals(choice.ChoiceId, requestedChoiceId, StringComparison.Ordinal));
			if (selected != null)
			{
				_questProgramTestChoiceSelections.Remove(selectionKey);
				return selected;
			}
		}
		_questProgramTestChoiceSelections.Remove(selectionKey);
		return availableChoices[0];
	}

	private QuestNode ResolveQuestProgramNodeById(QuestProgram program, string nodeId)
	{
		if (program?.Nodes == null || string.IsNullOrWhiteSpace(nodeId))
		{
			return null;
		}
		return program.Nodes.FirstOrDefault((QuestNode node) => node != null && string.Equals(node.NodeId, nodeId, StringComparison.Ordinal));
	}

	private bool CompleteQuestProgramNode(QuestProgram program, QuestNode node, bool completesProgram, string explicitNextNodeId = null)
	{
		if (program == null || node == null)
		{
			return false;
		}
		bool changed = false;
		if (!TryExecuteQuestProgramCommandBlock(program, node.OnCompleted, node.NodeId + ":onCompleted", ref changed))
		{
			return changed;
		}
		if (!program.Runtime.CompletedNodeIds.Contains(node.NodeId))
		{
			program.Runtime.CompletedNodeIds.Add(node.NodeId);
			changed = true;
		}
		if (program.Runtime.PendingNarrativeNodeIds.Remove(node.NodeId))
		{
			changed = true;
		}
		string nextNodeId = (string.IsNullOrWhiteSpace(explicitNextNodeId) ? node.NextNodeId : explicitNextNodeId);
		if (completesProgram || string.IsNullOrWhiteSpace(nextNodeId))
		{
			return CompleteQuestProgram(program) || changed;
		}
		QuestNode nextNode = program.Nodes?.FirstOrDefault((QuestNode candidate) => candidate != null && string.Equals(candidate.NodeId, nextNodeId, StringComparison.Ordinal));
		if (nextNode == null)
		{
			return CompleteQuestProgram(program) || changed;
		}
		if (!string.Equals(program.Runtime.CurrentNodeId, nextNode.NodeId, StringComparison.Ordinal))
		{
			program.Runtime.CurrentNodeId = nextNode.NodeId;
			changed = true;
		}
		if (!program.Runtime.ActivatedNodeIds.Contains(nextNode.NodeId))
		{
			program.Runtime.ActivatedNodeIds.Add(nextNode.NodeId);
			changed = true;
		}
		return changed;
	}

	private bool CompleteQuestProgram(QuestProgram program)
	{
		if (program == null)
		{
			return false;
		}
		bool changed = false;
		if (!TryExecuteQuestProgramCommandBlock(program, program.OnCompleted, program.ProgramId + ":onCompleted", ref changed))
		{
			return changed;
		}
		if (program.Status != QuestProgramStatus.Completed)
		{
			program.Status = QuestProgramStatus.Completed;
			changed = true;
		}
		if (EvaluateCampaignProgress(program))
		{
			changed = true;
		}
		if (RestoreQuestProgramForcedNpcStates(program))
		{
			changed = true;
		}
		if (string.Equals(_activeQuestProgramNarrativeProgramId, program.ProgramId, StringComparison.Ordinal))
		{
			ClearActiveQuestProgramNarrativeState();
		}
		return changed;
	}

	private bool TryExecuteQuestProgramCommandBlock(QuestProgram program, IEnumerable<QuestCommandInvocation> commands, string executionScope, ref bool changed)
	{
		if (commands == null)
		{
			return true;
		}
		int index = 0;
		foreach (QuestCommandInvocation command in commands)
		{
			string executionKey = $"{executionScope}:{index}";
			if (program.Runtime.ExecutedCommandKeys.Contains(executionKey))
			{
				index++;
				continue;
			}
			if (!TryExecuteQuestProgramCommand(program, command))
			{
				RestoreQuestProgramForcedNpcStates(program);
				program.Status = QuestProgramStatus.Suspended;
				program.Runtime.FailedReason = "命令未获放行: " + (command?.Opcode ?? "<null>");
				changed = true;
				return false;
			}
			program.Runtime.ExecutedCommandKeys.Add(executionKey);
			changed = true;
			index++;
		}
		return true;
	}

	private bool TryExecuteQuestProgramCommand(QuestProgram program, QuestCommandInvocation command)
	{
		if (!_questCommandExecutor.CanExecute(program, command) || command == null)
		{
			return false;
		}
		Dictionary<string, string> parameters = command.Params ?? new Dictionary<string, string>();
		return command.Opcode switch
		{
			"npc.set_action" => TryExecuteQuestProgramSetNpcAction(program, parameters),
			"npc.add_favor" => TryExecuteQuestProgramAddNpcFavor(parameters),
			"npc.add_cy_friend" => TryExecuteQuestProgramAddCyFriend(parameters),
			"npc.set_status" => TryExecuteQuestProgramSetNpcStatus(program, parameters),
			"npc.relocate" => TryExecuteQuestProgramRelocateNpc(program, parameters),
			"npc.kill" => TryExecuteQuestProgramKillNpc(parameters),
			"player.add_item" => TryExecuteQuestProgramAddPlayerItem(parameters),
			"player.add_money" => TryExecuteQuestProgramAddPlayerMoney(parameters),
			"player.learn_skill" => TryExecuteQuestProgramLearnPlayerSkill(parameters),
			"player.learn_static_skill" => TryExecuteQuestProgramLearnPlayerStaticSkill(parameters),
			"mail.send_notification" => TryExecuteQuestProgramSendNotification(program, parameters),
			"official_task.start" => TryExecuteQuestProgramStartOfficialTask(parameters),
			"official_task.finish_step" => TryExecuteQuestProgramFinishOfficialTaskStep(parameters),
			"combat.start_standard" => TryExecuteQuestProgramStartStandardCombat(parameters),
			"scene.load" => TryExecuteQuestProgramLoadScene(program, parameters),
			_ => false,
		};
	}

	private bool TryExecuteQuestProgramAddNpcFavor(Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramNpcId(parameters, out var npcId) || !TryParseQuestProgramIntParam(parameters, "amount", out var amount))
		{
			return false;
		}
		if (IsQuestProgramRuntimeTestMode())
		{
			_questProgramTestNpcFavorValues.TryGetValue(npcId, out var currentFavor);
			_questProgramTestNpcFavorValues[npcId] = currentFavor + amount;
			return true;
		}
		using (BeginQuestProgramNpcFavorMutationScope())
		{
			NPCEx.AddFavor(npcId, amount);
		}
		return true;
	}

	private bool TryExecuteQuestProgramAddCyFriend(Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramNpcId(parameters, out var npcId))
		{
			return false;
		}
		if (IsQuestProgramRuntimeTestMode())
		{
			_questProgramTestCyFriendStates[npcId] = true;
			return true;
		}
		Tools.instance?.getPlayer()?.AddFriend(npcId);
		return true;
	}

	private bool TryExecuteQuestProgramAddPlayerItem(Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramIntParam(parameters, "itemId", out var itemId) || !TryParseQuestProgramIntParam(parameters, "count", out var count) || itemId <= 0)
		{
			return false;
		}
		if (count == 0)
		{
			return true;
		}
		if (IsQuestProgramRuntimeTestMode())
		{
			_questProgramTestPlayerItemCounts.TryGetValue(itemId, out var currentCount);
			_questProgramTestPlayerItemCounts[itemId] = Math.Max(0, currentCount + count);
			return true;
		}
		Avatar player = Tools.instance?.getPlayer();
		if (player == null)
		{
			return false;
		}
		using (BeginQuestProgramPlayerItemMutationScope())
		{
			if (count > 0)
			{
				player.addItem(itemId, count, Tools.CreateItemSeid(itemId));
			}
			else
			{
				player.removeItem(itemId, Math.Abs(count));
			}
		}
		return true;
	}

	private bool TryExecuteQuestProgramAddPlayerMoney(Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramIntParam(parameters, "amount", out var amount))
		{
			return false;
		}
		if (IsQuestProgramRuntimeTestMode())
		{
			int currentMoney = _questProgramTestPlayerMoney.GetValueOrDefault();
			_questProgramTestPlayerMoney = Math.Max(0, currentMoney + amount);
			return true;
		}
		Avatar player = Tools.instance?.getPlayer();
		if (player == null)
		{
			return false;
		}
		using (BeginQuestProgramPlayerMoneyMutationScope())
		{
			player.AddMoney(amount);
		}
		return true;
	}

	private bool TryExecuteQuestProgramLearnPlayerSkill(Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramIntParam(parameters, "skillId", out var skillId) || skillId <= 0)
		{
			return false;
		}
		if (IsQuestProgramRuntimeTestMode())
		{
			_questProgramTestPlayerSkillIds.Add(skillId);
			return true;
		}
		Avatar player = Tools.instance?.getPlayer();
		if (player == null)
		{
			return false;
		}
		using (BeginQuestProgramPlayerSkillMutationScope())
		{
			player.addHasSkillList(skillId);
		}
		return PlayerEx.HasSkill(skillId);
	}

	private bool TryExecuteQuestProgramLearnPlayerStaticSkill(Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramIntParam(parameters, "skillId", out var skillId) || skillId <= 0)
		{
			return false;
		}
		int level = 1;
		if (TryParseQuestProgramIntParam(parameters, "level", out var explicitLevel) && explicitLevel > 0)
		{
			level = explicitLevel;
		}
		if (IsQuestProgramRuntimeTestMode())
		{
			_questProgramTestPlayerStaticSkillIds.Add(skillId);
			return true;
		}
		Avatar player = Tools.instance?.getPlayer();
		if (player == null)
		{
			return false;
		}
		using (BeginQuestProgramPlayerSkillMutationScope())
		{
			player.addHasStaticSkillList(skillId, level);
		}
		return PlayerEx.HasStaticSkill(skillId);
	}

	private bool TryExecuteQuestProgramKillNpc(Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramNpcId(parameters, out var npcId))
		{
			return false;
		}
		int deathType = 1;
		if (TryParseQuestProgramIntParam(parameters, "deathType", out var explicitDeathType) && explicitDeathType > 0)
		{
			deathType = explicitDeathType;
		}
		int killerNpcId = 0;
		TryParseQuestProgramIntParam(parameters, "killerNpcId", out killerNpcId);
		if (IsQuestProgramRuntimeTestMode())
		{
			_questProgramTestNpcAliveStates[npcId] = false;
			return true;
		}
		if (NpcJieSuanManager.inst?.npcDeath == null)
		{
			return false;
		}
		using (BeginQuestProgramNpcDeathMutationScope())
		{
			NpcJieSuanManager.inst.npcDeath.SetNpcDeath(deathType, npcId, killerNpcId);
		}
		return !IsQuestProgramNpcAlive(npcId);
	}

	private bool TryExecuteQuestProgramSendNotification(QuestProgram program, Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramNpcId(parameters, out var npcId) || !TryGetQuestProgramTextParam(parameters, "message", out var message))
		{
			return false;
		}
		string rawNpcName;
		string npcName = (TryGetQuestProgramTextParam(parameters, "npcName", out rawNpcName) ? rawNpcName : ResolveQuestProgramNpcName(npcId));
		string rawHistoryMessage;
		string historyMessage = (TryGetQuestProgramTextParam(parameters, "historyMessage", out rawHistoryMessage) ? rawHistoryMessage : message);
		if (IsQuestProgramRuntimeTestMode())
		{
			bool wasQueued = !IsQuestProgramCyFriend(npcId);
			_questProgramTestNotificationRecords.Add(new QuestProgramTestNotificationRecord
			{
				NpcId = npcId,
				NpcName = npcName,
				Message = message,
				HistoryMessage = historyMessage,
				WasQueued = wasQueued
			});
			if (!string.IsNullOrWhiteSpace(historyMessage))
			{
				AppendQuestTaskMemory(npcId, npcName, historyMessage, program?.ProgramId ?? string.Empty, program?.Runtime?.CurrentNodeId ?? string.Empty);
			}
			return true;
		}
		QuestMailEnvelope envelope = BuildNotificationMailEnvelope(npcId, npcName, message, historyMessage, program?.ProgramId ?? string.Empty, program?.Runtime?.CurrentNodeId ?? string.Empty);
		if (QuestCyMailHelper.HasNpcCyFriend(npcId) && TrySendQuestMailEnvelope(envelope))
		{
			return true;
		}
		EnqueuePendingQuestMail(envelope);
		return true;
	}

	private bool TryExecuteQuestProgramStartOfficialTask(Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramIntParam(parameters, "taskId", out var taskId) || taskId <= 0)
		{
			return false;
		}
		if (IsQuestProgramRuntimeTestMode())
		{
			_questProgramTestOfficialStartedTaskIds.Add(taskId);
			if (!_questProgramTestOfficialFinishedTaskSteps.ContainsKey(taskId))
			{
				_questProgramTestOfficialFinishedTaskSteps[taskId] = new HashSet<int>();
			}
			return true;
		}
		Avatar player = Tools.instance?.getPlayer();
		if (player?.taskMag == null)
		{
			return false;
		}
		using (BeginQuestProgramOfficialTaskMutationScope())
		{
			player.taskMag.addTask(taskId);
		}
		return player.taskMag.isHasTask(taskId);
	}

	private bool TryExecuteQuestProgramFinishOfficialTaskStep(Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramIntParam(parameters, "taskId", out var taskId) || !TryParseQuestProgramIntParam(parameters, "taskIndex", out var taskIndex) || taskId <= 0 || taskIndex <= 0)
		{
			return false;
		}
		if (IsQuestProgramRuntimeTestMode())
		{
			if (!_questProgramTestOfficialStartedTaskIds.Contains(taskId))
			{
				return false;
			}
			if (!_questProgramTestOfficialFinishedTaskSteps.TryGetValue(taskId, out var finishedSteps))
			{
				finishedSteps = new HashSet<int>();
				_questProgramTestOfficialFinishedTaskSteps[taskId] = finishedSteps;
			}
			finishedSteps.Add(taskIndex);
			return true;
		}
		Avatar player = Tools.instance?.getPlayer();
		if (player?.taskMag == null || !player.taskMag.isHasTask(taskId))
		{
			return false;
		}
		using (BeginQuestProgramOfficialTaskMutationScope())
		{
			SetTaskIndexFinish.Do(taskId, taskIndex);
		}
		return IsQuestProgramOfficialTaskStepFinished(taskId, taskIndex);
	}

	private bool TryExecuteQuestProgramStartStandardCombat(Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramNpcId(parameters, out var npcId))
		{
			return false;
		}
		string rawFightMusic;
		string fightMusic = (TryGetQuestProgramTextParam(parameters, "fightMusic", out rawFightMusic) ? rawFightMusic : "战斗3");
		if (IsQuestProgramRuntimeTestMode())
		{
			_questProgramTestCombatRecords.Add(new QuestProgramTestCombatRecord
			{
				NpcId = npcId,
				FightMusic = fightMusic
			});
			return true;
		}
		if (Tools.instance?.getPlayer() == null)
		{
			return false;
		}
		StartFight.Do(npcId, 1, StartFight.MonstarType.Normal, StartFight.FightEnumType.Normal, 0, 0, 0, 0, fightMusic, SeaRemoveNPCFlag: false, string.Empty, new List<StarttFightAddBuff>(), new List<StarttFightAddBuff>());
		return true;
	}

	private bool TryExecuteQuestProgramLoadScene(QuestProgram program, Dictionary<string, string> parameters)
	{
		if (!TryResolveQuestProgramSceneTarget(parameters, out var normalizedSceneId))
		{
			return false;
		}
		if (_questProgramTestLocationIds != null)
		{
			_questProgramTestLocationIds = BuildQuestProgramRuntimeLocationSnapshotForScene(normalizedSceneId);
			return true;
		}
		if (QuestElementPicker.TryParseFuBenLocationId(normalizedSceneId, out var fuBenSceneId, out var fuBenNodeIndex))
		{
			int targetNodeIndex = ((fuBenNodeIndex <= 0) ? 1 : fuBenNodeIndex);
			LoadFuBen.loadfuben(fuBenSceneId, targetNodeIndex);
			return true;
		}
		if (normalizedSceneId.StartsWith("S", StringComparison.Ordinal) && int.TryParse(normalizedSceneId.Substring(1), out var sceneMapId))
		{
			AvatarTransfer.Do(sceneMapId);
			Tools.instance.loadMapScenes(normalizedSceneId);
			return true;
		}
		if (int.TryParse(normalizedSceneId, out var bigMapNodeId))
		{
			AvatarTransfer.Do(bigMapNodeId);
			Tools.instance.loadMapScenes("AllMaps");
			return true;
		}
		return false;
	}

	private bool TryExecuteQuestProgramSetNpcAction(QuestProgram program, Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramNpcId(parameters, out var npcId) || !TryParseQuestProgramIntParam(parameters, "actionId", out var actionId) || !EnsureQuestProgramForcedNpcState(program, npcId, parameters))
		{
			return false;
		}
		string rawLockAction;
		bool parsedLockAction = default(bool);
		bool lockAction = parameters.TryGetValue("lockAction", out rawLockAction) && bool.TryParse(rawLockAction, out parsedLockAction) && parsedLockAction;
		if (HasQuestProgramRuntimeTestNpcWorldState(npcId))
		{
			_questProgramTestNpcActions[npcId] = actionId;
			if (lockAction)
			{
				_questProgramTestNpcLockActions[npcId] = actionId;
			}
			return true;
		}
		int normalizedNpcId = NPCEx.NPCIDToNew(npcId);
		using (BeginQuestProgramNpcActionMutationScope())
		{
			NPCEx.SetNPCAction(normalizedNpcId, actionId);
		}
		if (lockAction)
		{
			(NpcJieSuanManager.inst?.GetNpcData(normalizedNpcId))?.SetField("LockAction", actionId);
		}
		if (NpcJieSuanManager.inst != null)
		{
			NpcJieSuanManager.inst.isUpDateNpcList = true;
		}
		return true;
	}

	private bool TryExecuteQuestProgramSetNpcStatus(QuestProgram program, Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramNpcId(parameters, out var npcId) || !TryParseQuestProgramIntParam(parameters, "statusId", out var statusId) || !EnsureQuestProgramForcedNpcState(program, npcId, parameters))
		{
			return false;
		}
		if (HasQuestProgramRuntimeTestNpcWorldState(npcId))
		{
			_questProgramTestNpcStatuses[npcId] = statusId;
			return true;
		}
		int normalizedNpcId = NPCEx.NPCIDToNew(npcId);
		using (BeginQuestProgramNpcStatusMutationScope())
		{
			NpcJieSuanManager.inst?.npcStatus?.SetNpcStatus(normalizedNpcId, statusId);
		}
		if (NpcJieSuanManager.inst != null)
		{
			NpcJieSuanManager.inst.isUpDateNpcList = true;
		}
		return true;
	}

	private bool TryExecuteQuestProgramRelocateNpc(QuestProgram program, Dictionary<string, string> parameters)
	{
		if (!TryParseQuestProgramNpcId(parameters, out var npcId) || !parameters.TryGetValue("sceneId", out var sceneId) || string.IsNullOrWhiteSpace(sceneId) || !EnsureQuestProgramForcedNpcState(program, npcId, parameters))
		{
			return false;
		}
		string normalizedSceneId = sceneId.Trim();
		if (HasQuestProgramRuntimeTestNpcWorldState(npcId))
		{
			_questProgramTestNpcLocations[npcId] = normalizedSceneId;
			return true;
		}
		bool moved = TryRelocateNpcToLocation(npcId, normalizedSceneId);
		if (moved && NpcJieSuanManager.inst != null)
		{
			NpcJieSuanManager.inst.isUpDateNpcList = true;
		}
		return moved;
	}

	private bool EnsureQuestProgramForcedNpcState(QuestProgram program, int npcId, Dictionary<string, string> parameters)
	{
		if (program == null || npcId <= 0)
		{
			return false;
		}
		program.Runtime.ActiveForcedNpcStates = program.Runtime.ActiveForcedNpcStates ?? new List<QuestForcedNpcState>();
		int normalizedNpcId = NPCEx.NPCIDToNew(npcId);
		if (program.Runtime.ActiveForcedNpcStates.Any((QuestForcedNpcState state) => state != null && NPCEx.NPCIDToNew(state.NpcId) == normalizedNpcId))
		{
			return true;
		}
		QuestForcedNpcState snapshot = CaptureQuestProgramForcedNpcState(npcId, parameters);
		if (snapshot == null)
		{
			return false;
		}
		program.Runtime.ActiveForcedNpcStates.Add(snapshot);
		return true;
	}

	private QuestForcedNpcState CaptureQuestProgramForcedNpcState(int npcId, Dictionary<string, string> parameters)
	{
		string rawNpcName;
		string npcName = ((parameters != null && parameters.TryGetValue("npcName", out rawNpcName)) ? rawNpcName : string.Empty);
		if (_questProgramTestNpcLocations.TryGetValue(npcId, out var locationId) && _questProgramTestNpcActions.TryGetValue(npcId, out var actionId) && _questProgramTestNpcStatuses.TryGetValue(npcId, out var statusId))
		{
			_questProgramTestNpcLockActions.TryGetValue(npcId, out var lockActionId);
			return new QuestForcedNpcState
			{
				NpcId = npcId,
				NpcName = npcName,
				OriginalLocationId = locationId,
				OriginalActionId = actionId,
				OriginalStatusId = statusId,
				HadLockAction = (lockActionId > 0),
				OriginalLockActionId = lockActionId
			};
		}
		return CaptureForcedNpcState(npcId, npcName);
	}

	private static QuestForcedNpcState CaptureForcedNpcState(int npcId, string npcName)
	{
		int normalizedNpcId = NPCEx.NPCIDToNew(npcId);
		JSONObject npcData = NpcJieSuanManager.inst?.GetNpcData(normalizedNpcId);
		if (npcData == null)
		{
			return null;
		}
		return new QuestForcedNpcState
		{
			NpcId = normalizedNpcId,
			NpcName = ((!string.IsNullOrWhiteSpace(npcName)) ? npcName : QuestElementPicker.GetNpcDisplayName(normalizedNpcId)),
			OriginalLocationId = QuestElementPicker.GetNpcCurrentScene(normalizedNpcId),
			OriginalActionId = npcData["ActionId"].I,
			OriginalStatusId = npcData["Status"]["StatusId"].I,
			HadLockAction = npcData.HasField("LockAction"),
			OriginalLockActionId = (npcData.HasField("LockAction") ? npcData["LockAction"].I : 0)
		};
	}

	private bool TryRelocateNpcToLocation(int npcId, string targetLocationId)
	{
		if (npcId <= 0 || string.IsNullOrWhiteSpace(targetLocationId) || NpcJieSuanManager.inst?.npcMap == null)
		{
			return false;
		}
		int normalizedNpcId = NPCEx.NPCIDToNew(npcId);
		string normalizedTargetLocationId = targetLocationId.Trim();
		if (NpcJieSuanManager.inst.allBigMapNpcList != null)
		{
			NpcJieSuanManager.inst.allBigMapNpcList.Remove(normalizedNpcId);
		}
		if (normalizedTargetLocationId.StartsWith("S", StringComparison.Ordinal))
		{
			NpcJieSuanManager.inst.npcMap.RemoveNpcByList(normalizedNpcId);
			NpcJieSuanManager.inst.npcMap.AddNpcToThreeScene(normalizedNpcId, normalizedTargetLocationId);
			return true;
		}
		if (normalizedTargetLocationId.StartsWith("F", StringComparison.Ordinal))
		{
			string[] parts = normalizedTargetLocationId.Split(',');
			if (!int.TryParse(parts[0].Substring(1), out var fuBenSceneId))
			{
				return false;
			}
			int fuBenNode = 1;
			if (parts.Length >= 2 && int.TryParse(parts[1], out var parsedNode))
			{
				fuBenNode = parsedNode;
			}
			NpcJieSuanManager.inst.npcMap.RemoveNpcByList(normalizedNpcId);
			NpcJieSuanManager.inst.npcMap.AddNpcToFuBen(normalizedNpcId, fuBenSceneId, fuBenNode);
			return true;
		}
		if (int.TryParse(normalizedTargetLocationId, out var bigMapIndex))
		{
			NpcJieSuanManager.inst.npcMap.RemoveNpcByList(normalizedNpcId);
			Dictionary<int, List<int>> bigMapNpcDictionary = NpcJieSuanManager.inst.npcMap.bigMapNPCDictionary;
			if (!bigMapNpcDictionary.TryGetValue(bigMapIndex, out var npcList))
			{
				npcList = (bigMapNpcDictionary[bigMapIndex] = new List<int>());
			}
			if (!npcList.Contains(normalizedNpcId))
			{
				npcList.Add(normalizedNpcId);
			}
			if (NPCEx.GetFavor(normalizedNpcId) < 200 && NpcJieSuanManager.inst.allBigMapNpcList != null && !NpcJieSuanManager.inst.allBigMapNpcList.Contains(normalizedNpcId))
			{
				NpcJieSuanManager.inst.allBigMapNpcList.Add(normalizedNpcId);
			}
			return true;
		}
		return false;
	}

	private bool RestoreQuestProgramForcedNpcStates(QuestProgram program)
	{
		if (program?.Runtime?.ActiveForcedNpcStates == null || program.Runtime.ActiveForcedNpcStates.Count == 0)
		{
			return false;
		}
		bool changed = false;
		foreach (QuestForcedNpcState state in program.Runtime.ActiveForcedNpcStates.ToList())
		{
			if (state == null || state.NpcId <= 0)
			{
				continue;
			}
			int npcId = NPCEx.NPCIDToNew(state.NpcId);
			if (HasQuestProgramRuntimeTestNpcWorldState(npcId))
			{
				_questProgramTestNpcLocations[npcId] = state.OriginalLocationId ?? string.Empty;
				_questProgramTestNpcActions[npcId] = state.OriginalActionId;
				_questProgramTestNpcStatuses[npcId] = state.OriginalStatusId;
				_questProgramTestNpcLockActions[npcId] = (state.HadLockAction ? state.OriginalLockActionId : 0);
				changed = true;
			}
			else
			{
				if (NpcJieSuanManager.inst == null || NpcJieSuanManager.inst.IsDeath(npcId))
				{
					continue;
				}
				JSONObject npcData = NpcJieSuanManager.inst.GetNpcData(npcId);
				if (npcData == null)
				{
					continue;
				}
				TryRelocateNpcToLocation(npcId, state.OriginalLocationId);
				using (BeginQuestProgramNpcActionMutationScope())
				{
					NPCEx.SetNPCAction(npcId, state.OriginalActionId);
				}
				if (state.OriginalStatusId > 0)
				{
					using (BeginQuestProgramNpcStatusMutationScope())
					{
						NpcJieSuanManager.inst.npcStatus.SetNpcStatus(npcId, state.OriginalStatusId);
					}
				}
				if (state.HadLockAction)
				{
					npcData.SetField("LockAction", state.OriginalLockActionId);
				}
				else if (npcData.HasField("LockAction"))
				{
					npcData.RemoveField("LockAction");
				}
				changed = true;
			}
		}
		program.Runtime.ActiveForcedNpcStates.Clear();
		if (changed && NpcJieSuanManager.inst != null)
		{
			NpcJieSuanManager.inst.isUpDateNpcList = true;
		}
		return changed;
	}

	private bool TryParseQuestProgramNpcId(Dictionary<string, string> parameters, out int npcId)
	{
		npcId = 0;
		string rawNpcId;
		return parameters != null && parameters.TryGetValue("npcId", out rawNpcId) && int.TryParse(rawNpcId, out npcId) && npcId > 0;
	}

	private static bool TryParseQuestProgramIntParam(Dictionary<string, string> parameters, string key, out int value)
	{
		value = 0;
		string rawValue;
		return parameters != null && parameters.TryGetValue(key, out rawValue) && int.TryParse(rawValue, out value);
	}

	private static bool TryGetQuestProgramTextParam(Dictionary<string, string> parameters, string key, out string value)
	{
		value = string.Empty;
		string rawValue;
		return parameters != null && parameters.TryGetValue(key, out rawValue) && !string.IsNullOrWhiteSpace(rawValue) && !string.IsNullOrWhiteSpace(value = rawValue.Trim());
	}

	private static string ResolveQuestProgramNpcName(int npcId)
	{
		if (npcId <= 0)
		{
			return "NPC";
		}
		JSONObject npcData = NpcJieSuanManager.inst?.GetNpcData(npcId);
		if (npcData != null && npcData.HasField("Name"))
		{
			string npcName = npcData["Name"].Str;
			if (!string.IsNullOrWhiteSpace(npcName))
			{
				return npcName;
			}
		}
		return $"NPC_{npcId}";
	}

	private static string ResolvePlayerFacingQuestNpcName(int npcId, string preferredName = null, string genericName = "目标人物")
	{
		string candidateName = ((!string.IsNullOrWhiteSpace(preferredName)) ? preferredName.Trim() : ResolveQuestProgramNpcName(npcId));
		return (string.IsNullOrWhiteSpace(candidateName) || string.Equals(candidateName, "NPC", StringComparison.Ordinal) || candidateName.StartsWith("NPC_", StringComparison.Ordinal)) ? genericName : candidateName;
	}

	private static string SanitizePlayerFacingQuestText(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		string sanitized = text.Trim();
		sanitized = QuestPlayerFacingNpcLabelPattern.Replace(sanitized, delegate(Match match)
		{
			int npcId = ParseQuestPlayerFacingNpcId(match.Groups[1].Value);
			return ResolvePlayerFacingQuestNpcName(npcId);
		});
		return QuestPlayerFacingNpcIdPattern.Replace(sanitized, delegate(Match match)
		{
			int npcId = ParseQuestPlayerFacingNpcId(match.Groups[1].Value);
			return ResolvePlayerFacingQuestNpcName(npcId);
		});
	}

	private static int ParseQuestPlayerFacingNpcId(string rawNpcId)
	{
		int npcId;
		return int.TryParse(rawNpcId, out npcId) ? npcId : 0;
	}

	private static string ResolvePlayerFacingQuestSceneName(string sceneId, QuestElementPicker.BeatExecutionContext executionContext, string fallbackSceneName = null, string genericName = "约定地点")
	{
		string candidateName = ResolveCompiledSceneName(sceneId, executionContext, fallbackSceneName);
		return (string.IsNullOrWhiteSpace(candidateName) || string.Equals(candidateName, sceneId, StringComparison.Ordinal)) ? genericName : candidateName;
	}

	private bool TryResolveQuestProgramSceneTarget(Dictionary<string, string> parameters, out string normalizedSceneId)
	{
		normalizedSceneId = string.Empty;
		if (parameters == null)
		{
			return false;
		}
		if (parameters.TryGetValue("sceneId", out var rawSceneId) && QuestElementPicker.TryNormalizeOfficialLocationId(rawSceneId, out normalizedSceneId))
		{
			return true;
		}
		string rawSceneName;
		return parameters.TryGetValue("sceneName", out rawSceneName) && QuestElementPicker.TryResolveOfficialLocationId(rawSceneName, out normalizedSceneId);
	}

	private static HashSet<string> BuildQuestProgramRuntimeLocationSnapshotForScene(string normalizedSceneId)
	{
		HashSet<string> locationIds = new HashSet<string>(StringComparer.Ordinal);
		if (string.IsNullOrWhiteSpace(normalizedSceneId))
		{
			return locationIds;
		}
		locationIds.Add(normalizedSceneId);
		if (QuestElementPicker.TryParseFuBenLocationId(normalizedSceneId, out var fuBenSceneId, out var _))
		{
			locationIds.Add(fuBenSceneId);
		}
		return locationIds;
	}

	private bool EvaluateQuestProgramPredicates(IEnumerable<QuestPredicateInvocation> predicates)
	{
		return _questPredicateEngine.EvaluateAll(predicates, BuildQuestProgramPredicateContext());
	}

	private QuestPredicateEvaluationContext BuildQuestProgramPredicateContext()
	{
		DateTime now;
		return new QuestPredicateEvaluationContext
		{
			CurrentGameTime = (TryGetQuestProgramRuntimeGameTime(out now) ? new DateTime?(now) : ((DateTime?)null)),
			IsSceneMatch = IsQuestProgramSceneMatch,
			GetCurrentSceneType = GetQuestProgramRuntimeSceneType,
			GetFuBenRemainingDays = GetQuestProgramRuntimeFuBenRemainingDays,
			IsNpcAlive = IsQuestProgramNpcAlive,
			IsNpcStatus = IsQuestProgramNpcStatus,
			IsNpcAction = IsQuestProgramNpcAction,
			HasTalkedToNpc = HasQuestProgramTalkedToNpc,
			IsCyFriend = IsQuestProgramCyFriend,
			IsNpcPresentInCurrentScene = IsQuestProgramNpcPresentInCurrentScene,
			IsNpcPatrolPresent = IsQuestProgramNpcPatrolPresent,
			IsNpcPresentInScene = IsQuestProgramNpcPresentInScene,
			HasRecentBattleVictory = HasQuestProgramRecentBattleVictory,
			HasRecentDeliveryMailSubmission = HasQuestProgramRecentDeliveryMailSubmission,
			GetPlayerItemCount = GetQuestProgramRuntimePlayerItemCount,
			GetPlayerMoney = GetQuestProgramRuntimePlayerMoney,
			GetPlayerLevel = GetQuestProgramRuntimePlayerLevel,
			HasPlayerSkill = HasQuestProgramPlayerSkill,
			HasPlayerStaticSkill = HasQuestProgramPlayerStaticSkill,
			GetNpcFavor = GetQuestProgramRuntimeNpcFavor,
			IsOfficialTaskStarted = IsQuestProgramOfficialTaskStarted,
			IsOfficialTaskNotOutdated = IsQuestProgramOfficialTaskNotOutdated,
			IsOfficialTaskStepFinished = IsQuestProgramOfficialTaskStepFinished,
			IsOfficialTaskFinishable = IsQuestProgramOfficialTaskFinishable
		};
	}

	private bool TryGetQuestProgramRuntimeGameTime(out DateTime gameTime)
	{
		if (_questProgramTestGameTime.HasValue)
		{
			gameTime = _questProgramTestGameTime.Value;
			return true;
		}
		return TryGetGameTime(out gameTime);
	}

	private HashSet<string> GetQuestProgramRuntimeLocationIds()
	{
		if (_questProgramTestLocationIds != null)
		{
			return new HashSet<string>(_questProgramTestLocationIds, StringComparer.Ordinal);
		}
		return GetCurrentLocationIds();
	}

	private bool IsQuestProgramSceneMatch(string sceneId, string sceneName)
	{
		string normalizedTargetLocationId = sceneId;
		if (!QuestElementPicker.TryNormalizeOfficialLocationId(normalizedTargetLocationId, out normalizedTargetLocationId) && !QuestElementPicker.TryResolveOfficialLocationId(sceneName, out normalizedTargetLocationId))
		{
			return false;
		}
		foreach (string currentLocationId in GetQuestProgramRuntimeLocationIds())
		{
			if (LocationIdMatchesTargetScene(currentLocationId, normalizedTargetLocationId, sceneName))
			{
				return true;
			}
		}
		return false;
	}

	private int? GetQuestProgramRuntimeFuBenRemainingDays()
	{
		if (_questProgramTestFuBenRemainingDays.HasValue)
		{
			return _questProgramTestFuBenRemainingDays.Value;
		}
		Avatar player = Tools.instance?.getPlayer();
		if (player?.fubenContorl == null)
		{
			return null;
		}
		string screenName = string.Empty;
		try
		{
			screenName = Tools.getScreenName();
		}
		catch (Exception ex)
		{
			LogQuestWarning("GetQuestProgramRuntimeFuBenRemainingDays screenName", ex);
		}
		if (string.IsNullOrWhiteSpace(screenName) || jsonData.instance?.FuBenInfoJsonData == null || !jsonData.instance.FuBenInfoJsonData.HasField(screenName))
		{
			return null;
		}
		return player.fubenContorl[screenName].ResidueTimeDay;
	}

	private int? GetQuestProgramRuntimeSceneType()
	{
		if (_questProgramTestSceneType.HasValue)
		{
			return _questProgramTestSceneType.Value;
		}
		try
		{
			return SceneEx.GetNowSceneType();
		}
		catch (Exception ex)
		{
			LogQuestWarning("GetQuestProgramRuntimeSceneType", ex);
			return null;
		}
	}

	private bool IsQuestProgramNpcAlive(int npcId)
	{
		if (_questProgramTestNpcAliveStates != null && _questProgramTestNpcAliveStates.TryGetValue(npcId, out var alive))
		{
			return alive;
		}
		if (npcId <= 0 || NpcJieSuanManager.inst == null)
		{
			return false;
		}
		return !NpcJieSuanManager.inst.IsDeath(npcId);
	}

	private bool IsQuestProgramNpcStatus(int npcId, int statusId)
	{
		if (npcId <= 0 || statusId <= 0)
		{
			return false;
		}
		if (_questProgramTestNpcStatuses != null && _questProgramTestNpcStatuses.TryGetValue(npcId, out var testStatusId))
		{
			return testStatusId == statusId;
		}
		return NpcJieSuanManager.inst?.npcStatus != null && NpcJieSuanManager.inst.npcStatus.IsInTargetStatus(npcId, statusId);
	}

	private bool IsQuestProgramNpcAction(int npcId, int actionId)
	{
		if (npcId <= 0 || actionId <= 0)
		{
			return false;
		}
		if (_questProgramTestNpcActions != null && _questProgramTestNpcActions.TryGetValue(npcId, out var testActionId))
		{
			return testActionId == actionId;
		}
		int normalizedNpcId = NPCEx.NPCIDToNew(npcId);
		JSONObject npcData = NpcJieSuanManager.inst?.GetNpcData(normalizedNpcId);
		return npcData != null && npcData["ActionId"].I == actionId;
	}

	private bool HasQuestProgramRecentBattleVictory(int npcId)
	{
		if (npcId <= 0)
		{
			return false;
		}
		CleanupRecentBattleVictories();
		float winTime;
		return _recentBattleVictories.TryGetValue(npcId, out winTime) && Time.realtimeSinceStartup - winTime <= 15f;
	}

	private bool HasQuestProgramRecentDeliveryMailSubmission(Dictionary<string, string> parameters)
	{
		CleanupQuestProgramRecentDeliveryMailSubmissions();
		int npcIdFilter = 0;
		int itemIdFilter = 0;
		int mailIdFilter = 0;
		string questIdFilter = string.Empty;
		string stepIdFilter = string.Empty;
		if (parameters != null)
		{
			if (parameters.TryGetValue("npcId", out var rawNpcId))
			{
				int.TryParse(rawNpcId, out npcIdFilter);
			}
			if (parameters.TryGetValue("itemId", out var rawItemId))
			{
				int.TryParse(rawItemId, out itemIdFilter);
			}
			if (parameters.TryGetValue("mailId", out var rawMailId))
			{
				int.TryParse(rawMailId, out mailIdFilter);
			}
			if (parameters.TryGetValue("questId", out var rawQuestId))
			{
				questIdFilter = rawQuestId?.Trim() ?? string.Empty;
			}
			if (parameters.TryGetValue("stepId", out var rawStepId))
			{
				stepIdFilter = rawStepId?.Trim() ?? string.Empty;
			}
		}
		int normalizedNpcIdFilter = ((npcIdFilter > 0) ? QuestCyMailHelper.NormalizeNpcId(npcIdFilter) : 0);
		foreach (QuestProgramRecentDeliveryMailSubmission submission in _recentQuestProgramDeliveryMailSubmissions)
		{
			if (submission == null || (normalizedNpcIdFilter > 0 && QuestCyMailHelper.NormalizeNpcId(submission.NpcId) != normalizedNpcIdFilter) || (itemIdFilter > 0 && submission.ItemId != itemIdFilter) || (mailIdFilter != 0 && submission.MailId != mailIdFilter) || (!string.IsNullOrWhiteSpace(questIdFilter) && !string.Equals(submission.QuestId ?? string.Empty, questIdFilter, StringComparison.Ordinal)) || (!string.IsNullOrWhiteSpace(stepIdFilter) && !string.Equals(submission.StepId ?? string.Empty, stepIdFilter, StringComparison.Ordinal)))
			{
				continue;
			}
			return true;
		}
		return false;
	}

	private bool HasQuestProgramTalkedToNpc(int npcId)
	{
		bool talked = default(bool);
		return npcId > 0 && _talkFlags != null && _talkFlags.TryGetValue(npcId, out talked) && talked;
	}

	private bool IsQuestProgramCyFriend(int npcId)
	{
		if (_questProgramTestCyFriendStates != null && _questProgramTestCyFriendStates.TryGetValue(npcId, out var isFriend))
		{
			return isFriend;
		}
		Avatar player = Tools.instance?.getPlayer();
		return npcId > 0 && player?.emailDateMag != null && player.emailDateMag.IsFriend(npcId);
	}

	private bool IsQuestProgramNpcPatrolPresent(int npcId)
	{
		if (npcId <= 0)
		{
			return false;
		}
		if (_questProgramTestNpcPatrolPresence != null && _questProgramTestNpcPatrolPresence.TryGetValue(npcId, out var isPresent))
		{
			return isPresent;
		}
		Avatar player = Tools.instance?.getPlayer();
		string screenName = Tools.getScreenName();
		if (player == null || string.IsNullOrWhiteSpace(screenName) || jsonData.instance?.FuBenInfoJsonData == null || !jsonData.instance.FuBenInfoJsonData.HasField(screenName) || player.fubenContorl == null || NpcJieSuanManager.inst == null)
		{
			return false;
		}
		int nowIndex;
		try
		{
			nowIndex = player.fubenContorl[screenName].NowIndex;
		}
		catch
		{
			return false;
		}
		return NpcJieSuanManager.inst.GetXunLuoNpcList(screenName, nowIndex).Contains(NPCEx.NPCIDToNew(npcId));
	}

	private int GetQuestProgramRuntimePlayerItemCount(Dictionary<string, string> parameters)
	{
		if (parameters == null)
		{
			return 0;
		}
		if (_questProgramTestPlayerItemCounts != null && _questProgramTestPlayerItemCounts.Count > 0)
		{
			if (parameters.TryGetValue("itemId", out var rawTestItemId) && int.TryParse(rawTestItemId, out var testItemId) && testItemId > 0 && _questProgramTestPlayerItemCounts.TryGetValue(testItemId, out var testCount))
			{
				return Math.Max(0, testCount);
			}
			return 0;
		}
		Avatar player = Tools.instance?.getPlayer();
		if (player == null)
		{
			return 0;
		}
		if (parameters.TryGetValue("itemId", out var rawItemId) && int.TryParse(rawItemId, out var itemId) && itemId > 0)
		{
			return Math.Max(0, player.getItemNum(itemId));
		}
		string rawItemName;
		string itemName = (parameters.TryGetValue("itemName", out rawItemName) ? rawItemName : string.Empty);
		int itemQuality = 0;
		if (parameters.TryGetValue("itemQuality", out var rawItemQuality))
		{
			int.TryParse(rawItemQuality, out itemQuality);
		}
		List<int> resolvedIds = ResolveEquivalentItemIds(0, itemName, itemQuality);
		return (resolvedIds.Count != 0) ? GetPlayerEquivalentItemCount(resolvedIds) : 0;
	}

	private long GetQuestProgramRuntimePlayerMoney()
	{
		if (_questProgramTestPlayerMoney.HasValue)
		{
			return _questProgramTestPlayerMoney.Value;
		}
		return (long)((Tools.instance?.getPlayer())?.money ?? 0);
	}

	private int GetQuestProgramRuntimePlayerLevel()
	{
		if (_questProgramTestPlayerLevel.HasValue)
		{
			return _questProgramTestPlayerLevel.Value;
		}
		return (Tools.instance?.getPlayer())?.level ?? 0;
	}

	private bool HasQuestProgramPlayerSkill(int skillId)
	{
		if (skillId <= 0)
		{
			return false;
		}
		if (_questProgramTestPlayerSkillIds != null && _questProgramTestPlayerSkillIds.Count > 0)
		{
			return _questProgramTestPlayerSkillIds.Contains(skillId);
		}
		return PlayerEx.HasSkill(skillId);
	}

	private bool HasQuestProgramPlayerStaticSkill(int skillId)
	{
		if (skillId <= 0)
		{
			return false;
		}
		if (_questProgramTestPlayerStaticSkillIds != null && _questProgramTestPlayerStaticSkillIds.Count > 0)
		{
			return _questProgramTestPlayerStaticSkillIds.Contains(skillId);
		}
		return PlayerEx.HasStaticSkill(skillId);
	}

	private int GetQuestProgramRuntimeNpcFavor(int npcId)
	{
		if (npcId <= 0)
		{
			return 0;
		}
		if (_questProgramTestNpcFavorValues != null && _questProgramTestNpcFavorValues.TryGetValue(npcId, out var favorValue))
		{
			return favorValue;
		}
		return NPCEx.GetFavor(npcId);
	}

	private int GetQuestProgramRuntimeNpcAction(int npcId)
	{
		if (npcId <= 0)
		{
			return 0;
		}
		if (_questProgramTestNpcActions != null && _questProgramTestNpcActions.TryGetValue(npcId, out var actionId))
		{
			return actionId;
		}
		int normalizedNpcId = NPCEx.NPCIDToNew(npcId);
		return ((NpcJieSuanManager.inst?.GetNpcData(normalizedNpcId))?["ActionId"]?.I).GetValueOrDefault();
	}

	private bool IsQuestProgramNpcPresentInCurrentScene(int npcId)
	{
		if (npcId <= 0)
		{
			return false;
		}
		if (!TryGetQuestProgramRuntimeNpcLocationId(npcId, out var npcLocationId))
		{
			return false;
		}
		foreach (string currentLocationId in GetQuestProgramRuntimeLocationIds())
		{
			if (LocationIdMatchesTargetScene(currentLocationId, npcLocationId, QuestElementPicker.BuildSceneHierarchy(npcLocationId)))
			{
				return true;
			}
		}
		return false;
	}

	private bool IsQuestProgramNpcPresentInScene(int npcId, string sceneId, string sceneName)
	{
		if (npcId <= 0 || !TryGetQuestProgramRuntimeNpcLocationId(npcId, out var npcLocationId))
		{
			return false;
		}
		string normalizedTargetLocationId = sceneId?.Trim() ?? string.Empty;
		if (!QuestElementPicker.TryNormalizeOfficialLocationId(normalizedTargetLocationId, out normalizedTargetLocationId) && !QuestElementPicker.TryResolveOfficialLocationId(sceneName, out normalizedTargetLocationId))
		{
			return false;
		}
		return LocationIdMatchesTargetScene(npcLocationId, normalizedTargetLocationId, sceneName);
	}

	private bool TryGetQuestProgramRuntimeNpcLocationId(int npcId, out string locationId)
	{
		locationId = string.Empty;
		if (npcId <= 0)
		{
			return false;
		}
		if (_questProgramTestNpcLocations != null && _questProgramTestNpcLocations.TryGetValue(npcId, out var testLocationId) && !string.IsNullOrWhiteSpace(testLocationId))
		{
			locationId = testLocationId.Trim();
			return true;
		}
		string npcLocationId = QuestElementPicker.GetNpcCurrentScene(npcId);
		if (string.IsNullOrWhiteSpace(npcLocationId))
		{
			return false;
		}
		locationId = npcLocationId.Trim();
		return true;
	}

	private bool IsQuestProgramOfficialTaskStarted(int taskId)
	{
		if (taskId <= 0)
		{
			return false;
		}
		if (_questProgramTestOfficialStartedTaskIds != null && _questProgramTestOfficialStartedTaskIds.Count > 0)
		{
			return _questProgramTestOfficialStartedTaskIds.Contains(taskId);
		}
		Avatar player = Tools.instance?.getPlayer();
		return player?.taskMag != null && player.taskMag.isHasTask(taskId);
	}

	private bool IsQuestProgramOfficialTaskNotOutdated(int taskId)
	{
		if (taskId <= 0)
		{
			return false;
		}
		if (_questProgramTestOfficialTaskOutdatedStates != null && _questProgramTestOfficialTaskOutdatedStates.TryGetValue(taskId, out var isOutdated))
		{
			return !isOutdated;
		}
		Avatar player = Tools.instance?.getPlayer();
		JSONObject taskData = player?.taskMag?._TaskData;
		if (player?.taskMag == null || taskData == null || !player.taskMag.isHasTask(taskId) || !taskData.HasField("Task") || !taskData["Task"].HasField(taskId.ToString()))
		{
			return false;
		}
		return !TaskUIManager.checkIsGuoShi(taskData["Task"][taskId.ToString()]);
	}

	private bool IsQuestProgramOfficialTaskStepFinished(int taskId, int taskIndex)
	{
		if (taskId <= 0 || taskIndex <= 0)
		{
			return false;
		}
		if (_questProgramTestOfficialFinishedTaskSteps != null && _questProgramTestOfficialFinishedTaskSteps.TryGetValue(taskId, out var finishedSteps))
		{
			return finishedSteps.Contains(taskIndex);
		}
		JSONObject taskData = (Tools.instance?.getPlayer())?.taskMag?._TaskData;
		if (taskData == null || !taskData.HasField("Task") || !taskData["Task"].HasField(taskId.ToString()) || !taskData["Task"][taskId.ToString()].HasField("finishIndex"))
		{
			return false;
		}
		foreach (JSONObject indexJson in taskData["Task"][taskId.ToString()]["finishIndex"].list)
		{
			if (indexJson != null && indexJson.I == taskIndex)
			{
				return true;
			}
		}
		return false;
	}

	private bool IsQuestProgramOfficialTaskFinishable(int taskId)
	{
		if (taskId <= 0)
		{
			return false;
		}
		HashSet<int> finishedSteps;
		if (_questProgramTestOfficialRequiredTaskStepCounts != null && _questProgramTestOfficialRequiredTaskStepCounts.TryGetValue(taskId, out var requiredCount) && requiredCount > 0)
		{
			return _questProgramTestOfficialStartedTaskIds.Contains(taskId) && _questProgramTestOfficialFinishedTaskSteps.TryGetValue(taskId, out finishedSteps) && finishedSteps.Count >= requiredCount;
		}
		Avatar player = Tools.instance?.getPlayer();
		JSONObject taskData = player?.taskMag?._TaskData;
		if (player?.taskMag == null || taskData == null || !player.taskMag.isHasTask(taskId) || !taskData.HasField("Task") || !taskData["Task"].HasField(taskId.ToString()) || !taskData["Task"][taskId.ToString()].HasField("finishIndex"))
		{
			return false;
		}
		int totalStepCount = jsonData.instance.TaskInfoJsonData.list.Count((JSONObject info) => info["TaskID"].I == taskId);
		if (totalStepCount <= 0)
		{
			return false;
		}
		HashSet<int> finishedIndices = new HashSet<int>();
		foreach (JSONObject indexJson in taskData["Task"][taskId.ToString()]["finishIndex"].list)
		{
			if (indexJson != null && indexJson.I > 0)
			{
				finishedIndices.Add(indexJson.I);
			}
		}
		return finishedIndices.Count >= totalStepCount;
	}

	private QuestProgram GetQuestProgramById(string programId)
	{
		if (string.IsNullOrWhiteSpace(programId))
		{
			return null;
		}
		return EnsureQuestProgramState().Programs.FirstOrDefault((QuestProgram program) => program != null && string.Equals(program.ProgramId, programId, StringComparison.Ordinal));
	}

	private void UpsertQuestProgram(QuestProgram program)
	{
		if (program != null)
		{
			QuestProgramState state = EnsureQuestProgramState();
			int index = state.Programs.FindIndex((QuestProgram candidate) => candidate != null && string.Equals(candidate.ProgramId, program.ProgramId, StringComparison.Ordinal));
			if (index >= 0)
			{
				state.Programs[index] = program;
			}
			else
			{
				state.Programs.Add(program);
			}
		}
	}

	private bool RemoveQuestProgram(string programId)
	{
		if (string.IsNullOrWhiteSpace(programId))
		{
			return false;
		}
		QuestProgramState state = EnsureQuestProgramState();
		int removed = state.Programs.RemoveAll((QuestProgram program) => program != null && string.Equals(program.ProgramId, programId, StringComparison.Ordinal));
		return removed > 0;
	}

	private bool HasMainlineQuestProgram(QuestProgramStatus status)
	{
		return EnsureQuestProgramState().Programs.Any((QuestProgram program) => program != null && program.Status == status);
	}

	private bool CanQuestProgramClaimWorldControl(QuestProgram candidate, QuestProgram currentOwner)
	{
		if (candidate == null)
		{
			return false;
		}
		if (currentOwner == null)
		{
			return true;
		}
		if (string.Equals(candidate.ProgramId, currentOwner.ProgramId, StringComparison.Ordinal))
		{
			return true;
		}
		return GetMainlineQuestPriority(candidate?.Priority ?? 0) > GetMainlineQuestPriority(currentOwner?.Priority ?? 0);
	}

	private MainlineChapter ResolveQuestChapter(MainlineCampaign campaign, string chapterId)
	{
		if (campaign?.Chapters == null || string.IsNullOrWhiteSpace(chapterId))
		{
			return null;
		}
		return campaign.Chapters.FirstOrDefault((MainlineChapter chapter) => string.Equals(chapter?.ChapterId, chapterId, StringComparison.Ordinal));
	}

	private MainlineBeat ResolveQuestBeat(MainlineCampaign campaign, string chapterId, string beatId)
	{
		MainlineChapter chapter = ResolveQuestChapter(campaign, chapterId);
		if (chapter?.CurrentBeat == null || string.IsNullOrWhiteSpace(beatId))
		{
			return null;
		}
		return string.Equals(chapter.CurrentBeat.BeatId, beatId, StringComparison.Ordinal) ? chapter.CurrentBeat : null;
	}

	private bool AdvanceMainlineBeat(MainlineCampaign campaign, QuestProgram program)
	{
		if (campaign == null || program == null || string.IsNullOrWhiteSpace(program.ChapterId) || string.IsNullOrWhiteSpace(program.BeatId))
		{
			return false;
		}
		MainlineChapter chapter = ResolveQuestChapter(campaign, program.ChapterId);
		MainlineBeat beat = ResolveQuestBeat(campaign, program.ChapterId, program.BeatId);
		if (chapter == null || beat == null)
		{
			return false;
		}
		beat.Status = MainlineBeatStatus.Completed;
		if (beat.QuestIds == null)
		{
			beat.QuestIds = new List<string>();
		}
		if (!string.IsNullOrWhiteSpace(program.ProgramId) && !beat.QuestIds.Contains(program.ProgramId))
		{
			beat.QuestIds.Add(program.ProgramId);
		}
		PendingBiographyEventState pendingEvent = chapter.PendingBiographyEvent;
		if (pendingEvent == null)
		{
			chapter.Status = MainlineChapterStatus.Completed;
			campaign.Status = CampaignStatus.Active;
			return true;
		}
		ResolvePendingBiographyEventSuccess(campaign, chapter, beat, program);
		return true;
	}

	private bool EvaluateCampaignProgress(QuestProgram program)
	{
		if (program == null || program.TrackType != QuestTrackType.Mainline)
		{
			return false;
		}
		return AdvanceMainlineBeat(_mainlineCampaign, program);
	}

	private bool ShouldRequestPendingBiographyGeneration(MainlineChapter chapter, PendingBiographyEventState pendingEvent)
	{
		if (chapter == null || pendingEvent == null || pendingEvent.GenerationAttempted)
		{
			return false;
		}
		if (!string.IsNullOrWhiteSpace(pendingEvent.LastGenerationError))
		{
			return false;
		}
		if (pendingEvent.Status == PendingBiographyEventStatus.Resolved || pendingEvent.Status == PendingBiographyEventStatus.Restored || pendingEvent.Status == PendingBiographyEventStatus.ActiveStory)
		{
			return false;
		}
		if (HasMainlineQuestProgram(QuestProgramStatus.Active) || HasMainlineQuestProgram(QuestProgramStatus.Draft))
		{
			return false;
		}
		return true;
	}

	private bool TryProgressPendingBiographyStory()
	{
		MainlineChapter chapter = _mainlineCampaign?.GetCurrentChapter();
		PendingBiographyEventState pendingEvent = chapter?.PendingBiographyEvent;
		if (!_pendingBiographyStoryGenerationRequested || chapter == null || pendingEvent == null || pendingEvent.GenerationAttempted || _isGenerating)
		{
			return false;
		}
		NPCInfo npcInfo = ResolvePendingBiographyEventNpcInfo(pendingEvent);
		if (npcInfo == null || npcInfo.NpcID <= 0)
		{
			_pendingBiographyStoryGenerationRequested = false;
			pendingEvent.LastGenerationError = $"无法读取锚点人物 {pendingEvent.NpcName}({pendingEvent.NpcId}) 的主线上下文";
			return false;
		}
		_pendingBiographyStoryGenerationRequested = false;
		pendingEvent.GenerationAttempted = true;
		pendingEvent.LastGenerationError = null;
		UIPopTip.Inst?.Pop(pendingEvent.NpcName + "的官方生平事件正在改写为剧情", PopTipIconType.任务进度);
		StartManagedCoroutine(GenerateOrResumeMainlineCoroutine(npcInfo, delegate(QuestProgram program)
		{
			PendingBiographyEventState pendingBiographyEventState = _mainlineCampaign?.GetCurrentChapter()?.PendingBiographyEvent;
			if (pendingBiographyEventState != null)
			{
				pendingBiographyEventState.GenerationAttempted = false;
				pendingBiographyEventState.Status = PendingBiographyEventStatus.ActiveStory;
				pendingBiographyEventState.LastGenerationError = null;
				pendingBiographyEventState.ConsecutiveGenerationFailures = 0;
			}
			MainlineChapter mainlineChapter = _mainlineCampaign?.GetCurrentChapter();
			if (mainlineChapter != null)
			{
				mainlineChapter.Status = MainlineChapterStatus.Active;
			}
			if (_mainlineCampaign != null)
			{
				_mainlineCampaign.Status = CampaignStatus.Active;
			}
			AcceptQuestProgram(program.ProgramId);
			MarkQuestPersistenceDirty();
			UIPopTip.Inst?.Pop(program.Title + " 已生成并开始执行", PopTipIconType.任务进度);
		}, delegate(string error)
		{
			FailPendingBiographyGeneration(error ?? "当前剧情生成失败");
			UIPopTip.Inst?.Pop(error ?? "当前剧情生成失败");
		}));
		return true;
	}

	private void FailPendingBiographyGeneration(string failureReason)
	{
		MainlineChapter chapter = _mainlineCampaign?.GetCurrentChapter();
		PendingBiographyEventState pendingEvent = chapter?.PendingBiographyEvent;
		if (pendingEvent != null)
		{
			pendingEvent.GenerationAttempted = false;
			pendingEvent.LastGenerationError = failureReason ?? "当前剧情生成失败";
			pendingEvent.ConsecutiveGenerationFailures++;
			pendingEvent.Status = PendingBiographyEventStatus.Restored;
			_pendingBiographyStoryGenerationRequested = false;
			MainlineBeat currentBeat = chapter?.GetCurrentBeat();
			if (currentBeat != null && currentBeat.Status == MainlineBeatStatus.Active)
			{
				currentBeat.Status = MainlineBeatStatus.Failed;
			}
			if (chapter != null)
			{
				chapter.PendingBiographyEvent = null;
				chapter.Status = MainlineChapterStatus.Failed;
			}
			if (_mainlineCampaign != null)
			{
				_mainlineCampaign.Status = CampaignStatus.Active;
			}
			MarkQuestPersistenceDirty();
		}
	}

	internal bool TryInterceptOfficialBiographyEvent(BiographyEventKind eventKind, int npcId, string sourceMethodKey, JObject originalPayload, string originalOfficialTime = null)
	{
		if (_suppressBiographyEventInterception || npcId <= 0)
		{
			return false;
		}
		TryLoadPersistenceIfReady();
		MainlineCampaign campaign = _mainlineCampaign;
		if (campaign == null || campaign.SeedNpcId != npcId)
		{
			return false;
		}
		if (campaign.GetCurrentChapter()?.PendingBiographyEvent != null)
		{
			AIChatManager.logger.LogInfo((object)$"[AIQuestManager] 官方生平事件未改写: NPC={campaign.SeedNpcName}({npcId}), kind={eventKind}, source={sourceMethodKey}, reason=已有PendingBiographyEvent");
			return false;
		}
		if (HasMainlineQuestProgram(QuestProgramStatus.Active))
		{
			AIChatManager.logger.LogInfo((object)$"[AIQuestManager] 官方生平事件未改写: NPC={campaign.SeedNpcName}({npcId}), kind={eventKind}, source={sourceMethodKey}, reason=已有Active主线");
			return false;
		}
		if (HasMainlineQuestProgram(QuestProgramStatus.Draft))
		{
			AIChatManager.logger.LogInfo((object)$"[AIQuestManager] 官方生平事件未改写: NPC={campaign.SeedNpcName}({npcId}), kind={eventKind}, source={sourceMethodKey}, reason=已有Draft主线");
			return false;
		}
		NPCInfo npcInfo = NpcDataExtractor.ExtractFromGameMemory(npcId);
		if (npcInfo == null)
		{
			AIChatManager.logger.LogWarning((object)$"[AIQuestManager] 官方生平事件未改写: NPC={campaign.SeedNpcName}({npcId}), kind={eventKind}, source={sourceMethodKey}, reason=无法抽取NPC上下文");
			return false;
		}
		MainlineChapter chapter = EnsureCurrentMainlineChapter(campaign, npcInfo);
		DateTime now;
		DateTime capturedAt = (TryGetGameTime(out now) ? now : DateTime.Now);
		chapter.PendingBiographyEvent = new PendingBiographyEventState
		{
			EventId = Guid.NewGuid().ToString("N"),
			NpcId = npcId,
			NpcName = (string.IsNullOrWhiteSpace(npcInfo.Name) ? (campaign.SeedNpcName ?? string.Empty) : npcInfo.Name.Trim()),
			BiographyEventKind = eventKind,
			SourceMethodKey = (sourceMethodKey ?? string.Empty),
			CapturedAtGameTime = capturedAt.ToString("yyyy-MM-dd HH:mm:ss"),
			OriginalOfficialTime = (string.IsNullOrWhiteSpace(originalOfficialTime) ? capturedAt.ToString("yyyy-MM-dd HH:mm:ss") : originalOfficialTime.Trim()),
			OriginalPayload = (originalPayload ?? new JObject()),
			Status = PendingBiographyEventStatus.Captured,
			CampaignId = campaign.CampaignId,
			Summary = BuildBiographyEventSummaryText(eventKind, npcInfo.Name),
			PlayerFacingHint = BuildBiographyEventPlayerHint(eventKind, npcInfo.Name)
		};
		chapter.Status = MainlineChapterStatus.Active;
		campaign.Status = CampaignStatus.Active;
		_pendingBiographyStoryGenerationRequested = true;
		AIChatManager.logger.LogInfo((object)$"[AIQuestManager] 官方生平事件已拦截: NPC={chapter.PendingBiographyEvent.NpcName}({npcId}), kind={eventKind}, source={sourceMethodKey}, officialTime={chapter.PendingBiographyEvent.OriginalOfficialTime}");
		MarkQuestPersistenceDirty();
		return true;
	}

	private NPCInfo ResolvePendingBiographyEventNpcInfo(PendingBiographyEventState pendingEvent)
	{
		if (pendingEvent == null || pendingEvent.NpcId <= 0)
		{
			return null;
		}
		NPCInfo npcInfo = NpcDataExtractor.ExtractFromGameMemory(pendingEvent.NpcId);
		if (npcInfo == null)
		{
			return null;
		}
		if (string.IsNullOrWhiteSpace(npcInfo.Name))
		{
			npcInfo.Name = pendingEvent.NpcName ?? string.Empty;
		}
		return npcInfo;
	}

	private void ApplyPendingBiographyEventTiming(PendingBiographyEventState pendingEvent, NPCInfo npcInfo, MainlineBeat beat)
	{
		if (pendingEvent != null && npcInfo != null && beat?.TimeIntent != null)
		{
			string timeBand = (string.IsNullOrWhiteSpace(beat.TimeIntent.TimeBand) ? "中期" : beat.TimeIntent.TimeBand.Trim());
			int maxSpanMonths = QuestElementPicker.ResolveTimeBandMaxMonthsForNpc(npcInfo, timeBand);
			beat.TimeIntent.TimeBand = timeBand;
			beat.TimeIntent.MaxSpanMonths = maxSpanMonths;
			if (string.IsNullOrWhiteSpace(pendingEvent.TimeCategory))
			{
				pendingEvent.TimeCategory = timeBand;
			}
			if (!(pendingEvent.DeadlineGameTime != DateTime.MinValue) && maxSpanMonths > 0)
			{
				DateTime parsedCapturedAt;
				DateTime now;
				DateTime capturedAt = (DateTime.TryParse(pendingEvent.CapturedAtGameTime, out parsedCapturedAt) ? parsedCapturedAt : (TryGetGameTime(out now) ? now : DateTime.Now));
				pendingEvent.DeadlineGameTime = AddGameMonths(capturedAt, maxSpanMonths);
			}
		}
	}

	private void ResolvePendingBiographyEventSuccess(MainlineCampaign campaign, MainlineChapter chapter, MainlineBeat beat, QuestProgram program)
	{
		PendingBiographyEventState pendingEvent = chapter?.PendingBiographyEvent;
		ApplyCompletedBeatSummary(beat, program);
		if (pendingEvent != null)
		{
			WriteBiographyStorySummaryNotebookEntry(pendingEvent, beat, program);
			pendingEvent.Status = PendingBiographyEventStatus.Resolved;
		}
		if (chapter != null)
		{
			chapter.PendingBiographyEvent = null;
			chapter.Status = MainlineChapterStatus.Completed;
		}
		if (campaign != null)
		{
			campaign.Status = CampaignStatus.Active;
		}
	}

	private void ApplyCompletedBeatSummary(MainlineBeat beat, QuestProgram program)
	{
		if (beat != null)
		{
			string summaryText = ResolveCompletedBeatSummary(beat, program);
			if (!string.IsNullOrWhiteSpace(summaryText))
			{
				beat.Summary = summaryText;
			}
		}
	}

	private string ResolveCompletedBeatSummary(MainlineBeat beat, QuestProgram program)
	{
		QuestNode currentNode = ResolveCurrentQuestProgramNode(program);
		if (!string.IsNullOrWhiteSpace(currentNode?.Summary))
		{
			return currentNode.Summary.Trim();
		}
		return beat?.Summary?.Trim() ?? string.Empty;
	}

	private void WriteBiographyStorySummaryNotebookEntry(PendingBiographyEventState pendingEvent, MainlineBeat beat, QuestProgram program)
	{
		if (pendingEvent != null && pendingEvent.NpcId > 0)
		{
			string summaryText = ResolveCompletedBeatSummary(beat, program);
			if (!string.IsNullOrWhiteSpace(summaryText))
			{
				DateTime now;
				string eventTime = (TryGetGameTime(out now) ? now.ToString("yyyy-MM-dd HH:mm:ss") : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				string eventDesc = (string.IsNullOrWhiteSpace(beat?.Title) ? summaryText : (beat.Title.Trim() + "：" + summaryText));
				NPCEx.AddEvent(pendingEvent.NpcId, eventTime, eventDesc);
			}
		}
	}

	private bool TryHandlePendingBiographyEventTimeout()
	{
		PendingBiographyEventState pendingEvent = _mainlineCampaign?.GetCurrentChapter()?.PendingBiographyEvent;
		if (pendingEvent == null || pendingEvent.DeadlineGameTime == DateTime.MinValue)
		{
			return false;
		}
		if (!TryGetGameTime(out var now) || now < pendingEvent.DeadlineGameTime)
		{
			return false;
		}
		RestorePendingBiographyEventAsFailure("已超过" + pendingEvent.NpcName + "当前剧情链的期限");
		return true;
	}

	private void RestorePendingBiographyEventAsFailure(string failureReason)
	{
		MainlineChapter chapter = _mainlineCampaign?.GetCurrentChapter();
		PendingBiographyEventState pendingEvent = chapter?.PendingBiographyEvent;
		if (pendingEvent != null)
		{
			pendingEvent.Status = PendingBiographyEventStatus.Restored;
			pendingEvent.LastGenerationError = failureReason ?? "当前剧情链失败";
			_pendingBiographyStoryGenerationRequested = false;
			MarkProgramsFailedForPendingBiographyEvent(pendingEvent, pendingEvent.LastGenerationError);
			ReplayOfficialBiographyEvent(pendingEvent);
			MainlineBeat currentBeat = chapter?.GetCurrentBeat();
			if (currentBeat != null && currentBeat.Status == MainlineBeatStatus.Active)
			{
				currentBeat.Status = MainlineBeatStatus.Failed;
			}
			if (chapter != null)
			{
				chapter.PendingBiographyEvent = null;
				chapter.Status = MainlineChapterStatus.Failed;
			}
			if (_mainlineCampaign != null)
			{
				_mainlineCampaign.Status = CampaignStatus.Active;
			}
			MarkQuestPersistenceDirty();
		}
	}

	private void MarkProgramsFailedForPendingBiographyEvent(PendingBiographyEventState pendingEvent, string failureReason)
	{
		if (pendingEvent == null)
		{
			return;
		}
		foreach (QuestProgram program in EnsureQuestProgramState().Programs.Where((QuestProgram candidate) => candidate != null && candidate.TrackType == QuestTrackType.Mainline && string.Equals(candidate.CampaignId, pendingEvent.CampaignId, StringComparison.Ordinal) && (candidate.Status == QuestProgramStatus.Active || candidate.Status == QuestProgramStatus.Draft)).ToList())
		{
			program.Status = QuestProgramStatus.Failed;
			program.Runtime = program.Runtime ?? new QuestRuntimeState();
			program.Runtime.FailedReason = failureReason ?? "当前剧情链失败";
			RestoreQuestProgramForcedNpcStates(program);
			if (string.Equals(_activeQuestProgramNarrativeProgramId, program.ProgramId, StringComparison.Ordinal))
			{
				ClearActiveQuestProgramNarrativeState();
			}
		}
	}

	private void ReplayOfficialBiographyEvent(PendingBiographyEventState pendingEvent)
	{
		if (pendingEvent == null || string.IsNullOrWhiteSpace(pendingEvent.SourceMethodKey))
		{
			return;
		}
		_suppressBiographyEventInterception = true;
		try
		{
			switch (pendingEvent.SourceMethodKey)
			{
			case "NPCUseItem.UseDanYao":
				ReplayUseDanYaoEvent(pendingEvent);
				break;
			case "NPCTuPo.NpcSmallToPo":
				ReplayNpcSmallTuPoEvent(pendingEvent);
				break;
			case "NPCTuPo.NpcTuPoZhuJi":
			case "NPCTuPo.NpcTuPoJinDan":
			case "NPCTuPo.NpcTuPoYuanYing":
			case "NPCTuPo.NpcTuPoHuaShen":
			case "NPCFuYe.NpcLianDan":
			case "NPCFuYe.NpcLianQi":
			case "NPCLiLian.NPCYouLi":
				ReplaySourceMethodEvent(pendingEvent);
				break;
			case "NpcJieSuanManager.CheckImportantEvent":
				ReplayImportantEvent(pendingEvent);
				break;
			case "NPCTeShu.NextNpcPaiMai":
				ReplayNpcPaiMaiEvent(pendingEvent);
				break;
			case "NPCTeShu.NextJieSha":
				ReplayNpcJieShaEvent(pendingEvent);
				break;
			case "NPCXiuLian.NextNpcLunDao":
				ReplayNpcLunDaoEvent(pendingEvent);
				break;
			case "TianJiDaBiManager.AddMatchPlayerEvent":
				ReplayTianJiDaBiEvent(pendingEvent);
				break;
			default:
				throw new InvalidOperationException("未实现的官方生平 replay: " + pendingEvent.SourceMethodKey);
			}
		}
		finally
		{
			_suppressBiographyEventInterception = false;
		}
	}

	private void ReplayUseDanYaoEvent(PendingBiographyEventState pendingEvent)
	{
		int npcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "npcId");
		int itemId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "itemId");
		int seid = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "seid");
		JSONObject item = FindNpcBackpackItemByItemId(npcId, itemId);
		AccessTools.Method(typeof(NPCUseItem), "UseDanYao", (Type[])null, (Type[])null)?.Invoke(NpcJieSuanManager.inst?.npcUseItem, new object[3] { npcId, item, seid });
	}

	private void ReplayNpcSmallTuPoEvent(PendingBiographyEventState pendingEvent)
	{
		int npcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "npcId");
		int nextLevel = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "nextLevel");
		JSONObject npcData = NpcJieSuanManager.inst?.GetNpcData(npcId);
		AccessTools.Method(typeof(NPCTuPo), "NpcSmallToPo", (Type[])null, (Type[])null)?.Invoke(NpcJieSuanManager.inst?.npcTuPo, new object[2] { npcData, nextLevel });
	}

	private void ReplaySourceMethodEvent(PendingBiographyEventState pendingEvent)
	{
		string sourceMethodKey = pendingEvent.SourceMethodKey ?? string.Empty;
		int npcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "npcId");
		bool isKuaiSu = ResolveBiographyPayloadBool(pendingEvent.OriginalPayload, "isKuaiSu");
		switch (sourceMethodKey)
		{
		case "NPCTuPo.NpcTuPoZhuJi":
			AccessTools.Method(typeof(NPCTuPo), "NpcTuPoZhuJi", (Type[])null, (Type[])null)?.Invoke(NpcJieSuanManager.inst?.npcTuPo, new object[2] { npcId, isKuaiSu });
			break;
		case "NPCTuPo.NpcTuPoJinDan":
			AccessTools.Method(typeof(NPCTuPo), "NpcTuPoJinDan", (Type[])null, (Type[])null)?.Invoke(NpcJieSuanManager.inst?.npcTuPo, new object[2] { npcId, isKuaiSu });
			break;
		case "NPCTuPo.NpcTuPoYuanYing":
			AccessTools.Method(typeof(NPCTuPo), "NpcTuPoYuanYing", (Type[])null, (Type[])null)?.Invoke(NpcJieSuanManager.inst?.npcTuPo, new object[2] { npcId, isKuaiSu });
			break;
		case "NPCTuPo.NpcTuPoHuaShen":
			AccessTools.Method(typeof(NPCTuPo), "NpcTuPoHuaShen", (Type[])null, (Type[])null)?.Invoke(NpcJieSuanManager.inst?.npcTuPo, new object[2] { npcId, isKuaiSu });
			break;
		case "NPCFuYe.NpcLianDan":
			AccessTools.Method(typeof(NPCFuYe), "NpcLianDan", (Type[])null, (Type[])null)?.Invoke(NpcJieSuanManager.inst?.npcFuYe, new object[1] { npcId });
			break;
		case "NpcJieSuanManager.AddNpcEquip":
		{
			int equipType = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "equipType");
			bool isLianQi = ResolveBiographyPayloadBool(pendingEvent.OriginalPayload, "isLianQi");
			AccessTools.Method(typeof(NpcJieSuanManager), "AddNpcEquip", new Type[3]
			{
				typeof(int),
				typeof(int),
				typeof(bool)
			}, (Type[])null)?.Invoke(NpcJieSuanManager.inst, new object[3] { npcId, equipType, isLianQi });
			break;
		}
		case "NPCLiLian.NPCYouLi":
			AccessTools.Method(typeof(NPCLiLian), "NPCYouLi", new Type[1] { typeof(int) }, (Type[])null)?.Invoke(NpcJieSuanManager.inst?.npcLiLian, new object[1] { npcId });
			break;
		default:
			throw new InvalidOperationException("未实现的官方生平 source replay: " + sourceMethodKey);
		}
	}

	private void ReplayImportantEvent(PendingBiographyEventState pendingEvent)
	{
		int npcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "npcId");
		int eventId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "eventId");
		string eventTime = ResolveBiographyPayloadString(pendingEvent.OriginalPayload, "eventTime", pendingEvent.OriginalOfficialTime);
		NpcJieSuanManager.inst?.npcNoteBook?.NoteImprotantEvent(npcId, eventId, eventTime);
	}

	private void ReplayNpcPaiMaiEvent(PendingBiographyEventState pendingEvent)
	{
		int npcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "npcId");
		int itemId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "itemId");
		int price = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "price");
		string sellerName = ResolveBiographyPayloadString(pendingEvent.OriginalPayload, "sellerName", string.Empty);
		NpcJieSuanManager.inst?.AddItemToNpcBackpack(npcId, itemId, 1);
		NpcJieSuanManager.inst?.npcNoteBook?.NotePaiMai(npcId, itemId, sellerName);
		NpcJieSuanManager.inst?.npcSetField?.AddNpcMoney(npcId, -price);
	}

	private void ReplayNpcJieShaEvent(PendingBiographyEventState pendingEvent)
	{
		int npcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "npcId");
		int targetNpcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "targetNpcId");
		string targetName = ResolveBiographyPayloadString(pendingEvent.OriginalPayload, "targetName", string.Empty);
		int deathType = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "deathType");
		int killNpcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "killNpcId");
		int moneyDelta = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "moneyDelta");
		string resultKind = ResolveBiographyPayloadString(pendingEvent.OriginalPayload, "resultKind", string.Empty);
		switch (resultKind)
		{
		case "JieShaFail1":
			NpcJieSuanManager.inst?.npcNoteBook?.NoteJieShaFail1(npcId, targetName);
			NpcJieSuanManager.inst?.npcNoteBook?.NoteFanShaFail1(targetNpcId, ResolveBiographyPayloadString(pendingEvent.OriginalPayload, "actorName", string.Empty));
			break;
		case "JieShaSuccess":
			NpcJieSuanManager.inst?.npcNoteBook?.NoteJieShaSuccess(npcId, targetName);
			if (deathType > 0)
			{
				NpcJieSuanManager.inst?.npcDeath?.SetNpcDeath(deathType, targetNpcId, killNpcId);
			}
			if (moneyDelta != 0)
			{
				NpcJieSuanManager.inst?.npcSetField?.AddNpcMoney(npcId, moneyDelta);
			}
			break;
		case "JieShaFail2":
			NpcJieSuanManager.inst?.npcNoteBook?.NoteJieShaFail2(npcId, targetName);
			NpcJieSuanManager.inst?.npcNoteBook?.NoteFanShaFail2(targetNpcId, ResolveBiographyPayloadString(pendingEvent.OriginalPayload, "actorName", string.Empty));
			break;
		case "FanShaSuccess":
			NpcJieSuanManager.inst?.npcNoteBook?.NoteFanShaSuccess(npcId, targetName);
			if (deathType > 0)
			{
				NpcJieSuanManager.inst?.npcDeath?.SetNpcDeath(deathType, targetNpcId, killNpcId);
			}
			if (moneyDelta != 0)
			{
				NpcJieSuanManager.inst?.npcSetField?.AddNpcMoney(npcId, moneyDelta);
			}
			break;
		default:
			throw new InvalidOperationException("未实现的结杀 replay 结果: " + resultKind);
		}
	}

	private void ReplayNpcLunDaoEvent(PendingBiographyEventState pendingEvent)
	{
		int npcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "npcId");
		int otherNpcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "otherNpcId");
		string otherNpcName = ResolveBiographyPayloadString(pendingEvent.OriginalPayload, "otherNpcName", string.Empty);
		string selfName = ResolveBiographyPayloadString(pendingEvent.OriginalPayload, "selfName", string.Empty);
		string resultKind = ResolveBiographyPayloadString(pendingEvent.OriginalPayload, "resultKind", string.Empty);
		string text = resultKind;
		string text2 = text;
		if (!(text2 == "LunDaoFail"))
		{
			if (!(text2 == "LunDaoSuccess"))
			{
				throw new InvalidOperationException("未实现的论道 replay 结果: " + resultKind);
			}
			NpcJieSuanManager.inst?.npcSetField?.AddNpcWuDaoZhi(npcId, ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "selfWuDaoZhi"));
			NpcJieSuanManager.inst?.npcSetField?.AddNpcWuDaoZhi(otherNpcId, ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "otherWuDaoZhi"));
			NpcJieSuanManager.inst?.npcSetField?.AddNpcWuDaoExp(npcId, ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "wuDaoId"), ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "selfWuDaoExp"));
			NpcJieSuanManager.inst?.npcSetField?.AddNpcWuDaoExp(otherNpcId, ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "wuDaoId"), ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "otherWuDaoExp"));
			NpcJieSuanManager.inst?.npcNoteBook?.NoteLunDaoSuccess(npcId, otherNpcName);
			NpcJieSuanManager.inst?.npcNoteBook?.NoteLunDaoSuccess(otherNpcId, selfName);
		}
		else
		{
			NpcJieSuanManager.inst?.npcNoteBook?.NoteLunDaoFail(npcId, otherNpcName);
			NpcJieSuanManager.inst?.npcNoteBook?.NoteLunDaoFail(otherNpcId, selfName);
		}
	}

	private void ReplayTianJiDaBiEvent(PendingBiographyEventState pendingEvent)
	{
		int npcId = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "npcId");
		int rank = ResolveBiographyPayloadInt(pendingEvent.OriginalPayload, "rank");
		string eventTime = ResolveBiographyPayloadString(pendingEvent.OriginalPayload, "eventTime", pendingEvent.OriginalOfficialTime);
		NpcJieSuanManager.inst?.npcNoteBook?.NoteTianJiDaBi(npcId, rank, eventTime);
	}

	private static int ResolveBiographyPayloadInt(JObject payload, string key, int fallback = 0)
	{
		JToken token = payload?[key];
		return (token != null && token.Type != JTokenType.Null) ? token.Value<int>() : fallback;
	}

	private static bool ResolveBiographyPayloadBool(JObject payload, string key, bool fallback = false)
	{
		JToken token = payload?[key];
		return (token != null && token.Type != JTokenType.Null) ? token.Value<bool>() : fallback;
	}

	private static string ResolveBiographyPayloadString(JObject payload, string key, string fallback = "")
	{
		string value = payload?[key]?.ToString();
		return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
	}

	private JSONObject FindNpcBackpackItemByItemId(int npcId, int itemId)
	{
		if (npcId <= 0 || itemId <= 0 || !jsonData.instance.AvatarBackpackJsonData.HasField(npcId.ToString()))
		{
			return null;
		}
		JSONObject backpack = jsonData.instance.AvatarBackpackJsonData[npcId.ToString()]["Backpack"];
		foreach (JSONObject entry in backpack.list)
		{
			if (entry != null && entry.HasField("ItemID") && entry["ItemID"].I == itemId)
			{
				return entry;
			}
		}
		return null;
	}

	private static string BuildBiographyEventSummaryText(BiographyEventKind eventKind, string npcName)
	{
		string displayName = (string.IsNullOrWhiteSpace(npcName) ? "该NPC" : npcName.Trim());
		switch (eventKind)
		{
		case BiographyEventKind.QiYu:
			return displayName + "刚撞上一场新的奇遇，原本会顺着官方结算直接落地。";
		case BiographyEventKind.ImprotantEvent:
			return displayName + "迎来了一条官方固定生平节点。";
		case BiographyEventKind.UseDanYao:
			return displayName + "原本会在这次结算里服下一味丹药，让一段既定变化直接生效。";
		case BiographyEventKind.SmallTuPo:
			return displayName + "原本会在这次结算里完成一次小境界突破。";
		case BiographyEventKind.LianDan:
			return displayName + "原本会在官方结算里完成一轮炼丹经历。";
		case BiographyEventKind.LianQi:
			return displayName + "原本会在官方结算里完成一轮炼器经历。";
		case BiographyEventKind.ZhuJiSuccess:
			return displayName + "正站在筑基关口前，这次官方结算原本会直接决定成败。";
		case BiographyEventKind.JinDanSuccess:
			return displayName + "正站在结丹关口前，这次官方结算原本会直接决定成败。";
		case BiographyEventKind.YuanYingSuccess:
			return displayName + "正站在凝婴关口前，这次官方结算原本会直接决定成败。";
		case BiographyEventKind.HuaShenSuccess:
			return displayName + "正站在化神关口前，这次官方结算原本会直接决定成败。";
		case BiographyEventKind.PaiMai:
			return displayName + "原本会在拍卖相关结算里获得一件新物品。";
		case BiographyEventKind.LunDaoSuccess:
		case BiographyEventKind.LunDaoFail:
			return displayName + "原本会在一次论道经历后写入新的生平。";
		case BiographyEventKind.JieShaSuccess:
		case BiographyEventKind.JieShaFail1:
		case BiographyEventKind.JieShaFail2:
		case BiographyEventKind.FanShaSuccess:
		case BiographyEventKind.FanShaFail1:
		case BiographyEventKind.FanShaFail2:
			return displayName + "原本会在一次结杀/反杀遭遇后写入新的生平。";
		case BiographyEventKind.TianJiDaBi:
			return displayName + "原本会在天机大比结算后写入新的排名经历。";
		default:
			return displayName + "原本会写入一条新的官方生平事件。";
		}
	}

	private static string BuildBiographyEventPlayerHint(BiographyEventKind eventKind, string npcName)
	{
		string displayName = (string.IsNullOrWhiteSpace(npcName) ? "对方" : npcName.Trim());
		switch (eventKind)
		{
		case BiographyEventKind.QiYu:
			return "关注" + displayName + "这场刚被拦下的奇遇，它会被改写成你的下一段主线。";
		case BiographyEventKind.ImprotantEvent:
			return displayName + "的一条官方命运节点已经被拦下，接下来会改写成剧情任务。";
		case BiographyEventKind.ZhuJiSuccess:
		case BiographyEventKind.JinDanSuccess:
		case BiographyEventKind.YuanYingSuccess:
		case BiographyEventKind.HuaShenSuccess:
			return displayName + "的突破关口已经被拦下，接下来会被改写成一段更完整的剧情链。";
		case BiographyEventKind.LunDaoSuccess:
		case BiographyEventKind.LunDaoFail:
			return displayName + "刚被拦下的一场论道会被改写成你的下一段主线。";
		case BiographyEventKind.JieShaSuccess:
		case BiographyEventKind.JieShaFail1:
		case BiographyEventKind.JieShaFail2:
		case BiographyEventKind.FanShaSuccess:
		case BiographyEventKind.FanShaFail1:
		case BiographyEventKind.FanShaFail2:
			return displayName + "刚被拦下的一场杀伐遭遇会被改写成新的剧情任务。";
		default:
			return displayName + "的新生平事件已被拦下，接下来会改写成主线剧情。";
		}
	}

	private string BuildBiographyStoryHistorySummary(NPCInfo npcInfo, MainlineCampaign campaign, MainlineChapter chapter, PendingBiographyEventState pendingEvent)
	{
		List<string> sections = new List<string>();
		string taskMemorySummary = BuildQuestTaskMemorySummaryForPrompt(npcInfo?.NpcID ?? pendingEvent?.NpcId ?? 0, npcInfo?.Name ?? pendingEvent?.NpcName ?? string.Empty);
		if (!string.IsNullOrWhiteSpace(taskMemorySummary))
		{
			sections.Add(taskMemorySummary.Trim());
		}
		if (campaign?.Continuity?.RecentWorldChanges != null && campaign.Continuity.RecentWorldChanges.Count > 0)
		{
			sections.Add(string.Join("\n", campaign.Continuity.RecentWorldChanges.Select((string change) => "- " + change)));
		}
		if (chapter?.CurrentBeat != null && chapter.CurrentBeat.Status == MainlineBeatStatus.Completed)
		{
			string beatSummary = "- [" + chapter.CurrentBeat.Title + "] " + chapter.CurrentBeat.Summary;
			if (!string.IsNullOrWhiteSpace(beatSummary))
			{
				sections.Add(beatSummary);
			}
		}
		return string.Join("\n\n", sections.Where((string section) => !string.IsNullOrWhiteSpace(section)));
	}

	private IEnumerator PlayQuestProgramStoryLineCoroutine(QuestProgram program, AIQuestStoryLine line)
	{
		if (!HasStoryLineText(line))
		{
			yield break;
		}
		bool finished = false;
		SayDialog sayDialog = SayDialog.GetSayDialog();
		AIQuestStoryPresentation presentation = ResolveQuestProgramStoryPresentation(program, line);
		AppendQuestProgramStoryLineHistory(program, line, sayDialog == null);
		if (sayDialog == null)
		{
			yield break;
		}
		sayDialog.SetActive(state: true);
		sayDialog.SetCharacter(null, presentation.CharacterId);
		if (presentation.ShowCharacterImage)
		{
			sayDialog.SetCharacterImage(null, presentation.CharacterId);
		}
		else
		{
			sayDialog.SetCharacterImage(null);
		}
		sayDialog.FadeWhenDone = false;
		sayDialog.Say(line.Text, clearPrevious: true, waitForInput: true, fadeWhenDone: false, stopVoiceover: true, waitForVO: false, null, delegate
		{
			finished = true;
		});
		while (!finished)
		{
			yield return null;
		}
		try
		{
			sayDialog.Clear();
			sayDialog.SetActive(state: false);
		}
		catch
		{
		}
	}

	private static AIQuestStoryPresentation ResolveQuestProgramStoryPresentation(QuestProgram program, AIQuestStoryLine line)
	{
		AIQuestStoryLine resolvedLine = line ?? AIQuestStoryLine.CreateNarrator("……");
		if (resolvedLine.SpeakerType == QuestStorySpeakerType.Player)
		{
			return new AIQuestStoryPresentation
			{
				SpeakerType = QuestStorySpeakerType.Player,
				SpeakerName = "你",
				CharacterId = 1,
				ShowCharacterImage = true,
				IsPlayerBubble = true,
				IsNarrator = false
			};
		}
		if (resolvedLine.SpeakerType == QuestStorySpeakerType.Narrator)
		{
			return new AIQuestStoryPresentation
			{
				SpeakerType = QuestStorySpeakerType.Narrator,
				SpeakerName = "旁白",
				CharacterId = 0,
				ShowCharacterImage = false,
				IsPlayerBubble = false,
				IsNarrator = true
			};
		}
		int npcId = resolvedLine.SpeakerNpcId;
		if (npcId <= 0 && program?.WorldContract?.AllowedNpcIds != null && program.WorldContract.AllowedNpcIds.Count > 0)
		{
			npcId = program.WorldContract.AllowedNpcIds[0];
		}
		string speakerName = ((npcId > 0) ? QuestElementPicker.GetNpcDisplayName(npcId) : "某位道友");
		return new AIQuestStoryPresentation
		{
			SpeakerType = QuestStorySpeakerType.Npc,
			SpeakerName = (string.IsNullOrWhiteSpace(speakerName) ? "某位道友" : speakerName),
			CharacterId = ((npcId > 0) ? npcId : 0),
			ShowCharacterImage = (npcId > 0),
			IsPlayerBubble = false,
			IsNarrator = false
		};
	}

	private void AppendQuestProgramStoryLineHistory(QuestProgram program, AIQuestStoryLine line, bool showInUi)
	{
		if (HasStoryLineText(line))
		{
			AIQuestStoryPresentation presentation = ResolveQuestProgramStoryPresentation(program, line);
			if (showInUi)
			{
				UIManager.Instance?.AppendMessage(presentation.SpeakerName, line.Text, presentation.IsPlayerBubble, NPCDialog.GetCurrentGameTimeTextOrDefault(), null, presentation.SpeakerType);
			}
		}
	}

	private static bool HasStoryLineText(AIQuestStoryLine line)
	{
		return line != null && !string.IsNullOrWhiteSpace(line.Text);
	}

	private IEnumerator ShowQuestProgramChoicesCoroutine(List<QuestChoiceOption> choices, Action<QuestChoiceOption> onSelected)
	{
		if (choices == null || choices.Count == 0)
		{
			yield break;
		}
		MenuDialog menuDialog = MenuDialog.GetMenuDialog();
		if (menuDialog == null)
		{
			onSelected?.Invoke(choices[0]);
			yield break;
		}
		QuestChoiceOption selectedChoice = null;
		menuDialog.Clear();
		menuDialog.SetActive(state: true);
		foreach (QuestChoiceOption choice in choices)
		{
			menuDialog.AddOption(choice.Text, interactable: true, hideOption: false, null);
			int buttonIndex = menuDialog.NowOption;
			menuDialog.CachedButtons[buttonIndex].onClick.RemoveAllListeners();
			QuestChoiceOption capturedChoice = choice;
			menuDialog.CachedButtons[buttonIndex].onClick.AddListener(delegate
			{
				selectedChoice = capturedChoice;
				menuDialog.Clear();
				menuDialog.SetActive(state: false);
			});
		}
		while (selectedChoice == null)
		{
			yield return null;
		}
		onSelected?.Invoke(selectedChoice);
		try
		{
			menuDialog.Clear();
			menuDialog.SetActive(state: false);
		}
		catch
		{
		}
	}

	private void ClearActiveQuestProgramNarrativeState()
	{
		_questProgramNarrativeCoroutine = null;
		_activeQuestProgramNarrativeProgramId = string.Empty;
		_activeQuestProgramNarrativeNodeId = string.Empty;
	}

	private void MarkDirectorProgramFailed(QuestProgram program, string reason)
	{
		if (program == null || program.TrackType != QuestTrackType.Mainline)
		{
			return;
		}
		MainlineChapter chapter = ResolveQuestChapter(_mainlineCampaign, program.ChapterId);
		MainlineBeat beat = ResolveQuestBeat(_mainlineCampaign, program.ChapterId, program.BeatId);
		if (beat != null)
		{
			beat.Status = MainlineBeatStatus.Failed;
		}
		if (chapter != null)
		{
			chapter.Status = MainlineChapterStatus.Failed;
		}
		if (_mainlineCampaign != null)
		{
			_mainlineCampaign.Status = CampaignStatus.Failed;
			if (_mainlineCampaign.Continuity == null)
			{
				_mainlineCampaign.Continuity = new ContinuityMemorySnapshot();
			}
			if (_mainlineCampaign.Continuity.RecentWorldChanges == null)
			{
				_mainlineCampaign.Continuity.RecentWorldChanges = new List<string>();
			}
			if (!string.IsNullOrWhiteSpace(reason))
			{
				_mainlineCampaign.Continuity.RecentWorldChanges.Add(reason);
			}
			_mainlineCampaign.Continuity.LastUpdatedAt = DateTime.Now;
		}
	}

	internal bool EvaluateCampaignProgressForTest(QuestProgram program)
	{
		return EvaluateCampaignProgress(program);
	}

	internal bool CanQuestProgramClaimWorldControlForTest(QuestProgram candidate, QuestProgram currentOwner)
	{
		return CanQuestProgramClaimWorldControl(candidate, currentOwner);
	}

	private static void LogQuestWarning(string context, Exception ex)
	{
		ManualLogSource logger = AIChatManager.logger;
		if (logger != null)
		{
			logger.LogWarning((object)$"[AIQuestManager] {context}: {ex}");
		}
	}

	private void RecordVisitedLocation(string locationId)
	{
		if (!string.IsNullOrEmpty(locationId))
		{
			_visitedSceneIds.Add(locationId);
		}
	}

	private HashSet<string> RecordCurrentVisitedLocations()
	{
		HashSet<string> currentLocationIds = GetCurrentLocationIds();
		foreach (string locationId in currentLocationIds)
		{
			RecordVisitedLocation(locationId);
		}
		return currentLocationIds;
	}

	private bool VisitedLocationMatchesSceneName(string visitedId, string targetSceneName)
	{
		if (!QuestElementPicker.TryResolveOfficialLocationId(targetSceneName, out var targetLocationId))
		{
			return false;
		}
		return LocationIdMatchesTargetScene(visitedId, targetLocationId, string.Empty);
	}

	private void Update()
	{
		TryLoadPersistenceIfReady();
		CleanupRecentBattleVictories();
		HashSet<string> currentLocationIds = RecordCurrentVisitedLocations();
		string locationSignature = BuildLocationSignature(currentLocationIds);
		if (!string.Equals(locationSignature, _lastNarrativeLocationSignature, StringComparison.Ordinal))
		{
			_lastNarrativeLocationSignature = locationSignature;
			PublishQuestProgramRuntimeEvent("scene.changed", locationSignature, new Dictionary<string, string> { ["locationSignature"] = locationSignature });
		}
		if (HasActiveQuestPrograms())
		{
			PumpQuestProgramObservedStateChanges(persistChanges: true);
		}
		if (TryProgressPendingBiographyStory())
		{
			MarkQuestPersistenceDirty();
		}
		if (TryHandlePendingBiographyEventTimeout())
		{
			MarkQuestPersistenceDirty();
		}
		_checkTimer += Time.deltaTime;
		if (_checkTimer >= _checkInterval && HasMainlineRuntimeWork())
		{
			_checkTimer = 0f;
			if (TryGetGameTime(out var _))
			{
				PublishQuestProgramRuntimeEvent("time.tick");
			}
		}
	}

	public void OnPlayerTimeAdvanced(int addDays, int addMonths, int addYears)
	{
		if (Instance != this || !HasMainlineRuntimeWork() || !TryGetGameTime(out var now))
		{
			return;
		}
		AIChatManager.logger.LogInfo((object)$"[AIQuestManager] 收到官方时间推进: +{addYears}年 +{addMonths}月 +{addDays}日, now={now:yyyy-MM-dd HH:mm}, programs={(_questProgramState?.Programs?.Count).GetValueOrDefault()}");
		if (HasActiveQuestPrograms())
		{
			int? currentFuBenRemainingDays = GetQuestProgramRuntimeFuBenRemainingDays();
			if (_lastQuestProgramObservedFuBenRemainingDays.HasValue && currentFuBenRemainingDays.HasValue && _lastQuestProgramObservedFuBenRemainingDays.Value != currentFuBenRemainingDays.Value)
			{
				NotifyFuBenRemainingDaysChanged(_lastQuestProgramObservedFuBenRemainingDays.Value, currentFuBenRemainingDays.Value);
			}
			_lastQuestProgramObservedFuBenRemainingDays = currentFuBenRemainingDays;
		}
	}

	private bool TryGetGameTime(out DateTime gameTime)
	{
		try
		{
			Avatar player = Tools.instance?.getPlayer();
			if (player != null)
			{
				gameTime = player.worldTimeMag.getNowTime();
				return true;
			}
		}
		catch (Exception ex)
		{
			LogQuestWarning("TryGetGameTime", ex);
		}
		gameTime = DateTime.MinValue;
		return false;
	}

	private static DateTime AddGameMonths(DateTime startTime, int months)
	{
		if (months <= 0)
		{
			return startTime;
		}
		return Tools.GetEndTime(startTime.ToString("yyyy-MM-dd HH:mm:ss"), 0, months);
	}

	private HashSet<string> GetCurrentLocationIds()
	{
		HashSet<string> locationIds = new HashSet<string>(StringComparer.Ordinal);
		try
		{
			if (QuestElementPicker.TryNormalizeOfficialLocationId(SceneEx.NowSceneName, out var currentSceneId))
			{
				locationIds.Add(currentSceneId);
			}
		}
		catch (Exception ex)
		{
			LogQuestWarning("GetCurrentLocationIds scene", ex);
		}
		try
		{
			Avatar player = PlayerEx.Player;
			if (player != null)
			{
				string screenName = string.Empty;
				try
				{
					screenName = Tools.getScreenName();
				}
				catch (Exception ex2)
				{
					LogQuestWarning("GetCurrentLocationIds screenName", ex2);
				}
				if (screenName == "AllMaps" && player.NowMapIndex > 0)
				{
					locationIds.Add(player.NowMapIndex.ToString());
				}
				if (QuestElementPicker.TryNormalizeOfficialLocationId(player.NowFuBen, out var fuBenSceneId))
				{
					locationIds.Add(fuBenSceneId);
				}
				if (QuestElementPicker.TryNormalizeOfficialLocationId(player.lastFuBenScence, out var lastFuBenSceneId))
				{
					locationIds.Add(lastFuBenSceneId);
				}
				if (!string.IsNullOrEmpty(screenName) && screenName.StartsWith("F", StringComparison.Ordinal))
				{
					if (QuestElementPicker.TryNormalizeOfficialLocationId(screenName, out var normalizedFuBenScreenId))
					{
						locationIds.Add(normalizedFuBenScreenId);
					}
					try
					{
						int nowIndex = player.fubenContorl[screenName].NowIndex;
						if (nowIndex > 0)
						{
							string fuBenNodeId = $"{screenName},{nowIndex}";
							if (QuestElementPicker.TryNormalizeOfficialLocationId(fuBenNodeId, out var normalizedFuBenNodeId))
							{
								locationIds.Add(normalizedFuBenNodeId);
							}
						}
					}
					catch (Exception ex3)
					{
						LogQuestWarning("GetCurrentLocationIds fubenNode", ex3);
					}
				}
			}
		}
		catch (Exception ex4)
		{
			LogQuestWarning("GetCurrentLocationIds player/fuben", ex4);
		}
		return locationIds;
	}

	private bool LocationIdMatchesTargetScene(string locationId, string targetSceneId, string sceneName)
	{
		if (string.IsNullOrEmpty(locationId))
		{
			return false;
		}
		if (!string.IsNullOrEmpty(targetSceneId) && locationId == targetSceneId)
		{
			return true;
		}
		if (QuestElementPicker.TryParseFuBenLocationId(locationId, out var currentFuBenSceneId, out var _) && targetSceneId == currentFuBenSceneId)
		{
			return true;
		}
		if (QuestElementPicker.TryParseFuBenLocationId(targetSceneId, out var targetFuBenSceneId, out var targetFuBenNodeIndex) && targetFuBenNodeIndex <= 0 && locationId == targetFuBenSceneId)
		{
			return true;
		}
		return !string.IsNullOrEmpty(sceneName) && VisitedLocationMatchesSceneName(locationId, sceneName);
	}

	private void CleanupRecentBattleVictories()
	{
		if (_recentBattleVictories.Count == 0)
		{
			return;
		}
		float now = Time.realtimeSinceStartup;
		List<int> expiredNpcIds = new List<int>();
		foreach (KeyValuePair<int, float> kvp in _recentBattleVictories)
		{
			if (now - kvp.Value > 15f)
			{
				expiredNpcIds.Add(kvp.Key);
			}
		}
		foreach (int npcId in expiredNpcIds)
		{
			_recentBattleVictories.Remove(npcId);
		}
	}

	private List<int> ResolveEquivalentItemIds(int aiProvidedId, string itemName, int itemQuality)
	{
		List<int> resolvedIds = new List<int>();
		string targetName = QuestElementPicker.NormalizeNarrativeEntityText(itemName);
		int targetQuality = itemQuality;
		if (aiProvidedId > 0 && _ItemJsonData.DataDict.ContainsKey(aiProvidedId))
		{
			_ItemJsonData aiItem = _ItemJsonData.DataDict[aiProvidedId];
			if (string.IsNullOrEmpty(targetName))
			{
				targetName = QuestElementPicker.NormalizeNarrativeEntityText(aiItem.name);
			}
			if (targetQuality <= 0)
			{
				targetQuality = aiItem.quality;
			}
		}
		if (!string.IsNullOrEmpty(targetName))
		{
			resolvedIds = FindEquivalentItemIds(targetName, targetQuality, allowFuzzy: false);
			if (resolvedIds.Count == 0)
			{
				resolvedIds = FindEquivalentItemIds(targetName, targetQuality, allowFuzzy: true);
			}
			if (resolvedIds.Count == 0 && targetQuality > 0)
			{
				resolvedIds = FindEquivalentItemIds(targetName, 0, allowFuzzy: false);
				if (resolvedIds.Count == 0)
				{
					resolvedIds = FindEquivalentItemIds(targetName, 0, allowFuzzy: true);
				}
			}
		}
		if (resolvedIds.Count == 0 && aiProvidedId > 0 && _ItemJsonData.DataDict.ContainsKey(aiProvidedId))
		{
			resolvedIds.Add(aiProvidedId);
		}
		resolvedIds = resolvedIds.Where((int id) => _ItemJsonData.DataDict.ContainsKey(id)).Distinct().ToList();
		if (resolvedIds.Count == 0)
		{
			return resolvedIds;
		}
		if (aiProvidedId > 0 && resolvedIds.Contains(aiProvidedId))
		{
			resolvedIds.Remove(aiProvidedId);
			resolvedIds.Insert(0, aiProvidedId);
		}
		else
		{
			resolvedIds.Sort();
		}
		return resolvedIds;
	}

	private List<int> FindEquivalentItemIds(string itemName, int itemQuality, bool allowFuzzy)
	{
		List<int> ids = new List<int>();
		string normalizedTargetName = QuestElementPicker.NormalizeNarrativeEntityText(itemName);
		if (string.IsNullOrWhiteSpace(normalizedTargetName))
		{
			return ids;
		}
		foreach (KeyValuePair<int, _ItemJsonData> kvp in _ItemJsonData.DataDict)
		{
			_ItemJsonData item = kvp.Value;
			if (item == null || string.IsNullOrEmpty(item.name) || (itemQuality > 0 && item.quality != itemQuality))
			{
				continue;
			}
			string normalizedItemName = QuestElementPicker.NormalizeNarrativeEntityText(item.name);
			if (string.IsNullOrWhiteSpace(normalizedItemName))
			{
				continue;
			}
			bool num;
			if (!allowFuzzy)
			{
				num = QuestElementPicker.NarrativeEntityTextEquals(normalizedItemName, normalizedTargetName);
			}
			else
			{
				if (QuestElementPicker.NarrativeEntityTextContains(normalizedItemName, normalizedTargetName))
				{
					goto IL_00c4;
				}
				num = QuestElementPicker.NarrativeEntityTextContains(normalizedTargetName, normalizedItemName);
			}
			if (!num)
			{
				continue;
			}
			goto IL_00c4;
			IL_00c4:
			ids.Add(kvp.Key);
		}
		return ids;
	}

	private int GetPlayerEquivalentItemCount(List<int> itemIds)
	{
		Avatar player = PlayerEx.Player;
		if (player == null || itemIds == null || itemIds.Count == 0)
		{
			return 0;
		}
		int total = 0;
		foreach (int itemId in itemIds)
		{
			if (itemId > 0)
			{
				total += Math.Max(0, player.getItemNum(itemId));
			}
		}
		return total;
	}

	private static int GetPlayerExactItemCount(int itemId)
	{
		Avatar player = PlayerEx.Player;
		if (player == null || itemId <= 0)
		{
			return 0;
		}
		int total = 0;
		foreach (ITEM_INFO itemInfo in player.itemList.values)
		{
			if (itemInfo.itemId == itemId)
			{
				total += (int)itemInfo.itemCount;
			}
		}
		return total;
	}

	private static int ComputeStableTextHash(string text)
	{
		int hash = 23;
		string raw = text ?? string.Empty;
		for (int i = 0; i < raw.Length; i++)
		{
			hash = hash * 31 + raw[i];
		}
		if (hash == int.MinValue)
		{
			hash = int.MaxValue;
		}
		return Math.Abs(hash);
	}

	private QuestMailEnvelope BuildNotificationMailEnvelope(int npcId, string npcName, string message, string historyMessage, string questId = "", string stepId = "")
	{
		string resolvedNpcName = ResolvePlayerFacingQuestNpcName(npcId, npcName, "对方");
		int extraIndex = ComputeStableTextHash(message);
		return new QuestMailEnvelope
		{
			MailId = QuestCyMailHelper.BuildQuestMailId(questId ?? string.Empty, stepId ?? string.Empty, "Notification", 0, extraIndex),
			NpcId = npcId,
			NpcName = resolvedNpcName,
			QuestId = (questId ?? string.Empty),
			StepId = (stepId ?? string.Empty),
			MailType = "Notification",
			ItemId = 0,
			ItemCount = 0,
			FavorAmount = 0,
			Message = (message ?? string.Empty),
			HistoryMessage = (historyMessage ?? string.Empty),
			SendTime = QuestCyMailHelper.GetCurrentGameTimeString(),
			MemoryRecorded = false
		};
	}

	private void AppendQuestTaskMemory(int npcId, string npcName, string summary, string questId = "", string stepId = "")
	{
		if (npcId <= 0 || string.IsNullOrWhiteSpace(summary))
		{
			return;
		}
		string trimmedSummary = summary.Trim();
		if (!_questTaskMemories.TryGetValue(npcId, out var entries) || entries == null)
		{
			entries = new List<QuestTaskMemoryEntry>();
			_questTaskMemories[npcId] = entries;
		}
		QuestTaskMemoryEntry lastEntry = ((entries.Count > 0) ? entries[entries.Count - 1] : null);
		if (lastEntry == null || !string.Equals(lastEntry.Summary, trimmedSummary, StringComparison.Ordinal) || !string.Equals(lastEntry.QuestId ?? string.Empty, questId ?? string.Empty, StringComparison.Ordinal) || !string.Equals(lastEntry.StepId ?? string.Empty, stepId ?? string.Empty, StringComparison.Ordinal))
		{
			entries.Add(new QuestTaskMemoryEntry
			{
				NpcId = npcId,
				NpcName = ResolvePlayerFacingQuestNpcName(npcId, npcName, "对方"),
				QuestId = (questId ?? string.Empty),
				StepId = (stepId ?? string.Empty),
				Summary = trimmedSummary,
				GameTimeText = QuestCyMailHelper.GetCurrentGameTimeString()
			});
			if (entries.Count > 12)
			{
				entries.RemoveRange(0, entries.Count - 12);
			}
		}
	}

	private void TryRecordQuestMailMemory(QuestMailEnvelope envelope)
	{
		if (envelope != null && !envelope.MemoryRecorded && !string.IsNullOrWhiteSpace(envelope.HistoryMessage))
		{
			AppendQuestTaskMemory(envelope.NpcId, envelope.NpcName, envelope.HistoryMessage, envelope.QuestId, envelope.StepId);
			envelope.MemoryRecorded = true;
		}
	}

	private static string TruncateQuestTaskMemoryText(string text, int maxChars)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		string trimmed = text.Trim();
		if (trimmed.Length <= maxChars)
		{
			return trimmed;
		}
		return trimmed.Substring(0, maxChars) + "…";
	}

	private IEnumerable<string> BuildQuestStateSummaryLines(int npcId)
	{
		foreach (QuestProgram program in EnsureQuestProgramState().Programs.Where((QuestProgram questProgram) => questProgram != null && questProgram.AnchorNpcId == npcId && (questProgram.Status == QuestProgramStatus.Active || questProgram.Status == QuestProgramStatus.Draft)))
		{
			QuestNode currentNode = ResolveCurrentQuestProgramNode(program);
			string stepTitle = ((currentNode != null) ? currentNode.Title : "未知节点");
			string statePrefix = ((program.Status == QuestProgramStatus.Draft) ? "待确认任务程序" : "当前任务程序");
			yield return "- " + statePrefix + "《" + program.Title + "》仍在进行，眼下推进到“" + stepTitle + "”。";
		}
		foreach (QuestProgram program2 in EnsureQuestProgramState().Programs.Where((QuestProgram questProgram) => questProgram != null && questProgram.AnchorNpcId == npcId && (questProgram.Status == QuestProgramStatus.Completed || questProgram.Status == QuestProgramStatus.Failed || questProgram.Status == QuestProgramStatus.Suspended)).Reverse().Take(2))
		{
			string statusText = ((program2.Status == QuestProgramStatus.Completed) ? "已经圆满结束" : ((program2.Status == QuestProgramStatus.Failed) ? "已经失败收束" : "已经暂时搁置"));
			yield return "- 过往任务程序《" + program2.Title + "》" + statusText + "。";
		}
	}

	private string BuildQuestTaskMemorySummary(int npcId, string npcName)
	{
		if (npcId <= 0)
		{
			return string.Empty;
		}
		List<string> lines = new List<string>();
		if (_questTaskMemories.TryGetValue(npcId, out var entries) && entries != null)
		{
			foreach (QuestTaskMemoryEntry entry in entries.Skip(Math.Max(0, entries.Count - 6)))
			{
				string timePrefix = (string.IsNullOrWhiteSpace(entry.GameTimeText) ? string.Empty : ("[" + entry.GameTimeText + "] "));
				string summary = TruncateQuestTaskMemoryText(entry.Summary, 80);
				if (!string.IsNullOrWhiteSpace(summary))
				{
					lines.Add("- " + timePrefix + summary);
				}
			}
		}
		foreach (string stateLine in BuildQuestStateSummaryLines(npcId))
		{
			if (!lines.Contains(stateLine))
			{
				lines.Add(stateLine);
			}
		}
		return string.Join("\n", lines);
	}

	internal string BuildQuestTaskMemorySummaryForPrompt(int npcId, string npcName)
	{
		return BuildQuestTaskMemorySummary(npcId, npcName);
	}

	internal void AppendQuestTaskMemoryForTest(int npcId, string npcName, string summary)
	{
		AppendQuestTaskMemory(npcId, npcName, summary);
	}

	private bool TrySendQuestMailEnvelope(QuestMailEnvelope envelope)
	{
		if (envelope == null)
		{
			return false;
		}
		int normalizedNpcId = QuestCyMailHelper.NormalizeNpcId(envelope.NpcId);
		if (!QuestCyMailHelper.HasNpcCyFriend(normalizedNpcId))
		{
			return false;
		}
		Avatar player = PlayerEx.Player;
		if (player == null || player.emailDateMag == null)
		{
			return false;
		}
		if (string.IsNullOrEmpty(envelope.SendTime))
		{
			envelope.SendTime = QuestCyMailHelper.GetCurrentGameTimeString();
		}
		JSONObject mailData = QuestCyMailHelper.CreateQuestMailData(envelope.MailId, normalizedNpcId, envelope.NpcName, envelope.Message, envelope.SendTime, envelope.MailType, envelope.QuestId, envelope.StepId, envelope.ItemId, envelope.ItemCount, envelope.FavorAmount);
		player.NewChuanYingList.SetField(envelope.MailId.ToString(), mailData);
		player.emailDateMag.AddNewEmail(normalizedNpcId.ToString(), new EmailData(normalizedNpcId, isOld: true, envelope.MailId, envelope.SendTime));
		TryRecordQuestMailMemory(envelope);
		return true;
	}

	private void EnqueuePendingQuestMail(QuestMailEnvelope envelope)
	{
		if (envelope != null)
		{
			TryRecordQuestMailMemory(envelope);
			_pendingQuestMails.RemoveAll((QuestMailEnvelope m) => m != null && m.MailId == envelope.MailId);
			_pendingQuestMails.Add(envelope);
			MarkQuestPersistenceDirty();
		}
	}

	private void TryDispatchPendingQuestMails()
	{
		if (!_hasLoadedPersistence || _pendingQuestMails.Count == 0)
		{
			return;
		}
		bool changed = false;
		for (int i = _pendingQuestMails.Count - 1; i >= 0; i--)
		{
			QuestMailEnvelope envelope = _pendingQuestMails[i];
			if (TrySendQuestMailEnvelope(envelope))
			{
				_pendingQuestMails.RemoveAt(i);
				changed = true;
			}
		}
		if (changed)
		{
			MarkQuestPersistenceDirty();
		}
	}

	internal void NotifyQuestDeliveryMailSubmitted(string questId, string stepId, int mailId)
	{
		NotifyQuestDeliveryMailSubmitted(questId, stepId, mailId, 0, 0);
	}

	private void RecordQuestProgramDeliveryMailSubmission(int npcId, int itemId, int mailId, string questId, string stepId)
	{
		if (npcId > 0 && itemId > 0 && mailId != 0)
		{
			CleanupQuestProgramRecentDeliveryMailSubmissions();
			int normalizedNpcId = QuestCyMailHelper.NormalizeNpcId(npcId);
			_recentQuestProgramDeliveryMailSubmissions.RemoveAll((QuestProgramRecentDeliveryMailSubmission submission) => submission != null && submission.MailId == mailId && QuestCyMailHelper.NormalizeNpcId(submission.NpcId) == normalizedNpcId);
			_recentQuestProgramDeliveryMailSubmissions.Add(new QuestProgramRecentDeliveryMailSubmission
			{
				NpcId = normalizedNpcId,
				ItemId = itemId,
				MailId = mailId,
				QuestId = (questId ?? string.Empty),
				StepId = (stepId ?? string.Empty),
				SubmittedAt = Time.realtimeSinceStartup
			});
		}
	}

	private void CleanupQuestProgramRecentDeliveryMailSubmissions()
	{
		if (_recentQuestProgramDeliveryMailSubmissions.Count != 0)
		{
			float now = Time.realtimeSinceStartup;
			_recentQuestProgramDeliveryMailSubmissions.RemoveAll((QuestProgramRecentDeliveryMailSubmission submission) => submission == null || now - submission.SubmittedAt > 30f);
		}
	}

	internal void NotifyQuestDeliveryMailSubmitted(string questId, string stepId, int mailId, int npcId, int itemId)
	{
		if (npcId > 0 && itemId > 0 && mailId != 0)
		{
			RecordQuestProgramDeliveryMailSubmission(npcId, itemId, mailId, questId, stepId);
			QuestRuntimeEvent runtimeEvent = BuildQuestProgramDeliveryMailSubmittedEvent(npcId, itemId, mailId, questId, stepId);
			PublishQuestProgramRuntimeEvent(runtimeEvent.EventType, runtimeEvent.PrimaryId, runtimeEvent.Payload);
		}
	}

	private static string BuildLocationSignature(IEnumerable<string> locationIds)
	{
		if (locationIds == null)
		{
			return string.Empty;
		}
		return string.Join("|", locationIds.Where((string id) => !string.IsNullOrWhiteSpace(id)).Distinct().OrderBy((string id) => id, StringComparer.Ordinal));
	}

	private void CleanupStoryDialogs()
	{
		try
		{
			SayDialog sayDialog = SayDialog.GetSayDialog();
			if (sayDialog != null)
			{
				sayDialog.StopAllCoroutines();
				sayDialog.Clear();
				sayDialog.SetActive(state: false);
			}
		}
		catch (Exception ex)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogWarning((object)("[AIQuestManager] Cleanup SayDialog failed: " + ex.Message));
			}
		}
		try
		{
			MenuDialog menuDialog = MenuDialog.GetMenuDialog();
			if (menuDialog != null)
			{
				menuDialog.Clear();
				menuDialog.SetActive(state: false);
			}
		}
		catch (Exception ex2)
		{
			ManualLogSource logger2 = AIChatManager.logger;
			if (logger2 != null)
			{
				logger2.LogWarning((object)("[AIQuestManager] Cleanup MenuDialog failed: " + ex2.Message));
			}
		}
	}

	private void ReportQuestGenerationProgress(Action<string, int, int, float> onProgress, string message, int currentStep, int totalSteps, float stageProgress = -1f)
	{
		AIChatManager.logger.LogInfo((object)$"[AIQuestManager] 任务生成进度 {currentStep}/{totalSteps}: {message}");
		onProgress?.Invoke(message, currentStep, totalSteps, stageProgress);
	}

	private IEnumerator GenerateOrResumeMainlineCoroutine(NPCInfo npcInfo, Action<QuestProgram> onComplete, Action<string> onError, Action<string, int, int, float> onProgress = null)
	{
		_isGenerating = true;
		AIChatManager.logger.LogInfo((object)("[AIQuestManager] 开始生成主线章节: NPC=" + npcInfo.Name));
		BaseAIClient client = AIModelFactory.CreateFromConfig();
		if (client == null && !HasQueuedQuestProgramAiResponsesForTest())
		{
			_isGenerating = false;
			onError?.Invoke("AI 客户端创建失败，请检查 API 配置。");
			yield break;
		}
		QuestElementPicker.PickedElements pickedElements = QuestElementPicker.Pick(npcInfo, EnsureQuestProgramState());
		MainlineCampaign campaign = EnsureMainlineCampaignShell(npcInfo);
		TokenUsage usage = new TokenUsage();
		MainlineChapter chapter = EnsureCurrentMainlineChapter(campaign, npcInfo);
		PendingBiographyEventState pendingEvent = chapter?.PendingBiographyEvent;
		if (pendingEvent == null || pendingEvent.NpcId != npcInfo.NpcID)
		{
			_isGenerating = false;
			string missingEventError = "当前没有可改写的官方生平事件：" + npcInfo.Name;
			AIChatManager.logger.LogError((object)("[AIQuestManager] " + missingEventError));
			onError?.Invoke(missingEventError);
			yield break;
		}
		MainlineBeat beat = GetCurrentMainlineBeatForGeneration(chapter);
		if (beat == null || !HasRawNarrativeDraft(beat))
		{
			ReportQuestGenerationProgress(onProgress, "正在编写当前主线剧情正文", 1, 6, 0.1f);
			string storyHistorySummary = BuildBiographyStoryHistorySummary(npcInfo, campaign, chapter, pendingEvent);
			Task<MainlineStageExecutionResult<MainlineRawNarrativeDraft>> rawNarrativeTask = RunRepairableMainlineStageAsync(client, AIQuestPromptBuilder.BuildMainlineStoryBeatMessages(npcInfo, pickedElements, campaign, pendingEvent, storyHistorySummary), "当前主线剧情正文", 1, 6, 0.2f, "当前主线剧情正文不合法，正在请求修正", onProgress, (string rawResponseText) => TryParseMainlineRawNarrativeResponse(rawResponseText), (string previousResponse, string validationError) => AIQuestPromptBuilder.BuildMainlineStoryBeatRepairMessages(npcInfo, pickedElements, campaign, pendingEvent, storyHistorySummary, previousResponse, validationError));
			while (!rawNarrativeTask.IsCompleted)
			{
				yield return null;
			}
			if (rawNarrativeTask.IsFaulted)
			{
				_isGenerating = false;
				Exception rawNarrativeException = rawNarrativeTask.Exception?.InnerException ?? rawNarrativeTask.Exception;
				string failureResponse = (rawNarrativeException as MainlineRepairableStageException)?.ResponseText;
				AIChatManager.logger.LogError((object)FormatMainlineGenerationFailure("当前主线剧情正文", 1, 6, failureResponse, rawNarrativeException));
				onError?.Invoke((rawNarrativeException is MainlineRepairableStageException) ? ("主线剧情正文解析失败: " + rawNarrativeException.Message) : (rawNarrativeException?.Message ?? "主线剧情正文生成失败"));
				yield break;
			}
			MainlineStageExecutionResult<MainlineRawNarrativeDraft> rawNarrativeResult = rawNarrativeTask.Result;
			if (rawNarrativeResult?.Usage != null)
			{
				usage.Add(rawNarrativeResult.Usage);
			}
			if (beat == null)
			{
				beat = new MainlineBeat();
			}
			beat.RawNarrativeDraft = rawNarrativeResult?.Value ?? new MainlineRawNarrativeDraft();
		}
		if (beat == null || !HasStoryDraft(beat))
		{
			ReportQuestGenerationProgress(onProgress, "正在整理当前主线剧情结构", 2, 6, 0.3f);
			Task<MainlineStageExecutionResult<MainlineBeat>> structuredBeatTask = RunRepairableMainlineStageAsync(client, AIQuestPromptBuilder.BuildMainlineStructuredBeatMessages(npcInfo, pickedElements, pendingEvent, beat?.RawNarrativeDraft), "当前主线剧情结构化", 2, 6, 0.35f, "当前主线剧情结构输出不合法，正在请求修正", onProgress, (string rawResponseText) => TryParseMainlineStructuredBeatResponse(rawResponseText, npcInfo, pickedElements, beat?.RawNarrativeDraft), (string previousResponse, string validationError) => AIQuestPromptBuilder.BuildMainlineStructuredBeatRepairMessages(npcInfo, pickedElements, pendingEvent, beat?.RawNarrativeDraft, previousResponse, validationError));
			while (!structuredBeatTask.IsCompleted)
			{
				yield return null;
			}
			if (structuredBeatTask.IsFaulted)
			{
				_isGenerating = false;
				Exception structuredBeatException = structuredBeatTask.Exception?.InnerException ?? structuredBeatTask.Exception;
				string failureResponse2 = (structuredBeatException as MainlineRepairableStageException)?.ResponseText;
				AIChatManager.logger.LogError((object)FormatMainlineGenerationFailure("当前主线剧情结构化", 2, 6, failureResponse2, structuredBeatException));
				onError?.Invoke((structuredBeatException is MainlineRepairableStageException) ? ("主线剧情结构化失败: " + structuredBeatException.Message) : (structuredBeatException?.Message ?? "主线剧情结构化失败"));
				yield break;
			}
			MainlineStageExecutionResult<MainlineBeat> structuredBeatResult = structuredBeatTask.Result;
			if (structuredBeatResult?.Usage != null)
			{
				usage.Add(structuredBeatResult.Usage);
			}
			MainlineRawNarrativeDraft rawNarrativeDraft = beat?.RawNarrativeDraft;
			beat = structuredBeatResult?.Value;
			if (beat == null)
			{
				throw new InvalidOperationException("主线剧情结构化没有产出有效 beat");
			}
			beat.RawNarrativeDraft = rawNarrativeDraft ?? new MainlineRawNarrativeDraft();
			ApplyPendingBiographyEventTiming(pendingEvent, npcInfo, beat);
		}
		QuestElementPicker.BeatExecutionContext executionContext = QuestElementPicker.BuildBeatExecutionContext(pickedElements, beat);
		if (beat.GamePlan == null || beat.GamePlan.FlowSteps == null || beat.GamePlan.FlowSteps.Count == 0)
		{
			ReportQuestGenerationProgress(onProgress, "正在转化剧情对应的功能步骤", 3, 6, 0.45f);
			Task<MainlineStageExecutionResult<MainlineBeatGamePlan>> beatGamePlanTask = RunRepairableMainlineStageAsync(client, AIQuestPromptBuilder.BuildMainlineBeatGamePlanningMessages(npcInfo, beat, executionContext), "当前主线功能规划", 3, 6, 0.55f, "当前主线功能规划输出不合法，正在请求修正", onProgress, (string rawResponseText) => TryParseMainlineBeatGamePlanResponse(rawResponseText, beat), (string previousResponse, string validationError) => AIQuestPromptBuilder.BuildMainlineBeatGamePlanningRepairMessages(npcInfo, beat, executionContext, previousResponse, validationError));
			while (!beatGamePlanTask.IsCompleted)
			{
				yield return null;
			}
			if (beatGamePlanTask.IsFaulted)
			{
				_isGenerating = false;
				Exception beatGamePlanException = beatGamePlanTask.Exception?.InnerException ?? beatGamePlanTask.Exception;
				string failureResponse3 = (beatGamePlanException as MainlineRepairableStageException)?.ResponseText;
				AIChatManager.logger.LogError((object)FormatMainlineGenerationFailure("当前主线功能规划", 3, 6, failureResponse3, beatGamePlanException));
				onError?.Invoke((beatGamePlanException is MainlineRepairableStageException) ? ("剧情功能计划解析失败: " + beatGamePlanException.Message) : (beatGamePlanException?.Message ?? "剧情功能计划生成失败"));
				yield break;
			}
			MainlineStageExecutionResult<MainlineBeatGamePlan> beatGamePlanResult = beatGamePlanTask.Result;
			if (beatGamePlanResult?.Usage != null)
			{
				usage.Add(beatGamePlanResult.Usage);
			}
			ApplyBeatGamePlan(beat, beatGamePlanResult?.Value);
		}
		ReportQuestGenerationProgress(onProgress, "正在编译当前主线 beat", 4, 6, 0.7f);
		string responseText = null;
		try
		{
			QuestProgram program = CompileMainlineBeatToQuestProgram(beat, beat.GamePlan, executionContext);
			ReportQuestGenerationProgress(onProgress, "正在校验主线 beat", 5, 6, 0.85f);
			AttachCurrentBeatToChapter(chapter, beat);
			ApplyBeatWorldContract(program, beat);
			BindProgramToAnchorNpc(program, npcInfo);
			BindProgramToCampaignBeat(program, campaign, chapter, beat);
			program.ContinuitySummary = beat?.Summary ?? chapter?.Summary ?? campaign?.Premise ?? string.Empty;
			program.Status = QuestProgramStatus.Draft;
			UpsertQuestProgram(program);
			MarkQuestPersistenceDirty();
			ReportQuestGenerationProgress(onProgress, "主线 beat 已生成", 6, 6, 1f);
			_isGenerating = false;
			onComplete?.Invoke(program);
		}
		catch (Exception ex)
		{
			_isGenerating = false;
			AIChatManager.logger.LogError((object)FormatMainlineGenerationFailure("当前主线 beat 解析", 6, 6, responseText, ex));
			onError?.Invoke("主线 beat 解析失败: " + ex.Message);
		}
	}

	private static string FormatMainlineGenerationFailure(string stageName, int currentStep, int totalSteps, string responseText, Exception ex)
	{
		string exceptionType = ex?.GetType().Name ?? "UnknownException";
		string stepText = ((currentStep > 0 && totalSteps > 0) ? $"{currentStep}/{totalSteps}" : "n/a");
		return "[AIQuestManager] 主线生成失败: stage=" + stageName + ", step=" + stepText + ", exceptionType=" + exceptionType + ", error=" + ex?.Message + ", responsePreview=" + SafeLogPreview(responseText, 3000);
	}

	private static string BuildQuestGenerationMessagePreview(List<ChatMessage> messages)
	{
		if (messages == null || messages.Count == 0)
		{
			return string.Empty;
		}
		return string.Join(" || ", from message in messages.Take(3)
			select message?.Role + ":" + SafeLogPreview(message?.Content, 300));
	}

	private static string SafeLogPreview(string text, int maxChars)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}
		string normalized = text.Replace("\r", "\\r").Replace("\n", "\\n");
		return normalized.Substring(0, Math.Min(normalized.Length, maxChars));
	}

	private MainlineCampaign EnsureMainlineCampaignShell(NPCInfo npcInfo)
	{
		MainlineCampaign campaign = _mainlineCampaign;
		if (campaign != null && campaign.Status != CampaignStatus.Completed && campaign.Status != CampaignStatus.Failed && (npcInfo == null || campaign.SeedNpcId == 0 || campaign.SeedNpcId == npcInfo.NpcID))
		{
			return campaign;
		}
		TryGetGameTime(out var createdAt);
		return _mainlineCampaign = new MainlineCampaign
		{
			CampaignId = Guid.NewGuid().ToString("N"),
			Title = (npcInfo?.Name ?? "无名") + "主线",
			Premise = string.Empty,
			StoryBible = string.Empty,
			SeedNpcId = (npcInfo?.NpcID ?? 0),
			SeedNpcName = (npcInfo?.Name ?? string.Empty),
			CreatedAt = createdAt,
			Status = CampaignStatus.Active,
			Continuity = new ContinuityMemorySnapshot
			{
				Summary = string.Empty,
				LastUpdatedAt = createdAt
			}
		};
	}

	private MainlineChapter EnsureCurrentMainlineChapter(MainlineCampaign campaign, NPCInfo npcInfo)
	{
		MainlineChapter chapter = campaign?.GetCurrentChapter();
		if (chapter != null && chapter.Status != MainlineChapterStatus.Completed && chapter.Status != MainlineChapterStatus.Failed)
		{
			return chapter;
		}
		chapter = new MainlineChapter
		{
			ChapterId = Guid.NewGuid().ToString("N"),
			Title = (npcInfo?.Name ?? "无名") + "当前主线",
			Summary = string.Empty,
			Theme = string.Empty,
			Status = MainlineChapterStatus.Active
		};
		campaign?.Chapters.Add(chapter);
		if (campaign != null)
		{
			campaign.CurrentChapterIndex = Math.Max(0, campaign.Chapters.Count - 1);
			campaign.Status = CampaignStatus.Active;
		}
		return chapter;
	}

	private MainlineBeat GetCurrentMainlineBeatForGeneration(MainlineChapter chapter)
	{
		MainlineBeat beat = chapter?.GetCurrentBeat();
		if (beat != null && beat.Status != MainlineBeatStatus.Completed && beat.Status != MainlineBeatStatus.Failed)
		{
			return beat;
		}
		return null;
	}

	private bool HasStoryDraft(MainlineBeat beat)
	{
		return beat?.StoryDraft?.ScriptBlocks != null && beat.StoryDraft.ScriptBlocks.Any((MainlineScriptBlock block) => block != null && ((!string.IsNullOrWhiteSpace(block.ScenePurpose) && block.Lines != null && block.Lines.Count > 0) || (block.Kind == MainlineScriptBlockKind.Choice && block.Choices != null && block.Choices.Count > 0)));
	}

	private static bool HasRawNarrativeDraft(MainlineBeat beat)
	{
		return !string.IsNullOrWhiteSpace(beat?.RawNarrativeDraft?.RawText);
	}

	private void AttachCurrentBeatToChapter(MainlineChapter chapter, MainlineBeat beat)
	{
		if (chapter != null && beat != null)
		{
			beat.Status = MainlineBeatStatus.Active;
			beat.BeatId = (string.IsNullOrWhiteSpace(beat.BeatId) ? Guid.NewGuid().ToString("N") : beat.BeatId.Trim());
			chapter.CurrentBeat = beat;
			chapter.Status = MainlineChapterStatus.Active;
			if (chapter.PendingBiographyEvent != null)
			{
				chapter.PendingBiographyEvent.Status = PendingBiographyEventStatus.ActiveStory;
			}
		}
	}

	private void ApplyBeatGamePlan(MainlineBeat beat, MainlineBeatGamePlan gamePlan)
	{
		if (beat != null && gamePlan != null)
		{
			beat.GamePlan = gamePlan;
			if (gamePlan.UsedNpcIds != null && gamePlan.UsedNpcIds.Count > 0)
			{
				beat.KeyNpcIds = new List<int>(gamePlan.UsedNpcIds);
			}
			if (gamePlan.UsedSceneIds != null && gamePlan.UsedSceneIds.Count > 0)
			{
				beat.KeyLocationIds = new List<string>(gamePlan.UsedSceneIds);
			}
		}
	}

	private static bool LooksLikeStructuredMainlineOutput(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string trimmed = text.TrimStart();
		if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("```", StringComparison.Ordinal))
		{
			return true;
		}
		string[] lines = trimmed.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		if (lines.Length != 0 && lines.All((string line) => line.TrimStart().StartsWith("- ", StringComparison.Ordinal) || line.TrimStart().StartsWith("* ", StringComparison.Ordinal)))
		{
			return true;
		}
		return false;
	}

	private static int CountVisibleMainlineNarrativeCharacters(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0;
		}
		return text.Count((char ch) => !char.IsWhiteSpace(ch));
	}

	private MainlineStageParseResult<MainlineRawNarrativeDraft> TryParseMainlineRawNarrativeResponse(string responseText)
	{
		if (!TryParseMainlineRawNarrativeDraft(responseText, out var draft, out var error))
		{
			return FailMainlineStageParse<MainlineRawNarrativeDraft>(error);
		}
		return SucceedMainlineStageParse(draft);
	}

	private static bool TryParseMainlineRawNarrativeDraft(string responseText, out MainlineRawNarrativeDraft draft, out string error)
	{
		draft = null;
		error = null;
		string text = responseText?.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			error = "剧情正文不能为空";
			return false;
		}
		if (LooksLikeStructuredMainlineOutput(text))
		{
			error = "剧情正文必须是纯文本，不能返回 JSON、Markdown 围栏或提纲";
			return false;
		}
		int visibleLength = CountVisibleMainlineNarrativeCharacters(text);
		if (visibleLength < 2500)
		{
			error = $"剧情正文长度不足，当前可见字符数为 {visibleLength}，至少需要 {2500}";
			return false;
		}
		draft = new MainlineRawNarrativeDraft
		{
			RawText = text
		};
		return true;
	}

	private MainlineStageParseResult<MainlineBeat> TryParseMainlineStructuredBeatResponse(string responseText, NPCInfo npcInfo, QuestElementPicker.PickedElements pickedElements, MainlineRawNarrativeDraft rawNarrativeDraft)
	{
		if (!TryParseJsonObjectResponse(responseText, "主线剧情结构化", out var beatObj, out var error))
		{
			return FailMainlineStageParse<MainlineBeat>(error);
		}
		if (!TryParseMainlineStructuredBeat(beatObj, npcInfo, pickedElements, rawNarrativeDraft, out var beat, out error))
		{
			return FailMainlineStageParse<MainlineBeat>(error);
		}
		return SucceedMainlineStageParse(beat);
	}

	private bool TryParseMainlineStructuredBeat(JObject beatObj, NPCInfo npcInfo, QuestElementPicker.PickedElements pickedElements, MainlineRawNarrativeDraft rawNarrativeDraft, out MainlineBeat beat, out string error)
	{
		beat = null;
		error = null;
		if (beatObj == null)
		{
			error = "主线剧情结构化 JSON 为空";
			return false;
		}
		beat = new MainlineBeat
		{
			BeatId = Guid.NewGuid().ToString("N"),
			Title = (beatObj["title"]?.ToString() ?? ((npcInfo?.Name ?? "无名") + "当前拍")),
			Summary = (beatObj["summary"]?.ToString() ?? string.Empty),
			RawNarrativeDraft = (rawNarrativeDraft ?? new MainlineRawNarrativeDraft()),
			StoryDraft = new MainlineBeatStoryDraft(),
			TimeIntent = new BeatTimeIntent()
		};
		if (!TryNormalizeMainlineTimeCategory(beatObj["timeCategory"]?.ToString(), out var timeCategory, out error))
		{
			beat = null;
			return false;
		}
		beat.TimeIntent.TimeBand = timeCategory;
		if (!(beatObj["scriptBlocks"] is JArray { Count: not 0 } scriptBlocks))
		{
			error = "scriptBlocks 不能为空";
			beat = null;
			return false;
		}
		string firstBlockError = null;
		for (int blockIndex = 0; blockIndex < scriptBlocks.Count; blockIndex++)
		{
			MainlineScriptBlock block;
			if (!(scriptBlocks[blockIndex] is JObject blockObj))
			{
				if (string.IsNullOrWhiteSpace(firstBlockError))
				{
					firstBlockError = $"scriptBlocks[{blockIndex}] 必须是对象";
				}
			}
			else if (!TryParseMainlineScriptBlock(blockObj, $"scriptBlocks[{blockIndex}]", npcInfo, pickedElements, out block, out error))
			{
				if (string.IsNullOrWhiteSpace(firstBlockError))
				{
					firstBlockError = error;
				}
			}
			else
			{
				beat.StoryDraft.ScriptBlocks.Add(block);
			}
		}
		if (beat.StoryDraft.ScriptBlocks.Count == 0 && !string.IsNullOrWhiteSpace(firstBlockError))
		{
			error = firstBlockError;
			beat = null;
			return false;
		}
		PruneInvalidStructuredBeatContent(beat);
		if (!TryValidateParsedMainlineStructuredBeat(beat, out error))
		{
			beat = null;
			return false;
		}
		error = null;
		PopulateDerivedBeatReferences(beat);
		return true;
	}

	private MainlineStageParseResult<MainlineBeatGamePlan> TryParseMainlineBeatGamePlanResponse(string responseText, MainlineBeat beat)
	{
		if (!TryParseJsonObjectResponse(responseText, "剧情功能计划", out var gamePlanObj, out var error))
		{
			return FailMainlineStageParse<MainlineBeatGamePlan>(error);
		}
		if (!TryParseMainlineBeatGamePlan(gamePlanObj, beat, out var gamePlan, out error))
		{
			return FailMainlineStageParse<MainlineBeatGamePlan>(error);
		}
		return SucceedMainlineStageParse(gamePlan);
	}

	private MainlineBeatGamePlan ParseMainlineBeatGamePlan(JObject gamePlanObj, MainlineBeat beat)
	{
		MainlineBeatGamePlan gamePlan;
		string error;
		return TryParseMainlineBeatGamePlan(gamePlanObj, beat, out gamePlan, out error) ? gamePlan : null;
	}

	private bool TryParseMainlineBeatGamePlan(JObject gamePlanObj, MainlineBeat beat, out MainlineBeatGamePlan gamePlan, out string error)
	{
		gamePlan = null;
		error = null;
		if (gamePlanObj == null)
		{
			error = "剧情功能计划 JSON 为空";
			return false;
		}
		if (beat == null)
		{
			error = "剧情功能计划缺少对应的剧情拍上下文";
			return false;
		}
		gamePlan = new MainlineBeatGamePlan();
		if (!(gamePlanObj["flowSteps"] is JArray { Count: not 0 } flowSteps))
		{
			error = "剧情功能计划缺少非空 flowSteps";
			gamePlan = null;
			return false;
		}
		for (int stepIndex = 0; stepIndex < flowSteps.Count; stepIndex++)
		{
			if (flowSteps[stepIndex] is JObject stepObj && TryParseMainlineSemanticFlowStep(stepObj, $"flowSteps[{stepIndex}]", beat, out var step, out error))
			{
				gamePlan.FlowSteps.Add(step);
			}
		}
		PruneInvalidSemanticPlanContent(beat, gamePlan);
		if (gamePlan.FlowSteps.Count == 0)
		{
			error = "剧情功能计划缺少有效 flowSteps";
			gamePlan = null;
			return false;
		}
		PopulateDerivedSemanticPlanReferences(gamePlan);
		if (!TryValidateMainlineSemanticPlanFlowStructure(gamePlan, beat, out error))
		{
			gamePlan = null;
			return false;
		}
		if (!TryValidateMainlineSemanticPlanScriptBlockReferences(gamePlan, beat, out error))
		{
			gamePlan = null;
			return false;
		}
		error = null;
		return true;
	}

	private static void PopulateDerivedBeatReferences(MainlineBeat beat)
	{
		if (beat == null)
		{
			return;
		}
		beat.KeyNpcIds.Clear();
		beat.KeyLocationIds.Clear();
		foreach (MainlineScriptBlock block in beat.StoryDraft?.ScriptBlocks ?? new List<MainlineScriptBlock>())
		{
			if (block == null)
			{
				continue;
			}
			foreach (int npcId in block.ParticipantNpcIds ?? new List<int>())
			{
				if (npcId > 0 && !beat.KeyNpcIds.Contains(npcId))
				{
					beat.KeyNpcIds.Add(npcId);
				}
			}
			if (!string.IsNullOrWhiteSpace(block.SceneId) && !beat.KeyLocationIds.Contains(block.SceneId))
			{
				beat.KeyLocationIds.Add(block.SceneId);
			}
		}
	}

	private static void PopulateDerivedSemanticPlanReferences(MainlineBeatGamePlan gamePlan)
	{
		if (gamePlan == null)
		{
			return;
		}
		gamePlan.UsedNpcIds.Clear();
		gamePlan.UsedSceneIds.Clear();
		foreach (MainlineSemanticFlowStep step in gamePlan.FlowSteps ?? new List<MainlineSemanticFlowStep>())
		{
			if (step == null)
			{
				continue;
			}
			if (step.TargetNpcId > 0 && !gamePlan.UsedNpcIds.Contains(step.TargetNpcId))
			{
				gamePlan.UsedNpcIds.Add(step.TargetNpcId);
			}
			if (!string.IsNullOrWhiteSpace(step.SceneId) && !gamePlan.UsedSceneIds.Contains(step.SceneId))
			{
				gamePlan.UsedSceneIds.Add(step.SceneId);
			}
			foreach (MainlineSemanticEffectStep effect in step.Effects ?? new List<MainlineSemanticEffectStep>())
			{
				if (effect != null && effect.TargetNpcId > 0 && !gamePlan.UsedNpcIds.Contains(effect.TargetNpcId))
				{
					gamePlan.UsedNpcIds.Add(effect.TargetNpcId);
				}
			}
		}
	}

	private static void PruneInvalidStructuredBeatContent(MainlineBeat beat)
	{
		if (beat?.StoryDraft?.ScriptBlocks == null || beat.StoryDraft.ScriptBlocks.Count == 0)
		{
			return;
		}
		List<MainlineScriptBlock> prunedBlocks = new List<MainlineScriptBlock>();
		MainlineScriptBlock primaryChoiceBlock = null;
		bool choiceSeen = false;
		foreach (MainlineScriptBlock block in beat.StoryDraft.ScriptBlocks.Where((MainlineScriptBlock candidate) => candidate != null))
		{
			bool hasBranchRef = !string.IsNullOrWhiteSpace(block.BranchRef);
			if (block.Kind == MainlineScriptBlockKind.Choice)
			{
				if (!choiceSeen)
				{
					block.BranchRef = string.Empty;
					primaryChoiceBlock = block;
					choiceSeen = true;
					prunedBlocks.Add(block);
				}
			}
			else if (!choiceSeen)
			{
				if (!hasBranchRef)
				{
					prunedBlocks.Add(block);
				}
			}
			else
			{
				string normalizedBranchRef = NormalizeMainlineReferenceToken(block.BranchRef);
				if (!string.IsNullOrWhiteSpace(normalizedBranchRef))
				{
					block.BranchRef = normalizedBranchRef;
					prunedBlocks.Add(block);
				}
			}
		}
		if (primaryChoiceBlock != null)
		{
			HashSet<string> declaredChoiceRefs = new HashSet<string>(from choice in primaryChoiceBlock.Choices ?? new List<MainlineDialogueChoiceDraft>()
				where choice != null && !string.IsNullOrWhiteSpace(choice.ChoiceRef)
				select NormalizeMainlineReferenceToken(choice.ChoiceRef), StringComparer.OrdinalIgnoreCase);
			HashSet<string> survivingBranchRefs = new HashSet<string>(from mainlineScriptBlock in prunedBlocks
				where mainlineScriptBlock != null && mainlineScriptBlock != primaryChoiceBlock && !string.IsNullOrWhiteSpace(mainlineScriptBlock.BranchRef) && declaredChoiceRefs.Contains(NormalizeMainlineReferenceToken(mainlineScriptBlock.BranchRef))
				select NormalizeMainlineReferenceToken(mainlineScriptBlock.BranchRef), StringComparer.OrdinalIgnoreCase);
			primaryChoiceBlock.Choices = (primaryChoiceBlock.Choices ?? new List<MainlineDialogueChoiceDraft>()).Where((MainlineDialogueChoiceDraft choice) => choice != null && survivingBranchRefs.Contains(NormalizeMainlineReferenceToken(choice.ChoiceRef))).Select(delegate(MainlineDialogueChoiceDraft choice)
			{
				choice.ChoiceRef = NormalizeMainlineReferenceToken(choice.ChoiceRef);
				return choice;
			}).ToList();
			prunedBlocks = prunedBlocks.Where((MainlineScriptBlock mainlineScriptBlock) => mainlineScriptBlock == primaryChoiceBlock || string.IsNullOrWhiteSpace(mainlineScriptBlock.BranchRef) || survivingBranchRefs.Contains(NormalizeMainlineReferenceToken(mainlineScriptBlock.BranchRef))).ToList();
		}
		beat.StoryDraft.ScriptBlocks = prunedBlocks;
	}

	private static bool TryValidateParsedMainlineStructuredBeat(MainlineBeat beat, out string error)
	{
		error = null;
		List<MainlineScriptBlock> scriptBlocks = beat?.StoryDraft?.ScriptBlocks ?? new List<MainlineScriptBlock>();
		if (scriptBlocks.Count == 0)
		{
			error = "scriptBlocks 不能为空";
			return false;
		}
		MainlineScriptBlock choiceBlock = scriptBlocks.FirstOrDefault((MainlineScriptBlock block) => block != null && block.Kind == MainlineScriptBlockKind.Choice);
		if (choiceBlock == null)
		{
			error = "scriptBlocks 缺少主 choice block";
			return false;
		}
		if (choiceBlock.Choices == null || choiceBlock.Choices.Count == 0)
		{
			error = "主 choice block 缺少有效 choices";
			return false;
		}
		if (!scriptBlocks.Any((MainlineScriptBlock block) => block != null && block != choiceBlock && !string.IsNullOrWhiteSpace(block.BranchRef)))
		{
			error = "主 choice block 之后缺少有效分支 block";
			return false;
		}
		return true;
	}

	private static void PruneInvalidSemanticPlanContent(MainlineBeat beat, MainlineBeatGamePlan gamePlan)
	{
		if (gamePlan?.FlowSteps == null || gamePlan.FlowSteps.Count == 0)
		{
			return;
		}
		MainlineScriptBlock choiceBlock = beat?.StoryDraft?.ScriptBlocks?.FirstOrDefault((MainlineScriptBlock block) => block != null && block.Kind == MainlineScriptBlockKind.Choice);
		HashSet<string> validChoiceRefs = new HashSet<string>(from choice in choiceBlock?.Choices ?? new List<MainlineDialogueChoiceDraft>()
			where choice != null && !string.IsNullOrWhiteSpace(choice.ChoiceRef)
			select NormalizeMainlineReferenceToken(choice.ChoiceRef), StringComparer.OrdinalIgnoreCase);
		List<MainlineSemanticFlowStep> prunedSteps = new List<MainlineSemanticFlowStep>();
		bool presentChoiceSeen = false;
		foreach (MainlineSemanticFlowStep step in gamePlan.FlowSteps.Where((MainlineSemanticFlowStep candidate) => candidate != null))
		{
			bool hasBranchRef = !string.IsNullOrWhiteSpace(step.BranchRef);
			if (step.Kind == MainlineSemanticFlowStepKind.PresentChoice)
			{
				if (!presentChoiceSeen)
				{
					step.BranchRef = string.Empty;
					presentChoiceSeen = true;
					prunedSteps.Add(step);
				}
			}
			else if (!presentChoiceSeen)
			{
				if (!hasBranchRef)
				{
					prunedSteps.Add(step);
				}
			}
			else
			{
				string normalizedBranchRef = NormalizeMainlineReferenceToken(step.BranchRef);
				if (!string.IsNullOrWhiteSpace(normalizedBranchRef) && validChoiceRefs.Contains(normalizedBranchRef))
				{
					step.BranchRef = normalizedBranchRef;
					prunedSteps.Add(step);
				}
			}
		}
		if (choiceBlock != null)
		{
			HashSet<string> survivingBranchRefs = new HashSet<string>(from mainlineSemanticFlowStep in prunedSteps
				where mainlineSemanticFlowStep != null && !string.IsNullOrWhiteSpace(mainlineSemanticFlowStep.BranchRef)
				select NormalizeMainlineReferenceToken(mainlineSemanticFlowStep.BranchRef), StringComparer.OrdinalIgnoreCase);
			choiceBlock.Choices = (choiceBlock.Choices ?? new List<MainlineDialogueChoiceDraft>()).Where((MainlineDialogueChoiceDraft choice) => choice != null && survivingBranchRefs.Contains(NormalizeMainlineReferenceToken(choice.ChoiceRef))).Select(delegate(MainlineDialogueChoiceDraft choice)
			{
				choice.ChoiceRef = NormalizeMainlineReferenceToken(choice.ChoiceRef);
				return choice;
			}).ToList();
		}
		gamePlan.FlowSteps = prunedSteps;
	}

	private static bool TryValidateMainlineSemanticPlanFlowStructure(MainlineBeatGamePlan gamePlan, MainlineBeat beat, out string error)
	{
		error = null;
		if (gamePlan?.FlowSteps == null || gamePlan.FlowSteps.Count == 0)
		{
			return true;
		}
		MainlineScriptBlock choiceBlock = beat?.StoryDraft?.ScriptBlocks?.FirstOrDefault((MainlineScriptBlock block) => block != null && block.Kind == MainlineScriptBlockKind.Choice);
		if (choiceBlock == null)
		{
			error = "结构化剧情拍缺少主 choice block";
			return false;
		}
		HashSet<string> validChoiceRefs = new HashSet<string>(from choice in choiceBlock.Choices ?? new List<MainlineDialogueChoiceDraft>()
			where !string.IsNullOrWhiteSpace(choice?.ChoiceRef)
			select NormalizeMainlineReferenceToken(choice.ChoiceRef), StringComparer.OrdinalIgnoreCase);
		int presentChoiceIndex = -1;
		Dictionary<string, int> branchFollowups = validChoiceRefs.ToDictionary((string choiceRef) => choiceRef, (string _) => 0, StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < gamePlan.FlowSteps.Count; i++)
		{
			MainlineSemanticFlowStep step = gamePlan.FlowSteps[i];
			if (step == null)
			{
				continue;
			}
			bool isChoiceScoped = !string.IsNullOrWhiteSpace(step.BranchRef);
			if (step.Kind == MainlineSemanticFlowStepKind.PresentChoice)
			{
				if (presentChoiceIndex >= 0)
				{
					error = "flowSteps 只能有一个 present_choice";
					return false;
				}
				if (isChoiceScoped)
				{
					error = $"flowSteps[{i}] 的 present_choice 不允许携带 branchRef";
					return false;
				}
				presentChoiceIndex = i;
				continue;
			}
			if (presentChoiceIndex < 0)
			{
				if (isChoiceScoped)
				{
					error = $"flowSteps[{i}] 在没有前置 present_choice 的情况下使用了 branchRef";
					return false;
				}
				continue;
			}
			if (!isChoiceScoped)
			{
				error = $"flowSteps[{i}] 位于 present_choice 之后，必须携带 branchRef";
				return false;
			}
			string branchRef = NormalizeMainlineReferenceToken(step.BranchRef);
			if (!validChoiceRefs.Contains(branchRef))
			{
				error = $"flowSteps[{i}] 引用了不存在的 branchRef: {branchRef}";
				return false;
			}
			branchFollowups[branchRef]++;
		}
		if (presentChoiceIndex < 0)
		{
			error = "flowSteps 缺少 present_choice";
			return false;
		}
		foreach (KeyValuePair<string, int> pair in branchFollowups)
		{
			if (pair.Value <= 0)
			{
				error = "choiceRef=" + pair.Key + " 缺少独立后续步骤";
				return false;
			}
		}
		return true;
	}

	private static bool TryValidateMainlineSemanticPlanScriptBlockReferences(MainlineBeatGamePlan gamePlan, MainlineBeat beat, out string error)
	{
		error = null;
		if (gamePlan?.FlowSteps == null || gamePlan.FlowSteps.Count == 0)
		{
			return true;
		}
		Dictionary<string, MainlineScriptBlock> scriptBlocks = (beat?.StoryDraft?.ScriptBlocks ?? new List<MainlineScriptBlock>()).Where((MainlineScriptBlock mainlineScriptBlock) => mainlineScriptBlock != null && !string.IsNullOrWhiteSpace(mainlineScriptBlock.BlockRef)).ToDictionary((MainlineScriptBlock mainlineScriptBlock) => NormalizeMainlineReferenceToken(mainlineScriptBlock.BlockRef), (MainlineScriptBlock result) => result, StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < gamePlan.FlowSteps.Count; i++)
		{
			MainlineSemanticFlowStep step = gamePlan.FlowSteps[i];
			if (step == null || (step.Kind != MainlineSemanticFlowStepKind.PlayScriptBlock && step.Kind != MainlineSemanticFlowStepKind.PresentChoice))
			{
				continue;
			}
			string sourceBlockRef = NormalizeMainlineReferenceToken(step.SourceBlockRef);
			if (string.IsNullOrWhiteSpace(sourceBlockRef))
			{
				error = $"flowSteps[{i}] 的 {step.Kind} 缺少 sourceBlockRef";
				return false;
			}
			if (!scriptBlocks.TryGetValue(sourceBlockRef, out var block))
			{
				error = $"flowSteps[{i}] 引用了不存在的 sourceBlockRef: {sourceBlockRef}";
				return false;
			}
			if (step.Kind == MainlineSemanticFlowStepKind.PresentChoice && block.Kind != MainlineScriptBlockKind.Choice)
			{
				error = $"flowSteps[{i}] 的 present_choice 必须引用 kind=choice 的 script block，当前是 {block.Kind}";
				return false;
			}
			if (step.Kind == MainlineSemanticFlowStepKind.PlayScriptBlock && block.Kind == MainlineScriptBlockKind.Choice)
			{
				error = $"flowSteps[{i}] 的 play_script_block 不能引用 kind=choice 的 script block: {sourceBlockRef}";
				return false;
			}
			if (!string.IsNullOrWhiteSpace(step.BranchRef))
			{
				string branchRef = NormalizeMainlineReferenceToken(step.BranchRef);
				string blockBranchRef = NormalizeMainlineReferenceToken(block.BranchRef);
				if (!string.IsNullOrWhiteSpace(blockBranchRef) && !string.Equals(blockBranchRef, branchRef, StringComparison.OrdinalIgnoreCase))
				{
					error = $"flowSteps[{i}] 的 {sourceBlockRef} 不属于 branchRef={branchRef} 分支";
					return false;
				}
			}
		}
		return true;
	}

	private static bool TryNormalizeMainlineTimeCategory(string rawValue, out string normalized, out string error)
	{
		normalized = string.Empty;
		error = null;
		string value = rawValue?.Trim();
		if (string.IsNullOrWhiteSpace(value))
		{
			error = "timeCategory 不能为空，且只允许 短期 / 中期 / 长期";
			return false;
		}
		switch (value)
		{
		case "短期":
		case "中期":
		case "长期":
			normalized = value;
			return true;
		default:
			error = "timeCategory 不合法: " + value;
			return false;
		}
	}

	private static string NormalizeMainlineProtocolToken(string rawValue)
	{
		if (string.IsNullOrWhiteSpace(rawValue))
		{
			return string.Empty;
		}
		StringBuilder builder = new StringBuilder(rawValue.Length);
		foreach (char ch in rawValue)
		{
			if (!char.IsWhiteSpace(ch))
			{
				builder.Append(char.ToLowerInvariant(ch));
			}
		}
		return builder.ToString();
	}

	private static string NormalizeMainlineReferenceToken(string rawValue)
	{
		return NormalizeMainlineProtocolToken(rawValue);
	}

	private bool TryResolveMainlineNpcReference(string rawNpcName, NPCInfo anchorNpcInfo, QuestElementPicker.PickedElements pickedElements, out int npcId, out string resolvedNpcName, out string error)
	{
		npcId = 0;
		resolvedNpcName = string.Empty;
		error = null;
		string npcName = QuestElementPicker.NormalizeNarrativeEntityText(rawNpcName);
		if (string.IsNullOrWhiteSpace(npcName))
		{
			error = "NPC 名称不能为空";
			return false;
		}
		List<(int, string)> candidates = new List<(int, string)>();
		if (anchorNpcInfo != null && QuestElementPicker.NarrativeEntityTextEquals(anchorNpcInfo.Name, npcName))
		{
			candidates.Add((anchorNpcInfo.NpcID, anchorNpcInfo.Name.Trim()));
		}
		foreach (QuestElementPicker.PickedNpc pickedNpc in pickedElements?.Npcs ?? new List<QuestElementPicker.PickedNpc>())
		{
			if (pickedNpc != null && QuestElementPicker.NarrativeEntityTextEquals(pickedNpc.Name, npcName))
			{
				if (!int.TryParse(pickedNpc.Id, out var parsedNpcId) || parsedNpcId <= 0)
				{
					throw new InvalidOperationException("候选 NPC " + pickedNpc.Name + " 缺少可用 id");
				}
				candidates.Add((parsedNpcId, pickedNpc.Name.Trim()));
			}
		}
		candidates = candidates.Where<(int, string)>(((int NpcId, string Name) candidate) => candidate.NpcId > 0 && !string.IsNullOrWhiteSpace(candidate.Name)).Distinct().ToList();
		if (candidates.Count == 0)
		{
			error = "无法从当前主线候选集中解析 NPC 名称: " + npcName;
			return false;
		}
		if (candidates.Count > 1)
		{
			error = "当前主线候选集中存在重名 NPC，无法唯一解析: " + npcName;
			return false;
		}
		npcId = candidates[0].Item1;
		resolvedNpcName = candidates[0].Item2;
		return true;
	}

	private bool TryResolveMainlineSceneReference(string rawSceneName, QuestElementPicker.PickedElements pickedElements, out string sceneId, out string resolvedSceneName, out string error)
	{
		sceneId = string.Empty;
		resolvedSceneName = string.Empty;
		error = null;
		string sceneName = QuestElementPicker.NormalizeNarrativeEntityText(rawSceneName);
		if (string.IsNullOrWhiteSpace(sceneName))
		{
			error = "场景名称不能为空";
			return false;
		}
		List<QuestElementPicker.PickedLocation> candidatePool = (pickedElements?.Locations ?? new List<QuestElementPicker.PickedLocation>()).Where((QuestElementPicker.PickedLocation location) => location != null && !string.IsNullOrWhiteSpace(location.Name)).ToList();
		List<QuestElementPicker.PickedLocation> candidates = candidatePool.Where((QuestElementPicker.PickedLocation location) => QuestElementPicker.NarrativeEntityTextEquals(location.Name, sceneName)).ToList();
		if (TryResolveSingleMainlineSceneCandidate(candidates, sceneName, out sceneId, out resolvedSceneName, out error))
		{
			return true;
		}
		if (!string.IsNullOrWhiteSpace(error))
		{
			return false;
		}
		candidates = candidatePool.Where((QuestElementPicker.PickedLocation location) => MainlineSceneTextMatchesCandidate(sceneName, location)).ToList();
		if (TryResolveSingleMainlineSceneCandidate(candidates, sceneName, out sceneId, out resolvedSceneName, out error))
		{
			return true;
		}
		if (!string.IsNullOrWhiteSpace(error))
		{
			return false;
		}
		if (QuestElementPicker.TryResolveOfficialLocationId(sceneName, out var officialSceneId))
		{
			sceneId = officialSceneId;
			resolvedSceneName = (string.IsNullOrWhiteSpace(rawSceneName) ? sceneName : rawSceneName.Trim());
			error = null;
			return true;
		}
		error = "无法从当前主线候选集或官方场景总池中解析场景名称: " + sceneName;
		return false;
	}

	private static bool TryResolveSingleMainlineSceneCandidate(List<QuestElementPicker.PickedLocation> candidates, string rawSceneName, out string sceneId, out string resolvedSceneName, out string error)
	{
		sceneId = string.Empty;
		resolvedSceneName = string.Empty;
		error = null;
		if (candidates.Count == 1)
		{
			sceneId = candidates[0].Id?.Trim() ?? string.Empty;
			resolvedSceneName = candidates[0].Name?.Trim() ?? rawSceneName;
			if (string.IsNullOrWhiteSpace(sceneId))
			{
				throw new InvalidOperationException("候选场景 " + resolvedSceneName + " 缺少可用 id");
			}
			return true;
		}
		if (candidates.Count > 1)
		{
			error = "当前主线候选集中存在重名场景，无法唯一解析: " + rawSceneName;
			return false;
		}
		return false;
	}

	private static bool MainlineSceneTextMatchesCandidate(string rawSceneName, QuestElementPicker.PickedLocation location)
	{
		string candidateName = location?.Name;
		if (string.IsNullOrWhiteSpace(rawSceneName) || string.IsNullOrWhiteSpace(candidateName))
		{
			return false;
		}
		return QuestElementPicker.NarrativeEntityTextContains(rawSceneName, candidateName) || QuestElementPicker.NarrativeEntityTextContains(candidateName, rawSceneName);
	}

	private bool TryResolveBeatNpcReference(string rawNpcName, MainlineBeat beat, out int npcId, out string resolvedNpcName, out string error)
	{
		npcId = 0;
		resolvedNpcName = string.Empty;
		error = null;
		string npcName = QuestElementPicker.NormalizeNarrativeEntityText(rawNpcName);
		if (string.IsNullOrWhiteSpace(npcName))
		{
			error = "NPC 名称不能为空";
			return false;
		}
		List<(int, string)> candidates = new List<(int, string)>();
		foreach (MainlineScriptBlock block in beat?.StoryDraft?.ScriptBlocks ?? new List<MainlineScriptBlock>())
		{
			if (block == null || block.ParticipantNpcNames == null)
			{
				continue;
			}
			for (int i = 0; i < block.ParticipantNpcNames.Count; i++)
			{
				if (QuestElementPicker.NarrativeEntityTextEquals(block.ParticipantNpcNames[i], npcName))
				{
					int candidateNpcId = ((i < (block.ParticipantNpcIds?.Count ?? 0)) ? block.ParticipantNpcIds[i] : 0);
					candidates.Add((candidateNpcId, block.ParticipantNpcNames[i].Trim()));
				}
			}
		}
		candidates = candidates.Where<(int, string)>(((int NpcId, string Name) candidate) => candidate.NpcId > 0 && !string.IsNullOrWhiteSpace(candidate.Name)).Distinct().ToList();
		if (candidates.Count == 0)
		{
			error = "无法从当前剧情拍解析 NPC 名称: " + npcName;
			return false;
		}
		if (candidates.Count > 1)
		{
			error = "当前剧情拍中存在重名 NPC，无法唯一解析: " + npcName;
			return false;
		}
		npcId = candidates[0].Item1;
		resolvedNpcName = candidates[0].Item2;
		return true;
	}

	private bool TryResolveBeatSceneReference(string rawSceneName, MainlineBeat beat, out string sceneId, out string resolvedSceneName, out string error)
	{
		sceneId = string.Empty;
		resolvedSceneName = string.Empty;
		error = null;
		string sceneName = QuestElementPicker.NormalizeNarrativeEntityText(rawSceneName);
		if (string.IsNullOrWhiteSpace(sceneName))
		{
			error = "场景名称不能为空";
			return false;
		}
		List<(string, string)> candidates = new List<(string, string)>();
		foreach (MainlineScriptBlock block in beat?.StoryDraft?.ScriptBlocks ?? new List<MainlineScriptBlock>())
		{
			if (block != null && QuestElementPicker.NarrativeEntityTextEquals(block.SceneName, sceneName))
			{
				candidates.Add((block.SceneId?.Trim() ?? string.Empty, block.SceneName.Trim()));
			}
		}
		candidates = candidates.Where<(string, string)>(((string SceneId, string Name) candidate) => !string.IsNullOrWhiteSpace(candidate.SceneId) && !string.IsNullOrWhiteSpace(candidate.Name)).Distinct().ToList();
		if (candidates.Count == 0)
		{
			error = "无法从当前剧情拍解析场景名称: " + sceneName;
			return false;
		}
		if (candidates.Count > 1)
		{
			error = "当前剧情拍中存在重名场景，无法唯一解析: " + sceneName;
			return false;
		}
		if (candidates.Count == 1)
		{
			sceneId = candidates[0].Item1;
			resolvedSceneName = candidates[0].Item2;
			return true;
		}
		if (QuestElementPicker.TryResolveOfficialLocationId(sceneName, out var officialSceneId))
		{
			sceneId = officialSceneId;
			resolvedSceneName = (string.IsNullOrWhiteSpace(rawSceneName) ? sceneName : rawSceneName.Trim());
			error = null;
			return true;
		}
		error = "无法从当前剧情拍或官方场景总池中解析场景名称: " + sceneName;
		return false;
	}

	private static bool TrySplitCompactStoryLine(string rawLine, out string speakerType, out string speakerName, out string text)
	{
		speakerType = string.Empty;
		speakerName = string.Empty;
		text = string.Empty;
		if (string.IsNullOrWhiteSpace(rawLine))
		{
			return false;
		}
		List<string> parts = new List<string>();
		StringBuilder current = new StringBuilder();
		bool escaping = false;
		foreach (char ch in rawLine)
		{
			if (escaping)
			{
				current.Append(ch);
				escaping = false;
				continue;
			}
			switch (ch)
			{
			case '\\':
				escaping = true;
				break;
			case '|':
				parts.Add(current.ToString());
				current.Clear();
				break;
			default:
				current.Append(ch);
				break;
			}
		}
		if (escaping)
		{
			return false;
		}
		parts.Add(current.ToString());
		if (parts.Count != 3)
		{
			return false;
		}
		speakerType = NormalizeMainlineProtocolToken(parts[0]);
		speakerName = parts[1].Trim();
		text = parts[2].Trim();
		return true;
	}

	private static bool IsMainlinePayloadInteger(JObject payload, string key)
	{
		return payload != null && payload[key]?.Type == JTokenType.Integer;
	}

	private static bool HasMainlinePayloadText(JObject payload, string key)
	{
		return !string.IsNullOrWhiteSpace(payload?[key]?.ToString()?.Trim());
	}

	private static bool StepKindRequiresSceneName(MainlineSemanticFlowStepKind kind)
	{
		if ((uint)kind <= 3u || (uint)(kind - 7) <= 2u)
		{
			return true;
		}
		return false;
	}

	private static bool StepKindRequiresTargetNpcName(MainlineSemanticFlowStepKind kind)
	{
		switch (kind)
		{
		case MainlineSemanticFlowStepKind.EncounterAtScene:
		case MainlineSemanticFlowStepKind.FormalTalkGate:
		case MainlineSemanticFlowStepKind.ReportBackInPerson:
		case MainlineSemanticFlowStepKind.DeliverItemInPerson:
		case MainlineSemanticFlowStepKind.CombatGate:
			return true;
		default:
			return false;
		}
	}

	private static bool StepKindRequiresSourceBlockRef(MainlineSemanticFlowStepKind kind)
	{
		return kind == MainlineSemanticFlowStepKind.PlayScriptBlock || kind == MainlineSemanticFlowStepKind.PresentChoice;
	}

	private static bool StepKindRequiresItemName(MainlineSemanticFlowStepKind kind)
	{
		return kind == MainlineSemanticFlowStepKind.AcquireItemGate || kind == MainlineSemanticFlowStepKind.DeliverItemInPerson;
	}

	private static bool EffectKindRequiresTargetNpcName(MainlineSemanticEffectStepKind kind)
	{
		return kind == MainlineSemanticEffectStepKind.FavorChange;
	}

	private bool TryParseMainlineCompactStoryLine(string rawLine, string path, NPCInfo anchorNpcInfo, QuestElementPicker.PickedElements pickedElements, MainlineScriptBlock block, out AIQuestStoryLine line, out string error)
	{
		line = null;
		error = null;
		if (!TrySplitCompactStoryLine(rawLine, out var speakerType, out var speakerName, out var text))
		{
			error = path + " 必须是 <speakerType>|<speakerName>|<text> 格式";
			return false;
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			error = path + ".text 不能为空";
			return false;
		}
		line = new AIQuestStoryLine
		{
			Text = text
		};
		switch (speakerType)
		{
		case "npc":
		{
			if (string.IsNullOrWhiteSpace(speakerName))
			{
				error = path + ".speakerName 在 speakerType=Npc 时不能为空";
				line = null;
				return false;
			}
			if (!TryResolveMainlineNpcReference(speakerName, anchorNpcInfo, pickedElements, out var speakerNpcId, out var _, out error))
			{
				line = null;
				return false;
			}
			line.SpeakerType = QuestStorySpeakerType.Npc;
			line.SpeakerNpcId = speakerNpcId;
			return true;
		}
		case "player":
			if (!string.IsNullOrEmpty(speakerName))
			{
				error = path + ".speakerName 在 speakerType=Player 时必须留空";
				line = null;
				return false;
			}
			line.SpeakerType = QuestStorySpeakerType.Player;
			return true;
		case "narrator":
			if (!string.IsNullOrEmpty(speakerName))
			{
				error = path + ".speakerName 在 speakerType=Narrator 时必须留空";
				line = null;
				return false;
			}
			line.SpeakerType = QuestStorySpeakerType.Narrator;
			return true;
		default:
			error = path + ".speakerType 只允许 npc / player / narrator（大小写不敏感）";
			line = null;
			return false;
		}
	}

	private static JObject CloneMainlinePayloadObject(JToken payloadToken, string path)
	{
		if (payloadToken == null)
		{
			return new JObject();
		}
		if (!(payloadToken is JObject payloadObj))
		{
			return new JObject();
		}
		return (JObject)payloadObj.DeepClone();
	}

	private bool TryParseMainlineScriptBlock(JObject blockObj, string path, NPCInfo anchorNpcInfo, QuestElementPicker.PickedElements pickedElements, out MainlineScriptBlock block, out string error)
	{
		block = null;
		error = null;
		if (!TryParseMainlineScriptBlockKind(blockObj["kind"]?.ToString(), out var kind, out error))
		{
			error = path + ".kind " + error;
			return false;
		}
		string blockRef = NormalizeMainlineReferenceToken(blockObj["blockRef"]?.ToString());
		if (string.IsNullOrWhiteSpace(blockRef))
		{
			error = path + ".blockRef 不能为空";
			return false;
		}
		string sceneName = blockObj["sceneName"]?.ToString()?.Trim();
		if (string.IsNullOrWhiteSpace(sceneName))
		{
			error = path + ".sceneName 不能为空";
			return false;
		}
		if (!TryResolveMainlineSceneReference(sceneName, pickedElements, out var sceneId, out var resolvedSceneName, out error))
		{
			error = path + ".sceneName " + error;
			return false;
		}
		string scenePurpose = blockObj["scenePurpose"]?.ToString()?.Trim();
		if (string.IsNullOrWhiteSpace(scenePurpose))
		{
			error = path + ".scenePurpose 不能为空";
			return false;
		}
		block = new MainlineScriptBlock
		{
			BlockRef = blockRef,
			Kind = kind,
			SceneName = resolvedSceneName,
			SceneId = sceneId,
			ScenePurpose = scenePurpose
		};
		if (blockObj["participantNpcNames"] is JArray participantNpcNames)
		{
			foreach (JToken item in participantNpcNames)
			{
				string npcName = item?.ToString()?.Trim();
				if (!string.IsNullOrWhiteSpace(npcName) && TryResolveMainlineNpcReference(npcName, anchorNpcInfo, pickedElements, out var npcId, out var resolvedNpcName, out error))
				{
					if (!block.ParticipantNpcNames.Contains(resolvedNpcName))
					{
						block.ParticipantNpcNames.Add(resolvedNpcName);
					}
					if (npcId > 0 && !block.ParticipantNpcIds.Contains(npcId))
					{
						block.ParticipantNpcIds.Add(npcId);
					}
				}
			}
		}
		if (blockObj["lines"] is JArray lineArray)
		{
			for (int lineIndex = 0; lineIndex < lineArray.Count; lineIndex++)
			{
				string rawLine = lineArray[lineIndex]?.ToString();
				if (!string.IsNullOrWhiteSpace(rawLine) && TryParseMainlineCompactStoryLine(rawLine, $"{path}.lines[{lineIndex}]", anchorNpcInfo, pickedElements, block, out var line, out error))
				{
					block.Lines.Add(line);
				}
			}
		}
		if (blockObj["choices"] is JArray choiceArray)
		{
			for (int choiceIndex = 0; choiceIndex < choiceArray.Count; choiceIndex++)
			{
				if (choiceArray[choiceIndex] is JObject choiceObj)
				{
					string choiceRef = NormalizeMainlineReferenceToken(choiceObj["choiceRef"]?.ToString());
					string choiceText = choiceObj["text"]?.ToString()?.Trim();
					if (!string.IsNullOrWhiteSpace(choiceRef) && !string.IsNullOrWhiteSpace(choiceText))
					{
						block.Choices.Add(new MainlineDialogueChoiceDraft
						{
							ChoiceRef = choiceRef,
							Text = choiceText,
							ConsequenceDirection = (choiceObj["consequenceDirection"]?.ToString()?.Trim() ?? string.Empty)
						});
					}
				}
			}
		}
		block.BranchRef = NormalizeMainlineReferenceToken(blockObj["branchRef"]?.ToString());
		if (block.Lines.Count == 0)
		{
			error = path + ".lines 不能为空";
			block = null;
			return false;
		}
		if (block.Kind == MainlineScriptBlockKind.Choice)
		{
			if (block.Choices.Count == 0)
			{
				error = path + ".choices 在 kind=choice 时不能为空";
				block = null;
				return false;
			}
		}
		else if (block.Choices.Count > 0)
		{
			error = path + ".choices 只允许出现在 kind=choice 的 script block 中";
			block = null;
			return false;
		}
		return true;
	}

	private bool TryParseMainlineSemanticFlowStep(JObject stepObj, string path, MainlineBeat beat, out MainlineSemanticFlowStep step, out string error)
	{
		step = null;
		error = null;
		if (!TryParseMainlineSemanticFlowStepKind(stepObj["kind"]?.ToString(), out var kind, out error))
		{
			error = path + ".kind " + error;
			return false;
		}
		string stepRef = NormalizeMainlineReferenceToken(stepObj["stepRef"]?.ToString());
		if (string.IsNullOrWhiteSpace(stepRef))
		{
			error = path + ".stepRef 不能为空";
			return false;
		}
		step = new MainlineSemanticFlowStep
		{
			StepRef = stepRef,
			Kind = kind,
			SourceBlockRef = NormalizeMainlineReferenceToken(stepObj["sourceBlockRef"]?.ToString()),
			SceneName = (stepObj["sceneName"]?.ToString()?.Trim() ?? string.Empty),
			TargetNpcName = (stepObj["targetNpcName"]?.ToString()?.Trim() ?? string.Empty),
			BranchRef = NormalizeMainlineReferenceToken(stepObj["branchRef"]?.ToString()),
			Payload = CloneMainlinePayloadObject(stepObj["payload"], path + ".payload")
		};
		if (StepKindRequiresSourceBlockRef(kind) && string.IsNullOrWhiteSpace(step.SourceBlockRef))
		{
			error = path + ".sourceBlockRef 不能为空";
			step = null;
			return false;
		}
		if (StepKindRequiresSceneName(kind) && string.IsNullOrWhiteSpace(step.SceneName))
		{
			error = path + ".sceneName 不能为空";
			step = null;
			return false;
		}
		if (StepKindRequiresTargetNpcName(kind) && string.IsNullOrWhiteSpace(step.TargetNpcName))
		{
			error = path + ".targetNpcName 不能为空";
			step = null;
			return false;
		}
		if (!string.IsNullOrWhiteSpace(step.SceneName))
		{
			if (!TryResolveBeatSceneReference(step.SceneName, beat, out var sceneId, out var resolvedSceneName, out error))
			{
				step = null;
				return false;
			}
			step.SceneId = sceneId;
			step.SceneName = resolvedSceneName;
		}
		if (!string.IsNullOrWhiteSpace(step.TargetNpcName))
		{
			if (!TryResolveBeatNpcReference(step.TargetNpcName, beat, out var targetNpcId, out var resolvedNpcName, out error))
			{
				step = null;
				return false;
			}
			step.TargetNpcId = targetNpcId;
			step.TargetNpcName = resolvedNpcName;
		}
		if (StepKindRequiresSourceBlockRef(kind))
		{
			string sourceBlockRef = step.SourceBlockRef;
			MainlineScriptBlock sourceBlock = (beat?.StoryDraft?.ScriptBlocks ?? new List<MainlineScriptBlock>()).FirstOrDefault((MainlineScriptBlock block) => string.Equals(NormalizeMainlineReferenceToken(block?.BlockRef), sourceBlockRef, StringComparison.OrdinalIgnoreCase));
			if (sourceBlock == null)
			{
				error = path + ".sourceBlockRef 不存在: " + sourceBlockRef;
				step = null;
				return false;
			}
			if (kind == MainlineSemanticFlowStepKind.PresentChoice && sourceBlock.Kind != MainlineScriptBlockKind.Choice)
			{
				error = path + ".sourceBlockRef 必须引用 kind=choice 的剧情块";
				step = null;
				return false;
			}
			if (kind == MainlineSemanticFlowStepKind.PlayScriptBlock && sourceBlock.Kind == MainlineScriptBlockKind.Choice)
			{
				error = path + ".sourceBlockRef 不能引用 kind=choice 的剧情块";
				step = null;
				return false;
			}
		}
		if (StepKindRequiresItemName(kind) && !HasMainlinePayloadText(step.Payload, "itemName"))
		{
			error = path + ".payload.itemName 不能为空";
			step = null;
			return false;
		}
		if (StepKindRequiresItemName(kind))
		{
			string rawItemName = step.Payload["itemName"]?.ToString();
			if (!TryResolveSemanticPlanConcreteItemName(rawItemName, out var resolvedItemName, out error))
			{
				step = null;
				return false;
			}
			step.Payload["itemName"] = resolvedItemName;
		}
		if (step.Payload["count"] != null && !IsMainlinePayloadInteger(step.Payload, "count"))
		{
			error = path + ".payload.count 必须是整数";
			step = null;
			return false;
		}
		if (stepObj["effects"] is JArray effects)
		{
			for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
			{
				if (effects[effectIndex] is JObject effectObj && TryParseMainlineSemanticEffectStep(effectObj, $"{path}.effects[{effectIndex}]", beat, out var effect, out error))
				{
					step.Effects.Add(effect);
				}
			}
		}
		return true;
	}

	private bool TryParseMainlineSemanticEffectStep(JObject effectObj, string path, MainlineBeat beat, out MainlineSemanticEffectStep effect, out string error)
	{
		effect = null;
		error = null;
		if (!TryParseMainlineSemanticEffectStepKind(effectObj["kind"]?.ToString(), out var kind, out error))
		{
			error = path + ".kind " + error;
			return false;
		}
		string effectRef = NormalizeMainlineReferenceToken(effectObj["effectRef"]?.ToString());
		if (string.IsNullOrWhiteSpace(effectRef))
		{
			error = path + ".effectRef 不能为空";
			return false;
		}
		effect = new MainlineSemanticEffectStep
		{
			EffectRef = effectRef,
			Kind = kind,
			TargetNpcName = (effectObj["targetNpcName"]?.ToString()?.Trim() ?? string.Empty),
			Payload = CloneMainlinePayloadObject(effectObj["payload"], path + ".payload")
		};
		if (EffectKindRequiresTargetNpcName(kind) && string.IsNullOrWhiteSpace(effect.TargetNpcName))
		{
			error = path + ".targetNpcName 不能为空";
			effect = null;
			return false;
		}
		if (!string.IsNullOrWhiteSpace(effect.TargetNpcName))
		{
			if (!TryResolveBeatNpcReference(effect.TargetNpcName, beat, out var targetNpcId, out var resolvedNpcName, out error))
			{
				effect = null;
				return false;
			}
			effect.TargetNpcId = targetNpcId;
			effect.TargetNpcName = resolvedNpcName;
		}
		switch (kind)
		{
		case MainlineSemanticEffectStepKind.FavorChange:
			if (!IsMainlinePayloadInteger(effect.Payload, "amount"))
			{
				error = path + ".payload.amount 必须是整数";
				effect = null;
				return false;
			}
			break;
		case MainlineSemanticEffectStepKind.GrantItem:
		{
			if (!HasMainlinePayloadText(effect.Payload, "itemName"))
			{
				error = path + ".payload.itemName 不能为空";
				effect = null;
				return false;
			}
			if (!TryResolveSemanticPlanConcreteItemName(effect.Payload["itemName"]?.ToString(), out var resolvedItemName, out error))
			{
				effect = null;
				return false;
			}
			effect.Payload["itemName"] = resolvedItemName;
			if (effect.Payload["count"] != null && !IsMainlinePayloadInteger(effect.Payload, "count"))
			{
				error = path + ".payload.count 必须是整数";
				effect = null;
				return false;
			}
			break;
		}
		case MainlineSemanticEffectStepKind.GrantMoney:
			if (!IsMainlinePayloadInteger(effect.Payload, "money"))
			{
				error = path + ".payload.money 必须是整数";
				effect = null;
				return false;
			}
			break;
		}
		return true;
	}

	private bool TryResolveSemanticPlanConcreteItemName(string rawItemName, out string resolvedItemName, out string error)
	{
		resolvedItemName = string.Empty;
		error = null;
		string normalizedItemName = QuestElementPicker.NormalizeNarrativeEntityText(rawItemName);
		if (string.IsNullOrWhiteSpace(normalizedItemName))
		{
			error = "payload.itemName 不能为空";
			return false;
		}
		List<int> exactMatches = FindEquivalentItemIds(normalizedItemName, 0, allowFuzzy: false);
		if (exactMatches.Count == 1)
		{
			resolvedItemName = BuildCompiledConcreteItemOption(exactMatches[0]).Name;
			return true;
		}
		if (exactMatches.Count > 1)
		{
			error = "payload.itemName 在官方物品总池中存在多个完全匹配项: " + DescribeCompiledGlobalItemNames(exactMatches);
			return false;
		}
		List<int> fuzzyMatches = FindEquivalentItemIds(normalizedItemName, 0, allowFuzzy: true);
		if (fuzzyMatches.Count == 1)
		{
			resolvedItemName = BuildCompiledConcreteItemOption(fuzzyMatches[0]).Name;
			return true;
		}
		if (fuzzyMatches.Count > 1)
		{
			error = "payload.itemName 在官方物品总池中存在多个模糊匹配项: " + DescribeCompiledGlobalItemNames(fuzzyMatches);
			return false;
		}
		error = "payload.itemName 在官方物品总池中不存在: " + normalizedItemName;
		return false;
	}

	private static bool TryParseMainlineScriptBlockKind(string rawValue, out MainlineScriptBlockKind kind, out string error)
	{
		kind = MainlineScriptBlockKind.Dialogue;
		error = null;
		switch (NormalizeMainlineProtocolToken(rawValue))
		{
		case "encounter":
			kind = MainlineScriptBlockKind.Encounter;
			return true;
		case "dialogue":
			kind = MainlineScriptBlockKind.Dialogue;
			return true;
		case "choice":
			kind = MainlineScriptBlockKind.Choice;
			return true;
		case "fallout":
			kind = MainlineScriptBlockKind.Fallout;
			return true;
		case "delivery_prompt":
			kind = MainlineScriptBlockKind.DeliveryPrompt;
			return true;
		case "combat_prelude":
			kind = MainlineScriptBlockKind.CombatPrelude;
			return true;
		default:
			error = "不合法，只允许 encounter / dialogue / choice / fallout / delivery_prompt / combat_prelude，当前值: " + rawValue;
			return false;
		}
	}

	private static bool TryParseMainlineSemanticFlowStepKind(string rawValue, out MainlineSemanticFlowStepKind kind, out string error)
	{
		kind = MainlineSemanticFlowStepKind.PlayScriptBlock;
		error = null;
		switch (NormalizeMainlineProtocolToken(rawValue))
		{
		case "encounter_at_scene":
			kind = MainlineSemanticFlowStepKind.EncounterAtScene;
			return true;
		case "visit_scene_gate":
			kind = MainlineSemanticFlowStepKind.VisitSceneGate;
			return true;
		case "formal_talk_gate":
			kind = MainlineSemanticFlowStepKind.FormalTalkGate;
			return true;
		case "report_back_in_person":
			kind = MainlineSemanticFlowStepKind.ReportBackInPerson;
			return true;
		case "play_script_block":
			kind = MainlineSemanticFlowStepKind.PlayScriptBlock;
			return true;
		case "present_choice":
			kind = MainlineSemanticFlowStepKind.PresentChoice;
			return true;
		case "acquire_item_gate":
			kind = MainlineSemanticFlowStepKind.AcquireItemGate;
			return true;
		case "deliver_item_in_person":
			kind = MainlineSemanticFlowStepKind.DeliverItemInPerson;
			return true;
		case "combat_gate":
			kind = MainlineSemanticFlowStepKind.CombatGate;
			return true;
		case "scene_transition":
			kind = MainlineSemanticFlowStepKind.SceneTransition;
			return true;
		default:
			error = "不合法，只允许 encounter_at_scene / visit_scene_gate / formal_talk_gate / report_back_in_person / play_script_block / present_choice / acquire_item_gate / deliver_item_in_person / combat_gate / scene_transition，当前值: " + rawValue;
			return false;
		}
	}

	private static bool TryParseMainlineSemanticEffectStepKind(string rawValue, out MainlineSemanticEffectStepKind kind, out string error)
	{
		kind = MainlineSemanticEffectStepKind.FavorChange;
		error = null;
		switch (NormalizeMainlineProtocolToken(rawValue))
		{
		case "favor_change":
			kind = MainlineSemanticEffectStepKind.FavorChange;
			return true;
		case "grant_item":
			kind = MainlineSemanticEffectStepKind.GrantItem;
			return true;
		case "grant_money":
			kind = MainlineSemanticEffectStepKind.GrantMoney;
			return true;
		default:
			error = "不合法，只允许 favor_change / grant_item / grant_money，当前值: " + rawValue;
			return false;
		}
	}

	private ChapterTimeIntent ParseChapterTimeIntent(JToken timeIntentToken)
	{
		if (!(timeIntentToken is JObject timeIntentObj))
		{
			return new ChapterTimeIntent();
		}
		return new ChapterTimeIntent
		{
			TimeBand = (timeIntentObj["timeBand"]?.ToString() ?? string.Empty),
			Urgency = (timeIntentObj["urgency"]?.ToString() ?? string.Empty),
			MaxSpanMonths = (timeIntentObj["maxSpanMonths"]?.Value<int>() ?? 0),
			RequiresBeforeBreakthrough = (timeIntentObj["requiresBeforeBreakthrough"]?.Value<bool>() ?? false)
		};
	}

	private QuestProgram CompileMainlineBeatToQuestProgram(MainlineBeat beat, MainlineBeatGamePlan semanticPlan, QuestElementPicker.BeatExecutionContext executionContext)
	{
		if (beat == null)
		{
			throw new InvalidOperationException("当前主线 beat 不存在");
		}
		if (beat.StoryDraft?.ScriptBlocks == null || beat.StoryDraft.ScriptBlocks.Count == 0)
		{
			throw new InvalidOperationException("当前主线 beat 缺少 scriptBlocks");
		}
		if (semanticPlan?.FlowSteps == null || semanticPlan.FlowSteps.Count == 0)
		{
			throw new InvalidOperationException("当前主线 beat 缺少 flowSteps");
		}
		int anchorNpcId = ResolveCompiledBeatAnchorNpcId(beat, semanticPlan);
		if (anchorNpcId <= 0)
		{
			throw new InvalidOperationException("无法从当前 beat 推导锚点 NPC");
		}
		Dictionary<string, MainlineScriptBlock> scriptBlocks = beat.StoryDraft.ScriptBlocks.Where((MainlineScriptBlock mainlineScriptBlock) => mainlineScriptBlock != null).ToDictionary((MainlineScriptBlock mainlineScriptBlock) => mainlineScriptBlock.BlockRef ?? string.Empty, StringComparer.Ordinal);
		if (scriptBlocks.Count == 0)
		{
			throw new InvalidOperationException("当前主线 beat 的 scriptBlocks 为空");
		}
		List<QuestNode> nodes = new List<QuestNode>();
		Dictionary<string, QuestChoiceOption> choiceMap = new Dictionary<string, QuestChoiceOption>(StringComparer.Ordinal);
		string currentSceneId = string.Empty;
		QuestNode linearTail = null;
		CompiledChoiceBranchContext branchContext = null;
		HashSet<MainlineSemanticFlowStepKind> branchableKinds = new HashSet<MainlineSemanticFlowStepKind>
		{
			MainlineSemanticFlowStepKind.VisitSceneGate,
			MainlineSemanticFlowStepKind.FormalTalkGate,
			MainlineSemanticFlowStepKind.ReportBackInPerson,
			MainlineSemanticFlowStepKind.PlayScriptBlock,
			MainlineSemanticFlowStepKind.AcquireItemGate,
			MainlineSemanticFlowStepKind.DeliverItemInPerson,
			MainlineSemanticFlowStepKind.CombatGate,
			MainlineSemanticFlowStepKind.SceneTransition
		};
		foreach (MainlineSemanticFlowStep step in semanticPlan.FlowSteps)
		{
			if (step == null)
			{
				throw new InvalidOperationException("flowSteps 中存在空步骤");
			}
			bool isChoiceScoped = !string.IsNullOrWhiteSpace(step.BranchRef);
			if (isChoiceScoped && !branchableKinds.Contains(step.Kind))
			{
				throw new InvalidOperationException($"flow step {step.StepRef} 的 {step.Kind} 不允许携带 branchRef");
			}
			QuestNode node = null;
			switch (step.Kind)
			{
			case MainlineSemanticFlowStepKind.EncounterAtScene:
				currentSceneId = ResolveCompiledSceneId(step.SceneId, null, beat);
				node = BuildCompiledEncounterNode(step, beat, executionContext, currentSceneId, anchorNpcId);
				break;
			case MainlineSemanticFlowStepKind.VisitSceneGate:
				currentSceneId = ResolveCompiledSceneId(step.SceneId, null, beat, currentSceneId);
				node = BuildCompiledVisitSceneNode(step, executionContext, currentSceneId);
				break;
			case MainlineSemanticFlowStepKind.FormalTalkGate:
				currentSceneId = ResolveCompiledSceneId(step.SceneId, null, beat, currentSceneId);
				node = BuildCompiledFormalTalkNode(step, executionContext, currentSceneId, ResolveCompiledTargetNpcId(step.TargetNpcId, anchorNpcId));
				break;
			case MainlineSemanticFlowStepKind.ReportBackInPerson:
				currentSceneId = ResolveCompiledSceneId(step.SceneId, null, beat, currentSceneId);
				node = BuildCompiledReportBackInPersonNode(step, executionContext, currentSceneId, ResolveCompiledTargetNpcId(step.TargetNpcId, anchorNpcId));
				break;
			case MainlineSemanticFlowStepKind.PlayScriptBlock:
			{
				MainlineScriptBlock block2 = ResolveCompiledScriptBlock(step.SourceBlockRef, scriptBlocks);
				if (block2.Kind == MainlineScriptBlockKind.Choice)
				{
					throw new InvalidOperationException("flow step " + step.StepRef + " 不能用 play_script_block 编译 choice block " + block2.BlockRef);
				}
				currentSceneId = ResolveCompiledSceneId(step.SceneId, block2.SceneId, beat, currentSceneId);
				node = BuildCompiledNarrativeNode(step, block2);
				break;
			}
			case MainlineSemanticFlowStepKind.PresentChoice:
			{
				MainlineScriptBlock block = ResolveCompiledScriptBlock(step.SourceBlockRef, scriptBlocks);
				if (block.Kind != MainlineScriptBlockKind.Choice)
				{
					throw new InvalidOperationException($"flow step {step.StepRef} 只能引用 kind=choice 的 script block，当前是 {block.Kind}");
				}
				currentSceneId = ResolveCompiledSceneId(step.SceneId, block.SceneId, beat, currentSceneId);
				node = BuildCompiledChoiceNode(step, block, choiceMap);
				break;
			}
			case MainlineSemanticFlowStepKind.AcquireItemGate:
				node = BuildCompiledAcquireItemNode(step, executionContext);
				break;
			case MainlineSemanticFlowStepKind.DeliverItemInPerson:
				currentSceneId = ResolveCompiledSceneId(step.SceneId, null, beat, currentSceneId);
				node = BuildCompiledDeliverItemInPersonNode(step, executionContext, currentSceneId, ResolveCompiledTargetNpcId(step.TargetNpcId, anchorNpcId));
				break;
			case MainlineSemanticFlowStepKind.CombatGate:
				currentSceneId = ResolveCompiledSceneId(step.SceneId, null, beat, currentSceneId);
				node = BuildCompiledCombatNode(step, currentSceneId, ResolveCompiledTargetNpcId(step.TargetNpcId, anchorNpcId));
				break;
			case MainlineSemanticFlowStepKind.SceneTransition:
				currentSceneId = ResolveCompiledSceneId(step.SceneId, null, beat, currentSceneId);
				node = BuildCompiledSceneTransitionNode(step, executionContext, currentSceneId);
				break;
			default:
				throw new InvalidOperationException($"当前剧情编排超出已支持模板: {step.Kind}");
			}
			if (step.Kind == MainlineSemanticFlowStepKind.PresentChoice)
			{
				if (isChoiceScoped)
				{
					throw new InvalidOperationException("present_choice 步骤 " + step.StepRef + " 不允许携带 branchRef");
				}
				if (branchContext != null)
				{
					throw new InvalidOperationException("当前编译器暂不支持未汇合前再次进入新的 present_choice: " + step.StepRef);
				}
				if (step.Effects != null && step.Effects.Count > 0)
				{
					throw new InvalidOperationException("present_choice 步骤 " + step.StepRef + " 不允许内嵌 effects");
				}
				if (node == null)
				{
					throw new InvalidOperationException("present_choice 步骤 " + step.StepRef + " 没有产出 choice 节点");
				}
				if (linearTail != null)
				{
					ConnectCompiledNode(linearTail, node);
				}
				nodes.Add(node);
				branchContext = new CompiledChoiceBranchContext();
				foreach (QuestChoiceOption choice in node.Narrative?.Choices ?? new List<QuestChoiceOption>())
				{
					if (string.IsNullOrWhiteSpace(choice?.ChoiceId))
					{
						throw new InvalidOperationException("present_choice 步骤 " + step.StepRef + " 产出了空 choiceRef");
					}
					branchContext.BranchRefs.Add(choice.ChoiceId);
				}
				if (branchContext.BranchRefs.Count == 0)
				{
					throw new InvalidOperationException("present_choice 步骤 " + step.StepRef + " 缺少可用 choice");
				}
				linearTail = null;
				continue;
			}
			if (isChoiceScoped)
			{
				if (branchContext == null)
				{
					throw new InvalidOperationException("flow step " + step.StepRef + " 在没有前置 present_choice 的情况下使用了 branchRef");
				}
				if (node != null)
				{
					AttachCompiledStepEffects(node, step, executionContext, anchorNpcId);
					nodes.Add(node);
				}
				string branchRef = step.BranchRef.Trim();
				if (!branchContext.BranchRefs.Contains(branchRef))
				{
					throw new InvalidOperationException("flow step " + step.StepRef + " 引用了不存在的 branchRef: " + branchRef);
				}
				if (!choiceMap.TryGetValue(branchRef, out var choice2))
				{
					throw new InvalidOperationException("当前编译结果里不存在 branchRef=" + branchRef + " 的选项");
				}
				if (node != null)
				{
					if (branchContext.BranchTails.TryGetValue(branchRef, out var branchTail))
					{
						ConnectCompiledNode(branchTail, node);
					}
					else
					{
						choice2.NextNodeId = node.NodeId;
					}
					branchContext.BranchTails[branchRef] = node;
				}
				continue;
			}
			if (branchContext != null)
			{
				throw new InvalidOperationException("present_choice 之后不允许出现公共步骤: " + step.StepRef);
			}
			if (node != null)
			{
				AttachCompiledStepEffects(node, step, executionContext, anchorNpcId);
				nodes.Add(node);
				if (linearTail != null)
				{
					ConnectCompiledNode(linearTail, node);
				}
				linearTail = node;
			}
		}
		if (nodes.Count > 0 && nodes.All((QuestNode questNode2) => questNode2.Type == QuestNodeType.Narrative || questNode2.Type == QuestNodeType.Choice))
		{
			string syntheticSceneId = currentSceneId;
			if (string.IsNullOrWhiteSpace(syntheticSceneId))
			{
				syntheticSceneId = beat?.KeyLocationIds?.FirstOrDefault((string id) => !string.IsNullOrWhiteSpace(id))?.Trim();
			}
			if (string.IsNullOrWhiteSpace(syntheticSceneId))
			{
				throw new InvalidOperationException("当前纯叙事剧情编排无法推导 sceneId，编译器无法合成前置节点");
			}
			string syntheticSceneName = ResolvePlayerFacingQuestSceneName(syntheticSceneId, executionContext);
			string syntheticNpcName = ResolvePlayerFacingQuestNpcName(anchorNpcId);
			QuestNode questNode = new QuestNode();
			questNode.NodeId = BuildCompiledNodeId("synthetic_encounter");
			questNode.Type = QuestNodeType.Objective;
			questNode.Title = "前往" + syntheticSceneName;
			questNode.Summary = "在" + syntheticSceneName + "与" + syntheticNpcName + "会面。";
			questNode.ActivationPredicates = BuildCompiledEncounterActivationPredicates(syntheticSceneId, anchorNpcId);
			questNode.CompletionPredicates = new List<QuestPredicateInvocation> { CreateSceneIsPredicate(syntheticSceneId) };
			questNode.OnActivated = new List<QuestCommandInvocation> { CreateNpcSetActionCommand(anchorNpcId, ResolveQuestProgramPresenceActionId(QuestNpcPresenceMode.Encounter, syntheticSceneId)) };
			questNode.WorldControl = BuildEncounterWorldControl(anchorNpcId, syntheticSceneId);
			QuestNode syntheticEncounterNode = questNode;
			ConnectCompiledNode(syntheticEncounterNode, nodes[0]);
			nodes.Insert(0, syntheticEncounterNode);
			if (linearTail == null)
			{
				linearTail = syntheticEncounterNode;
			}
		}
		if (nodes.Count == 0)
		{
			throw new InvalidOperationException("当前剧情编排没有产出任何可执行节点");
		}
		if (branchContext != null)
		{
			foreach (string choiceId in branchContext.BranchRefs)
			{
				if (!choiceMap.TryGetValue(choiceId, out var _))
				{
					throw new InvalidOperationException("当前编译结果里不存在 choiceRef=" + choiceId + " 的选项");
				}
				if (!branchContext.BranchTails.TryGetValue(choiceId, out var branchTail2))
				{
					throw new InvalidOperationException("choiceRef=" + choiceId + " 缺少独立后续节点，无法在当前拍内结束");
				}
				QuestNode branchTerminalNode = new QuestNode
				{
					NodeId = BuildCompiledNodeId("terminal_" + choiceId),
					Type = QuestNodeType.Terminal,
					Title = "当前事件收束",
					Summary = ResolveCompiledBeatTerminalSummary(beat, choiceId)
				};
				nodes.Add(branchTerminalNode);
				ConnectCompiledNode(branchTail2, branchTerminalNode);
			}
		}
		else
		{
			QuestNode terminalNode = new QuestNode
			{
				NodeId = BuildCompiledNodeId("terminal"),
				Type = QuestNodeType.Terminal,
				Title = "当前事件收束",
				Summary = ResolveCompiledBeatTerminalSummary(beat)
			};
			nodes.Add(terminalNode);
			if (linearTail != null)
			{
				ConnectCompiledNode(linearTail, terminalNode);
			}
		}
		QuestProgram program = new QuestProgram
		{
			ProgramId = Guid.NewGuid().ToString("N"),
			Title = (string.IsNullOrWhiteSpace(beat.Title) ? "当前主线" : beat.Title.Trim()),
			Summary = (beat.Summary ?? string.Empty),
			Nodes = nodes
		};
		WorldContract derivedContract = (beat.WorldContract = DeriveCompiledWorldContract(program, beat, semanticPlan));
		program.WorldContract = new QuestWorldContract
		{
			AllowedNpcIds = new List<int>(derivedContract.AllowedNpcIds),
			AllowedSceneIds = new List<string>(derivedContract.AllowedSceneIds),
			SelectedPredicates = new List<string>(derivedContract.SelectedPredicates),
			SelectedCommands = new List<string>(derivedContract.SelectedCommands)
		};
		string json = JsonConvert.SerializeObject(program, _jsonSettings);
		if (!QuestProgramParser.TryParseAndValidate(json, out var validatedProgram, out var validationError))
		{
			throw new InvalidOperationException(validationError);
		}
		beat.WorldContract = new WorldContract
		{
			ContractId = Guid.NewGuid().ToString("N"),
			Summary = (beat.Summary ?? string.Empty),
			AllowedNpcIds = new List<int>(validatedProgram.WorldContract.AllowedNpcIds ?? new List<int>()),
			AllowedSceneIds = new List<string>(validatedProgram.WorldContract.AllowedSceneIds ?? new List<string>()),
			SelectedPredicates = new List<string>(validatedProgram.WorldContract.SelectedPredicates ?? new List<string>()),
			SelectedCommands = new List<string>(validatedProgram.WorldContract.SelectedCommands ?? new List<string>())
		};
		return validatedProgram;
	}

	private static int ResolveCompiledBeatAnchorNpcId(MainlineBeat beat, MainlineBeatGamePlan semanticPlan)
	{
		if (beat?.KeyNpcIds != null && beat.KeyNpcIds.Count > 0)
		{
			return beat.KeyNpcIds[0];
		}
		if (semanticPlan?.UsedNpcIds != null && semanticPlan.UsedNpcIds.Count > 0)
		{
			return semanticPlan.UsedNpcIds[0];
		}
		return 0;
	}

	private static string ResolveCompiledBeatTerminalSummary(MainlineBeat beat, string branchRef = null)
	{
		IEnumerable<MainlineScriptBlock> blocks = beat?.StoryDraft?.ScriptBlocks ?? new List<MainlineScriptBlock>();
		MainlineScriptBlock summaryBlock = (string.IsNullOrWhiteSpace(branchRef) ? blocks.LastOrDefault((MainlineScriptBlock block) => block != null && string.IsNullOrWhiteSpace(block.BranchRef) && !string.IsNullOrWhiteSpace(block.ScenePurpose)) : blocks.LastOrDefault((MainlineScriptBlock block) => block != null && string.Equals(block.BranchRef, branchRef, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(block.ScenePurpose)));
		if (!string.IsNullOrWhiteSpace(summaryBlock?.ScenePurpose))
		{
			return summaryBlock.ScenePurpose.Trim();
		}
		return beat?.Summary?.Trim() ?? string.Empty;
	}

	private static int ResolveCompiledTargetNpcId(int explicitNpcId, int anchorNpcId)
	{
		return (explicitNpcId > 0) ? explicitNpcId : anchorNpcId;
	}

	private static MainlineScriptBlock ResolveCompiledScriptBlock(string sourceBlockRef, Dictionary<string, MainlineScriptBlock> scriptBlocks)
	{
		string normalizedBlockRef = sourceBlockRef?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(normalizedBlockRef))
		{
			throw new InvalidOperationException("flow step 缺少 sourceBlockRef");
		}
		if (!scriptBlocks.TryGetValue(normalizedBlockRef, out var block) || block == null)
		{
			throw new InvalidOperationException("找不到 sourceBlockRef 对应的剧情脚本: " + normalizedBlockRef);
		}
		return block;
	}

	private static string ResolveCompiledSceneId(string explicitSceneId, string blockSceneId, MainlineBeat beat, string currentSceneId = null)
	{
		string sceneId = ((!string.IsNullOrWhiteSpace(explicitSceneId)) ? explicitSceneId.Trim() : ((!string.IsNullOrWhiteSpace(blockSceneId)) ? blockSceneId.Trim() : ((!string.IsNullOrWhiteSpace(currentSceneId)) ? currentSceneId.Trim() : beat?.KeyLocationIds?.FirstOrDefault((string id) => !string.IsNullOrWhiteSpace(id))?.Trim())));
		if (string.IsNullOrWhiteSpace(sceneId))
		{
			throw new InvalidOperationException("当前剧情编排缺少 sceneId，编译器无法确定落地点");
		}
		return sceneId;
	}

	private QuestNodeWorldControl BuildCompiledNodeWorldControl(MainlineSemanticFlowStep step, string sceneId, int anchorNpcId)
	{
		if (step == null)
		{
			return null;
		}
		int targetNpcId = ResolveCompiledTargetNpcId(step.TargetNpcId, anchorNpcId);
		return step.Kind switch
		{
			MainlineSemanticFlowStepKind.EncounterAtScene => BuildEncounterWorldControl(targetNpcId, sceneId),
			MainlineSemanticFlowStepKind.FormalTalkGate => BuildFormalTalkWorldControl(targetNpcId, sceneId),
			MainlineSemanticFlowStepKind.ReportBackInPerson => BuildReportBackWorldControl(targetNpcId, sceneId),
			MainlineSemanticFlowStepKind.DeliverItemInPerson => BuildDeliverWorldControl(targetNpcId, sceneId),
			MainlineSemanticFlowStepKind.CombatGate => BuildCombatWorldControl(targetNpcId, sceneId),
			_ => null,
		};
	}

	private static QuestNodeWorldControl BuildEncounterWorldControl(int npcId, string sceneId)
	{
		return BuildQuestNodeWorldControl(sceneId, npcId, QuestNpcPresenceMode.Encounter);
	}

	private static QuestNodeWorldControl BuildFormalTalkWorldControl(int npcId, string sceneId)
	{
		return BuildQuestNodeWorldControl(sceneId, npcId, QuestNpcPresenceMode.FormalTalk);
	}

	private static QuestNodeWorldControl BuildReportBackWorldControl(int npcId, string sceneId)
	{
		return BuildQuestNodeWorldControl(sceneId, npcId, QuestNpcPresenceMode.ReportBack);
	}

	private static QuestNodeWorldControl BuildDeliverWorldControl(int npcId, string sceneId)
	{
		return BuildQuestNodeWorldControl(sceneId, npcId, QuestNpcPresenceMode.Delivery);
	}

	private static QuestNodeWorldControl BuildCombatWorldControl(int npcId, string sceneId)
	{
		return BuildQuestNodeWorldControl(sceneId, npcId, QuestNpcPresenceMode.Combat);
	}

	private static QuestNodeWorldControl BuildQuestNodeWorldControl(string sceneId, int npcId, QuestNpcPresenceMode mode)
	{
		if (npcId <= 0)
		{
			throw new InvalidOperationException($"节点世界控制缺少 npcId: {mode}");
		}
		if (string.IsNullOrWhiteSpace(sceneId))
		{
			throw new InvalidOperationException($"节点世界控制缺少 sceneId: {mode}");
		}
		return new QuestNodeWorldControl
		{
			SceneId = sceneId.Trim(),
			NpcRequirements = new List<QuestNpcPresenceRequirement>
			{
				new QuestNpcPresenceRequirement
				{
					NpcId = npcId,
					Mode = mode
				}
			}
		};
	}

	private QuestNode BuildCompiledEncounterNode(MainlineSemanticFlowStep step, MainlineBeat beat, QuestElementPicker.BeatExecutionContext executionContext, string sceneId, int anchorNpcId)
	{
		int targetNpcId = ResolveCompiledTargetNpcId(step.TargetNpcId, anchorNpcId);
		string sceneName = ResolvePlayerFacingQuestSceneName(sceneId, executionContext, step.SceneName);
		string npcName = ResolvePlayerFacingQuestNpcName(targetNpcId);
		QuestNode questNode = new QuestNode();
		questNode.NodeId = BuildCompiledNodeId(step.StepRef);
		questNode.Type = QuestNodeType.Objective;
		questNode.Title = "前往" + sceneName;
		questNode.Summary = "在" + sceneName + "与" + npcName + "会面。";
		questNode.ActivationPredicates = BuildCompiledEncounterActivationPredicates(sceneId, targetNpcId);
		questNode.CompletionPredicates = new List<QuestPredicateInvocation> { CreateSceneIsPredicate(sceneId) };
		questNode.OnActivated = new List<QuestCommandInvocation> { CreateNpcSetActionCommand(targetNpcId, ResolveQuestProgramPresenceActionId(QuestNpcPresenceMode.Encounter, sceneId)) };
		questNode.WorldControl = BuildCompiledNodeWorldControl(step, sceneId, anchorNpcId);
		return questNode;
	}

	private QuestNode BuildCompiledFormalTalkNode(MainlineSemanticFlowStep step, QuestElementPicker.BeatExecutionContext executionContext, string sceneId, int npcId)
	{
		string sceneName = ResolvePlayerFacingQuestSceneName(sceneId, executionContext, step.SceneName);
		string npcName = ResolvePlayerFacingQuestNpcName(npcId);
		QuestNode questNode = new QuestNode();
		questNode.NodeId = BuildCompiledNodeId(step.StepRef);
		questNode.Type = QuestNodeType.Objective;
		questNode.Title = "与" + npcName + "交谈";
		questNode.Summary = "在" + sceneName + "正式与" + npcName + "交谈。";
		questNode.ActivationPredicates = BuildCompiledEncounterActivationPredicates(sceneId, npcId);
		questNode.CompletionPredicates = new List<QuestPredicateInvocation> { CreateNpcTalkedPredicate(npcId) };
		questNode.OnActivated = new List<QuestCommandInvocation> { CreateNpcSetActionCommand(npcId, ResolveQuestProgramPresenceActionId(QuestNpcPresenceMode.FormalTalk, sceneId)) };
		questNode.WorldControl = BuildFormalTalkWorldControl(npcId, sceneId);
		return questNode;
	}

	private QuestNode BuildCompiledVisitSceneNode(MainlineSemanticFlowStep step, QuestElementPicker.BeatExecutionContext executionContext, string sceneId)
	{
		string sceneName = ResolvePlayerFacingQuestSceneName(sceneId, executionContext, step.SceneName);
		return new QuestNode
		{
			NodeId = BuildCompiledNodeId(step.StepRef),
			Type = QuestNodeType.Objective,
			Title = "前往" + sceneName + "查探",
			Summary = "前往" + sceneName + "踩点并确认当前线索。",
			CompletionPredicates = new List<QuestPredicateInvocation> { CreateSceneIsPredicate(sceneId) },
			WorldControl = null
		};
	}

	private QuestNode BuildCompiledReportBackInPersonNode(MainlineSemanticFlowStep step, QuestElementPicker.BeatExecutionContext executionContext, string sceneId, int npcId)
	{
		string sceneName = ResolvePlayerFacingQuestSceneName(sceneId, executionContext, step.SceneName);
		string npcName = ResolvePlayerFacingQuestNpcName(npcId);
		QuestNode questNode = new QuestNode();
		questNode.NodeId = BuildCompiledNodeId(step.StepRef);
		questNode.Type = QuestNodeType.Objective;
		questNode.Title = "向" + npcName + "回报";
		questNode.Summary = "回到" + sceneName + "当面向" + npcName + "回报调查结果。";
		questNode.ActivationPredicates = BuildCompiledEncounterActivationPredicates(sceneId, npcId);
		questNode.CompletionPredicates = new List<QuestPredicateInvocation> { CreateLogicAllPredicate(CreateSceneIsPredicate(sceneId), CreateNpcTalkedPredicate(npcId)) };
		questNode.OnActivated = new List<QuestCommandInvocation> { CreateNpcSetActionCommand(npcId, ResolveQuestProgramPresenceActionId(QuestNpcPresenceMode.ReportBack, sceneId)) };
		questNode.WorldControl = BuildReportBackWorldControl(npcId, sceneId);
		return questNode;
	}

	private QuestNode BuildCompiledNarrativeNode(MainlineSemanticFlowStep step, MainlineScriptBlock block)
	{
		return new QuestNode
		{
			NodeId = BuildCompiledNodeId(step.StepRef),
			Type = QuestNodeType.Narrative,
			Title = (string.IsNullOrWhiteSpace(block.ScenePurpose) ? block.BlockRef : block.ScenePurpose),
			Summary = (block.ScenePurpose ?? string.Empty),
			Narrative = new QuestNarrativeBlock
			{
				NarrativeId = "narrative_" + NormalizeCompiledToken(step.StepRef),
				Lines = CloneStoryLines(block.Lines)
			}
		};
	}

	private QuestNode BuildCompiledChoiceNode(MainlineSemanticFlowStep step, MainlineScriptBlock block, Dictionary<string, QuestChoiceOption> choiceMap)
	{
		QuestNode node = new QuestNode
		{
			NodeId = BuildCompiledNodeId(step.StepRef),
			Type = QuestNodeType.Choice,
			Title = (string.IsNullOrWhiteSpace(block.ScenePurpose) ? block.BlockRef : block.ScenePurpose),
			Summary = (block.ScenePurpose ?? string.Empty),
			Narrative = new QuestNarrativeBlock
			{
				NarrativeId = "narrative_" + NormalizeCompiledToken(step.StepRef),
				Lines = CloneStoryLines(block.Lines)
			}
		};
		foreach (MainlineDialogueChoiceDraft choice in block.Choices ?? new List<MainlineDialogueChoiceDraft>())
		{
			QuestChoiceOption option = new QuestChoiceOption
			{
				ChoiceId = choice.ChoiceRef,
				Text = choice.Text
			};
			node.Narrative.Choices.Add(option);
			choiceMap[choice.ChoiceRef] = option;
		}
		return node;
	}

	private QuestNode BuildCompiledAcquireItemNode(MainlineSemanticFlowStep step, QuestElementPicker.BeatExecutionContext executionContext)
	{
		string itemName = GetRequiredCompiledPayloadString(step.Payload, "itemName", "flow step " + step.StepRef);
		QuestElementPicker.ConcreteItemOption item = ResolveCompiledConcreteItem(itemName, executionContext);
		int count = GetCompiledPayloadInt(step.Payload, "count", "flow step " + step.StepRef, 1);
		return new QuestNode
		{
			NodeId = BuildCompiledNodeId(step.StepRef),
			Type = QuestNodeType.Objective,
			Title = "准备" + item.Name,
			Summary = $"准备好{count}份{item.Name}。",
			CompletionPredicates = new List<QuestPredicateInvocation> { CreatePlayerHasItemPredicate(item.ItemId, count) }
		};
	}

	private QuestNode BuildCompiledDeliverItemInPersonNode(MainlineSemanticFlowStep step, QuestElementPicker.BeatExecutionContext executionContext, string sceneId, int npcId)
	{
		string itemName = GetRequiredCompiledPayloadString(step.Payload, "itemName", "flow step " + step.StepRef);
		QuestElementPicker.ConcreteItemOption item = ResolveCompiledConcreteItem(itemName, executionContext);
		int count = GetCompiledPayloadInt(step.Payload, "count", "flow step " + step.StepRef, 1);
		string npcName = ResolvePlayerFacingQuestNpcName(npcId);
		string sceneName = ResolvePlayerFacingQuestSceneName(sceneId, executionContext, step.SceneName);
		QuestNode questNode = new QuestNode();
		questNode.NodeId = BuildCompiledNodeId(step.StepRef);
		questNode.Type = QuestNodeType.Objective;
		questNode.Title = "向" + npcName + "交付" + item.Name;
		questNode.Summary = $"带着{count}份{item.Name}在{sceneName}与{npcName}当面交付。";
		questNode.ActivationPredicates = BuildCompiledEncounterActivationPredicates(sceneId, npcId);
		questNode.CompletionPredicates = new List<QuestPredicateInvocation> { CreateLogicAllPredicate(CreatePlayerHasItemPredicate(item.ItemId, count), CreateNpcTalkedPredicate(npcId)) };
		questNode.OnCompleted = new List<QuestCommandInvocation> { CreateQuestCommand("player.add_item", ("itemId", item.ItemId.ToString()), ("count", (-count).ToString())) };
		questNode.WorldControl = BuildDeliverWorldControl(npcId, sceneId);
		return questNode;
	}

	private QuestNode BuildCompiledCombatNode(MainlineSemanticFlowStep step, string sceneId, int npcId)
	{
		QuestNode questNode = new QuestNode();
		questNode.NodeId = BuildCompiledNodeId(step.StepRef);
		questNode.Type = QuestNodeType.Combat;
		questNode.Title = "与" + ResolvePlayerFacingQuestNpcName(npcId) + "冲突";
		questNode.Summary = "当前剧情在此进入正面冲突。";
		questNode.ActivationPredicates = BuildCompiledEncounterActivationPredicates(sceneId, npcId);
		questNode.CompletionPredicates = new List<QuestPredicateInvocation> { CreateCombatVictoryPredicate(npcId) };
		questNode.OnActivated = new List<QuestCommandInvocation> { CreateQuestCommand("combat.start_standard", ("npcId", npcId.ToString()), ("fightMusic", "default")) };
		questNode.WorldControl = BuildCombatWorldControl(npcId, sceneId);
		return questNode;
	}

	private QuestNode BuildCompiledSceneTransitionNode(MainlineSemanticFlowStep step, QuestElementPicker.BeatExecutionContext executionContext, string sceneId)
	{
		if (string.IsNullOrWhiteSpace(sceneId))
		{
			throw new InvalidOperationException("scene_transition 步骤 " + step.StepRef + " 缺少 sceneName");
		}
		string sceneName = ResolvePlayerFacingQuestSceneName(sceneId, executionContext, step.SceneName);
		return new QuestNode
		{
			NodeId = BuildCompiledNodeId(step.StepRef),
			Type = QuestNodeType.Objective,
			Title = "前往" + sceneName,
			Summary = "继续推进当前剧情并前往下一处地点。",
			CompletionPredicates = new List<QuestPredicateInvocation> { CreateSceneIsPredicate(sceneId) }
		};
	}

	private void AttachCompiledStepEffects(QuestNode node, MainlineSemanticFlowStep step, QuestElementPicker.BeatExecutionContext executionContext, int anchorNpcId)
	{
		if (node == null)
		{
			throw new InvalidOperationException("flow step " + (step?.StepRef ?? "<null>") + " 缺少可附着 effects 的节点");
		}
		if (step?.Effects == null || step.Effects.Count == 0)
		{
			return;
		}
		if (node.OnCompleted == null)
		{
			node.OnCompleted = new List<QuestCommandInvocation>();
		}
		foreach (MainlineSemanticEffectStep effect in step.Effects)
		{
			node.OnCompleted.AddRange(BuildCompiledEffectCommands(effect, executionContext, anchorNpcId));
		}
	}

	private List<QuestCommandInvocation> BuildCompiledEffectCommands(MainlineSemanticEffectStep effect, QuestElementPicker.BeatExecutionContext executionContext, int anchorNpcId)
	{
		if (effect == null)
		{
			throw new InvalidOperationException("flowStep.effects 中存在空效果");
		}
		switch (effect.Kind)
		{
		case MainlineSemanticEffectStepKind.FavorChange:
		{
			List<QuestCommandInvocation> list = new List<QuestCommandInvocation>();
			list.Add(CreateQuestCommand("npc.add_favor", ("npcId", ResolveCompiledTargetNpcId(effect.TargetNpcId, anchorNpcId).ToString()), ("amount", GetRequiredCompiledPayloadInt(effect.Payload, "amount", "effect " + effect.EffectRef).ToString())));
			return list;
		}
		case MainlineSemanticEffectStepKind.GrantItem:
		{
			string itemName = GetRequiredCompiledPayloadString(effect.Payload, "itemName", "effect " + effect.EffectRef);
			QuestElementPicker.ConcreteItemOption item = ResolveCompiledConcreteItem(itemName, executionContext);
			int count = GetCompiledPayloadInt(effect.Payload, "count", "effect " + effect.EffectRef, 1);
			List<QuestCommandInvocation> list = new List<QuestCommandInvocation>();
			list.Add(CreateQuestCommand("player.add_item", ("itemId", item.ItemId.ToString()), ("count", count.ToString())));
			return list;
		}
		case MainlineSemanticEffectStepKind.GrantMoney:
		{
			List<QuestCommandInvocation> list = new List<QuestCommandInvocation>();
			list.Add(CreateQuestCommand("player.add_money", ("amount", GetRequiredCompiledPayloadInt(effect.Payload, "money", "effect " + effect.EffectRef).ToString())));
			return list;
		}
		default:
			throw new InvalidOperationException($"当前剧情编排超出已支持效果类型: {effect.Kind}");
		}
	}

	private static string GetRequiredCompiledPayloadString(JObject payload, string key, string owner)
	{
		string value = payload?[key]?.ToString()?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new InvalidOperationException(owner + " 缺少 payload." + key);
		}
		return value;
	}

	private static int GetRequiredCompiledPayloadInt(JObject payload, string key, string owner)
	{
		JToken token = payload?[key];
		if (token == null)
		{
			throw new InvalidOperationException(owner + " 缺少 payload." + key);
		}
		if (token.Type != JTokenType.Integer)
		{
			throw new InvalidOperationException(owner + ".payload." + key + " 必须是整数");
		}
		return token.Value<int>();
	}

	private static int GetCompiledPayloadInt(JObject payload, string key, string owner, int defaultValue)
	{
		JToken token = payload?[key];
		if (token == null)
		{
			return defaultValue;
		}
		if (token.Type != JTokenType.Integer)
		{
			throw new InvalidOperationException(owner + ".payload." + key + " 必须是整数");
		}
		return token.Value<int>();
	}

	private QuestElementPicker.ConcreteItemOption ResolveCompiledConcreteItem(string itemName, QuestElementPicker.BeatExecutionContext executionContext)
	{
		List<QuestElementPicker.ConcreteItemOption> items = executionContext?.ConcreteItems ?? new List<QuestElementPicker.ConcreteItemOption>();
		string normalizedItemName = QuestElementPicker.NormalizeNarrativeEntityText(itemName);
		if (string.IsNullOrWhiteSpace(normalizedItemName))
		{
			throw new InvalidOperationException("当前语义步骤缺少 itemName，编译器无法映射具体物品");
		}
		List<QuestElementPicker.ConcreteItemOption> exactMatches = items.Where((QuestElementPicker.ConcreteItemOption item) => item != null && QuestElementPicker.NarrativeEntityTextEquals(item.Name, normalizedItemName)).ToList();
		if (exactMatches.Count == 1)
		{
			return exactMatches[0];
		}
		if (exactMatches.Count > 1)
		{
			throw new InvalidOperationException("当前 beat 上下文中存在多个同名具体物品，无法唯一映射: " + normalizedItemName);
		}
		List<QuestElementPicker.ConcreteItemOption> fuzzyMatches = items.Where((QuestElementPicker.ConcreteItemOption item) => item != null && (QuestElementPicker.NarrativeEntityTextContains(item.Name, normalizedItemName) || QuestElementPicker.NarrativeEntityTextContains(normalizedItemName, item.Name))).ToList();
		if (fuzzyMatches.Count == 1)
		{
			return fuzzyMatches[0];
		}
		if (fuzzyMatches.Count > 1)
		{
			string matchedNames = string.Join("、", (from item in fuzzyMatches
				select item?.Name?.Trim() into name
				where !string.IsNullOrWhiteSpace(name)
				select name).Distinct(StringComparer.OrdinalIgnoreCase));
			throw new InvalidOperationException("当前 beat 上下文中存在多个与 " + normalizedItemName + " 模糊匹配的具体物品: " + matchedNames);
		}
		List<int> globalExactMatches = FindEquivalentItemIds(normalizedItemName, 0, allowFuzzy: false);
		if (globalExactMatches.Count == 1)
		{
			return BuildCompiledConcreteItemOption(globalExactMatches[0]);
		}
		if (globalExactMatches.Count > 1)
		{
			throw new InvalidOperationException("官方物品总池中存在多个与 " + normalizedItemName + " 完全匹配的具体物品: " + DescribeCompiledGlobalItemNames(globalExactMatches));
		}
		List<int> globalFuzzyMatches = FindEquivalentItemIds(normalizedItemName, 0, allowFuzzy: true);
		if (globalFuzzyMatches.Count == 1)
		{
			return BuildCompiledConcreteItemOption(globalFuzzyMatches[0]);
		}
		if (globalFuzzyMatches.Count > 1)
		{
			throw new InvalidOperationException("官方物品总池中存在多个与 " + normalizedItemName + " 模糊匹配的具体物品: " + DescribeCompiledGlobalItemNames(globalFuzzyMatches));
		}
		string availableNames = string.Join("、", (from item in items
			where item != null && !string.IsNullOrWhiteSpace(item.Name)
			select item.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
		throw new InvalidOperationException("当前 beat 上下文与官方物品总池中都找不到名为 " + normalizedItemName + " 的具体物品；当前 beat 可用物品: " + availableNames);
	}

	private QuestElementPicker.ConcreteItemOption BuildCompiledConcreteItemOption(int itemId)
	{
		if (!_ItemJsonData.DataDict.TryGetValue(itemId, out var item) || item == null || string.IsNullOrWhiteSpace(item.name))
		{
			throw new InvalidOperationException($"官方物品总池中无法构造 itemId={itemId} 的具体物品");
		}
		return new QuestElementPicker.ConcreteItemOption
		{
			FunctionTag = string.Empty,
			ItemId = itemId,
			Name = item.name.Trim(),
			Quality = item.quality,
			Effect = (string.IsNullOrWhiteSpace(item.desc2) ? (item.desc ?? string.Empty) : item.desc2)
		};
	}

	private string DescribeCompiledGlobalItemNames(IEnumerable<int> itemIds)
	{
		_ItemJsonData value;
		return string.Join("、", (from id in itemIds ?? Enumerable.Empty<int>()
			select (_ItemJsonData.DataDict.TryGetValue(id, out value) && value != null && !string.IsNullOrWhiteSpace(value.name)) ? value.name.Trim() : null into name
			where !string.IsNullOrWhiteSpace(name)
			select name).Distinct(StringComparer.OrdinalIgnoreCase));
	}

	private WorldContract DeriveCompiledWorldContract(QuestProgram program, MainlineBeat beat, MainlineBeatGamePlan semanticPlan)
	{
		HashSet<int> allowedNpcIds = new HashSet<int>((beat?.KeyNpcIds ?? new List<int>()).Concat(semanticPlan?.UsedNpcIds ?? new List<int>()));
		HashSet<string> allowedSceneIds = new HashSet<string>((beat?.KeyLocationIds ?? new List<string>()).Concat(semanticPlan?.UsedSceneIds ?? new List<string>()), StringComparer.OrdinalIgnoreCase);
		HashSet<string> predicateOpcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> commandOpcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (QuestNode node in program?.Nodes ?? new List<QuestNode>())
		{
			if (node?.WorldControl != null)
			{
				if (!string.IsNullOrWhiteSpace(node.WorldControl.SceneId))
				{
					allowedSceneIds.Add(node.WorldControl.SceneId.Trim());
				}
				foreach (QuestNpcPresenceRequirement requirement in node.WorldControl.NpcRequirements ?? new List<QuestNpcPresenceRequirement>())
				{
					if (requirement != null && requirement.NpcId > 0)
					{
						allowedNpcIds.Add(requirement.NpcId);
					}
				}
			}
			CollectCompiledPredicates(node?.ActivationPredicates, predicateOpcodes, allowedNpcIds, allowedSceneIds);
			CollectCompiledPredicates(node?.CompletionPredicates, predicateOpcodes, allowedNpcIds, allowedSceneIds);
			CollectCompiledCommands(node?.OnActivated, commandOpcodes, allowedNpcIds, allowedSceneIds);
			CollectCompiledCommands(node?.OnCompleted, commandOpcodes, allowedNpcIds, allowedSceneIds);
			CollectCompiledPredicates(node?.Narrative?.Gates, predicateOpcodes, allowedNpcIds, allowedSceneIds);
			CollectCompiledCommands(node?.Narrative?.OnStarted, commandOpcodes, allowedNpcIds, allowedSceneIds);
			foreach (QuestChoiceOption choice in node?.Narrative?.Choices ?? new List<QuestChoiceOption>())
			{
				CollectCompiledPredicates(choice?.Requirements, predicateOpcodes, allowedNpcIds, allowedSceneIds);
				CollectCompiledCommands(choice?.Consequences, commandOpcodes, allowedNpcIds, allowedSceneIds);
			}
		}
		return new WorldContract
		{
			ContractId = Guid.NewGuid().ToString("N"),
			Summary = (beat?.Summary ?? string.Empty),
			AllowedNpcIds = allowedNpcIds.OrderBy((int id) => id).ToList(),
			AllowedSceneIds = allowedSceneIds.Where((string id) => !string.IsNullOrWhiteSpace(id)).OrderBy((string id) => id, StringComparer.OrdinalIgnoreCase).ToList(),
			SelectedPredicates = predicateOpcodes.OrderBy((string opcode) => opcode, StringComparer.OrdinalIgnoreCase).ToList(),
			SelectedCommands = commandOpcodes.OrderBy((string opcode) => opcode, StringComparer.OrdinalIgnoreCase).ToList()
		};
	}

	private static void CollectCompiledPredicates(IEnumerable<QuestPredicateInvocation> predicates, HashSet<string> usedPredicates, HashSet<int> allowedNpcIds, HashSet<string> allowedSceneIds)
	{
		foreach (QuestPredicateInvocation predicate in predicates ?? Enumerable.Empty<QuestPredicateInvocation>())
		{
			if (predicate == null || string.IsNullOrWhiteSpace(predicate.Opcode))
			{
				continue;
			}
			usedPredicates.Add(predicate.Opcode.Trim());
			if (predicate.Params != null)
			{
				if (predicate.Params.TryGetValue("npcId", out var npcIdText) && int.TryParse(npcIdText, out var npcId) && npcId > 0)
				{
					allowedNpcIds.Add(npcId);
				}
				if (predicate.Params.TryGetValue("sceneId", out var sceneId) && !string.IsNullOrWhiteSpace(sceneId))
				{
					allowedSceneIds.Add(sceneId.Trim());
				}
			}
			CollectCompiledPredicates(predicate.Children, usedPredicates, allowedNpcIds, allowedSceneIds);
		}
	}

	private static void CollectCompiledCommands(IEnumerable<QuestCommandInvocation> commands, HashSet<string> usedCommands, HashSet<int> allowedNpcIds, HashSet<string> allowedSceneIds)
	{
		foreach (QuestCommandInvocation command in commands ?? Enumerable.Empty<QuestCommandInvocation>())
		{
			if (command == null || string.IsNullOrWhiteSpace(command.Opcode))
			{
				continue;
			}
			usedCommands.Add(command.Opcode.Trim());
			if (command.Params != null)
			{
				if (command.Params.TryGetValue("npcId", out var npcIdText) && int.TryParse(npcIdText, out var npcId) && npcId > 0)
				{
					allowedNpcIds.Add(npcId);
				}
				if (command.Params.TryGetValue("sceneId", out var sceneId) && !string.IsNullOrWhiteSpace(sceneId))
				{
					allowedSceneIds.Add(sceneId.Trim());
				}
			}
		}
	}

	private static List<QuestPredicateInvocation> BuildCompiledEncounterActivationPredicates(string sceneId, int npcId)
	{
		List<QuestPredicateInvocation> list = new List<QuestPredicateInvocation>();
		list.Add(CreateLogicAllPredicate(CreateSceneIsPredicate(sceneId), CreateNpcPresentPredicate(npcId)));
		return list;
	}

	private static QuestPredicateInvocation CreateLogicAllPredicate(params QuestPredicateInvocation[] children)
	{
		List<QuestPredicateInvocation> validChildren = children?.Where((QuestPredicateInvocation child) => child != null).ToList() ?? new List<QuestPredicateInvocation>();
		if (validChildren.Count == 0)
		{
			throw new InvalidOperationException("logic.all 必须至少包含一个 child predicate");
		}
		if (validChildren.Count == 1)
		{
			return validChildren[0];
		}
		return new QuestPredicateInvocation
		{
			Opcode = "logic.all",
			Children = validChildren
		};
	}

	private static QuestPredicateInvocation CreateSceneIsPredicate(string sceneId)
	{
		if (string.IsNullOrWhiteSpace(sceneId))
		{
			throw new InvalidOperationException("scene.is 缺少 sceneId");
		}
		return new QuestPredicateInvocation
		{
			Opcode = "scene.is",
			Params = new Dictionary<string, string> { ["sceneId"] = sceneId.Trim() }
		};
	}

	private static QuestPredicateInvocation CreateNpcPresentPredicate(int npcId)
	{
		if (npcId <= 0)
		{
			throw new InvalidOperationException("npc.present_in_current_scene 缺少 npcId");
		}
		return new QuestPredicateInvocation
		{
			Opcode = "npc.present_in_current_scene",
			Params = new Dictionary<string, string> { ["npcId"] = npcId.ToString() }
		};
	}

	private static QuestPredicateInvocation CreateNpcTalkedPredicate(int npcId)
	{
		if (npcId <= 0)
		{
			throw new InvalidOperationException("npc.talked_to_player 缺少 npcId");
		}
		return new QuestPredicateInvocation
		{
			Opcode = "npc.talked_to_player",
			Params = new Dictionary<string, string> { ["npcId"] = npcId.ToString() }
		};
	}

	private static QuestPredicateInvocation CreatePlayerHasItemPredicate(int itemId, int count)
	{
		if (itemId <= 0)
		{
			throw new InvalidOperationException("player.has_item 缺少 itemId");
		}
		return new QuestPredicateInvocation
		{
			Opcode = "player.has_item",
			Params = new Dictionary<string, string>
			{
				["itemId"] = itemId.ToString(),
				["count"] = count.ToString()
			}
		};
	}

	private static QuestPredicateInvocation CreateCombatVictoryPredicate(int npcId)
	{
		if (npcId <= 0)
		{
			throw new InvalidOperationException("combat.victory_over_npc 缺少 npcId");
		}
		return new QuestPredicateInvocation
		{
			Opcode = "combat.victory_over_npc",
			Params = new Dictionary<string, string> { ["npcId"] = npcId.ToString() }
		};
	}

	private static QuestCommandInvocation CreateNpcSetActionCommand(int npcId, int actionId)
	{
		if (npcId <= 0 || actionId <= 0)
		{
			throw new InvalidOperationException("npc.set_action 缺少 npcId 或 actionId");
		}
		return CreateQuestCommand("npc.set_action", ("npcId", npcId.ToString()), ("actionId", actionId.ToString()));
	}

	private static void ConnectCompiledNode(QuestNode fromNode, QuestNode toNode)
	{
		if (fromNode == null || toNode == null)
		{
			throw new InvalidOperationException("编译器尝试连接空节点");
		}
		fromNode.NextNodeId = toNode.NodeId;
	}

	private static QuestCommandInvocation CreateQuestCommand(string opcode, params (string Key, string Value)[] parameters)
	{
		QuestCommandInvocation command = new QuestCommandInvocation
		{
			Opcode = opcode,
			Params = new Dictionary<string, string>()
		};
		for (int i = 0; i < parameters.Length; i++)
		{
			var (key, value) = parameters[i];
			if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
			{
				command.Params[key] = value;
			}
		}
		return command;
	}

	private static QuestCommandInvocation CloneQuestCommandInvocation(QuestCommandInvocation command)
	{
		return new QuestCommandInvocation
		{
			Opcode = (command?.Opcode ?? string.Empty),
			Params = ((command?.Params == null) ? new Dictionary<string, string>() : new Dictionary<string, string>(command.Params))
		};
	}

	private static List<AIQuestStoryLine> CloneStoryLines(IEnumerable<AIQuestStoryLine> lines)
	{
		return (from line in lines ?? Enumerable.Empty<AIQuestStoryLine>()
			where line != null
			select new AIQuestStoryLine
			{
				SpeakerType = line.SpeakerType,
				SpeakerNpcId = line.SpeakerNpcId,
				Text = line.Text
			}).ToList();
	}

	private static string BuildCompiledNodeId(string token)
	{
		return "node_" + NormalizeCompiledToken(token);
	}

	private static string NormalizeCompiledToken(string token)
	{
		string raw = (string.IsNullOrWhiteSpace(token) ? Guid.NewGuid().ToString("N") : token.Trim());
		char[] chars = raw.Select((char ch) => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
		return new string(chars);
	}

	private static string ResolveCompiledSceneName(string sceneId, QuestElementPicker.BeatExecutionContext executionContext, string fallbackSceneName = null)
	{
		QuestElementPicker.PickedLocation location = executionContext?.AllowedLocations?.FirstOrDefault((QuestElementPicker.PickedLocation candidate) => candidate != null && string.Equals(candidate.Id, sceneId, StringComparison.OrdinalIgnoreCase));
		if (!string.IsNullOrWhiteSpace(location?.Name))
		{
			return location.Name;
		}
		if (!string.IsNullOrWhiteSpace(fallbackSceneName))
		{
			return fallbackSceneName.Trim();
		}
		if (QuestElementPicker.TryGetOfficialLocationName(sceneId, out var officialSceneName))
		{
			return officialSceneName;
		}
		return sceneId;
	}

	private void BindProgramToCampaignBeat(QuestProgram program, MainlineCampaign campaign, MainlineChapter chapter, MainlineBeat beat)
	{
		if (program != null)
		{
			program.TrackType = QuestTrackType.Mainline;
			program.CampaignId = campaign?.CampaignId ?? string.Empty;
			program.ChapterId = chapter?.ChapterId ?? string.Empty;
			program.BeatId = beat?.BeatId ?? string.Empty;
			program.Priority = GetMainlineQuestPriority(program.Priority);
			if (beat?.QuestIds != null && !beat.QuestIds.Contains(program.ProgramId))
			{
				beat.QuestIds.Add(program.ProgramId);
			}
		}
	}

	private void ApplyBeatWorldContract(QuestProgram program, MainlineBeat beat)
	{
		if (program != null && beat?.WorldContract != null)
		{
			WorldContract beatContract = beat.WorldContract;
			program.WorldContract = program.WorldContract ?? new QuestWorldContract();
			program.WorldContract.AllowedNpcIds = new List<int>(beatContract.AllowedNpcIds ?? new List<int>());
			program.WorldContract.AllowedSceneIds = new List<string>(beatContract.AllowedSceneIds ?? new List<string>());
			program.WorldContract.SelectedPredicates = new List<string>(beatContract.SelectedPredicates ?? new List<string>());
			program.WorldContract.SelectedCommands = new List<string>(beatContract.SelectedCommands ?? new List<string>());
		}
	}

	private void BindProgramToAnchorNpc(QuestProgram program, NPCInfo npcInfo)
	{
		if (program != null && npcInfo != null)
		{
			program.TrackType = QuestTrackType.Mainline;
			program.AnchorNpcId = npcInfo.NpcID;
			program.AnchorNpcName = (string.IsNullOrWhiteSpace(npcInfo.Name) ? ResolveQuestProgramNpcName(npcInfo.NpcID) : npcInfo.Name);
			program.Title = (string.IsNullOrWhiteSpace(program.Title) ? (program.AnchorNpcName + "的主线") : program.Title.Trim());
			program.Summary = (program.Summary ?? string.Empty).Trim();
			program.WorldContract = program.WorldContract ?? new QuestWorldContract();
			program.Nodes = program.Nodes ?? new List<QuestNode>();
			program.Runtime = program.Runtime ?? new QuestRuntimeState();
			program.Runtime.ActivatedNodeIds = program.Runtime.ActivatedNodeIds ?? new List<string>();
			program.Runtime.CompletedNodeIds = program.Runtime.CompletedNodeIds ?? new List<string>();
			program.Runtime.ExecutedCommandKeys = program.Runtime.ExecutedCommandKeys ?? new List<string>();
			program.Runtime.PendingNarrativeNodeIds = program.Runtime.PendingNarrativeNodeIds ?? new List<string>();
			program.Runtime.ActiveForcedNpcStates = program.Runtime.ActiveForcedNpcStates ?? new List<QuestForcedNpcState>();
			if (!program.WorldContract.AllowedNpcIds.Contains(program.AnchorNpcId))
			{
				program.WorldContract.AllowedNpcIds.Insert(0, program.AnchorNpcId);
			}
		}
	}

	private string FormatNpcValidationList(IEnumerable<int> npcIds)
	{
		if (npcIds == null)
		{
			return "none";
		}
		List<int> ids = npcIds.Distinct().ToList();
		return (ids.Count == 0) ? "none" : string.Join(",", ids);
	}

	private string ResolveSceneId(string sceneName)
	{
		string locationId;
		return QuestElementPicker.TryResolveOfficialLocationId(sceneName, out locationId) ? locationId : null;
	}

	public LegacyDataWipeSummary ClearAllQuestDataForLegacyWipe(string persistentDataRoot, LegacyDataWipeSummary summary = null)
	{
		summary = summary ?? new LegacyDataWipeSummary();
		try
		{
			CleanupStoryDialogs();
			_pendingQuestMails.Clear();
			_questTaskMemories.Clear();
			_questProgramState = QuestProgramPersistence.CreateEmptyState();
			_talkFlags.Clear();
			_recentBattleVictories.Clear();
			_recentQuestProgramDeliveryMailSubmissions.Clear();
			_visitedSceneIds.Clear();
			_lastNarrativeLocationSignature = string.Empty;
			_checkTimer = 0f;
			_hasLoadedPersistence = true;
		}
		catch (Exception ex)
		{
			ManualLogSource logger = AIChatManager.logger;
			if (logger != null)
			{
				logger.LogWarning((object)$"[AIQuestManager] ClearAllQuestDataForLegacyWipe runtime reset failed: {ex}");
			}
			summary.AddError("重置任务运行态失败: " + ex.Message);
		}
		DeleteQuestPersistenceFilesForLegacyWipe(persistentDataRoot, summary);
		return summary;
	}

	private QuestProgramState BuildQuestProgramStateForSave(DateTime savedAt)
	{
		QuestProgramState state = QuestProgramPersistence.NormalizeState(_questProgramState);
		state.SavedAt = savedAt;
		state.Programs = state.Programs.Where((QuestProgram program) => program != null && program.TrackType == QuestTrackType.Mainline).ToList();
		return state;
	}

	private bool ShouldWipeLegacyQuestSave(JToken rootToken, QuestSaveData saveData, out string reason)
	{
		reason = null;
		if (rootToken == null || rootToken.Type != JTokenType.Object)
		{
			return false;
		}
		JObject obj = (JObject)rootToken;
		string[] legacyKeys = new string[3] { "ActiveQuests", "PendingQuests", "HistoryQuests" };
		string[] array = legacyKeys;
		foreach (string key in array)
		{
			if (obj.Property(key, StringComparison.OrdinalIgnoreCase) != null)
			{
				reason = "legacy_cutover:" + key;
				return true;
			}
		}
		if (saveData != null && saveData.SaveFormatVersion >= 3)
		{
			return false;
		}
		return false;
	}

	private void ApplyQuestProgramHardCutoverWipe(string reason)
	{
		AIChatManager.logger.LogWarning((object)("[AIQuestManager] 检测到旧 AI 任务存档，已按硬切换策略清空。reason=" + reason));
		ClearAllQuestDataForLegacyWipe(Application.persistentDataPath);
		_questProgramState = QuestProgramPersistence.CreateEmptyState();
		SaveQuests();
	}

	internal static LegacyDataWipeSummary DeleteQuestPersistenceFilesForLegacyWipe(string persistentDataRoot, LegacyDataWipeSummary summary = null)
	{
		summary = summary ?? new LegacyDataWipeSummary();
		if (string.IsNullOrWhiteSpace(persistentDataRoot))
		{
			return summary;
		}
		string questSavesDir = Path.Combine(persistentDataRoot, "QuestSaves");
		if (Directory.Exists(questSavesDir))
		{
			try
			{
				summary.DeletedQuestPersistenceFiles += Directory.GetFiles(questSavesDir, "*", SearchOption.AllDirectories).Length;
				Directory.Delete(questSavesDir, recursive: true);
				summary.QuestSavesDirectoryDeleted = true;
			}
			catch (Exception arg)
			{
				ManualLogSource logger = AIChatManager.logger;
				if (logger != null)
				{
					logger.LogWarning((object)$"[AIQuestManager] Delete quest save directory failed: {questSavesDir} | {arg}");
				}
				summary.AddError("删除任务存档目录失败");
			}
		}
		string[] array = new string[3] { "AIQuestData.json", "AIQuestHistory.json", "AIQuestDebugSnapshot.json" };
		foreach (string fileName in array)
		{
			string filePath = Path.Combine(persistentDataRoot, fileName);
			if (!File.Exists(filePath))
			{
				continue;
			}
			try
			{
				File.Delete(filePath);
				summary.DeletedQuestPersistenceFiles++;
			}
			catch (Exception arg2)
			{
				ManualLogSource logger2 = AIChatManager.logger;
				if (logger2 != null)
				{
					logger2.LogWarning((object)$"[AIQuestManager] Delete legacy quest file failed: {filePath} | {arg2}");
				}
				summary.AddError("删除任务文件失败: " + fileName);
			}
		}
		return summary;
	}

	private static string BuildQuestSaveFilePath(int avatarIndex, int slotIndex, bool useNewSaveSystem)
	{
		int safeAvatar = Mathf.Max(0, avatarIndex);
		int safeSlot = Mathf.Max(0, slotIndex);
		if (useNewSaveSystem)
		{
			string avatarPath = YSNewSaveSystem.GetAvatarSavePathPre(safeAvatar, safeSlot) ?? string.Empty;
			if (string.IsNullOrWhiteSpace(avatarPath))
			{
				avatarPath = $"Avatar{safeAvatar}/Slot{safeSlot}";
			}
			return Path.Combine(EffectiveNewSaveRoot, avatarPath, "AIQuestData.json");
		}
		string saveId = Tools.instance.getSaveID(safeAvatar, safeSlot);
		return Path.Combine(EffectiveOldSaveRoot, "AIQuestData" + saveId + ".json");
	}

	private static string BuildQuestHistoryFilePath(int avatarIndex, int slotIndex, bool useNewSaveSystem)
	{
		int safeAvatar = Mathf.Max(0, avatarIndex);
		int safeSlot = Mathf.Max(0, slotIndex);
		if (useNewSaveSystem)
		{
			string avatarPath = YSNewSaveSystem.GetAvatarSavePathPre(safeAvatar, safeSlot) ?? string.Empty;
			if (string.IsNullOrWhiteSpace(avatarPath))
			{
				avatarPath = $"Avatar{safeAvatar}/Slot{safeSlot}";
			}
			return Path.Combine(EffectiveNewSaveRoot, avatarPath, "AIQuestHistory.json");
		}
		string saveId = Tools.instance.getSaveID(safeAvatar, safeSlot);
		return Path.Combine(EffectiveOldSaveRoot, "AIQuestHistory" + saveId + ".json");
	}

	private void TryAutoMigrateLegacyPersistence()
	{
		if (string.IsNullOrEmpty(_saveFilePathOverride) && string.IsNullOrEmpty(_historyFilePathOverride))
		{
			string scope = (string.IsNullOrEmpty(_currentSaveScopeKey) ? "legacy_default" : _currentSaveScopeKey);
			TryAutoMigrateLegacyFile(SaveFilePath, "任务存档", LegacyRootSaveFilePath, Path.Combine(EffectiveLegacyQuestPersistenceRoot, "QuestSaves", scope, "AIQuestData.json"));
		}
	}

	private void RetireLegacyQuestHistoryPersistenceFiles()
	{
		string[] array = new string[2] { HistoryFilePath, LegacyRootHistoryFilePath };
		foreach (string path in array)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch (Exception arg)
			{
				AIChatManager.logger.LogWarning((object)$"[AIQuestManager] 退役旧任务历史文件失败: {path} | {arg}");
			}
		}
	}

	private void TryAutoMigrateLegacyFile(string targetPath, string label, params string[] sourceCandidates)
	{
		if (File.Exists(targetPath) || sourceCandidates == null || sourceCandidates.Length == 0)
		{
			return;
		}
		string fullTargetPath = Path.GetFullPath(targetPath);
		string sourcePath = (from path in sourceCandidates.Where((string path) => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath)
			where !string.Equals(path, fullTargetPath, StringComparison.OrdinalIgnoreCase)
			select path).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
		if (!string.IsNullOrEmpty(sourcePath))
		{
			string targetDirectory = Path.GetDirectoryName(targetPath);
			if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
			{
				Directory.CreateDirectory(targetDirectory);
			}
			File.Copy(sourcePath, targetPath, overwrite: false);
			AIChatManager.logger.LogInfo((object)("[AIQuestManager] 已自动迁移" + label + ": " + sourcePath + " -> " + targetPath));
		}
	}

	private void MarkQuestPersistenceDirty()
	{
		_questPersistenceDirty = true;
	}

	public void SaveQuests()
	{
		try
		{
			DateTime savedAt;
			QuestSaveData saveData = new QuestSaveData
			{
				SaveFormatVersion = 3,
				SavedAt = (TryGetGameTime(out savedAt) ? savedAt : DateTime.Now),
				PendingQuestMails = new List<QuestMailEnvelope>(_pendingQuestMails),
				QuestTaskMemories = _questTaskMemories.ToDictionary((KeyValuePair<int, List<QuestTaskMemoryEntry>> pair) => pair.Key, (KeyValuePair<int, List<QuestTaskMemoryEntry>> pair) => (pair.Value != null) ? new List<QuestTaskMemoryEntry>(pair.Value) : new List<QuestTaskMemoryEntry>()),
				MainlineCampaign = _mainlineCampaign,
				ProgramState = BuildQuestProgramStateForSave(savedAt)
			};
			string json = JsonConvert.SerializeObject(saveData, _jsonSettings);
			string dir = Path.GetDirectoryName(SaveFilePath);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}
			TextEncodingUtil.WriteAllTextUtf8(SaveFilePath, json);
			_questPersistenceDirty = false;
			RetireLegacyQuestHistoryPersistenceFiles();
			int activePrograms = (saveData.ProgramState?.Programs?.Count((QuestProgram program) => program != null && program.Status == QuestProgramStatus.Active)).GetValueOrDefault();
			int draftPrograms = (saveData.ProgramState?.Programs?.Count((QuestProgram program) => program != null && program.Status == QuestProgramStatus.Draft)).GetValueOrDefault();
			AIChatManager.logger.LogInfo((object)$"[AIQuestManager] 已保存主线 QuestProgram 存档到 {SaveFilePath} | activePrograms={activePrograms}, draftPrograms={draftPrograms}, pendingMails={saveData.PendingQuestMails.Count}, taskMemoryNpcs={saveData.QuestTaskMemories.Count}");
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogError((object)$"[AIQuestManager] 保存任务失败: {arg}");
		}
	}

	public void LoadQuests()
	{
		try
		{
			AIChatManager.logger.LogInfo((object)("[AIQuestManager] 尝试加载任务存档: " + SaveFilePath));
			if (!File.Exists(SaveFilePath))
			{
				AIChatManager.logger.LogInfo((object)"[AIQuestManager] 无存档文件，跳过加载");
				return;
			}
			string json = TextEncodingUtil.ReadAllTextWithFallback(SaveFilePath);
			if (string.IsNullOrEmpty(json))
			{
				return;
			}
			JToken rootToken = JToken.Parse(json);
			if (rootToken.Type == JTokenType.Array)
			{
				ApplyQuestProgramHardCutoverWipe("legacy_array_root");
				return;
			}
			QuestSaveData saveData = rootToken.ToObject<QuestSaveData>();
			if (ShouldWipeLegacyQuestSave(rootToken, saveData, out var legacyReason))
			{
				ApplyQuestProgramHardCutoverWipe(legacyReason);
				return;
			}
			List<QuestMailEnvelope> pendingQuestMails = saveData?.PendingQuestMails ?? new List<QuestMailEnvelope>();
			Dictionary<int, List<QuestTaskMemoryEntry>> questTaskMemories = saveData?.QuestTaskMemories ?? new Dictionary<int, List<QuestTaskMemoryEntry>>();
			MainlineCampaign mainlineCampaign = saveData?.MainlineCampaign;
			QuestProgramState programState = QuestProgramPersistence.NormalizeState(saveData?.ProgramState);
			if (programState?.Programs != null)
			{
				programState.Programs = programState.Programs.Where((QuestProgram program) => program != null && program.TrackType == QuestTrackType.Mainline).ToList();
			}
			_pendingQuestMails = pendingQuestMails;
			_questTaskMemories = questTaskMemories ?? new Dictionary<int, List<QuestTaskMemoryEntry>>();
			_mainlineCampaign = mainlineCampaign;
			_questProgramState = programState ?? QuestProgramPersistence.CreateEmptyState();
			RetireLegacyQuestHistoryPersistenceFiles();
			int activePrograms = (_questProgramState?.Programs?.Count((QuestProgram program) => program != null && program.Status == QuestProgramStatus.Active)).GetValueOrDefault();
			int draftPrograms = (_questProgramState?.Programs?.Count((QuestProgram program) => program != null && program.Status == QuestProgramStatus.Draft)).GetValueOrDefault();
			AIChatManager.logger.LogInfo((object)$"[AIQuestManager] 已加载主线 QuestProgram 存档: activePrograms={activePrograms}, draftPrograms={draftPrograms}, pendingMails={_pendingQuestMails.Count}, taskMemoryNpcs={_questTaskMemories.Count}");
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogError((object)$"[AIQuestManager] 加载任务失败: {arg}");
		}
	}

	private void Start()
	{
		TryLoadPersistenceIfReady();
	}

	private void OnDestroy()
	{
		if (Instance != this)
		{
			return;
		}
		if (!_hasLoadedPersistence)
		{
			AIChatManager.logger.LogInfo((object)"[AIQuestManager] OnDestroy skipped save before persistence load");
			return;
		}
		try
		{
			if (_questPersistenceDirty)
			{
				AIChatManager.logger.LogWarning((object)"[AIQuestManager] OnDestroy 检测到未保存的任务进度；当前版本仅随官方存档快照持久化。");
			}
		}
		catch (Exception arg)
		{
			AIChatManager.logger.LogError((object)$"[AIQuestManager] OnDestroy 保存异常: {arg}");
		}
	}

	private void OnApplicationQuit()
	{
		if (!(Instance != this) && _hasLoadedPersistence && _questPersistenceDirty)
		{
			AIChatManager.logger.LogWarning((object)"[AIQuestManager] OnApplicationQuit 检测到未保存的任务进度；当前版本仅随官方存档快照持久化。");
		}
	}
}
