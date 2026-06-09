using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MCS_AIChatMod;

public static class QuestProgramParser
{
	private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
	{
		NullValueHandling = NullValueHandling.Ignore
	};

	public static bool TryParseAndValidate(string json, out QuestProgram program, out string error)
	{
		program = null;
		error = null;
		if (string.IsNullOrWhiteSpace(json))
		{
			error = "QuestProgram JSON 为空";
			return false;
		}
		if (!TryExtractQuestProgramJson(json, out var normalizedJson, out error))
		{
			return false;
		}
		try
		{
			program = JsonConvert.DeserializeObject<QuestProgram>(normalizedJson, Settings);
		}
		catch (Exception ex)
		{
			error = "QuestProgram 解析失败: " + ex.Message;
			return false;
		}
		if (program == null)
		{
			error = "QuestProgram 解析结果为空";
			return false;
		}
		program.TrackType = QuestTrackType.Mainline;
		program.ProgramId = (string.IsNullOrWhiteSpace(program.ProgramId) ? Guid.NewGuid().ToString("N") : program.ProgramId.Trim());
		program.WorldContract = program.WorldContract ?? new QuestWorldContract();
		program.Nodes = program.Nodes ?? new List<QuestNode>();
		program.Runtime = program.Runtime ?? new QuestRuntimeState();
		program.WorldContract.SelectedPredicates = program.WorldContract.SelectedPredicates ?? new List<string>();
		program.WorldContract.SelectedCommands = program.WorldContract.SelectedCommands ?? new List<string>();
		if (program.Nodes.Count == 0)
		{
			error = "QuestProgram 至少需要一个 node";
			program = null;
			return false;
		}
		if (program.WorldContract.SelectedPredicates.Count == 0)
		{
			error = "QuestProgram.worldContract.selectedPredicates 不能为空";
			program = null;
			return false;
		}
		HashSet<string> usedPredicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> usedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		HashSet<string> nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (QuestNode node in program.Nodes)
		{
			if (node == null)
			{
				error = "QuestProgram 包含空 node";
				program = null;
				return false;
			}
			if (string.IsNullOrWhiteSpace(node.NodeId))
			{
				error = "存在缺少 nodeId 的节点: " + (node.Title ?? "<untitled>");
				program = null;
				return false;
			}
			node.NodeId = node.NodeId.Trim();
			if (!nodeIds.Add(node.NodeId))
			{
				error = "QuestProgram 中存在重复 nodeId: " + node.NodeId;
				program = null;
				return false;
			}
		}
		if (!ValidateCommands(program.OnAccepted, program, "program.onAccepted", usedCommands, out error) || !ValidateCommands(program.OnCompleted, program, "program.onCompleted", usedCommands, out error) || !ValidateCommands(program.OnFailed, program, "program.onFailed", usedCommands, out error))
		{
			program = null;
			return false;
		}
		foreach (QuestNode node2 in program.Nodes)
		{
			node2.ActivationPredicates = node2.ActivationPredicates ?? new List<QuestPredicateInvocation>();
			node2.CompletionPredicates = node2.CompletionPredicates ?? new List<QuestPredicateInvocation>();
			node2.OnActivated = node2.OnActivated ?? new List<QuestCommandInvocation>();
			node2.OnCompleted = node2.OnCompleted ?? new List<QuestCommandInvocation>();
			if (node2.WorldControl != null)
			{
				node2.WorldControl.SceneId = node2.WorldControl.SceneId?.Trim();
				node2.WorldControl.NpcRequirements = node2.WorldControl.NpcRequirements ?? new List<QuestNpcPresenceRequirement>();
				if (node2.WorldControl.NpcRequirements.Count > 0 && string.IsNullOrWhiteSpace(node2.WorldControl.SceneId))
				{
					error = "节点 " + node2.NodeId + " 的 worldControl 缺少 sceneId";
					program = null;
					return false;
				}
				for (int i = 0; i < node2.WorldControl.NpcRequirements.Count; i++)
				{
					QuestNpcPresenceRequirement requirement = node2.WorldControl.NpcRequirements[i];
					if (requirement == null)
					{
						error = $"节点 {node2.NodeId} 的 worldControl.npcRequirements[{i}] 为空";
						program = null;
						return false;
					}
					if (requirement.NpcId <= 0)
					{
						error = $"节点 {node2.NodeId} 的 worldControl.npcRequirements[{i}] 缺少合法 npcId";
						program = null;
						return false;
					}
				}
			}
			node2.NextNodeId = node2.NextNodeId?.Trim();
			if (!ValidatePredicates(node2.ActivationPredicates, program, node2.NodeId + ".activationPredicates", usedPredicates, out error) || !ValidatePredicates(node2.CompletionPredicates, program, node2.NodeId + ".completionPredicates", usedPredicates, out error) || !ValidateCommands(node2.OnActivated, program, node2.NodeId + ".onActivated", usedCommands, out error) || !ValidateCommands(node2.OnCompleted, program, node2.NodeId + ".onCompleted", usedCommands, out error))
			{
				program = null;
				return false;
			}
			if (node2.Narrative != null)
			{
				node2.Narrative.Gates = node2.Narrative.Gates ?? new List<QuestPredicateInvocation>();
				node2.Narrative.OnStarted = node2.Narrative.OnStarted ?? new List<QuestCommandInvocation>();
				node2.Narrative.Choices = node2.Narrative.Choices ?? new List<QuestChoiceOption>();
				if (!ValidatePredicates(node2.Narrative.Gates, program, node2.NodeId + ".narrative.gates", usedPredicates, out error) || !ValidateCommands(node2.Narrative.OnStarted, program, node2.NodeId + ".narrative.onStarted", usedCommands, out error))
				{
					program = null;
					return false;
				}
				foreach (QuestChoiceOption choice in node2.Narrative.Choices)
				{
					if (choice == null)
					{
						error = "节点 " + node2.NodeId + " 存在空 choice";
						program = null;
						return false;
					}
					choice.Requirements = choice.Requirements ?? new List<QuestPredicateInvocation>();
					choice.Consequences = choice.Consequences ?? new List<QuestCommandInvocation>();
					if (!ValidatePredicates(choice.Requirements, program, node2.NodeId + ".choice." + choice.ChoiceId + ".requirements", usedPredicates, out error) || !ValidateCommands(choice.Consequences, program, node2.NodeId + ".choice." + choice.ChoiceId + ".consequences", usedCommands, out error))
					{
						program = null;
						return false;
					}
				}
			}
			if (!ValidateNodeStructure(node2, nodeIds, out error))
			{
				program = null;
				return false;
			}
		}
		if (!ValidateSelectedSetCoverage(program, usedPredicates, usedCommands, out error))
		{
			program = null;
			return false;
		}
		return true;
	}

	private static bool ValidateNodeStructure(QuestNode node, HashSet<string> nodeIds, out string error)
	{
		error = null;
		if (node == null)
		{
			error = "QuestProgram 包含空 node";
			return false;
		}
		bool hasDirectNext = !string.IsNullOrWhiteSpace(node.NextNodeId);
		if (hasDirectNext && !nodeIds.Contains(node.NextNodeId))
		{
			error = "节点 " + node.NodeId + " 的 nextNodeId 指向了不存在的节点: " + node.NextNodeId;
			return false;
		}
		switch (node.Type)
		{
		case QuestNodeType.Objective:
		case QuestNodeType.Combat:
		case QuestNodeType.WorldState:
			if (node.CompletionPredicates == null || node.CompletionPredicates.Count == 0)
			{
				error = $"节点 {node.NodeId} 的 {node.Type} 类型必须提供 completionPredicates";
				return false;
			}
			if (!hasDirectNext)
			{
				error = $"节点 {node.NodeId} 的 {node.Type} 类型必须提供 nextNodeId";
				return false;
			}
			return true;
		case QuestNodeType.Narrative:
			if (!hasDirectNext)
			{
				error = "节点 " + node.NodeId + " 的 Narrative 类型必须提供 nextNodeId";
				return false;
			}
			return true;
		case QuestNodeType.Choice:
			if (node.Narrative?.Choices == null || node.Narrative.Choices.Count == 0)
			{
				error = "节点 " + node.NodeId + " 的 Choice 类型必须在 narrative.choices 中提供至少一个选项";
				return false;
			}
			foreach (QuestChoiceOption choice in node.Narrative.Choices)
			{
				choice.NextNodeId = choice.NextNodeId?.Trim();
				if (!string.IsNullOrWhiteSpace(choice.NextNodeId))
				{
					if (!nodeIds.Contains(choice.NextNodeId))
					{
						error = "节点 " + node.NodeId + " 的 choice " + choice.ChoiceId + " 指向了不存在的 nextNodeId: " + choice.NextNodeId;
						return false;
					}
				}
				else if (!hasDirectNext)
				{
					error = "节点 " + node.NodeId + " 的 Choice 类型必须提供 nextNodeId，或为每个 choice 单独提供 nextNodeId";
					return false;
				}
			}
			return true;
		case QuestNodeType.Terminal:
			return true;
		default:
			return true;
		}
	}

	internal static bool TryParseAndValidateForTest(string json, out QuestProgram program, out string error)
	{
		return TryParseAndValidate(json, out program, out error);
	}

	private static bool TryExtractQuestProgramJson(string text, out string json, out string error)
	{
		json = null;
		error = null;
		string trimmed = text?.Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
		{
			error = "QuestProgram JSON 为空";
			return false;
		}
		if (trimmed.StartsWith("```", StringComparison.Ordinal))
		{
			int firstNewLine = trimmed.IndexOf('\n');
			if (firstNewLine < 0)
			{
				error = "QuestProgram 缺少完整 JSON 对象";
				return false;
			}
			trimmed = trimmed.Substring(firstNewLine + 1).Trim();
			int closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
			if (closingFence >= 0)
			{
				trimmed = trimmed.Substring(0, closingFence).Trim();
			}
		}
		int start = trimmed.IndexOf('{');
		if (start < 0)
		{
			error = "QuestProgram 缺少完整 JSON 对象";
			return false;
		}
		int depth = 0;
		bool inString = false;
		bool escaped = false;
		for (int i = start; i < trimmed.Length; i++)
		{
			char ch = trimmed[i];
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
					json = trimmed.Substring(start, i - start + 1);
					return true;
				}
				break;
			}
		}
		error = "QuestProgram 缺少完整 JSON 对象";
		return false;
	}

	private static bool ValidatePredicates(IEnumerable<QuestPredicateInvocation> predicates, QuestProgram program, string path, HashSet<string> usedPredicates, out string error)
	{
		error = null;
		if (predicates == null)
		{
			return true;
		}
		int index = 0;
		foreach (QuestPredicateInvocation predicate in predicates)
		{
			if (predicate == null)
			{
				error = $"路径 {path}[{index}] 存在空 predicate";
				return false;
			}
			QuestCapabilityDescriptor descriptor = QuestCapabilityRegistry.GetPredicateDescriptor(predicate.Opcode);
			if (descriptor == null)
			{
				error = string.Format("路径 {0}[{1}] 使用了未知 predicate opcode: {2}", path, index, predicate.Opcode ?? "<null>");
				return false;
			}
			if (!program.WorldContract.SelectedPredicates.Any((string opcode) => string.Equals(opcode, descriptor.Opcode, StringComparison.OrdinalIgnoreCase)))
			{
				error = $"路径 {path}[{index}] 的 predicate {descriptor.Opcode} 不在 worldContract.selectedPredicates 中";
				return false;
			}
			if (!descriptor.AllowedTracks.Contains(program.TrackType))
			{
				error = $"路径 {path}[{index}] 的 predicate {descriptor.Opcode} 不允许用于轨道 {program.TrackType}";
				return false;
			}
			predicate.Children = predicate.Children ?? new List<QuestPredicateInvocation>();
			if (string.Equals(descriptor.Opcode, "logic.all", StringComparison.OrdinalIgnoreCase) || string.Equals(descriptor.Opcode, "logic.any", StringComparison.OrdinalIgnoreCase) || string.Equals(descriptor.Opcode, "logic.not", StringComparison.OrdinalIgnoreCase))
			{
				if (predicate.Params != null && predicate.Params.ContainsKey("children"))
				{
					error = $"路径 {path}[{index}] 的 predicate {descriptor.Opcode} 必须使用顶层 children，不能写在 params.children 中";
					return false;
				}
				if (predicate.Children.Count == 0)
				{
					error = $"路径 {path}[{index}] 的 predicate {descriptor.Opcode} 必须至少包含一个 child predicate";
					return false;
				}
				if (!ValidatePredicates(predicate.Children, program, $"{path}[{index}].children", usedPredicates, out error))
				{
					return false;
				}
			}
			else
			{
				if (!ValidateParams(predicate.Params, descriptor, path, index, out error))
				{
					return false;
				}
				if (predicate.Children.Count > 0)
				{
					error = $"路径 {path}[{index}] 的 predicate {descriptor.Opcode} 不允许携带 children";
					return false;
				}
			}
			usedPredicates.Add(descriptor.Opcode);
			index++;
		}
		return true;
	}

	private static bool ValidateCommands(IEnumerable<QuestCommandInvocation> commands, QuestProgram program, string path, HashSet<string> usedCommands, out string error)
	{
		error = null;
		if (commands == null)
		{
			return true;
		}
		int index = 0;
		foreach (QuestCommandInvocation command in commands)
		{
			if (command == null)
			{
				error = $"路径 {path}[{index}] 存在空 command";
				return false;
			}
			QuestCapabilityDescriptor descriptor = QuestCapabilityRegistry.GetCommandDescriptor(command.Opcode);
			if (descriptor == null)
			{
				error = string.Format("路径 {0}[{1}] 使用了未知 command opcode: {2}", path, index, command.Opcode ?? "<null>");
				return false;
			}
			if (!program.WorldContract.SelectedCommands.Any((string opcode) => string.Equals(opcode, descriptor.Opcode, StringComparison.OrdinalIgnoreCase)))
			{
				error = $"路径 {path}[{index}] 的 command {descriptor.Opcode} 不在 worldContract.selectedCommands 中";
				return false;
			}
			if (!descriptor.AllowedTracks.Contains(program.TrackType))
			{
				error = $"路径 {path}[{index}] 的 command {descriptor.Opcode} 不允许用于轨道 {program.TrackType}";
				return false;
			}
			if (!ValidateParams(command.Params, descriptor, path, index, out error))
			{
				return false;
			}
			if (descriptor.RequiresWorldContract && program.WorldContract == null)
			{
				error = $"路径 {path}[{index}] 的 command {descriptor.Opcode} 缺少 worldContract";
				return false;
			}
			usedCommands.Add(descriptor.Opcode);
			index++;
		}
		return true;
	}

	private static bool ValidateSelectedSetCoverage(QuestProgram program, HashSet<string> usedPredicates, HashSet<string> usedCommands, out string error)
	{
		error = null;
		foreach (string opcode in program.WorldContract.SelectedPredicates)
		{
			if (!usedPredicates.Contains(opcode))
			{
				error = "worldContract.selectedPredicates 中的 " + opcode + " 没有在 QuestProgram 中实际出现";
				return false;
			}
		}
		foreach (string opcode2 in program.WorldContract.SelectedCommands)
		{
			if (!usedCommands.Contains(opcode2))
			{
				error = "worldContract.selectedCommands 中的 " + opcode2 + " 没有在 QuestProgram 中实际出现";
				return false;
			}
		}
		return true;
	}

	private static bool ValidateParams(Dictionary<string, string> parameters, QuestCapabilityDescriptor descriptor, string path, int index, out string error)
	{
		error = null;
		Dictionary<string, string> actual = parameters ?? new Dictionary<string, string>();
		foreach (KeyValuePair<string, string> requirement in descriptor.ParameterSchema)
		{
			if (!actual.TryGetValue(requirement.Key, out var value) || string.IsNullOrWhiteSpace(value))
			{
				error = $"路径 {path}[{index}] 的 {descriptor.Opcode} 缺少参数 {requirement.Key}";
				return false;
			}
		}
		return true;
	}
}
