namespace MCS_AIChatMod;

public sealed class QuestNarrativeDirector
{
	public bool CanStartNarrative(QuestProgram program, QuestNode node)
	{
		return program != null && node != null && node.Narrative != null;
	}
}
