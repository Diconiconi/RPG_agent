using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MCS_AIChatMod;

[Serializable]
public class QuestNode
{
	public string NodeId;

	[JsonConverter(typeof(StringEnumConverter))]
	public QuestNodeType Type = QuestNodeType.Objective;

	public string Title;

	public string Summary;

	public List<QuestPredicateInvocation> ActivationPredicates = new List<QuestPredicateInvocation>();

	public List<QuestPredicateInvocation> CompletionPredicates = new List<QuestPredicateInvocation>();

	public List<QuestCommandInvocation> OnActivated = new List<QuestCommandInvocation>();

	public List<QuestCommandInvocation> OnCompleted = new List<QuestCommandInvocation>();

	public QuestNarrativeBlock Narrative;

	public string NextNodeId;

	public QuestNodeWorldControl WorldControl;
}
