using System;

namespace MCS_AIChatMod;

[Serializable]
public class QuestProgramLoadResult
{
	public QuestProgramState State = new QuestProgramState();

	public bool WasLegacyQuestDataWiped;

	public string LegacyFormatReason;
}
