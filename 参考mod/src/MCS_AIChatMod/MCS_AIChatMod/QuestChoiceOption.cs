using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class QuestChoiceOption
{
	public string ChoiceId;

	public string Text;

	public List<QuestPredicateInvocation> Requirements = new List<QuestPredicateInvocation>();

	public List<QuestCommandInvocation> Consequences = new List<QuestCommandInvocation>();

	public string NextNodeId;
}
