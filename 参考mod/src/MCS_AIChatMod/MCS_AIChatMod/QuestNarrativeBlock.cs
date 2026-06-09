using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class QuestNarrativeBlock
{
	public string NarrativeId;

	public List<AIQuestStoryLine> Lines = new List<AIQuestStoryLine>();

	public List<QuestPredicateInvocation> Gates = new List<QuestPredicateInvocation>();

	public List<QuestCommandInvocation> OnStarted = new List<QuestCommandInvocation>();

	public List<QuestChoiceOption> Choices = new List<QuestChoiceOption>();
}
