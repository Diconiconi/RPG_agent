using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MCS_AIChatMod;

public static class QuestProgramPersistence
{
	public const int CurrentFormatVersion = 1;

	private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
	{
		NullValueHandling = NullValueHandling.Ignore,
		Formatting = Formatting.Indented
	};

	public static string Serialize(QuestProgramState state)
	{
		QuestProgramState normalized = state ?? CreateEmptyState();
		normalized.FormatVersion = 1;
		if (normalized.Programs == null)
		{
			normalized.Programs = new List<QuestProgram>();
		}
		return JsonConvert.SerializeObject(normalized, Settings);
	}

	public static QuestProgramLoadResult Deserialize(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return new QuestProgramLoadResult
			{
				State = CreateEmptyState()
			};
		}
		JToken token = JToken.Parse(json);
		if (IsLegacyQuestRoot(token, out var reason))
		{
			return new QuestProgramLoadResult
			{
				State = CreateEmptyState(),
				WasLegacyQuestDataWiped = true,
				LegacyFormatReason = reason
			};
		}
		QuestProgramState state = token.ToObject<QuestProgramState>() ?? CreateEmptyState();
		state.FormatVersion = 1;
		state.Programs = state.Programs ?? new List<QuestProgram>();
		foreach (QuestProgram program in state.Programs)
		{
			NormalizeProgram(program);
		}
		return new QuestProgramLoadResult
		{
			State = state
		};
	}

	internal static string SerializeForTest(QuestProgramState state)
	{
		return Serialize(state);
	}

	internal static QuestProgramLoadResult DeserializeForTest(string json)
	{
		return Deserialize(json);
	}

	public static QuestProgramState NormalizeState(QuestProgramState state)
	{
		QuestProgramState normalized = state ?? CreateEmptyState();
		normalized.FormatVersion = 1;
		normalized.Programs = normalized.Programs ?? new List<QuestProgram>();
		foreach (QuestProgram program in normalized.Programs)
		{
			NormalizeProgram(program);
		}
		return normalized;
	}

	public static QuestProgramState CreateEmptyState()
	{
		return new QuestProgramState
		{
			FormatVersion = 1,
			SavedAt = DateTime.MinValue,
			Programs = new List<QuestProgram>()
		};
	}

	private static bool IsLegacyQuestRoot(JToken token, out string reason)
	{
		reason = null;
		if (token == null)
		{
			return false;
		}
		if (token.Type == JTokenType.Array)
		{
			reason = "legacy_array_root";
			return true;
		}
		if (token.Type != JTokenType.Object)
		{
			return false;
		}
		JObject obj = (JObject)token;
		string[] legacyKeys = new string[6] { "ActiveQuests", "PendingQuests", "PendingQuestMails", "QuestTaskMemories", "OfficialProactiveContacts", "ExpandedOfficialTaskStates" };
		string[] array = legacyKeys;
		foreach (string key in array)
		{
			if (obj.Property(key, StringComparison.OrdinalIgnoreCase) != null)
			{
				reason = "legacy_key:" + key;
				return true;
			}
		}
		return false;
	}

	private static void NormalizeProgram(QuestProgram program)
	{
		if (program == null)
		{
			return;
		}
		program.WorldContract = program.WorldContract ?? new QuestWorldContract();
		program.Nodes = program.Nodes ?? new List<QuestNode>();
		program.Runtime = program.Runtime ?? new QuestRuntimeState();
		program.OnAccepted = program.OnAccepted ?? new List<QuestCommandInvocation>();
		program.OnCompleted = program.OnCompleted ?? new List<QuestCommandInvocation>();
		program.OnFailed = program.OnFailed ?? new List<QuestCommandInvocation>();
		foreach (QuestNode node in program.Nodes)
		{
			if (node == null)
			{
				continue;
			}
			node.ActivationPredicates = node.ActivationPredicates ?? new List<QuestPredicateInvocation>();
			node.CompletionPredicates = node.CompletionPredicates ?? new List<QuestPredicateInvocation>();
			NormalizePredicateList(node.ActivationPredicates);
			NormalizePredicateList(node.CompletionPredicates);
			node.OnActivated = node.OnActivated ?? new List<QuestCommandInvocation>();
			node.OnCompleted = node.OnCompleted ?? new List<QuestCommandInvocation>();
			if (node.Narrative == null)
			{
				continue;
			}
			node.Narrative.Lines = node.Narrative.Lines ?? new List<AIQuestStoryLine>();
			node.Narrative.Gates = node.Narrative.Gates ?? new List<QuestPredicateInvocation>();
			NormalizePredicateList(node.Narrative.Gates);
			node.Narrative.OnStarted = node.Narrative.OnStarted ?? new List<QuestCommandInvocation>();
			node.Narrative.Choices = node.Narrative.Choices ?? new List<QuestChoiceOption>();
			foreach (QuestChoiceOption choice in node.Narrative.Choices)
			{
				if (choice != null)
				{
					choice.Requirements = choice.Requirements ?? new List<QuestPredicateInvocation>();
					NormalizePredicateList(choice.Requirements);
					choice.Consequences = choice.Consequences ?? new List<QuestCommandInvocation>();
				}
			}
		}
		program.Runtime.ActivatedNodeIds = program.Runtime.ActivatedNodeIds ?? new List<string>();
		program.Runtime.CompletedNodeIds = program.Runtime.CompletedNodeIds ?? new List<string>();
		program.Runtime.ExecutedCommandKeys = program.Runtime.ExecutedCommandKeys ?? new List<string>();
		program.Runtime.PendingNarrativeNodeIds = program.Runtime.PendingNarrativeNodeIds ?? new List<string>();
	}

	private static void NormalizePredicateList(List<QuestPredicateInvocation> predicates)
	{
		if (predicates == null)
		{
			return;
		}
		foreach (QuestPredicateInvocation predicate in predicates)
		{
			NormalizePredicate(predicate);
		}
	}

	private static void NormalizePredicate(QuestPredicateInvocation predicate)
	{
		if (predicate == null)
		{
			return;
		}
		predicate.Params = predicate.Params ?? new Dictionary<string, string>();
		predicate.Children = predicate.Children ?? new List<QuestPredicateInvocation>();
		foreach (QuestPredicateInvocation child in predicate.Children)
		{
			NormalizePredicate(child);
		}
	}
}
