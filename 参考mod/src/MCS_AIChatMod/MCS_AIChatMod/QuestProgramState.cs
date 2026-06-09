using System;
using System.Collections.Generic;

namespace MCS_AIChatMod;

[Serializable]
public class QuestProgramState
{
	public int FormatVersion = 1;

	public DateTime SavedAt;

	public List<QuestProgram> Programs = new List<QuestProgram>();
}
