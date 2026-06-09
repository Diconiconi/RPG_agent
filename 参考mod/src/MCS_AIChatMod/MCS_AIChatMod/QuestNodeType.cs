using System;

namespace MCS_AIChatMod;

[Serializable]
public enum QuestNodeType
{
	Objective,
	Narrative,
	Choice,
	Combat,
	WorldState,
	Terminal
}
