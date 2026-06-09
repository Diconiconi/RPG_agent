using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Configuration;
using HarmonyLib;
using JSONClass;
using LlmTornado.Chat;
using LlmTornado.ChatFunctions;
using LlmTornado.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YSGame.TianJiDaBi;

namespace MCS_AIChatMod;

internal static class QuestSelfTestRunner
{
	private sealed class TestCaseResult
	{
		public string Name;

		public bool Passed;

		public string Details;
	}

	private sealed class ManagerStateBackup
	{
		public string QuestProgramStateJson;

		public Dictionary<int, List<object>> QuestTaskMemories;

		public string MainlineCampaignJson;

		public Dictionary<int, bool> TalkFlags;

		public Dictionary<int, float> RecentBattleVictories;

		public HashSet<string> VisitedSceneIds;

		public bool HasLoadedPersistence;

		public string SavePathOverride;

		public string HistoryPathOverride;

		public string NewSaveRootOverride;

		public string OldSaveRootOverride;

		public string LegacyQuestPersistenceRootOverride;
	}

	private sealed class NarrativeActorCandidate
	{
		public int NpcId;

		public string NpcName;

		public string OriginalLocationId;
	}

	private sealed class NpcWorldSnapshot
	{
		public int NpcId;

		public string LocationId;

		public int ActionId;

		public int StatusId;

		public bool HasLockAction;

		public int LockActionId;
	}

	private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
	{
		Formatting = Formatting.Indented,
		NullValueHandling = NullValueHandling.Ignore,
		Converters = { (JsonConverter)new StringEnumConverter() }
	};

	public static bool Run(AIQuestManager manager, out string summary, out string reportPath)
	{
		reportPath = Path.Combine(Application.persistentDataPath, "MCS_AIChatMod_QuestSelfTest.txt");
		List<TestCaseResult> results = new List<TestCaseResult>();
		if (manager == null)
		{
			results.Add(new TestCaseResult
			{
				Name = "ManagerAvailability",
				Passed = false,
				Details = "AIQuestManager.Instance is null"
			});
			WriteReport(reportPath, results);
			summary = "Quest self-test failed: AIQuestManager unavailable";
			return false;
		}
		string tempDir = Path.Combine(Application.persistentDataPath, "MCS_AIChatMod_SelfTest");
		string tempQuestPath = Path.Combine(tempDir, "AIQuestData.selftest.json");
		string tempHistoryPath = Path.Combine(tempDir, "AIQuestHistory.selftest.json");
		Directory.CreateDirectory(tempDir);
		ManagerStateBackup backup = CaptureState(manager);
		try
		{
			SetPersistenceOverrides(tempQuestPath, tempHistoryPath);
			SafeDelete(tempQuestPath);
			SafeDelete(tempHistoryPath);
			results.Add(TestOnDestroySkipsSaveBeforeLoad(manager, tempQuestPath, tempHistoryPath));
			results.Add(TestStructuredStoryLineParsesSpeakerMetadata());
			results.Add(TestStorySpeakerPresentationResolvesRuntimeRouting());
			results.Add(TestLlmTornadoProviderInferenceFromEndpoint());
			results.Add(TestLlmTornadoCompatibleEndpointPrefersCustomProvider());
			results.Add(TestLlmTornadoCompatiblePayloadSanitization());
			results.Add(TestGeminiCompatibleModelNameNormalization());
			results.Add(TestProviderModelSettingsPersistSeparately());
			results.Add(TestApplyProviderPresetRestoresProviderSpecificModel());
			results.Add(TestCompatibilityProfileResolvesKnownProviders());
			results.Add(TestQwenCompatibilityDisablesStreamWithTools());
			results.Add(TestCustomCompatibilityOverrideForcesAnthropicNative());
			results.Add(TestCompatibleRequestFailureDiagnosticIncludesPayloadPreview());
			results.Add(TestMainlineGenerationFailureDiagnosticIncludesStageAndResponsePreview());
			results.Add(TestOfficialNpcPatchesMatchOriginalParameterNames());
			results.Add(TestOfficialBiographyInterceptionPatchesCoverSourceMethods());
			results.Add(TestOfficialBiographyInterceptionPatchesAreRegistered());
			results.Add(TestLlmTornadoRequestMappingPreservesMessagesAndTools());
			results.Add(TestLlmTornadoChatResultMapsToOpenAiResponse());
			results.Add(TestQuestProgramPersistenceRoundTrips());
			results.Add(TestQuestProgramPersistenceWipesLegacyQuestSave());
			results.Add(TestQuestCapabilityRegistryExposesMainlineOnlyOpcodes());
			results.Add(TestTimeCandidatePoolUsesRemainingLifeAndThreeBands());
			results.Add(TestMainlineStoryBeatMessagesUseSingleBeatProtocol());
			results.Add(TestMainlineStoryBeatRepairMessagesAvoidStructuredReplay());
			results.Add(TestMainlineBiographySummaryPrunesOlderEvents());
			results.Add(TestMainlineDialogSummaryPrunesAndFiltersNoise());
			results.Add(TestMainlineBeatGamePlanningMessagesUseFunctionalProtocol());
			results.Add(TestMainlineBeatGamePlanRejectsPresentChoiceWithoutScriptBlockId(manager));
			results.Add(TestMainlineBeatCompilationUsesCodeCompiler(manager));
			results.Add(TestQuestProgramParserAllowsSelectedDangerousCommandWithoutExtraPermission());
			results.Add(TestQuestProgramParserAcceptsPredicateOnlyMainlineProgram());
			results.Add(TestQuestProgramParserRejectsOpcodesOutsideSelectedSets());
			results.Add(TestQuestProgramParserAcceptsValidMainlineProgram());
			results.Add(TestQuestProgramParserAcceptsFencedMarkdownJson());
			results.Add(TestQuestProgramParserRejectsTruncatedFencedJson());
			results.Add(TestQuestProgramParserRejectsObjectiveWithoutCompletionPredicates());
			results.Add(TestQuestProgramParserRejectsNonTerminalNodeWithoutNextNodeId());
			results.Add(TestMainlinePromptsUseCurrentSemanticProtocol());
			results.Add(TestMainlineContractsUseDescriptiveOutputShells());
			results.Add(TestMainlineContractsDeclareCompactLinesAndMultipleBlocks());
			results.Add(TestMainlineStructuredBeatIgnoresIllegalContent(manager));
			results.Add(TestMainlineRawNarrativeDraftRejectsStructuredOutput(manager));
			results.Add(TestMainlineStructuredBeatAcceptsChoiceScopedBlocks(manager));
			results.Add(TestMainlineStructuredBeatAcceptsDecoratedSceneNames(manager));
			results.Add(TestMainlineStructuredBeatAcceptsOfficialSceneOutsideCandidatePool(manager));
			results.Add(TestMainlineStructuredBeatAcceptsDecoratedNpcNames(manager));
			results.Add(TestMainlineStructuredBeatReportsFirstBlockErrorWhenAllBlocksInvalid(manager));
			results.Add(TestMainlineStructuredBeatNormalizesProtocolValueCase(manager));
			results.Add(TestMainlineBeatGamePlanRequiresFlowSteps(manager));
			results.Add(TestMainlineBeatGamePlanAllowsTerminalBeatWithoutExtraFields(manager));
			results.Add(TestMainlineBeatGamePlanNormalizesProtocolValueCase(manager));
			results.Add(TestMainlineBeatGamePlanAcceptsDecoratedNpcAndSceneNames(manager));
			results.Add(TestMainlineBeatGamePlanAcceptsAcquireAndDeliverFlowKinds(manager));
			results.Add(TestMainlineBeatGamePlanAcceptsVisitAndReportFlowKinds(manager));
			results.Add(TestMainlineBeatGamePlanRejectsUnsupportedFlowKind(manager));
			results.Add(TestMainlineBeatGamePlanIgnoresUnsupportedEffectKind(manager));
			results.Add(TestMainlineBeatGamePlanIgnoresIllegalGrantItemEffect(manager));
			results.Add(TestMainlineBeatGamePlanIgnoresIllegalItemFlowStep(manager));
			results.Add(TestMainlineBeatGamePlanRejectsChoiceScopedFlowWithoutPresentChoice(manager));
			results.Add(TestMainlineBeatGamePlanRejectsMissingScriptBlockReference(manager));
			results.Add(TestMainlineBeatGamePlanIgnoresUnexpectedRootField(manager));
			results.Add(TestMainlineBeatGamePlanIgnoresUnexpectedProtocolField(manager));
			results.Add(TestMainlineBeatCompilationCompilesAcquireAndDeliverInPerson(manager));
			results.Add(TestMainlineBeatCompilationResolvesDecoratedConcreteItemNames(manager));
			results.Add(TestMainlineBeatCompilationResolvesUniqueFuzzyConcreteItemNames(manager));
			results.Add(TestMainlineBeatCompilationResolvesGlobalConcreteItemOutsideBeatContext(manager));
			results.Add(TestMainlineBeatCompilationCompilesVisitAndReportBackInPerson(manager));
			results.Add(TestMainlineBeatCompilationAssignsWorldControlContracts(manager));
			results.Add(TestMainlineBeatCompilationRoutesChoiceScopedFlowIntoIndependentBranches(manager));
			results.Add(TestMainlineBeatCompilationRoutesChoiceScopedVisitAndReportIntoIndependentBranches(manager));
			results.Add(TestMainlineBeatCompilationTerminatesEachChoiceBranchIndependently(manager));
			results.Add(TestMainlineBeatCompilationSynthesizesEncounterForNarrativeOnlyFlow(manager));
			results.Add(TestMainlineStoryBeatGenerationRetriesOnceOnInvalidFormat(manager));
			results.Add(TestMainlineStructuredBeatGenerationRetriesOnceOnInvalidFormat(manager));
			results.Add(TestMainlineBeatGamePlanGenerationRetriesOnceOnInvalidFormat(manager));
			results.Add(TestQuestProgramParserAcceptsCompositePredicateChildren());
			results.Add(TestQuestProgramParserRejectsCompositePredicateParamsChildren());
			results.Add(TestQuestProgramParserRejectsCompositePredicateWithoutChildren());
			results.Add(TestAcceptQuestProgramSeedsRuntimeNode(manager));
			results.Add(TestGetActiveQuestProgramsReturnsOnlyActive(manager));
			results.Add(TestGetPendingQuestProgramsReturnsOnlyDraft(manager));
			results.Add(TestGetQuestProgramHistoryReturnsOnlyTerminal(manager));
			results.Add(TestQuestVisibilityQueriesUseQuestPrograms(manager));
			results.Add(TestMainlineProgramGenerationValidationUsesQuestPrograms(manager));
			results.Add(TestBuildCurrentProgramGuideTextUsesCurrentNodeSummary(manager));
			results.Add(TestBuildCurrentProgramGuideTextOmitsPredicateHint(manager));
			results.Add(TestBuildCurrentProgramStatusTextReportsUnsatisfiedActivation(manager));
			results.Add(TestBuildCurrentProgramStatusTextUsesAnchorNpcName(manager));
			results.Add(TestBuildCurrentProgramStatusTextReportsPendingNarrative(manager));
			results.Add(TestBuildOfficialTaskUiEntriesIncludesActiveMainlineProgram(manager));
			results.Add(TestBuildOfficialTaskUiEntriesIncludesWaitingMainlineEntry(manager));
			results.Add(TestBuildOfficialTaskUiEntriesSanitizeLegacyInternalNpcFallbacks(manager));
			results.Add(TestOfficialTaskUiInjectionTargetsCurrentMainlineViewOnly());
			results.Add(TestOfficialTaskUiDetailPayloadUsesStatusRemainingAndGuide());
			results.Add(TestOfficialTaskUiPatchSetIncludesMainlineInitAndClickHooks());
			results.Add(TestBuildCurrentProgramRemainingTimeTextUsesBeatDeadline(manager));
			results.Add(TestMalformedObjectiveNodeSuspendsInsteadOfAutoCompleting(manager));
			results.Add(TestQuestProgramSceneEventAdvancesObjectiveNode(manager));
			results.Add(TestQuestProgramTickCompletesTerminalNode(manager));
			results.Add(TestQuestProgramActivationCommandMutatesNpcState(manager));
			results.Add(TestQuestProgramCompletionRestoresForcedNpcState(manager));
			results.Add(TestQuestProgramNarrativeNodePlaysAndAdvances(manager));
			results.Add(TestQuestProgramChoiceNodeExecutesConsequenceAndJumps(manager));
			results.Add(TestQuestProgramSceneLoadCommandUpdatesLocation(manager));
			results.Add(TestQuestProgramSceneLoadWithoutContractSuspends(manager));
			results.Add(TestQuestProgramOfficialTaskStartCommandCompletesNode(manager));
			results.Add(TestQuestProgramOfficialTaskFinishStepCommandCompletesNode(manager));
			results.Add(TestQuestProgramOfficialTaskFinishablePredicateCompletesNode(manager));
			results.Add(TestQuestProgramCombatStartCommandRecordsEncounter(manager));
			results.Add(TestQuestProgramKillNpcCommandCompletesNode(manager));
			results.Add(TestQuestProgramNotificationCommandQueuesWithoutCyFriend(manager));
			results.Add(TestQuestProgramAddCyFriendCommandCompletesNode(manager));
			results.Add(TestQuestProgramRewardCommandsMutatePlayerState(manager));
			results.Add(TestQuestProgramSkillLearnCommandsMutatePlayerSkillState(manager));
			results.Add(TestQuestProgramPlayerHasItemPredicateCompletesNode(manager));
			results.Add(TestQuestProgramPlayerMoneyComparePredicateCompletesNode(manager));
			results.Add(TestQuestProgramPlayerHasSkillPredicateCompletesNode(manager));
			results.Add(TestQuestProgramPlayerHasStaticSkillPredicateCompletesNode(manager));
			results.Add(TestQuestProgramTimeWindowPredicateCompletesNode(manager));
			results.Add(TestQuestProgramFuBenRemainingDaysPredicateCompletesNode(manager));
			results.Add(TestQuestProgramNpcActionPredicateCompletesNode(manager));
			results.Add(TestQuestProgramPlayerLevelComparePredicateCompletesNode(manager));
			results.Add(TestQuestProgramPlayerFavorComparePredicateCompletesNode(manager));
			results.Add(TestQuestProgramSceneTypePredicateCompletesNode(manager));
			results.Add(TestQuestProgramNpcPatrolPresentPredicateCompletesNode(manager));
			results.Add(TestQuestProgramOfficialTaskNotOutdatedPredicateCompletesNode(manager));
			results.Add(TestQuestProgramLogicAllPredicateCompletesNode(manager));
			results.Add(TestQuestProgramLogicAnyPredicateCompletesNode(manager));
			results.Add(TestQuestProgramLogicNotPredicateCompletesNode(manager));
			results.Add(TestQuestProgramNpcPresentPredicateActivatesNode(manager));
			results.Add(TestQuestProgramPlayerLevelObservedStateAdvancesNode(manager));
			results.Add(TestQuestProgramNpcActionObservedStateAdvancesNode(manager));
			results.Add(TestQuestProgramMoneyChangeEventAdvancesNode(manager));
			results.Add(TestQuestProgramItemChangeEventAdvancesNode(manager));
			results.Add(TestQuestProgramNpcAliveChangeEventCompletesTerminal(manager));
			results.Add(TestQuestProgramOfficialTaskStartEventAdvancesNode(manager));
			results.Add(TestQuestProgramOfficialTaskStepFinishedEventAdvancesNode(manager));
			results.Add(TestQuestProgramOfficialTaskFinishableEventAdvancesNode(manager));
			results.Add(TestQuestProgramBattleVictoryEventAdvancesNode(manager));
			results.Add(TestQuestProgramNpcStatusChangeEventAdvancesNode(manager));
			results.Add(TestQuestProgramNpcActionChangeEventAdvancesNode(manager));
			results.Add(TestQuestProgramNpcFavorChangeEventAdvancesNode(manager));
			results.Add(TestQuestProgramNpcDeathEventCompletesTerminal(manager));
			results.Add(TestQuestProgramPlayerSkillLearnedEventAdvancesNode(manager));
			results.Add(TestQuestProgramPlayerStaticSkillLearnedEventAdvancesNode(manager));
			results.Add(TestQuestProgramPlayerLevelChangedEventAdvancesNode(manager));
			results.Add(TestQuestProgramFuBenRemainingDaysChangedEventAdvancesNode(manager));
			results.Add(TestQuestProgramDeliveryMailSubmissionEventAdvancesNode(manager));
			results.Add(TestQuestProgramDeliveryMailSubmissionIgnoresWrongItem(manager));
			results.Add(TestQuestProgramFavorCommandIsIdempotent(manager));
			results.Add(TestCyEmailValidatorRejectsOrphanedOldMail());
			results.Add(TestCyEmailSanitizerDropsInvalidOldMail());
			results.Add(TestLegacyDataWipePreservesCharacterCards(tempDir));
			results.Add(TestLegacyDataWipeClearsQuestRuntimeAndPersistence(manager, tempDir));
			results.Add(TestLegacyDataWipeConfirmationContract());
			results.Add(TestExperimentalMainlineOptInContract());
			results.Add(TestNpcInteractionRefreshDoesNotMutateSharedNpcInfo());
			results.Add(TestDialogHistoryNormalizationCollapsesMismatchedNpcFiles(tempDir));
			results.Add(TestDialogHistorySummariesDeduplicateNpcIds(tempDir));
			results.Add(TestResolveInitialContextUsesTargetNpcHistory());
			results.Add(TestMainlineGenerationRequestWaitsForInterceptedBiographyEvent(manager, tempQuestPath, tempHistoryPath));
			results.Add(TestRestoredWaitingMainlineAnchorStatusAndButtonContract(manager));
			results.Add(TestOfficialSaveContextSaveFlushesOnlyOnOutermostPostfix(manager));
			results.Add(TestOfficialSaveContextCleanSaveSkipsRedundantFlush(manager, tempDir));
			results.Add(TestOfficialSaveContextCollapsesSameFrameLoadLikeCallbacks(manager));
			results.Add(TestOfficialSaveContextCleanSameScopeSwitchSkipsRedundantReload(manager, tempDir));
			results.Add(TestOfficialSaveContextCleanSameScopeLoadSkipsRedundantReload(manager, tempDir));
			results.Add(TestOfficialSaveContextLoadRollsBackUnsavedProgress(manager, tempDir));
			results.Add(TestOfficialSaveContextDifferentSlotsRemainIsolated(manager, tempDir));
			results.Add(TestOfficialSaveContextDifferentAvatarsRemainIsolated(manager, tempDir));
			results.Add(TestLegacyQuestScopeMigratesIntoOfficialSlotPath(manager, tempDir));
			results.Add(TestMainlineCampaignPersistsSingleBeat(manager, tempQuestPath, tempHistoryPath));
			results.Add(TestSaveQuestsDoesNotEmitDebugSnapshot(manager, tempQuestPath, tempHistoryPath));
			results.Add(TestMainlineSingleBeatCompletionCompletesChapter(manager));
			results.Add(TestMainlineBeatCompletionResolvesPendingBiographyEvent(manager));
			results.Add(TestMainlineBeatCompletionWithoutPendingBiographyEventCompletesChapter(manager));
			results.Add(TestPendingBiographyGenerationFailureClearsPendingBiographyEvent(manager));
			results.Add(TestPendingBiographyGenerationFailureAllowsNextBiographyCapture(manager));
			results.Add(TestPendingBiographyEventNpcResolutionDoesNotFabricateContext(manager));
			results.Add(TestAcceptQuestProgramPreparesNpcPresenceForActivation(manager));
			results.Add(TestAcceptQuestProgramDoesNotStageFutureNodeNpcBeforeItIsCurrent(manager));
			results.Add(TestQuestProgramSceneTransitionStagesNextNodeNpcWhenItBecomesCurrent(manager));
			results.Add(TestQuestProgramChoiceStagesOnlySelectedBranchNpc(manager));
			results.Add(TestMainlineBeatCompletionDoesNotGenerateFollowupBeat(manager));
			results.Add(TestMainlineWorldControlPrefersHigherPriorityProgram(manager));
		}
		catch (Exception ex)
		{
			results.Add(new TestCaseResult
			{
				Name = "UnhandledException",
				Passed = false,
				Details = ex.ToString()
			});
		}
		finally
		{
			RestoreState(manager, backup);
			SetPersistenceOverrides(backup.SavePathOverride, backup.HistoryPathOverride);
			try
			{
				if (Directory.Exists(tempDir))
				{
					Directory.Delete(tempDir, recursive: true);
				}
			}
			catch (Exception ex2)
			{
				AIChatManager.logger.LogWarning((object)("[QuestSelfTest] Cleanup failed: " + ex2.Message));
			}
		}
		WriteReport(reportPath, results);
		int passedCount = results.Count((TestCaseResult r) => r.Passed);
		summary = $"Quest self-test passed {passedCount}/{results.Count}";
		AIChatManager.logger.LogInfo((object)("[QuestSelfTest] " + summary));
		AIChatManager.logger.LogInfo((object)("[QuestSelfTest] Report: " + reportPath));
		foreach (TestCaseResult result in results.Where((TestCaseResult r) => !r.Passed))
		{
			AIChatManager.logger.LogWarning((object)("[QuestSelfTest] FAIL - " + result.Name + ": " + result.Details));
		}
		return results.All((TestCaseResult r) => r.Passed);
	}

	private static TestCaseResult TestOnDestroySkipsSaveBeforeLoad(AIQuestManager manager, string savePath, string historyPath)
	{
		ResetWorkingState(manager);
		File.WriteAllText(savePath, "[{\"QuestId\":\"sentinel-active\"}]", Encoding.UTF8);
		File.WriteAllText(historyPath, "[{\"QuestId\":\"sentinel-history\"}]", Encoding.UTF8);
		SetField(manager, "_hasLoadedPersistence", value: false);
		InvokeVoid(manager, "OnDestroy");
		bool saveUnchanged = File.ReadAllText(savePath, Encoding.UTF8) == "[{\"QuestId\":\"sentinel-active\"}]";
		bool historyUnchanged = File.ReadAllText(historyPath, Encoding.UTF8) == "[{\"QuestId\":\"sentinel-history\"}]";
		return new TestCaseResult
		{
			Name = "OnDestroySkipsSaveBeforeLoad",
			Passed = (saveUnchanged && historyUnchanged),
			Details = ((saveUnchanged && historyUnchanged) ? "OnDestroy did not overwrite quest files before persistence load" : "OnDestroy overwrote quest files before persistence load")
		};
	}

	private static TestCaseResult TestStructuredStoryLineParsesSpeakerMetadata()
	{
		JToken token = JObject.Parse("{ \"speakerType\": \"Player\", \"text\": \"我去看看。\" }");
		AIQuestStoryLine line = JsonConvert.DeserializeObject<AIQuestStoryLine>(token.ToString(), JsonSettings);
		bool ok = line != null && line.SpeakerType == QuestStorySpeakerType.Player && line.Text == "我去看看。" && line.SpeakerNpcId == 0;
		return new TestCaseResult
		{
			Name = "StructuredStoryLineParsesSpeakerMetadata",
			Passed = ok,
			Details = (ok ? "Structured story line token parsed speaker type and text" : string.Format("Parsed line mismatch: type={0}, text={1}, npcId={2}", line?.SpeakerType.ToString() ?? "null", line?.Text ?? "null", line?.SpeakerNpcId ?? (-1)))
		};
	}

	private static TestCaseResult TestStorySpeakerPresentationResolvesRuntimeRouting()
	{
		QuestProgram program = new QuestProgram
		{
			ProgramId = Guid.NewGuid().ToString("N"),
			Title = "StoryQuestProgram",
			AnchorNpcId = 900052,
			AnchorNpcName = "StoryNpc",
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 900052 }
			}
		};
		AIQuestStoryPresentation playerPresentation = ResolveQuestProgramStoryPresentationForTest(program, new AIQuestStoryLine
		{
			SpeakerType = QuestStorySpeakerType.Player,
			Text = "我知道了。"
		});
		AIQuestStoryPresentation narratorPresentation = ResolveQuestProgramStoryPresentationForTest(program, new AIQuestStoryLine
		{
			SpeakerType = QuestStorySpeakerType.Narrator,
			Text = "山风掠过，气氛渐沉。"
		});
		AIQuestStoryPresentation npcPresentation = ResolveQuestProgramStoryPresentationForTest(program, new AIQuestStoryLine
		{
			SpeakerType = QuestStorySpeakerType.Npc,
			SpeakerNpcId = 900052,
			Text = "此事便托付于你。"
		});
		bool ok = playerPresentation != null && narratorPresentation != null && npcPresentation != null && playerPresentation.CharacterId == 1 && playerPresentation.SpeakerName == "你" && playerPresentation.IsPlayerBubble && narratorPresentation.CharacterId == 0 && narratorPresentation.SpeakerName == "旁白" && narratorPresentation.IsNarrator && !narratorPresentation.IsPlayerBubble && npcPresentation.CharacterId == 900052 && npcPresentation.SpeakerName == "StoryNpc" && !npcPresentation.IsNarrator && !npcPresentation.IsPlayerBubble;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "StorySpeakerPresentationResolvesRuntimeRouting";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Speaker routing resolved player, narrator, and NPC presentation" : ("Presentation mismatch: player=" + DescribePresentation(playerPresentation) + ", narrator=" + DescribePresentation(narratorPresentation) + ", npc=" + DescribePresentation(npcPresentation)));
		return testCaseResult;
	}

	private static TestCaseResult TestLlmTornadoProviderInferenceFromEndpoint()
	{
		string openAi = BaseAIClient.ResolveProviderNameForTest("https://api.openai.com/v1/chat/completions");
		string deepSeek = BaseAIClient.ResolveProviderNameForTest("https://api.deepseek.com/chat/completions");
		string openRouter = BaseAIClient.ResolveProviderNameForTest("https://openrouter.ai/api/v1/chat/completions");
		string custom = BaseAIClient.ResolveProviderNameForTest("https://example.invalid/custom/chat/completions");
		bool ok = string.Equals(openAi, "OpenAi", StringComparison.Ordinal) && string.Equals(deepSeek, "DeepSeek", StringComparison.Ordinal) && string.Equals(openRouter, "OpenRouter", StringComparison.Ordinal) && string.Equals(custom, "Custom", StringComparison.Ordinal);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "LlmTornadoProviderInferenceFromEndpoint";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Endpoint inference resolves supported providers and falls back to Custom" : ("Unexpected provider mapping: openai=" + openAi + ", deepseek=" + deepSeek + ", openrouter=" + openRouter + ", custom=" + custom));
		return testCaseResult;
	}

	private static TestCaseResult TestLlmTornadoCompatibleEndpointPrefersCustomProvider()
	{
		MethodInfo method = typeof(BaseAIClient).GetMethod("ResolveProviderForConfiguration", BindingFlags.Static | BindingFlags.NonPublic);
		string qwenCompatible = method?.Invoke(null, new object[2] { "通义千问 (Qwen)", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions" })?.ToString();
		string geminiCompatible = method?.Invoke(null, new object[2] { "Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions" })?.ToString();
		bool ok = string.Equals(qwenCompatible, "Custom", StringComparison.Ordinal) && string.Equals(geminiCompatible, "Custom", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "LlmTornadoCompatibleEndpointPrefersCustomProvider",
			Passed = ok,
			Details = (ok ? "OpenAI-compatible endpoints bypass native provider routing and stay on Custom" : ("Unexpected compatible provider mapping: qwen=" + (qwenCompatible ?? "null") + ", gemini=" + (geminiCompatible ?? "null")))
		};
	}

	private static TestCaseResult TestLlmTornadoCompatiblePayloadSanitization()
	{
		JObject payload = JObject.Parse("{\r\n              \"model\": \"models/gemini-3.1-flash-lite-preview\",\r\n              \"frequency_penalty\": 0.3,\r\n              \"presence_penalty\": 0.2,\r\n              \"tools\": [\r\n                {\r\n                  \"type\": \"function\",\r\n                  \"ResolvedName\": \"FriendlyRequestItemTool\",\r\n                  \"ResolvedDescription\": \"Resolve desc\",\r\n                  \"function\": {\r\n                    \"name\": \"FriendlyRequestItemTool\",\r\n                    \"description\": \"Resolve desc\",\r\n                    \"parameters\": { \"type\": \"object\" }\r\n                  }\r\n                }\r\n              ]\r\n            }");
		BaseAIClient.SanitizeCompatibleOpenAiPayloadForTest(payload);
		bool ok = payload["frequency_penalty"] == null && payload["presence_penalty"] == null && payload["tools"] is JArray { Count: 1 } tools && tools[0]["ResolvedName"] == null && tools[0]["ResolvedDescription"] == null && string.Equals((string?)tools[0]["function"]?["name"], "FriendlyRequestItemTool", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "LlmTornadoCompatiblePayloadSanitization",
			Passed = ok,
			Details = (ok ? "Compatible OpenAI payload strips unsupported penalty and internal tool metadata fields" : ("Unexpected sanitized payload: " + payload.ToString(Formatting.None)))
		};
	}

	private static TestCaseResult TestGeminiCompatibleModelNameNormalization()
	{
		string fetchedModel = BaseAIClient.NormalizeFetchedModelIdForTest("https://generativelanguage.googleapis.com/v1beta/openai/models", "models/gemini-3.1-flash-lite-preview");
		string resolvedModel = ConfigManager.ResolveChatModelName("Google Gemini", "models/gemini-3.1-flash-lite-preview");
		bool ok = string.Equals(fetchedModel, "gemini-3.1-flash-lite-preview", StringComparison.Ordinal) && string.Equals(resolvedModel, "gemini-3.1-flash-lite-preview", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "GeminiCompatibleModelNameNormalization",
			Passed = ok,
			Details = (ok ? "Gemini OpenAI-compatible model ids strip the leading models/ prefix before UI display and request send" : ("Unexpected normalized model names: fetched=" + (fetchedModel ?? "null") + ", resolved=" + (resolvedModel ?? "null")))
		};
	}

	private static TestCaseResult TestCompatibilityProfileResolvesKnownProviders()
	{
		string gemini = BaseAIClient.ResolveCompatibilityProfileNameForTest("Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", "gemini-2.0-flash", "Auto");
		string groq = BaseAIClient.ResolveCompatibilityProfileNameForTest("Groq", "https://api.groq.com/openai/v1/chat/completions", "llama-3.3-70b-versatile", "Auto");
		string qwen = BaseAIClient.ResolveCompatibilityProfileNameForTest("通义千问 (Qwen)", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen-max", "Auto");
		string together = BaseAIClient.ResolveCompatibilityProfileNameForTest("Together AI", "https://api.together.xyz/v1/chat/completions", "meta-llama/Llama-3-70b-chat-hf", "Auto");
		string deepSeekReasoner = BaseAIClient.ResolveCompatibilityProfileNameForTest("DeepSeek", "https://api.deepseek.com/chat/completions", "deepseek-reasoner", "Auto");
		bool ok = string.Equals(gemini, "GeminiOpenAi", StringComparison.Ordinal) && string.Equals(groq, "GroqOpenAi", StringComparison.Ordinal) && string.Equals(qwen, "QwenCompatible", StringComparison.Ordinal) && string.Equals(together, "GenericOpenAiCompatible", StringComparison.Ordinal) && string.Equals(deepSeekReasoner, "DeepSeekReasoning", StringComparison.Ordinal);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "CompatibilityProfileResolvesKnownProviders";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Known presets and model-level quirks resolve to explicit compatibility profiles" : ("Unexpected profile mapping: gemini=" + (gemini ?? "null") + ", groq=" + (groq ?? "null") + ", qwen=" + (qwen ?? "null") + ", together=" + (together ?? "null") + ", deepseekReasoner=" + (deepSeekReasoner ?? "null")));
		return testCaseResult;
	}

	private static TestCaseResult TestQwenCompatibilityDisablesStreamWithTools()
	{
		ChatRequest request = new ChatRequest
		{
			Model = "qwen-max",
			Stream = true,
			Tools = new List<Tool>
			{
				new Tool
				{
					Type = "function",
					Function = new FunctionDefinition
					{
						Name = "FriendlyRequestItemTool",
						Description = "Test tool",
						Parameters = new
						{
							type = "object"
						}
					}
				}
			}
		};
		ChatRequest adjusted = BaseAIClient.NormalizeRequestForCompatibilityForTest("通义千问 (Qwen)", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "Auto", request);
		bool ok = adjusted != null && !adjusted.Stream && adjusted.Tools != null && adjusted.Tools.Count == 1;
		return new TestCaseResult
		{
			Name = "QwenCompatibilityDisablesStreamWithTools",
			Passed = ok,
			Details = (ok ? "Qwen compatibility profile downgrades tool-calling requests to non-streaming" : ("Unexpected Qwen request normalization: stream=" + (adjusted?.Stream.ToString() ?? "null") + ", tools=" + (adjusted?.Tools?.Count.ToString() ?? "null")))
		};
	}

	private static TestCaseResult TestCustomCompatibilityOverrideForcesAnthropicNative()
	{
		string profile = BaseAIClient.ResolveCompatibilityProfileNameForTest("自定义 (Custom)", "https://example.invalid/v1/messages", "claude-sonnet-4-20250514", "AnthropicNative");
		string transport = BaseAIClient.ResolveTransportKindNameForTest("自定义 (Custom)", "https://example.invalid/v1/messages", "claude-sonnet-4-20250514", "AnthropicNative");
		bool ok = string.Equals(profile, "AnthropicNative", StringComparison.Ordinal) && string.Equals(transport, "NativeTornado", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "CustomCompatibilityOverrideForcesAnthropicNative",
			Passed = ok,
			Details = (ok ? "Custom compatibility override can force a native provider transport plan" : ("Unexpected custom override resolution: profile=" + (profile ?? "null") + ", transport=" + (transport ?? "null")))
		};
	}

	private static TestCaseResult TestLlmTornadoRequestMappingPreservesMessagesAndTools()
	{
		ChatRequest request = new ChatRequest
		{
			Model = "deepseek-chat",
			MaxTokens = 256,
			Temperature = 0.6f,
			TopP = 0.8f,
			FrequencyPenalty = 0.1f,
			PresencePenalty = 0.2f,
			Tools = new List<Tool>
			{
				new Tool
				{
					Type = "function",
					Function = new FunctionDefinition
					{
						Name = "FriendlyRequestItemTool",
						Description = "Test tool",
						Parameters = new
						{
							type = "object",
							properties = new
							{
								item_name = new
								{
									type = "string"
								}
							}
						}
					}
				}
			},
			Messages = new List<ChatMessage>
			{
				new ChatMessage("system", "你是测试系统。"),
				new ChatMessage("user", "把明心丹给我。"),
				new ChatMessage("tool", "{\"status\":\"ok\"}", "call_1")
			}
		};
		LlmTornado.Chat.ChatRequest tornadoRequest = BaseAIClient.BuildTornadoRequestForTest(request);
		string mappedModel = tornadoRequest?.Model;
		bool ok = tornadoRequest != null && string.Equals(mappedModel, "deepseek-chat", StringComparison.Ordinal) && tornadoRequest.MaxTokens == 256 && !tornadoRequest.FrequencyPenalty.HasValue && !tornadoRequest.PresencePenalty.HasValue && tornadoRequest.Messages != null && tornadoRequest.Messages.Count == 3 && tornadoRequest.Messages[0].Role == ChatMessageRoles.System && tornadoRequest.Messages[1].Role == ChatMessageRoles.User && tornadoRequest.Messages[2].Role == ChatMessageRoles.Tool && string.Equals(tornadoRequest.Messages[2].ToolCallId, "call_1", StringComparison.Ordinal) && tornadoRequest.Tools != null && tornadoRequest.Tools.Count == 1 && string.Equals(tornadoRequest.Tools[0].Function?.Name, "FriendlyRequestItemTool", StringComparison.Ordinal);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "LlmTornadoRequestMappingPreservesMessagesAndTools";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "ChatRequest maps into Tornado request without losing roles or tools, and omits unsupported penalty fields" : ("Unexpected Tornado request: model=" + (mappedModel ?? "null") + ", freq=" + (tornadoRequest?.FrequencyPenalty?.ToString() ?? "null") + ", presence=" + (tornadoRequest?.PresencePenalty?.ToString() ?? "null") + ", roles=" + DescribeTornadoRoles(tornadoRequest?.Messages) + ", tool=" + (tornadoRequest?.Tools?.FirstOrDefault()?.Function?.Name ?? "null")));
		return testCaseResult;
	}

	private static TestCaseResult TestLlmTornadoChatResultMapsToOpenAiResponse()
	{
		ChatResult chatResult = new ChatResult
		{
			Id = "chatcmpl_selftest",
			Choices = new List<ChatChoice>
			{
				new ChatChoice
				{
					Index = 0,
					Message = new LlmTornado.Chat.ChatMessage(ChatMessageRoles.Assistant, "药已经带来了。")
					{
						ToolCalls = new List<LlmTornado.ChatFunctions.ToolCall>
						{
							new LlmTornado.ChatFunctions.ToolCall
							{
								Id = "call_1",
								Type = "function",
								FunctionCall = new LlmTornado.ChatFunctions.FunctionCall
								{
									Name = "FriendlyRequestItemTool",
									Arguments = "{\"item_name\":\"明心丹\"}"
								}
							}
						}
					}
				}
			},
			Usage = new ChatUsage(LLmProviders.OpenAi)
			{
				PromptTokenDetails = new ChatPromptTokenDetails
				{
					TextTokens = 21
				},
				CompletionTokens = 34
			}
		};
		OpenAIResponse response = BaseAIClient.MapChatResultToOpenAIResponseForTest(chatResult, "gpt-4o");
		bool ok = response != null && string.Equals(response.Id, "chatcmpl_selftest", StringComparison.Ordinal) && string.Equals(response.Model, "gpt-4o", StringComparison.Ordinal) && response.Choices != null && response.Choices.Count == 1 && string.Equals(response.Choices[0].Message?.Content, "药已经带来了。", StringComparison.Ordinal) && response.Choices[0].Message?.ToolCalls != null && response.Choices[0].Message.ToolCalls.Count == 1 && string.Equals(response.Choices[0].Message.ToolCalls[0].Function?.Name, "FriendlyRequestItemTool", StringComparison.Ordinal) && response.Usage != null && response.Usage.PromptTokens == 21 && response.Usage.CompletionTokens == 34 && response.Usage.ResolvedTotalTokens == 55;
		return new TestCaseResult
		{
			Name = "LlmTornadoChatResultMapsToOpenAiResponse",
			Passed = ok,
			Details = (ok ? "Tornado chat results convert back into the existing OpenAIResponse contract including tool calls and token usage" : ("Unexpected mapped response: " + JsonConvert.SerializeObject(response, JsonSettings)))
		};
	}

	private static TestCaseResult TestQuestProgramPersistenceRoundTrips()
	{
		QuestProgramState state = new QuestProgramState
		{
			SavedAt = new DateTime(2, 7, 25),
			Programs = new List<QuestProgram>
			{
				new QuestProgram
				{
					ProgramId = "program_mainline_001",
					Title = "药经之躁",
					TrackType = QuestTrackType.Mainline,
					CampaignId = "campaign_alpha",
					ChapterId = "chapter_1",
					BeatId = "beat_1",
					Status = QuestProgramStatus.Active,
					WorldContract = new QuestWorldContract
					{
						AllowedNpcIds = new List<int> { 20004 },
						AllowedSceneIds = new List<string> { "S116" }
					},
					Nodes = new List<QuestNode>
					{
						new QuestNode
						{
							NodeId = "node_1",
							Type = QuestNodeType.Objective,
							Title = "先去见井紫铃",
							ActivationPredicates = new List<QuestPredicateInvocation>
							{
								new QuestPredicateInvocation
								{
									Opcode = "time.compare",
									Params = new Dictionary<string, string>
									{
										["operator"] = ">=",
										["value"] = "0002-07-01 00:00:00"
									}
								}
							},
							OnActivated = new List<QuestCommandInvocation>
							{
								new QuestCommandInvocation
								{
									Opcode = "npc.set_action",
									Params = new Dictionary<string, string>
									{
										["npcId"] = "20004",
										["actionId"] = "46"
									}
								}
							}
						}
					},
					Runtime = new QuestRuntimeState
					{
						CurrentNodeId = "node_1",
						CompletedNodeIds = new List<string>(),
						ExecutedCommandKeys = new List<string> { "node_1:on_activated:0" }
					}
				}
			}
		};
		string json = QuestProgramPersistence.SerializeForTest(state);
		QuestProgramLoadResult loadResult = QuestProgramPersistence.DeserializeForTest(json);
		QuestProgram loaded = loadResult?.State?.Programs?.FirstOrDefault();
		bool ok = loadResult != null && !loadResult.WasLegacyQuestDataWiped && loadResult.State != null && loadResult.State.Programs != null && loadResult.State.Programs.Count == 1 && loaded != null && string.Equals(loaded.ProgramId, "program_mainline_001", StringComparison.Ordinal) && loaded.TrackType == QuestTrackType.Mainline && loaded.WorldContract != null && loaded.WorldContract.AllowedNpcIds != null && loaded.WorldContract.AllowedNpcIds.SequenceEqual(new int[1] { 20004 }) && loaded.Nodes != null && loaded.Nodes.Count == 1 && loaded.Nodes[0].ActivationPredicates != null && loaded.Nodes[0].ActivationPredicates.Count == 1 && string.Equals(loaded.Nodes[0].ActivationPredicates[0].Opcode, "time.compare", StringComparison.Ordinal) && loaded.Runtime != null && string.Equals(loaded.Runtime.CurrentNodeId, "node_1", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "QuestProgramPersistenceRoundTrips",
			Passed = ok,
			Details = (ok ? "New quest program state round-trips through the persistence envelope without falling back to legacy quest fields" : ("Unexpected quest program load result: " + DescribeQuestProgramLoadResult(loadResult)))
		};
	}

	private static TestCaseResult TestQuestProgramPersistenceWipesLegacyQuestSave()
	{
		string legacyJson = "{\r\n  \"SavedAt\": \"0002-07-25T00:00:00\",\r\n  \"ActiveQuests\": [\r\n    {\r\n      \"QuestId\": \"legacy_quest_001\",\r\n      \"Title\": \"旧任务\",\r\n      \"Status\": \"Active\"\r\n    }\r\n  ],\r\n  \"PendingQuests\": []\r\n}";
		QuestProgramLoadResult loadResult = QuestProgramPersistence.DeserializeForTest(legacyJson);
		bool ok = loadResult != null && loadResult.WasLegacyQuestDataWiped && loadResult.State != null && loadResult.State.Programs != null && loadResult.State.Programs.Count == 0;
		return new TestCaseResult
		{
			Name = "QuestProgramPersistenceWipesLegacyQuestSave",
			Passed = ok,
			Details = (ok ? "Legacy AI quest save roots are rejected and converted into a fresh quest program state" : ("Legacy save was not wiped as expected: " + DescribeQuestProgramLoadResult(loadResult)))
		};
	}

	private static TestCaseResult TestQuestCapabilityRegistryExposesMainlineOnlyOpcodes()
	{
		QuestCapabilityDescriptor logicAll = QuestCapabilityRegistry.GetPredicateDescriptorForTest("logic.all");
		QuestCapabilityDescriptor timeCompare = QuestCapabilityRegistry.GetPredicateDescriptorForTest("time.compare");
		QuestCapabilityDescriptor setAction = QuestCapabilityRegistry.GetCommandDescriptorForTest("npc.set_action");
		QuestCapabilityDescriptor loadScene = QuestCapabilityRegistry.GetCommandDescriptorForTest("scene.load");
		bool ok = logicAll != null && logicAll.Kind == QuestCapabilityKind.Predicate && logicAll.AllowedTracks.SequenceEqual(new QuestTrackType[1]) && (logicAll.ParameterSchema == null || !logicAll.ParameterSchema.ContainsKey("children")) && timeCompare != null && timeCompare.Kind == QuestCapabilityKind.Predicate && timeCompare.AllowedTracks.SequenceEqual(new QuestTrackType[1]) && setAction != null && setAction.Kind == QuestCapabilityKind.Command && setAction.AllowedTracks.SequenceEqual(new QuestTrackType[1]) && loadScene != null && loadScene.IsDangerous && loadScene.AllowedTracks.SequenceEqual(new QuestTrackType[1]);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "QuestCapabilityRegistryExposesMainlineOnlyOpcodes";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Capability registry now exposes only mainline-allowed opcodes, and logic.* no longer advertises children as a normal parameter" : ("Unexpected capability descriptors: logicAll=" + DescribeCapability(logicAll) + ", time=" + DescribeCapability(timeCompare) + ", action=" + DescribeCapability(setAction) + ", scene=" + DescribeCapability(loadScene)));
		return testCaseResult;
	}

	private static TestCaseResult TestCompatibleRequestFailureDiagnosticIncludesPayloadPreview()
	{
		string detail = BaseAIClient.FormatCompatibleRequestFailureForTest("QwenCompatible", "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", "qwen3.5-plus", "{\"model\":\"qwen3.5-plus\",\"messages\":[{\"role\":\"user\",\"content\":\"你好\"}]}", new TaskCanceledException("A task was canceled."));
		bool ok = detail.Contains("profile=QwenCompatible") && detail.Contains("endpoint=https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions") && detail.Contains("model=qwen3.5-plus") && detail.Contains("exceptionType=TaskCanceledException") && detail.Contains("payloadPreview=") && detail.Contains("timeoutSeconds=600");
		return new TestCaseResult
		{
			Name = "CompatibleRequestFailureDiagnosticIncludesPayloadPreview",
			Passed = ok,
			Details = (ok ? "Compatible request failure diagnostics include transport context, exception type, and payload preview" : ("Unexpected compatible request failure diagnostic: " + detail))
		};
	}

	private static TestCaseResult TestMainlineGenerationFailureDiagnosticIncludesStageAndResponsePreview()
	{
		string detail = AIQuestManager.FormatMainlineGenerationFailureForTest("当前主线功能规划", 2, 3, "模型返回了一个过长的 worldContract 草稿，但缺少 selectedCommands", new InvalidOperationException("selectedCommands missing"));
		bool ok = detail.Contains("stage=当前主线功能规划") && detail.Contains("step=2/3") && detail.Contains("responsePreview=") && detail.Contains("selectedCommands missing") && detail.Contains("exceptionType=InvalidOperationException");
		return new TestCaseResult
		{
			Name = "MainlineGenerationFailureDiagnosticIncludesStageAndResponsePreview",
			Passed = ok,
			Details = (ok ? "Mainline generation failures now include stage, step index, response preview, and exception type" : ("Unexpected mainline generation failure diagnostic: " + detail))
		};
	}

	private static TestCaseResult TestOfficialNpcPatchesMatchOriginalParameterNames()
	{
		MethodInfo setNpcAction = typeof(NPCEx).GetMethod("SetNPCAction", BindingFlags.Static | BindingFlags.Public, null, new Type[2]
		{
			typeof(int),
			typeof(int)
		}, null);
		MethodInfo addFavor = typeof(NPCEx).GetMethod("AddFavor", BindingFlags.Static | BindingFlags.Public, null, new Type[4]
		{
			typeof(int),
			typeof(int),
			typeof(bool),
			typeof(bool)
		}, null);
		MethodInfo setNpcActionPrefix = typeof(OfficialNpcActionChangedPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
		MethodInfo setNpcActionPostfix = typeof(OfficialNpcActionChangedPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
		MethodInfo addFavorPrefix = typeof(OfficialNpcFavorChangedPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
		MethodInfo addFavorPostfix = typeof(OfficialNpcFavorChangedPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
		bool ok = setNpcAction != null && addFavor != null && setNpcActionPrefix != null && setNpcActionPostfix != null && addFavorPrefix != null && addFavorPostfix != null && string.Equals(setNpcAction.GetParameters()[0].Name, "npcid", StringComparison.Ordinal) && string.Equals(setNpcActionPrefix.GetParameters()[0].Name, "npcid", StringComparison.Ordinal) && string.Equals(setNpcActionPostfix.GetParameters()[0].Name, "npcid", StringComparison.Ordinal) && string.Equals(setNpcActionPostfix.GetParameters()[1].Name, "actionID", StringComparison.Ordinal) && string.Equals(addFavor.GetParameters()[0].Name, "npcid", StringComparison.Ordinal) && string.Equals(addFavorPrefix.GetParameters()[0].Name, "npcid", StringComparison.Ordinal) && string.Equals(addFavorPostfix.GetParameters()[0].Name, "npcid", StringComparison.Ordinal);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "OfficialNpcPatchesMatchOriginalParameterNames";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Harmony patch parameter names now match NPCEx original signatures, so startup patch binding will succeed" : ("Unexpected patch parameter names: SetNPCAction=" + DescribeMethodParameters(setNpcAction) + ", Prefix=" + DescribeMethodParameters(setNpcActionPrefix) + ", Postfix=" + DescribeMethodParameters(setNpcActionPostfix) + ", AddFavor=" + DescribeMethodParameters(addFavor) + ", AddFavorPrefix=" + DescribeMethodParameters(addFavorPrefix) + ", AddFavorPostfix=" + DescribeMethodParameters(addFavorPostfix)));
		return testCaseResult;
	}

	private static TestCaseResult TestOfficialBiographyInterceptionPatchesCoverSourceMethods()
	{
		Type[] patchTypes = new Type[14]
		{
			typeof(OfficialBiographyUseDanYaoInterceptionPatch),
			typeof(OfficialBiographySmallTuPoInterceptionPatch),
			typeof(OfficialBiographyLianDanInterceptionPatch),
			typeof(OfficialBiographyLianQiInterceptionPatch),
			typeof(OfficialBiographyQiYuInterceptionPatch),
			typeof(OfficialBiographyImportantEventInterceptionPatch),
			typeof(OfficialBiographyPaiMaiInterceptionPatch),
			typeof(OfficialBiographyTianJiDaBiInterceptionPatch),
			typeof(OfficialBiographyZhuJiInterceptionPatch),
			typeof(OfficialBiographyJinDanInterceptionPatch),
			typeof(OfficialBiographyYuanYingInterceptionPatch),
			typeof(OfficialBiographyHuaShenInterceptionPatch),
			typeof(OfficialBiographyJieShaInterceptionPatch),
			typeof(OfficialBiographyLunDaoInterceptionPatch)
		};
		string[] expectedTargets = new string[14]
		{
			"NPCUseItem.UseDanYao", "NPCTuPo.NpcSmallToPo", "NPCFuYe.NpcLianDan", "NpcJieSuanManager.AddNpcEquip", "NPCLiLian.NPCYouLi", "NpcJieSuanManager.CheckImportantEvent", "NPCTeShu.NextNpcPaiMai", "TianJiDaBiManager.AddMatchPlayerEvent", "NPCTuPo.NpcTuPoZhuJi", "NPCTuPo.NpcTuPoJinDan",
			"NPCTuPo.NpcTuPoYuanYing", "NPCTuPo.NpcTuPoHuaShen", "NPCTeShu.NextJieSha", "NPCXiuLian.NextNpcLunDao"
		};
		HashSet<string> actualTargets = new HashSet<string>(StringComparer.Ordinal);
		Type[] array = patchTypes;
		foreach (Type patchType in array)
		{
			foreach (HarmonyPatch attribute in patchType.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).OfType<HarmonyPatch>())
			{
				if (!(((HarmonyAttribute)attribute).info.declaringType == null) && !string.IsNullOrWhiteSpace(((HarmonyAttribute)attribute).info.methodName))
				{
					actualTargets.Add(((HarmonyAttribute)attribute).info.declaringType.Name + "." + ((HarmonyAttribute)attribute).info.methodName);
				}
			}
		}
		bool ok = expectedTargets.All(actualTargets.Contains);
		return new TestCaseResult
		{
			Name = "OfficialBiographyInterceptionPatchesCoverSourceMethods",
			Passed = ok,
			Details = (ok ? "Official biography interception patches now cover the configured upstream source methods instead of only a partial subset" : ("Missing interception patches: " + string.Join(", ", expectedTargets.Where((string target) => !actualTargets.Contains(target)))))
		};
	}

	private static TestCaseResult TestOfficialBiographyInterceptionPatchesAreRegistered()
	{
		(Type, MethodBase, string)[] checks = new(Type, MethodBase, string)[14]
		{
			(typeof(OfficialBiographyUseDanYaoInterceptionPatch), AccessTools.Method(typeof(NPCUseItem), "UseDanYao", new Type[3]
			{
				typeof(int),
				typeof(JSONObject),
				typeof(int)
			}, (Type[])null), "NPCUseItem.UseDanYao"),
			(typeof(OfficialBiographySmallTuPoInterceptionPatch), AccessTools.Method(typeof(NPCTuPo), "NpcSmallToPo", new Type[2]
			{
				typeof(JSONObject),
				typeof(int)
			}, (Type[])null), "NPCTuPo.NpcSmallToPo"),
			(typeof(OfficialBiographyLianDanInterceptionPatch), AccessTools.Method(typeof(NPCFuYe), "NpcLianDan", new Type[1] { typeof(int) }, (Type[])null), "NPCFuYe.NpcLianDan"),
			(typeof(OfficialBiographyLianQiInterceptionPatch), AccessTools.Method(typeof(NpcJieSuanManager), "AddNpcEquip", new Type[3]
			{
				typeof(int),
				typeof(int),
				typeof(bool)
			}, (Type[])null), "NpcJieSuanManager.AddNpcEquip"),
			(typeof(OfficialBiographyQiYuInterceptionPatch), AccessTools.Method(typeof(NPCLiLian), "NPCYouLi", new Type[1] { typeof(int) }, (Type[])null), "NPCLiLian.NPCYouLi"),
			(typeof(OfficialBiographyImportantEventInterceptionPatch), AccessTools.Method(typeof(NpcJieSuanManager), "CheckImportantEvent", new Type[1] { typeof(string) }, (Type[])null), "NpcJieSuanManager.CheckImportantEvent"),
			(typeof(OfficialBiographyPaiMaiInterceptionPatch), AccessTools.Method(typeof(NPCTeShu), "NextNpcPaiMai", Type.EmptyTypes, (Type[])null), "NPCTeShu.NextNpcPaiMai"),
			(typeof(OfficialBiographyTianJiDaBiInterceptionPatch), AccessTools.Method(typeof(TianJiDaBiManager), "AddMatchPlayerEvent", new Type[2]
			{
				typeof(Match),
				typeof(bool)
			}, (Type[])null), "TianJiDaBiManager.AddMatchPlayerEvent"),
			(typeof(OfficialBiographyZhuJiInterceptionPatch), AccessTools.Method(typeof(NPCTuPo), "NpcTuPoZhuJi", new Type[2]
			{
				typeof(int),
				typeof(bool)
			}, (Type[])null), "NPCTuPo.NpcTuPoZhuJi"),
			(typeof(OfficialBiographyJinDanInterceptionPatch), AccessTools.Method(typeof(NPCTuPo), "NpcTuPoJinDan", new Type[2]
			{
				typeof(int),
				typeof(bool)
			}, (Type[])null), "NPCTuPo.NpcTuPoJinDan"),
			(typeof(OfficialBiographyYuanYingInterceptionPatch), AccessTools.Method(typeof(NPCTuPo), "NpcTuPoYuanYing", new Type[2]
			{
				typeof(int),
				typeof(bool)
			}, (Type[])null), "NPCTuPo.NpcTuPoYuanYing"),
			(typeof(OfficialBiographyHuaShenInterceptionPatch), AccessTools.Method(typeof(NPCTuPo), "NpcTuPoHuaShen", new Type[2]
			{
				typeof(int),
				typeof(bool)
			}, (Type[])null), "NPCTuPo.NpcTuPoHuaShen"),
			(typeof(OfficialBiographyJieShaInterceptionPatch), AccessTools.Method(typeof(NPCTeShu), "NextJieSha", Type.EmptyTypes, (Type[])null), "NPCTeShu.NextJieSha"),
			(typeof(OfficialBiographyLunDaoInterceptionPatch), AccessTools.Method(typeof(NPCXiuLian), "NextNpcLunDao", Type.EmptyTypes, (Type[])null), "NPCXiuLian.NextNpcLunDao")
		};
		List<string> missingRegistrations = new List<string>();
		(Type, MethodBase, string)[] array = checks;
		for (int i = 0; i < array.Length; i++)
		{
			var (patchType, originalMethod, label) = array[i];
			if (originalMethod == null)
			{
				missingRegistrations.Add(label + "(missing method)");
				continue;
			}
			Patches patchInfo = Harmony.GetPatchInfo(originalMethod);
			if (patchInfo == null || patchInfo.Prefixes?.Any((Patch prefix) => prefix.PatchMethod?.DeclaringType == patchType) != true)
			{
				missingRegistrations.Add(label);
			}
		}
		bool ok = missingRegistrations.Count == 0;
		return new TestCaseResult
		{
			Name = "OfficialBiographyInterceptionPatchesAreRegistered",
			Passed = ok,
			Details = (ok ? "Official biography interception patches are actively registered in Harmony, not just defined in code" : ("Unregistered interception patches: " + string.Join(", ", missingRegistrations)))
		};
	}

	private static string DescribeMethodParameters(MethodBase method)
	{
		if (method == null)
		{
			return "null";
		}
		return string.Join(", ", from parameter in method.GetParameters()
			select parameter.ParameterType.Name + " " + parameter.Name);
	}

	private static TestCaseResult TestProviderModelSettingsPersistSeparately()
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		ConfigFile originalConfig = GetConfigManagerConfigFile();
		string tempConfigPath = Path.Combine(Application.persistentDataPath, $"MCS_AIChatMod_ModelConfig_{Guid.NewGuid():N}.cfg");
		try
		{
			ConfigFile tempConfig = new ConfigFile(tempConfigPath, true);
			ConfigManager.Initialize(tempConfig);
			ConfigManager.SetSavedModelName("DeepSeek", "deepseek-reasoner");
			ConfigManager.SetSavedModelName("OpenAI", "gpt-4.1");
			ConfigManager.SaveConfig();
			string content = (File.Exists(tempConfigPath) ? File.ReadAllText(tempConfigPath, Encoding.UTF8) : string.Empty);
			bool ok = string.Equals(ConfigManager.GetSavedModelName("DeepSeek"), "deepseek-reasoner", StringComparison.Ordinal) && string.Equals(ConfigManager.GetSavedModelName("OpenAI"), "gpt-4.1", StringComparison.Ordinal) && content.Contains("Model_DeepSeek") && content.Contains("Model_OpenAI") && content.Contains("deepseek-reasoner") && content.Contains("gpt-4.1");
			TestCaseResult testCaseResult = new TestCaseResult();
			testCaseResult.Name = "ProviderModelSettingsPersistSeparately";
			testCaseResult.Passed = ok;
			testCaseResult.Details = (ok ? "Provider-specific model names are persisted as separate config entries" : ("Unexpected provider model persistence state: deepseek=" + ConfigManager.GetSavedModelName("DeepSeek") + ", openai=" + ConfigManager.GetSavedModelName("OpenAI") + ", config=" + content));
			return testCaseResult;
		}
		finally
		{
			RestoreConfigManagerConfigFile(originalConfig);
			SafeDelete(tempConfigPath);
		}
	}

	private static TestCaseResult TestApplyProviderPresetRestoresProviderSpecificModel()
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		ConfigFile originalConfig = GetConfigManagerConfigFile();
		string tempConfigPath = Path.Combine(Application.persistentDataPath, $"MCS_AIChatMod_ModelPreset_{Guid.NewGuid():N}.cfg");
		try
		{
			ConfigFile tempConfig = new ConfigFile(tempConfigPath, true);
			ConfigManager.Initialize(tempConfig);
			ConfigManager.SetSavedModelName("DeepSeek", "deepseek-reasoner");
			ConfigManager.SetSavedModelName("OpenAI", "gpt-4.1");
			ConfigManager.ApplyProviderPreset("OpenAI");
			bool restoredOpenAi = string.Equals(ConfigManager.modelNameConfig.Value, "gpt-4.1", StringComparison.Ordinal);
			ConfigManager.ApplyProviderPreset("DeepSeek");
			bool restoredDeepSeek = string.Equals(ConfigManager.modelNameConfig.Value, "deepseek-reasoner", StringComparison.Ordinal);
			ConfigManager.ApplyProviderPreset("Google Gemini");
			bool defaultGemini = string.Equals(ConfigManager.modelNameConfig.Value, "gemini-2.0-flash", StringComparison.Ordinal);
			bool ok = restoredOpenAi && restoredDeepSeek && defaultGemini;
			return new TestCaseResult
			{
				Name = "ApplyProviderPresetRestoresProviderSpecificModel",
				Passed = ok,
				Details = (ok ? "Provider switching restores the last saved model for that provider and falls back to default when none is saved" : $"Unexpected restored models: openai={restoredOpenAi}, deepseek={restoredDeepSeek}, geminiCurrent={ConfigManager.modelNameConfig.Value}")
			};
		}
		finally
		{
			RestoreConfigManagerConfigFile(originalConfig);
			SafeDelete(tempConfigPath);
		}
	}

	private static TestCaseResult TestMainlineStoryBeatMessagesUseSingleBeatProtocol()
	{
		List<UINPCEventData> biographyEvents = (from index in Enumerable.Range(1, 8)
			select new UINPCEventData
			{
				EventTime = new DateTime(1, Math.Min(index, 12), 1),
				EventTimeStr = $"第{index}年{Math.Min(index, 12)}月",
				EventDesc = $"第{index}条生平"
			}).ToList();
		QuestElementPicker.PickedElements elements = new QuestElementPicker.PickedElements
		{
			NarrativeContext = new QuestElementPicker.NarrativeMaterialContext
			{
				ConcreteStoryItems = new List<QuestElementPicker.NarrativeConcreteItemCandidate>
				{
					new QuestElementPicker.NarrativeConcreteItemCandidate
					{
						ItemId = 3101,
						Name = "清心玉露丸",
						Effect = "安神定气，压住躁动药力",
						OfficialDescription = "丹气清润，可安抚心神躁动。",
						Quality = 5,
						Scarcity = "稀有"
					},
					new QuestElementPicker.NarrativeConcreteItemCandidate
					{
						ItemId = 4102,
						Name = "养脉散",
						Effect = "温养经脉，缓和灵力反冲",
						OfficialDescription = "散药入体后可温养经络，缓解灵力冲撞。",
						Quality = 4,
						Scarcity = "少见"
					}
				},
				TimeCandidates = new List<QuestElementPicker.TimeCandidate>
				{
					new QuestElementPicker.TimeCandidate
					{
						Label = "中期",
						Summary = "适合跨多年铺垫、调查、往返与关系演变",
						MinMonths = 145,
						MaxMonths = 504
					}
				}
			}
		};
		List<ChatMessage> messages = AIQuestPromptBuilder.BuildMainlineStoryBeatMessages(new NPCInfo
		{
			NpcID = 20004,
			Name = "井紫铃",
			Events = biographyEvents
		}, elements, null, new PendingBiographyEventState
		{
			EventId = "event_story_prompt",
			NpcId = 20004,
			NpcName = "井紫铃",
			BiographyEventKind = BiographyEventKind.QiYu,
			Summary = "井紫铃刚被拦下一条新的官方奇遇。",
			PlayerFacingHint = "这条奇遇会被改写成当前主线。"
		}, "此前你已帮她压过一次心火。");
		string combined = string.Join("\n", messages?.Select((ChatMessage message) => message?.Content ?? string.Empty) ?? Enumerable.Empty<string>());
		bool ok = messages != null && messages.Count >= 2 && combined.Contains("只返回一段完整剧情正文") && combined.Contains("不要返回 JSON") && combined.Contains("第一句就直接进入剧情") && combined.Contains("不要使用大括号") && combined.Contains("不要写字段名") && combined.Contains("不要把背景改写成列表") && combined.Contains("单生平单拍完结") && combined.Contains($"{3000} 字左右为目标") && combined.Contains($"最低不少于 {2500} 字") && combined.Contains("必须出现明确选择") && combined.Contains("每个分支都要写出独立后续") && combined.Contains("分支彼此独立，不汇合") && combined.Contains("每个分支都必须在这一拍内结束") && combined.Contains("官方生平事件可作为背景线索之一") && combined.Contains("NPC、场景、具体物品都只能使用素材库里已经给出的内容") && combined.Contains("优先参考候选池里的官方介绍") && combined.Contains("即使你知道游戏里还存在别的官方场景或官方物品，只要这次素材库没给出，也不要写") && combined.Contains("系统会负责让 NPC 到位") && combined.Contains("对话必须符合修仙界尊卑、门派、修为差距与暗流涌动的氛围") && !combined.Contains("===") && !combined.Contains("【") && !combined.Contains("\n- ") && combined.Contains("第8条生平") && !combined.Contains("第1条生平") && combined.Contains("更早还有 2 条生平") && !combined.Contains("scriptBlocks") && !combined.Contains("eventChainResolved") && !combined.Contains("任务链") && !combined.Contains("CollectItem") && !combined.Contains("ReachFavor") && !combined.Contains("TalkToNpc") && !combined.Contains("物品ID:") && combined.Contains("清心玉露丸") && combined.Contains("养脉散") && combined.Contains("官方介绍丹气清润，可安抚心神躁动。") && combined.Contains("中期") && !combined.Contains("短期数月") && !combined.Contains("中期数年") && !combined.Contains("一甲子内") && !combined.Contains("\"worldContract\"") && !combined.Contains("\"selectedPredicates\"") && !combined.Contains("\"nodes\"") && !combined.Contains("dialogueBlocks");
		return new TestCaseResult
		{
			Name = "MainlineStoryBeatMessagesUseSingleBeatProtocol",
			Passed = ok,
			Details = (ok ? "Stage-1 prompt now emits pure single-beat narrative requirements instead of scriptBlock schema" : ("Unexpected stage-1 story-beat prompt: " + combined))
		};
	}

	private static TestCaseResult TestMainlineStoryBeatRepairMessagesAvoidStructuredReplay()
	{
		QuestElementPicker.PickedElements elements = new QuestElementPicker.PickedElements
		{
			NarrativeContext = new QuestElementPicker.NarrativeMaterialContext()
		};
		List<ChatMessage> messages = AIQuestPromptBuilder.BuildMainlineStoryBeatRepairMessages(new NPCInfo
		{
			NpcID = 20004,
			Name = "井紫铃"
		}, elements, null, new PendingBiographyEventState
		{
			EventId = "event_story_repair",
			NpcId = 20004,
			NpcName = "井紫铃",
			BiographyEventKind = BiographyEventKind.QiYu,
			Summary = "井紫铃刚被拦下一条新的官方奇遇。",
			PlayerFacingHint = "这条奇遇会被改写成当前主线。"
		}, "此前你已帮她压过一次心火。", "{\n  \"title\": \"错误正文\",\n  \"scriptBlocks\": []\n}", "剧情正文必须是纯文本，不能返回 JSON、Markdown 围栏或提纲");
		string combined = string.Join("\n", messages?.Select((ChatMessage message) => message?.Content ?? string.Empty) ?? Enumerable.Empty<string>());
		bool ok = messages != null && messages.Count == 3 && messages.All((ChatMessage message) => !string.Equals(message?.Role, "assistant", StringComparison.Ordinal)) && !combined.Contains("\"scriptBlocks\"") && !combined.Contains("\"title\"") && combined.Contains("上一版出现了结构化输出") && combined.Contains("不要使用大括号") && combined.Contains("不要写字段名");
		return new TestCaseResult
		{
			Name = "MainlineStoryBeatRepairMessagesAvoidStructuredReplay",
			Passed = ok,
			Details = (ok ? "Stage-1 repair prompt now restarts from a clean prose brief instead of replaying the model's structured output" : ("Unexpected stage-1 repair prompt: " + combined))
		};
	}

	private static TestCaseResult TestMainlineBiographySummaryPrunesOlderEvents()
	{
		List<UINPCEventData> events = (from index in Enumerable.Range(1, 9)
			select new UINPCEventData
			{
				EventTime = new DateTime(1, Math.Min(index, 12), 1),
				EventTimeStr = $"第{index}年{Math.Min(index, 12)}月",
				EventDesc = $"生平事件{index}"
			}).ToList();
		string summary = AIQuestPromptBuilder.BuildMainlineBiographySummaryForTest(events);
		bool ok = !string.IsNullOrWhiteSpace(summary) && summary.Contains("生平事件9") && summary.Contains("生平事件4") && !summary.Contains("生平事件3") && summary.Contains("更早还有 3 条生平");
		return new TestCaseResult
		{
			Name = "MainlineBiographySummaryPrunesOlderEvents",
			Passed = ok,
			Details = (ok ? "Mainline biography summary now keeps only the most recent major events and compresses older history" : ("Unexpected mainline biography summary: " + summary))
		};
	}

	private static TestCaseResult TestMainlineDialogSummaryPrunesAndFiltersNoise()
	{
		string summary = AIQuestPromptBuilder.BuildMainlineDialogSummaryForTest(new string[7] { "NPC|||你好", "玩家|||嗯", "NPC|||你上次替我挡下那枚烈性丹，我一直记着。", "玩家|||先说重点，你现在的经脉到底伤到什么地步？", "NPC|||若再强冲一次，我这条命大概也就到头了。", "玩家|||那就别再拿自己的道途赌气。", "NPC|||好。可若宗门再逼我，我未必还能忍住。" });
		bool ok = !string.IsNullOrWhiteSpace(summary) && !summary.Contains("你好") && !summary.Contains("- 玩家: 嗯") && summary.Contains("你上次替我挡下那枚烈性丹") && summary.Contains("先说重点，你现在的经脉到底伤到什么地步") && summary.Contains("更早还有 2 条互动");
		return new TestCaseResult
		{
			Name = "MainlineDialogSummaryPrunesAndFiltersNoise",
			Passed = ok,
			Details = (ok ? "Mainline dialog summary now filters greeting noise and keeps only recent substantive exchanges" : ("Unexpected mainline dialog summary: " + summary))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanningMessagesUseFunctionalProtocol()
	{
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_1",
			Title = "神兵阁问火",
			Summary = "在神兵阁与井紫铃长谈",
			TimeIntent = new BeatTimeIntent
			{
				TimeBand = "短期",
				Urgency = "高",
				MaxSpanMonths = 144
			},
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "S116" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_1",
						Kind = MainlineScriptBlockKind.Dialogue,
						SceneId = "S116",
						ScenePurpose = "确认道途危机",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("我若压不住这团火，别说长生，连丹道都要断。", 20004) }
					}
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S116",
					Name = "竹山宗神兵阁"
				}
			},
			ConcreteItems = new List<QuestElementPicker.ConcreteItemOption>
			{
				new QuestElementPicker.ConcreteItemOption
				{
					ItemId = 3101,
					Name = "清心玉露丸",
					Quality = 5,
					Effect = "安神定气，压住躁动药力"
				}
			}
		};
		List<ChatMessage> messages = AIQuestPromptBuilder.BuildMainlineBeatGamePlanningMessages(new NPCInfo
		{
			NpcID = 20004,
			Name = "井紫铃"
		}, beat, executionContext);
		string combined = string.Join("\n", messages?.Select((ChatMessage message) => message?.Content ?? string.Empty) ?? Enumerable.Empty<string>());
		bool ok = messages != null && messages.Count >= 2 && combined.Contains("你只返回一个 JSON 对象") && combined.Contains("\"stage\": \"stage3_semantic_contract\"") && combined.Contains("\"ContractId\": \"stage3_semantic_contract\"") && combined.Contains("\"Fields\"") && combined.Contains("\"Rules\"") && combined.Contains("\"OutputTemplate\"") && combined.Contains("flowSteps") && combined.Contains("effects") && combined.Contains("encounter_at_scene") && combined.Contains("formal_talk_gate") && combined.Contains("present_choice") && combined.Contains("play_script_block") && combined.Contains("legalMaterials") && combined.Contains("npcNames") && combined.Contains("sceneNames") && combined.Contains("清心玉露丸") && combined.Contains("RequiredWhen") && combined.Contains("ContentRequirement") && !combined.Contains("selectedPredicates") && !combined.Contains("selectedCommands") && !combined.Contains("worldContract") && !combined.Contains("QuestProgram") && !combined.Contains("triggerPredicates") && !combined.Contains("bridge=") && !combined.Contains("dangerous") && !combined.Contains("usable_in_this_track") && !combined.Contains("tracks=") && !combined.Contains("delivery_check");
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanningMessagesUseFunctionalProtocol",
			Passed = ok,
			Details = (ok ? "Stage-3 prompt now emits a semantic-plan contract envelope instead of prose worldContract planning" : ("Unexpected stage-2 beat functional prompt: " + combined))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanRejectsPresentChoiceWithoutScriptBlockId(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"summary\": \"先展示分支选择。\",\n  \"flowSteps\": [\n    { \"stepRef\": \"赶到现场\", \"kind\": \"encounter_at_scene\", \"sceneName\": \"竹山宗神兵阁\", \"targetNpcName\": \"井紫铃\" },\n    { \"stepRef\": \"当场抉择\", \"kind\": \"present_choice\", \"branchRef\": \"上前应答\" }\n  ]\n}");
		bool threw = false;
		string error = null;
		try
		{
			InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
			{
				BeatId = "beat_choice_missing_block",
				Title = "测试",
				Summary = "测试",
				StoryDraft = new MainlineBeatStoryDraft
				{
					ScriptBlocks = new List<MainlineScriptBlock>
					{
						new MainlineScriptBlock
						{
							BlockRef = "block_choice",
							Kind = MainlineScriptBlockKind.Choice,
							SceneId = "S116",
							ScenePurpose = "测试",
							ParticipantNpcIds = new List<int> { 20004 },
							Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreatePlayer("如何应对？") },
							Choices = new List<MainlineDialogueChoiceDraft>
							{
								new MainlineDialogueChoiceDraft
								{
									ChoiceRef = "choice_a",
									Text = "上前应答",
									ConsequenceDirection = "测试"
								}
							}
						}
					}
				}
			});
		}
		catch (TargetInvocationException ex)
		{
			threw = true;
			error = ex.InnerException?.Message ?? ex.Message;
		}
		bool ok = threw && !string.IsNullOrWhiteSpace(error) && error.Contains("present_choice") && error.Contains("缺少 sourceBlockRef");
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanRejectsPresentChoiceWithoutScriptBlockId",
			Passed = ok,
			Details = (ok ? "Stage-3 parser now rejects present_choice steps that omit the required choice sourceBlockRef" : ("Unexpected stage-2 missing choice block result: " + (error ?? "<no error>")))
		};
	}

	private static TestCaseResult TestTimeCandidatePoolUsesRemainingLifeAndThreeBands()
	{
		MethodInfo method = typeof(QuestElementPicker).GetMethod("BuildTimeCandidatePool", BindingFlags.Static | BindingFlags.NonPublic);
		if (method == null)
		{
			return new TestCaseResult
			{
				Name = "TimeCandidatePoolUsesRemainingLifeAndThreeBands",
				Passed = false,
				Details = "BuildTimeCandidatePool method not found"
			};
		}
		NPCInfo npcInfo = new NPCInfo
		{
			NpcID = 0,
			Name = "井紫铃",
			Level = 7,
			ShouYuan = 120,
			Age = 240
		};
		List<QuestElementPicker.TimeCandidate> candidates = method.Invoke(null, new object[1] { npcInfo }) as List<QuestElementPicker.TimeCandidate>;
		QuestElementPicker.TimeCandidate shortCandidate = candidates?.FirstOrDefault((QuestElementPicker.TimeCandidate candidate) => string.Equals(candidate.Label, "短期", StringComparison.Ordinal));
		QuestElementPicker.TimeCandidate mediumCandidate = candidates?.FirstOrDefault((QuestElementPicker.TimeCandidate candidate) => string.Equals(candidate.Label, "中期", StringComparison.Ordinal));
		QuestElementPicker.TimeCandidate longCandidate = candidates?.FirstOrDefault((QuestElementPicker.TimeCandidate candidate) => string.Equals(candidate.Label, "长期", StringComparison.Ordinal));
		int remainingLifeMonths = (npcInfo.ShouYuan - npcInfo.Age / 12) * 12;
		bool ok = candidates != null && candidates.Count == 3 && shortCandidate != null && mediumCandidate != null && longCandidate != null && shortCandidate.MinMonths == 1 && shortCandidate.MaxMonths == Math.Max(1, (int)Math.Floor((double)remainingLifeMonths * 0.1)) && mediumCandidate.MinMonths == shortCandidate.MaxMonths + 1 && mediumCandidate.MaxMonths == Math.Max(mediumCandidate.MinMonths, (int)Math.Floor((double)remainingLifeMonths * 0.35)) && longCandidate.MinMonths == mediumCandidate.MaxMonths + 1 && longCandidate.MaxMonths == remainingLifeMonths;
		return new TestCaseResult
		{
			Name = "TimeCandidatePoolUsesRemainingLifeAndThreeBands",
			Passed = ok,
			Details = (ok ? "Time candidate pool now uses NPC remaining life to build short/medium/long ranges" : ("Unexpected time candidates: " + DescribeTimeCandidates(candidates)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationUsesCodeCompiler(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_compile",
			Title = "神兵阁问火",
			Summary = "在神兵阁与井紫铃会面并稳住局面",
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "S116" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_dialogue",
						Kind = MainlineScriptBlockKind.Dialogue,
						SceneId = "S116",
						ScenePurpose = "稳住局面",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine>
						{
							AIQuestStoryLine.CreateNpc("先别急着拿命去赌。", 20004),
							AIQuestStoryLine.CreatePlayer("把话说清楚，我先替你把局面稳下来。")
						}
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "S116" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_1",
					Kind = MainlineSemanticFlowStepKind.EncounterAtScene,
					SceneId = "S116",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_2",
					Kind = MainlineSemanticFlowStepKind.FormalTalkGate,
					SceneId = "S116",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_3",
					Kind = MainlineSemanticFlowStepKind.PlayScriptBlock,
					SourceBlockRef = "block_dialogue",
					Effects = new List<MainlineSemanticEffectStep>
					{
						new MainlineSemanticEffectStep
						{
							EffectRef = "effect_1",
							Kind = MainlineSemanticEffectStepKind.FavorChange,
							TargetNpcId = 20004,
							Payload = JObject.Parse("{ \"amount\": 10 }")
						}
					}
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S116",
					Name = "竹山宗神兵阁"
				}
			}
		};
		QuestProgram program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		bool ok = program != null && program.Nodes != null && program.Nodes.Count >= 4 && program.WorldContract != null && program.WorldContract.SelectedPredicates.Contains("logic.all") && program.WorldContract.SelectedPredicates.Contains("scene.is") && program.WorldContract.SelectedPredicates.Contains("npc.present_in_current_scene") && program.WorldContract.SelectedCommands.Contains("npc.set_action") && program.WorldContract.SelectedCommands.Contains("npc.add_favor") && program.Nodes[0].OnActivated.Any((QuestCommandInvocation command) => string.Equals(command.Opcode, "npc.set_action", StringComparison.Ordinal)) && program.Nodes.Any((QuestNode node) => node.Type == QuestNodeType.Narrative);
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationUsesCodeCompiler",
			Passed = ok,
			Details = (ok ? "Stage-3 compilation is now code-driven and auto-derives encounter predicates, commands, and worldContract" : ("Unexpected compiled program: " + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationCompilesAcquireAndDeliverInPerson(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_delivery_compile",
			Title = "当面交付",
			Summary = "先备药，再回神兵阁交给井紫铃。",
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "S116" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_delivery",
						Kind = MainlineScriptBlockKind.DeliveryPrompt,
						SceneId = "S116",
						ScenePurpose = "交付资源",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("东西带来了就当面给我。", 20004) }
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "S116" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_1",
					Kind = MainlineSemanticFlowStepKind.EncounterAtScene,
					SceneId = "S116",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_2",
					Kind = MainlineSemanticFlowStepKind.AcquireItemGate,
					Payload = JObject.Parse("{ \"itemName\": \"清心玉露丸\", \"count\": 2 }")
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_3",
					Kind = MainlineSemanticFlowStepKind.DeliverItemInPerson,
					SceneId = "S116",
					TargetNpcId = 20004,
					Payload = JObject.Parse("{ \"itemName\": \"清心玉露丸\", \"count\": 2 }")
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S116",
					Name = "竹山宗神兵阁"
				}
			},
			ConcreteItems = new List<QuestElementPicker.ConcreteItemOption>
			{
				new QuestElementPicker.ConcreteItemOption
				{
					ItemId = 3101,
					Name = "清心玉露丸",
					FunctionTag = "心境稳定资源"
				}
			}
		};
		QuestProgram program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		QuestNode acquireNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_2", StringComparison.Ordinal));
		QuestNode deliverNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_3", StringComparison.Ordinal));
		QuestPredicateInvocation deliverCompletion = deliverNode?.CompletionPredicates?.FirstOrDefault();
		QuestCommandInvocation consumeCommand = deliverNode?.OnCompleted?.FirstOrDefault((QuestCommandInvocation command) => string.Equals(command.Opcode, "player.add_item", StringComparison.Ordinal));
		string consumeCount;
		bool ok = program != null && acquireNode != null && acquireNode.CompletionPredicates.Any((QuestPredicateInvocation predicate) => string.Equals(predicate.Opcode, "player.has_item", StringComparison.Ordinal)) && acquireNode.Title.Contains("清心玉露丸") && !acquireNode.Title.Contains("心境稳定资源") && deliverNode != null && deliverCompletion != null && string.Equals(deliverCompletion.Opcode, "logic.all", StringComparison.Ordinal) && deliverCompletion.Children.Any((QuestPredicateInvocation predicate) => string.Equals(predicate.Opcode, "player.has_item", StringComparison.Ordinal)) && deliverCompletion.Children.Any((QuestPredicateInvocation predicate) => string.Equals(predicate.Opcode, "npc.talked_to_player", StringComparison.Ordinal)) && consumeCommand != null && consumeCommand.Params.TryGetValue("count", out consumeCount) && string.Equals(consumeCount, "-2", StringComparison.Ordinal) && program.WorldContract.SelectedPredicates.Contains("player.has_item") && program.WorldContract.SelectedPredicates.Contains("npc.talked_to_player") && program.WorldContract.SelectedCommands.Contains("player.add_item");
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationCompilesAcquireAndDeliverInPerson",
			Passed = ok,
			Details = (ok ? "Stage-3 compiler now turns acquire/deliver primitives into item checks, in-person talk gates, and item consumption" : ("Unexpected compiled delivery program: " + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationResolvesDecoratedConcreteItemNames(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_decorated_item_compile",
			Title = "功法抄本",
			Summary = "取得功法后当面交给井紫铃。",
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "S116" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_delivery",
						Kind = MainlineScriptBlockKind.DeliveryPrompt,
						SceneId = "S116",
						ScenePurpose = "交付功法抄本",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("若你真拿到了那卷功法，就亲手交给我。", 20004) }
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "S116" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_1",
					Kind = MainlineSemanticFlowStepKind.AcquireItemGate,
					Payload = JObject.Parse("{ \"itemName\": \"《 玉\\n云 功 》\", \"count\": 1 }")
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_2",
					Kind = MainlineSemanticFlowStepKind.DeliverItemInPerson,
					SceneId = "S116",
					TargetNpcId = 20004,
					Payload = JObject.Parse("{ \"itemName\": \"《 玉\\n云 功 》\", \"count\": 1 }")
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S116",
					Name = "竹山宗神兵阁"
				}
			},
			ConcreteItems = new List<QuestElementPicker.ConcreteItemOption>
			{
				new QuestElementPicker.ConcreteItemOption
				{
					ItemId = 4201,
					Name = "玉云功",
					FunctionTag = string.Empty
				}
			}
		};
		QuestProgram program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		QuestNode acquireNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_1", StringComparison.Ordinal));
		QuestNode deliverNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_2", StringComparison.Ordinal));
		bool ok = program != null && acquireNode != null && acquireNode.Title.Contains("玉云功") && !acquireNode.Title.Contains("《") && deliverNode != null && deliverNode.Summary.Contains("玉云功") && !deliverNode.Summary.Contains("《");
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationResolvesDecoratedConcreteItemNames",
			Passed = ok,
			Details = (ok ? "Stage-3 compiler now resolves decorated item mentions like 《玉云功》 back to the unique concrete item in the beat context" : ("Unexpected decorated-item compilation result: " + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationResolvesUniqueFuzzyConcreteItemNames(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_fuzzy_item_compile",
			Title = "药草交付",
			Summary = "取得药草后当面交给井紫铃。",
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "S116" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_delivery",
						Kind = MainlineScriptBlockKind.DeliveryPrompt,
						SceneId = "S116",
						ScenePurpose = "交付药草",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("若你真找到了那味药草，就亲手交给我。", 20004) }
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "S116" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_1",
					Kind = MainlineSemanticFlowStepKind.AcquireItemGate,
					Payload = JObject.Parse("{ \"itemName\": \"宁心草\", \"count\": 1 }")
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_2",
					Kind = MainlineSemanticFlowStepKind.DeliverItemInPerson,
					SceneId = "S116",
					TargetNpcId = 20004,
					Payload = JObject.Parse("{ \"itemName\": \"宁心草\", \"count\": 1 }")
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S116",
					Name = "竹山宗神兵阁"
				}
			},
			ConcreteItems = new List<QuestElementPicker.ConcreteItemOption>
			{
				new QuestElementPicker.ConcreteItemOption
				{
					ItemId = 5201,
					Name = "上品宁心草",
					FunctionTag = string.Empty
				}
			}
		};
		QuestProgram program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		QuestNode acquireNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_1", StringComparison.Ordinal));
		QuestNode deliverNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_2", StringComparison.Ordinal));
		bool ok = program != null && acquireNode != null && acquireNode.Title.Contains("上品宁心草") && deliverNode != null && deliverNode.Summary.Contains("上品宁心草");
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationResolvesUniqueFuzzyConcreteItemNames",
			Passed = ok,
			Details = (ok ? "Stage-3 compiler now resolves a uniquely matching concrete item even when the model uses a shortened item name" : ("Unexpected fuzzy-item compilation result: " + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationResolvesGlobalConcreteItemOutsideBeatContext(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		string globalItemName = (from item in _ItemJsonData.DataDict?.Values?.Where((_ItemJsonData item) => item != null && !string.IsNullOrWhiteSpace(item.name))
			select item.name.Trim()).FirstOrDefault((string name) => !QuestElementPicker.NarrativeEntityTextEquals(name, "清心玉露丸"));
		if (string.IsNullOrWhiteSpace(globalItemName))
		{
			return new TestCaseResult
			{
				Name = "MainlineBeatCompilationResolvesGlobalConcreteItemOutsideBeatContext",
				Passed = false,
				Details = "测试环境未能从官方物品总池中找到可用的具体物品样本"
			};
		}
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_global_item_compile",
			Title = "全局物品落地",
			Summary = "即使当前 beat 候选里没有，也应能从官方总池解析合法物品。",
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "S116" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_reward",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S116",
						ScenePurpose = "落地总池合法物品",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("这件东西你先拿去。", 20004) }
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "S116" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_1",
					Kind = MainlineSemanticFlowStepKind.PlayScriptBlock,
					SourceBlockRef = "block_reward",
					Effects = new List<MainlineSemanticEffectStep>
					{
						new MainlineSemanticEffectStep
						{
							EffectRef = "effect_global_item",
							Kind = MainlineSemanticEffectStepKind.GrantItem,
							Payload = JObject.Parse("{ \"itemName\": \"" + globalItemName.Replace("\"", "\\\"") + "\", \"count\": 1 }")
						}
					}
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S116",
					Name = "竹山宗神兵阁"
				}
			},
			ConcreteItems = new List<QuestElementPicker.ConcreteItemOption>
			{
				new QuestElementPicker.ConcreteItemOption
				{
					ItemId = 3101,
					Name = "清心玉露丸",
					FunctionTag = string.Empty
				}
			}
		};
		QuestProgram program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		QuestNode rewardNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_1", StringComparison.Ordinal));
		QuestCommandInvocation grantCommand = rewardNode?.OnCompleted?.FirstOrDefault((QuestCommandInvocation command) => string.Equals(command.Opcode, "player.add_item", StringComparison.Ordinal));
		bool ok = program != null && rewardNode != null && grantCommand != null && rewardNode.Summary.Contains(globalItemName);
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationResolvesGlobalConcreteItemOutsideBeatContext",
			Passed = ok,
			Details = (ok ? "Stage-3 compiler now falls back from beat-local concrete items to the official total item pool when resolving a legal item name" : ("Unexpected global-item compilation result: " + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationCompilesVisitAndReportBackInPerson(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_visit_report_compile",
			Title = "查探后回报",
			Summary = "先去云汐城踩点，再回藏经阁向井紫铃回报。",
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "S1381", "S114" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_report",
						Kind = MainlineScriptBlockKind.Dialogue,
						SceneId = "S114",
						ScenePurpose = "回报调查结果",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine>
						{
							AIQuestStoryLine.CreatePlayer("我去云汐城转了一圈，已经查清楚了。"),
							AIQuestStoryLine.CreateNpc("说。", 20004)
						}
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "S1381", "S114" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_visit",
					Kind = MainlineSemanticFlowStepKind.VisitSceneGate,
					SceneId = "S1381"
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_report",
					Kind = MainlineSemanticFlowStepKind.ReportBackInPerson,
					SceneId = "S114",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_script",
					Kind = MainlineSemanticFlowStepKind.PlayScriptBlock,
					SourceBlockRef = "block_report"
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S1381",
					Name = "云汐城"
				},
				new QuestElementPicker.PickedLocation
				{
					Id = "S114",
					Name = "竹山宗藏经阁"
				}
			}
		};
		QuestProgram program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		QuestNode visitNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_visit", StringComparison.Ordinal));
		QuestNode reportNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_report", StringComparison.Ordinal));
		QuestPredicateInvocation reportCompletion = reportNode?.CompletionPredicates?.FirstOrDefault();
		bool ok = program != null && visitNode != null && visitNode.CompletionPredicates.Count == 1 && string.Equals(visitNode.CompletionPredicates[0].Opcode, "scene.is", StringComparison.Ordinal) && reportNode != null && reportNode.ActivationPredicates.Count == 1 && string.Equals(reportNode.ActivationPredicates[0].Opcode, "logic.all", StringComparison.Ordinal) && reportNode.ActivationPredicates[0].Children.Any((QuestPredicateInvocation predicate) => string.Equals(predicate.Opcode, "scene.is", StringComparison.Ordinal)) && reportNode.ActivationPredicates[0].Children.Any((QuestPredicateInvocation predicate) => string.Equals(predicate.Opcode, "npc.present_in_current_scene", StringComparison.Ordinal)) && reportCompletion != null && string.Equals(reportCompletion.Opcode, "logic.all", StringComparison.Ordinal) && reportCompletion.Children.Any((QuestPredicateInvocation predicate) => string.Equals(predicate.Opcode, "scene.is", StringComparison.Ordinal)) && reportCompletion.Children.Any((QuestPredicateInvocation predicate) => string.Equals(predicate.Opcode, "npc.talked_to_player", StringComparison.Ordinal)) && program.WorldContract.SelectedPredicates.Contains("scene.is") && program.WorldContract.SelectedPredicates.Contains("npc.present_in_current_scene") && program.WorldContract.SelectedPredicates.Contains("npc.talked_to_player");
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationCompilesVisitAndReportBackInPerson",
			Passed = ok,
			Details = (ok ? "Stage-3 compiler now turns visit/report primitives into scene reachability and in-person report gates" : ("Unexpected compiled visit/report program: " + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationAssignsWorldControlContracts(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_world_control_compile",
			Title = "世界控制编译",
			Summary = "验证现有原语会被编译成显式的节点世界控制合同。",
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "147", "S114" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_dialogue",
						Kind = MainlineScriptBlockKind.Dialogue,
						SceneId = "S114",
						ScenePurpose = "当前拍中的正式交谈",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("你既然来了，就把话说清楚。", 20004) }
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "147", "S114" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_encounter",
					Kind = MainlineSemanticFlowStepKind.EncounterAtScene,
					SceneId = "147",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_visit",
					Kind = MainlineSemanticFlowStepKind.VisitSceneGate,
					SceneId = "S114"
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_talk",
					Kind = MainlineSemanticFlowStepKind.FormalTalkGate,
					SceneId = "S114",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_report",
					Kind = MainlineSemanticFlowStepKind.ReportBackInPerson,
					SceneId = "S114",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_acquire",
					Kind = MainlineSemanticFlowStepKind.AcquireItemGate,
					Payload = JObject.Parse("{ \"itemName\": \"清心玉露丸\", \"count\": 1 }")
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_deliver",
					Kind = MainlineSemanticFlowStepKind.DeliverItemInPerson,
					SceneId = "S114",
					TargetNpcId = 20004,
					Payload = JObject.Parse("{ \"itemName\": \"清心玉露丸\", \"count\": 1 }")
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_combat",
					Kind = MainlineSemanticFlowStepKind.CombatGate,
					SceneId = "S114",
					TargetNpcId = 20004
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "147",
					Name = "逸风城附近"
				},
				new QuestElementPicker.PickedLocation
				{
					Id = "S114",
					Name = "竹山宗藏经阁"
				}
			},
			ConcreteItems = new List<QuestElementPicker.ConcreteItemOption>
			{
				new QuestElementPicker.ConcreteItemOption
				{
					FunctionTag = "心境稳定资源",
					ItemId = 501,
					Name = "清心玉露丸"
				}
			}
		};
		QuestProgram program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		QuestNode encounterNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_encounter", StringComparison.Ordinal));
		QuestNode visitNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_visit", StringComparison.Ordinal));
		QuestNode talkNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_talk", StringComparison.Ordinal));
		QuestNode reportNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_report", StringComparison.Ordinal));
		QuestNode deliverNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_deliver", StringComparison.Ordinal));
		QuestNode combatNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_combat", StringComparison.Ordinal));
		bool ok = program != null && encounterNode?.WorldControl?.SceneId == "147" && encounterNode.WorldControl.NpcRequirements.Count == 1 && encounterNode.WorldControl.NpcRequirements[0].NpcId == 20004 && encounterNode.WorldControl.NpcRequirements[0].Mode == QuestNpcPresenceMode.Encounter && visitNode?.WorldControl == null && talkNode?.WorldControl?.SceneId == "S114" && talkNode.WorldControl.NpcRequirements.Count == 1 && talkNode.WorldControl.NpcRequirements[0].Mode == QuestNpcPresenceMode.FormalTalk && reportNode?.WorldControl?.SceneId == "S114" && reportNode.WorldControl.NpcRequirements.Count == 1 && reportNode.WorldControl.NpcRequirements[0].Mode == QuestNpcPresenceMode.ReportBack && deliverNode?.WorldControl?.SceneId == "S114" && deliverNode.WorldControl.NpcRequirements.Count == 1 && deliverNode.WorldControl.NpcRequirements[0].Mode == QuestNpcPresenceMode.Delivery && combatNode?.WorldControl?.SceneId == "S114" && combatNode.WorldControl.NpcRequirements.Count == 1 && combatNode.WorldControl.NpcRequirements[0].Mode == QuestNpcPresenceMode.Combat;
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationAssignsWorldControlContracts",
			Passed = ok,
			Details = (ok ? "Stage-3 compiler now attaches explicit node world-control contracts instead of relying on activation-predicate inference" : ("Unexpected world-control compilation: " + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationRoutesChoiceScopedFlowIntoIndependentBranches(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_choice_compile",
			Title = "分支后续",
			Summary = "不同选择导向不同后续段落，并在各自分支内结束。",
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "S116" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_choice",
						Kind = MainlineScriptBlockKind.Choice,
						SceneId = "S116",
						ScenePurpose = "你决定如何回应井紫铃",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("你打算怎么选？", 20004) },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_a",
								Text = "先稳住她"
							},
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_b",
								Text = "直接逼问她"
							}
						}
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_a",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S116",
						ScenePurpose = "稳住后的回应",
						BranchRef = "choice_a",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("好，我先听你一句。", 20004) }
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_b",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S116",
						ScenePurpose = "逼问后的回应",
						BranchRef = "choice_b",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("你倒是问得狠。", 20004) }
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "S116" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_choice",
					Kind = MainlineSemanticFlowStepKind.PresentChoice,
					SourceBlockRef = "block_choice",
					SceneId = "S116",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_branch_a",
					Kind = MainlineSemanticFlowStepKind.PlayScriptBlock,
					SourceBlockRef = "block_a",
					BranchRef = "choice_a"
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_branch_b",
					Kind = MainlineSemanticFlowStepKind.PlayScriptBlock,
					SourceBlockRef = "block_b",
					BranchRef = "choice_b"
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S116",
					Name = "竹山宗神兵阁"
				}
			}
		};
		QuestProgram program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		QuestNode choiceNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_choice", StringComparison.Ordinal));
		QuestNode branchANode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_branch_a", StringComparison.Ordinal));
		QuestNode branchBNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_branch_b", StringComparison.Ordinal));
		QuestChoiceOption choiceA = choiceNode?.Narrative?.Choices?.FirstOrDefault((QuestChoiceOption choice) => string.Equals(choice.ChoiceId, "choice_a", StringComparison.Ordinal));
		QuestChoiceOption choiceB = choiceNode?.Narrative?.Choices?.FirstOrDefault((QuestChoiceOption choice) => string.Equals(choice.ChoiceId, "choice_b", StringComparison.Ordinal));
		QuestNode branchATerminal = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, branchANode?.NextNodeId, StringComparison.Ordinal));
		QuestNode branchBTerminal = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, branchBNode?.NextNodeId, StringComparison.Ordinal));
		bool ok = program != null && choiceNode != null && branchANode != null && branchBNode != null && string.Equals(choiceA?.NextNodeId, branchANode.NodeId, StringComparison.Ordinal) && string.Equals(choiceB?.NextNodeId, branchBNode.NodeId, StringComparison.Ordinal) && branchATerminal != null && branchATerminal.Type == QuestNodeType.Terminal && branchBTerminal != null && branchBTerminal.Type == QuestNodeType.Terminal && !string.Equals(branchANode.NextNodeId, branchBNode.NextNodeId, StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationRoutesChoiceScopedFlowIntoIndependentBranches",
			Passed = ok,
			Details = (ok ? "Stage-4 compiler now routes branchRef-scoped steps onto branch-specific subchains and terminates each branch independently" : ("Unexpected compiled branched program: " + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationRoutesChoiceScopedVisitAndReportIntoIndependentBranches(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_choice_visit_report",
			Title = "先查再报",
			Summary = "不同选择导向不同调查路径，并在各自分支内回报后结束。",
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "S1381", "S1312", "S114" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_choice",
						Kind = MainlineScriptBlockKind.Choice,
						SceneId = "S114",
						ScenePurpose = "你决定先去查哪一路线索",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("先查城里，还是先查坊市？", 20004) },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_city",
								Text = "先去云汐城"
							},
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_market",
								Text = "先去坊市"
							}
						}
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "S1381", "S1312", "S114" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_choice",
					Kind = MainlineSemanticFlowStepKind.PresentChoice,
					SourceBlockRef = "block_choice",
					SceneId = "S114",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_visit_city",
					Kind = MainlineSemanticFlowStepKind.VisitSceneGate,
					SceneId = "S1381",
					BranchRef = "choice_city"
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_report_city",
					Kind = MainlineSemanticFlowStepKind.ReportBackInPerson,
					SceneId = "S114",
					TargetNpcId = 20004,
					BranchRef = "choice_city"
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_visit_market",
					Kind = MainlineSemanticFlowStepKind.VisitSceneGate,
					SceneId = "S1312",
					BranchRef = "choice_market"
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_report_market",
					Kind = MainlineSemanticFlowStepKind.ReportBackInPerson,
					SceneId = "S114",
					TargetNpcId = 20004,
					BranchRef = "choice_market"
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S1381",
					Name = "云汐城"
				},
				new QuestElementPicker.PickedLocation
				{
					Id = "S1312",
					Name = "坊市"
				},
				new QuestElementPicker.PickedLocation
				{
					Id = "S114",
					Name = "竹山宗藏经阁"
				}
			}
		};
		QuestProgram program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		QuestNode choiceNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_choice", StringComparison.Ordinal));
		QuestNode visitCityNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_visit_city", StringComparison.Ordinal));
		QuestNode visitMarketNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_visit_market", StringComparison.Ordinal));
		QuestNode reportCityNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_report_city", StringComparison.Ordinal));
		QuestNode reportMarketNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_report_market", StringComparison.Ordinal));
		QuestChoiceOption choiceCity = choiceNode?.Narrative?.Choices?.FirstOrDefault((QuestChoiceOption choice) => string.Equals(choice.ChoiceId, "choice_city", StringComparison.Ordinal));
		QuestChoiceOption choiceMarket = choiceNode?.Narrative?.Choices?.FirstOrDefault((QuestChoiceOption choice) => string.Equals(choice.ChoiceId, "choice_market", StringComparison.Ordinal));
		QuestNode cityTerminal = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, reportCityNode?.NextNodeId, StringComparison.Ordinal));
		QuestNode marketTerminal = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, reportMarketNode?.NextNodeId, StringComparison.Ordinal));
		bool ok = program != null && choiceNode != null && visitCityNode != null && visitMarketNode != null && reportCityNode != null && reportMarketNode != null && string.Equals(choiceCity?.NextNodeId, visitCityNode.NodeId, StringComparison.Ordinal) && string.Equals(choiceMarket?.NextNodeId, visitMarketNode.NodeId, StringComparison.Ordinal) && string.Equals(visitCityNode.NextNodeId, reportCityNode.NodeId, StringComparison.Ordinal) && string.Equals(visitMarketNode.NextNodeId, reportMarketNode.NodeId, StringComparison.Ordinal) && cityTerminal != null && cityTerminal.Type == QuestNodeType.Terminal && marketTerminal != null && marketTerminal.Type == QuestNodeType.Terminal;
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationRoutesChoiceScopedVisitAndReportIntoIndependentBranches",
			Passed = ok,
			Details = (ok ? "Choice-scoped visit/report primitives now stay inside their own branches and terminate independently" : ("Unexpected choice-scoped visit/report compilation: " + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationTerminatesEachChoiceBranchIndependently(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_choice_terminal",
			Title = "只做抉择",
			Summary = "玩家做出抉择后，各分支各自走到收束。",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_choice",
						Kind = MainlineScriptBlockKind.Choice,
						SceneId = "S116",
						ScenePurpose = "决定是否信她一次",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("你到底信不信我这一次？", 20004) },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_yes",
								Text = "我信"
							},
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_no",
								Text = "我不信"
							}
						}
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_yes",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S116",
						ScenePurpose = "信任后的收束",
						BranchRef = "choice_yes",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("既然你肯信，那我今日便把旧事都说给你听。", 20004) }
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_no",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S116",
						ScenePurpose = "拒绝后的收束",
						BranchRef = "choice_no",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("既然你不肯信，那这段话也到此为止。", 20004) }
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "S116" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_choice",
					Kind = MainlineSemanticFlowStepKind.PresentChoice,
					SourceBlockRef = "block_choice",
					SceneId = "S116",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_yes",
					Kind = MainlineSemanticFlowStepKind.PlayScriptBlock,
					SourceBlockRef = "block_yes",
					BranchRef = "choice_yes",
					Effects = new List<MainlineSemanticEffectStep>
					{
						new MainlineSemanticEffectStep
						{
							EffectRef = "effect_yes",
							Kind = MainlineSemanticEffectStepKind.FavorChange,
							TargetNpcId = 20004,
							Payload = JObject.Parse("{ \"amount\": 5 }")
						}
					}
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_no",
					Kind = MainlineSemanticFlowStepKind.PlayScriptBlock,
					SourceBlockRef = "block_no",
					BranchRef = "choice_no",
					Effects = new List<MainlineSemanticEffectStep>
					{
						new MainlineSemanticEffectStep
						{
							EffectRef = "effect_no",
							Kind = MainlineSemanticEffectStepKind.FavorChange,
							TargetNpcId = 20004,
							Payload = JObject.Parse("{ \"amount\": -5 }")
						}
					}
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S116",
					Name = "竹山宗神兵阁"
				}
			}
		};
		QuestProgram program = null;
		string error = null;
		try
		{
			program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		}
		catch (TargetInvocationException ex)
		{
			error = ex.InnerException?.Message ?? ex.Message;
		}
		QuestNode choiceNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_choice", StringComparison.Ordinal));
		QuestNode yesNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_yes", StringComparison.Ordinal));
		QuestNode noNode = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, "node_step_no", StringComparison.Ordinal));
		QuestChoiceOption choiceYes = choiceNode?.Narrative?.Choices?.FirstOrDefault((QuestChoiceOption choice) => string.Equals(choice.ChoiceId, "choice_yes", StringComparison.Ordinal));
		QuestChoiceOption choiceNo = choiceNode?.Narrative?.Choices?.FirstOrDefault((QuestChoiceOption choice) => string.Equals(choice.ChoiceId, "choice_no", StringComparison.Ordinal));
		QuestNode yesTerminal = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, yesNode?.NextNodeId, StringComparison.Ordinal));
		QuestNode noTerminal = program?.Nodes?.FirstOrDefault((QuestNode node) => string.Equals(node.NodeId, noNode?.NextNodeId, StringComparison.Ordinal));
		bool ok = string.IsNullOrWhiteSpace(error) && program != null && choiceNode != null && yesNode != null && noNode != null && string.Equals(choiceYes?.NextNodeId, yesNode.NodeId, StringComparison.Ordinal) && string.Equals(choiceNo?.NextNodeId, noNode.NodeId, StringComparison.Ordinal) && yesTerminal != null && yesTerminal.Type == QuestNodeType.Terminal && noTerminal != null && noTerminal.Type == QuestNodeType.Terminal && !string.Equals(yesNode.NextNodeId, noNode.NextNodeId, StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationTerminatesEachChoiceBranchIndependently",
			Passed = ok,
			Details = (ok ? "Choice-only beats now require branch-specific followups and terminate each choice path independently" : ("Unexpected choice terminal compilation result: error=" + (error ?? "<none>") + ", program=" + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompilationSynthesizesEncounterForNarrativeOnlyFlow(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineBeat beat = new MainlineBeat
		{
			BeatId = "beat_narrative_only",
			Title = "烈阳丹抉择",
			Summary = "井紫铃欲服烈阳丹，玩家提供两套方案。",
			KeyNpcIds = new List<int> { 20004 },
			KeyLocationIds = new List<string> { "S116" },
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "intro_dialogue",
						Kind = MainlineScriptBlockKind.Dialogue,
						SceneId = "S116",
						ScenePurpose = "引入冲突",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("这丹药名为烈阳丹，服之可破关。", 20004) }
					},
					new MainlineScriptBlock
					{
						BlockRef = "main_choice_point",
						Kind = MainlineScriptBlockKind.Choice,
						SceneId = "S116",
						ScenePurpose = "核心决策点",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNarrator("祝语芙心中已有计较。") },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_stable",
								Text = "压制火毒，徐徐图之"
							},
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_risky",
								Text = "爆发速决，险中求胜"
							}
						}
					},
					new MainlineScriptBlock
					{
						BlockRef = "outcome_stable",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S116",
						ScenePurpose = "稳健方案后的结果",
						BranchRef = "choice_stable",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("罢了，听你的。", 20004) }
					},
					new MainlineScriptBlock
					{
						BlockRef = "outcome_risky",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S116",
						ScenePurpose = "激进方案后的结果",
						BranchRef = "choice_risky",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("这便是我想要的痛快！", 20004) }
					}
				}
			}
		};
		MainlineBeatGamePlan semanticPlan = new MainlineBeatGamePlan
		{
			UsedNpcIds = new List<int> { 20004 },
			UsedSceneIds = new List<string> { "S116" },
			FlowSteps = new List<MainlineSemanticFlowStep>
			{
				new MainlineSemanticFlowStep
				{
					StepRef = "step_01_dialogue",
					Kind = MainlineSemanticFlowStepKind.PlayScriptBlock,
					SourceBlockRef = "intro_dialogue",
					SceneId = "S116",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_02_choice",
					Kind = MainlineSemanticFlowStepKind.PresentChoice,
					SourceBlockRef = "main_choice_point",
					SceneId = "S116",
					TargetNpcId = 20004
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_03_stable",
					Kind = MainlineSemanticFlowStepKind.PlayScriptBlock,
					SourceBlockRef = "outcome_stable",
					BranchRef = "choice_stable"
				},
				new MainlineSemanticFlowStep
				{
					StepRef = "step_04_risky",
					Kind = MainlineSemanticFlowStepKind.PlayScriptBlock,
					SourceBlockRef = "outcome_risky",
					BranchRef = "choice_risky"
				}
			}
		};
		QuestElementPicker.BeatExecutionContext executionContext = new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S116",
					Name = "竹山宗神兵阁"
				}
			}
		};
		QuestProgram program = null;
		string error = null;
		try
		{
			program = InvokeNonPublic(manager, "CompileMainlineBeatToQuestProgram", beat, semanticPlan, executionContext) as QuestProgram;
		}
		catch (TargetInvocationException ex)
		{
			error = ex.InnerException?.Message ?? ex.Message;
		}
		QuestNode syntheticEncounter = program?.Nodes?.FirstOrDefault((QuestNode node) => node.NodeId != null && node.NodeId.Contains("synthetic_encounter"));
		bool ok = string.IsNullOrWhiteSpace(error) && program != null && program.WorldContract != null && program.WorldContract.SelectedPredicates.Count > 0 && program.WorldContract.SelectedCommands.Count > 0 && program.WorldContract.SelectedPredicates.Contains("scene.is") && program.WorldContract.SelectedCommands.Contains("npc.set_action") && syntheticEncounter != null && syntheticEncounter.Type == QuestNodeType.Objective && syntheticEncounter.CompletionPredicates.Count > 0 && program.Nodes.IndexOf(syntheticEncounter) == 0;
		return new TestCaseResult
		{
			Name = "MainlineBeatCompilationSynthesizesEncounterForNarrativeOnlyFlow",
			Passed = ok,
			Details = (ok ? "Narrative-only flow auto-synthesizes a leading encounter node to satisfy worldContract predicate/command requirements" : ("Narrative-only compilation failed: error=" + (error ?? "<none>") + ", program=" + DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestQuestProgramParserAllowsSelectedDangerousCommandWithoutExtraPermission()
	{
		string json = "{\n  \"title\": \"危险命令直接由 selected sets 决定\",\n  \"summary\": \"测试用程序\",\r\n  \"worldContract\": {\r\n    \"allowedSceneIds\": [\"S116\"],\r\n    \"selectedPredicates\": [\"scene.is\"],\r\n    \"selectedCommands\": [\"scene.load\"]\r\n  },\r\n  \"nodes\": [\r\n    {\r\n      \"nodeId\": \"n1\",\r\n      \"type\": \"WorldState\",\r\n      \"title\": \"强制切图\",\r\n      \"activationPredicates\": [\r\n        { \"opcode\": \"scene.is\", \"params\": { \"sceneId\": \"S116\", \"sceneName\": \"竹山宗神兵阁\" } }\r\n      ],\r\n      \"onActivated\": [\r\n        { \"opcode\": \"scene.load\", \"params\": { \"sceneId\": \"S116\" } }\r\n      ]\r\n    }\r\n  ]\r\n}";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && program != null && program.WorldContract != null && program.WorldContract.SelectedCommands.SequenceEqual(new string[1] { "scene.load" });
		return new TestCaseResult
		{
			Name = "QuestProgramParserAllowsSelectedDangerousCommandWithoutExtraPermission",
			Passed = ok,
			Details = (ok ? "Stage-3 parser now treats selectedCommands as the sole authority, without extra dangerous-command permission checks" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestQuestProgramParserAcceptsPredicateOnlyMainlineProgram()
	{
		string json = "{\n  \"title\": \"仅谓词主线程序\",\n  \"summary\": \"测试无命令的主线程序也能通过 parser。\",\n  \"worldContract\": {\n    \"allowedSceneIds\": [\"S116\"],\n    \"selectedPredicates\": [\"scene.is\"],\n    \"selectedCommands\": []\n  },\n  \"nodes\": [\n    {\n      \"nodeId\": \"n1\",\n      \"type\": \"Objective\",\n      \"title\": \"前往竹山宗\",\n      \"completionPredicates\": [\n        { \"opcode\": \"scene.is\", \"params\": { \"sceneId\": \"S116\" } }\n      ],\n      \"nextNodeId\": \"n2\"\n    },\n    {\n      \"nodeId\": \"n2\",\n      \"type\": \"Terminal\",\n      \"title\": \"结束\"\n    }\n  ]\n}";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && program != null && program.WorldContract != null && program.WorldContract.SelectedCommands.Count == 0 && program.WorldContract.SelectedPredicates.SequenceEqual(new string[1] { "scene.is" });
		return new TestCaseResult
		{
			Name = "QuestProgramParserAcceptsPredicateOnlyMainlineProgram",
			Passed = ok,
			Details = (ok ? "Predicate-only mainline programs now pass parser validation when they do not actually use any commands" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestQuestProgramParserRejectsOpcodesOutsideSelectedSets()
	{
		string json = "{\n  \"title\": \"越界指令\",\n  \"summary\": \"测试 selected set 严格校验\",\r\n  \"worldContract\": {\r\n    \"selectedPredicates\": [\"scene.is\"],\r\n    \"selectedCommands\": [\"npc.set_action\"]\r\n  },\r\n  \"nodes\": [\r\n    {\r\n      \"nodeId\": \"n1\",\r\n      \"type\": \"Objective\",\r\n      \"title\": \"越界节点\",\r\n      \"activationPredicates\": [\r\n        { \"opcode\": \"time.compare\", \"params\": { \"operator\": \">=\", \"value\": \"0002-07-01 00:00:00\" } }\r\n      ],\r\n      \"onActivated\": [\r\n        { \"opcode\": \"npc.set_action\", \"params\": { \"npcId\": \"20004\", \"actionId\": \"46\" } }\r\n      ]\r\n    }\r\n  ]\r\n}";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = !parsed && program == null && !string.IsNullOrWhiteSpace(error) && error.Contains("selectedPredicates") && error.Contains("time.compare");
		return new TestCaseResult
		{
			Name = "QuestProgramParserRejectsOpcodesOutsideSelectedSets",
			Passed = ok,
			Details = (ok ? "Stage-3 parser now rejects predicates or commands that escape the exact selected opcode sets" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestQuestProgramParserAcceptsValidMainlineProgram()
	{
		string json = "{\r\n  \"title\": \"主线测试程序\",\r\n  \"summary\": \"测试主线编译输出\",\r\n  \"worldContract\": {\r\n    \"allowedNpcIds\": [20004],\r\n    \"allowedSceneIds\": [\"S116\"],\r\n    \"selectedPredicates\": [\"time.compare\"],\r\n    \"selectedCommands\": [\"scene.load\", \"npc.set_action\"]\r\n  },\r\n  \"nodes\": [\r\n    {\r\n      \"nodeId\": \"n1\",\r\n      \"type\": \"Objective\",\r\n      \"title\": \"先切场\",\r\n      \"activationPredicates\": [\r\n        { \"opcode\": \"time.compare\", \"params\": { \"operator\": \">=\", \"value\": \"0002-07-01 00:00:00\" } }\r\n      ],\r\n      \"onActivated\": [\r\n        { \"opcode\": \"scene.load\", \"params\": { \"sceneId\": \"S116\" } },\r\n        { \"opcode\": \"npc.set_action\", \"params\": { \"npcId\": \"20004\", \"actionId\": \"46\" } }\r\n      ],\r\n      \"completionPredicates\": [\r\n        { \"opcode\": \"time.compare\", \"params\": { \"operator\": \">=\", \"value\": \"0002-07-01 00:00:00\" } }\r\n      ],\r\n      \"nextNodeId\": \"n2\"\r\n    },\r\n    {\r\n      \"nodeId\": \"n2\",\r\n      \"type\": \"Terminal\",\r\n      \"title\": \"结束\"\r\n    }\r\n  ]\r\n}";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && program != null && program.TrackType == QuestTrackType.Mainline && program.Nodes != null && program.Nodes.Count == 1 && program.WorldContract != null && program.WorldContract.SelectedCommands.SequenceEqual(new string[2] { "scene.load", "npc.set_action" });
		return new TestCaseResult
		{
			Name = "QuestProgramParserAcceptsValidMainlineProgram",
			Passed = ok,
			Details = (ok ? "Mainline parser accepts structured programs when opcodes, params, and world contract all align" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestQuestProgramParserAcceptsFencedMarkdownJson()
	{
		string json = "```json\r\n{\r\n  \"title\": \"围栏主线程序\",\r\n  \"summary\": \"测试 fenced markdown JSON 也能被 QuestProgramParser 识别。\",\r\n  \"worldContract\": {\r\n    \"allowedNpcIds\": [20004],\r\n    \"allowedSceneIds\": [\"147\"],\r\n    \"selectedPredicates\": [\"scene.is\", \"npc.talked_to_player\"],\r\n    \"selectedCommands\": [\"npc.set_action\"]\r\n  },\r\n  \"nodes\": [\r\n    {\r\n      \"nodeId\": \"n1\",\r\n      \"type\": \"Objective\",\r\n      \"title\": \"见面\",\r\n      \"activationPredicates\": [\r\n        { \"opcode\": \"scene.is\", \"params\": { \"sceneId\": \"147\", \"sceneName\": \"逸风城附近\" } }\r\n      ],\r\n      \"completionPredicates\": [\r\n        { \"opcode\": \"npc.talked_to_player\", \"params\": { \"npcId\": \"20004\" } }\r\n      ],\r\n      \"onActivated\": [\r\n        { \"opcode\": \"npc.set_action\", \"params\": { \"npcId\": \"20004\", \"actionId\": \"46\" } }\r\n      ],\r\n      \"nextNodeId\": \"n2\"\r\n    },\r\n    {\r\n      \"nodeId\": \"n2\",\r\n      \"type\": \"Terminal\",\r\n      \"title\": \"结束\"\r\n    }\r\n  ]\r\n}\r\n```";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && program != null && string.Equals(program.Title, "围栏主线程序", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "QuestProgramParserAcceptsFencedMarkdownJson",
			Passed = ok,
			Details = (ok ? "QuestProgramParser strips markdown fences before JSON parsing" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestQuestProgramParserRejectsTruncatedFencedJson()
	{
		string json = "```json\r\n{\r\n  \"title\": \"截断主线程序\",\r\n  \"summary\": \"测试 fenced markdown 截断时不会被偷偷修补。\",\r\n  \"worldContract\": {\r\n    \"allowedNpcIds\": [20004],\r\n    \"allowedSceneIds\": [\"147\"],\r\n    \"selectedPredicates\": [\"scene.is\"],\r\n    \"selectedCommands\": [\"npc.set_action\"]\r\n  },\r\n  \"nodes\": [\r\n    {\r\n      \"nodeId\": \"n1\",\r\n      \"type\": \"Objective\",\r\n      \"title\": \"坏节点\"\r\n";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = !parsed && program == null && !string.IsNullOrWhiteSpace(error) && error.IndexOf("完整 JSON", StringComparison.OrdinalIgnoreCase) >= 0;
		return new TestCaseResult
		{
			Name = "QuestProgramParserRejectsTruncatedFencedJson",
			Passed = ok,
			Details = (ok ? "QuestProgramParser still rejects truncated fenced JSON instead of auto-repairing it" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestQuestProgramParserRejectsObjectiveWithoutCompletionPredicates()
	{
		string json = "{\r\n  \"title\": \"缺完成条件的节点\",\r\n  \"summary\": \"测试 Objective 节点必须显式给出完成条件\",\r\n  \"worldContract\": {\r\n    \"allowedNpcIds\": [20004],\r\n    \"allowedSceneIds\": [\"147\"],\r\n    \"selectedPredicates\": [\"scene.is\"],\r\n    \"selectedCommands\": [\"npc.set_action\"]\r\n  },\r\n  \"nodes\": [\r\n    {\r\n      \"nodeId\": \"n1\",\r\n      \"type\": \"Objective\",\r\n      \"title\": \"逸风城重逢\",\r\n      \"activationPredicates\": [\r\n        { \"opcode\": \"scene.is\", \"params\": { \"sceneId\": \"147\", \"sceneName\": \"逸风城附近\" } }\r\n      ],\r\n      \"onActivated\": [\r\n        { \"opcode\": \"npc.set_action\", \"params\": { \"npcId\": \"20004\", \"actionId\": \"46\" } }\r\n      ],\r\n      \"nextNodeId\": \"n2\"\r\n    },\r\n    {\r\n      \"nodeId\": \"n2\",\r\n      \"type\": \"Terminal\",\r\n      \"title\": \"结束\"\r\n    }\r\n  ]\r\n}";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = !parsed && program == null && !string.IsNullOrWhiteSpace(error) && error.IndexOf("completionPredicates", StringComparison.OrdinalIgnoreCase) >= 0;
		return new TestCaseResult
		{
			Name = "QuestProgramParserRejectsObjectiveWithoutCompletionPredicates",
			Passed = ok,
			Details = (ok ? "Objective/Combat/WorldState nodes without completionPredicates are now rejected instead of silently auto-completing at runtime" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestQuestProgramParserRejectsNonTerminalNodeWithoutNextNodeId()
	{
		string json = "{\r\n  \"title\": \"缺跳转的节点\",\r\n  \"summary\": \"测试非终点节点必须显式给出跳转\",\r\n  \"worldContract\": {\r\n    \"allowedNpcIds\": [20004],\r\n    \"allowedSceneIds\": [\"147\"],\r\n    \"selectedPredicates\": [\"scene.is\", \"npc.talked_to_player\"],\r\n    \"selectedCommands\": [\"npc.set_action\"]\r\n  },\r\n  \"nodes\": [\r\n    {\r\n      \"nodeId\": \"n1\",\r\n      \"type\": \"Objective\",\r\n      \"title\": \"逸风城重逢\",\r\n      \"activationPredicates\": [\r\n        { \"opcode\": \"scene.is\", \"params\": { \"sceneId\": \"147\", \"sceneName\": \"逸风城附近\" } }\r\n      ],\r\n      \"completionPredicates\": [\r\n        { \"opcode\": \"npc.talked_to_player\", \"params\": { \"npcId\": \"20004\" } }\r\n      ],\r\n      \"onActivated\": [\r\n        { \"opcode\": \"npc.set_action\", \"params\": { \"npcId\": \"20004\", \"actionId\": \"46\" } }\r\n      ]\r\n    }\r\n  ]\r\n}";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = !parsed && program == null && !string.IsNullOrWhiteSpace(error) && error.IndexOf("nextNodeId", StringComparison.OrdinalIgnoreCase) >= 0;
		return new TestCaseResult
		{
			Name = "QuestProgramParserRejectsNonTerminalNodeWithoutNextNodeId",
			Passed = ok,
			Details = (ok ? "Non-terminal nodes without nextNodeId are now rejected so staged NPCs are not restored immediately by a malformed beat" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestMainlinePromptsUseCurrentSemanticProtocol()
	{
		QuestElementPicker.PickedElements elements = new QuestElementPicker.PickedElements
		{
			NarrativeContext = new QuestElementPicker.NarrativeMaterialContext
			{
				ConcreteStoryItems = new List<QuestElementPicker.NarrativeConcreteItemCandidate>
				{
					new QuestElementPicker.NarrativeConcreteItemCandidate
					{
						ItemId = 3101,
						Name = "清心玉露丸",
						Effect = "安神定气，压住躁动药力",
						OfficialDescription = "丹气清润，可安抚心神躁动。",
						Quality = 5,
						Scarcity = "稀有"
					},
					new QuestElementPicker.NarrativeConcreteItemCandidate
					{
						ItemId = 4102,
						Name = "养脉散",
						Effect = "温养经脉，缓和灵力反冲",
						OfficialDescription = "散药入体后可温养经络，缓解灵力冲撞。",
						Quality = 4,
						Scarcity = "少见"
					}
				},
				TimeCandidates = new List<QuestElementPicker.TimeCandidate>
				{
					new QuestElementPicker.TimeCandidate
					{
						Label = "短期",
						Summary = "适合在相对靠近的时间内推进一次阶段性事件",
						MinMonths = 1,
						MaxMonths = 144
					}
				}
			}
		};
		List<ChatMessage> storyMessages = AIQuestPromptBuilder.BuildMainlineStoryBeatMessages(new NPCInfo
		{
			NpcID = 20004,
			Name = "井紫铃"
		}, elements, null, new PendingBiographyEventState
		{
			EventId = "event_longsheng_prompt",
			NpcId = 20004,
			NpcName = "井紫铃",
			BiographyEventKind = BiographyEventKind.ImprotantEvent,
			Summary = "井紫铃的一条官方命运节点被拦下。",
			PlayerFacingHint = "接下来会围绕这条命运节点展开剧情。"
		}, "此前她已经向你提过一次长生的代价。");
		List<ChatMessage> structuredMessages = AIQuestPromptBuilder.BuildMainlineStructuredBeatMessages(new NPCInfo
		{
			NpcID = 20004,
			Name = "井紫铃"
		}, elements, new PendingBiographyEventState
		{
			EventId = "event_longsheng_prompt",
			NpcId = 20004,
			NpcName = "井紫铃",
			BiographyEventKind = BiographyEventKind.ImprotantEvent,
			Summary = "井紫铃的一条官方命运节点被拦下。",
			PlayerFacingHint = "接下来会围绕这条命运节点展开剧情。"
		}, new MainlineRawNarrativeDraft
		{
			RawText = new string('心', 2500) + "你赶到神兵阁外时，井紫铃正立在风里。她说今日必须把这团心火的来处说破。若你选择先稳住她，她会在压抑里慢慢吐露旧事；若你选择逼她直面，她会在怒意里把多年执念一并撕开。两条路都在今日走到尽头，不再拖向下一拍。"
		});
		List<ChatMessage> planningMessages = AIQuestPromptBuilder.BuildMainlineBeatGamePlanningMessages(new NPCInfo
		{
			NpcID = 20004,
			Name = "井紫铃"
		}, new MainlineBeat
		{
			BeatId = "beat_1",
			Title = "神兵阁问火",
			Summary = "在神兵阁确认井紫铃的道途危机",
			TimeIntent = new BeatTimeIntent
			{
				TimeBand = "中期",
				Urgency = "中",
				MaxSpanMonths = 240
			},
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						ScenePurpose = "确认求长生背后的失去焦虑",
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("若连这团火都压不住，还谈什么长生。", 20004) }
					}
				}
			}
		}, new QuestElementPicker.BeatExecutionContext
		{
			AllowedNpcs = new List<QuestElementPicker.PickedNpc>
			{
				new QuestElementPicker.PickedNpc
				{
					Id = "20004",
					Name = "井紫铃"
				}
			},
			AllowedLocations = new List<QuestElementPicker.PickedLocation>
			{
				new QuestElementPicker.PickedLocation
				{
					Id = "S116",
					Name = "竹山宗神兵阁"
				}
			},
			ConcreteItems = new List<QuestElementPicker.ConcreteItemOption>
			{
				new QuestElementPicker.ConcreteItemOption
				{
					ItemId = 3101,
					Name = "清心玉露丸",
					Quality = 5,
					Effect = "安神定气，压住躁动药力"
				}
			}
		});
		string story = string.Join("\n", storyMessages?.Select((ChatMessage message) => message?.Content ?? string.Empty) ?? Enumerable.Empty<string>());
		string structured = string.Join("\n", structuredMessages?.Select((ChatMessage message) => message?.Content ?? string.Empty) ?? Enumerable.Empty<string>());
		string plan = string.Join("\n", planningMessages?.Select((ChatMessage message) => message?.Content ?? string.Empty) ?? Enumerable.Empty<string>());
		bool ok = story.Contains("只返回一段完整剧情正文") && story.Contains("第一句就直接进入剧情") && story.Contains("不要使用大括号") && story.Contains("不要写字段名") && story.Contains("不要把背景改写成列表") && story.Contains("单生平单拍完结") && story.Contains("每个分支都要写出独立后续") && story.Contains("官方生平事件可作为背景线索之一") && story.Contains("NPC、场景、具体物品都只能使用素材库里已经给出的内容") && story.Contains("优先参考候选池里的官方介绍") && story.Contains("即使你知道游戏里还存在别的官方场景或官方物品，只要这次素材库没给出，也不要写") && story.Contains("清心玉露丸") && story.Contains("养脉散") && story.Contains("官方介绍丹气清润，可安抚心神躁动。") && !story.Contains("===") && !story.Contains("【") && !story.Contains("\n- ") && !story.Contains("物品功能池") && !story.Contains("scriptBlocks") && !story.Contains("eventChainResolved") && !story.Contains("NPC_ID:") && !story.Contains("场景ID:") && !story.Contains("\"npcId\"") && !story.Contains("\"npcName\"") && !story.Contains("\"kind\"") && !story.Contains("\"eventId\"") && !story.Contains("\"summary\":") && !story.Contains("\"playerFacingHint\"") && !story.Contains("\"capturedAtGameTime\"") && !story.Contains("20004") && !story.Contains("\"worldContract\"") && !story.Contains("dialogueBlocks") && structured.TrimStart().StartsWith("{", StringComparison.Ordinal) && structured.Contains("\"contract_id\": \"stage2_story_contract\"") && structured.Contains("\"response_requirements\"") && structured.Contains("\"omit_unused_optional_fields\": true") && structured.Contains("\"forbid_empty_property_names\": true") && structured.Contains("\"field_contracts\"") && structured.Contains("\"rules\"") && structured.Contains("\"output_shell\"") && structured.Contains("legalMaterials") && structured.Contains("npcNames") && structured.Contains("sceneNames") && structured.Contains("items") && structured.Contains("清心玉露丸") && structured.Contains("官方介绍") && structured.Contains("timeCategory") && structured.Contains("blockRef") && structured.Contains("branchRef") && structured.Contains("npc|") && !structured.Contains("resourceIntent") && !structured.Contains("keyNpcNames") && !structured.Contains("keyLocationNames") && !structured.Contains("keyNpcIds") && !structured.Contains("keyLocationIds") && !structured.Contains("participantNpcIds") && !structured.Contains("speakerNpcId") && !structured.Contains("NPC_ID:") && !structured.Contains("场景ID:") && !structured.Contains("\"npcId\"") && !structured.Contains("\"eventId\"") && !structured.Contains("\"block_1\"") && !structured.Contains("\"choice_a\"") && !structured.Contains("\"kind\": \"encounter\"") && !structured.Contains("\"kind\": \"choice\"") && !structured.Contains("\"kind\": \"fallout\"") && !structured.Contains("\"speakerType\": \"Npc\"") && !structured.Contains("\"speakerType\": \"Player\"") && !structured.Contains("20004") && !structured.Contains("S114") && plan.TrimStart().StartsWith("{", StringComparison.Ordinal) && plan.Contains("\"contract_id\": \"stage3_semantic_contract\"") && plan.Contains("\"response_requirements\"") && plan.Contains("\"omit_unused_optional_fields\": true") && plan.Contains("\"forbid_empty_property_names\": true") && plan.Contains("\"field_contracts\"") && plan.Contains("\"rules\"") && plan.Contains("\"output_shell\"") && plan.Contains("flowSteps") && plan.Contains("sourceBlockRef") && plan.Contains("targetNpcName") && plan.Contains("branchRef") && plan.Contains("payload") && plan.Contains("effects") && plan.Contains("legalMaterials") && plan.Contains("npcNames") && plan.Contains("sceneNames") && plan.Contains("itemName") && plan.Contains("清心玉露丸") && !plan.Contains("summary") && !plan.Contains("usedNpcNames") && !plan.Contains("usedSceneNames") && !plan.Contains("usedNpcIds") && !plan.Contains("usedSceneIds") && !plan.Contains("scriptBlockId") && !plan.Contains("targetNpcId") && !plan.Contains("itemFunctionTag") && !plan.Contains("resourcePurpose") && !plan.Contains("statusId") && !plan.Contains("effectSteps") && !plan.Contains("NPC_ID:") && !plan.Contains("场景ID:") && !plan.Contains("\"npcId\"") && !plan.Contains("\"eventId\"") && !plan.Contains("\"block_1\"") && !plan.Contains("\"choice_a\"") && !plan.Contains("\"kind\": \"encounter_at_scene\"") && !plan.Contains("\"kind\": \"present_choice\"") && !plan.Contains("\"kind\": \"play_script_block\"") && !plan.Contains("\"kind\": \"favor_change\"") && !plan.Contains("20004") && !plan.Contains("S114") && !plan.Contains("worldContract") && !plan.Contains("triggerPredicates") && !plan.Contains("delivery_check") && !plan.Contains("official_task") && !plan.Contains("taskId") && !plan.Contains("taskStep") && story.Contains("短期") && !story.Contains("短期数月") && !plan.Contains("短期数月");
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "MainlinePromptsUseCurrentSemanticProtocol";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Mainline prompts now split pure narrative stage-1 from contract-driven stage-2 and stage-3 envelopes" : ("Unexpected mainline thematic prompts: story=" + story + "; structured=" + structured + "; plan=" + plan));
		return testCaseResult;
	}

	private static TestCaseResult TestMainlineContractsUseDescriptiveOutputShells()
	{
		MainlineJsonContract stage2 = AIQuestPromptBuilder.BuildMainlineJsonContractForTest(MainlineJsonContractStage.Stage2Story);
		MainlineJsonContract stage3 = AIQuestPromptBuilder.BuildMainlineJsonContractForTest(MainlineJsonContractStage.Stage3Semantic);
		JObject stage2Shell = stage2?.OutputShell;
		JObject stage3Shell = stage3?.OutputShell;
		JObject stage2BlockShell = ((stage2Shell?["scriptBlocks"] is JArray { Count: 1 } stage2Blocks) ? (stage2Blocks[0] as JObject) : null);
		JValue stage2LineShell = ((stage2BlockShell?["lines"] is JArray { Count: 1 } stage2Lines) ? (stage2Lines[0] as JValue) : null);
		JObject stage3FlowShell = ((stage3Shell?["flowSteps"] is JArray { Count: 1 } stage3Flows) ? (stage3Flows[0] as JObject) : null);
		JObject stage3EffectShell = ((stage3FlowShell?["effects"] is JArray { Count: 1 } stage3Effects) ? (stage3Effects[0] as JObject) : null);
		int num;
		if (stage2BlockShell != null && stage2LineShell != null && stage3FlowShell != null && stage3EffectShell != null)
		{
			string? text = (string?)stage2BlockShell["kind"];
			if (text != null && text.StartsWith("<", StringComparison.Ordinal))
			{
				string obj = (string)stage2LineShell.Value;
				if (obj != null && obj.StartsWith("narrator||<", StringComparison.Ordinal))
				{
					string? text2 = (string?)stage3FlowShell["kind"];
					if (text2 != null && text2.StartsWith("<", StringComparison.Ordinal))
					{
						num = ((((string?)stage3EffectShell["kind"])?.StartsWith("<", StringComparison.Ordinal) ?? false) ? 1 : 0);
						goto IL_01b9;
					}
				}
			}
		}
		num = 0;
		goto IL_01b9;
		IL_01b9:
		bool ok = (byte)num != 0;
		return new TestCaseResult
		{
			Name = "MainlineContractsUseDescriptiveOutputShells",
			Passed = ok,
			Details = (ok ? "Stage-2 and stage-3 contracts now use descriptive output shells instead of deterministic sample values" : ("Unexpected contract shells: stage2=" + (stage2Shell?.ToString(Formatting.None) ?? "<null>") + "; stage3=" + (stage3Shell?.ToString(Formatting.None) ?? "<null>")))
		};
	}

	private static TestCaseResult TestMainlineContractsDeclareCompactLinesAndMultipleBlocks()
	{
		MainlineJsonContract stage2 = AIQuestPromptBuilder.BuildMainlineJsonContractForTest(MainlineJsonContractStage.Stage2Story);
		MainlineJsonContract stage3 = AIQuestPromptBuilder.BuildMainlineJsonContractForTest(MainlineJsonContractStage.Stage3Semantic);
		MainlineJsonFieldContract stage2LineField = (stage2?.FieldContracts?.FirstOrDefault((MainlineJsonFieldContract field) => string.Equals(field?.Name, "scriptBlocks", StringComparison.Ordinal)))?.ItemContract?.FieldContracts?.FirstOrDefault((MainlineJsonFieldContract field) => string.Equals(field?.Name, "lines", StringComparison.Ordinal));
		JArray stage2BlocksShell = stage2?.OutputShell?["scriptBlocks"] as JArray;
		JArray stage3FlowsShell = stage3?.OutputShell?["flowSteps"] as JArray;
		bool ok = stage2LineField != null && string.Equals(stage2LineField.Type, "array", StringComparison.Ordinal) && stage2LineField.Required && stage2LineField.ItemContract != null && string.Equals(stage2LineField.ItemContract.Type, "string", StringComparison.Ordinal) && stage2BlocksShell != null && stage2BlocksShell.Count >= 3 && stage3FlowsShell != null && stage3FlowsShell.Count >= 3;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "MainlineContractsDeclareCompactLinesAndMultipleBlocks";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Stage-2 now declares lines explicitly and both stage-2/stage-3 shells show multi-block, multi-step structure" : ("Unexpected contract structure: stage2LineField=" + (stage2LineField?.Name ?? "<null>") + ":" + (stage2LineField?.Type ?? "<null>") + ", stage2Blocks=" + (stage2BlocksShell?.Count.ToString() ?? "<null>") + ", stage3Flows=" + (stage3FlowsShell?.Count.ToString() ?? "<null>")));
		return testCaseResult;
	}

	private static TestCaseResult TestMainlineStructuredBeatIgnoresIllegalContent(AIQuestManager manager)
	{
		JObject outline = JObject.Parse("{\n  \"title\": \"测试\",\n  \"summary\": \"测试\",\n  \"timeCategory\": \"短期\",\n  \"scriptBlocks\": [\n    {\n      \"\": [\"Narrator||测试\"],\n      \"blockRef\": \"block_1\",\n      \"kind\": \"dialogue\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"测试\",\n      \"participantNpcNames\": [\"井紫铃\", \"素材库外人物\"],\n      \"lines\": [\"Npc|井紫铃|测试\", \"Ghost|素材库外人物|无效对白\"],\n      \"unexpectedField\": true\n    },\n    {\n      \"blockRef\": \"block_choice\",\n      \"kind\": \"choice\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"抉择\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Player||如何应对？\"],\n      \"choices\": [\n        { \"choiceRef\": \"choice_a\", \"text\": \"稳住她\", \"consequenceDirection\": \"安抚\" },\n        { \"choiceRef\": \"\", \"text\": \"这条无效选择会被忽略\" }\n      ]\n    },\n    {\n      \"blockRef\": \"invalid_branch\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"素材库外场景\",\n      \"scenePurpose\": \"这段无效分支应被忽略\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Npc|井紫铃|这段不该保留下来。\"],\n      \"branchRef\": \"choice_b\"\n    },\n    {\n      \"blockRef\": \"branch_a\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"有效分支\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Npc|井紫铃|这一段会保留下来。\"],\n      \"branchRef\": \"choice_a\"\n    }\n  ]\n}");
		MethodInfo method = typeof(AIQuestManager).GetMethod("TryParseMainlineStructuredBeat", BindingFlags.Instance | BindingFlags.NonPublic);
		object[] args = new object[6]
		{
			outline,
			new NPCInfo
			{
				NpcID = 20004,
				Name = "井紫铃"
			},
			null,
			new MainlineRawNarrativeDraft
			{
				RawText = new string('心', 2500)
			},
			null,
			null
		};
		bool parsed = method != null && (bool)method.Invoke(manager, args);
		MainlineBeat beat = args[4] as MainlineBeat;
		string error = args[5] as string;
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && beat != null && beat.StoryDraft?.ScriptBlocks?.Count == 3 && beat.StoryDraft.ScriptBlocks[0].Lines.Count == 1 && beat.StoryDraft.ScriptBlocks[1].Choices.Count == 1 && string.Equals(beat.StoryDraft.ScriptBlocks[1].Choices[0].ChoiceRef, "choice_a", StringComparison.Ordinal) && beat.StoryDraft.ScriptBlocks.Count((MainlineScriptBlock block) => string.Equals(block.BranchRef, "choice_a", StringComparison.Ordinal)) == 1 && beat.StoryDraft.ScriptBlocks.All((MainlineScriptBlock block) => !string.Equals(block.SceneName, "素材库外场景", StringComparison.Ordinal));
		return new TestCaseResult
		{
			Name = "MainlineStructuredBeatIgnoresIllegalContent",
			Passed = ok,
			Details = (ok ? "Stage-2 parser now ignores illegal block content and keeps only valid structured beat material" : ("Unexpected structured-beat tolerance result: error=" + (error ?? "<null>") + ", beat=" + DescribeMainlineBeat(beat)))
		};
	}

	private static TestCaseResult TestMainlineRawNarrativeDraftRejectsStructuredOutput(AIQuestManager manager)
	{
		MethodInfo method = typeof(AIQuestManager).GetMethod("TryParseMainlineRawNarrativeDraft", BindingFlags.Static | BindingFlags.NonPublic);
		object[] args = new object[3] { "{ \"title\": \"错误剧情拍\", \"scriptBlocks\": [] }", null, null };
		bool parsed = method != null && (bool)method.Invoke(null, args);
		string error = args[2] as string;
		bool ok = !parsed && !string.IsNullOrWhiteSpace(error) && error.Contains("纯文本");
		return new TestCaseResult
		{
			Name = "MainlineRawNarrativeDraftRejectsStructuredOutput",
			Passed = ok,
			Details = (ok ? "Stage-1 parser now hard-rejects structured JSON and only accepts pure narrative text" : string.Format("Unexpected stage-1 raw narrative parse result: parsed={0}, error={1}", parsed, error ?? "<null>"))
		};
	}

	private static TestCaseResult TestMainlineStructuredBeatAcceptsChoiceScopedBlocks(AIQuestManager manager)
	{
		JObject outline = JObject.Parse("{\n  \"title\": \"终局问心\",\n  \"summary\": \"当前主线在这一拍内走完所有分支。\",\n  \"timeCategory\": \"长期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"开场对峙\",\n      \"kind\": \"encounter\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"主线开场\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Npc|井紫铃|走到这里，这条路也该由你自己定了。\"]\n    },\n    {\n      \"blockRef\": \"最后抉择\",\n      \"kind\": \"choice\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"决定最后如何应对\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Player||这一回，你打算怎么选？\"],\n      \"choices\": [\n        { \"choiceRef\": \"信任分支\", \"text\": \"信她一次\", \"consequenceDirection\": \"信任路线\" },\n        { \"choiceRef\": \"强压分支\", \"text\": \"逼她直面\", \"consequenceDirection\": \"强硬路线\" }\n      ]\n    },\n    {\n      \"blockRef\": \"信任收束\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"信任后的独立收束\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Npc|井紫铃|既然你肯信我，我便把心火的来处都说给你听。\"],\n      \"branchRef\": \"信任分支\"\n    },\n    {\n      \"blockRef\": \"强压收束\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"强硬后的独立收束\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Npc|井紫铃|你既逼我开口，那我今日便把旧账全翻出来。\"],\n      \"branchRef\": \"强压分支\"\n    }\n  ]\n}");
		MethodInfo method = typeof(AIQuestManager).GetMethod("TryParseMainlineStructuredBeat", BindingFlags.Instance | BindingFlags.NonPublic);
		object[] args = new object[5]
		{
			outline,
			new NPCInfo
			{
				NpcID = 20004,
				Name = "井紫铃"
			},
			new MainlineRawNarrativeDraft
			{
				RawText = new string('心', 2500)
			},
			null,
			null
		};
		bool parsed = method != null && (bool)method.Invoke(manager, args);
		MainlineBeat beat = args[3] as MainlineBeat;
		string error = args[4] as string;
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && beat != null && beat.StoryDraft?.ScriptBlocks?.Count == 4 && beat.KeyNpcIds.Count == 1 && beat.KeyNpcIds[0] == 20004 && beat.KeyLocationIds.Count == 1 && beat.KeyLocationIds[0] == "S116" && beat.StoryDraft.ScriptBlocks.Count((MainlineScriptBlock block) => block.Kind == MainlineScriptBlockKind.Choice) == 1 && beat.StoryDraft.ScriptBlocks.Count((MainlineScriptBlock block) => !string.IsNullOrWhiteSpace(block.BranchRef)) == 2;
		return new TestCaseResult
		{
			Name = "MainlineStructuredBeatAcceptsChoiceScopedBlocks",
			Passed = ok,
			Details = (ok ? "Stage-2 structured parser now accepts one choice入口 plus branch-scoped fallout blocks under the shared contract" : ("Unexpected structured beat parse result: error=" + (error ?? "<null>") + ", beat=" + DescribeMainlineBeat(beat)))
		};
	}

	private static TestCaseResult TestMainlineStructuredBeatAcceptsDecoratedSceneNames(AIQuestManager manager)
	{
		JObject outline = JObject.Parse("{\n  \"title\": \"雪夜问心\",\n  \"summary\": \"测试带修饰语的场景名仍能映射回候选场景。\",\n  \"timeCategory\": \"短期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"路上撞见\",\n      \"kind\": \"encounter\",\n      \"sceneName\": \"云汐城外的官道\",\n      \"scenePurpose\": \"在城外官道撞见井紫铃\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Narrator||寒风压过官道。\", \"Npc|井紫铃|你来得正好。\"]\n    },\n    {\n      \"blockRef\": \"当场抉择\",\n      \"kind\": \"choice\",\n      \"sceneName\": \"云汐城外的官道\",\n      \"scenePurpose\": \"决定如何介入这场危机\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Player||这一回你想我怎么帮你？\"],\n      \"choices\": [\n        { \"choiceRef\": \"去沂山\", \"text\": \"先带她去沂山避开人群\", \"consequenceDirection\": \"转场去沂山收束\" }\n      ]\n    },\n    {\n      \"blockRef\": \"沂山收束\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"沂山北麓的一处废弃客房\",\n      \"scenePurpose\": \"转去沂山后的独立后续\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Npc|井紫铃|这里总算能让我静一静。\"],\n      \"branchRef\": \"去沂山\"\n    }\n  ]\n}");
		MethodInfo method = typeof(AIQuestManager).GetMethod("TryParseMainlineStructuredBeat", BindingFlags.Instance | BindingFlags.NonPublic);
		object[] args = new object[6]
		{
			outline,
			new NPCInfo
			{
				NpcID = 20004,
				Name = "井紫铃"
			},
			new QuestElementPicker.PickedElements
			{
				Locations = new List<QuestElementPicker.PickedLocation>
				{
					new QuestElementPicker.PickedLocation
					{
						Id = "S201",
						Name = "云汐城",
						RegionName = "宁州"
					},
					new QuestElementPicker.PickedLocation
					{
						Id = "S301",
						Name = "沂山",
						RegionName = "宁州"
					}
				}
			},
			new MainlineRawNarrativeDraft
			{
				RawText = new string('心', 2500)
			},
			null,
			null
		};
		bool parsed = method != null && (bool)method.Invoke(manager, args);
		MainlineBeat beat = args[4] as MainlineBeat;
		string error = args[5] as string;
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && beat != null && beat.StoryDraft?.ScriptBlocks?.Count == 3 && string.Equals(beat.StoryDraft.ScriptBlocks[0].SceneName, "云汐城", StringComparison.Ordinal) && string.Equals(beat.StoryDraft.ScriptBlocks[1].SceneName, "云汐城", StringComparison.Ordinal) && string.Equals(beat.StoryDraft.ScriptBlocks[2].SceneName, "沂山", StringComparison.Ordinal) && beat.KeyLocationIds.Count == 2 && beat.KeyLocationIds.Contains("S201") && beat.KeyLocationIds.Contains("S301");
		return new TestCaseResult
		{
			Name = "MainlineStructuredBeatAcceptsDecoratedSceneNames",
			Passed = ok,
			Details = (ok ? "Stage-2 parser now maps naturally decorated scene phrases back to the unique candidate scenes" : ("Unexpected decorated-scene parse result: error=" + (error ?? "<null>") + ", beat=" + DescribeMainlineBeat(beat)))
		};
	}

	private static TestCaseResult TestMainlineStructuredBeatAcceptsOfficialSceneOutsideCandidatePool(AIQuestManager manager)
	{
		string officialSceneName = QuestElementPicker.GetUniqueOfficialLocationNameForTest();
		if (string.IsNullOrWhiteSpace(officialSceneName) || !QuestElementPicker.TryResolveOfficialLocationId(officialSceneName, out var officialSceneId))
		{
			return new TestCaseResult
			{
				Name = "MainlineStructuredBeatAcceptsOfficialSceneOutsideCandidatePool",
				Passed = false,
				Details = "测试环境未能从官方场景总池中找到可唯一解析的场景样本"
			};
		}
		JObject outline = new JObject
		{
			["title"] = "官方场景总池解析",
			["summary"] = "测试候选池外但官方合法的场景仍能被 stage2 接受。",
			["timeCategory"] = "短期",
			["scriptBlocks"] = new JArray
			{
				new JObject
				{
					["blockRef"] = "official_scene_intro",
					["kind"] = "encounter",
					["sceneName"] = officialSceneName,
					["scenePurpose"] = "在官方场景总池中的地点与井紫铃相见",
					["participantNpcNames"] = new JArray("井紫铃"),
					["lines"] = new JArray("narrator||你在此地与井紫铃意外重逢。")
				},
				new JObject
				{
					["blockRef"] = "official_scene_choice",
					["kind"] = "choice",
					["sceneName"] = officialSceneName,
					["scenePurpose"] = "决定下一步如何行动",
					["participantNpcNames"] = new JArray("井紫铃"),
					["lines"] = new JArray("player||先听她把事情说完。"),
					["choices"] = new JArray
					{
						new JObject
						{
							["choiceRef"] = "official_scene_branch",
							["text"] = "先稳住她",
							["consequenceDirection"] = "在此地继续收束"
						}
					}
				},
				new JObject
				{
					["blockRef"] = "official_scene_fallout",
					["kind"] = "fallout",
					["sceneName"] = officialSceneName,
					["scenePurpose"] = "在官方场景总池中的地点完成分支收束",
					["participantNpcNames"] = new JArray("井紫铃"),
					["lines"] = new JArray("npc|井紫铃|既然你已经来了，那就先听我说。"),
					["branchRef"] = "official_scene_branch"
				}
			}
		};
		MethodInfo method = typeof(AIQuestManager).GetMethod("TryParseMainlineStructuredBeat", BindingFlags.Instance | BindingFlags.NonPublic);
		object[] args = new object[6]
		{
			outline,
			new NPCInfo
			{
				NpcID = 20004,
				Name = "井紫铃"
			},
			new QuestElementPicker.PickedElements(),
			new MainlineRawNarrativeDraft
			{
				RawText = new string('心', 2500)
			},
			null,
			null
		};
		bool parsed = method != null && (bool)method.Invoke(manager, args);
		MainlineBeat beat = args[4] as MainlineBeat;
		string error = args[5] as string;
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && beat != null && beat.StoryDraft?.ScriptBlocks?.Count == 3 && beat.StoryDraft.ScriptBlocks.All((MainlineScriptBlock block) => string.Equals(block.SceneId, officialSceneId, StringComparison.Ordinal)) && beat.KeyLocationIds.Count == 1 && beat.KeyLocationIds.Contains(officialSceneId);
		return new TestCaseResult
		{
			Name = "MainlineStructuredBeatAcceptsOfficialSceneOutsideCandidatePool",
			Passed = ok,
			Details = (ok ? "Stage-2 parser now accepts officially valid scenes even when they are outside the current candidate pool" : ("Unexpected official-scene parse result: error=" + (error ?? "<null>") + ", beat=" + DescribeMainlineBeat(beat)))
		};
	}

	private static TestCaseResult TestMainlineStructuredBeatAcceptsDecoratedNpcNames(AIQuestManager manager)
	{
		JObject outline = JObject.Parse("{\n  \"title\": \"雨夜问火\",\n  \"summary\": \"测试带修饰语和空白的 NPC 名仍能映射回锚点 NPC。\",\n  \"timeCategory\": \"短期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"雨夜开场\",\n      \"kind\": \"dialogue\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"井紫铃在雨夜主动开口\",\n      \"participantNpcNames\": [\"《井 紫铃》\"],\n      \"lines\": [\"Narrator||窗外风雨渐急。\", \"Npc|「井\\n紫铃」|你既来了，就别只是看着。\"]\n    },\n    {\n      \"blockRef\": \"当场抉择\",\n      \"kind\": \"choice\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"决定如何介入\",\n      \"participantNpcNames\": [\"『井紫铃』\"],\n      \"lines\": [\"Player||这一回，你想我怎么帮你？\"],\n      \"choices\": [\n        { \"choiceRef\": \"安抚分支\", \"text\": \"先稳住她\", \"consequenceDirection\": \"安抚后独立收束\" }\n      ]\n    },\n    {\n      \"blockRef\": \"安抚收束\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"安抚后的独立后续\",\n      \"participantNpcNames\": [\"〈井 紫铃〉\"],\n      \"lines\": [\"Npc|《井 紫铃》|这次算你把我拉回来了。\"],\n      \"branchRef\": \"安抚分支\"\n    }\n  ]\n}");
		MethodInfo method = typeof(AIQuestManager).GetMethod("TryParseMainlineStructuredBeat", BindingFlags.Instance | BindingFlags.NonPublic);
		object[] args = new object[6]
		{
			outline,
			new NPCInfo
			{
				NpcID = 20004,
				Name = "井紫铃"
			},
			new QuestElementPicker.PickedElements
			{
				Locations = new List<QuestElementPicker.PickedLocation>
				{
					new QuestElementPicker.PickedLocation
					{
						Id = "S116",
						Name = "神兵阁",
						RegionName = "竹山宗"
					}
				}
			},
			new MainlineRawNarrativeDraft
			{
				RawText = new string('心', 2500)
			},
			null,
			null
		};
		bool parsed = method != null && (bool)method.Invoke(manager, args);
		MainlineBeat beat = args[4] as MainlineBeat;
		string error = args[5] as string;
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && beat != null && beat.StoryDraft?.ScriptBlocks?.Count == 3 && beat.StoryDraft.ScriptBlocks.All((MainlineScriptBlock block) => block.ParticipantNpcNames.All((string name) => string.Equals(name, "井紫铃", StringComparison.Ordinal))) && beat.StoryDraft.ScriptBlocks.SelectMany((MainlineScriptBlock block) => block.Lines).Count((AIQuestStoryLine line) => line.SpeakerType == QuestStorySpeakerType.Npc) == 2 && beat.KeyNpcIds.Count == 1 && beat.KeyNpcIds[0] == 20004;
		return new TestCaseResult
		{
			Name = "MainlineStructuredBeatAcceptsDecoratedNpcNames",
			Passed = ok,
			Details = (ok ? "Stage-2 parser now maps decorated NPC mentions with wrapper marks and whitespace back to the unique anchor NPC" : ("Unexpected decorated-NPC parse result: error=" + (error ?? "<null>") + ", beat=" + DescribeMainlineBeat(beat)))
		};
	}

	private static TestCaseResult TestMainlineStructuredBeatReportsFirstBlockErrorWhenAllBlocksInvalid(AIQuestManager manager)
	{
		JObject outline = JObject.Parse("{\n  \"title\": \"错误结构化剧情\",\n  \"summary\": \"所有 block 都无效时应返回首个真实错误。\",\n  \"timeCategory\": \"短期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"错误开场\",\n      \"kind\": \"encounter\",\n      \"sceneName\": \"云汐城外的官道\",\n      \"scenePurpose\": \"场景故意无法解析\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Npc|井紫铃|这里的场景名故意不在候选集中。\"]\n    }\n  ]\n}");
		MethodInfo method = typeof(AIQuestManager).GetMethod("TryParseMainlineStructuredBeat", BindingFlags.Instance | BindingFlags.NonPublic);
		object[] args = new object[6]
		{
			outline,
			new NPCInfo
			{
				NpcID = 20004,
				Name = "井紫铃"
			},
			new QuestElementPicker.PickedElements
			{
				Locations = new List<QuestElementPicker.PickedLocation>
				{
					new QuestElementPicker.PickedLocation
					{
						Id = "S301",
						Name = "沂山",
						RegionName = "宁州"
					}
				}
			},
			new MainlineRawNarrativeDraft
			{
				RawText = new string('心', 2500)
			},
			null,
			null
		};
		bool parsed = method != null && (bool)method.Invoke(manager, args);
		MainlineBeat beat = args[4] as MainlineBeat;
		string error = args[5] as string;
		bool ok = !parsed && beat == null && !string.IsNullOrWhiteSpace(error) && error.Contains("scriptBlocks[0].sceneName") && error.Contains("云汐城外的官道") && !string.Equals(error, "scriptBlocks 不能为空", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "MainlineStructuredBeatReportsFirstBlockErrorWhenAllBlocksInvalid",
			Passed = ok,
			Details = (ok ? "Stage-2 parser now surfaces the first real block parse error instead of collapsing to a fake empty-scriptBlocks failure" : string.Format("Unexpected all-invalid structured-beat error: parsed={0}, error={1}, beat={2}", parsed, error ?? "<null>", DescribeMainlineBeat(beat)))
		};
	}

	private static TestCaseResult TestMainlineStructuredBeatNormalizesProtocolValueCase(AIQuestManager manager)
	{
		JObject outline = JObject.Parse("{\n  \"title\": \"大小写归一测试\",\n  \"summary\": \"测试 stage2 对协议值大小写的归一能力。\",\n  \"timeCategory\": \"短期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"BLOCK_INTRO\",\n      \"kind\": \"DIALOGUE\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"主干开场\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"NARRATOR||风声骤紧。\", \"NPC|井紫铃|你既来了，就别只站着。\"]\n    },\n    {\n      \"blockRef\": \"BLOCK_CHOICE\",\n      \"kind\": \"CHOICE\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"当场抉择\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"PLAYER||这一回，你想我怎么做？\"],\n      \"choices\": [\n        { \"choiceRef\": \"CHOICE_A\", \"text\": \"先稳住她\", \"consequenceDirection\": \"安抚路线\" },\n        { \"choiceRef\": \"Choice_B\", \"text\": \"逼她直面\", \"consequenceDirection\": \"强压路线\" }\n      ]\n    },\n    {\n      \"blockRef\": \"BLOCK_A\",\n      \"kind\": \"FALLOUT\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"安抚后的收束\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"npc|井紫铃|这次算你把我拉了回来。\"],\n      \"branchRef\": \"choice_a\"\n    },\n    {\n      \"blockRef\": \"BLOCK_B\",\n      \"kind\": \"FALLOUT\",\n      \"sceneName\": \"神兵阁\",\n      \"scenePurpose\": \"强压后的收束\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\"Npc|井紫铃|既然你逼我，那我便把话说尽。\"],\n      \"branchRef\": \"CHOICE_B\"\n    }\n  ]\n}");
		MethodInfo method = typeof(AIQuestManager).GetMethod("TryParseMainlineStructuredBeat", BindingFlags.Instance | BindingFlags.NonPublic);
		object[] args = new object[5]
		{
			outline,
			new NPCInfo
			{
				NpcID = 20004,
				Name = "井紫铃"
			},
			new MainlineRawNarrativeDraft
			{
				RawText = new string('心', 2500)
			},
			null,
			null
		};
		bool parsed = method != null && (bool)method.Invoke(manager, args);
		MainlineBeat beat = args[3] as MainlineBeat;
		string error = args[4] as string;
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && beat != null && beat.StoryDraft?.ScriptBlocks?.Count == 4 && string.Equals(beat.StoryDraft.ScriptBlocks[0].BlockRef, "block_intro", StringComparison.Ordinal) && beat.StoryDraft.ScriptBlocks[1].Kind == MainlineScriptBlockKind.Choice && beat.StoryDraft.ScriptBlocks[1].Choices.Any((MainlineDialogueChoiceDraft choice) => string.Equals(choice.ChoiceRef, "choice_a", StringComparison.Ordinal)) && beat.StoryDraft.ScriptBlocks[2].Lines.Any((AIQuestStoryLine line) => line.SpeakerType == QuestStorySpeakerType.Npc) && string.Equals(beat.StoryDraft.ScriptBlocks[3].BranchRef, "choice_b", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "MainlineStructuredBeatNormalizesProtocolValueCase",
			Passed = ok,
			Details = (ok ? "Stage-2 parser now treats kind, compact speakerType, choiceRef, blockRef, and branchRef as case-insensitive protocol values" : ("Unexpected stage-2 normalization result: error=" + (error ?? "<null>") + ", beat=" + DescribeMainlineBeat(beat)))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanRequiresFlowSteps(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": []\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_1",
			Title = "第一拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						ScenePurpose = "测试",
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("测试对白", 20004) }
					}
				}
			}
		}) as MainlineBeatGamePlan;
		bool ok = parsed == null;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanRequiresFlowSteps",
			Passed = ok,
			Details = (ok ? "Stage-2 semantic planner now hard-rejects plans without non-empty flowSteps" : ("Stage-2 functional plan unexpectedly accepted missing flowSteps: " + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanAllowsTerminalBeatWithoutExtraFields(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": [\n    { \"stepRef\": \"进入现场\", \"kind\": \"encounter_at_scene\", \"sceneName\": \"神兵阁\", \"targetNpcName\": \"井紫铃\" },\n    { \"stepRef\": \"抉择入口\", \"kind\": \"present_choice\", \"sourceBlockRef\": \"最后抉择\" },\n    { \"stepRef\": \"信任后续\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"信任收束\", \"branchRef\": \"信任分支\" },\n    { \"stepRef\": \"强压后续\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"强压收束\", \"branchRef\": \"强压分支\" }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_terminal",
			Title = "终局拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "最后抉择",
						Kind = MainlineScriptBlockKind.Choice,
						SceneName = "神兵阁",
						ScenePurpose = "测试",
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreatePlayer("如何了断今日之事？") },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "信任分支",
								Text = "信她一次"
							},
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "强压分支",
								Text = "逼她直面"
							}
						}
					},
					new MainlineScriptBlock
					{
						BlockRef = "信任收束",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneName = "神兵阁",
						ScenePurpose = "分支 A 终局",
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("我会把这桩旧事亲手了断。", 20004) },
						BranchRef = "信任分支"
					},
					new MainlineScriptBlock
					{
						BlockRef = "强压收束",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneName = "神兵阁",
						ScenePurpose = "分支 B 终局",
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("既然你要逼我，那就把话一次说尽。", 20004) },
						BranchRef = "强压分支"
					}
				}
			}
		}) as MainlineBeatGamePlan;
		bool ok = parsed != null && parsed.UsedNpcIds.Count == 1 && parsed.UsedNpcIds[0] == 20004 && parsed.FlowSteps[0].StepRef == "进入现场" && parsed.FlowSteps != null && parsed.FlowSteps.Count == 4 && parsed.FlowSteps.All((MainlineSemanticFlowStep step) => step.Effects != null && step.Effects.Count == 0);
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanAllowsTerminalBeatWithoutExtraFields",
			Passed = ok,
			Details = (ok ? "Stage-3 parser now accepts a branch-only terminal beat without inventing any public merge step" : ("Stage-3 parser still rejected a branch-only terminal beat that only used current schema fields: parsed=" + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanNormalizesProtocolValueCase(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": [\n    { \"stepRef\": \"STEP_MEET\", \"kind\": \"ENCOUNTER_AT_SCENE\", \"sceneName\": \"神兵阁\", \"targetNpcName\": \"井紫铃\" },\n    { \"stepRef\": \"STEP_CHOICE\", \"kind\": \"PRESENT_CHOICE\", \"sourceBlockRef\": \"BLOCK_CHOICE\" },\n    { \"stepRef\": \"STEP_BRANCH_A\", \"kind\": \"PLAY_SCRIPT_BLOCK\", \"sourceBlockRef\": \"block_a\", \"branchRef\": \"choice_a\", \"effects\": [ { \"effectRef\": \"EFFECT_A\", \"kind\": \"FAVOR_CHANGE\", \"targetNpcName\": \"井紫铃\", \"payload\": { \"amount\": 10 } } ] },\n    { \"stepRef\": \"STEP_BRANCH_B\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"BLOCK_B\", \"branchRef\": \"CHOICE_B\", \"effects\": [ { \"effectRef\": \"effect_b\", \"kind\": \"grant_money\", \"payload\": { \"money\": 50 } } ] }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_case",
			Title = "大小写归一测试",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_choice",
						Kind = MainlineScriptBlockKind.Choice,
						SceneName = "神兵阁",
						SceneId = "S116",
						ScenePurpose = "抉择",
						ParticipantNpcNames = new List<string> { "井紫铃" },
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreatePlayer("这一回，你想让我怎么做？") },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_a",
								Text = "先稳住她"
							},
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_b",
								Text = "逼她直面"
							}
						}
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_a",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneName = "神兵阁",
						SceneId = "S116",
						ScenePurpose = "安抚后的收束",
						ParticipantNpcNames = new List<string> { "井紫铃" },
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("你总算把我拉回来了。", 20004) },
						BranchRef = "choice_a"
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_b",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneName = "神兵阁",
						SceneId = "S116",
						ScenePurpose = "强压后的收束",
						ParticipantNpcNames = new List<string> { "井紫铃" },
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("既然你逼我，那我便把话说尽。", 20004) },
						BranchRef = "choice_b"
					}
				}
			}
		}) as MainlineBeatGamePlan;
		bool ok = parsed != null && parsed.FlowSteps.Count == 4 && parsed.FlowSteps[0].Kind == MainlineSemanticFlowStepKind.EncounterAtScene && string.Equals(parsed.FlowSteps[0].StepRef, "step_meet", StringComparison.Ordinal) && string.Equals(parsed.FlowSteps[1].SourceBlockRef, "block_choice", StringComparison.Ordinal) && string.Equals(parsed.FlowSteps[2].BranchRef, "choice_a", StringComparison.Ordinal) && string.Equals(parsed.FlowSteps[2].Effects[0].EffectRef, "effect_a", StringComparison.Ordinal) && parsed.FlowSteps[2].Effects[0].Kind == MainlineSemanticEffectStepKind.FavorChange && string.Equals(parsed.FlowSteps[3].BranchRef, "choice_b", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanNormalizesProtocolValueCase",
			Passed = ok,
			Details = (ok ? "Stage-3 parser now treats flow/effect kind and all model-side refs as case-insensitive protocol values" : ("Unexpected stage-3 normalization result: " + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanAcceptsDecoratedNpcAndSceneNames(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": [\n    { \"stepRef\": \"先去见她\", \"kind\": \"encounter_at_scene\", \"sceneName\": \"《神兵\\n阁》\", \"targetNpcName\": \"「井 紫铃」\" },\n    { \"stepRef\": \"抉择入口\", \"kind\": \"present_choice\", \"sourceBlockRef\": \"抉择块\" },\n    { \"stepRef\": \"安抚后续\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"安抚块\", \"branchRef\": \"安抚分支\", \"effects\": [ { \"effectRef\": \"好感变化\", \"kind\": \"favor_change\", \"targetNpcName\": \"『井紫铃』\", \"payload\": { \"amount\": 12 } } ] }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_decorated_refs",
			Title = "装饰名解析测试",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "抉择块",
						Kind = MainlineScriptBlockKind.Choice,
						SceneName = "神兵阁",
						SceneId = "S116",
						ScenePurpose = "抉择",
						ParticipantNpcNames = new List<string> { "井紫铃" },
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreatePlayer("这一回，你想让我怎么做？") },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "安抚分支",
								Text = "先稳住她"
							}
						}
					},
					new MainlineScriptBlock
					{
						BlockRef = "安抚块",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneName = "神兵阁",
						SceneId = "S116",
						ScenePurpose = "安抚后的收束",
						ParticipantNpcNames = new List<string> { "井紫铃" },
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("这次算你把我拉回来了。", 20004) },
						BranchRef = "安抚分支"
					}
				}
			}
		}) as MainlineBeatGamePlan;
		bool ok = parsed != null && parsed.FlowSteps.Count == 3 && string.Equals(parsed.FlowSteps[0].SceneName, "神兵阁", StringComparison.Ordinal) && string.Equals(parsed.FlowSteps[0].SceneId, "S116", StringComparison.Ordinal) && string.Equals(parsed.FlowSteps[0].TargetNpcName, "井紫铃", StringComparison.Ordinal) && parsed.FlowSteps[0].TargetNpcId == 20004 && string.Equals(parsed.FlowSteps[2].BranchRef, "安抚分支", StringComparison.Ordinal) && parsed.FlowSteps[2].Effects.Count == 1 && parsed.FlowSteps[2].Effects[0].TargetNpcId == 20004 && string.Equals(parsed.FlowSteps[2].Effects[0].TargetNpcName, "井紫铃", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanAcceptsDecoratedNpcAndSceneNames",
			Passed = ok,
			Details = (ok ? "Stage-3 parser now resolves decorated NPC and scene names with whitespace variance back to beat-local entities" : ("Unexpected decorated stage-3 parse result: " + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanAcceptsAcquireAndDeliverFlowKinds(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": [\n    { \"stepRef\": \"先与她见面\", \"kind\": \"encounter_at_scene\", \"sceneName\": \"竹山宗神兵阁\", \"targetNpcName\": \"井紫铃\" },\n    { \"stepRef\": \"当场抉择\", \"kind\": \"present_choice\", \"sourceBlockRef\": \"block_1\" },\n    { \"stepRef\": \"筹备所需资源\", \"kind\": \"acquire_item_gate\", \"payload\": { \"itemName\": \"清心玉露丸\", \"count\": 2 }, \"branchRef\": \"choice_help\" },\n    { \"stepRef\": \"当面交付资源\", \"kind\": \"deliver_item_in_person\", \"sceneName\": \"竹山宗神兵阁\", \"targetNpcName\": \"井紫铃\", \"payload\": { \"itemName\": \"清心玉露丸\", \"count\": 2 }, \"branchRef\": \"choice_help\" },\n    { \"stepRef\": \"交付后回应\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"block_2\", \"branchRef\": \"choice_help\" },\n    { \"stepRef\": \"离开后回应\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"block_3\", \"branchRef\": \"choice_leave\" }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_terminal",
			Title = "交付拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_1",
						Kind = MainlineScriptBlockKind.Choice,
						SceneId = "S116",
						ScenePurpose = "测试",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("你是准备帮我，还是就此离开？", 20004) },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_help",
								Text = "去准备资源"
							},
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_leave",
								Text = "转身离开"
							}
						}
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_2",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S116",
						ScenePurpose = "交付后的后续",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("这份心境资源够我压住火毒了。", 20004) },
						BranchRef = "choice_help"
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_3",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S116",
						ScenePurpose = "离开的后续",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("既然你不肯插手，那便到此为止。", 20004) },
						BranchRef = "choice_leave"
					}
				}
			}
		}) as MainlineBeatGamePlan;
		bool ok = parsed != null && parsed.FlowSteps.Count == 6 && parsed.FlowSteps[2].Kind == MainlineSemanticFlowStepKind.AcquireItemGate && parsed.FlowSteps[3].Kind == MainlineSemanticFlowStepKind.DeliverItemInPerson;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanAcceptsAcquireAndDeliverFlowKinds",
			Passed = ok,
			Details = (ok ? "Stage-2 parser now accepts acquire_item_gate and deliver_item_in_person" : ("Stage-2 parser did not accept new delivery primitives: " + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanAcceptsVisitAndReportFlowKinds(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"summary\": \"先去云汐城踩点，再回竹山宗当面回报井紫铃。\",\n  \"flowSteps\": [\n    { \"stepRef\": \"先决定要不要查\", \"kind\": \"present_choice\", \"sourceBlockRef\": \"block_1\" },\n    { \"stepRef\": \"去云汐城踩点\", \"kind\": \"visit_scene_gate\", \"sceneName\": \"云汐城\", \"branchRef\": \"choice_check\" },\n    { \"stepRef\": \"回去当面回报\", \"kind\": \"report_back_in_person\", \"sceneName\": \"竹山宗藏经阁\", \"targetNpcName\": \"井紫铃\", \"branchRef\": \"choice_check\" },\n    { \"stepRef\": \"回报后的回应\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"block_2\", \"branchRef\": \"choice_check\" },\n    { \"stepRef\": \"按下不查的后续\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"block_3\", \"branchRef\": \"choice_ignore\" }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_visit_report",
			Title = "调查回报拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_1",
						Kind = MainlineScriptBlockKind.Choice,
						SceneId = "S114",
						ScenePurpose = "测试",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("你是先去踩点，还是暂且按下不表？", 20004) },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_check",
								Text = "去查清楚再回来"
							},
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_ignore",
								Text = "暂且不查"
							}
						}
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_2",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S114",
						ScenePurpose = "踩点回报后的后续",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("既已摸清风声，我便知道下一步该怎么走。", 20004) },
						BranchRef = "choice_check"
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_3",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S114",
						ScenePurpose = "不查的后续",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("既然你不愿去看，那这桩事便由我自己收拾。", 20004) },
						BranchRef = "choice_ignore"
					}
				}
			}
		}) as MainlineBeatGamePlan;
		bool ok = parsed != null && parsed.FlowSteps.Count == 5 && parsed.FlowSteps[1].Kind == MainlineSemanticFlowStepKind.VisitSceneGate && parsed.FlowSteps[2].Kind == MainlineSemanticFlowStepKind.ReportBackInPerson;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanAcceptsVisitAndReportFlowKinds",
			Passed = ok,
			Details = (ok ? "Stage-2 parser now accepts visit_scene_gate and report_back_in_person" : ("Stage-2 parser did not accept visit/report primitives: " + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanRejectsUnsupportedFlowKind(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": [\n    { \"stepRef\": \"错误步骤\", \"kind\": \"unsupported_flow_kind\", \"sceneName\": \"竹山宗神兵阁\", \"targetNpcName\": \"井紫铃\", \"payload\": { \"itemName\": \"清心玉露丸\" } }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_invalid_flow_kind",
			Title = "无效原语拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_1",
						Kind = MainlineScriptBlockKind.Dialogue,
						SceneId = "S116",
						ScenePurpose = "测试",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("未定义原语不该进来。", 20004) }
					}
				}
			}
		}) as MainlineBeatGamePlan;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanRejectsUnsupportedFlowKind",
			Passed = (parsed == null),
			Details = ((parsed == null) ? "Stage-2 parser now rejects unsupported flow kinds" : ("Stage-2 parser still accepted an unsupported flow kind: " + JsonConvert.SerializeObject(parsed, JsonSettings)))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanIgnoresUnsupportedEffectKind(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": [\n    { \"stepRef\": \"进入抉择\", \"kind\": \"present_choice\", \"sourceBlockRef\": \"block_choice\" },\n    { \"stepRef\": \"进入分支\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"block_branch\", \"branchRef\": \"choice_a\", \"effects\": [ { \"effectRef\": \"错误效果\", \"kind\": \"unsupported_effect_kind\" } ] }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_invalid_effect_kind",
			Title = "无效效果拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_choice",
						Kind = MainlineScriptBlockKind.Choice,
						SceneId = "S114",
						ScenePurpose = "测试",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreatePlayer("如何选择？") },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_a",
								Text = "继续",
								ConsequenceDirection = "测试"
							}
						}
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_branch",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S114",
						ScenePurpose = "测试分支",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("未定义效果不该进来。", 20004) },
						BranchRef = "choice_a"
					}
				}
			}
		}) as MainlineBeatGamePlan;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanIgnoresUnsupportedEffectKind",
			Passed = (parsed != null && parsed.FlowSteps.Count == 2 && parsed.FlowSteps.All((MainlineSemanticFlowStep step) => step.Effects.Count == 0)),
			Details = ((parsed != null && parsed.FlowSteps.Count == 2 && parsed.FlowSteps.All((MainlineSemanticFlowStep step) => step.Effects.Count == 0)) ? "Stage-3 parser now ignores unsupported effect kinds instead of aborting the whole plan" : ("Unexpected unsupported-effect tolerance result: " + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanIgnoresIllegalGrantItemEffect(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": [\n    { \"stepRef\": \"进入抉择\", \"kind\": \"present_choice\", \"sourceBlockRef\": \"block_choice\" },\n    {\n      \"stepRef\": \"进入分支\",\n      \"kind\": \"play_script_block\",\n      \"sourceBlockRef\": \"block_branch\",\n      \"branchRef\": \"choice_a\",\n      \"effects\": [\n        {\n          \"effectRef\": \"非法物品效果\",\n          \"kind\": \"grant_item\",\n          \"payload\": {\n            \"itemName\": \"剧情测试专用锦盒\",\n            \"count\": 1\n          }\n        }\n      ]\n    }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_ignore_illegal_grant_item_effect",
			Title = "非法物品效果拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_choice",
						Kind = MainlineScriptBlockKind.Choice,
						SceneId = "S114",
						ScenePurpose = "测试",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreatePlayer("如何选择？") },
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_a",
								Text = "继续",
								ConsequenceDirection = "测试"
							}
						}
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_branch",
						Kind = MainlineScriptBlockKind.Fallout,
						SceneId = "S114",
						ScenePurpose = "测试分支",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("非法道具奖励不该落地。", 20004) },
						BranchRef = "choice_a"
					}
				}
			}
		}) as MainlineBeatGamePlan;
		bool ok = parsed != null && parsed.FlowSteps.Count == 2 && parsed.FlowSteps[1].Kind == MainlineSemanticFlowStepKind.PlayScriptBlock && parsed.FlowSteps[1].Effects.Count == 0;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanIgnoresIllegalGrantItemEffect",
			Passed = ok,
			Details = (ok ? "Stage-3 parser now drops grant_item effects whose itemName is not present in the official item pool" : ("Unexpected illegal-grant-item tolerance result: " + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanIgnoresIllegalItemFlowStep(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": [\n    { \"stepRef\": \"先会面\", \"kind\": \"encounter_at_scene\", \"sceneName\": \"竹山宗神兵阁\", \"targetNpcName\": \"井紫铃\" },\n    {\n      \"stepRef\": \"非法交付\",\n      \"kind\": \"deliver_item_in_person\",\n      \"sceneName\": \"竹山宗神兵阁\",\n      \"targetNpcName\": \"井紫铃\",\n      \"payload\": {\n        \"itemName\": \"剧情测试专用锦盒\",\n        \"count\": 1\n      }\n    }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_ignore_illegal_item_step",
			Title = "非法交付步骤拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_1",
						Kind = MainlineScriptBlockKind.Dialogue,
						SceneId = "S114",
						ScenePurpose = "测试",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("先会面。", 20004) }
					}
				}
			}
		}) as MainlineBeatGamePlan;
		bool ok = parsed != null && parsed.FlowSteps.Count == 1 && parsed.FlowSteps[0].Kind == MainlineSemanticFlowStepKind.EncounterAtScene;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanIgnoresIllegalItemFlowStep",
			Passed = ok,
			Details = (ok ? "Stage-3 parser now drops item-bound flow steps whose itemName is not present in the official item pool" : ("Unexpected illegal-item-step tolerance result: " + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanRejectsChoiceScopedFlowWithoutPresentChoice(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"summary\": \"没有前置选项却试图走分支。\",\n  \"flowSteps\": [\n    { \"stepRef\": \"错误分支步骤\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"block_1\", \"branchRef\": \"choice_a\" }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_choice_scope",
			Title = "错误分支拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_1",
						Kind = MainlineScriptBlockKind.Dialogue,
						SceneId = "S116",
						ScenePurpose = "测试",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("没有 choice 也想分支。", 20004) }
					}
				}
			}
		}) as MainlineBeatGamePlan;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanRejectsChoiceScopedFlowWithoutPresentChoice",
			Passed = (parsed == null),
			Details = ((parsed == null) ? "Stage-3 parser now rejects branchRef without a preceding present_choice" : ("Stage-2 parser still accepted choice-scoped flow without present_choice: " + JsonConvert.SerializeObject(parsed, JsonSettings)))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanRejectsMissingScriptBlockReference(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"summary\": \"引用了不存在的剧情脚本块。\",\n  \"flowSteps\": [\n    { \"stepRef\": \"引用缺失脚本\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"missing_block\" }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_missing_block",
			Title = "缺块测试",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_1",
						Kind = MainlineScriptBlockKind.Dialogue,
						SceneId = "S116",
						ScenePurpose = "测试",
						ParticipantNpcIds = new List<int> { 20004 },
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("测试对白", 20004) }
					}
				}
			}
		}) as MainlineBeatGamePlan;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanRejectsMissingScriptBlockReference",
			Passed = (parsed == null),
			Details = ((parsed == null) ? "Stage-3 parser now rejects flow steps that invent sourceBlockRef values not present in the stage-2 scriptBlocks" : ("Stage-2 parser still accepted a missing scriptBlock reference: " + JsonConvert.SerializeObject(parsed, JsonSettings)))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanIgnoresUnexpectedRootField(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": [\n    { \"stepRef\": \"进入抉择\", \"kind\": \"present_choice\", \"sourceBlockRef\": \"block_choice\" },\n    { \"stepRef\": \"进入分支\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"block_branch\", \"branchRef\": \"choice_a\" }\n  ],\n  \"unexpectedField\": true\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_1",
			Title = "第一拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_choice",
						Kind = MainlineScriptBlockKind.Choice,
						ScenePurpose = "测试",
						SceneId = "S116",
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_a",
								Text = "继续",
								ConsequenceDirection = "测试"
							}
						},
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreatePlayer("测试对白") }
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_branch",
						Kind = MainlineScriptBlockKind.Fallout,
						ScenePurpose = "测试分支",
						SceneId = "S116",
						BranchRef = "choice_a",
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("测试分支对白", 20004) }
					}
				}
			}
		}) as MainlineBeatGamePlan;
		bool ok = parsed != null && parsed.FlowSteps.Count == 2 && parsed.FlowSteps[0].Kind == MainlineSemanticFlowStepKind.PresentChoice && parsed.FlowSteps[1].Kind == MainlineSemanticFlowStepKind.PlayScriptBlock;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanIgnoresUnexpectedRootField",
			Passed = ok,
			Details = (ok ? "Stage-3 parser now ignores extra root fields and keeps valid flowSteps" : ("Unexpected root-field tolerance result: parsed=" + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestMainlineBeatGamePlanIgnoresUnexpectedProtocolField(AIQuestManager manager)
	{
		JObject chapterPlan = JObject.Parse("{\n  \"flowSteps\": [\n    { \"stepRef\": \"进入抉择\", \"kind\": \"present_choice\", \"sourceBlockRef\": \"block_choice\", \"unexpectedField\": true },\n    { \"stepRef\": \"进入分支\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"block_branch\", \"branchRef\": \"choice_a\" }\n  ]\n}");
		MainlineBeatGamePlan parsed = InvokeNonPublic(manager, "ParseMainlineBeatGamePlan", chapterPlan, new MainlineBeat
		{
			BeatId = "beat_1",
			Title = "第一拍",
			Summary = "测试",
			StoryDraft = new MainlineBeatStoryDraft
			{
				ScriptBlocks = new List<MainlineScriptBlock>
				{
					new MainlineScriptBlock
					{
						BlockRef = "block_choice",
						Kind = MainlineScriptBlockKind.Choice,
						ScenePurpose = "测试",
						SceneId = "S116",
						Choices = new List<MainlineDialogueChoiceDraft>
						{
							new MainlineDialogueChoiceDraft
							{
								ChoiceRef = "choice_a",
								Text = "继续",
								ConsequenceDirection = "测试"
							}
						},
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreatePlayer("测试对白") }
					},
					new MainlineScriptBlock
					{
						BlockRef = "block_branch",
						Kind = MainlineScriptBlockKind.Fallout,
						ScenePurpose = "测试分支",
						SceneId = "S116",
						BranchRef = "choice_a",
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("测试分支对白", 20004) }
					}
				}
			}
		}) as MainlineBeatGamePlan;
		return new TestCaseResult
		{
			Name = "MainlineBeatGamePlanIgnoresUnexpectedProtocolField",
			Passed = (parsed != null && parsed.FlowSteps.Count == 2 && parsed.FlowSteps[0].Kind == MainlineSemanticFlowStepKind.PresentChoice && parsed.FlowSteps[1].Kind == MainlineSemanticFlowStepKind.PlayScriptBlock),
			Details = ((parsed != null && parsed.FlowSteps.Count == 2 && parsed.FlowSteps[0].Kind == MainlineSemanticFlowStepKind.PresentChoice && parsed.FlowSteps[1].Kind == MainlineSemanticFlowStepKind.PlayScriptBlock) ? "Stage-3 parser now ignores extra semantic-step fields and keeps the valid step" : ("Unexpected protocol-field tolerance result: " + ((parsed == null) ? "<null>" : JsonConvert.SerializeObject(parsed, JsonSettings))))
		};
	}

	private static TestCaseResult TestQuestProgramParserAcceptsCompositePredicateChildren()
	{
		string json = "{\r\n  \"title\": \"组合谓词合法程序\",\r\n  \"summary\": \"测试 logic.all 顶层 children 解析\",\r\n  \"worldContract\": {\r\n    \"allowedNpcIds\": [20004],\r\n    \"allowedSceneIds\": [\"S116\"],\r\n    \"selectedPredicates\": [\"logic.all\", \"scene.is\"],\r\n    \"selectedCommands\": [\"npc.set_action\"]\r\n  },\r\n  \"nodes\": [\r\n    {\r\n      \"nodeId\": \"n1\",\r\n      \"type\": \"Objective\",\r\n      \"title\": \"等待进入神兵阁\",\r\n      \"activationPredicates\": [\r\n        {\r\n          \"opcode\": \"logic.all\",\r\n          \"children\": [\r\n            {\r\n              \"opcode\": \"scene.is\",\r\n              \"params\": {\r\n                \"sceneId\": \"S116\"\r\n              }\r\n            }\r\n          ]\r\n        }\r\n      ],\r\n      \"onActivated\": [\r\n        { \"opcode\": \"npc.set_action\", \"params\": { \"npcId\": \"20004\", \"actionId\": \"46\" } }\r\n      ]\r\n    }\r\n  ]\r\n}";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = parsed && string.IsNullOrWhiteSpace(error) && program != null && program.Nodes != null && program.Nodes.Count == 1 && program.Nodes[0].ActivationPredicates.Count == 1 && program.Nodes[0].ActivationPredicates[0].Children.Count == 1;
		return new TestCaseResult
		{
			Name = "QuestProgramParserAcceptsCompositePredicateChildren",
			Passed = ok,
			Details = (ok ? "Stage-4 parser now accepts logic.* predicates when children are supplied as top-level structural fields" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestQuestProgramParserRejectsCompositePredicateParamsChildren()
	{
		string json = "{\r\n  \"title\": \"组合谓词错误程序\",\r\n  \"summary\": \"测试 params.children 仍然非法\",\r\n  \"worldContract\": {\r\n    \"allowedNpcIds\": [20004],\r\n    \"allowedSceneIds\": [\"S116\"],\r\n    \"selectedPredicates\": [\"logic.all\", \"scene.is\"],\r\n    \"selectedCommands\": [\"npc.set_action\"]\r\n  },\r\n  \"nodes\": [\r\n    {\r\n      \"nodeId\": \"n1\",\r\n      \"type\": \"Objective\",\r\n      \"title\": \"错误 children 结构\",\r\n      \"activationPredicates\": [\r\n        {\r\n          \"opcode\": \"logic.all\",\r\n          \"params\": {\r\n            \"children\": [\r\n              {\r\n                \"opcode\": \"scene.is\",\r\n                \"params\": {\r\n                  \"sceneId\": \"S116\"\r\n                }\r\n              }\r\n            ]\r\n          }\r\n        }\r\n      ],\r\n      \"onActivated\": [\r\n        { \"opcode\": \"npc.set_action\", \"params\": { \"npcId\": \"20004\", \"actionId\": \"46\" } }\r\n      ]\r\n    }\r\n  ]\r\n}";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = !parsed && program == null && !string.IsNullOrWhiteSpace(error) && error.Contains("QuestProgram 解析失败");
		return new TestCaseResult
		{
			Name = "QuestProgramParserRejectsCompositePredicateParamsChildren",
			Passed = ok,
			Details = (ok ? "Stage-4 parser still rejects params.children, so incorrect composite predicate structure is exposed immediately" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestQuestProgramParserRejectsCompositePredicateWithoutChildren()
	{
		string json = "{\r\n  \"title\": \"组合谓词缺 children\",\r\n  \"summary\": \"测试缺少 children 的 logic.all\",\r\n  \"worldContract\": {\r\n    \"allowedNpcIds\": [20004],\r\n    \"allowedSceneIds\": [\"S116\"],\r\n    \"selectedPredicates\": [\"logic.all\"],\r\n    \"selectedCommands\": [\"npc.set_action\"]\r\n  },\r\n  \"nodes\": [\r\n    {\r\n      \"nodeId\": \"n1\",\r\n      \"type\": \"Objective\",\r\n      \"title\": \"缺少 children\",\r\n      \"activationPredicates\": [\r\n        {\r\n          \"opcode\": \"logic.all\"\r\n        }\r\n      ],\r\n      \"onActivated\": [\r\n        { \"opcode\": \"npc.set_action\", \"params\": { \"npcId\": \"20004\", \"actionId\": \"46\" } }\r\n      ]\r\n    }\r\n  ]\r\n}";
		QuestProgram program;
		string error;
		bool parsed = QuestProgramParser.TryParseAndValidateForTest(json, out program, out error);
		bool ok = !parsed && program == null && !string.IsNullOrWhiteSpace(error) && error.Contains("必须至少包含一个 child predicate");
		return new TestCaseResult
		{
			Name = "QuestProgramParserRejectsCompositePredicateWithoutChildren",
			Passed = ok,
			Details = (ok ? "Stage-4 parser still rejects logic.* predicates when top-level children are missing" : string.Format("Unexpected parser result: parsed={0}, error={1}, program={2}", parsed, error ?? "null", DescribeQuestProgram(program)))
		};
	}

	private static TestCaseResult TestAcceptQuestProgramSeedsRuntimeNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		QuestProgramState state = new QuestProgramState
		{
			Programs = new List<QuestProgram>
			{
				new QuestProgram
				{
					ProgramId = "program_accept_001",
					Title = "主线第一拍",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Draft,
					Nodes = new List<QuestNode>
					{
						new QuestNode
						{
							NodeId = "node_intro",
							Title = "起势",
							Type = QuestNodeType.Narrative
						},
						new QuestNode
						{
							NodeId = "node_objective",
							Title = "查探",
							Type = QuestNodeType.Objective
						}
					},
					Runtime = new QuestRuntimeState()
				}
			}
		};
		SetField(manager, "_questProgramState", state);
		bool accepted = manager.AcceptQuestProgram("program_accept_001");
		QuestProgram acceptedProgram = GetQuestProgramState(manager).Programs.FirstOrDefault((QuestProgram program) => string.Equals(program.ProgramId, "program_accept_001", StringComparison.Ordinal));
		bool ok = accepted && acceptedProgram != null && acceptedProgram.Status == QuestProgramStatus.Active && string.Equals(acceptedProgram.Runtime?.CurrentNodeId, "node_intro", StringComparison.Ordinal) && acceptedProgram.Runtime?.ActivatedNodeIds != null && acceptedProgram.Runtime.ActivatedNodeIds.Contains("node_intro");
		return new TestCaseResult
		{
			Name = "AcceptQuestProgramSeedsRuntimeNode",
			Passed = ok,
			Details = (ok ? "Accepting a draft quest program now activates the first node directly in new runtime state" : $"Unexpected quest program activation state: accepted={accepted}, program={DescribeQuestProgram(acceptedProgram)}")
		};
	}

	private static TestCaseResult TestGetActiveQuestProgramsReturnsOnlyActive(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram>
			{
				new QuestProgram
				{
					ProgramId = "program_active",
					Title = "进行中的主线",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Active
				},
				new QuestProgram
				{
					ProgramId = "program_draft",
					Title = "待接主线",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Draft
				},
				new QuestProgram
				{
					ProgramId = "program_failed",
					Title = "失败主线",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Failed
				}
			}
		});
		List<QuestProgram> activePrograms = manager.GetActiveQuestPrograms();
		bool ok = activePrograms.Count == 1 && string.Equals(activePrograms[0].ProgramId, "program_active", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "GetActiveQuestProgramsReturnsOnlyActive",
			Passed = ok,
			Details = (ok ? "Quest panel data source can now enumerate only active quest programs without mixing in drafts or failed programs" : ("Unexpected active quest programs: " + JsonConvert.SerializeObject(activePrograms, JsonSettings)))
		};
	}

	private static TestCaseResult TestGetPendingQuestProgramsReturnsOnlyDraft(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram>
			{
				new QuestProgram
				{
					ProgramId = "program_draft_mainline",
					Title = "待接取主线",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Draft
				},
				new QuestProgram
				{
					ProgramId = "program_active_mainline",
					Title = "进行中的主线",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Active
				},
				new QuestProgram
				{
					ProgramId = "program_suspended_mainline",
					Title = "挂起的主线",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Suspended
				}
			}
		});
		List<QuestProgram> pendingPrograms = manager.GetPendingQuestPrograms();
		bool ok = pendingPrograms.Count == 1 && string.Equals(pendingPrograms[0].ProgramId, "program_draft_mainline", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "GetPendingQuestProgramsReturnsOnlyDraft",
			Passed = ok,
			Details = (ok ? "Quest panel pending data source now exposes only draft mainline QuestPrograms" : ("Unexpected pending quest programs: " + JsonConvert.SerializeObject(pendingPrograms, JsonSettings)))
		};
	}

	private static TestCaseResult TestGetQuestProgramHistoryReturnsOnlyTerminal(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram>
			{
				new QuestProgram
				{
					ProgramId = "program_completed",
					Title = "已完成程序",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Completed
				},
				new QuestProgram
				{
					ProgramId = "program_failed",
					Title = "已失败程序",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Failed
				},
				new QuestProgram
				{
					ProgramId = "program_suspended",
					Title = "已挂起程序",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Suspended
				},
				new QuestProgram
				{
					ProgramId = "program_active",
					Title = "进行中程序",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Active
				},
				new QuestProgram
				{
					ProgramId = "program_draft",
					Title = "草稿程序",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Draft
				}
			}
		});
		List<QuestProgram> historyPrograms = manager.GetQuestProgramHistory();
		bool ok = historyPrograms.Count == 3 && string.Equals(historyPrograms[0].ProgramId, "program_completed", StringComparison.Ordinal) && string.Equals(historyPrograms[1].ProgramId, "program_failed", StringComparison.Ordinal) && string.Equals(historyPrograms[2].ProgramId, "program_suspended", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "GetQuestProgramHistoryReturnsOnlyTerminal",
			Passed = ok,
			Details = (ok ? "Quest panel history now comes directly from terminal QuestProgram states" : ("Unexpected quest program history: " + JsonConvert.SerializeObject(historyPrograms, JsonSettings)))
		};
	}

	private static TestCaseResult TestQuestVisibilityQueriesUseQuestPrograms(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram>
			{
				new QuestProgram
				{
					ProgramId = "draft_for_20101",
					AnchorNpcId = 20101,
					Title = "草稿程序",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Draft
				},
				new QuestProgram
				{
					ProgramId = "active_for_20102",
					AnchorNpcId = 20102,
					Title = "进行中程序",
					TrackType = QuestTrackType.Mainline,
					Status = QuestProgramStatus.Active
				}
			}
		});
		bool hasPending = manager.HasPendingQuest(20101);
		bool hasActive = manager.HasActiveQuest(20102);
		bool reservedDraft = manager.IsNpcReservedByAIQuest(20101);
		bool reservedActive = manager.IsNpcReservedByAIQuest(20102);
		bool ok = hasPending && hasActive && reservedDraft && reservedActive;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "QuestVisibilityQueriesUseQuestPrograms";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Visibility queries resolve directly against QuestProgram state" : $"Legacy visibility mismatch: hasPending={hasPending}, hasActive={hasActive}, reservedDraft={reservedDraft}, reservedActive={reservedActive}");
		return testCaseResult;
	}

	private static TestCaseResult TestMainlineProgramGenerationValidationUsesQuestPrograms(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram>()
		});
		SetField(manager, "_mainlineCampaign", CreateSingleBeatMainlineCampaignWithPendingBiographyEvent("mainline_validation", PendingBiographyEventStatus.Captured));
		NPCInfo npcInfo = new NPCInfo
		{
			NpcID = 930100,
			Name = "主线发起人"
		};
		string error;
		bool ok = manager.TryValidateMainlineProgramGenerationForTest(npcInfo, out error) == MainlineGenerationStartResult.Started && string.IsNullOrWhiteSpace(error);
		return new TestCaseResult
		{
			Name = "MainlineProgramGenerationValidationUsesQuestPrograms",
			Passed = ok,
			Details = (ok ? "Mainline generation is blocked only by QuestProgram draft or active state" : ("Validation unexpectedly failed: " + (error ?? "null")))
		};
	}

	private static TestCaseResult TestBuildCurrentProgramGuideTextUsesCurrentNodeSummary(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_guide",
			Title = "主线导引",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_entry",
					Title = "前往神兵阁",
					Summary = "立刻前往竹山宗神兵阁，和井紫铃碰头。"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_entry",
				ActivatedNodeIds = new List<string> { "node_entry" }
			}
		};
		string guide = manager.BuildCurrentProgramGuideText(program);
		bool ok = !string.IsNullOrWhiteSpace(guide) && guide.Contains("竹山宗神兵阁") && guide.Contains("井紫铃");
		return new TestCaseResult
		{
			Name = "BuildCurrentProgramGuideTextUsesCurrentNodeSummary",
			Passed = ok,
			Details = (ok ? "Current program guide now surfaces the active node summary instead of falling back to empty quest-chain UI" : ("Unexpected program guide: " + (guide ?? "null")))
		};
	}

	private static TestCaseResult TestBuildCurrentProgramGuideTextOmitsPredicateHint(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_guide_no_predicate_echo",
			Title = "主线导引",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			AnchorNpcId = 20004,
			AnchorNpcName = "井紫铃",
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_entry",
					Title = "逸风城重逢",
					Summary = "在逸风城附近遇到井紫铃。",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "logic.all",
							Children = new List<QuestPredicateInvocation>
							{
								new QuestPredicateInvocation
								{
									Opcode = "scene.is",
									Params = new Dictionary<string, string> { ["sceneId"] = "147" }
								},
								new QuestPredicateInvocation
								{
									Opcode = "npc.present_in_current_scene",
									Params = new Dictionary<string, string> { ["npcId"] = "20004" }
								}
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_entry",
				ActivatedNodeIds = new List<string> { "node_entry" }
			}
		};
		string guide = manager.BuildCurrentProgramGuideText(program);
		bool ok = string.Equals(guide, "在逸风城附近遇到井紫铃。", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "BuildCurrentProgramGuideTextOmitsPredicateHint",
			Passed = ok,
			Details = (ok ? "Current program guide now avoids echoing predicate hints that are already surfaced in the status line" : ("Unexpected program guide: " + (guide ?? "null")))
		};
	}

	private static TestCaseResult TestBuildCurrentProgramStatusTextReportsUnsatisfiedActivation(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S1" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_status_activation",
			Title = "需要前往神兵阁",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_gate",
					Title = "去神兵阁见井紫铃",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_gate",
				ActivatedNodeIds = new List<string> { "node_gate" }
			}
		};
		string statusText = manager.BuildCurrentProgramStatusText(program);
		bool ok = !string.IsNullOrWhiteSpace(statusText) && statusText.Contains("待满足") && statusText.Contains("竹山宗神兵阁");
		return new TestCaseResult
		{
			Name = "BuildCurrentProgramStatusTextReportsUnsatisfiedActivation",
			Passed = ok,
			Details = (ok ? "Program status text now exposes unmet activation predicates instead of only a generic in-progress state" : ("Unexpected program status text: " + (statusText ?? "null")))
		};
	}

	private static TestCaseResult TestBuildCurrentProgramStatusTextUsesAnchorNpcName(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S1" }, new Dictionary<int, bool> { [20004] = true });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_status_anchor_name",
			Title = "逸风城会面",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			AnchorNpcId = 20004,
			AnchorNpcName = "井紫铃",
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_anchor_name",
					Title = "在逸风城见井紫铃",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "logic.all",
							Children = new List<QuestPredicateInvocation>
							{
								new QuestPredicateInvocation
								{
									Opcode = "scene.is",
									Params = new Dictionary<string, string> { ["sceneId"] = "147" }
								},
								new QuestPredicateInvocation
								{
									Opcode = "npc.present_in_current_scene",
									Params = new Dictionary<string, string> { ["npcId"] = "20004" }
								}
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_anchor_name",
				ActivatedNodeIds = new List<string> { "node_anchor_name" }
			}
		};
		string statusText = manager.BuildCurrentProgramStatusText(program);
		bool ok = !string.IsNullOrWhiteSpace(statusText) && statusText.Contains("井紫铃") && !statusText.Contains("20004");
		return new TestCaseResult
		{
			Name = "BuildCurrentProgramStatusTextUsesAnchorNpcName",
			Passed = ok,
			Details = (ok ? "Predicate hints now prefer immersive NPC names over raw NPC ids in mainline tracking" : ("Unexpected status text: " + (statusText ?? "null")))
		};
	}

	private static TestCaseResult TestBuildCurrentProgramStatusTextReportsPendingNarrative(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_status_pending_narrative",
			Title = "待播对白",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_story",
					Type = QuestNodeType.Narrative,
					Title = "井紫铃出场",
					Narrative = new QuestNarrativeBlock
					{
						NarrativeId = "narrative_1",
						Lines = new List<AIQuestStoryLine>
						{
							new AIQuestStoryLine
							{
								SpeakerType = QuestStorySpeakerType.Npc,
								Text = "师弟，你总算来了。"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_story",
				ActivatedNodeIds = new List<string> { "node_story" },
				PendingNarrativeNodeIds = new List<string> { "node_story" }
			}
		};
		string statusText = manager.BuildCurrentProgramStatusText(program);
		bool ok = !string.IsNullOrWhiteSpace(statusText) && statusText.Contains("待播放演出");
		return new TestCaseResult
		{
			Name = "BuildCurrentProgramStatusTextReportsPendingNarrative",
			Passed = ok,
			Details = (ok ? "Program status text now distinguishes queued narrative playback from generic node progress" : ("Unexpected pending narrative status text: " + (statusText ?? "null")))
		};
	}

	private static TestCaseResult TestBuildOfficialTaskUiEntriesIncludesActiveMainlineProgram(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S1" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_official_ui_active",
			Title = "井紫铃主线",
			Summary = "前往客房处理井紫铃体内失控的药力。",
			AnchorNpcId = 20004,
			AnchorNpcName = "井紫铃",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_official_ui_active",
					Title = "前往客房",
					Summary = "立刻前往客房见井紫铃。",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S1190",
								["sceneName"] = "客房"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_official_ui_active",
				ActivatedNodeIds = new List<string> { "node_official_ui_active" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		List<OfficialTaskUiEntry> entries = manager.BuildOfficialTaskUiEntriesForTest();
		OfficialTaskUiEntry entry = entries.FirstOrDefault((OfficialTaskUiEntry candidate) => candidate != null && string.Equals(candidate.EntryId, "mainline:program:program_official_ui_active", StringComparison.Ordinal));
		bool ok = entry != null && string.Equals(entry.Title, "井紫铃主线", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(entry.StatusText) && entry.StatusText.Contains("客房") && string.Equals(entry.GuideText, "立刻前往客房见井紫铃。", StringComparison.Ordinal) && string.Equals(entry.DescriptionText, "前往客房处理井紫铃体内失控的药力。", StringComparison.Ordinal);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "BuildOfficialTaskUiEntriesIncludesActiveMainlineProgram";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Active mainline programs now map into official-task-style UI entries with title, status, guide, and description." : ("Unexpected active official UI entries: " + ((entries == null) ? "null" : string.Join(" || ", entries.Select(DescribeOfficialTaskUiEntry)))));
		return testCaseResult;
	}

	private static TestCaseResult TestBuildOfficialTaskUiEntriesIncludesWaitingMainlineEntry(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineCampaign campaign = CreateMainlineCampaign("official_ui_waiting");
		MainlineChapter chapter = campaign.GetCurrentChapter();
		chapter.PendingBiographyEvent = new PendingBiographyEventState
		{
			EventId = "pending_waiting_1",
			NpcId = 20004,
			NpcName = "井紫铃",
			Status = PendingBiographyEventStatus.Captured,
			Summary = "井紫铃近日将有一场关键的服药生平事件。",
			PlayerFacingHint = "继续留意井紫铃的近日动向。"
		};
		SetField(manager, "_mainlineCampaign", campaign);
		List<OfficialTaskUiEntry> entries = manager.BuildOfficialTaskUiEntriesForTest();
		OfficialTaskUiEntry entry = entries.FirstOrDefault((OfficialTaskUiEntry candidate) => candidate != null && string.Equals(candidate.EntryId, "mainline:waiting:pending_waiting_1", StringComparison.Ordinal));
		bool ok = entry != null && entry.Title.Contains("第一章 山门异动") && entry.StatusText.Contains("等待拦截的官方生平事件改写为剧情") && string.Equals(entry.GuideText, "继续留意井紫铃的近日动向。", StringComparison.Ordinal) && string.Equals(entry.DescriptionText, "井紫铃近日将有一场关键的服药生平事件。", StringComparison.Ordinal);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "BuildOfficialTaskUiEntriesIncludesWaitingMainlineEntry";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Waiting mainline anchors now map into official-task-style UI entries without requiring a live QuestProgram." : ("Unexpected waiting official UI entries: " + ((entries == null) ? "null" : string.Join(" || ", entries.Select(DescribeOfficialTaskUiEntry)))));
		return testCaseResult;
	}

	private static TestCaseResult TestBuildOfficialTaskUiEntriesSanitizeLegacyInternalNpcFallbacks(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "F26,112" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_official_ui_legacy_npc_fallback",
			Title = "井紫铃灵脉强行突破",
			Summary = "散修祝语芙共需要决定是在青石灵脉·112号点位与NPC_20000会面。",
			AnchorNpcId = 20004,
			AnchorNpcName = "井紫铃",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_official_ui_legacy_npc_fallback",
					Title = "前往青石灵脉",
					Summary = "在青石灵脉·112号点位与NPC_20000会面。",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "F26,112" }
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_official_ui_legacy_npc_fallback",
				ActivatedNodeIds = new List<string> { "node_official_ui_legacy_npc_fallback" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		List<OfficialTaskUiEntry> entries = manager.BuildOfficialTaskUiEntriesForTest();
		OfficialTaskUiEntry entry = entries.FirstOrDefault((OfficialTaskUiEntry candidate) => candidate != null && string.Equals(candidate.EntryId, "mainline:program:program_official_ui_legacy_npc_fallback", StringComparison.Ordinal));
		bool ok = entry != null && entry.GuideText.IndexOf("NPC_", StringComparison.Ordinal) < 0 && entry.DescriptionText.IndexOf("NPC_", StringComparison.Ordinal) < 0 && entry.GuideText.IndexOf("目标人物", StringComparison.Ordinal) >= 0 && entry.DescriptionText.IndexOf("目标人物", StringComparison.Ordinal) >= 0;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "BuildOfficialTaskUiEntriesSanitizeLegacyInternalNpcFallbacks";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Official task UI now sanitizes legacy stored QuestProgram text so old NPC_ fallback names no longer leak into player-facing entries." : ((entry == null) ? "Failed to build the expected official task UI entry for the legacy fallback test." : ("Legacy official task UI entry still leaks internal NPC fallback text: title=" + (entry.Title ?? "null") + " | guide=" + (entry.GuideText ?? "null") + " | desc=" + (entry.DescriptionText ?? "null"))));
		return testCaseResult;
	}

	private static TestCaseResult TestOfficialTaskUiInjectionTargetsCurrentMainlineViewOnly()
	{
		bool ok = OfficialTaskUiBridge.ShouldInjectIntoCurrentOfficialTaskViewForTest(1, isOld: false, isChuanWen: false) && !OfficialTaskUiBridge.ShouldInjectIntoCurrentOfficialTaskViewForTest(0, isOld: false, isChuanWen: false) && !OfficialTaskUiBridge.ShouldInjectIntoCurrentOfficialTaskViewForTest(0, isOld: false, isChuanWen: true) && !OfficialTaskUiBridge.ShouldInjectIntoCurrentOfficialTaskViewForTest(1, isOld: true, isChuanWen: false) && !OfficialTaskUiBridge.ShouldInjectIntoCurrentOfficialTaskViewForTest(2, isOld: false, isChuanWen: false);
		return new TestCaseResult
		{
			Name = "OfficialTaskUiInjectionTargetsCurrentMainlineViewOnly",
			Passed = ok,
			Details = (ok ? "AI official-task injection now only targets the current unfinished mainline tab (curType=1), and skips rumors, commissions, plus old/completed pages." : "Official task UI injection scope still leaks outside the current unfinished mainline page.")
		};
	}

	private static TestCaseResult TestOfficialTaskUiDetailPayloadUsesStatusRemainingAndGuide()
	{
		OfficialTaskUiEntry entry = new OfficialTaskUiEntry
		{
			EntryId = "mainline:program:test_ui_payload",
			Title = "井紫铃主线",
			StatusText = "状态：进行中",
			GuideText = "立刻前往客房见井紫铃。",
			RemainingTimeText = "剩余时间:1年0月0日",
			DescriptionText = "前往客房，处理井紫铃体内失控的药力。"
		};
		AIOfficialTaskDetailPayload payload = OfficialTaskUiBridge.BuildDetailPayloadForTest(entry);
		bool ok = payload != null && string.Equals(payload.TaskName, entry.Title, StringComparison.Ordinal) && string.Equals(payload.StatusLabel, "剩余时间:", StringComparison.Ordinal) && string.Equals(payload.StatusValue, "1年0月0日", StringComparison.Ordinal) && payload.ProgressLines.Count == 1 && string.Equals(payload.ProgressLines[0], entry.GuideText, StringComparison.Ordinal) && string.Equals(payload.DescriptionText, entry.DescriptionText, StringComparison.Ordinal);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "OfficialTaskUiDetailPayloadUsesStatusRemainingAndGuide";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "AI official-task detail payload now maps title, status, remaining time, guide, and body into the same shape the official detail panel expects." : ((payload == null) ? "Official task UI detail payload was null." : ("Unexpected official task UI detail payload: taskName=" + (payload.TaskName ?? "null") + " | statusLabel=" + (payload.StatusLabel ?? "null") + " | statusValue=" + (payload.StatusValue ?? "null") + " | progressLines=" + ((payload.ProgressLines == null) ? "null" : string.Join(" / ", payload.ProgressLines)) + " | desc=" + (payload.DescriptionText ?? "null"))));
		return testCaseResult;
	}

	private static TestCaseResult TestOfficialTaskUiPatchSetIncludesMainlineInitAndClickHooks()
	{
		bool ok = typeof(OfficialTaskUiInjectAiMainlinePatch) != null && typeof(OfficialTaskUiAiCellClickPatch) != null;
		return new TestCaseResult
		{
			Name = "OfficialTaskUiPatchSetIncludesMainlineInitAndClickHooks",
			Passed = ok,
			Details = (ok ? "Official task UI now scopes AI entries through the authoritative task-list init hook and intercepts AI cell clicks without patching rumor/old/commission toggles." : "Official task UI is missing the required AI task-list init or click patch.")
		};
	}

	private static TestCaseResult TestBuildCurrentProgramRemainingTimeTextUsesBeatDeadline(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineCampaign campaign = CreateMainlineCampaign("remaining_time");
		MainlineChapter chapter = campaign.GetCurrentChapter();
		MainlineBeat beat = chapter.CurrentBeat;
		beat.TimeIntent = new BeatTimeIntent
		{
			TimeBand = "中期",
			Urgency = "中",
			MaxSpanMonths = 24
		};
		campaign.CreatedAt = new DateTime(2, 7, 25, 0, 0, 0);
		QuestProgram program = CreateQuestProgram("program_remaining_time", 20004, "井紫铃", QuestTrackType.Mainline, QuestProgramStatus.Active, new QuestNode
		{
			NodeId = "node_time",
			Type = QuestNodeType.Objective,
			Title = "等待时限显示",
			Summary = "测试主线剩余时间。"
		});
		program.Title = "主线时限";
		program.CampaignId = campaign.CampaignId;
		program.ChapterId = chapter.ChapterId;
		program.BeatId = beat.BeatId;
		program.Runtime.CurrentNodeId = "node_time";
		program.Runtime.AcceptedAtGameTime = new DateTime(2, 7, 25, 0, 0, 0);
		program.Runtime.DeadlineGameTime = new DateTime(4, 7, 25, 0, 0, 0);
		SetField(manager, "_mainlineCampaign", campaign);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(3, 7, 25, 0, 0, 0), new string[1] { "147" }, new Dictionary<int, bool>());
		string remaining = manager.BuildCurrentProgramRemainingTimeText(program);
		bool ok = string.Equals(remaining, "剩余时间:1年0月0日", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "BuildCurrentProgramRemainingTimeTextUsesBeatDeadline",
			Passed = ok,
			Details = (ok ? "Quest tracking can now show a concrete remaining-time line for active mainline beats" : ("Unexpected remaining time text: " + (remaining ?? "null")))
		};
	}

	private static TestCaseResult TestMalformedObjectiveNodeSuspendsInsteadOfAutoCompleting(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "147" }, new Dictionary<int, bool> { [20004] = true });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_runtime_invalid_objective",
			Title = "坏节点暴露",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			AnchorNpcId = 20004,
			AnchorNpcName = "井紫铃",
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004 },
				AllowedSceneIds = new List<string> { "147" },
				SelectedPredicates = new List<string> { "logic.all", "scene.is", "npc.present_in_current_scene" },
				SelectedCommands = new List<string>()
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_invalid",
					Type = QuestNodeType.Objective,
					Title = "逸风城重逢",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "logic.all",
							Children = new List<QuestPredicateInvocation>
							{
								new QuestPredicateInvocation
								{
									Opcode = "scene.is",
									Params = new Dictionary<string, string>
									{
										["sceneId"] = "147",
										["sceneName"] = "逸风城附近"
									}
								},
								new QuestPredicateInvocation
								{
									Opcode = "npc.present_in_current_scene",
									Params = new Dictionary<string, string> { ["npcId"] = "20004" }
								}
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_invalid"
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		bool changed = manager.PublishQuestProgramEventForTest("scene.changed", "147");
		bool ok = changed && program.Status == QuestProgramStatus.Suspended && !string.IsNullOrWhiteSpace(program.Runtime?.FailedReason) && program.Runtime.FailedReason.IndexOf("completionPredicates", StringComparison.Ordinal) >= 0;
		return new TestCaseResult
		{
			Name = "MalformedObjectiveNodeSuspendsInsteadOfAutoCompleting",
			Passed = ok,
			Details = (ok ? "Malformed objective nodes now suspend with an explicit reason instead of instantly completing and restoring staged NPCs" : string.Format("Unexpected invalid-objective runtime state: changed={0}, status={1}, failedReason={2}", changed, program.Status, program.Runtime?.FailedReason ?? "null"))
		};
	}

	private static TestCaseResult TestQuestProgramSceneEventAdvancesObjectiveNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_runtime_scene_001",
			Title = "进入神兵阁",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_start",
					Type = QuestNodeType.Objective,
					Title = "前往竹山宗神兵阁",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					NextNodeId = "node_follow"
				},
				new QuestNode
				{
					NodeId = "node_follow",
					Type = QuestNodeType.Objective,
					Title = "和井紫铃汇合"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_start",
				ActivatedNodeIds = new List<string> { "node_start" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_follow", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_start");
		return new TestCaseResult
		{
			Name = "QuestProgramSceneEventAdvancesObjectiveNode",
			Passed = ok,
			Details = (ok ? "scene.changed events advance active objective nodes when both activation and completion predicates match the current location" : ("Unexpected quest program runtime after scene event: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramTickCompletesTerminalNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_runtime_terminal_001",
			Title = "终章测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_terminal",
					Type = QuestNodeType.Terminal,
					Title = "尘埃落定",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "time.compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["value"] = "0002-07-01 00:00:00"
							}
						},
						new QuestPredicateInvocation
						{
							Opcode = "npc.alive",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_terminal",
				ActivatedNodeIds = new List<string> { "node_terminal" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("time.tick");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Completed && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_terminal");
		return new TestCaseResult
		{
			Name = "QuestProgramTickCompletesTerminalNode",
			Passed = ok,
			Details = (ok ? "time.tick events complete active terminal nodes once time.compare and npc.alive predicates are satisfied" : ("Unexpected quest program runtime after time tick: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramActivationCommandMutatesNpcState(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S001", 12, 3);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_command_action_001",
			Title = "动作命令测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004 }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_action",
					Type = QuestNodeType.Objective,
					Title = "切换动作",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "time.compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["value"] = "0002-08-01 00:00:00"
							}
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "npc.set_action",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["actionId"] = "46",
								["lockAction"] = "true"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_action",
				ActivatedNodeIds = new List<string> { "node_action" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		string locationId;
		int actionId;
		int statusId;
		int lockActionId;
		bool gotState = manager.TryGetQuestProgramRuntimeTestNpcStateForTest(20004, out locationId, out actionId, out statusId, out lockActionId);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && gotState && string.Equals(locationId, "S001", StringComparison.Ordinal) && actionId == 46 && statusId == 3 && lockActionId == 46 && updated.Runtime?.ExecutedCommandKeys != null && updated.Runtime.ExecutedCommandKeys.Contains("node_action:onActivated:0");
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "QuestProgramActivationCommandMutatesNpcState";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "OnActivated command blocks now mutate NPC runtime state through the quest-program command executor instead of only marking permission" : string.Format("Unexpected NPC runtime state after activation: gotState={0}, location={1}, action={2}, status={3}, lock={4}, program={5}", gotState, locationId ?? "null", actionId, statusId, lockActionId, DescribeQuestProgram(updated)));
		return testCaseResult;
	}

	private static TestCaseResult TestQuestProgramCompletionRestoresForcedNpcState(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S001", 12, 3);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_command_restore_001",
			Title = "恢复命令测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004 },
				AllowedSceneIds = new List<string> { "S116" }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_terminal",
					Type = QuestNodeType.Terminal,
					Title = "尘埃落定",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "npc.relocate",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["sceneId"] = "S116"
							}
						},
						new QuestCommandInvocation
						{
							Opcode = "npc.set_action",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["actionId"] = "46"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_terminal",
				ActivatedNodeIds = new List<string> { "node_terminal" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		string locationId;
		int actionId;
		int statusId;
		int lockActionId;
		bool gotState = manager.TryGetQuestProgramRuntimeTestNpcStateForTest(20004, out locationId, out actionId, out statusId, out lockActionId);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Completed && gotState && string.Equals(locationId, "S001", StringComparison.Ordinal) && actionId == 12 && statusId == 3 && lockActionId == 0 && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_terminal");
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "QuestProgramCompletionRestoresForcedNpcState";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Temporary command-driven NPC relocations and action overrides are restored when the quest program completes" : string.Format("Unexpected NPC runtime state after completion: gotState={0}, location={1}, action={2}, status={3}, lock={4}, program={5}", gotState, locationId ?? "null", actionId, statusId, lockActionId, DescribeQuestProgram(updated)));
		return testCaseResult;
	}

	private static TestCaseResult TestQuestProgramNarrativeNodePlaysAndAdvances(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_narrative_001",
			Title = "剧情节点测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_story",
					Type = QuestNodeType.Narrative,
					Title = "遇见井紫铃",
					Summary = "在神兵阁见她一面。",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					Narrative = new QuestNarrativeBlock
					{
						NarrativeId = "narrative_intro",
						Lines = new List<AIQuestStoryLine>
						{
							AIQuestStoryLine.CreateNpc("师弟，你终于来了。", 20004),
							AIQuestStoryLine.CreateNarrator("神兵阁内火气翻涌。")
						}
					},
					NextNodeId = "node_follow"
				},
				new QuestNode
				{
					NodeId = "node_follow",
					Type = QuestNodeType.Objective,
					Title = "继续下一节点",
					Summary = "剧情播完后应当切到这里。",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "time.compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["value"] = "0002-08-01 00:00:00"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_story",
				ActivatedNodeIds = new List<string> { "node_story" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_follow", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_story") && updated.Runtime.PendingNarrativeNodeIds != null && !updated.Runtime.PendingNarrativeNodeIds.Contains("node_story");
		return new TestCaseResult
		{
			Name = "QuestProgramNarrativeNodePlaysAndAdvances",
			Passed = ok,
			Details = (ok ? "Narrative nodes now consume their pending state, finish playback, and advance into the next node instead of hanging forever" : ("Unexpected quest program runtime after narrative event: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramChoiceNodeExecutesConsequenceAndJumps(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S116", 12, 3);
		manager.SetQuestProgramRuntimeTestChoiceSelectionForTest("program_choice_001", "node_choice", "choice_help");
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_choice_001",
			Title = "分支节点测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004 }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_choice",
					Type = QuestNodeType.Choice,
					Title = "如何回应",
					Summary = "选一个回答。",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					Narrative = new QuestNarrativeBlock
					{
						NarrativeId = "narrative_choice",
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("你准备怎么做？", 20004) },
						Choices = new List<QuestChoiceOption>
						{
							new QuestChoiceOption
							{
								ChoiceId = "choice_help",
								Text = "先安抚她。",
								Consequences = new List<QuestCommandInvocation>
								{
									new QuestCommandInvocation
									{
										Opcode = "npc.set_status",
										Params = new Dictionary<string, string>
										{
											["npcId"] = "20004",
											["statusId"] = "1"
										}
									}
								},
								NextNodeId = "node_follow"
							},
							new QuestChoiceOption
							{
								ChoiceId = "choice_leave",
								Text = "转身离开。",
								NextNodeId = "node_leave"
							}
						}
					}
				},
				new QuestNode
				{
					NodeId = "node_follow",
					Type = QuestNodeType.Objective,
					Title = "安抚后续",
					Summary = "执行了帮助分支后应该来到这里。",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "time.compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["value"] = "0002-08-01 00:00:00"
							}
						}
					}
				},
				new QuestNode
				{
					NodeId = "node_leave",
					Type = QuestNodeType.Objective,
					Title = "离开后续"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_choice",
				ActivatedNodeIds = new List<string> { "node_choice" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		string locationId;
		int actionId;
		int statusId;
		int lockActionId;
		bool gotState = manager.TryGetQuestProgramRuntimeTestNpcStateForTest(20004, out locationId, out actionId, out statusId, out lockActionId);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_follow", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_choice") && updated.Runtime?.ExecutedCommandKeys != null && updated.Runtime.ExecutedCommandKeys.Contains("node_choice:choice:choice_help:0") && gotState && string.Equals(locationId, "S116", StringComparison.Ordinal) && actionId == 12 && statusId == 1 && lockActionId == 0;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "QuestProgramChoiceNodeExecutesConsequenceAndJumps";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Choice nodes now resolve a selected branch, execute its consequence commands, and jump to the branch-specific next node" : string.Format("Unexpected choice-node runtime: gotState={0}, location={1}, action={2}, status={3}, lock={4}, program={5}", gotState, locationId ?? "null", actionId, statusId, lockActionId, DescribeQuestProgram(updated)));
		return testCaseResult;
	}

	private static TestCaseResult TestQuestProgramSceneLoadCommandUpdatesLocation(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S001" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_scene_load_001",
			Title = "切场命令测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract
			{
				AllowedSceneIds = new List<string> { "S116" }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_warp",
					Type = QuestNodeType.Objective,
					Title = "切到神兵阁",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "time.compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["value"] = "0002-07-01 00:00:00"
							}
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "scene.load",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "切场后续",
					Summary = "切场成功后应来到这里。"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_warp",
				ActivatedNodeIds = new List<string> { "node_warp" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("time.tick");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		string guide = ((updated != null) ? manager.BuildCurrentProgramGuideText(updated) : null);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_warp") && guide != null && guide.Contains("切场后续");
		return new TestCaseResult
		{
			Name = "QuestProgramSceneLoadCommandUpdatesLocation",
			Passed = ok,
			Details = (ok ? "scene.load now mutates the runtime location snapshot, so completion predicates can immediately see the new scene and continue the node chain" : ("Unexpected scene-load runtime: guide=" + (guide ?? "null") + ", program=" + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramSceneLoadWithoutContractSuspends(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S001" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_scene_load_denied_001",
			Title = "越权切场测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract
			{
				AllowedSceneIds = new List<string> { "S116" }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_warp",
					Type = QuestNodeType.Objective,
					Title = "越权切场",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "time.compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["value"] = "0002-07-01 00:00:00"
							}
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "scene.load",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_warp",
				ActivatedNodeIds = new List<string> { "node_warp" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("time.tick");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Suspended && !string.IsNullOrWhiteSpace(updated.Runtime?.FailedReason) && updated.Runtime.FailedReason.Contains("scene.load");
		return new TestCaseResult
		{
			Name = "QuestProgramSceneLoadWithoutContractSuspends",
			Passed = ok,
			Details = (ok ? "Dangerous scene.load commands are still blocked at runtime when the world contract does not grant permanent world mutation" : ("Unexpected denied scene-load runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramOfficialTaskStartCommandCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestOfficialTaskStateForTest(Array.Empty<int>(), new Dictionary<int, IEnumerable<int>>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_official_task_start_001",
			Title = "开启官方任务测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract(),
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_start_task",
					Type = QuestNodeType.Objective,
					Title = "开启官方任务",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "official_task.started",
							Params = new Dictionary<string, string> { ["taskId"] = "61" }
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "official_task.start",
							Params = new Dictionary<string, string> { ["taskId"] = "61" }
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "官方任务已开启"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_start_task",
				ActivatedNodeIds = new List<string> { "node_start_task" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool started;
		bool gotStarted = manager.TryGetQuestProgramRuntimeTestOfficialTaskStartedForTest(61, out started);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_start_task") && updated.Runtime?.ExecutedCommandKeys != null && updated.Runtime.ExecutedCommandKeys.Contains("node_start_task:onActivated:0") && gotStarted && started;
		return new TestCaseResult
		{
			Name = "QuestProgramOfficialTaskStartCommandCompletesNode",
			Passed = ok,
			Details = (ok ? "official_task.start now mutates the official-task runtime state before completion predicates run, so the same node can immediately observe official_task.started and advance" : $"Unexpected official-task start runtime: gotStarted={gotStarted}, started={started}, program={DescribeQuestProgram(updated)}")
		};
	}

	private static TestCaseResult TestQuestProgramOfficialTaskFinishStepCommandCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestOfficialTaskStateForTest(new int[1] { 61 }, new Dictionary<int, IEnumerable<int>> { [61] = Array.Empty<int>() });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_official_task_step_001",
			Title = "推进官方任务步骤测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract(),
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_finish_step",
					Type = QuestNodeType.Objective,
					Title = "推进官方任务步骤",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "official_task.step_finished",
							Params = new Dictionary<string, string>
							{
								["taskId"] = "61",
								["taskIndex"] = "2"
							}
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "official_task.finish_step",
							Params = new Dictionary<string, string>
							{
								["taskId"] = "61",
								["taskIndex"] = "2"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "官方任务步骤已完成"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_finish_step",
				ActivatedNodeIds = new List<string> { "node_finish_step" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool finished;
		bool gotFinished = manager.TryGetQuestProgramRuntimeTestOfficialTaskStepFinishedForTest(61, 2, out finished);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_finish_step") && updated.Runtime?.ExecutedCommandKeys != null && updated.Runtime.ExecutedCommandKeys.Contains("node_finish_step:onActivated:0") && gotFinished && finished;
		return new TestCaseResult
		{
			Name = "QuestProgramOfficialTaskFinishStepCommandCompletesNode",
			Passed = ok,
			Details = (ok ? "official_task.finish_step now writes the finished step before the node re-evaluates completion predicates, so official_task.step_finished can immediately advance the chain" : $"Unexpected official-task step runtime: gotFinished={gotFinished}, finished={finished}, program={DescribeQuestProgram(updated)}")
		};
	}

	private static TestCaseResult TestQuestProgramOfficialTaskFinishablePredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestOfficialTaskStateForTest(new int[1] { 88 }, new Dictionary<int, IEnumerable<int>> { [88] = new int[1] { 1 } }, new Dictionary<int, int> { [88] = 1 });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_official_task_finishable_001",
			Title = "官方任务可交测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_finishable",
					Type = QuestNodeType.Objective,
					Title = "等待官方任务可交",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "official_task.finishable",
							Params = new Dictionary<string, string> { ["taskId"] = "88" }
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "官方任务已可交"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_finishable",
				ActivatedNodeIds = new List<string> { "node_finishable" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_finishable");
		return new TestCaseResult
		{
			Name = "QuestProgramOfficialTaskFinishablePredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "official_task.finishable now reads the aggregate finished-step count, so a node can advance once an official task has reached its fully finishable state" : ("Unexpected official-task finishable runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramCombatStartCommandRecordsEncounter(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_combat_start_001",
			Title = "标准战斗启动测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20085 }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_combat",
					Type = QuestNodeType.Objective,
					Title = "发起标准战斗",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "combat.start_standard",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20085",
								["fightMusic"] = "战斗7"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_combat",
				ActivatedNodeIds = new List<string> { "node_combat" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		int npcId;
		string fightMusic;
		bool gotCombat = manager.TryGetQuestProgramRuntimeTestLastCombatForTest(out npcId, out fightMusic);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && gotCombat && npcId == 20085 && string.Equals(fightMusic, "战斗7", StringComparison.Ordinal) && updated.Runtime?.ExecutedCommandKeys != null && updated.Runtime.ExecutedCommandKeys.Contains("node_combat:onActivated:0");
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "QuestProgramCombatStartCommandRecordsEncounter";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "combat.start_standard now routes through the official fight-start bridge; in test mode the command records the intended encounter instead of leaving combat as an unhandled opcode" : string.Format("Unexpected combat-command runtime: gotCombat={0}, npcId={1}, fightMusic={2}, program={3}", gotCombat, npcId, fightMusic ?? "null", DescribeQuestProgram(updated)));
		return testCaseResult;
	}

	private static TestCaseResult TestQuestProgramKillNpcCommandCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20085] = true });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_kill_npc_001",
			Title = "NPC 死亡命令测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20085 }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_kill_target",
					Type = QuestNodeType.WorldState,
					Title = "让目标身亡",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.dead",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20085",
								["npcName"] = "岑续缘"
							}
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "npc.kill",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20085",
								["deathType"] = "1"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Terminal,
					Title = "目标已死亡"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_kill_target",
				ActivatedNodeIds = new List<string> { "node_kill_target" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_kill_target") && updated.Runtime?.ExecutedCommandKeys != null && updated.Runtime.ExecutedCommandKeys.Contains("node_kill_target:onActivated:0");
		return new TestCaseResult
		{
			Name = "QuestProgramKillNpcCommandCompletesNode",
			Passed = ok,
			Details = (ok ? "npc.kill now routes through the official死亡桥接, so a mainline world-state node can apply a dangerous permanent consequence and satisfy npc.dead in the same progression pass" : ("Unexpected npc-kill runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramNotificationCommandQueuesWithoutCyFriend(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>(), new Dictionary<int, bool> { [20004] = false });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_notification_001",
			Title = "任务通知测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004 }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_notify",
					Type = QuestNodeType.Objective,
					Title = "发送通知",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "mail.send_notification",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃",
								["message"] = "速来神兵阁一叙。",
								["historyMessage"] = "井紫铃通过任务传音催你速来神兵阁一叙。"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_notify",
				ActivatedNodeIds = new List<string> { "node_notify" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		int npcId;
		string message;
		string historyMessage;
		bool wasQueued;
		bool gotMail = manager.TryGetQuestProgramRuntimeTestLastNotificationForTest(out npcId, out message, out historyMessage, out wasQueued);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Completed && gotMail && npcId == 20004 && wasQueued && string.Equals(message, "速来神兵阁一叙。", StringComparison.Ordinal) && string.Equals(historyMessage, "井紫铃通过任务传音催你速来神兵阁一叙。", StringComparison.Ordinal) && updated.Runtime?.ExecutedCommandKeys != null && updated.Runtime.ExecutedCommandKeys.Contains("node_notify:onActivated:0");
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "QuestProgramNotificationCommandQueuesWithoutCyFriend";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "mail.send_notification now succeeds even without a cy-friend link by recording a queued notification payload instead of failing the node" : string.Format("Unexpected notification runtime: gotMail={0}, npcId={1}, queued={2}, message={3}, history={4}, program={5}", gotMail, npcId, wasQueued, message ?? "null", historyMessage ?? "null", DescribeQuestProgram(updated)));
		return testCaseResult;
	}

	private static TestCaseResult TestQuestProgramAddCyFriendCommandCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>(), new Dictionary<int, bool> { [20004] = false });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_add_friend_001",
			Title = "结交传音好友测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004 }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_friend",
					Type = QuestNodeType.Objective,
					Title = "结交传音好友",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.cy_friend",
							Params = new Dictionary<string, string> { ["npcId"] = "20004" }
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "npc.add_cy_friend",
							Params = new Dictionary<string, string> { ["npcId"] = "20004" }
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "好友已结成",
					Summary = "同一轮命令执行后就应进入这里。"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_friend",
				ActivatedNodeIds = new List<string> { "node_friend" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool isFriend;
		bool gotFriendState = manager.TryGetQuestProgramRuntimeTestCyFriendStateForTest(20004, out isFriend);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_friend") && gotFriendState && isFriend;
		return new TestCaseResult
		{
			Name = "QuestProgramAddCyFriendCommandCompletesNode",
			Passed = ok,
			Details = (ok ? "npc.add_cy_friend now updates runtime社交状态 immediately, so the same node can complete on its npc.cy_friend predicate without waiting for a later event" : $"Unexpected cy-friend runtime: gotFriendState={gotFriendState}, isFriend={isFriend}, program={DescribeQuestProgram(updated)}")
		};
	}

	private static TestCaseResult TestQuestProgramRewardCommandsMutatePlayerState(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(10, new Dictionary<int, int> { [5112] = 1 }, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_player_reward_001",
			Title = "玩家奖励命令测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract(),
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_reward",
					Type = QuestNodeType.Objective,
					Title = "发放奖励",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "time.compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["value"] = "0002-08-01 00:00:00"
							}
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "player.add_money",
							Params = new Dictionary<string, string> { ["amount"] = "25" }
						},
						new QuestCommandInvocation
						{
							Opcode = "player.add_item",
							Params = new Dictionary<string, string>
							{
								["itemId"] = "5112",
								["count"] = "2"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_reward",
				ActivatedNodeIds = new List<string> { "node_reward" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		int money;
		bool gotMoney = manager.TryGetQuestProgramRuntimeTestPlayerMoneyForTest(out money);
		int itemCount;
		bool gotItem = manager.TryGetQuestProgramRuntimeTestPlayerItemCountForTest(5112, out itemCount);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && gotMoney && money == 35 && gotItem && itemCount == 3 && updated.Runtime?.ExecutedCommandKeys != null && updated.Runtime.ExecutedCommandKeys.Contains("node_reward:onActivated:0") && updated.Runtime.ExecutedCommandKeys.Contains("node_reward:onActivated:1");
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "QuestProgramRewardCommandsMutatePlayerState";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "player.add_money and player.add_item now mutate the runtime player state through the command executor, so reward-like hooks no longer need to fake completion through old reward code" : $"Unexpected player reward runtime: gotMoney={gotMoney}, money={money}, gotItem={gotItem}, itemCount={itemCount}, program={DescribeQuestProgram(updated)}");
		return testCaseResult;
	}

	private static TestCaseResult TestQuestProgramSkillLearnCommandsMutatePlayerSkillState(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerSkillStateForTest(null, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_skill_rewards_001",
			Title = "学习技能命令测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract(),
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_learn_skills",
					Type = QuestNodeType.WorldState,
					Title = "习得技能与功法",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "player.learn_skill",
							Params = new Dictionary<string, string> { ["skillId"] = "10021" }
						},
						new QuestCommandInvocation
						{
							Opcode = "player.learn_static_skill",
							Params = new Dictionary<string, string>
							{
								["skillId"] = "804",
								["level"] = "1"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_learn_skills",
				ActivatedNodeIds = new List<string> { "node_learn_skills" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool hasSkill;
		bool gotSkill = manager.TryGetQuestProgramRuntimeTestPlayerSkillForTest(10021, out hasSkill);
		bool hasStaticSkill;
		bool gotStaticSkill = manager.TryGetQuestProgramRuntimeTestPlayerStaticSkillForTest(804, out hasStaticSkill);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && gotSkill && hasSkill && gotStaticSkill && hasStaticSkill && updated.Runtime?.ExecutedCommandKeys != null && updated.Runtime.ExecutedCommandKeys.Contains("node_learn_skills:onActivated:0") && updated.Runtime.ExecutedCommandKeys.Contains("node_learn_skills:onActivated:1");
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "QuestProgramSkillLearnCommandsMutatePlayerSkillState";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "player.learn_skill and player.learn_static_skill now mutate the runtime player skill state through the command executor, so programs can grant actual learned abilities instead of faking them in flavor text" : $"Unexpected player-skill runtime: gotSkill={gotSkill}, hasSkill={hasSkill}, gotStaticSkill={gotStaticSkill}, hasStaticSkill={hasStaticSkill}, program={DescribeQuestProgram(updated)}");
		return testCaseResult;
	}

	private static TestCaseResult TestQuestProgramPlayerHasItemPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(10, new Dictionary<int, int> { [5112] = 2 }, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_player_has_item_001",
			Title = "背包物品谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_need_item",
					Type = QuestNodeType.Objective,
					Title = "持有明心丹",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.has_item",
							Params = new Dictionary<string, string>
							{
								["itemId"] = "5112",
								["count"] = "2",
								["itemName"] = "明心丹"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "物品条件已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_need_item",
				ActivatedNodeIds = new List<string> { "node_need_item" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_need_item");
		return new TestCaseResult
		{
			Name = "QuestProgramPlayerHasItemPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "player.has_item now reads runtime inventory counts, so an objective node can advance as soon as the player already carries the required item stack" : ("Unexpected player-has-item runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramPlayerHasSkillPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerSkillStateForTest(new int[1] { 10021 }, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_player_has_skill_001",
			Title = "玩家神通谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_need_skill",
					Type = QuestNodeType.Objective,
					Title = "掌握所需神通",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.has_skill",
							Params = new Dictionary<string, string>
							{
								["skillId"] = "10021",
								["skillName"] = "烈阳剑气"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "神通条件已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_need_skill",
				ActivatedNodeIds = new List<string> { "node_need_skill" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_need_skill");
		return new TestCaseResult
		{
			Name = "QuestProgramPlayerHasSkillPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "player.has_skill now reads the live learned-skill state, so programs can require specific神通 before advancing" : ("Unexpected player-has-skill runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramPlayerHasStaticSkillPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerSkillStateForTest(null, new int[1] { 804 });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_player_has_static_skill_001",
			Title = "玩家功法谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_need_static_skill",
					Type = QuestNodeType.Objective,
					Title = "掌握所需功法",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.has_static_skill",
							Params = new Dictionary<string, string>
							{
								["skillId"] = "804",
								["skillName"] = "清心药经"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "功法条件已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_need_static_skill",
				ActivatedNodeIds = new List<string> { "node_need_static_skill" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_need_static_skill");
		return new TestCaseResult
		{
			Name = "QuestProgramPlayerHasStaticSkillPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "player.has_static_skill now reads the live learned-gongfa state, so programs can gate progression on specific功法 instead of only level or items" : ("Unexpected player-has-static-skill runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramPlayerMoneyComparePredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(125, null, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_player_money_compare_001",
			Title = "玩家金钱谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_need_money",
					Type = QuestNodeType.Objective,
					Title = "持有足够灵石",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S116" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.money_compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["amount"] = "100"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "金钱条件已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_need_money",
				ActivatedNodeIds = new List<string> { "node_need_money" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_need_money");
		return new TestCaseResult
		{
			Name = "QuestProgramPlayerMoneyComparePredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "player.money_compare now reads runtime money directly, so a node can advance immediately once the player already has the required amount" : ("Unexpected player-money runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramTimeWindowPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 6, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_time_window_001",
			Title = "时间窗谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_time_window",
					Type = QuestNodeType.Objective,
					Title = "等待进入目标时间窗",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "time.window",
							Params = new Dictionary<string, string>
							{
								["start"] = "0002-07-25 00:00:00",
								["end"] = "0002-07-25 12:00:00"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "时间窗已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_time_window",
				ActivatedNodeIds = new List<string> { "node_wait_time_window" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("time.tick");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_time_window");
		return new TestCaseResult
		{
			Name = "QuestProgramTimeWindowPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "time.window now gates nodes with an inclusive start/end window, so a node can advance as soon as the current game time falls inside the declared interval" : ("Unexpected time-window runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramFuBenRemainingDaysPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "F1001,2" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestAdvancedStateForTest(null, 6);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_fuben_days_001",
			Title = "副本剩余天数谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_fuben_days",
					Type = QuestNodeType.Objective,
					Title = "等待副本剩余天数充足",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "fuben.remaining_days",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["days"] = "5"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "副本剩余天数已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_fuben_days",
				ActivatedNodeIds = new List<string> { "node_wait_fuben_days" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("time.tick");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_fuben_days");
		return new TestCaseResult
		{
			Name = "QuestProgramFuBenRemainingDaysPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "fuben.remaining_days now reads the active dungeon timer directly, so a node can require a minimum safe margin before letting the program continue" : ("Unexpected fuben-days runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramNpcActionPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S116", 46, 0);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_npc_action_001",
			Title = "NPC 行为态谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_npc_action",
					Type = QuestNodeType.WorldState,
					Title = "等待井紫铃进入指定行为",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.action_is",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃",
								["actionId"] = "46"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "NPC 行为态已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_npc_action",
				ActivatedNodeIds = new List<string> { "node_wait_npc_action" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_npc_action");
		return new TestCaseResult
		{
			Name = "QuestProgramNpcActionPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "npc.action_is now reads the live ActionId, so programs can wait for an NPC to enter a specific official behavior state before advancing" : ("Unexpected npc-action runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramPlayerLevelComparePredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestAdvancedStateForTest(4, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_player_level_001",
			Title = "玩家境界谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_player_level",
					Type = QuestNodeType.Objective,
					Title = "等待玩家修为达到门槛",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.level_compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["level"] = "3"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "玩家修为已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_player_level",
				ActivatedNodeIds = new List<string> { "node_wait_player_level" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_player_level");
		return new TestCaseResult
		{
			Name = "QuestProgramPlayerLevelComparePredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "player.level_compare now reads the live player realm/level value, so programs can gate progression on cultivation thresholds" : ("Unexpected player-level runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramPlayerFavorComparePredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(null, null, new Dictionary<int, int> { [20004] = 68 });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_player_favor_001",
			Title = "玩家好感比较谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_player_favor",
					Type = QuestNodeType.Objective,
					Title = "等待对井紫铃好感达到门槛",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.favor_compare",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃",
								["operator"] = ">=",
								["value"] = "60"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "好感已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_player_favor",
				ActivatedNodeIds = new List<string> { "node_wait_player_favor" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_player_favor");
		return new TestCaseResult
		{
			Name = "QuestProgramPlayerFavorComparePredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "player.favor_compare now reads the live favor value toward a target NPC, so programs can branch on relationship depth instead of only hardcoded story beats" : ("Unexpected player-favor runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramSceneTypePredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestAdvancedStateForTest(null, null, 2);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_scene_type_001",
			Title = "场景类型谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_scene_type",
					Type = QuestNodeType.Objective,
					Title = "等待进入海域场景",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.type_is",
							Params = new Dictionary<string, string>
							{
								["sceneType"] = "2",
								["sceneTypeName"] = "海域"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "场景类型已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_scene_type",
				ActivatedNodeIds = new List<string> { "node_wait_scene_type" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		string guide = ((updated != null) ? manager.BuildCurrentProgramGuideText(updated) : null);
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_scene_type") && !string.IsNullOrWhiteSpace(guide) && guide.Contains("海域");
		return new TestCaseResult
		{
			Name = "QuestProgramSceneTypePredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "scene.type_is now reuses the official scene-type classification, so programs can gate nodes on world geography instead of only raw scene ids" : ("Unexpected scene-type runtime: guide=" + (guide ?? "<null>") + ", program=" + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramNpcPatrolPresentPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "F1001,2" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestNpcPatrolPresenceForTest(new Dictionary<int, bool> { [20004] = true });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_npc_patrol_present_001",
			Title = "巡逻到场谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_patrol",
					Type = QuestNodeType.WorldState,
					Title = "等待井紫铃巡逻到场",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.patrol_present",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "巡逻到场已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_patrol",
				ActivatedNodeIds = new List<string> { "node_wait_patrol" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "F1001,2");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_patrol");
		return new TestCaseResult
		{
			Name = "QuestProgramNpcPatrolPresentPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "npc.patrol_present now reuses the official patrol list, so a program can wait for dungeon巡逻到场 instead of faking NPC presence with ad-hoc location checks" : ("Unexpected npc-patrol runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramOfficialTaskNotOutdatedPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestOfficialTaskStateForTest(new int[1] { 61 }, new Dictionary<int, IEnumerable<int>> { [61] = Array.Empty<int>() });
		manager.SetQuestProgramRuntimeTestOfficialTaskOutdatedStateForTest(new Dictionary<int, bool> { [61] = false });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_official_task_not_outdated_001",
			Title = "官方任务未过时谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_task_not_outdated",
					Type = QuestNodeType.Objective,
					Title = "等待官方任务仍未过时",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "task.official_not_outdated",
							Params = new Dictionary<string, string>
							{
								["taskId"] = "61",
								["taskName"] = "宗门密令"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "官方任务仍未过时"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_task_not_outdated",
				ActivatedNodeIds = new List<string> { "node_wait_task_not_outdated" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("time.tick");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_task_not_outdated");
		return new TestCaseResult
		{
			Name = "QuestProgramOfficialTaskNotOutdatedPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "task.official_not_outdated now reuses the official timeout rule, so a program can gate progression on whether a bound official task is still valid" : ("Unexpected official-task-not-outdated runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramNpcPresentPredicateActivatesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S116", 12, 3);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_npc_present_001",
			Title = "NPC 到场谓词测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_npc",
					Type = QuestNodeType.Objective,
					Title = "等待井紫铃在场",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.present_in_current_scene",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃"
							}
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "time.compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["value"] = "0002-07-01 00:00:00"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "井紫铃已在场"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_npc"
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.ActivatedNodeIds != null && updated.Runtime.ActivatedNodeIds.Contains("node_wait_npc") && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_npc");
		return new TestCaseResult
		{
			Name = "QuestProgramNpcPresentPredicateActivatesNode",
			Passed = ok,
			Details = (ok ? "npc.present_in_current_scene now treats a runtime NPC who already shares the player's current scene as an immediate activation gate, so the node can activate and finish in the same scene event" : ("Unexpected npc-present runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramLogicAllPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(120, null, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_logic_all_001",
			Title = "组合谓词 all 测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_all",
					Type = QuestNodeType.Objective,
					Title = "同时满足地点和灵石",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "logic.all",
							Children = new List<QuestPredicateInvocation>
							{
								new QuestPredicateInvocation
								{
									Opcode = "scene.is",
									Params = new Dictionary<string, string>
									{
										["sceneId"] = "S116",
										["sceneName"] = "竹山宗神兵阁"
									}
								},
								new QuestPredicateInvocation
								{
									Opcode = "player.money_compare",
									Params = new Dictionary<string, string>
									{
										["operator"] = ">=",
										["amount"] = "100"
									}
								}
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "已同时满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_all",
				ActivatedNodeIds = new List<string> { "node_wait_all" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_all");
		return new TestCaseResult
		{
			Name = "QuestProgramLogicAllPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "logic.all now requires every child predicate to pass before the node can complete" : ("Unexpected logic-all runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramLogicAnyPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_logic_any_001",
			Title = "组合谓词 any 测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_any",
					Type = QuestNodeType.Objective,
					Title = "满足任一地点",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "logic.any",
							Children = new List<QuestPredicateInvocation>
							{
								new QuestPredicateInvocation
								{
									Opcode = "scene.is",
									Params = new Dictionary<string, string>
									{
										["sceneId"] = "S404",
										["sceneName"] = "不存在的地点"
									}
								},
								new QuestPredicateInvocation
								{
									Opcode = "scene.is",
									Params = new Dictionary<string, string>
									{
										["sceneId"] = "S116",
										["sceneName"] = "竹山宗神兵阁"
									}
								}
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "已满足任一地点"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_any",
				ActivatedNodeIds = new List<string> { "node_wait_any" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_any");
		return new TestCaseResult
		{
			Name = "QuestProgramLogicAnyPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "logic.any now allows the node to complete as soon as one child predicate passes" : ("Unexpected logic-any runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramLogicNotPredicateCompletesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestCyFriendStateForTest(20004, isFriend: false);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_logic_not_001",
			Title = "组合谓词 not 测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_not",
					Type = QuestNodeType.Objective,
					Title = "要求尚未结为传音好友",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "logic.not",
							Children = new List<QuestPredicateInvocation>
							{
								new QuestPredicateInvocation
								{
									Opcode = "npc.cy_friend",
									Params = new Dictionary<string, string>
									{
										["npcId"] = "20004",
										["npcName"] = "井紫铃"
									}
								}
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "尚未结为传音好友"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_not",
				ActivatedNodeIds = new List<string> { "node_wait_not" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_not");
		return new TestCaseResult
		{
			Name = "QuestProgramLogicNotPredicateCompletesNode",
			Passed = ok,
			Details = (ok ? "logic.not now inverts its child predicate, so nodes can express structured negative conditions without ad-hoc special cases" : ("Unexpected logic-not runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramPlayerLevelObservedStateAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestAdvancedStateForTest(2, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_player_level_observed_001",
			Title = "玩家境界观测唤醒测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_level",
					Type = QuestNodeType.Objective,
					Title = "等待修为突破",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.level_compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["level"] = "3"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "修为已突破"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_level",
				ActivatedNodeIds = new List<string> { "node_wait_level" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PumpQuestProgramObservedStateForTest();
		manager.SetQuestProgramRuntimeTestAdvancedStateForTest(3, null);
		manager.PumpQuestProgramObservedStateForTest();
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_level");
		return new TestCaseResult
		{
			Name = "QuestProgramPlayerLevelObservedStateAdvancesNode",
			Passed = ok,
			Details = (ok ? "Observed-state pumping now watches player.level_compare, so a cultivation breakthrough can wake the current node without waiting for an unrelated scene or time event" : ("Unexpected player-level-observed runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramNpcActionObservedStateAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S116", 1, 0);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_npc_action_observed_001",
			Title = "NPC 行为观测唤醒测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_action",
					Type = QuestNodeType.WorldState,
					Title = "等待井紫铃切换行为",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.action_is",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃",
								["actionId"] = "46"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "NPC 已切换行为"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_action",
				ActivatedNodeIds = new List<string> { "node_wait_action" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PumpQuestProgramObservedStateForTest();
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S116", 46, 0);
		manager.PumpQuestProgramObservedStateForTest();
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_action");
		return new TestCaseResult
		{
			Name = "QuestProgramNpcActionObservedStateAdvancesNode",
			Passed = ok,
			Details = (ok ? "Observed-state pumping now watches npc.action_is, so an NPC switching into the required official action can wake the node immediately" : ("Unexpected npc-action-observed runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramMoneyChangeEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(20, null, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_money_event_001",
			Title = "灵石变化事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_money",
					Type = QuestNodeType.Objective,
					Title = "等待灵石足够",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.money_compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["amount"] = "100"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "灵石已足够"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_money",
				ActivatedNodeIds = new List<string> { "node_wait_money" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(120, null, null);
		manager.NotifyQuestProgramPlayerMoneyChangedForTest(20L, 120L);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_money");
		return new TestCaseResult
		{
			Name = "QuestProgramMoneyChangeEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "A native player.money_changed runtime event now advances the waiting node immediately, without depending on the observed-state pump" : ("Unexpected money-change runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramItemChangeEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(null, new Dictionary<int, int> { [5112] = 0 }, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_item_event_001",
			Title = "物品变化事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_item",
					Type = QuestNodeType.Objective,
					Title = "等待明心丹",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.has_item",
							Params = new Dictionary<string, string>
							{
								["itemId"] = "5112",
								["count"] = "1",
								["itemName"] = "明心丹"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "已获得明心丹"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_item",
				ActivatedNodeIds = new List<string> { "node_wait_item" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(null, new Dictionary<int, int> { [5112] = 1 }, null);
		manager.NotifyQuestProgramPlayerItemChangedForTest(5112, 0, 1);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_item");
		return new TestCaseResult
		{
			Name = "QuestProgramItemChangeEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "A native player.item_changed runtime event now advances the waiting node immediately, without depending on the observed-state pump" : ("Unexpected item-change runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramNpcAliveChangeEventCompletesTerminal(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = false });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_npc_alive_event_001",
			Title = "NPC 生死变化事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_alive",
					Type = QuestNodeType.Terminal,
					Title = "等待井紫铃存活",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.alive",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_alive"
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PumpQuestProgramObservedStateForTest();
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		manager.PumpQuestProgramObservedStateForTest();
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Completed && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_alive");
		return new TestCaseResult
		{
			Name = "QuestProgramNpcAliveChangeEventCompletesTerminal",
			Passed = ok,
			Details = (ok ? "Observed NPC alive-state deltas now publish a runtime event, so npc.alive gates can immediately complete a terminal node when the target becomes available" : ("Unexpected npc-alive-change runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramOfficialTaskStartEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestOfficialTaskStateForTest(Array.Empty<int>(), new Dictionary<int, IEnumerable<int>>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_official_task_start_event_001",
			Title = "官方任务开始事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_task_started",
					Type = QuestNodeType.Objective,
					Title = "等待官方任务开启",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "official_task.started",
							Params = new Dictionary<string, string> { ["taskId"] = "61" }
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "官方任务已开启"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_task_started",
				ActivatedNodeIds = new List<string> { "node_wait_task_started" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.SetQuestProgramRuntimeTestOfficialTaskStateForTest(new int[1] { 61 }, new Dictionary<int, IEnumerable<int>>());
		manager.NotifyOfficialTaskProgressChangedForTest(61);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_task_started");
		return new TestCaseResult
		{
			Name = "QuestProgramOfficialTaskStartEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "External official-task start events now wake active programs immediately, so nodes waiting on official_task.started do not need a scene or time tick to advance" : ("Unexpected official-task-start-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramOfficialTaskStepFinishedEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestOfficialTaskStateForTest(new int[1] { 61 }, new Dictionary<int, IEnumerable<int>> { [61] = Array.Empty<int>() });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_official_task_step_event_001",
			Title = "官方任务步骤事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_task_step",
					Type = QuestNodeType.Objective,
					Title = "等待官方任务步骤完成",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "official_task.step_finished",
							Params = new Dictionary<string, string>
							{
								["taskId"] = "61",
								["taskIndex"] = "2"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "官方任务步骤已完成"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_task_step",
				ActivatedNodeIds = new List<string> { "node_wait_task_step" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.SetQuestProgramRuntimeTestOfficialTaskStateForTest(new int[1] { 61 }, new Dictionary<int, IEnumerable<int>> { [61] = new int[1] { 2 } });
		manager.NotifyOfficialTaskProgressChangedForTest(61, 2);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_task_step");
		return new TestCaseResult
		{
			Name = "QuestProgramOfficialTaskStepFinishedEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "External official-task step-finished events now wake active programs immediately, so official_task.step_finished can advance without waiting for another generic runtime tick" : ("Unexpected official-task-step-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramOfficialTaskFinishableEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestOfficialTaskStateForTest(new int[1] { 88 }, new Dictionary<int, IEnumerable<int>> { [88] = Array.Empty<int>() }, new Dictionary<int, int> { [88] = 1 });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_official_task_finishable_event_001",
			Title = "官方任务可交事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_task_finishable",
					Type = QuestNodeType.Objective,
					Title = "等待官方任务可交",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "official_task.finishable",
							Params = new Dictionary<string, string> { ["taskId"] = "88" }
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "官方任务已可交"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_task_finishable",
				ActivatedNodeIds = new List<string> { "node_wait_task_finishable" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.SetQuestProgramRuntimeTestOfficialTaskStateForTest(new int[1] { 88 }, new Dictionary<int, IEnumerable<int>> { [88] = new int[1] { 1 } }, new Dictionary<int, int> { [88] = 1 });
		manager.NotifyOfficialTaskProgressChangedForTest(88, 1);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_task_finishable");
		return new TestCaseResult
		{
			Name = "QuestProgramOfficialTaskFinishableEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "External official-task progress events now re-evaluate finishability immediately, so official_task.finishable wakes as soon as the last required step completes" : ("Unexpected official-task-finishable-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramBattleVictoryEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_battle_victory_event_001",
			Title = "战斗胜利事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_battle_victory",
					Type = QuestNodeType.Combat,
					Title = "等待击败岑续缘",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "combat.victory_over_npc",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20085",
								["npcName"] = "岑续缘"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "战斗已结束"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_battle_victory",
				ActivatedNodeIds = new List<string> { "node_wait_battle_victory" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.NotifyBattleVictory(20085);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_battle_victory");
		return new TestCaseResult
		{
			Name = "QuestProgramBattleVictoryEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "Battle victory now publishes a quest-program runtime event immediately, so combat.victory_over_npc no longer needs a scene or time tick to wake up" : ("Unexpected battle-victory-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramNpcStatusChangeEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S116", 46, 1);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_npc_status_event_001",
			Title = "NPC 状态变化事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_npc_status",
					Type = QuestNodeType.WorldState,
					Title = "等待井紫铃进入目标状态",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.status_is",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃",
								["statusId"] = "3"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "NPC 状态已切换"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_npc_status",
				ActivatedNodeIds = new List<string> { "node_wait_npc_status" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S116", 46, 3);
		manager.NotifyNpcStatusChangedForTest(20004, 3);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_npc_status");
		return new TestCaseResult
		{
			Name = "QuestProgramNpcStatusChangeEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "NPC status changes now wake active programs immediately, so npc.status_is can advance as soon as the target status lands" : ("Unexpected npc-status-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramNpcActionChangeEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S116", 1, 0);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_npc_action_event_001",
			Title = "NPC 行为变化事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_npc_action",
					Type = QuestNodeType.WorldState,
					Title = "等待井紫铃进入目标行为",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.action_is",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃",
								["actionId"] = "46"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "NPC 行为已切换"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_npc_action",
				ActivatedNodeIds = new List<string> { "node_wait_npc_action" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S116", 46, 0);
		manager.NotifyNpcActionChangedForTest(20004, 1, 46);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_npc_action");
		return new TestCaseResult
		{
			Name = "QuestProgramNpcActionChangeEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "NPC action changes now publish a quest-program runtime event immediately, so npc.action_is wakes as soon as the official ActionId lands" : ("Unexpected npc-action-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramNpcFavorChangeEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(null, null, new Dictionary<int, int> { [20004] = 20 });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_npc_favor_event_001",
			Title = "NPC 好感变化事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_npc_favor",
					Type = QuestNodeType.Objective,
					Title = "等待与井紫铃好感达标",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.favor_compare",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃",
								["operator"] = ">=",
								["value"] = "40"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "NPC 好感已达标"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_npc_favor",
				ActivatedNodeIds = new List<string> { "node_wait_npc_favor" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(null, null, new Dictionary<int, int> { [20004] = 45 });
		manager.NotifyNpcFavorChangedForTest(20004, 20, 45);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_npc_favor");
		return new TestCaseResult
		{
			Name = "QuestProgramNpcFavorChangeEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "NPC favor changes now publish a quest-program runtime event immediately, so player.favor_compare can wake without waiting for observed-state polling" : ("Unexpected npc-favor-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramNpcDeathEventCompletesTerminal(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_npc_death_event_001",
			Title = "NPC 死亡事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_dead",
					Type = QuestNodeType.Terminal,
					Title = "等待井紫铃死亡",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.dead",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_dead"
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = false });
		manager.NotifyNpcDeathForTest(20004);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && updated.Status == QuestProgramStatus.Completed && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_dead");
		return new TestCaseResult
		{
			Name = "QuestProgramNpcDeathEventCompletesTerminal",
			Passed = ok,
			Details = (ok ? "Official NPC death notifications now wake quest programs immediately, so npc.dead nodes no longer need observed-state polling to complete" : ("Unexpected npc-death-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramPlayerSkillLearnedEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerSkillStateForTest(null, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_skill_event_001",
			Title = "玩家神通习得事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_skill",
					Type = QuestNodeType.Objective,
					Title = "等待玩家习得神通",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.has_skill",
							Params = new Dictionary<string, string> { ["skillId"] = "10021" }
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "神通已习得"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_skill",
				ActivatedNodeIds = new List<string> { "node_wait_skill" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.NotifyQuestProgramPlayerSkillLearnedForTest(10021);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_skill");
		return new TestCaseResult
		{
			Name = "QuestProgramPlayerSkillLearnedEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "Learning a skill now publishes a native quest-program runtime event, so player.has_skill nodes wake immediately instead of waiting for unrelated polling" : ("Unexpected player-skill-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramPlayerStaticSkillLearnedEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestPlayerSkillStateForTest(null, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_static_skill_event_001",
			Title = "玩家功法习得事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_static_skill",
					Type = QuestNodeType.Objective,
					Title = "等待玩家习得功法",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.has_static_skill",
							Params = new Dictionary<string, string> { ["skillId"] = "804" }
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "功法已习得"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_static_skill",
				ActivatedNodeIds = new List<string> { "node_wait_static_skill" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.NotifyQuestProgramPlayerStaticSkillLearnedForTest(804);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_static_skill");
		return new TestCaseResult
		{
			Name = "QuestProgramPlayerStaticSkillLearnedEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "Learning a static skill now publishes a native quest-program runtime event, so player.has_static_skill nodes wake immediately instead of waiting for unrelated polling" : ("Unexpected player-static-skill-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramPlayerLevelChangedEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestAdvancedStateForTest(2, null);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_player_level_event_001",
			Title = "玩家修为提升事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_level",
					Type = QuestNodeType.Objective,
					Title = "等待玩家修为提升",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "player.level_compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["level"] = "3"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "修为已提升"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_level",
				ActivatedNodeIds = new List<string> { "node_wait_level" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.NotifyQuestProgramPlayerLevelChangedForTest(2, 3);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_level");
		return new TestCaseResult
		{
			Name = "QuestProgramPlayerLevelChangedEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "Level-up now publishes a native quest-program runtime event, so player.level_compare nodes wake immediately instead of waiting for observed-state polling" : ("Unexpected player-level-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramFuBenRemainingDaysChangedEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "F101,1" }, new Dictionary<int, bool>());
		manager.SetQuestProgramRuntimeTestAdvancedStateForTest(null, 4);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_fuben_days_event_001",
			Title = "副本剩余天数变化事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_fuben_days",
					Type = QuestNodeType.Objective,
					Title = "等待副本剩余天数满足条件",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "fuben.remaining_days",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["days"] = "5"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "副本剩余天数已满足"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_fuben_days",
				ActivatedNodeIds = new List<string> { "node_wait_fuben_days" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.NotifyQuestProgramFuBenRemainingDaysChangedForTest(4, 5);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_fuben_days");
		return new TestCaseResult
		{
			Name = "QuestProgramFuBenRemainingDaysChangedEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "Dungeon-day changes now publish a native quest-program runtime event, so fuben.remaining_days nodes wake immediately instead of waiting for observed-state polling" : ("Unexpected fuben-days-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramDeliveryMailSubmissionEventAdvancesNode(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_delivery_mail_event_001",
			Title = "传音交付事件测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_delivery_mail",
					Type = QuestNodeType.Objective,
					Title = "等待通过传音交付明心丹",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "mail.delivery_submitted",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃",
								["itemId"] = "5112",
								["itemName"] = "明心丹"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "传音交付已确认"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_delivery_mail",
				ActivatedNodeIds = new List<string> { "node_wait_delivery_mail" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.NotifyQuestProgramDeliveryMailSubmittedForTest(20004, 5112, 900201);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_after", StringComparison.Ordinal) && updated.Runtime?.CompletedNodeIds != null && updated.Runtime.CompletedNodeIds.Contains("node_wait_delivery_mail");
		return new TestCaseResult
		{
			Name = "QuestProgramDeliveryMailSubmissionEventAdvancesNode",
			Passed = ok,
			Details = (ok ? "Delivery mail submissions now publish a quest-program runtime event immediately, so mail.delivery_submitted can advance without polling" : ("Unexpected delivery-mail-event runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramDeliveryMailSubmissionIgnoresWrongItem(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_delivery_mail_event_002",
			Title = "传音交付物品过滤测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_wait_delivery_mail",
					Type = QuestNodeType.Objective,
					Title = "等待通过传音交付明心丹",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "mail.delivery_submitted",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["npcName"] = "井紫铃",
								["itemId"] = "5112",
								["itemName"] = "明心丹"
							}
						}
					},
					NextNodeId = "node_after"
				},
				new QuestNode
				{
					NodeId = "node_after",
					Type = QuestNodeType.Objective,
					Title = "传音交付已确认"
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_wait_delivery_mail",
				ActivatedNodeIds = new List<string> { "node_wait_delivery_mail" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.NotifyQuestProgramDeliveryMailSubmittedForTest(20004, 600001, 900202);
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = updated != null && string.Equals(updated.Runtime?.CurrentNodeId, "node_wait_delivery_mail", StringComparison.Ordinal) && (updated.Runtime?.CompletedNodeIds == null || !updated.Runtime.CompletedNodeIds.Contains("node_wait_delivery_mail"));
		return new TestCaseResult
		{
			Name = "QuestProgramDeliveryMailSubmissionIgnoresWrongItem",
			Passed = ok,
			Details = (ok ? "mail.delivery_submitted now filters by requested item instead of waking on any submission to the same NPC" : ("Unexpected wrong-item delivery runtime: " + DescribeQuestProgram(updated)))
		};
	}

	private static TestCaseResult TestQuestProgramFavorCommandIsIdempotent(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestPlayerStateForTest(null, null, new Dictionary<int, int> { [20004] = 10 });
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_favor_001",
			Title = "好感命令幂等测试",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Active,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004 }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_favor",
					Type = QuestNodeType.Objective,
					Title = "增加好感",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "S116",
								["sceneName"] = "竹山宗神兵阁"
							}
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "time.compare",
							Params = new Dictionary<string, string>
							{
								["operator"] = ">=",
								["value"] = "0002-08-01 00:00:00"
							}
						}
					},
					OnActivated = new List<QuestCommandInvocation>
					{
						new QuestCommandInvocation
						{
							Opcode = "npc.add_favor",
							Params = new Dictionary<string, string>
							{
								["npcId"] = "20004",
								["amount"] = "15"
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState
			{
				CurrentNodeId = "node_favor",
				ActivatedNodeIds = new List<string> { "node_favor" }
			}
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		manager.PublishQuestProgramEventForTest("scene.changed", "S116");
		QuestProgram updated = GetQuestProgramState(manager).Programs.FirstOrDefault();
		int favorValue;
		bool gotFavor = manager.TryGetQuestProgramRuntimeTestNpcFavorForTest(20004, out favorValue);
		bool ok = updated != null && updated.Status == QuestProgramStatus.Active && gotFavor && favorValue == 25 && updated.Runtime?.ExecutedCommandKeys != null && updated.Runtime.ExecutedCommandKeys.Count((string key) => key == "node_favor:onActivated:0") == 1;
		return new TestCaseResult
		{
			Name = "QuestProgramFavorCommandIsIdempotent",
			Passed = ok,
			Details = (ok ? "npc.add_favor now executes as a real social mutation, but only once per command key even if the same activation event is replayed" : $"Unexpected favor runtime: gotFavor={gotFavor}, favorValue={favorValue}, program={DescribeQuestProgram(updated)}")
		};
	}

	private static TestCaseResult TestCyEmailValidatorRejectsOrphanedOldMail()
	{
		EmailData email = new EmailData(900071, isOld: true, -712345, "2026-03-14 18:33:37");
		JSONObject newChuanYingList = new JSONObject();
		string reason;
		bool ok = !QuestCyMailHelper.IsCyEmailRenderableForTest(email, newChuanYingList, out reason) && !string.IsNullOrWhiteSpace(reason) && reason.IndexOf("NewChuanYingList", StringComparison.OrdinalIgnoreCase) >= 0;
		return new TestCaseResult
		{
			Name = "CyEmailValidatorRejectsOrphanedOldMail",
			Passed = ok,
			Details = (ok ? "Cy email validator rejects old mails whose backing NewChuanYingList entry is missing" : ("Validator unexpectedly accepted orphaned old mail. reason=" + (reason ?? "<null>")))
		};
	}

	private static TestCaseResult TestCyEmailSanitizerDropsInvalidOldMail()
	{
		string npcKey = "900072";
		EmailData brokenEmail = new EmailData(900072, isOld: true, -712346, "2026-03-14 18:33:37");
		EmailData validEmail = new EmailData(900072, isOld: true, -712347, "2026-03-14 18:34:01");
		Dictionary<string, List<EmailData>> buckets = new Dictionary<string, List<EmailData>> { [npcKey] = new List<EmailData> { brokenEmail, validEmail } };
		JSONObject newChuanYingList = new JSONObject();
		JSONObject validMailData = new JSONObject();
		validMailData.SetField("info", "道友，劳烦你替我留心此物。");
		validMailData.SetField("AvatarName", "韩秋");
		validMailData.SetField("CanCaoZuo", val: false);
		newChuanYingList.SetField(validEmail.oldId.ToString(), validMailData);
		string report;
		int removed = QuestCyMailHelper.SanitizeCyEmailBucketForTest(buckets, npcKey, newChuanYingList, out report);
		List<EmailData> remaining;
		bool ok = removed == 1 && buckets.TryGetValue(npcKey, out remaining) && remaining.Count == 1 && remaining[0] == validEmail && !string.IsNullOrWhiteSpace(report) && report.IndexOf(brokenEmail.oldId.ToString(), StringComparison.Ordinal) >= 0;
		return new TestCaseResult
		{
			Name = "CyEmailSanitizerDropsInvalidOldMail",
			Passed = ok,
			Details = (ok ? "Cy email sanitizer pruned orphaned old mails before official UI rendering" : string.Format("Sanitizer mismatch: removed={0}, remaining={1}, report={2}", removed, DescribeEmailBucket(buckets, npcKey), report ?? "<null>"))
		};
	}

	private static TestCaseResult TestLegacyDataWipePreservesCharacterCards(string tempDir)
	{
		string root = Path.Combine(tempDir, "LegacyWipeDialogs");
		Directory.CreateDirectory(root);
		string dialogA = Path.Combine(root, "101_甲.txt");
		string dialogB = Path.Combine(root, "202_乙_带下划线.txt");
		string card = Path.Combine(root, "303_丙_character.txt");
		string stray = Path.Combine(root, "not_npc_history.txt");
		string promptDir = Path.Combine(root, "MCS_AIChatMod");
		string promptFile = Path.Combine(promptDir, "prompt.txt");
		Directory.CreateDirectory(promptDir);
		File.WriteAllText(dialogA, "history-a", Encoding.UTF8);
		File.WriteAllText(dialogB, "history-b", Encoding.UTF8);
		File.WriteAllText(card, "character", Encoding.UTF8);
		File.WriteAllText(stray, "stray", Encoding.UTF8);
		File.WriteAllText(promptFile, "prompt", Encoding.UTF8);
		LegacyDataWipeSummary summary = AIChatManager.ClearLegacyModDataPreserveCharacterCardsForTest(root, null, null);
		bool ok = !File.Exists(dialogA) && !File.Exists(dialogB) && File.Exists(card) && File.Exists(stray) && File.Exists(promptFile) && summary != null && summary.DeletedDialogHistoryFiles == 2 && summary.PreservedCharacterCardFiles == 1;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "LegacyDataWipePreservesCharacterCards";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Legacy wipe deleted NPC dialog histories while preserving character cards and prompt files" : $"Legacy wipe mismatch: summary={DescribeLegacyDataWipeSummary(summary)}, files=dialogA:{File.Exists(dialogA)}, dialogB:{File.Exists(dialogB)}, card:{File.Exists(card)}, stray:{File.Exists(stray)}, prompt:{File.Exists(promptFile)}");
		return testCaseResult;
	}

	private static TestCaseResult TestLegacyDataWipeClearsQuestRuntimeAndPersistence(AIQuestManager manager, string tempDir)
	{
		ResetWorkingState(manager);
		string persistentRoot = Path.Combine(tempDir, "LegacyWipeQuests");
		string scopeDir = Path.Combine(persistentRoot, "QuestSaves", "scope_a");
		Directory.CreateDirectory(scopeDir);
		string scopedSave = Path.Combine(scopeDir, "AIQuestData.json");
		string scopedHistory = Path.Combine(scopeDir, "AIQuestHistory.json");
		string scopedDebug = Path.Combine(scopeDir, "AIQuestDebugSnapshot.json");
		string legacySave = Path.Combine(persistentRoot, "AIQuestData.json");
		string legacyHistory = Path.Combine(persistentRoot, "AIQuestHistory.json");
		string legacyDebug = Path.Combine(persistentRoot, "AIQuestDebugSnapshot.json");
		File.WriteAllText(scopedSave, "{}", Encoding.UTF8);
		File.WriteAllText(scopedHistory, "[]", Encoding.UTF8);
		File.WriteAllText(scopedDebug, "{}", Encoding.UTF8);
		File.WriteAllText(legacySave, "{}", Encoding.UTF8);
		File.WriteAllText(legacyHistory, "[]", Encoding.UTF8);
		File.WriteAllText(legacyDebug, "{}", Encoding.UTF8);
		QuestProgram activeProgram = CreateQuestProgram("wipe_program_active", 990101, "WipeNpcA", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode("wipe_node_active", "Talk"));
		activeProgram.Runtime.CurrentNodeId = "wipe_node_active";
		QuestProgram historyProgram = CreateQuestProgram("wipe_program_history", 990103, "WipeNpcC", QuestTrackType.Mainline, QuestProgramStatus.Completed, CreateTerminalNode("wipe_node_history", "Done"));
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { activeProgram, historyProgram }
		});
		SetField(manager, "_talkFlags", new Dictionary<int, bool> { [activeProgram.AnchorNpcId] = true });
		SetField(manager, "_recentBattleVictories", new Dictionary<int, float> { [activeProgram.AnchorNpcId] = 1f });
		SetField(manager, "_visitedSceneIds", new HashSet<string> { "S_TEST" });
		SetField(manager, "_lastNarrativeLocationSignature", "S_TEST");
		SetField(manager, "_checkTimer", 2.5f);
		SetField(manager, "_hasLoadedPersistence", value: false);
		List<ChatMessage> previousMessages = JsonConvert.DeserializeObject<List<ChatMessage>>(JsonConvert.SerializeObject(AIChatManager.Messages ?? new List<ChatMessage>(), JsonSettings), JsonSettings) ?? new List<ChatMessage>();
		Dictionary<int, List<ChatMessage>> previousRequests = JsonConvert.DeserializeObject<Dictionary<int, List<ChatMessage>>>(JsonConvert.SerializeObject(AIChatManager.requests ?? new Dictionary<int, List<ChatMessage>>(), JsonSettings), JsonSettings) ?? new Dictionary<int, List<ChatMessage>>();
		string previousTargetFilePath = AIChatManager.targetFilePath;
		try
		{
			AIChatManager.Messages = new List<ChatMessage>
			{
				new ChatMessage("user", "stale")
			};
			AIChatManager.requests = new Dictionary<int, List<ChatMessage>> { [activeProgram.AnchorNpcId] = new List<ChatMessage>
			{
				new ChatMessage("assistant", "stale")
			} };
			AIChatManager.targetFilePath = Path.Combine(persistentRoot, "stale_character.txt");
			LegacyDataWipeSummary summary = AIChatManager.ClearLegacyModDataPreserveCharacterCardsForTest(persistentRoot, manager, null);
			bool ok = GetQuestProgramState(manager).Programs.Count == 0 && GetTalkFlags(manager).Count == 0 && GetRecentBattleVictories(manager).Count == 0 && GetVisitedScenes(manager).Count == 0 && string.IsNullOrEmpty(GetField<string>(manager, "_lastNarrativeLocationSignature")) && Math.Abs(GetField<float>(manager, "_checkTimer")) < 0.001f && GetField<bool>(manager, "_hasLoadedPersistence") && AIChatManager.Messages.Count == 0 && AIChatManager.requests.Count == 0 && string.IsNullOrEmpty(AIChatManager.targetFilePath) && !Directory.Exists(Path.Combine(persistentRoot, "QuestSaves")) && !File.Exists(legacySave) && !File.Exists(legacyHistory) && !File.Exists(legacyDebug) && summary != null && summary.QuestSavesDirectoryDeleted && summary.DeletedQuestPersistenceFiles >= 6;
			TestCaseResult testCaseResult = new TestCaseResult();
			testCaseResult.Name = "LegacyDataWipeClearsQuestRuntimeAndPersistence";
			testCaseResult.Passed = ok;
			testCaseResult.Details = (ok ? "Legacy wipe cleared QuestProgram runtime state, AI chat caches, and quest persistence files" : string.Format("Legacy wipe runtime mismatch: summary={0}, programs={1}, talkFlags={2}, requests={3}, questDir={4}", DescribeLegacyDataWipeSummary(summary), GetQuestProgramState(manager).Programs.Count, GetTalkFlags(manager).Count, AIChatManager.requests.Count, Directory.Exists(Path.Combine(persistentRoot, "QuestSaves"))));
			return testCaseResult;
		}
		finally
		{
			AIChatManager.Messages = previousMessages;
			AIChatManager.requests = previousRequests;
			AIChatManager.targetFilePath = previousTargetFilePath;
		}
	}

	private static TestCaseResult TestLegacyDataWipeConfirmationContract()
	{
		string normalLabel = UIManager.GetLegacyWipeButtonLabelForTest(confirmPending: false);
		string confirmLabel = UIManager.GetLegacyWipeButtonLabelForTest(confirmPending: true);
		bool shouldExpire = UIManager.IsLegacyWipeConfirmationExpiredForTest(10f, 9.5f);
		bool shouldKeep = !UIManager.IsLegacyWipeConfirmationExpiredForTest(10f, 10.5f);
		bool ok = normalLabel == "删除过往" && confirmLabel == "再次点击确认清空" && shouldExpire && shouldKeep;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "LegacyDataWipeConfirmationContract";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Legacy wipe button contract exposes confirm label and expiry behavior" : $"Legacy wipe confirm mismatch: normal='{normalLabel}', confirm='{confirmLabel}', expire={shouldExpire}, keep={shouldKeep}");
		return testCaseResult;
	}

	private static TestCaseResult TestExperimentalMainlineOptInContract()
	{
		string warning = UIManager.GetExperimentalMainlineWarningMessage();
		string enabledButton = UIManager.GetExperimentalMainlineDisableButtonLabel(enabled: true);
		string disabledButton = UIManager.GetExperimentalMainlineDisableButtonLabel(enabled: false);
		string enabledStatus = UIManager.GetExperimentalMainlineStatusLabel(enabled: true);
		string disabledStatus = UIManager.GetExperimentalMainlineStatusLabel(enabled: false);
		bool disabledRequiresConfirmation = UIManager.ShouldRequireExperimentalMainlineConfirmation(enabled: false);
		bool enabledSkipsConfirmation = !UIManager.ShouldRequireExperimentalMainlineConfirmation(enabled: true);
		bool ok = warning.Contains("极早期的开发阶段") && warning.Contains("坏档") && warning.Contains("死档") && warning.Contains("谨慎开启") && enabledButton == "关闭实验性主线推演" && disabledButton == "实验性主线推演当前未启用" && enabledStatus == "实验性主线推演：已启用" && disabledStatus == "实验性主线推演：未启用" && disabledRequiresConfirmation && enabledSkipsConfirmation;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "ExperimentalMainlineOptInContract";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Experimental mainline opt-in exposes the expected warning and settings labels" : $"Experimental mainline contract mismatch: warning='{warning}', enabledButton='{enabledButton}', disabledButton='{disabledButton}', enabledStatus='{enabledStatus}', disabledStatus='{disabledStatus}', disabledRequiresConfirmation={disabledRequiresConfirmation}, enabledSkipsConfirmation={enabledSkipsConfirmation}");
		return testCaseResult;
	}

	private static TestCaseResult TestNpcInteractionRefreshDoesNotMutateSharedNpcInfo()
	{
		NPCInfo previousNpcInfo = AIChatManager.npcInfo;
		try
		{
			AIChatManager.npcInfo = new NPCInfo
			{
				NpcID = 20004,
				Name = "井紫铃"
			};
			AIChatManager.HandleNpcInteractionDetectedForTest(20010);
			bool ok = AIChatManager.npcInfo != null && AIChatManager.npcInfo.NpcID == 20004 && AIChatManager.npcInfo.Name == "井紫铃";
			return new TestCaseResult
			{
				Name = "NpcInteractionRefreshDoesNotMutateSharedNpcInfo",
				Passed = ok,
				Details = (ok ? "NPC interaction refresh no longer mutates shared npcInfo before full extraction" : string.Format("npcInfo mutated unexpectedly: id={0}, name={1}", AIChatManager.npcInfo?.NpcID ?? (-1), AIChatManager.npcInfo?.Name ?? "<null>"))
			};
		}
		finally
		{
			AIChatManager.npcInfo = previousNpcInfo;
		}
	}

	private static TestCaseResult TestDialogHistoryNormalizationCollapsesMismatchedNpcFiles(string tempDir)
	{
		string root = Path.Combine(tempDir, "DialogHistoryNormalize");
		Directory.CreateDirectory(root);
		string mismatchedPath = Path.Combine(root, "20010_井紫铃.txt");
		string canonicalPath = Path.Combine(root, "20010_郎鹏云.txt");
		File.WriteAllText(mismatchedPath, "mismatch-line\n", Encoding.UTF8);
		File.WriteAllText(canonicalPath, "canonical-line\n", Encoding.UTF8);
		string normalizedPath = NPCDialog.NormalizeDialogHistoryFilesForTest(root, 20010, "郎鹏云");
		string[] normalizedLines = (File.Exists(canonicalPath) ? File.ReadAllLines(canonicalPath, Encoding.UTF8) : Array.Empty<string>());
		bool ok = string.Equals(normalizedPath, canonicalPath, StringComparison.OrdinalIgnoreCase) && File.Exists(canonicalPath) && !File.Exists(mismatchedPath) && normalizedLines.Length == 2 && normalizedLines[0] == "canonical-line" && normalizedLines[1] == "mismatch-line";
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "DialogHistoryNormalizationCollapsesMismatchedNpcFiles";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Dialog history normalization merged mismatched same-id files into one canonical file" : string.Format("History normalization mismatch: normalized={0}, canonicalExists={1}, mismatchedExists={2}, lines={3}", normalizedPath, File.Exists(canonicalPath), File.Exists(mismatchedPath), string.Join("|", normalizedLines)));
		return testCaseResult;
	}

	private static TestCaseResult TestDialogHistorySummariesDeduplicateNpcIds(string tempDir)
	{
		string root = Path.Combine(tempDir, "DialogHistorySummaries");
		Directory.CreateDirectory(root);
		File.WriteAllText(Path.Combine(root, "20010_井紫铃.txt"), "wrong\n", Encoding.UTF8);
		File.WriteAllText(Path.Combine(root, "20010_郎鹏云.txt"), "right\n", Encoding.UTF8);
		File.WriteAllText(Path.Combine(root, "20004_井紫铃.txt"), "other\n", Encoding.UTF8);
		IReadOnlyList<NPCDialog.DialogHistorySummary> summaries = NPCDialog.BuildDialogHistorySummariesForTest(root, (int npcId) => npcId switch
		{
			20004 => "井紫铃",
			20010 => "郎鹏云",
			_ => string.Empty,
		});
		NPCDialog.DialogHistorySummary duplicateSummary = summaries.FirstOrDefault((NPCDialog.DialogHistorySummary summary) => summary.NpcId == 20010);
		bool ok = summaries.Count == 2 && duplicateSummary != null && duplicateSummary.NpcName == "郎鹏云" && duplicateSummary.MessageCount == 2;
		return new TestCaseResult
		{
			Name = "DialogHistorySummariesDeduplicateNpcIds",
			Passed = ok,
			Details = (ok ? "Dialog history summaries deduplicate same-id files and prefer canonical NPC names" : ("Dialog history summary mismatch: " + string.Join(", ", summaries.Select((NPCDialog.DialogHistorySummary summary) => $"{summary.NpcId}/{summary.NpcName}/{summary.MessageCount}"))))
		};
	}

	private static TestCaseResult TestResolveInitialContextUsesTargetNpcHistory()
	{
		NPCDialog dialog = NPCDialog.Instance;
		GameObject createdDialogObject = null;
		NPCInfo previousDialogNpcInfo = null;
		object previousDialogHistory = null;
		string previousTargetFilePath = AIChatManager.targetFilePath;
		string sourceHistoryPath = null;
		string targetHistoryPath = null;
		string targetCharacterPath = null;
		try
		{
			if (dialog == null)
			{
				createdDialogObject = new GameObject("QuestSelfTest_NPCDialog");
				dialog = createdDialogObject.AddComponent<NPCDialog>();
			}
			FieldInfo dialogHistoryField = typeof(NPCDialog).GetField("dialogHistory", BindingFlags.Instance | BindingFlags.NonPublic);
			previousDialogNpcInfo = dialog.npcInfo;
			previousDialogHistory = dialogHistoryField?.GetValue(dialog);
			dialog.npcInfo = new NPCInfo
			{
				NpcID = 9876501,
				Name = "HistoryLeakSourceNpc"
			};
			sourceHistoryPath = NPCDialog.NormalizeDialogHistoryFilesForTest(Application.persistentDataPath, 9876501, "HistoryLeakSourceNpc");
			targetHistoryPath = NPCDialog.NormalizeDialogHistoryFilesForTest(Application.persistentDataPath, 9876502, "HistoryLeakTargetNpc");
			targetCharacterPath = Path.Combine(Application.persistentDataPath, string.Format("{0}_{1}_character.txt", 9876502, "HistoryLeakTargetNpc"));
			TextEncodingUtil.WriteAllTextUtf8(sourceHistoryPath, "你|||SOURCE_ONLY_DIALOG_TOKEN\n");
			TextEncodingUtil.WriteAllTextUtf8(targetHistoryPath, "你|||TARGET_ONLY_DIALOG_TOKEN\n");
			NPCInfo targetNpcInfo = new NPCInfo
			{
				NpcID = 9876502,
				Name = "HistoryLeakTargetNpc",
				Gender = "女",
				XingGe = "沉静",
				ZhengXie = "中立",
				XiuWei = "筑基初期",
				HaoGanDu = "相熟",
				QingFen = 12,
				StatusStr = "安然",
				Title = "测试人物"
			};
			List<ChatMessage> contextMessages = PromptBuilder.ResolveInitialContext(targetNpcInfo);
			string combinedContent = string.Join("\n", contextMessages.Select((ChatMessage message) => message?.Content ?? string.Empty));
			bool ok = combinedContent.Contains("TARGET_ONLY_DIALOG_TOKEN") && !combinedContent.Contains("SOURCE_ONLY_DIALOG_TOKEN");
			return new TestCaseResult
			{
				Name = "ResolveInitialContextUsesTargetNpcHistory",
				Passed = ok,
				Details = (ok ? "Initial prompt history is loaded from the target NPC instead of the previously active NPC" : string.Format("Initial context leaked dialog history: containsSource={0}, containsTarget={1}", combinedContent.Contains("SOURCE_ONLY_DIALOG_TOKEN"), combinedContent.Contains("TARGET_ONLY_DIALOG_TOKEN")))
			};
		}
		finally
		{
			if (dialog != null)
			{
				dialog.npcInfo = previousDialogNpcInfo;
				typeof(NPCDialog).GetField("dialogHistory", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(dialog, previousDialogHistory);
			}
			AIChatManager.targetFilePath = previousTargetFilePath;
			SafeDelete(sourceHistoryPath);
			SafeDelete(targetHistoryPath);
			SafeDelete(targetCharacterPath);
			if (createdDialogObject != null)
			{
				UnityEngine.Object.DestroyImmediate(createdDialogObject);
			}
		}
	}

	private static TestCaseResult TestMainlineCampaignPersistsSingleBeat(AIQuestManager manager, string savePath, string historyPath)
	{
		ResetWorkingState(manager);
		SafeDelete(savePath);
		SafeDelete(historyPath);
		MainlineCampaign campaign = CreateMainlineCampaign("mainline_persist");
		MainlineChapter chapter = campaign.GetCurrentChapter();
		MainlineBeat beat = chapter?.CurrentBeat;
		QuestProgramState programState = new QuestProgramState
		{
			Programs = new List<QuestProgram>
			{
				new QuestProgram
				{
					ProgramId = "program_mainline_persist",
					Title = "主线第一幕",
					TrackType = QuestTrackType.Mainline,
					CampaignId = campaign.CampaignId,
					ChapterId = chapter?.ChapterId,
					BeatId = beat?.BeatId,
					Status = QuestProgramStatus.Active,
					Runtime = new QuestRuntimeState
					{
						CurrentNodeId = "node_intro",
						ActivatedNodeIds = new List<string> { "node_intro" }
					},
					Nodes = new List<QuestNode>
					{
						new QuestNode
						{
							NodeId = "node_intro",
							Type = QuestNodeType.Narrative,
							Title = "与主线NPC会面"
						}
					}
				}
			}
		};
		SetField(manager, "_mainlineCampaign", campaign);
		SetField(manager, "_questProgramState", programState);
		File.WriteAllText(historyPath, "[{\"questId\":\"legacy_history\"}]", Encoding.UTF8);
		manager.SaveQuests();
		string savedJson = (File.Exists(savePath) ? File.ReadAllText(savePath, Encoding.UTF8) : string.Empty);
		ResetWorkingState(manager);
		File.WriteAllText(historyPath, "[{\"questId\":\"legacy_history_after_save\"}]", Encoding.UTF8);
		SetField(manager, "_hasLoadedPersistence", value: false);
		manager.LoadQuests();
		MainlineCampaign loaded = GetField<MainlineCampaign>(manager, "_mainlineCampaign");
		QuestProgram loadedProgram = GetQuestProgramState(manager).Programs.FirstOrDefault((QuestProgram program) => string.Equals(program.ProgramId, "program_mainline_persist", StringComparison.Ordinal));
		bool saveHasLegacyFields = savedJson.IndexOf("\"ActiveQuests\"", StringComparison.Ordinal) >= 0 || savedJson.IndexOf("\"PendingQuests\"", StringComparison.Ordinal) >= 0;
		bool ok = loaded != null && loaded.CampaignId == campaign.CampaignId && loaded.GetCurrentChapter() != null && loaded.GetCurrentChapter().CurrentBeat != null && loaded.GetCurrentChapter().CurrentBeat.BeatId == campaign.GetCurrentChapter().CurrentBeat.BeatId && loadedProgram != null && loadedProgram.TrackType == QuestTrackType.Mainline && string.Equals(loadedProgram.CampaignId, campaign.CampaignId, StringComparison.Ordinal) && !saveHasLegacyFields && !File.Exists(historyPath);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "MainlineCampaignPersistsSingleBeat";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Single-beat mainline campaign and QuestProgram round-trip through SaveQuests/LoadQuests correctly" : string.Format("Loaded mainline campaign mismatch: campaign={0}, programTrack={1}, historyFileExists={2}, saveHasLegacyFields={3}", DescribeMainlineCampaign(loaded), loadedProgram?.TrackType.ToString() ?? "null", File.Exists(historyPath), saveHasLegacyFields));
		return testCaseResult;
	}

	private static TestCaseResult TestSaveQuestsDoesNotEmitDebugSnapshot(AIQuestManager manager, string savePath, string historyPath)
	{
		ResetWorkingState(manager);
		SafeDelete(savePath);
		SafeDelete(historyPath);
		string debugSnapshotPath = Path.Combine(Path.GetDirectoryName(savePath) ?? Application.persistentDataPath, "AIQuestDebugSnapshot.json");
		SafeDelete(debugSnapshotPath);
		SetField(manager, "_mainlineCampaign", CreateMainlineCampaign("mainline_no_debug_snapshot"));
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram>()
		});
		manager.SaveQuests();
		bool ok = File.Exists(savePath) && !File.Exists(debugSnapshotPath);
		return new TestCaseResult
		{
			Name = "SaveQuestsDoesNotEmitDebugSnapshot",
			Passed = ok,
			Details = (ok ? "SaveQuests now persists only the live quest save and no longer emits an unused debug snapshot sidecar" : $"Unexpected save outputs: saveExists={File.Exists(savePath)}, debugSnapshotExists={File.Exists(debugSnapshotPath)}, debugSnapshotPath={debugSnapshotPath}")
		};
	}

	private static TestCaseResult TestMainlineSingleBeatCompletionCompletesChapter(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineCampaign campaign = CreateMainlineCampaign("mainline_progress");
		MainlineChapter chapter = campaign.GetCurrentChapter();
		MainlineBeat currentBeat = chapter.CurrentBeat;
		QuestProgram program = CreateQuestProgram("program_mainline_progress", 930002, "主线执行者", QuestTrackType.Mainline, QuestProgramStatus.Completed, CreateObjectiveNode("node_progress", "推进主线"));
		program.CampaignId = campaign.CampaignId;
		program.ChapterId = chapter?.ChapterId;
		program.BeatId = currentBeat?.BeatId;
		program.Priority = 300;
		currentBeat.QuestIds.Add(program.ProgramId);
		SetField(manager, "_mainlineCampaign", campaign);
		bool ok = manager.EvaluateCampaignProgressForTest(program) && currentBeat.Status == MainlineBeatStatus.Completed && chapter.Status == MainlineChapterStatus.Completed && campaign.Status == CampaignStatus.Active && campaign.CurrentChapterIndex == 0;
		return new TestCaseResult
		{
			Name = "MainlineSingleBeatCompletionCompletesChapter",
			Passed = ok,
			Details = (ok ? "Completing the only mainline beat now completes the chapter instead of advancing hidden beat state" : ("Unexpected campaign progress state: " + DescribeMainlineCampaign(campaign)))
		};
	}

	private static TestCaseResult TestMainlineWorldControlPrefersHigherPriorityProgram(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		QuestProgram lowerPriorityProgram = CreateQuestProgram("program_mainline_priority_low", 930003, "主线NPC甲", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode("node_mainline_priority_low", "主线步骤甲"));
		lowerPriorityProgram.Priority = 100;
		QuestProgram higherPriorityProgram = CreateQuestProgram("program_mainline_priority_high", 930004, "主线NPC乙", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode("node_mainline_priority_high", "主线步骤乙"));
		higherPriorityProgram.Priority = 300;
		bool higherOverrides = manager.CanQuestProgramClaimWorldControlForTest(higherPriorityProgram, lowerPriorityProgram);
		bool lowerOverrides = manager.CanQuestProgramClaimWorldControlForTest(lowerPriorityProgram, higherPriorityProgram);
		bool ok = higherOverrides && !lowerOverrides;
		return new TestCaseResult
		{
			Name = "MainlineWorldControlPrefersHigherPriorityProgram",
			Passed = ok,
			Details = (ok ? "Mainline world control now resolves by explicit priority within the single-track runtime" : $"World control priority mismatch: higherOverrides={higherOverrides}, lowerOverrides={lowerOverrides}")
		};
	}

	private static TestCaseResult TestMainlineGenerationRequestWaitsForInterceptedBiographyEvent(AIQuestManager manager, string savePath, string historyPath)
	{
		ResetWorkingState(manager);
		SafeDelete(savePath);
		SafeDelete(historyPath);
		NPCInfo npcInfo = new NPCInfo
		{
			NpcID = 20004,
			Name = "井紫铃"
		};
		string error = null;
		string statusMessage;
		MainlineGenerationStartResult result = manager.StartGenerateMainline(npcInfo, delegate
		{
		}, delegate(string message)
		{
			error = message;
		}, null, out statusMessage);
		MainlineCampaign campaign = manager.GetMainlineCampaign();
		bool saveExists = File.Exists(savePath);
		string savedJson = (saveExists ? File.ReadAllText(savePath, Encoding.UTF8) : string.Empty);
		ResetWorkingState(manager);
		SetField(manager, "_hasLoadedPersistence", value: false);
		manager.LoadQuests();
		MainlineCampaign loadedCampaign = manager.GetMainlineCampaign();
		bool ok = result == MainlineGenerationStartResult.WaitingForBiographyEvent && error == null && !string.IsNullOrWhiteSpace(statusMessage) && statusMessage.Contains("等待其下一条官方生平事件被改写") && campaign != null && campaign.SeedNpcId == npcInfo.NpcID && saveExists && savedJson.Contains("\"SeedNpcId\": 20004") && loadedCampaign != null && loadedCampaign.SeedNpcId == npcInfo.NpcID && string.Equals(loadedCampaign.SeedNpcName, npcInfo.Name, StringComparison.Ordinal);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "MainlineGenerationRequestWaitsForInterceptedBiographyEvent";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Manual mainline start now enters waiting-for-official-biography state and immediately persists the anchor binding" : string.Format("Unexpected waiting-start result: result={0}, error={1}, status={2}, campaign={3}, saveExists={4}, loadedCampaign={5}", result, error ?? "<null>", statusMessage ?? "<null>", campaign?.SeedNpcId.ToString() ?? "<null>", saveExists, loadedCampaign?.SeedNpcId.ToString() ?? "<null>"));
		return testCaseResult;
	}

	private static TestCaseResult TestRestoredWaitingMainlineAnchorStatusAndButtonContract(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		SetField(manager, "_mainlineCampaign", CreateMainlineCampaign("mainline_waiting_anchor"));
		MainlineCampaign campaign = GetField<MainlineCampaign>(manager, "_mainlineCampaign");
		if (campaign != null)
		{
			campaign.Status = CampaignStatus.Active;
			campaign.SeedNpcId = 20004;
			campaign.SeedNpcName = "井紫铃";
			MainlineChapter chapter = campaign.GetCurrentChapter();
			if (chapter != null)
			{
				chapter.Status = MainlineChapterStatus.Active;
				chapter.PendingBiographyEvent = null;
			}
		}
		string status;
		bool isWaitingAnchor = manager.IsWaitingMainlineAnchorForNpcForTest(20004, out status);
		string status2;
		bool wrongNpcRejected = !manager.IsWaitingMainlineAnchorForNpcForTest(20005, out status2);
		string buttonLabel = UIManager.GetMainlineEventsGenerateButtonLabelForTest(waitingBoundAnchor: true);
		bool ok = isWaitingAnchor && wrongNpcRejected && !string.IsNullOrWhiteSpace(status) && status.Contains("已将井紫铃设为当前主线锚点") && buttonLabel == "已绑定，等待生平事件";
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "RestoredWaitingMainlineAnchorStatusAndButtonContract";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Restored waiting anchor state is recognized from persistence and maps to the bound mainline button label" : string.Format("Unexpected restored-anchor contract: isWaiting={0}, wrongNpcRejected={1}, status={2}, buttonLabel={3}", isWaitingAnchor, wrongNpcRejected, status ?? "<null>", buttonLabel));
		return testCaseResult;
	}

	private static TestCaseResult TestOfficialSaveContextSaveFlushesOnlyOnOutermostPostfix(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		bool firstPrefix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 0, 0, true, QuestSaveContextEventType.Save, false);
		bool nestedPrefix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 0, 0, true, QuestSaveContextEventType.Save, false);
		bool nestedPostfix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 0, 0, true, QuestSaveContextEventType.Save, true);
		bool outerPostfix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 0, 0, true, QuestSaveContextEventType.Save, true);
		bool duplicatePrefix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 0, 0, true, QuestSaveContextEventType.Save, false);
		bool duplicatePostfix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 0, 0, true, QuestSaveContextEventType.Save, true);
		bool ok = !firstPrefix && !nestedPrefix && !nestedPostfix && outerPostfix && !duplicatePrefix && !duplicatePostfix;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "OfficialSaveContextSaveFlushesOnlyOnOutermostPostfix";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Nested or same-frame duplicate official save callbacks now collapse into a single quest snapshot flush" : $"Unexpected save boundary results: firstPrefix={firstPrefix}, nestedPrefix={nestedPrefix}, nestedPostfix={nestedPostfix}, outerPostfix={outerPostfix}, duplicatePrefix={duplicatePrefix}, duplicatePostfix={duplicatePostfix}");
		return testCaseResult;
	}

	private static TestCaseResult TestOfficialSaveContextCleanSaveSkipsRedundantFlush(AIQuestManager manager, string tempDir)
	{
		string newSaveRoot = Path.Combine(tempDir, "OfficialCleanSaveSkipFlush", "MCSSave");
		string oldSaveRoot = Path.Combine(tempDir, "OfficialCleanSaveSkipFlush", "OldSave");
		Directory.CreateDirectory(newSaveRoot);
		Directory.CreateDirectory(oldSaveRoot);
		ResetWorkingState(manager);
		SetPersistenceOverrides(null, null);
		SetPersistenceRootOverrides(newSaveRoot, oldSaveRoot);
		QuestProgram program = CreateQuestProgram("program_clean_save_skip_flush", 941001, "井紫铃", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode("node_clean_save_skip_flush", "初始节点"));
		program.Runtime.CurrentNodeId = "node_clean_save_skip_flush";
		program.Runtime.ActivatedNodeIds = new List<string> { "node_clean_save_skip_flush" };
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Save);
		string savePath = AIQuestManager.BuildQuestSaveFilePathForTest(0, 0, useNewSaveSystem: true);
		DateTime baselineWriteTimeUtc = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		File.SetLastWriteTimeUtc(savePath, baselineWriteTimeUtc);
		SetField(manager, "_hasLoadedPersistence", value: true);
		SetField(manager, "_questPersistenceDirty", value: false);
		PrepareNextSaveContextFrameForTest(manager);
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Save);
		DateTime finalWriteTimeUtc = File.GetLastWriteTimeUtc(savePath);
		bool ok = finalWriteTimeUtc == baselineWriteTimeUtc;
		return new TestCaseResult
		{
			Name = "OfficialSaveContextCleanSaveSkipsRedundantFlush",
			Passed = ok,
			Details = (ok ? "A clean official save callback no longer rewrites the quest snapshot file when nothing changed" : $"Unexpected redundant save flush: baselineWriteTimeUtc={baselineWriteTimeUtc:o}, finalWriteTimeUtc={finalWriteTimeUtc:o}, savePath={savePath}")
		};
	}

	private static TestCaseResult TestOfficialSaveContextCollapsesSameFrameLoadLikeCallbacks(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		bool selectPrefix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 1, 1, true, QuestSaveContextEventType.Select, false);
		bool selectPostfix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 1, 1, true, QuestSaveContextEventType.Select, true);
		bool loadPrefix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 1, 1, true, QuestSaveContextEventType.Load, false);
		bool loadPostfix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 1, 1, true, QuestSaveContextEventType.Load, true);
		bool switchPrefix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 1, 1, true, QuestSaveContextEventType.Switch, false);
		bool switchPostfix = (bool)InvokeNonPublic(manager, "TryCompleteSaveContextEventBoundary", 1, 1, true, QuestSaveContextEventType.Switch, true);
		bool ok = !selectPrefix && selectPostfix && !loadPrefix && !loadPostfix && !switchPrefix && !switchPostfix;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "OfficialSaveContextCollapsesSameFrameLoadLikeCallbacks";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Same-frame select/load/switch callbacks for the same save scope now collapse into a single quest snapshot reload" : $"Unexpected load-like boundary results: selectPrefix={selectPrefix}, selectPostfix={selectPostfix}, loadPrefix={loadPrefix}, loadPostfix={loadPostfix}, switchPrefix={switchPrefix}, switchPostfix={switchPostfix}");
		return testCaseResult;
	}

	private static void DispatchSaveContextEventForTest(int avatarIndex, int slotIndex, bool useNewSaveSystem, QuestSaveContextEventType eventType)
	{
		AIQuestManager.NotifyGameSaveContextForTest(avatarIndex, slotIndex, useNewSaveSystem, eventType, isPostfix: false);
		AIQuestManager.NotifyGameSaveContextForTest(avatarIndex, slotIndex, useNewSaveSystem, eventType, isPostfix: true);
	}

	private static void PrepareNextSaveContextFrameForTest(AIQuestManager manager)
	{
		SetField(manager, "_lastCompletedSaveContextFrame", -1);
	}

	private static TestCaseResult TestOfficialSaveContextCleanSameScopeSwitchSkipsRedundantReload(AIQuestManager manager, string tempDir)
	{
		return TestOfficialSaveContextCleanSameScopeLoadLikeEventSkipsRedundantReload(manager, tempDir, QuestSaveContextEventType.Switch, "OfficialSaveContextCleanSameScopeSwitchSkipsRedundantReload", "A clean same-slot switch callback no longer replays quest snapshot loading after that slot has already been restored");
	}

	private static TestCaseResult TestOfficialSaveContextCleanSameScopeLoadSkipsRedundantReload(AIQuestManager manager, string tempDir)
	{
		return TestOfficialSaveContextCleanSameScopeLoadLikeEventSkipsRedundantReload(manager, tempDir, QuestSaveContextEventType.Load, "OfficialSaveContextCleanSameScopeLoadSkipsRedundantReload", "A clean same-slot load callback no longer reloads quests again unless unsaved quest state needs to be rolled back");
	}

	private static TestCaseResult TestOfficialSaveContextCleanSameScopeLoadLikeEventSkipsRedundantReload(AIQuestManager manager, string tempDir, QuestSaveContextEventType eventType, string testName, string successDetails)
	{
		string suffix = eventType.ToString().ToLowerInvariant();
		string newSaveRoot = Path.Combine(tempDir, $"OfficialRedundant{eventType}", "MCSSave");
		string oldSaveRoot = Path.Combine(tempDir, $"OfficialRedundant{eventType}", "OldSave");
		Directory.CreateDirectory(newSaveRoot);
		Directory.CreateDirectory(oldSaveRoot);
		ResetWorkingState(manager);
		SetPersistenceOverrides(null, null);
		SetPersistenceRootOverrides(newSaveRoot, oldSaveRoot);
		string initialProgramId = "program_" + suffix + "_initial";
		string replacementProgramId = "program_" + suffix + "_replacement";
		string initialNodeId = "node_" + suffix + "_initial";
		string replacementNodeId = "node_" + suffix + "_replacement";
		QuestProgram initialProgram = CreateQuestProgram(initialProgramId, (int)(940010 + eventType), "井紫铃", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode(initialNodeId, "初始节点"));
		initialProgram.Runtime.CurrentNodeId = initialNodeId;
		initialProgram.Runtime.ActivatedNodeIds = new List<string> { initialNodeId };
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { initialProgram }
		});
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Save);
		SetField(manager, "_questProgramState", QuestProgramPersistence.CreateEmptyState());
		SetField(manager, "_hasLoadedPersistence", value: false);
		SetField(manager, "_questPersistenceDirty", value: false);
		PrepareNextSaveContextFrameForTest(manager);
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Select);
		QuestProgram loadedInitial = GetQuestProgramState(manager).Programs.FirstOrDefault((QuestProgram current) => current?.ProgramId == initialProgramId);
		string initialStateJson = JsonConvert.SerializeObject(GetQuestProgramState(manager), JsonSettings);
		QuestProgram replacementProgram = CreateQuestProgram(replacementProgramId, (int)(940110 + eventType), "井紫铃", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode(replacementNodeId, "替换节点"));
		replacementProgram.Runtime.CurrentNodeId = replacementNodeId;
		replacementProgram.Runtime.ActivatedNodeIds = new List<string> { replacementNodeId };
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { replacementProgram }
		});
		manager.SaveQuests();
		QuestProgramState restoredInitialState = JsonConvert.DeserializeObject<QuestProgramState>(initialStateJson);
		SetField(manager, "_questProgramState", restoredInitialState ?? QuestProgramPersistence.CreateEmptyState());
		SetField(manager, "_hasLoadedPersistence", value: true);
		SetField(manager, "_questPersistenceDirty", value: false);
		PrepareNextSaveContextFrameForTest(manager);
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, eventType);
		QuestProgram afterEvent = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = loadedInitial != null && string.Equals(loadedInitial.ProgramId, initialProgramId, StringComparison.Ordinal) && string.Equals(afterEvent?.ProgramId, initialProgramId, StringComparison.Ordinal) && string.Equals(afterEvent?.Runtime?.CurrentNodeId, initialNodeId, StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = testName,
			Passed = ok,
			Details = (ok ? successDetails : $"Unexpected same-scope {eventType} reload state: loadedInitial={DescribeQuestProgram(loadedInitial)}, afterEvent={DescribeQuestProgram(afterEvent)}")
		};
	}

	private static TestCaseResult TestOfficialSaveContextLoadRollsBackUnsavedProgress(AIQuestManager manager, string tempDir)
	{
		string newSaveRoot = Path.Combine(tempDir, "OfficialSnapshotRollback", "MCSSave");
		string oldSaveRoot = Path.Combine(tempDir, "OfficialSnapshotRollback", "OldSave");
		Directory.CreateDirectory(newSaveRoot);
		Directory.CreateDirectory(oldSaveRoot);
		ResetWorkingState(manager);
		SetPersistenceOverrides(null, null);
		SetPersistenceRootOverrides(newSaveRoot, oldSaveRoot);
		QuestProgram program = CreateQuestProgram("program_official_rollback", 940001, "井紫铃", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode("node_1", "初始节点"));
		program.Runtime.CurrentNodeId = "node_1";
		program.Runtime.ActivatedNodeIds = new List<string> { "node_1" };
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Save);
		program.Runtime.CurrentNodeId = "node_3";
		program.Runtime.ActivatedNodeIds.Add("node_3");
		program.Runtime.CompletedNodeIds.Add("node_2");
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Load);
		QuestProgram loaded = GetQuestProgramState(manager).Programs.FirstOrDefault((QuestProgram current) => current?.ProgramId == "program_official_rollback");
		bool ok = loaded != null && string.Equals(loaded.Runtime?.CurrentNodeId, "node_1", StringComparison.Ordinal) && !loaded.Runtime.CompletedNodeIds.Contains("node_2") && !loaded.Runtime.ActivatedNodeIds.Contains("node_3");
		return new TestCaseResult
		{
			Name = "OfficialSaveContextLoadRollsBackUnsavedProgress",
			Passed = ok,
			Details = (ok ? "Loading the same official slot rewinds QuestProgram runtime to the last official save snapshot" : ("Unexpected same-slot rollback state: " + DescribeQuestProgram(loaded)))
		};
	}

	private static TestCaseResult TestOfficialSaveContextDifferentSlotsRemainIsolated(AIQuestManager manager, string tempDir)
	{
		string newSaveRoot = Path.Combine(tempDir, "OfficialSlotIsolation", "MCSSave");
		string oldSaveRoot = Path.Combine(tempDir, "OfficialSlotIsolation", "OldSave");
		Directory.CreateDirectory(newSaveRoot);
		Directory.CreateDirectory(oldSaveRoot);
		ResetWorkingState(manager);
		SetPersistenceOverrides(null, null);
		SetPersistenceRootOverrides(newSaveRoot, oldSaveRoot);
		QuestProgram slot0Program = CreateQuestProgram("program_slot0", 940002, "井紫铃", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode("slot0_node", "槽位0节点"));
		slot0Program.Runtime.CurrentNodeId = "slot0_node";
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { slot0Program }
		});
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Save);
		QuestProgram slot1Program = CreateQuestProgram("program_slot1", 940003, "井紫铃", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode("slot1_node", "槽位1节点"));
		slot1Program.Runtime.CurrentNodeId = "slot1_node";
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { slot1Program }
		});
		DispatchSaveContextEventForTest(0, 1, useNewSaveSystem: true, QuestSaveContextEventType.Save);
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Load);
		QuestProgram loadedSlot0 = GetQuestProgramState(manager).Programs.FirstOrDefault();
		DispatchSaveContextEventForTest(0, 1, useNewSaveSystem: true, QuestSaveContextEventType.Load);
		QuestProgram loadedSlot1 = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = string.Equals(loadedSlot0?.ProgramId, "program_slot0", StringComparison.Ordinal) && string.Equals(loadedSlot0?.Runtime?.CurrentNodeId, "slot0_node", StringComparison.Ordinal) && string.Equals(loadedSlot1?.ProgramId, "program_slot1", StringComparison.Ordinal) && string.Equals(loadedSlot1?.Runtime?.CurrentNodeId, "slot1_node", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "OfficialSaveContextDifferentSlotsRemainIsolated",
			Passed = ok,
			Details = (ok ? "Different official save slots restore their own independent quest snapshots" : ("Unexpected slot isolation state: slot0=" + DescribeQuestProgram(loadedSlot0) + ", slot1=" + DescribeQuestProgram(loadedSlot1)))
		};
	}

	private static TestCaseResult TestOfficialSaveContextDifferentAvatarsRemainIsolated(AIQuestManager manager, string tempDir)
	{
		string newSaveRoot = Path.Combine(tempDir, "OfficialAvatarIsolation", "MCSSave");
		string oldSaveRoot = Path.Combine(tempDir, "OfficialAvatarIsolation", "OldSave");
		Directory.CreateDirectory(newSaveRoot);
		Directory.CreateDirectory(oldSaveRoot);
		ResetWorkingState(manager);
		SetPersistenceOverrides(null, null);
		SetPersistenceRootOverrides(newSaveRoot, oldSaveRoot);
		QuestProgram avatar0Program = CreateQuestProgram("program_avatar0", 940004, "井紫铃", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode("avatar0_node", "角色0节点"));
		avatar0Program.Runtime.CurrentNodeId = "avatar0_node";
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { avatar0Program }
		});
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Save);
		QuestProgram avatar1Program = CreateQuestProgram("program_avatar1", 940005, "井紫铃", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode("avatar1_node", "角色1节点"));
		avatar1Program.Runtime.CurrentNodeId = "avatar1_node";
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { avatar1Program }
		});
		DispatchSaveContextEventForTest(1, 0, useNewSaveSystem: true, QuestSaveContextEventType.Save);
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Load);
		QuestProgram loadedAvatar0 = GetQuestProgramState(manager).Programs.FirstOrDefault();
		DispatchSaveContextEventForTest(1, 0, useNewSaveSystem: true, QuestSaveContextEventType.Load);
		QuestProgram loadedAvatar1 = GetQuestProgramState(manager).Programs.FirstOrDefault();
		bool ok = string.Equals(loadedAvatar0?.ProgramId, "program_avatar0", StringComparison.Ordinal) && string.Equals(loadedAvatar0?.Runtime?.CurrentNodeId, "avatar0_node", StringComparison.Ordinal) && string.Equals(loadedAvatar1?.ProgramId, "program_avatar1", StringComparison.Ordinal) && string.Equals(loadedAvatar1?.Runtime?.CurrentNodeId, "avatar1_node", StringComparison.Ordinal);
		return new TestCaseResult
		{
			Name = "OfficialSaveContextDifferentAvatarsRemainIsolated",
			Passed = ok,
			Details = (ok ? "Different avatars restore independent quest snapshots even on the same slot index" : ("Unexpected avatar isolation state: avatar0=" + DescribeQuestProgram(loadedAvatar0) + ", avatar1=" + DescribeQuestProgram(loadedAvatar1)))
		};
	}

	private static TestCaseResult TestLegacyQuestScopeMigratesIntoOfficialSlotPath(AIQuestManager manager, string tempDir)
	{
		string root = Path.Combine(tempDir, "OfficialMigration");
		string newSaveRoot = Path.Combine(root, "MCSSave");
		string oldSaveRoot = Path.Combine(root, "OldSave");
		string legacyPersistentRoot = Path.Combine(root, "PersistentData");
		Directory.CreateDirectory(newSaveRoot);
		Directory.CreateDirectory(oldSaveRoot);
		Directory.CreateDirectory(legacyPersistentRoot);
		ResetWorkingState(manager);
		SetPersistenceOverrides(null, null);
		SetPersistenceRootOverrides(newSaveRoot, oldSaveRoot);
		string legacyScopeDir = Path.Combine(legacyPersistentRoot, "QuestSaves", "new_avatar0_slot0");
		Directory.CreateDirectory(legacyScopeDir);
		string legacySavePath = Path.Combine(legacyScopeDir, "AIQuestData.json");
		string officialSavePath = AIQuestManager.BuildQuestSaveFilePathForTest(0, 0, useNewSaveSystem: true);
		string legacyJson = AIQuestManager.SerializeQuestSaveDataForTest(new DateTime(2, 7, 25, 0, 0, 0), CreateMainlineCampaign("migration_test"), new QuestProgramState
		{
			Programs = new List<QuestProgram> { CreateQuestProgram("program_migration", 940006, "井紫铃", QuestTrackType.Mainline, QuestProgramStatus.Active, CreateObjectiveNode("migration_node", "迁移节点")) }
		});
		File.WriteAllText(legacySavePath, legacyJson, Encoding.UTF8);
		AIQuestManager.SetLegacyQuestPersistenceRootForTest(legacyPersistentRoot);
		DispatchSaveContextEventForTest(0, 0, useNewSaveSystem: true, QuestSaveContextEventType.Load);
		QuestProgram loaded = GetQuestProgramState(manager).Programs.FirstOrDefault((QuestProgram program) => program?.ProgramId == "program_migration");
		bool ok = loaded != null && File.Exists(officialSavePath);
		return new TestCaseResult
		{
			Name = "LegacyQuestScopeMigratesIntoOfficialSlotPath",
			Passed = ok,
			Details = (ok ? "Legacy QuestSaves scope is migrated into the official slot path on first load" : $"Migration mismatch: officialExists={File.Exists(officialSavePath)}, loaded={DescribeQuestProgram(loaded)}")
		};
	}

	private static TestCaseResult TestMainlineBeatCompletionResolvesPendingBiographyEvent(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineCampaign campaign = CreateSingleBeatMainlineCampaignWithPendingBiographyEvent("mainline_bio_resolve");
		MainlineChapter chapter = campaign.GetCurrentChapter();
		MainlineBeat currentBeat = chapter.CurrentBeat;
		PendingBiographyEventState pendingEvent = chapter.PendingBiographyEvent;
		QuestProgram program = CreateQuestProgram("program_mainline_bio_resolve", 930100, "主线执行者", QuestTrackType.Mainline, QuestProgramStatus.Completed, CreateObjectiveNode("node_bio_resolve", "完成当前拍"));
		program.CampaignId = campaign.CampaignId;
		program.ChapterId = chapter?.ChapterId;
		program.BeatId = currentBeat?.BeatId;
		program.Priority = 300;
		currentBeat.QuestIds.Add(program.ProgramId);
		SetField(manager, "_mainlineCampaign", campaign);
		bool changed = manager.EvaluateCampaignProgressForTest(program);
		bool pendingRequested = GetField<bool>(manager, "_pendingBiographyStoryGenerationRequested");
		bool ok = changed && currentBeat.Status == MainlineBeatStatus.Completed && pendingEvent != null && pendingEvent.Status == PendingBiographyEventStatus.Resolved && chapter.PendingBiographyEvent == null && chapter.Status == MainlineChapterStatus.Completed && campaign.Status == CampaignStatus.Active && chapter.CurrentBeat != null && !pendingRequested;
		return new TestCaseResult
		{
			Name = "MainlineBeatCompletionResolvesPendingBiographyEvent",
			Passed = ok,
			Details = (ok ? "Completing the only beat now resolves and clears the intercepted biography event instead of queuing another beat" : ("Unexpected pending biography resolution state: " + DescribeMainlineCampaign(campaign)))
		};
	}

	private static TestCaseResult TestMainlineBeatCompletionWithoutPendingBiographyEventCompletesChapter(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineCampaign campaign = CreateSingleBeatMainlineCampaignWithoutPendingBiographyEvent("mainline_terminal_complete");
		MainlineChapter chapter = campaign.GetCurrentChapter();
		MainlineBeat currentBeat = chapter.CurrentBeat;
		QuestProgram program = CreateQuestProgram("program_mainline_terminal_complete", 930101, "主线执行者", QuestTrackType.Mainline, QuestProgramStatus.Completed, CreateObjectiveNode("node_terminal_complete", "完成终章"));
		program.CampaignId = campaign.CampaignId;
		program.ChapterId = chapter?.ChapterId;
		program.BeatId = currentBeat?.BeatId;
		program.Priority = 300;
		currentBeat.QuestIds.Add(program.ProgramId);
		SetField(manager, "_mainlineCampaign", campaign);
		bool ok = manager.EvaluateCampaignProgressForTest(program) && currentBeat.Status == MainlineBeatStatus.Completed && chapter.PendingBiographyEvent == null && chapter.CurrentBeat != null && chapter.Status == MainlineChapterStatus.Completed && campaign.Status == CampaignStatus.Active;
		return new TestCaseResult
		{
			Name = "MainlineBeatCompletionWithoutPendingBiographyEventCompletesChapter",
			Passed = ok,
			Details = (ok ? "A beat with no intercepted biography event now completes the current chapter without fabricating continuation state" : ("Unexpected terminal chapter completion state: " + DescribeMainlineCampaign(campaign)))
		};
	}

	private static TestCaseResult TestPendingBiographyGenerationFailureClearsPendingBiographyEvent(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 26, 0, 0, 0), new string[1] { "1261" }, new Dictionary<int, bool> { [930100] = true });
		manager.QueueQuestProgramAiResponsesForTest(BuildValidMainlineRawNarrativeResponse(), "{\n  \"title\": \"错误结构化剧情\",\n  \"summary\": \"故意缺少 branchRef 的分支后续\",\n  \"timeCategory\": \"中期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"当场抉择\",\n      \"kind\": \"choice\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"测试\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"Player||你准备怎么做？\"\n      ],\n      \"choices\": [\n        { \"choiceRef\": \"先稳住她\", \"text\": \"先稳住她\", \"consequenceDirection\": \"测试\" }\n      ]\n    },\n    {\n      \"blockRef\": \"错误后续\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"故意遗漏 branchRef\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"Npc|井紫铃|这里故意不带分支归属。\"\n      ]\n    }\n  ]\n}", "{\n  \"title\": \"错误结构化剧情\",\n  \"summary\": \"第二次仍然故意缺少 branchRef 的分支后续\",\n  \"timeCategory\": \"中期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"当场抉择\",\n      \"kind\": \"choice\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"测试\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"Player||你准备怎么做？\"\n      ],\n      \"choices\": [\n        { \"choiceRef\": \"先稳住她\", \"text\": \"先稳住她\", \"consequenceDirection\": \"测试\" }\n      ]\n    },\n    {\n      \"blockRef\": \"错误后续\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"第二次仍故意遗漏 branchRef\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"Npc|井紫铃|这里第二次仍不带分支归属。\"\n      ]\n    }\n  ]\n}");
		MainlineCampaign campaign = CreateSingleBeatMainlineCampaignWithPendingBiographyEvent("mainline_bio_retry", PendingBiographyEventStatus.Captured);
		MainlineChapter chapter = campaign.GetCurrentChapter();
		SetField(manager, "_mainlineCampaign", campaign);
		SetField(manager, "_pendingBiographyStoryGenerationRequested", value: true);
		bool triggered = (bool)InvokeNonPublic(manager, "TryProgressPendingBiographyStory");
		bool pendingRequested = GetField<bool>(manager, "_pendingBiographyStoryGenerationRequested");
		bool ok = triggered && chapter.PendingBiographyEvent == null && chapter.Status == MainlineChapterStatus.Failed && campaign.Status == CampaignStatus.Active && !pendingRequested && chapter.CurrentBeat != null && chapter.CurrentBeat.Status == MainlineBeatStatus.Failed && manager.GetActiveQuestPrograms().Count == 0;
		return new TestCaseResult
		{
			Name = "PendingBiographyGenerationFailureClearsPendingBiographyEvent",
			Passed = ok,
			Details = (ok ? "When single-beat generation fails, the intercepted biography event is cleared so later settlements can capture a new first biography event" : ("Unexpected pending biography failure state: " + DescribeMainlineCampaign(campaign)))
		};
	}

	private static TestCaseResult TestPendingBiographyGenerationFailureAllowsNextBiographyCapture(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 26, 0, 0, 0), new string[1] { "1261" }, new Dictionary<int, bool> { [930100] = true });
		manager.QueueQuestProgramAiResponsesForTest(BuildValidMainlineRawNarrativeResponse(), "{\n  \"title\": \"错误结构化剧情\",\n  \"summary\": \"故意缺少 branchRef 的分支后续\",\n  \"timeCategory\": \"中期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"当场抉择\",\n      \"kind\": \"choice\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"测试\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"player||你准备怎么做？\"\n      ],\n      \"choices\": [\n        { \"choiceRef\": \"先稳住她\", \"text\": \"先稳住她\", \"consequenceDirection\": \"测试\" }\n      ]\n    },\n    {\n      \"blockRef\": \"错误后续\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"故意遗漏 branchRef\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"npc|井紫铃|这里故意不带分支归属。\"\n      ]\n    }\n  ]\n}", "{\n  \"title\": \"错误结构化剧情\",\n  \"summary\": \"第二次仍然故意缺少 branchRef 的分支后续\",\n  \"timeCategory\": \"中期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"当场抉择\",\n      \"kind\": \"choice\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"测试\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"player||你准备怎么做？\"\n      ],\n      \"choices\": [\n        { \"choiceRef\": \"先稳住她\", \"text\": \"先稳住她\", \"consequenceDirection\": \"测试\" }\n      ]\n    },\n    {\n      \"blockRef\": \"错误后续\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"第二次仍故意遗漏 branchRef\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"npc|井紫铃|这里第二次仍不带分支归属。\"\n      ]\n    }\n  ]\n}");
		MainlineCampaign campaign = CreateSingleBeatMainlineCampaignWithPendingBiographyEvent("mainline_bio_failed_next_capture", PendingBiographyEventStatus.Captured);
		SetField(manager, "_mainlineCampaign", campaign);
		SetField(manager, "_pendingBiographyStoryGenerationRequested", value: true);
		bool triggered = (bool)InvokeNonPublic(manager, "TryProgressPendingBiographyStory");
		bool capturedNext = manager.TryInterceptOfficialBiographyEvent(BiographyEventKind.UseDanYao, 930100, "NPCUseItem.UseDanYao", new JObject
		{
			["npcId"] = 930100,
			["itemId"] = 3101
		}, "0002-07-27 00:00:00");
		return new TestCaseResult
		{
			Name = "PendingBiographyGenerationFailureAllowsNextBiographyCapture",
			Passed = (triggered && capturedNext),
			Details = ((triggered && capturedNext) ? "After a failed generation clears the pending biography event, the next time-advance settlement can capture a new first biography event again" : $"Unexpected follow-up biography capture state: triggered={triggered}, capturedNext={capturedNext}, campaign={DescribeMainlineCampaign(campaign)}")
		};
	}

	private static TestCaseResult TestPendingBiographyEventNpcResolutionDoesNotFabricateContext(AIQuestManager manager)
	{
		PendingBiographyEventState pendingEvent = new PendingBiographyEventState
		{
			NpcId = 2147483000,
			NpcName = "不存在的NPC"
		};
		NPCInfo resolved = InvokeNonPublic(manager, "ResolvePendingBiographyEventNpcInfo", pendingEvent) as NPCInfo;
		bool ok = resolved == null;
		return new TestCaseResult
		{
			Name = "PendingBiographyEventNpcResolutionDoesNotFabricateContext",
			Passed = ok,
			Details = (ok ? "Pending biography continuation now fails fast when NPC runtime context is unavailable instead of fabricating a partial NPCInfo" : ("Unexpected fabricated pending biography NPC context: " + JsonConvert.SerializeObject(resolved, JsonSettings)))
		};
	}

	private static TestCaseResult TestAcceptQuestProgramPreparesNpcPresenceForActivation(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "147" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "S116", 46, 0, 46);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_prepare_activation",
			Title = "逸风城畔的旧识",
			AnchorNpcId = 20004,
			AnchorNpcName = "井紫铃",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Draft,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004 },
				AllowedSceneIds = new List<string> { "147" },
				SelectedPredicates = new List<string> { "scene.is", "npc.talked_to_player" },
				SelectedCommands = new List<string> { "npc.set_action" }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_intro",
					Type = QuestNodeType.Objective,
					Title = "逸风城重逢",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string>
							{
								["sceneId"] = "147",
								["sceneName"] = "逸风城附近"
							}
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.talked_to_player",
							Params = new Dictionary<string, string> { ["npcId"] = "20004" }
						}
					},
					WorldControl = new QuestNodeWorldControl
					{
						SceneId = "147",
						NpcRequirements = new List<QuestNpcPresenceRequirement>
						{
							new QuestNpcPresenceRequirement
							{
								NpcId = 20004,
								Mode = QuestNpcPresenceMode.Encounter
							}
						}
					}
				}
			},
			Runtime = new QuestRuntimeState()
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		bool accepted = manager.AcceptQuestProgram(program.ProgramId);
		Dictionary<int, string> npcLocations = GetField<Dictionary<int, string>>(manager, "_questProgramTestNpcLocations");
		string locationId;
		int actionId;
		int statusId;
		int lockActionId;
		bool hasNpcState = manager.TryGetQuestProgramRuntimeTestNpcStateForTest(20004, out locationId, out actionId, out statusId, out lockActionId);
		string relocatedSceneId;
		bool ok = accepted && npcLocations != null && npcLocations.TryGetValue(20004, out relocatedSceneId) && string.Equals(relocatedSceneId, "147", StringComparison.Ordinal) && hasNpcState && actionId == 33 && lockActionId == 0;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "AcceptQuestProgramPreparesNpcPresenceForActivation";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Accepting a beat now relocates required NPCs to the activation scene and resets big-map encounters to a visible traveling action" : string.Format("Unexpected activation preparation state: accepted={0}, location={1}, action={2}, lock={3}", accepted, (npcLocations != null && npcLocations.TryGetValue(20004, out var currentLocation)) ? currentLocation : "null", hasNpcState ? actionId.ToString() : "null", hasNpcState ? lockActionId.ToString() : "null"));
		return testCaseResult;
	}

	private static TestCaseResult TestAcceptQuestProgramDoesNotStageFutureNodeNpcBeforeItIsCurrent(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "147", 33, 0, 46);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_future_stage_guard",
			Title = "先踩点再见人",
			AnchorNpcId = 20004,
			AnchorNpcName = "井紫铃",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Draft,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004 },
				AllowedSceneIds = new List<string> { "S1381", "S114" },
				SelectedPredicates = new List<string> { "scene.is", "npc.talked_to_player" },
				SelectedCommands = new List<string> { "npc.set_action" }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_visit",
					Type = QuestNodeType.Objective,
					Title = "先去云汐城查探",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S1381" }
						}
					},
					NextNodeId = "node_talk"
				},
				new QuestNode
				{
					NodeId = "node_talk",
					Type = QuestNodeType.Objective,
					Title = "回到藏经阁交谈",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S114" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.talked_to_player",
							Params = new Dictionary<string, string> { ["npcId"] = "20004" }
						}
					},
					NextNodeId = "node_terminal",
					WorldControl = new QuestNodeWorldControl
					{
						SceneId = "S114",
						NpcRequirements = new List<QuestNpcPresenceRequirement>
						{
							new QuestNpcPresenceRequirement
							{
								NpcId = 20004,
								Mode = QuestNpcPresenceMode.FormalTalk
							}
						}
					}
				},
				new QuestNode
				{
					NodeId = "node_terminal",
					Type = QuestNodeType.Terminal,
					Title = "结束"
				}
			},
			Runtime = new QuestRuntimeState()
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		bool accepted = manager.AcceptQuestProgram(program.ProgramId);
		Dictionary<int, string> npcLocations = GetField<Dictionary<int, string>>(manager, "_questProgramTestNpcLocations");
		string locationId;
		bool notMoved = accepted && npcLocations != null && npcLocations.TryGetValue(20004, out locationId) && string.Equals(locationId, "147", StringComparison.Ordinal);
		string currentLocation;
		return new TestCaseResult
		{
			Name = "AcceptQuestProgramDoesNotStageFutureNodeNpcBeforeItIsCurrent",
			Passed = notMoved,
			Details = (notMoved ? "Accepting a program now stages only the current step; future NPC talk targets stay in place until their node becomes current" : string.Format("Future-node NPC was staged too early: accepted={0}, location={1}", accepted, (npcLocations != null && npcLocations.TryGetValue(20004, out currentLocation)) ? currentLocation : "null"))
		};
	}

	private static TestCaseResult TestQuestProgramSceneTransitionStagesNextNodeNpcWhenItBecomesCurrent(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [20004] = true });
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "147", 33, 0, 46);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_scene_transition_stage",
			Title = "切场后再见人",
			AnchorNpcId = 20004,
			AnchorNpcName = "井紫铃",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Draft,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004 },
				AllowedSceneIds = new List<string> { "S116", "S1312" },
				SelectedPredicates = new List<string> { "scene.is", "npc.talked_to_player" },
				SelectedCommands = new List<string> { "npc.set_action" }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_transition",
					Type = QuestNodeType.Objective,
					Title = "前往坊市",
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S1312" }
						}
					},
					NextNodeId = "node_talk"
				},
				new QuestNode
				{
					NodeId = "node_talk",
					Type = QuestNodeType.Objective,
					Title = "与井紫铃交谈",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S1312" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.talked_to_player",
							Params = new Dictionary<string, string> { ["npcId"] = "20004" }
						}
					},
					NextNodeId = "node_terminal",
					WorldControl = new QuestNodeWorldControl
					{
						SceneId = "S1312",
						NpcRequirements = new List<QuestNpcPresenceRequirement>
						{
							new QuestNpcPresenceRequirement
							{
								NpcId = 20004,
								Mode = QuestNpcPresenceMode.FormalTalk
							}
						}
					}
				},
				new QuestNode
				{
					NodeId = "node_terminal",
					Type = QuestNodeType.Terminal,
					Title = "结束"
				}
			},
			Runtime = new QuestRuntimeState()
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		bool accepted = manager.AcceptQuestProgram(program.ProgramId);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 1, 0), new string[1] { "S1312" }, new Dictionary<int, bool> { [20004] = true });
		bool changed = manager.PublishQuestProgramEventForTest("scene.changed");
		Dictionary<int, string> npcLocations = GetField<Dictionary<int, string>>(manager, "_questProgramTestNpcLocations");
		string locationId;
		int actionId;
		int statusId;
		int lockActionId;
		bool hasNpcState = manager.TryGetQuestProgramRuntimeTestNpcStateForTest(20004, out locationId, out actionId, out statusId, out lockActionId);
		string locationId2;
		bool ok = accepted && changed && npcLocations != null && npcLocations.TryGetValue(20004, out locationId2) && string.Equals(locationId2, "S1312", StringComparison.Ordinal) && hasNpcState && actionId == 113 && lockActionId == 0;
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "QuestProgramSceneTransitionStagesNextNodeNpcWhenItBecomesCurrent";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "After a scene-transition node completes, the next formal-talk node now stages its NPC only when it becomes current" : string.Format("Unexpected post-transition staging: accepted={0}, changed={1}, location={2}, action={3}, lock={4}", accepted, changed, (npcLocations != null && npcLocations.TryGetValue(20004, out var currentLocation)) ? currentLocation : "null", hasNpcState ? actionId.ToString() : "null", hasNpcState ? lockActionId.ToString() : "null"));
		return testCaseResult;
	}

	private static TestCaseResult TestQuestProgramChoiceStagesOnlySelectedBranchNpc(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S114" }, new Dictionary<int, bool>
		{
			[20004] = true,
			[20085] = true
		});
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20004, "147", 33, 0, 46);
		manager.SetQuestProgramRuntimeTestNpcWorldStateForTest(20085, "S1312", 113, 0, 46);
		QuestProgram program = new QuestProgram
		{
			ProgramId = "program_choice_stage_selected",
			Title = "分支只预摆已选中节点",
			AnchorNpcId = 20004,
			AnchorNpcName = "井紫铃",
			TrackType = QuestTrackType.Mainline,
			Status = QuestProgramStatus.Draft,
			WorldContract = new QuestWorldContract
			{
				AllowedNpcIds = new List<int> { 20004, 20085 },
				AllowedSceneIds = new List<string> { "S114" },
				SelectedPredicates = new List<string> { "scene.is", "npc.talked_to_player" },
				SelectedCommands = new List<string> { "npc.set_action" }
			},
			Nodes = new List<QuestNode>
			{
				new QuestNode
				{
					NodeId = "node_choice",
					Type = QuestNodeType.Choice,
					Title = "先找谁",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S114" }
						}
					},
					Narrative = new QuestNarrativeBlock
					{
						NarrativeId = "narr_choice",
						Lines = new List<AIQuestStoryLine> { AIQuestStoryLine.CreateNpc("先去见谁？", 20004) },
						Choices = new List<QuestChoiceOption>
						{
							new QuestChoiceOption
							{
								ChoiceId = "choice_a",
								Text = "先见井紫铃",
								NextNodeId = "node_branch_a"
							},
							new QuestChoiceOption
							{
								ChoiceId = "choice_b",
								Text = "先见另一人",
								NextNodeId = "node_branch_b"
							}
						}
					}
				},
				new QuestNode
				{
					NodeId = "node_branch_a",
					Type = QuestNodeType.Objective,
					Title = "与井紫铃交谈",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S114" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.talked_to_player",
							Params = new Dictionary<string, string> { ["npcId"] = "20004" }
						}
					},
					NextNodeId = "node_terminal",
					WorldControl = new QuestNodeWorldControl
					{
						SceneId = "S114",
						NpcRequirements = new List<QuestNpcPresenceRequirement>
						{
							new QuestNpcPresenceRequirement
							{
								NpcId = 20004,
								Mode = QuestNpcPresenceMode.FormalTalk
							}
						}
					}
				},
				new QuestNode
				{
					NodeId = "node_branch_b",
					Type = QuestNodeType.Objective,
					Title = "与另一人交谈",
					ActivationPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "scene.is",
							Params = new Dictionary<string, string> { ["sceneId"] = "S114" }
						}
					},
					CompletionPredicates = new List<QuestPredicateInvocation>
					{
						new QuestPredicateInvocation
						{
							Opcode = "npc.talked_to_player",
							Params = new Dictionary<string, string> { ["npcId"] = "20085" }
						}
					},
					NextNodeId = "node_terminal",
					WorldControl = new QuestNodeWorldControl
					{
						SceneId = "S114",
						NpcRequirements = new List<QuestNpcPresenceRequirement>
						{
							new QuestNpcPresenceRequirement
							{
								NpcId = 20085,
								Mode = QuestNpcPresenceMode.FormalTalk
							}
						}
					}
				},
				new QuestNode
				{
					NodeId = "node_terminal",
					Type = QuestNodeType.Terminal,
					Title = "结束"
				}
			},
			Runtime = new QuestRuntimeState()
		};
		SetField(manager, "_questProgramState", new QuestProgramState
		{
			Programs = new List<QuestProgram> { program }
		});
		manager.SetQuestProgramRuntimeTestChoiceSelectionForTest(program.ProgramId, "node_choice", "choice_a");
		bool accepted = manager.AcceptQuestProgram(program.ProgramId);
		Dictionary<int, string> npcLocations = GetField<Dictionary<int, string>>(manager, "_questProgramTestNpcLocations");
		string selectedLocation;
		bool selectedMoved = accepted && npcLocations != null && npcLocations.TryGetValue(20004, out selectedLocation) && string.Equals(selectedLocation, "S114", StringComparison.Ordinal);
		string unselectedLocation;
		bool unselectedStayed = npcLocations != null && npcLocations.TryGetValue(20085, out unselectedLocation) && string.Equals(unselectedLocation, "S1312", StringComparison.Ordinal);
		string currentSelected;
		string currentUnselected;
		return new TestCaseResult
		{
			Name = "QuestProgramChoiceStagesOnlySelectedBranchNpc",
			Passed = (selectedMoved && unselectedStayed),
			Details = ((selectedMoved && unselectedStayed) ? "Choice resolution now stages only the NPCs required by the selected branch" : string.Format("Unexpected branch staging: accepted={0}, selected={1}, unselected={2}", accepted, (npcLocations != null && npcLocations.TryGetValue(20004, out currentSelected)) ? currentSelected : "null", (npcLocations != null && npcLocations.TryGetValue(20085, out currentUnselected)) ? currentUnselected : "null"))
		};
	}

	private static TestCaseResult TestMainlineBeatCompletionDoesNotGenerateFollowupBeat(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		MainlineCampaign campaign = CreateSingleBeatMainlineCampaignWithPendingBiographyEvent("mainline_bio_no_followup");
		MainlineChapter chapter = campaign.GetCurrentChapter();
		MainlineBeat currentBeat = chapter.CurrentBeat;
		PendingBiographyEventState pendingEvent = chapter.PendingBiographyEvent;
		QuestProgram completedProgram = CreateQuestProgram("program_mainline_bio_no_followup", 930100, "主线执行者", QuestTrackType.Mainline, QuestProgramStatus.Completed, CreateObjectiveNode("node_bio_no_followup", "完成当前拍"));
		completedProgram.CampaignId = campaign.CampaignId;
		completedProgram.ChapterId = chapter?.ChapterId;
		completedProgram.BeatId = currentBeat?.BeatId;
		completedProgram.Priority = 300;
		currentBeat.QuestIds.Add(completedProgram.ProgramId);
		SetField(manager, "_mainlineCampaign", campaign);
		bool changed = manager.EvaluateCampaignProgressForTest(completedProgram);
		bool triggered = (bool)InvokeNonPublic(manager, "TryProgressPendingBiographyStory");
		bool pendingRequested = GetField<bool>(manager, "_pendingBiographyStoryGenerationRequested");
		bool ok = changed && !triggered && chapter.Status == MainlineChapterStatus.Completed && campaign.Status == CampaignStatus.Active && pendingEvent != null && pendingEvent.Status == PendingBiographyEventStatus.Resolved && chapter.PendingBiographyEvent == null && chapter.CurrentBeat != null && !pendingRequested && manager.GetActiveQuestPrograms().Count == 0;
		return new TestCaseResult
		{
			Name = "MainlineBeatCompletionDoesNotGenerateFollowupBeat",
			Passed = ok,
			Details = (ok ? "Completing a biography-backed beat no longer generates any follow-up beat from the same event" : ("Unexpected single-beat completion state: campaign=" + DescribeMainlineCampaign(campaign)))
		};
	}

	private static TestCaseResult TestMainlineStoryBeatGenerationRetriesOnceOnInvalidFormat(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [930100] = true });
		manager.QueueQuestProgramAiResponsesForTest("{\n  \"title\": \"错误正文\",\n  \"scriptBlocks\": []\n}", BuildValidMainlineRawNarrativeResponse(), BuildValidMainlineStructuredBeatResponse(), BuildValidMainlineBeatGamePlanResponse());
		MainlineCampaign campaign = CreateSingleBeatMainlineCampaignWithPendingBiographyEvent("mainline_story_retry");
		SetField(manager, "_mainlineCampaign", campaign);
		NPCInfo npcInfo = CreateMainlineRetryNpcInfo();
		QuestProgram generated = null;
		string error = null;
		List<string> progressMessages = new List<string>();
		MainlineGenerationStartResult result = manager.StartGenerateMainline(npcInfo, delegate(QuestProgram program)
		{
			generated = program;
		}, delegate(string message)
		{
			error = message;
		}, delegate(string message, int _, int __, float ___)
		{
			progressMessages.Add(message);
		});
		bool ok = result == MainlineGenerationStartResult.Started && string.IsNullOrWhiteSpace(error) && generated != null && progressMessages.Any((string message) => message.Contains("当前主线剧情正文不合法，正在请求修正")) && string.Equals(generated.Title, "岫络谷赴约", StringComparison.Ordinal);
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "MainlineStoryBeatGenerationRetriesOnceOnInvalidFormat";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Stage-1 generation now retries once with a repair prompt when the first raw narrative output is structurally invalid" : string.Format("Unexpected story-beat retry state: result={0}, error={1}, generated={2}, progress={3}", result, error ?? "<null>", DescribeQuestProgram(generated), string.Join(" | ", progressMessages)));
		return testCaseResult;
	}

	private static TestCaseResult TestMainlineStructuredBeatGenerationRetriesOnceOnInvalidFormat(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [930100] = true });
		manager.QueueQuestProgramAiResponsesForTest(BuildValidMainlineRawNarrativeResponse(), "{\n  \"title\": \"错误结构化剧情\",\n  \"summary\": \"故意少分支后续\",\n  \"timeCategory\": \"中期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"当场抉择\",\n      \"kind\": \"choice\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"测试\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"Player||如何应对？\"\n      ],\n      \"choices\": [\n        { \"choiceRef\": \"稳住她\", \"text\": \"稳住她\", \"consequenceDirection\": \"测试\" }\n      ]\n    }\n  ]\n}", BuildValidMainlineStructuredBeatResponse(), BuildValidMainlineBeatGamePlanResponse());
		MainlineCampaign campaign = CreateSingleBeatMainlineCampaignWithPendingBiographyEvent("mainline_gameplan_retry");
		SetField(manager, "_mainlineCampaign", campaign);
		NPCInfo npcInfo = CreateMainlineRetryNpcInfo();
		QuestProgram generated = null;
		string error = null;
		List<string> progressMessages = new List<string>();
		MainlineGenerationStartResult result = manager.StartGenerateMainline(npcInfo, delegate(QuestProgram program)
		{
			generated = program;
		}, delegate(string message)
		{
			error = message;
		}, delegate(string message, int _, int __, float ___)
		{
			progressMessages.Add(message);
		});
		bool ok = result == MainlineGenerationStartResult.Started && string.IsNullOrWhiteSpace(error) && generated != null && progressMessages.Any((string message) => message.Contains("当前主线剧情结构输出不合法，正在请求修正")) && generated.WorldContract != null && generated.WorldContract.SelectedCommands.Contains("npc.set_action");
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "MainlineStructuredBeatGenerationRetriesOnceOnInvalidFormat";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Stage-2 generation now retries once with a repair prompt when the first structured beat output is contract-invalid" : string.Format("Unexpected game-plan retry state: result={0}, error={1}, generated={2}, progress={3}", result, error ?? "<null>", DescribeQuestProgram(generated), string.Join(" | ", progressMessages)));
		return testCaseResult;
	}

	private static TestCaseResult TestMainlineBeatGamePlanGenerationRetriesOnceOnInvalidFormat(AIQuestManager manager)
	{
		ResetWorkingState(manager);
		manager.SetQuestProgramRuntimeTestSnapshotForTest(new DateTime(2, 7, 25, 0, 0, 0), new string[1] { "S116" }, new Dictionary<int, bool> { [930100] = true });
		manager.QueueQuestProgramAiResponsesForTest(BuildValidMainlineRawNarrativeResponse(), BuildValidMainlineStructuredBeatResponse(), "{\n  \"summary\": \"错误的功能计划\",\n  \"flowSteps\": [\n    { \"stepRef\": \"赴谷相见\", \"kind\": \"encounter_at_scene\", \"sceneName\": \"岫络谷\", \"targetNpcName\": \"井紫铃\" },\n    { \"stepRef\": \"当场抉择\", \"kind\": \"present_choice\", \"sourceBlockRef\": \"当场抉择\" },\n    { \"stepRef\": \"错误公共后续\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"安抚收束\" }\n  ]\n}", BuildValidMainlineBeatGamePlanResponse(), BuildValidMainlineBeatGamePlanResponse());
		MainlineCampaign campaign = CreateSingleBeatMainlineCampaignWithPendingBiographyEvent("mainline_compile_retry");
		SetField(manager, "_mainlineCampaign", campaign);
		NPCInfo npcInfo = CreateMainlineRetryNpcInfo();
		QuestProgram generated = null;
		string error = null;
		List<string> progressMessages = new List<string>();
		MainlineGenerationStartResult result = manager.StartGenerateMainline(npcInfo, delegate(QuestProgram program)
		{
			generated = program;
		}, delegate(string message)
		{
			error = message;
		}, delegate(string message, int _, int __, float ___)
		{
			progressMessages.Add(message);
		});
		bool ok = result == MainlineGenerationStartResult.Started && string.IsNullOrWhiteSpace(error) && generated != null && progressMessages.Any((string message) => message.Contains("当前主线功能规划输出不合法，正在请求修正")) && generated.WorldContract != null && generated.WorldContract.SelectedCommands.Contains("npc.set_action");
		TestCaseResult testCaseResult = new TestCaseResult();
		testCaseResult.Name = "MainlineBeatGamePlanGenerationRetriesOnceOnInvalidFormat";
		testCaseResult.Passed = ok;
		testCaseResult.Details = (ok ? "Stage-3 generation now retries once with a repair prompt when the first semantic plan output is structurally invalid" : string.Format("Unexpected game-plan retry state: result={0}, error={1}, generated={2}, progress={3}", result, error ?? "<null>", DescribeQuestProgram(generated), string.Join(" | ", progressMessages)));
		return testCaseResult;
	}

	private static QuestProgram CreateQuestProgram(string programId, int anchorNpcId, string anchorNpcName, QuestTrackType trackType, QuestProgramStatus status, params QuestNode[] nodes)
	{
		QuestProgram program = new QuestProgram
		{
			ProgramId = programId,
			Title = programId,
			AnchorNpcId = anchorNpcId,
			AnchorNpcName = anchorNpcName,
			TrackType = trackType,
			Status = status,
			Nodes = (nodes?.ToList() ?? new List<QuestNode>()),
			Runtime = new QuestRuntimeState()
		};
		if (program.Nodes.Count > 0)
		{
			program.Runtime.CurrentNodeId = program.Nodes[0].NodeId;
			program.Runtime.ActivatedNodeIds.Add(program.Nodes[0].NodeId);
		}
		return program;
	}

	private static QuestNode CreateObjectiveNode(string nodeId, string title)
	{
		return new QuestNode
		{
			NodeId = nodeId,
			Type = QuestNodeType.Objective,
			Title = title,
			Summary = title
		};
	}

	private static QuestNode CreateTerminalNode(string nodeId, string title)
	{
		return new QuestNode
		{
			NodeId = nodeId,
			Type = QuestNodeType.Terminal,
			Title = title,
			Summary = title
		};
	}

	private static MainlineCampaign CreateMainlineCampaign(string suffix)
	{
		string campaignId = "campaign_" + suffix;
		string chapterId = "chapter_" + suffix + "_1";
		string beatId = "beat_" + suffix + "_current";
		return new MainlineCampaign
		{
			CampaignId = campaignId,
			Title = "自测主线",
			Premise = "主线总纲",
			StoryBible = "连续性圣经",
			SeedNpcId = 930000,
			SeedNpcName = "主线发起人",
			Status = CampaignStatus.Active,
			CurrentChapterIndex = 0,
			CreatedAt = DateTime.Now,
			Continuity = new ContinuityMemorySnapshot
			{
				Summary = "主线连续性摘要",
				LastUpdatedAt = DateTime.Now,
				RecentWorldChanges = new List<string> { "序章已经发生" }
			},
			Chapters = new List<MainlineChapter>
			{
				new MainlineChapter
				{
					ChapterId = chapterId,
					Title = "第一章 山门异动",
					Summary = "调查山门异动并推进主线。",
					Theme = "异动",
					Status = MainlineChapterStatus.Active,
					CurrentBeat = new MainlineBeat
					{
						BeatId = beatId,
						Title = "当前拍",
						Summary = "与关键人物接头并在本拍内完成剧情。",
						Status = MainlineBeatStatus.Active,
						QuestIds = new List<string>(),
						WorldContract = new WorldContract
						{
							ContractId = "contract_" + suffix + "_current",
							Summary = "需要拉主线NPC到场",
							AllowedNpcIds = new List<int> { 930000 },
							AllowedSceneIds = new List<string> { "S1" }
						}
					}
				}
			}
		};
	}

	private static string DescribePresentation(AIQuestStoryPresentation presentation)
	{
		if (presentation == null)
		{
			return "null";
		}
		return $"{presentation.SpeakerName}|char={presentation.CharacterId}|player={presentation.IsPlayerBubble}|narrator={presentation.IsNarrator}";
	}

	private static string DescribeStoryLines(List<AIQuestStoryLine> lines)
	{
		if (lines == null)
		{
			return "null";
		}
		return string.Join(" || ", lines.Select((AIQuestStoryLine line) => (line == null) ? "null" : $"{line.SpeakerType}:{line.Text}"));
	}

	private static string DescribeOfficialTaskUiEntry(OfficialTaskUiEntry entry)
	{
		if (entry == null)
		{
			return "null";
		}
		return entry.EntryId + "|" + entry.Title + "|" + entry.StatusText + "|" + entry.GuideText + "|" + entry.DescriptionText;
	}

	private static string GetPendingMailType(object pendingMail)
	{
		if (pendingMail == null)
		{
			return "null";
		}
		PropertyInfo property = pendingMail.GetType().GetProperty("MailType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (property != null)
		{
			return (property.GetValue(pendingMail) as string) ?? "null";
		}
		return (pendingMail.GetType().GetField("MailType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(pendingMail) as string) ?? "null";
	}

	private static string DescribePendingMailTypes(List<object> pendingMails)
	{
		if (pendingMails == null)
		{
			return "null";
		}
		return string.Join(", ", pendingMails.Select(GetPendingMailType));
	}

	private static string DescribeTornadoRoles(List<LlmTornado.Chat.ChatMessage> messages)
	{
		if (messages == null)
		{
			return "null";
		}
		return string.Join(", ", messages.Select((LlmTornado.Chat.ChatMessage message) => message?.Role?.ToString() ?? "null"));
	}

	private static string DescribeLegacyDataWipeSummary(LegacyDataWipeSummary summary)
	{
		if (summary == null)
		{
			return "null";
		}
		return JsonConvert.SerializeObject(summary, JsonSettings);
	}

	private static string DescribeMainlineCampaign(MainlineCampaign campaign)
	{
		if (campaign == null)
		{
			return "null";
		}
		return JsonConvert.SerializeObject(campaign, JsonSettings);
	}

	private static MainlineCampaign CreateSingleBeatMainlineCampaignWithPendingBiographyEvent(string suffix, PendingBiographyEventStatus pendingStatus = PendingBiographyEventStatus.ActiveStory)
	{
		string campaignId = "campaign_" + suffix;
		string chapterId = "chapter_" + suffix + "_1";
		string beatAId = "beat_" + suffix + "_a";
		return new MainlineCampaign
		{
			CampaignId = campaignId,
			Title = "自测主线",
			Premise = "主线总纲",
			StoryBible = "连续性圣经",
			SeedNpcId = 930100,
			SeedNpcName = "主线发起人",
			Status = CampaignStatus.Active,
			CurrentChapterIndex = 0,
			CreatedAt = DateTime.Now,
			Continuity = new ContinuityMemorySnapshot
			{
				Summary = "主线连续性摘要",
				LastUpdatedAt = DateTime.Now
			},
			Chapters = new List<MainlineChapter>
			{
				new MainlineChapter
				{
					ChapterId = chapterId,
					Title = "第一章 心火照骨",
					Summary = "围绕被拦截生平事件完成唯一一拍。",
					Theme = "心火",
					Status = MainlineChapterStatus.Active,
					PendingBiographyEvent = new PendingBiographyEventState
					{
						EventId = "event_" + suffix,
						NpcId = 930100,
						NpcName = "主线发起人",
						BiographyEventKind = BiographyEventKind.QiYu,
						SourceMethodKey = "NPCLiLian.NPCYouLi",
						CapturedAtGameTime = "0002-07-25 00:00:00",
						OriginalOfficialTime = "0002-07-25 00:00:00",
						OriginalPayload = new JObject { ["npcId"] = 930100 },
						TimeCategory = "中期",
						Status = pendingStatus,
						CampaignId = campaignId,
						Summary = "这是一条刚被拦下的官方奇遇，会被改写成当前主线。",
						PlayerFacingHint = "围绕这条奇遇推进当前唯一一拍主线。",
						GenerationAttempted = false
					},
					CurrentBeat = new MainlineBeat
					{
						BeatId = beatAId,
						Title = "当前拍",
						Summary = "与关键人物接头并在本拍内完成全部剧情。",
						Status = MainlineBeatStatus.Active,
						StoryDraft = new MainlineBeatStoryDraft(),
						GamePlan = new MainlineBeatGamePlan(),
						WorldContract = new WorldContract
						{
							ContractId = "contract_" + suffix + "_a",
							Summary = "当前拍",
							AllowedNpcIds = new List<int> { 930100 },
							AllowedSceneIds = new List<string> { "S116" },
							SelectedPredicates = new List<string> { "scene.is" },
							SelectedCommands = new List<string> { "npc.set_action" }
						},
						QuestIds = new List<string>()
					}
				}
			}
		};
	}

	private static MainlineCampaign CreateSingleBeatMainlineCampaignWithoutPendingBiographyEvent(string suffix)
	{
		MainlineCampaign campaign = CreateSingleBeatMainlineCampaignWithPendingBiographyEvent(suffix);
		MainlineChapter chapter = campaign?.GetCurrentChapter();
		if (chapter != null)
		{
			chapter.PendingBiographyEvent = null;
		}
		return campaign;
	}

	private static NPCInfo CreateMainlineRetryNpcInfo()
	{
		return new NPCInfo
		{
			NpcID = 930100,
			Name = "井紫铃",
			Gender = "女",
			XiuWei = "炼气后期",
			HaoGanDu = "冷淡",
			QingFen = 8,
			Age = 24
		};
	}

	private static string BuildValidMainlineRawNarrativeResponse()
	{
		return string.Join("", Enumerable.Repeat("岫络谷外山风猎猎，你循着井紫铃留下的暗记走到谷口，见她独自立在乱石间，像是把整段道途的焦躁都压进了呼吸里。她先不肯明说，只问你是否还愿意听她把那团心火背后的旧事讲完。你听出她表面强撑，实则已被连日服药与闭关失败逼到边缘。她说若再这样拖下去，自己迟早会把求长生的执念烧成疯魔。你知道这一刻不能再像旁观者那样站着不动，必须当场做出抉择。若你选择先稳住她的心神，便放缓语气，劝她把真正惧怕失去的东西讲清楚，她会在沉默之后承认自己害怕的从来不是一时落后，而是被宗门、同辈与自己亲手抛下；你陪她把旧账一点点翻开，她最终在夜色里收束怒火，答应暂时停药，与你约定从今日开始改走更稳的路。若你选择逼她直面那团火，便当场戳破她把烈药当捷径的执念，逼她承认自己已经在道途中越走越偏；她会先被你的话刺得发怒，甚至几乎与你翻脸，但在被你步步追问之后，终于承认这些年一直在拿暴躁和逞强遮掩恐惧，随后在激烈争执里把积压已久的话全部说破，最后仍在这一夜与过去决裂。两条路都在谷中当场走到尽头，不再留下下一拍，也不再把结局拖到以后。", 3));
	}

	private static string BuildValidMainlineStructuredBeatResponse()
	{
		return "{\n  \"title\": \"岫络谷赴约\",\n  \"summary\": \"井紫铃在岫络谷逼自己直面心火，玩家必须当场作出选择。\",\n  \"timeCategory\": \"中期\",\n  \"scriptBlocks\": [\n    {\n      \"blockRef\": \"谷口相见\",\n      \"kind\": \"encounter\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"在岫络谷撞见井紫铃强压心火\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"Npc|井紫铃|你既然来了，就别只站在一旁看我继续发疯。\",\n        \"Player||你把我约到岫络谷，不会只是为了让我看你硬撑。\"\n      ]\n    },\n    {\n      \"blockRef\": \"当场抉择\",\n      \"kind\": \"choice\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"决定以何种姿态介入这场心火危机\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"Player||这一回，你要我怎么救你，还是你要继续拿命去赌？\"\n      ],\n      \"choices\": [\n        { \"choiceRef\": \"安抚她\", \"text\": \"先稳住她，让她自己把心里的话说出来\", \"consequenceDirection\": \"以安抚路线收束这一夜\" },\n        { \"choiceRef\": \"逼她面对\", \"text\": \"逼她直面执念，当场把旧账全揭开\", \"consequenceDirection\": \"以强硬路线收束这一夜\" }\n      ]\n    },\n    {\n      \"blockRef\": \"安抚收束\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"安抚路线的独立后续\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"Npc|井紫铃|若我今日还能收住这团火，算你替我把最后一丝理智拽了回来。\"\n      ],\n      \"branchRef\": \"安抚她\"\n    },\n    {\n      \"blockRef\": \"强压收束\",\n      \"kind\": \"fallout\",\n      \"sceneName\": \"岫络谷\",\n      \"scenePurpose\": \"强硬路线的独立后续\",\n      \"participantNpcNames\": [\"井紫铃\"],\n      \"lines\": [\n        \"Npc|井紫铃|你既逼我把这层皮撕开，那我今日便认了，这些年我就是在拿自己赌。\"\n      ],\n      \"branchRef\": \"逼她面对\"\n    }\n  ]\n}";
	}

	private static string BuildValidMainlineBeatGamePlanResponse()
	{
		return "{\n  \"flowSteps\": [\n    { \"stepRef\": \"赴谷相见\", \"kind\": \"encounter_at_scene\", \"sceneName\": \"岫络谷\", \"targetNpcName\": \"井紫铃\" },\n    { \"stepRef\": \"撞见心火\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"谷口相见\" },\n    { \"stepRef\": \"当场抉择\", \"kind\": \"present_choice\", \"sourceBlockRef\": \"当场抉择\" },\n    { \"stepRef\": \"安抚后续\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"安抚收束\", \"branchRef\": \"安抚她\", \"effects\": [{ \"effectRef\": \"安抚增益\", \"kind\": \"favor_change\", \"targetNpcName\": \"井紫铃\", \"payload\": { \"amount\": 10 } }] },\n    { \"stepRef\": \"强压后续\", \"kind\": \"play_script_block\", \"sourceBlockRef\": \"强压收束\", \"branchRef\": \"逼她面对\", \"effects\": [{ \"effectRef\": \"强压代价\", \"kind\": \"favor_change\", \"targetNpcName\": \"井紫铃\", \"payload\": { \"amount\": -5 } }] }\n  ]\n}";
	}

	private static string DescribeMainlineChapter(MainlineChapter chapter)
	{
		if (chapter == null)
		{
			return "null";
		}
		return JsonConvert.SerializeObject(chapter, JsonSettings);
	}

	private static string DescribeMainlineBeat(MainlineBeat beat)
	{
		if (beat == null)
		{
			return "null";
		}
		return JsonConvert.SerializeObject(beat, JsonSettings);
	}

	private static string DescribeWorldContract(WorldContract contract)
	{
		if (contract == null)
		{
			return "null";
		}
		return JsonConvert.SerializeObject(contract, JsonSettings);
	}

	private static string DescribeQuestProgramLoadResult(QuestProgramLoadResult result)
	{
		if (result == null)
		{
			return "null";
		}
		return JsonConvert.SerializeObject(result, JsonSettings);
	}

	private static string DescribeQuestProgram(QuestProgram program)
	{
		if (program == null)
		{
			return "null";
		}
		return JsonConvert.SerializeObject(program, JsonSettings);
	}

	private static string DescribeCapability(QuestCapabilityDescriptor descriptor)
	{
		if (descriptor == null)
		{
			return "null";
		}
		return JsonConvert.SerializeObject(descriptor, JsonSettings);
	}

	private static string DescribeEmailBucket(Dictionary<string, List<EmailData>> buckets, string npcKey)
	{
		if (buckets == null)
		{
			return "null";
		}
		if (!buckets.TryGetValue(npcKey, out var emails) || emails == null)
		{
			return "<missing>";
		}
		return string.Join(" | ", emails.Select((EmailData email) => (email == null) ? "null" : $"old={email.isOld},oldId={email.oldId},time={email.sendTime ?? string.Empty}"));
	}

	private static void ResetWorkingState(AIQuestManager manager)
	{
		manager.SetQuestProgramRuntimeTestSnapshotForTest(null, null, null);
		manager.ClearQuestProgramRuntimeTestNpcWorldStateForTest();
		manager.ClearQuestProgramRuntimeTestChoiceSelectionsForTest();
		manager.ClearQuestProgramAiResponsesForTest();
		SetField(manager, "_questProgramState", QuestProgramPersistence.CreateEmptyState());
		SetField<Coroutine>(manager, "_questProgramNarrativeCoroutine", null);
		SetField(manager, "_activeQuestProgramNarrativeProgramId", string.Empty);
		SetField(manager, "_activeQuestProgramNarrativeNodeId", string.Empty);
		SetField(manager, "_questTaskMemories", RehydrateQuestTaskMemoryMap(new Dictionary<int, List<object>>()));
		SetField<MainlineCampaign>(manager, "_mainlineCampaign", null);
		GetTalkFlags(manager).Clear();
		GetRecentBattleVictories(manager).Clear();
		GetVisitedScenes(manager).Clear();
		SetField(manager, "_hasLoadedPersistence", value: true);
		SetField(manager, "_questPersistenceDirty", value: false);
	}

	private static ManagerStateBackup CaptureState(AIQuestManager manager)
	{
		return new ManagerStateBackup
		{
			QuestProgramStateJson = CloneFieldJson(manager, "_questProgramState"),
			QuestTaskMemories = CloneQuestTaskMemoryMap(GetQuestTaskMemoryMap(manager)),
			MainlineCampaignJson = CloneFieldJson(manager, "_mainlineCampaign"),
			TalkFlags = new Dictionary<int, bool>(GetTalkFlags(manager)),
			RecentBattleVictories = new Dictionary<int, float>(GetRecentBattleVictories(manager)),
			VisitedSceneIds = new HashSet<string>(GetVisitedScenes(manager)),
			HasLoadedPersistence = GetField<bool>(manager, "_hasLoadedPersistence"),
			SavePathOverride = GetStaticField<string>("_saveFilePathOverride"),
			HistoryPathOverride = GetStaticField<string>("_historyFilePathOverride"),
			NewSaveRootOverride = GetStaticField<string>("_newSaveRootOverride"),
			OldSaveRootOverride = GetStaticField<string>("_oldSaveRootOverride"),
			LegacyQuestPersistenceRootOverride = GetStaticField<string>("_legacyQuestPersistenceRootOverride")
		};
	}

	private static void RestoreState(AIQuestManager manager, ManagerStateBackup backup)
	{
		RestoreFieldFromJson(manager, "_questProgramState", backup.QuestProgramStateJson);
		SetField(manager, "_questTaskMemories", RehydrateQuestTaskMemoryMap(backup.QuestTaskMemories));
		RestoreFieldFromJson(manager, "_mainlineCampaign", backup.MainlineCampaignJson);
		SetField(manager, "_talkFlags", backup.TalkFlags);
		SetField(manager, "_recentBattleVictories", backup.RecentBattleVictories);
		SetField(manager, "_visitedSceneIds", backup.VisitedSceneIds);
		SetField(manager, "_hasLoadedPersistence", backup.HasLoadedPersistence);
		AIQuestManager.SetPersistenceRootOverrides(backup.NewSaveRootOverride, backup.OldSaveRootOverride);
		AIQuestManager.SetLegacyQuestPersistenceRootForTest(backup.LegacyQuestPersistenceRootOverride);
	}

	private static void SetPersistenceOverrides(string savePath, string historyPath)
	{
		AIQuestManager.SetPersistencePathOverrides(savePath, historyPath);
	}

	private static void SetPersistenceRootOverrides(string newSaveRoot, string oldSaveRoot)
	{
		AIQuestManager.SetPersistenceRootOverrides(newSaveRoot, oldSaveRoot);
	}

	private static void WriteReport(string reportPath, List<TestCaseResult> results)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("MCS_AIChatMod Quest Self-Test Report");
		sb.AppendLine($"GeneratedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
		sb.AppendLine("PersistentDataPath: " + Application.persistentDataPath);
		sb.AppendLine();
		foreach (TestCaseResult result in results)
		{
			sb.Append(result.Passed ? "[PASS] " : "[FAIL] ");
			sb.Append(result.Name);
			sb.Append(" - ");
			sb.AppendLine(result.Details);
		}
		File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
	}

	private static void SafeDelete(string path)
	{
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	private static QuestProgramState GetQuestProgramState(AIQuestManager manager)
	{
		return GetField<QuestProgramState>(manager, "_questProgramState");
	}

	private static Dictionary<int, bool> GetTalkFlags(AIQuestManager manager)
	{
		return GetField<Dictionary<int, bool>>(manager, "_talkFlags");
	}

	private static Dictionary<int, float> GetRecentBattleVictories(AIQuestManager manager)
	{
		return GetField<Dictionary<int, float>>(manager, "_recentBattleVictories");
	}

	private static List<object> GetPendingQuestMailList(AIQuestManager manager)
	{
		FieldInfo field = typeof(AIQuestManager).GetField("_pendingQuestMails", BindingFlags.Instance | BindingFlags.NonPublic);
		IEnumerable values = field.GetValue(manager) as IEnumerable;
		List<object> result = new List<object>();
		if (values == null)
		{
			return result;
		}
		foreach (object value in values)
		{
			result.Add(value);
		}
		return result;
	}

	private static Dictionary<int, List<object>> GetQuestTaskMemoryMap(AIQuestManager manager)
	{
		FieldInfo field = typeof(AIQuestManager).GetField("_questTaskMemories", BindingFlags.Instance | BindingFlags.NonPublic);
		IDictionary dictionary = field.GetValue(manager) as IDictionary;
		Dictionary<int, List<object>> result = new Dictionary<int, List<object>>();
		if (dictionary == null)
		{
			return result;
		}
		foreach (DictionaryEntry entry in dictionary)
		{
			List<object> values = new List<object>();
			if (entry.Value is IEnumerable enumerable)
			{
				foreach (object value in enumerable)
				{
					values.Add(value);
				}
			}
			result[(int)entry.Key] = values;
		}
		return result;
	}

	private static Dictionary<int, List<object>> CloneQuestTaskMemoryMap(Dictionary<int, List<object>> source)
	{
		Dictionary<int, List<object>> result = new Dictionary<int, List<object>>();
		if (source == null)
		{
			return result;
		}
		foreach (KeyValuePair<int, List<object>> pair in source)
		{
			result[pair.Key] = ((pair.Value != null) ? new List<object>(pair.Value) : new List<object>());
		}
		return result;
	}

	private static object RehydrateQuestTaskMemoryMap(Dictionary<int, List<object>> source)
	{
		Type entryType = typeof(AIQuestManager).GetNestedType("QuestTaskMemoryEntry", BindingFlags.NonPublic);
		Type listType = typeof(List<>).MakeGenericType(entryType);
		Type dictionaryType = typeof(Dictionary<, >).MakeGenericType(typeof(int), listType);
		object result = Activator.CreateInstance(dictionaryType);
		MethodInfo addPair = dictionaryType.GetMethod("Add", new Type[2]
		{
			typeof(int),
			listType
		});
		MethodInfo addEntry = listType.GetMethod("Add", new Type[1] { entryType });
		if (source == null)
		{
			return result;
		}
		foreach (KeyValuePair<int, List<object>> pair in source)
		{
			object list = Activator.CreateInstance(listType);
			if (pair.Value != null)
			{
				foreach (object entry in pair.Value)
				{
					addEntry.Invoke(list, new object[1] { entry });
				}
			}
			addPair.Invoke(result, new object[2] { pair.Key, list });
		}
		return result;
	}

	private static string CloneFieldJson(AIQuestManager manager, string fieldName)
	{
		object value = typeof(AIQuestManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(manager);
		return JsonConvert.SerializeObject(value, JsonSettings);
	}

	private static void RestoreFieldFromJson(AIQuestManager manager, string fieldName, string json)
	{
		FieldInfo field = typeof(AIQuestManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		if (!(field == null))
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				ResetFieldToEmptyInstance(manager, fieldName);
				return;
			}
			object value = JsonConvert.DeserializeObject(json, field.FieldType, JsonSettings);
			field.SetValue(manager, value ?? Activator.CreateInstance(field.FieldType));
		}
	}

	private static void ResetFieldToEmptyInstance(AIQuestManager manager, string fieldName)
	{
		FieldInfo field = typeof(AIQuestManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		if (!(field == null))
		{
			object empty = Activator.CreateInstance(field.FieldType);
			field.SetValue(manager, empty);
		}
	}

	private static HashSet<string> GetVisitedScenes(AIQuestManager manager)
	{
		return GetField<HashSet<string>>(manager, "_visitedSceneIds");
	}

	private static string GetCurrentLocationIdForTrigger(AIQuestManager manager)
	{
		if (!(InvokeNonPublic(manager, "GetCurrentLocationIds") is HashSet<string> { Count: not 0 } locationIds))
		{
			return string.Empty;
		}
		return locationIds.FirstOrDefault((string id) => !string.IsNullOrWhiteSpace(id)) ?? string.Empty;
	}

	private static NarrativeActorCandidate FindMovableNarrativeActorCandidate(string currentLocationId)
	{
		if (string.IsNullOrWhiteSpace(currentLocationId) || NpcJieSuanManager.inst?.npcMap == null)
		{
			return null;
		}
		IEnumerable<int> candidateIds = NpcJieSuanManager.inst.npcMap.threeSenceNPCDictionary.Values.SelectMany((List<int> v) => v).Concat(NpcJieSuanManager.inst.npcMap.bigMapNPCDictionary.Values.SelectMany((List<int> v) => v)).Concat(NpcJieSuanManager.inst.npcMap.fuBenNPCDictionary.Values.SelectMany((Dictionary<int, List<int>> dict) => dict.Values.SelectMany((List<int> v) => v)))
			.Distinct()
			.Select(NPCEx.NPCIDToNew);
		foreach (int npcId in candidateIds)
		{
			if (npcId <= 1 || NpcJieSuanManager.inst.IsDeath(npcId))
			{
				continue;
			}
			string locationId = QuestElementPicker.GetNpcCurrentScene(npcId);
			if (!string.IsNullOrWhiteSpace(locationId) && !string.Equals(locationId, currentLocationId, StringComparison.Ordinal))
			{
				JSONObject npcData = NpcJieSuanManager.inst.GetNpcData(npcId);
				if (npcData != null)
				{
					string npcName = (jsonData.instance.AvatarRandomJsonData.HasField(npcId.ToString()) ? jsonData.instance.AvatarRandomJsonData[npcId.ToString()]["Name"].Str : $"NPC_{npcId}");
					return new NarrativeActorCandidate
					{
						NpcId = npcId,
						NpcName = npcName,
						OriginalLocationId = locationId
					};
				}
			}
		}
		return null;
	}

	private static NpcWorldSnapshot CaptureNpcWorldSnapshot(int npcId)
	{
		int normalizedNpcId = NPCEx.NPCIDToNew(npcId);
		JSONObject npcData = NpcJieSuanManager.inst.GetNpcData(normalizedNpcId);
		return new NpcWorldSnapshot
		{
			NpcId = normalizedNpcId,
			LocationId = QuestElementPicker.GetNpcCurrentScene(normalizedNpcId),
			ActionId = (npcData?["ActionId"]?.I).GetValueOrDefault(),
			StatusId = (npcData?["Status"]?["StatusId"]?.I).GetValueOrDefault(),
			HasLockAction = (npcData?.HasField("LockAction") ?? false),
			LockActionId = ((npcData != null && npcData.HasField("LockAction")) ? npcData["LockAction"].I : 0)
		};
	}

	private static void RestoreNpcWorldSnapshot(NpcWorldSnapshot snapshot)
	{
		if (snapshot == null || snapshot.NpcId <= 0 || NpcJieSuanManager.inst?.npcMap == null)
		{
			return;
		}
		int npcId = NPCEx.NPCIDToNew(snapshot.NpcId);
		if (NpcJieSuanManager.inst.IsDeath(npcId))
		{
			return;
		}
		JSONObject npcData = NpcJieSuanManager.inst.GetNpcData(npcId);
		if (npcData == null)
		{
			return;
		}
		NpcJieSuanManager.inst.npcMap.RemoveNpcByList(npcId);
		if (!string.IsNullOrWhiteSpace(snapshot.LocationId))
		{
			int bigMapIndex;
			if (snapshot.LocationId.StartsWith("S", StringComparison.Ordinal) && int.TryParse(snapshot.LocationId.Substring(1), out var threeSceneId))
			{
				NpcJieSuanManager.inst.npcMap.AddNpcToThreeScene(npcId, threeSceneId);
			}
			else if (snapshot.LocationId.StartsWith("F", StringComparison.Ordinal))
			{
				string[] parts = snapshot.LocationId.Split(',');
				if (parts.Length == 2 && int.TryParse(parts[0].Substring(1), out var fuBenSceneId) && int.TryParse(parts[1], out var fuBenNode))
				{
					NpcJieSuanManager.inst.npcMap.AddNpcToFuBen(npcId, fuBenSceneId, fuBenNode);
				}
			}
			else if (int.TryParse(snapshot.LocationId, out bigMapIndex))
			{
				Dictionary<int, List<int>> bigMapNpcDict = NpcJieSuanManager.inst.npcMap.bigMapNPCDictionary;
				if (!bigMapNpcDict.TryGetValue(bigMapIndex, out var npcList))
				{
					npcList = (bigMapNpcDict[bigMapIndex] = new List<int>());
				}
				if (!npcList.Contains(npcId))
				{
					npcList.Add(npcId);
				}
			}
		}
		NPCEx.SetNPCAction(npcId, snapshot.ActionId);
		if (snapshot.StatusId > 0)
		{
			NpcJieSuanManager.inst.npcStatus.SetNpcStatus(npcId, snapshot.StatusId);
		}
		if (snapshot.HasLockAction)
		{
			npcData.SetField("LockAction", snapshot.LockActionId);
		}
		else if (npcData.HasField("LockAction"))
		{
			npcData.RemoveField("LockAction");
		}
		NpcJieSuanManager.inst.isUpDateNpcList = true;
	}

	private static string DescribeForcedNpcStates(List<QuestForcedNpcState> states)
	{
		if (states == null)
		{
			return "null";
		}
		return JsonConvert.SerializeObject(states, JsonSettings);
	}

	private static string DescribeNpcWorldSnapshot(NpcWorldSnapshot snapshot)
	{
		if (snapshot == null)
		{
			return "null";
		}
		return JsonConvert.SerializeObject(snapshot, JsonSettings);
	}

	private static T GetField<T>(AIQuestManager manager, string fieldName)
	{
		FieldInfo field = typeof(AIQuestManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		return (T)field.GetValue(manager);
	}

	private static bool TryGetField<T>(AIQuestManager manager, string fieldName, out T value)
	{
		FieldInfo field = typeof(AIQuestManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		if (field == null)
		{
			value = default(T);
			return false;
		}
		object raw = field.GetValue(manager);
		if (raw is T typed)
		{
			value = typed;
			return true;
		}
		value = default(T);
		return raw == null;
	}

	private static void SetField<T>(AIQuestManager manager, string fieldName, T value)
	{
		FieldInfo field = typeof(AIQuestManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		if (!(field == null))
		{
			field.SetValue(manager, value);
		}
	}

	private static T GetStaticField<T>(string fieldName)
	{
		FieldInfo field = typeof(AIQuestManager).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		return (T)field.GetValue(null);
	}

	private static ConfigFile GetConfigManagerConfigFile()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		FieldInfo field = typeof(ConfigManager).GetField("_configFile", BindingFlags.Static | BindingFlags.NonPublic);
		return (field == null) ? ((ConfigFile)null) : ((ConfigFile)field.GetValue(null));
	}

	private static void RestoreConfigManagerConfigFile(ConfigFile configFile)
	{
		if (configFile != null)
		{
			ConfigManager.Initialize(configFile);
		}
	}

	private static void SetStaticField(string fieldName, object value)
	{
		FieldInfo field = typeof(AIQuestManager).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
		field.SetValue(null, value);
	}

	private static void InvokeVoid(AIQuestManager manager, string methodName)
	{
		MethodInfo method = typeof(AIQuestManager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		method.Invoke(manager, null);
	}

	private static object InvokeNonPublic(AIQuestManager manager, string methodName, params object[] args)
	{
		MethodInfo method = typeof(AIQuestManager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
		return method.Invoke(manager, args);
	}

	private static string DescribeTimeCandidates(IEnumerable<QuestElementPicker.TimeCandidate> candidates)
	{
		if (candidates == null)
		{
			return "<null>";
		}
		return string.Join("; ", candidates.Select((QuestElementPicker.TimeCandidate candidate) => (candidate == null) ? "<null>" : $"{candidate.Label}:{candidate.MinMonths}-{candidate.MaxMonths}"));
	}

	private static AIQuestStoryPresentation ResolveQuestProgramStoryPresentationForTest(QuestProgram program, AIQuestStoryLine line)
	{
		MethodInfo method = typeof(AIQuestManager).GetMethod("ResolveQuestProgramStoryPresentation", BindingFlags.Static | BindingFlags.NonPublic);
		return (AIQuestStoryPresentation)method.Invoke(null, new object[2] { program, line });
	}
}
